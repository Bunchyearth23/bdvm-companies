using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace BDVM.Domain;

public enum CompanyLiquidationState { Succeeded, Rejected, ReconcileRequired }

public interface ICompanyContractCancellationPort
{
    WorldOwnershipOutcome Cancel(string operationId, string companyId);
    WorldOwnershipOutcome Inspect(string companyId);
}

public sealed class NoActiveCompanyContractsPort : ICompanyContractCancellationPort
{
    public WorldOwnershipOutcome Cancel(string operationId, string companyId) => WorldOwnershipOutcome.Applied;
    public WorldOwnershipOutcome Inspect(string companyId) => WorldOwnershipOutcome.Applied;
}

public interface ICompanyLiquidationCheckpointPort
{
    bool TryCheckpoint(CompanyLiquidationRecord record, string phase);
}

public sealed class NoOpCompanyLiquidationCheckpointPort : ICompanyLiquidationCheckpointPort
{
    public bool TryCheckpoint(CompanyLiquidationRecord record, string phase) => true;
}

public sealed class CompositeCompanyContractCancellationPort : ICompanyContractCancellationPort
{
    private readonly IReadOnlyList<ICompanyContractCancellationPort> ports;
    public CompositeCompanyContractCancellationPort(params ICompanyContractCancellationPort[] ports) { this.ports = (ports ?? Array.Empty<ICompanyContractCancellationPort>()).Where(x => x != null).ToArray(); }
    public WorldOwnershipOutcome Cancel(string operationId, string companyId)
    {
        var unknown = false;
        for (var index = 0; index < ports.Count; index++)
        {
            var outcome = ports[index].Cancel(operationId + ":" + index, companyId);
            if (outcome == WorldOwnershipOutcome.NotApplied) return outcome;
            if (outcome == WorldOwnershipOutcome.Unknown) unknown = true;
        }
        return unknown ? WorldOwnershipOutcome.Unknown : WorldOwnershipOutcome.Applied;
    }
    public WorldOwnershipOutcome Inspect(string companyId)
    {
        var outcomes = ports.Select(x => x.Inspect(companyId)).ToArray();
        return outcomes.Any(x => x == WorldOwnershipOutcome.NotApplied) ? WorldOwnershipOutcome.NotApplied : outcomes.Any(x => x == WorldOwnershipOutcome.Unknown) ? WorldOwnershipOutcome.Unknown : WorldOwnershipOutcome.Applied;
    }
}

[DataContract]
public sealed class CompanyLiquidationRecord
{
    [DataMember(Name = "commandId", Order = 1)] public string CommandId { get; set; } = "";
    [DataMember(Name = "fingerprint", Order = 2)] public string Fingerprint { get; set; } = "";
    [DataMember(Name = "requesterId", Order = 3)] public string RequesterId { get; set; } = "";
    [DataMember(Name = "companyId", Order = 4)] public string CompanyId { get; set; } = "";
    [DataMember(Name = "beneficiaryIds", Order = 5)] public List<string> BeneficiaryIds { get; set; } = new List<string>();
    [DataMember(Name = "assetIds", Order = 6)] public List<string> AssetIds { get; set; } = new List<string>();
    [DataMember(Name = "debts", Order = 7)] public long Debts { get; set; }
    [DataMember(Name = "penalties", Order = 8)] public long Penalties { get; set; }
    [DataMember(Name = "state", Order = 9)] public CompanyLiquidationState State { get; set; }
    [DataMember(Name = "resultCode", Order = 10)] public string ResultCode { get; set; } = "";
    [DataMember(Name = "detail", Order = 11)] public string Detail { get; set; } = "";
    [DataMember(Name = "contractsCancelled", Order = 12)] public bool ContractsCancelled { get; set; }
    [DataMember(Name = "ownershipCommitted", Order = 13)] public bool OwnershipCommitted { get; set; }
    [DataMember(Name = "economyCommitted", Order = 14)] public bool EconomyCommitted { get; set; }
}

public sealed class CompanyLiquidationEngine
{
    private readonly object gate = new object();
    private readonly VehicleAcquisitionSnapshot state;
    private readonly INetworkRoleDetector authority;
    private readonly IAssetReleaseGuard releaseGuard;
    private readonly IExistingVehicleOwnershipAdapter world;
    private readonly ICompanyContractCancellationPort contracts;
    private readonly ICompanyLiquidationCheckpointPort checkpoint;

