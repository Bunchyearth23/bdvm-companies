using System;
using System.Linq;

namespace BDVM.Domain;

/// <summary>Cancels non-physical company workflows before asset sale and final distribution.</summary>
public sealed class CompanyWorkflowCancellationPort : ICompanyContractCancellationPort
{
    private readonly object gate = new object();
    private readonly VehicleAcquisitionSnapshot state;
    private readonly INetworkRoleDetector authority;
    public CompanyWorkflowCancellationPort(VehicleAcquisitionSnapshot state, INetworkRoleDetector authority) { this.state = state ?? throw new ArgumentNullException(nameof(state)); this.authority = authority ?? throw new ArgumentNullException(nameof(authority)); VehicleAcquisitionPersistence.Validate(state); }

    public WorldOwnershipOutcome Cancel(string operationId, string companyId)
    {
        lock (gate)
        {
            if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out _)) return WorldOwnershipOutcome.NotApplied;
            if (string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(companyId)) return WorldOwnershipOutcome.NotApplied;
            var fingerprint = "company-workflows-cancelled:" + companyId;
            var known = state.Economy.History.SingleOrDefault(x => x.EventId == operationId);
            if (known != null) return known.Kind == "company-workflows-cancelled" && known.CompanyId == companyId && known.Fingerprint == fingerprint ? Inspect(companyId) : WorldOwnershipOutcome.NotApplied;
            if (HasUnknownPhysicalTransition(companyId)) return WorldOwnershipOutcome.Unknown;