    public CompanyLiquidationEngine(VehicleAcquisitionSnapshot state, INetworkRoleDetector authority, IAssetReleaseGuard releaseGuard,
        IExistingVehicleOwnershipAdapter world, ICompanyContractCancellationPort contracts, ICompanyLiquidationCheckpointPort? checkpoint = null)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
        this.releaseGuard = releaseGuard ?? throw new ArgumentNullException(nameof(releaseGuard));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        this.checkpoint = checkpoint ?? new NoOpCompanyLiquidationCheckpointPort();
        VehicleAcquisitionPersistence.Validate(state);
    }

    public CompanyLiquidationRecord Dissolve(string commandId, string requesterId, string companyId, long debts, long penalties)
    {
        lock (gate)
        {
            if (string.IsNullOrWhiteSpace(commandId) || string.IsNullOrWhiteSpace(requesterId) || string.IsNullOrWhiteSpace(companyId)) throw new ArgumentException("Complete liquidation identity is required.");
            if (debts < 0 || penalties < 0 || debts > long.MaxValue - penalties) throw new ArgumentOutOfRangeException(nameof(debts));
            var fingerprint = string.Join("|", requesterId, companyId, debts, penalties);
            var known = state.CompanyLiquidations.SingleOrDefault(x => x.CommandId == commandId);
            if (known != null)
            {
                if (known.Fingerprint != fingerprint) throw new InvalidOperationException("A liquidation command ID cannot be reused with another payload.");
                return known;
            }
            if (!NetworkAuthorityPolicy.CanExecuteEconomy(authority.Detect(), out _)) return AddRejected(commandId, fingerprint, requesterId, companyId, debts, penalties, "host-authority-required");
            var company = state.Economy.Companies.SingleOrDefault(x => x.CompanyId == companyId);
            if (company == null) return AddRejected(commandId, fingerprint, requesterId, companyId, debts, penalties, "company-not-found");
            if (!CanDissolve(company, requesterId)) return AddRejected(commandId, fingerprint, requesterId, companyId, debts, penalties, "dissolve-permission-denied");
            var assetIds = state.Ownership.Where(x => x.Owner.Kind == AssetOwnerKind.Company && x.Owner.OwnerId == companyId).Select(x => x.AssetId).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (state.OperatingCosts.Any(x => assetIds.Contains(x.AssetId) && (x.State == OperatingCostState.Open || x.ExternalSettlement == ExternalSettlementState.Pending || x.ExternalSettlement == ExternalSettlementState.Conflict)))
                return AddRejected(commandId, fingerprint, requesterId, companyId, debts, penalties, "operating-cost-session-active");
            var record = new CompanyLiquidationRecord
            {
                CommandId = commandId, Fingerprint = fingerprint, RequesterId = requesterId, CompanyId = companyId,
                BeneficiaryIds = company.Members.OrderBy(x => x, StringComparer.Ordinal).ToList(), AssetIds = assetIds,
                Debts = debts, Penalties = penalties, State = CompanyLiquidationState.ReconcileRequired, ResultCode = "prepared"
            };
            state.CompanyLiquidations.Add(record);
            if (!TryCheckpoint(record, "prepared")) return Pending(record, "checkpoint-prepared-failed");
            var inspections = InspectAll(assetIds);
            record.Detail = string.Join("|", inspections.Select(x => x.Key + ":" + x.Value.Detail));
            if (inspections.Values.Any(x => x.Status == AssetReleaseStatus.Blocked)) return Reject(record, "asset-not-releasable");
            if (inspections.Values.Any(x => x.Status == AssetReleaseStatus.Unknown)) return Pending(record, "release-state-unknown");
            if (!PreviewEconomy(record)) return Reject(record, "economy-preview-refused");
            var contractOutcome = contracts.Cancel(commandId + ":contracts", companyId);
            if (contractOutcome == WorldOwnershipOutcome.NotApplied) return Reject(record, "contract-cancellation-refused");
            if (contractOutcome == WorldOwnershipOutcome.Unknown) return Pending(record, "contract-cancellation-unknown");
            record.ContractsCancelled = true;
            if (!TryCheckpoint(record, "contracts-cancelled")) return Pending(record, "checkpoint-contracts-failed");
            foreach (var assetId in assetIds)
            {
                var asset = state.Assets.Assets.Single(x => x.AssetId == assetId);
                var outcome = world.ApplyOwner(commandId + ":merchant:" + assetId, asset.GameLink.Value!, AssetOwnerRef.Merchant("runtime-market"));
                if (outcome != WorldOwnershipOutcome.Applied) return Pending(record, outcome == WorldOwnershipOutcome.Unknown ? "world-owner-unknown" : "world-partial-transfer");
                if (!TryCheckpoint(record, "world-owner:" + assetId)) return Pending(record, "checkpoint-world-owner-failed");
            }
            return Commit(record);
        }
    }

    public CompanyLiquidationRecord Reconcile(string commandId)
    {
        lock (gate)
        {
            var record = state.CompanyLiquidations.Single(x => x.CommandId == commandId);
            if (record.State != CompanyLiquidationState.ReconcileRequired) return record;
            if (!record.ContractsCancelled)
            {
                var contractState = contracts.Inspect(record.CompanyId);
                if (contractState == WorldOwnershipOutcome.Unknown) return Pending(record, "contract-cancellation-unknown");
                if (contractState == WorldOwnershipOutcome.NotApplied)
                {
                    contractState = contracts.Cancel(record.CommandId + ":contracts", record.CompanyId);
                    if (contractState != WorldOwnershipOutcome.Applied) return Pending(record, "contract-cancellation-pending");
                }
                record.ContractsCancelled = true;
                if (!TryCheckpoint(record, "contracts-reconciled")) return Pending(record, "checkpoint-contracts-failed");
            }
            if (!record.OwnershipCommitted)
            {
                foreach (var id in record.AssetIds)
                {
                    var link = state.Assets.Assets.Single(x => x.AssetId == id).GameLink.Value!;
                    var outcome = world.InspectOwner(link, AssetOwnerRef.Merchant("runtime-market"));
                    if (outcome == WorldOwnershipOutcome.NotApplied) outcome = world.ApplyOwner(record.CommandId + ":merchant:" + id, link, AssetOwnerRef.Merchant("runtime-market"));
                    if (outcome != WorldOwnershipOutcome.Applied) return Pending(record, "world-partial-or-unknown");
                    if (!TryCheckpoint(record, "world-owner-reconciled:" + id)) return Pending(record, "checkpoint-world-owner-failed");
                }
            }
            return Commit(record);
        }
    }

    private CompanyLiquidationRecord Commit(CompanyLiquidationRecord record)
    {
        if (!record.OwnershipCommitted)
        {
            foreach (var id in record.AssetIds)
            {
                var ownership = state.Ownership.Single(x => x.AssetId == id); ownership.Owner = AssetOwnerRef.Merchant("runtime-market"); ownership.Version++;
                var fleet = state.Fleet.SingleOrDefault(x => x.AssetId == id); if (fleet != null) { fleet.Operator = null; fleet.OperationalState = FleetOperationalState.Stored; fleet.Version++; }
            }
            record.OwnershipCommitted = true;
            if (!TryCheckpoint(record, "ownership-committed")) return Pending(record, "checkpoint-ownership-failed");
        }
        if (!record.EconomyCommitted)
        {
            if (state.Economy.Companies.Any(x => x.CompanyId == record.CompanyId))
            {
                var company = state.Economy.Companies.Single(x => x.CompanyId == record.CompanyId);
                var result = new CompanyEconomyEngine(state.Economy).Dissolve(new EconomyCommand
                {
                    CommandId = record.CommandId, RequesterId = record.RequesterId, CompanyId = record.CompanyId,
                    ExpectedVersions = new Dictionary<string, long> { ["company:" + record.CompanyId] = company.Version }
                }, LiquidationAssets(record.AssetIds), record.Debts, record.Penalties);
                if (result.State != CommandState.Succeeded) return Pending(record, "economy-commit-refused:" + result.ResultCode);
            }
            record.EconomyCommitted = true;
            if (!TryCheckpoint(record, "economy-committed")) return Pending(record, "checkpoint-economy-failed");
        }
        record.State = CompanyLiquidationState.Succeeded; record.ResultCode = "dissolved";
        if (!TryCheckpoint(record, "completed")) return Pending(record, "checkpoint-completion-failed");
        return record;
    }

    private bool PreviewEconomy(CompanyLiquidationRecord record)
    {
        var clone = CompanyEconomyPersistence.Deserialize(CompanyEconomyPersistence.Serialize(state.Economy), state.Economy.CheckpointId);
        var company = clone.Companies.Single(x => x.CompanyId == record.CompanyId);
        var result = new CompanyEconomyEngine(clone).Dissolve(new EconomyCommand { CommandId = record.CommandId, RequesterId = record.RequesterId, CompanyId = record.CompanyId, ExpectedVersions = new Dictionary<string, long> { ["company:" + record.CompanyId] = company.Version } }, LiquidationAssets(record.AssetIds), record.Debts, record.Penalties);
        return result.State == CommandState.Succeeded;
    }

    private IReadOnlyList<LiquidationAsset> LiquidationAssets(IEnumerable<string> assetIds) => assetIds.Select(id =>
    {
        var acquisition = state.Acquisitions.LastOrDefault(x => x.AssetId == id && x.State == AcquisitionState.Succeeded);
        if (acquisition == null) throw new InvalidOperationException("A company asset lacks a frozen acquisition reference: " + id);
        return new LiquidationAsset { AssetId = id, ConfiguredModelValue = Math.Max(acquisition.ReferenceValue, acquisition.Price), DynamicMarketValue = acquisition.ReferenceSource == ReferenceValueSource.DynamicMarket ? (long?)Math.Max(acquisition.ReferenceValue, acquisition.Price) : null, Condition = acquisition.ObservedCondition };
    }).ToArray();

    private Dictionary<string, AssetReleaseInspection> InspectAll(IReadOnlyList<string> assetIds)
    {
        var links = assetIds.ToDictionary(id => id, id => state.Assets.Assets.Single(x => x.AssetId == id).GameLink.Value!, StringComparer.Ordinal);
        if (releaseGuard is IAssetBundleReleaseGuard bundleGuard)
        {
            var result = bundleGuard.InspectBundle(links.Values.ToArray());
            return links.ToDictionary(x => x.Key, x => result.TryGetValue(x.Value, out var inspection) ? inspection : new AssetReleaseInspection { Status = AssetReleaseStatus.Unknown, Detail = "inspection-result-missing" }, StringComparer.Ordinal);
        }
        return links.ToDictionary(x => x.Key, x => releaseGuard.Inspect(x.Value), StringComparer.Ordinal);
    }

    private static bool CanDissolve(CompanyState company, string playerId) => company.LeaderId == playerId || (company.DelegatedPermissions.TryGetValue(playerId, out var rights) && rights.Contains(CompanyPermission.Dissolve));
    private bool TryCheckpoint(CompanyLiquidationRecord record, string phase)
    {
        try { return checkpoint.TryCheckpoint(record, phase); }
        catch (Exception exception) { record.Detail = string.IsNullOrWhiteSpace(record.Detail) ? "checkpoint:" + phase + ":" + exception.GetType().Name : record.Detail + "|checkpoint:" + phase + ":" + exception.GetType().Name; return false; }
    }
    private CompanyLiquidationRecord AddRejected(string command, string fingerprint, string requester, string company, long debts, long penalties, string code)
    {
        var record = new CompanyLiquidationRecord { CommandId = command, Fingerprint = fingerprint, RequesterId = requester, CompanyId = company, Debts = debts, Penalties = penalties };
        state.CompanyLiquidations.Add(record); return Reject(record, code);
    }
    private static CompanyLiquidationRecord Reject(CompanyLiquidationRecord record, string code) { record.State = CompanyLiquidationState.Rejected; record.ResultCode = code; return record; }
    private static CompanyLiquidationRecord Pending(CompanyLiquidationRecord record, string code) { record.State = CompanyLiquidationState.ReconcileRequired; record.ResultCode = code; return record; }
}