            CancelTriage(companyId);
            CancelPassengers(companyId);
            CancelAssignments(companyId);
            CancelIndustrial(companyId);
            CancelInboundLeases(operationId, companyId);
            state.Economy.History.Add(new EconomicHistoryRecord { EventId = operationId, Kind = "company-workflows-cancelled", CompanyId = companyId, Fingerprint = fingerprint });
            return Inspect(companyId);
        }
    }

    public WorldOwnershipOutcome Inspect(string companyId)
    {
        lock (gate)
        {
            if (HasUnknownPhysicalTransition(companyId)) return WorldOwnershipOutcome.Unknown;
            return HasActiveWorkflow(companyId) ? WorldOwnershipOutcome.NotApplied : WorldOwnershipOutcome.Applied;
        }
    }

    private void CancelTriage(string companyId)
    {
        var companyAssignments = state.Assignments.Where(x => IsCompany(x.Operator, companyId)).Select(x => x.AssignmentId).ToHashSet(StringComparer.Ordinal);
        foreach (var plan in state.TriageAssistance.Plans.Where(x => companyAssignments.Contains(x.AssignmentId) && x.State == TriagePlanState.Planned)) { plan.State = TriagePlanState.Cancelled; plan.ResultCode = "company-liquidation"; plan.Version++; }
    }

    private void CancelPassengers(string companyId)
    {
        foreach (var contract in state.PassengerContracts.Where(x => IsCompany(x.Operator, companyId) && x.State != PassengerContractState.Completed && x.State != PassengerContractState.Cancelled))
        {
            if (!contract.DemandRestored) { var route = state.PassengerRoutes.Single(x => x.RouteId == contract.RouteId); route.DemandUnits = Math.Min(route.MaximumDemandUnits, route.DemandUnits + contract.BookedPassengers); route.Version++; contract.DemandRestored = true; }
            contract.State = PassengerContractState.Cancelled; contract.ResultCode = "company-liquidation"; contract.Version++;
        }
    }

    private void CancelAssignments(string companyId)
    {
        foreach (var assignment in state.Assignments.Where(x => IsCompany(x.Operator, companyId) && x.State != MissionAssignmentState.Completed && x.State != MissionAssignmentState.Cancelled))
        {
            assignment.State = MissionAssignmentState.Cancelled; assignment.ResultCode = "company-liquidation"; assignment.Version++;
            foreach (var id in assignment.AssetIds) { var fleet = state.Fleet.Single(x => x.AssetId == id); if (fleet.OperationalState == FleetOperationalState.Reserved || fleet.OperationalState == FleetOperationalState.InService) { fleet.OperationalState = FleetOperationalState.Available; fleet.Version++; } }
        }
    }

    private void CancelIndustrial(string companyId)
    {
        foreach (var contract in state.IndustrialContracts.Where(x => x.Beneficiary.Kind == AccountKind.Company && x.Beneficiary.OwnerId == companyId && x.State != IndustrialContractState.Completed && x.State != IndustrialContractState.Cancelled))
        {
            var remaining = contract.Quantity - contract.DeliveredQuantity;
            if (contract.State != IndustrialContractState.Offered) { var source = state.IndustrialStocks.Single(x => x.FacilityId == contract.OriginFacilityId && x.CargoId == contract.CargoId); var destination = state.IndustrialStocks.Single(x => x.FacilityId == contract.DestinationFacilityId && x.CargoId == contract.CargoId); source.ReservedOutbound -= remaining; source.Version++; destination.ReservedInbound -= remaining; destination.Version++; }
            contract.State = IndustrialContractState.Cancelled; contract.Version++;
        }
    }

    private void CancelInboundLeases(string operationId, string companyId)
    {
        foreach (var lease in state.Leases.Where(x => x.Lessee != null && IsCompany(x.Lessee, companyId) && x.State != LeaseState.Returned && x.State != LeaseState.Purchased && x.State != LeaseState.Cancelled))
        {
            var payer = lease.Payer ?? AccountRef.Company(companyId); var applied = Math.Min(lease.HeldDeposit, lease.OutstandingDebt); var refund = lease.HeldDeposit - applied; var cancelledDebt = lease.OutstandingDebt - applied; lease.HeldDeposit = 0; lease.OutstandingDebt = 0;
            var wallet = state.Economy.Wallets.Single(x => x.Account.Key == payer.Key); if (refund > 0) { wallet.Balance = checked(wallet.Balance + refund); wallet.Version++; AddLedger(operationId + ":lease-refund:" + lease.LeaseId, LedgerEntryKind.LeaseDepositRefund, null, wallet.Account, refund, "company-liquidation;lease=" + lease.LeaseId); }
            if (applied > 0) AddLedger(operationId + ":lease-debt:" + lease.LeaseId, LedgerEntryKind.DebtPayment, payer, null, applied, "company-liquidation;lease=" + lease.LeaseId);
            if (cancelledDebt > 0) state.Economy.History.Add(new EconomicHistoryRecord { EventId = operationId + ":lease-writeoff:" + lease.LeaseId, Kind = "lease-debt-written-off", CompanyId = companyId, Fingerprint = cancelledDebt.ToString() });
            foreach (var id in lease.AssetIds) { var fleet = state.Fleet.Single(x => x.AssetId == id); fleet.Operator = null; fleet.OperationalState = FleetOperationalState.Stored; fleet.Version++; }
            lease.State = LeaseState.Cancelled; lease.Version++;
        }
    }

    private bool HasUnknownPhysicalTransition(string companyId) =>
        state.Leases.Any(x => x.Lessee != null && IsCompany(x.Lessee, companyId) && x.State == LeaseState.PurchasePending) ||
        state.TriageAssistance.Plans.Any(x => x.State == TriagePlanState.ExecutionPending && state.Assignments.Any(a => a.AssignmentId == x.AssignmentId && IsCompany(a.Operator, companyId)));

    private bool HasActiveWorkflow(string companyId) =>
        state.Leases.Any(x => x.Lessee != null && IsCompany(x.Lessee, companyId) && x.State != LeaseState.Returned && x.State != LeaseState.Purchased && x.State != LeaseState.Cancelled) ||
        state.Assignments.Any(x => IsCompany(x.Operator, companyId) && x.State != MissionAssignmentState.Completed && x.State != MissionAssignmentState.Cancelled) ||
        state.PassengerContracts.Any(x => IsCompany(x.Operator, companyId) && x.State != PassengerContractState.Completed && x.State != PassengerContractState.Cancelled) ||
        state.IndustrialContracts.Any(x => x.Beneficiary.Kind == AccountKind.Company && x.Beneficiary.OwnerId == companyId && x.State != IndustrialContractState.Completed && x.State != IndustrialContractState.Cancelled) ||
        state.TriageAssistance.Plans.Any(x => x.State != TriagePlanState.Completed && x.State != TriagePlanState.Cancelled && x.State != TriagePlanState.Rejected && state.Assignments.Any(a => a.AssignmentId == x.AssignmentId && IsCompany(a.Operator, companyId)));

    private void AddLedger(string id, LedgerEntryKind kind, AccountRef? debit, AccountRef? credit, long amount, string detail) { if (amount <= 0 || state.Economy.Ledger.Any(x => x.EntryId == id)) return; state.Economy.Ledger.Add(new LedgerEntry { EntryId = id, CommandId = id, Kind = kind, Debit = debit, Credit = credit, Amount = amount, Detail = detail }); }
    private static bool IsCompany(AssetOwnerRef owner, string companyId) => owner.Kind == AssetOwnerKind.Company && owner.OwnerId == companyId;
}
