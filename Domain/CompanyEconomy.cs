using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BDVM.Domain;

public enum AccountKind { Player, Company }
public enum CompanyPermission { ManageMembers, ManagePermissions, ManageFunds, Dissolve, ManageFleet }
public enum MembershipPolicy { ApplicationWithApproval, InvitationOnly, Open }
public enum MembershipRequestKind { Application, Invitation }
public enum MembershipRequestState { Pending, Accepted, Rejected }
public enum CommandState { InProgress, Succeeded, Rejected }
public enum LedgerEntryKind { Contribution, Withdrawal, Salary, Reimbursement, MissionRevenue, VehiclePurchase, LiquidationSale, DebtPayment, PenaltyPayment, Distribution, LicenseCharge, LicenseDepositRefund, ExternalWalletSync, VehicleSale, OperatingCost, LeaseDeposit, LeaseDepositRefund, LeaseRent, LeaseDamage, LeasePurchase, IndustrialRevenue, OutboundLeaseRent, OutboundLeaseRecallFee, FinancingPrincipal, FinancingRepayment, FinancingGuarantee, FinancingGuaranteeRelease, FinancingGuaranteeApplied }
public enum ReferenceValueSource { DynamicMarket, ConfiguredModel }
public enum LicenseEconomicMechanism { Fee, Authorization, Guarantee, Insurance, ReviewCost }

[DataContract]
public sealed class AccountRef
{
    [DataMember(Name = "kind", Order = 1)] public AccountKind Kind { get; set; }
    [DataMember(Name = "ownerId", Order = 2)] public string OwnerId { get; set; } = "";
    public static AccountRef Player(string id) => new AccountRef { Kind = AccountKind.Player, OwnerId = id };
    public static AccountRef Company(string id) => new AccountRef { Kind = AccountKind.Company, OwnerId = id };
    public string Key => Kind + ":" + OwnerId;
}

[DataContract]
public sealed class Wallet
{
    [DataMember(Name = "account", Order = 1)] public AccountRef Account { get; set; } = new AccountRef();
    [DataMember(Name = "balance", Order = 2)] public long Balance { get; set; }
    [DataMember(Name = "version", Order = 3)] public long Version { get; set; }
}

[DataContract]
public sealed class PlayerEconomicState
{
    [DataMember(Name = "playerId", Order = 1)] public string PlayerId { get; set; } = "";
    [DataMember(Name = "companyId", Order = 2)] public string? CompanyId { get; set; }
    [DataMember(Name = "version", Order = 3)] public long Version { get; set; }
    [DataMember(Name = "activeOperation", Order = 4)] public bool ActiveOperation { get; set; }
}

[DataContract]
public sealed class CompanyState
{
    [DataMember(Name = "companyId", Order = 1)] public string CompanyId { get; set; } = "";
    [DataMember(Name = "name", Order = 2)] public string Name { get; set; } = "";
    [DataMember(Name = "leaderId", Order = 3)] public string LeaderId { get; set; } = "";
    [DataMember(Name = "members", Order = 4)] public List<string> Members { get; set; } = new List<string>();
    [DataMember(Name = "delegatedPermissions", Order = 5)] public Dictionary<string, List<CompanyPermission>> DelegatedPermissions { get; set; } = new Dictionary<string, List<CompanyPermission>>();
    [DataMember(Name = "membershipPolicy", Order = 6)] public MembershipPolicy MembershipPolicy { get; set; } = MembershipPolicy.ApplicationWithApproval;
    [DataMember(Name = "version", Order = 7)] public long Version { get; set; }
    [DataMember(Name = "liquidating", Order = 8)] public bool Liquidating { get; set; }
}

[DataContract]
public sealed class MembershipRequest
{
    [DataMember(Name = "requestId", Order = 1)] public string RequestId { get; set; } = "";
    [DataMember(Name = "companyId", Order = 2)] public string CompanyId { get; set; } = "";
    [DataMember(Name = "playerId", Order = 3)] public string PlayerId { get; set; } = "";
    [DataMember(Name = "kind", Order = 4)] public MembershipRequestKind Kind { get; set; }
    [DataMember(Name = "state", Order = 5)] public MembershipRequestState State { get; set; }
    [DataMember(Name = "version", Order = 6)] public long Version { get; set; }
    [DataMember(Name = "decidedBy", Order = 7)] public string? DecidedBy { get; set; }
}

[DataContract]
public sealed class EconomyCommand
{
    [DataMember(Name = "commandId", Order = 1)] public string CommandId { get; set; } = "";
    [DataMember(Name = "requesterId", Order = 2)] public string RequesterId { get; set; } = "";
    [DataMember(Name = "companyId", Order = 3)] public string? CompanyId { get; set; }
    [DataMember(Name = "expectedVersions", Order = 4)] public Dictionary<string, long> ExpectedVersions { get; set; } = new Dictionary<string, long>();
}

[DataContract]
public sealed class CommandRecord
{
    [DataMember(Name = "commandId", Order = 1)] public string CommandId { get; set; } = "";
    [DataMember(Name = "requesterId", Order = 2)] public string RequesterId { get; set; } = "";
    [DataMember(Name = "companyId", Order = 3)] public string? CompanyId { get; set; }
    [DataMember(Name = "state", Order = 4)] public CommandState State { get; set; }
    [DataMember(Name = "resultCode", Order = 5)] public string ResultCode { get; set; } = "";
    [DataMember(Name = "operationIds", Order = 6)] public List<string> OperationIds { get; set; } = new List<string>();
    [DataMember(Name = "fingerprint", Order = 7, EmitDefaultValue = false)] public string Fingerprint { get; set; } = "";
}

[DataContract]
public sealed class LedgerEntry
{
    [DataMember(Name = "entryId", Order = 1)] public string EntryId { get; set; } = "";
    [DataMember(Name = "commandId", Order = 2)] public string CommandId { get; set; } = "";
    [DataMember(Name = "kind", Order = 3)] public LedgerEntryKind Kind { get; set; }
    [DataMember(Name = "debit", Order = 4)] public AccountRef? Debit { get; set; }
    [DataMember(Name = "credit", Order = 5)] public AccountRef? Credit { get; set; }
    [DataMember(Name = "amount", Order = 6)] public long Amount { get; set; }
    [DataMember(Name = "detail", Order = 7)] public string Detail { get; set; } = "";
}

[DataContract]
public sealed class LiquidationAsset
{
    [DataMember(Name = "assetId", Order = 1)] public string AssetId { get; set; } = "";
    [DataMember(Name = "condition", Order = 2)] public decimal Condition { get; set; }
    [DataMember(Name = "dynamicMarketValue", Order = 3)] public long? DynamicMarketValue { get; set; }
    [DataMember(Name = "configuredModelValue", Order = 4)] public long ConfiguredModelValue { get; set; }
}

[DataContract]
public sealed class LiquidationSaleRecord
{
    [DataMember(Name = "assetId", Order = 1)] public string AssetId { get; set; } = "";
    [DataMember(Name = "referenceValue", Order = 2)] public long ReferenceValue { get; set; }
    [DataMember(Name = "source", Order = 3)] public ReferenceValueSource Source { get; set; }
    [DataMember(Name = "condition", Order = 4)] public decimal Condition { get; set; }
    [DataMember(Name = "rate", Order = 5)] public decimal Rate { get; set; }
    [DataMember(Name = "proceeds", Order = 6)] public long Proceeds { get; set; }
}

[DataContract]
public sealed class EconomicHistoryRecord
{
    [DataMember(Name = "eventId", Order = 1)] public string EventId { get; set; } = "";
    [DataMember(Name = "kind", Order = 2)] public string Kind { get; set; } = "";
    [DataMember(Name = "companyId", Order = 3)] public string CompanyId { get; set; } = "";
    [DataMember(Name = "actorIds", Order = 4)] public List<string> ActorIds { get; set; } = new List<string>();
    [DataMember(Name = "fingerprint", Order = 5)] public string Fingerprint { get; set; } = "";
}

[DataContract]
public sealed class LicenseEconomicRule
{
    [DataMember(Name = "stableLicenseId", Order = 1)] public string StableLicenseId { get; set; } = "";
    [DataMember(Name = "mechanism", Order = 2)] public LicenseEconomicMechanism Mechanism { get; set; }
    [DataMember(Name = "amount", Order = 3)] public long Amount { get; set; }
    [DataMember(Name = "vanillaProjectionRequired", Order = 4)] public bool VanillaProjectionRequired { get; set; }
    [DataMember(Name = "blocksGameplayWhenAbsent", Order = 5)] public bool BlocksGameplayWhenAbsent { get; set; }
}

[DataContract]
public sealed class CompanyEconomySnapshot
{
    public const string CurrentSchema = "bdvm.company-economy";
    public const int CurrentVersion = 2;
    [DataMember(Name = "schema", Order = 1)] public string Schema { get; set; } = CurrentSchema;
    [DataMember(Name = "schemaVersion", Order = 2)] public int SchemaVersion { get; set; } = CurrentVersion;
    [DataMember(Name = "checkpointId", Order = 3)] public string CheckpointId { get; set; } = "";
    [DataMember(Name = "players", Order = 4)] public List<PlayerEconomicState> Players { get; set; } = new List<PlayerEconomicState>();
    [DataMember(Name = "companies", Order = 5)] public List<CompanyState> Companies { get; set; } = new List<CompanyState>();
    [DataMember(Name = "wallets", Order = 6)] public List<Wallet> Wallets { get; set; } = new List<Wallet>();
    [DataMember(Name = "membershipRequests", Order = 7)] public List<MembershipRequest> MembershipRequests { get; set; } = new List<MembershipRequest>();
    [DataMember(Name = "commands", Order = 8)] public List<CommandRecord> Commands { get; set; } = new List<CommandRecord>();
    [DataMember(Name = "ledger", Order = 9)] public List<LedgerEntry> Ledger { get; set; } = new List<LedgerEntry>();
    [DataMember(Name = "history", Order = 10)] public List<EconomicHistoryRecord> History { get; set; } = new List<EconomicHistoryRecord>();
    [DataMember(Name = "liquidationSales", Order = 11)] public List<LiquidationSaleRecord> LiquidationSales { get; set; } = new List<LiquidationSaleRecord>();
    [DataMember(Name = "licenseRules", Order = 12)] public List<LicenseEconomicRule> LicenseRules { get; set; } = new List<LicenseEconomicRule>();
    [DataMember(Name = "licenseEconomy", Order = 13)] public LicenseEconomyState LicenseEconomy { get; set; } = new LicenseEconomyState();
}

public interface IPlayerWalletAdapter
{
    long ReadPersonalBalance(string playerId);
    void ApplyPersonalDelta(string operationId, string playerId, long delta);
}

public interface ICompanyCheckpointAdapter
{
    CompanyEconomySnapshot LoadFromCareerCheckpoint(string checkpointId);
    void SaveToCareerCheckpoint(string checkpointId, CompanyEconomySnapshot snapshot);
}

public interface ICompanyLiquidationWorldAdapter
{
    void CancelCompanyContracts(string operationId, string companyId);
    IReadOnlyList<LiquidationAsset> ReadAndSellCompanyAssets(string operationId, string companyId);
}

public sealed class CompanyEconomyEngine
{
    private readonly object gate = new object();
    public CompanyEconomySnapshot State { get; }
    public CompanyEconomyEngine(CompanyEconomySnapshot state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        CompanyEconomyPersistence.Validate(state);
    }

    public void EnsurePlayer(string playerId, long initialPersonalBalance = 0)
    {
        lock (gate)
        {
            RequireId(playerId, nameof(playerId));
            if (State.Players.Any(p => p.PlayerId == playerId)) return;
            State.Players.Add(new PlayerEconomicState { PlayerId = playerId });
            State.Wallets.Add(new Wallet { Account = AccountRef.Player(playerId), Balance = initialPersonalBalance });
        }
    }

    public CommandRecord CreateCompany(EconomyCommand command, string name)
    {
        return Execute(command, () =>
        {
            var player = Player(command.RequesterId);
            if (player.CompanyId != null) return "already-member";
            if (string.IsNullOrWhiteSpace(name)) return "invalid-name";
            if (State.Companies.Any(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))) return "name-collision";
            var id = string.IsNullOrWhiteSpace(command.CompanyId) ? Guid.NewGuid().ToString("N") : command.CompanyId!;
            var normalizedName = name.Trim().ToUpperInvariant();
            if (State.Companies.Any(c => c.CompanyId == id) || State.History.Any(h => h.CompanyId == id || (h.Kind == "company-created" && h.Fingerprint == normalizedName))) return "identity-collision";
            if (State.History.Any(h => h.Kind == "company-dissolved" && h.ActorIds.Contains(player.PlayerId) && h.Fingerprint != "cancelledDebt=0")) return "unresolved-anti-abuse-hold";
            var company = new CompanyState { CompanyId = id, Name = name.Trim(), LeaderId = player.PlayerId, Members = new List<string> { player.PlayerId }, Version = 1 };
            State.Companies.Add(company);
            State.Wallets.Add(new Wallet { Account = AccountRef.Company(id), Balance = 0 });
            player.CompanyId = id; player.Version++;
            State.History.Add(History("company-created", id, new[] { player.PlayerId }, normalizedName));
            return "created";
        }, "company-create|" + (name ?? "").Trim().ToUpperInvariant());
    }

    public CommandRecord Transfer(EconomyCommand command, AccountRef debit, AccountRef credit, long amount, LedgerEntryKind kind)
    {
        return Execute(command, () =>
        {
            if (amount <= 0 || debit == null || credit == null || debit.Key == credit.Key) return "invalid-transfer";
            if (!AllowedTransferKind(kind)) return "ambiguous-transfer-kind";
            var requester = Player(command.RequesterId);
            if (kind == LedgerEntryKind.Contribution &&
                (debit.Kind != AccountKind.Player || debit.OwnerId != requester.PlayerId ||
                 credit.Kind != AccountKind.Company || requester.CompanyId != credit.OwnerId))
                return "invalid-contribution-route";
            if (kind == LedgerEntryKind.Withdrawal &&
                (debit.Kind != AccountKind.Company || requester.CompanyId != debit.OwnerId ||
                 credit.Kind != AccountKind.Player || credit.OwnerId != requester.PlayerId))
                return "invalid-withdrawal-route";
            AuthorizeAccount(command.RequesterId, debit);
            var from = Wallet(debit); var to = Wallet(credit);
            CheckVersion(command, from.Account.Key, from.Version); CheckVersion(command, to.Account.Key, to.Version);
            var fingerprint = debit.Key + ">" + credit.Key;
            if ((kind == LedgerEntryKind.Salary || kind == LedgerEntryKind.Reimbursement) &&
                State.History.Any(h => h.Kind == "account-transfer" && h.Fingerprint == credit.Key + ">" + debit.Key))
                return "circular-transfer-blocked";
            if (from.Balance < amount) return "insufficient-funds";
            from.Balance -= amount; from.Version++; to.Balance += amount; to.Version++;
            AddEntry(command, kind, debit, credit, amount, kind.ToString());
            State.History.Add(History("account-transfer", command.CompanyId ?? "", new[] { command.RequesterId }, fingerprint));
            return "transferred";
        });
    }

    public CommandRecord SynchronizePersonalWallet(EconomyCommand command, long authoritativeBalance, string source)
    {
        return Execute(command, () =>
        {
            if (authoritativeBalance < 0) return "invalid-balance";
            var player = Player(command.RequesterId);
            var wallet = Wallet(AccountRef.Player(player.PlayerId));
            if (wallet.Balance == authoritativeBalance) return "wallet-current";
            var previous = wallet.Balance;
            wallet.Balance = authoritativeBalance;
            wallet.Version++;
            var amount = authoritativeBalance >= previous ? authoritativeBalance - previous : previous - authoritativeBalance;
            AddEntry(command, LedgerEntryKind.ExternalWalletSync,
                authoritativeBalance < previous ? wallet.Account : null,
                authoritativeBalance > previous ? wallet.Account : null,
                amount, "source=" + (source ?? "external") + ";previous=" + previous + ";current=" + authoritativeBalance);
            return "wallet-synchronized";
        });
    }

    public CommandRecord RouteMissionRevenue(EconomyCommand command, string operatorPlayerId, string? operatingCompanyId, long amount)
    {
        return Execute(command, () =>
        {
            if (amount <= 0) return "invalid-amount";
            var player = Player(operatorPlayerId);
            AccountRef target;
            if (operatingCompanyId == null) { if (player.CompanyId != null) return "company-context-required"; target = AccountRef.Player(player.PlayerId); }
            else { if (player.CompanyId != operatingCompanyId) return "invalid-operating-relation"; target = AccountRef.Company(operatingCompanyId); }
            var wallet = Wallet(target); wallet.Balance += amount; wallet.Version++;
            AddEntry(command, LedgerEntryKind.MissionRevenue, null, target, amount, operatingCompanyId == null ? "independent" : "company-operation");
            return "revenue-routed";
        });
    }

    public MembershipRequest RequestMembership(string requestId, string playerId, string companyId, MembershipRequestKind kind)
    {
        lock (gate)
        {
            var known = State.MembershipRequests.SingleOrDefault(r => r.RequestId == requestId);
            if (known != null)
            {
                if (known.PlayerId != playerId || known.CompanyId != companyId || known.Kind != kind)
                    throw new InvalidOperationException("Membership request ID is already bound to another request.");
                return known;
            }
            var player = Player(playerId); var company = Company(companyId);
            if (player.CompanyId != null || company.Liquidating) throw new InvalidOperationException("Player cannot enter this company.");
            if (kind == MembershipRequestKind.Application && company.MembershipPolicy == MembershipPolicy.InvitationOnly) throw new InvalidOperationException("Applications are disabled.");
            var pending = State.MembershipRequests.FirstOrDefault(r => r.PlayerId == playerId && r.CompanyId == companyId && r.Kind == kind && r.State == MembershipRequestState.Pending);
            if (pending != null) return pending;
            var autoAccept = kind == MembershipRequestKind.Application && company.MembershipPolicy == MembershipPolicy.Open;
            var request = new MembershipRequest { RequestId = requestId, PlayerId = playerId, CompanyId = companyId, Kind = kind, State = autoAccept ? MembershipRequestState.Accepted : MembershipRequestState.Pending, Version = 1 };
            State.MembershipRequests.Add(request);
            if (request.State == MembershipRequestState.Accepted) Join(player, company);
            return request;
        }
    }

    public MembershipRequest InviteMembership(string actorId, string requestId, string playerId, string companyId)
    {
        lock (gate)
        {
            var company = Company(companyId);
            RequirePermission(company, actorId, CompanyPermission.ManageMembers);
            return RequestMembership(requestId, playerId, companyId, MembershipRequestKind.Invitation);
        }
    }

    public void DecideMembership(string actorId, string requestId, bool accept)
    {
        lock (gate)
        {
            var request = State.MembershipRequests.Single(r => r.RequestId == requestId);
            var company = Company(request.CompanyId); RequirePermission(company, actorId, CompanyPermission.ManageMembers);
            if (request.State != MembershipRequestState.Pending) return;
            request.State = accept ? MembershipRequestState.Accepted : MembershipRequestState.Rejected;
            request.DecidedBy = actorId; request.Version++;
            if (accept) Join(Player(request.PlayerId), company);
        }
    }

    public void LeaveCompany(string playerId)
    {
        lock (gate)
        {
            var player = Player(playerId); if (player.CompanyId == null) return;
            if (player.ActiveOperation) throw new InvalidOperationException("Membership cannot change during an active operation.");
            var company = Company(player.CompanyId);
            if (company.Liquidating || company.LeaderId == playerId) throw new InvalidOperationException("Leader must transfer leadership or dissolve the company.");
            company.Members.Remove(playerId); company.DelegatedPermissions.Remove(playerId); company.Version++;
            player.CompanyId = null; player.Version++;
        }
    }

    public void Delegate(string actorId, string companyId, string memberId, CompanyPermission permission, bool enabled)
    {
        lock (gate)
        {
            var company = Company(companyId); RequirePermission(company, actorId, CompanyPermission.ManagePermissions);
            if (!company.Members.Contains(memberId)) throw new InvalidOperationException("Delegate must be a member.");
            if (!company.DelegatedPermissions.TryGetValue(memberId, out var rights)) company.DelegatedPermissions[memberId] = rights = new List<CompanyPermission>();
            if (enabled && !rights.Contains(permission)) rights.Add(permission); else if (!enabled) rights.Remove(permission);
            company.Version++;
        }
    }

    public void TransferLeadership(string leaderId, string companyId, string newLeaderId)
    {
        lock (gate)
        {
            var company = Company(companyId); if (company.LeaderId != leaderId || !company.Members.Contains(newLeaderId)) throw new InvalidOperationException("Invalid leadership transfer.");
            company.LeaderId = newLeaderId; company.Version++;
        }
    }

    public CommandRecord SubmitApplication(EconomyCommand command, string companyId)
    {
        return Execute(command, () =>
        {
            var player = Player(command.RequesterId);
            var company = Company(companyId);
            CheckVersion(command, "player:" + player.PlayerId, player.Version);
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            var request = RequestMembership(command.CommandId, command.RequesterId, companyId, MembershipRequestKind.Application);
            return "membership-" + request.State.ToString().ToLowerInvariant();
        }, "membership-apply|" + companyId);
    }

    public CommandRecord SendInvitation(EconomyCommand command, string targetPlayerId)
    {
        return Execute(command, () =>
        {
            var companyId = command.CompanyId ?? "";
            var company = Company(companyId);
            var target = Player(targetPlayerId);
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            CheckVersion(command, "player:" + target.PlayerId, target.Version);
            RequirePermission(company, command.RequesterId, CompanyPermission.ManageMembers);
            var request = RequestMembership(command.CommandId, targetPlayerId, companyId, MembershipRequestKind.Invitation);
            return "invitation-" + request.State.ToString().ToLowerInvariant();
        }, "membership-invite|" + (command.CompanyId ?? "") + "|" + targetPlayerId);
    }

    public CommandRecord DecideApplication(EconomyCommand command, string membershipRequestId, bool accept)
    {
        return Execute(command, () =>
        {
            var request = State.MembershipRequests.Single(x => x.RequestId == membershipRequestId);
            if (request.Kind != MembershipRequestKind.Application) return "request-kind-mismatch";
            var company = Company(request.CompanyId);
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            CheckVersion(command, "player:" + request.PlayerId, Player(request.PlayerId).Version);
            CheckVersion(command, "membership:" + request.RequestId, request.Version);
            RequirePermission(company, command.RequesterId, CompanyPermission.ManageMembers);
            return DecideRequest(request, command.RequesterId, company, accept);
        }, "membership-application-decision|" + membershipRequestId + "|" + accept);
    }

    public CommandRecord RespondToInvitation(EconomyCommand command, string membershipRequestId, bool accept)
    {
        return Execute(command, () =>
        {
            var request = State.MembershipRequests.Single(x => x.RequestId == membershipRequestId);
            if (request.Kind != MembershipRequestKind.Invitation) return "request-kind-mismatch";
            if (request.PlayerId != command.RequesterId) return "invitation-target-required";
            var company = Company(request.CompanyId);
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            CheckVersion(command, "player:" + request.PlayerId, Player(request.PlayerId).Version);
            CheckVersion(command, "membership:" + request.RequestId, request.Version);
            return DecideRequest(request, command.RequesterId, company, accept);
        }, "membership-invitation-response|" + membershipRequestId + "|" + accept);
    }

    public CommandRecord ChangeMembershipPolicy(EconomyCommand command, MembershipPolicy policy)
    {
        return Execute(command, () =>
        {
            var company = Company(command.CompanyId ?? "");
            RequirePermission(company, command.RequesterId, CompanyPermission.ManageMembers);
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            if (company.MembershipPolicy == policy) return "membership-policy-current";
            company.MembershipPolicy = policy; company.Version++;
            return "membership-policy-updated";
        }, "membership-policy|" + policy);
    }

    public CommandRecord ChangeDelegation(EconomyCommand command, string memberId, CompanyPermission permission, bool enabled)
    {
        return Execute(command, () =>
        {
            var company = Company(command.CompanyId ?? "");
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            RequirePermission(company, command.RequesterId, CompanyPermission.ManagePermissions);
            if (!company.Members.Contains(memberId)) return "delegate-not-member";
            var current = company.DelegatedPermissions.TryGetValue(memberId, out var rights) && rights.Contains(permission);
            if (current == enabled) return "permission-current";
            Delegate(command.RequesterId, company.CompanyId, memberId, permission, enabled);
            return "permission-updated";
        }, "permission|" + memberId + "|" + permission + "|" + enabled);
    }

    public CommandRecord Leave(EconomyCommand command)
    {
        return Execute(command, () =>
        {
            var player = Player(command.RequesterId);
            CheckVersion(command, "player:" + player.PlayerId, player.Version);
            if (player.CompanyId == null) return "already-independent";
            var company = Company(player.CompanyId);
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            LeaveCompany(player.PlayerId);
            return "company-left";
        }, "company-leave|" + command.RequesterId + "|" + (command.CompanyId ?? "independent"));
    }

    public CommandRecord TransferLeadershipCommand(EconomyCommand command, string newLeaderId)
    {
        return Execute(command, () =>
        {
            var company = Company(command.CompanyId ?? "");
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            if (company.LeaderId != command.RequesterId) return "leader-required";
            if (company.LeaderId == newLeaderId) return "leadership-current";
            TransferLeadership(command.RequesterId, company.CompanyId, newLeaderId);
            return "leadership-transferred";
        }, "leadership-transfer|" + newLeaderId);
    }

    public CommandRecord Dissolve(EconomyCommand command, IReadOnlyList<LiquidationAsset> assets, long debts, long penalties)
    {
        return Execute(command, () =>
        {
            var company = Company(command.CompanyId ?? ""); RequirePermission(company, command.RequesterId, CompanyPermission.Dissolve);
            CheckVersion(command, "company:" + company.CompanyId, company.Version);
            company.Liquidating = true; company.Version++;
            var beneficiaries = company.Members.OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var wallet = Wallet(AccountRef.Company(company.CompanyId));
            foreach (var asset in assets ?? Array.Empty<LiquidationAsset>())
            {
                if (State.History.Any(h => h.Kind == "liquidated-asset" && h.Fingerprint == asset.AssetId)) continue;
                var source = asset.DynamicMarketValue.HasValue && asset.DynamicMarketValue.Value >= 0 ? ReferenceValueSource.DynamicMarket : ReferenceValueSource.ConfiguredModel;
                var reference = source == ReferenceValueSource.DynamicMarket ? asset.DynamicMarketValue!.Value : asset.ConfiguredModelValue;
                if (reference < 0) throw new InvalidOperationException("Reference value cannot be negative.");
                var condition = Math.Max(0m, Math.Min(1m, asset.Condition));
                var rate = 0.15m + (0.35m * condition);
                var proceeds = decimal.ToInt64(decimal.Floor(reference * rate));
                wallet.Balance += proceeds; wallet.Version++;
                State.LiquidationSales.Add(new LiquidationSaleRecord { AssetId = asset.AssetId, ReferenceValue = reference, Source = source, Condition = condition, Rate = rate, Proceeds = proceeds });
                AddEntry(command, LedgerEntryKind.LiquidationSale, null, wallet.Account, proceeds, source + ";condition=" + condition + ";rate=" + rate);
                State.History.Add(History("liquidated-asset", company.CompanyId, beneficiaries, asset.AssetId));
            }
            var liabilities = Math.Max(0, debts) + Math.Max(0, penalties);
            var paid = Math.Min(wallet.Balance, liabilities); wallet.Balance -= paid; if (paid > 0) { wallet.Version++; AddEntry(command, LedgerEntryKind.DebtPayment, wallet.Account, null, paid, "debts-and-penalties-before-distribution"); }
            var cancelled = liabilities - paid;
            if (wallet.Balance > 0 && beneficiaries.Length > 0)
            {
                var share = wallet.Balance / beneficiaries.Length; var remainder = wallet.Balance % beneficiaries.Length;
                for (var i = 0; i < beneficiaries.Length; i++)
                {
                    var amount = share + (i < remainder ? 1 : 0); if (amount == 0) continue;
                    var target = Wallet(AccountRef.Player(beneficiaries[i])); target.Balance += amount; target.Version++;
                    AddEntry(command, LedgerEntryKind.Distribution, wallet.Account, target.Account, amount, "frozen-beneficiaries");
                }
                wallet.Balance = 0; wallet.Version++;
            }
            State.History.Add(History("company-dissolved", company.CompanyId, beneficiaries, "cancelledDebt=" + cancelled));
            ClosePendingRequestsForCompany(company.CompanyId, command.RequesterId);
            foreach (var member in beneficiaries) { var p = Player(member); p.CompanyId = null; p.Version++; }
            State.Companies.Remove(company); State.Wallets.Remove(wallet);
            return "dissolved";
        });
    }

    private CommandRecord Execute(EconomyCommand command, Func<string> action, string fingerprint = "")
    {
        lock (gate)
        {
            RequireId(command?.CommandId, "commandId"); RequireId(command!.RequesterId, "requesterId");
            var known = State.Commands.FirstOrDefault(c => c.CommandId == command.CommandId);
            if (known != null)
            {
                var sameScope = known.RequesterId == command.RequesterId && known.CompanyId == command.CompanyId;
                var sameFingerprint = string.IsNullOrEmpty(known.Fingerprint) || string.IsNullOrEmpty(fingerprint) || known.Fingerprint == fingerprint;
                return sameScope && sameFingerprint ? known : new CommandRecord { CommandId = command.CommandId, RequesterId = command.RequesterId, CompanyId = command.CompanyId, State = CommandState.Rejected, ResultCode = "command-id-reused", Fingerprint = fingerprint };
            }
            var before = CompanyEconomyPersistence.Serialize(State);
            var record = new CommandRecord { CommandId = command.CommandId, RequesterId = command.RequesterId, CompanyId = command.CompanyId, State = CommandState.InProgress, Fingerprint = fingerprint };
            State.Commands.Add(record);
            try { record.ResultCode = action(); record.State = IsSuccessfulResult(record.ResultCode) ? CommandState.Succeeded : CommandState.Rejected; }
            catch (Exception ex)
            {
                Restore(CompanyEconomyPersistence.Deserialize(before, State.CheckpointId));
                record = new CommandRecord { CommandId = command.CommandId, RequesterId = command.RequesterId, CompanyId = command.CompanyId, State = CommandState.Rejected, ResultCode = ex.GetType().Name + ":" + ex.Message, Fingerprint = fingerprint };
                State.Commands.Add(record);
            }
            return record;
        }
    }

    private void Restore(CompanyEconomySnapshot source)
    {
        State.Players = source.Players; State.Companies = source.Companies; State.Wallets = source.Wallets;
        State.MembershipRequests = source.MembershipRequests; State.Commands = source.Commands; State.Ledger = source.Ledger;
        State.History = source.History; State.LiquidationSales = source.LiquidationSales; State.LicenseRules = source.LicenseRules;
        State.LicenseEconomy = source.LicenseEconomy;
    }

    private void AddEntry(EconomyCommand command, LedgerEntryKind kind, AccountRef? debit, AccountRef? credit, long amount, string detail)
    {
        var id = command.CommandId + ":" + State.Ledger.Count(e => e.CommandId == command.CommandId);
        if (State.Ledger.Any(e => e.EntryId == id)) return;
        State.Ledger.Add(new LedgerEntry { EntryId = id, CommandId = command.CommandId, Kind = kind, Debit = debit, Credit = credit, Amount = amount, Detail = detail });
        State.Commands.Single(c => c.CommandId == command.CommandId).OperationIds.Add(id);
    }
    private static bool AllowedTransferKind(LedgerEntryKind k) => k == LedgerEntryKind.Contribution || k == LedgerEntryKind.Withdrawal || k == LedgerEntryKind.Salary || k == LedgerEntryKind.Reimbursement;
    private static bool IsSuccessfulResult(string code) => code == "created" || code == "transferred" || code == "revenue-routed" || code == "dissolved" || code == "wallet-synchronized" || code == "wallet-current" || code == "membership-policy-current" || code == "membership-policy-updated" || code == "permission-current" || code == "permission-updated" || code == "company-left" || code == "already-independent" || code == "leadership-current" || code == "leadership-transferred" || code.StartsWith("membership-") || code.StartsWith("invitation-");
    private string DecideRequest(MembershipRequest request, string actorId, CompanyState company, bool accept)
    {
        if (request.State != MembershipRequestState.Pending) return "membership-" + request.State.ToString().ToLowerInvariant();
        request.State = accept ? MembershipRequestState.Accepted : MembershipRequestState.Rejected;
        request.DecidedBy = actorId; request.Version++;
        if (accept) Join(Player(request.PlayerId), company);
        return (request.Kind == MembershipRequestKind.Invitation ? "invitation-" : "membership-") + request.State.ToString().ToLowerInvariant();
    }
    private void AuthorizeAccount(string actor, AccountRef account) { if (account.Kind == AccountKind.Player && account.OwnerId != actor) throw new InvalidOperationException("Personal account owner required."); if (account.Kind == AccountKind.Company) RequirePermission(Company(account.OwnerId), actor, CompanyPermission.ManageFunds); }
    private static void CheckVersion(EconomyCommand command, string key, long actual) { if (!command.ExpectedVersions.TryGetValue(key, out var expected) || expected != actual) throw new InvalidOperationException("Expected version mismatch: " + key); }
    private static void RequirePermission(CompanyState c, string player, CompanyPermission permission) { if (c.LeaderId == player) return; if (!c.DelegatedPermissions.TryGetValue(player, out var rights) || !rights.Contains(permission)) throw new InvalidOperationException("Permission denied: " + permission); }
    private void Join(PlayerEconomicState player, CompanyState company)
    {
        if (player.ActiveOperation || player.CompanyId != null || company.Liquidating) throw new InvalidOperationException("Membership change is not allowed.");
        player.CompanyId = company.CompanyId; player.Version++; company.Members.Add(player.PlayerId); company.Version++;
        foreach (var stale in State.MembershipRequests.Where(r => r.PlayerId == player.PlayerId && r.State == MembershipRequestState.Pending).ToArray())
        {
            stale.State = MembershipRequestState.Rejected;
            stale.DecidedBy = "system:joined:" + company.CompanyId;
            stale.Version++;
        }
    }
    private void ClosePendingRequestsForCompany(string companyId, string actorId)
    {
        foreach (var request in State.MembershipRequests.Where(r => r.CompanyId == companyId && r.State == MembershipRequestState.Pending).ToArray())
        {
            request.State = MembershipRequestState.Rejected;
            request.DecidedBy = actorId;
            request.Version++;
        }
    }
    private PlayerEconomicState Player(string id) => State.Players.SingleOrDefault(p => p.PlayerId == id) ?? throw new InvalidOperationException("Unknown player: " + id);
    private CompanyState Company(string id) => State.Companies.SingleOrDefault(c => c.CompanyId == id) ?? throw new InvalidOperationException("Unknown company: " + id);
    private Wallet Wallet(AccountRef account) => State.Wallets.SingleOrDefault(w => w.Account.Key == account.Key) ?? throw new InvalidOperationException("Unknown account: " + account.Key);
    private static void RequireId(string? value, string name) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required."); }
    private static EconomicHistoryRecord History(string kind, string company, IEnumerable<string> actors, string fingerprint) => new EconomicHistoryRecord { EventId = Guid.NewGuid().ToString("N"), Kind = kind, CompanyId = company, ActorIds = actors.ToList(), Fingerprint = fingerprint };
}

public static class CompanyEconomyPersistence
{
    private static readonly DataContractJsonSerializer Serializer = new DataContractJsonSerializer(typeof(CompanyEconomySnapshot));
    public static string Serialize(CompanyEconomySnapshot snapshot) { Validate(snapshot); using (var s = new MemoryStream()) { Serializer.WriteObject(s, snapshot); return Encoding.UTF8.GetString(s.ToArray()); } }
    public static CompanyEconomySnapshot Deserialize(string json, string expectedCheckpointId)
    {
        using (var s = new MemoryStream(Encoding.UTF8.GetBytes(json ?? "")))
        {
            var value = Serializer.ReadObject(s) as CompanyEconomySnapshot ?? throw new InvalidDataException("Missing economy snapshot.");
            Validate(value); if (value.CheckpointId != expectedCheckpointId) throw new InvalidDataException("Checkpoint mismatch; cross-save state is forbidden."); return value;
        }
    }
    public static void Validate(CompanyEconomySnapshot s)
    {
        if (s == null || s.Schema != CompanyEconomySnapshot.CurrentSchema || s.SchemaVersion != CompanyEconomySnapshot.CurrentVersion || string.IsNullOrWhiteSpace(s.CheckpointId)) throw new InvalidDataException("Unsupported or unscoped company economy snapshot.");
        if (s.Players.GroupBy(p => p.PlayerId).Any(g => g.Count() != 1) || s.Companies.GroupBy(c => c.CompanyId).Any(g => g.Count() != 1) || s.Wallets.GroupBy(w => w.Account.Key).Any(g => g.Count() != 1) || s.Commands.GroupBy(c => c.CommandId).Any(g => g.Count() != 1) || s.Ledger.GroupBy(e => e.EntryId).Any(g => g.Count() != 1)) throw new InvalidDataException("Duplicate durable economy identity.");
        foreach (var p in s.Players) if (p.CompanyId != null && !s.Companies.Any(c => c.CompanyId == p.CompanyId && c.Members.Contains(p.PlayerId))) throw new InvalidDataException("Invalid membership relation.");
        foreach (var company in s.Companies)
        {
            if (!company.Members.Contains(company.LeaderId) || company.Members.Distinct(StringComparer.Ordinal).Count() != company.Members.Count || company.Members.Any(memberId => !s.Players.Any(p => p.PlayerId == memberId && p.CompanyId == company.CompanyId))) throw new InvalidDataException("Invalid company membership invariant.");
            if (company.DelegatedPermissions.Keys.Any(memberId => !company.Members.Contains(memberId)) || company.DelegatedPermissions.Values.Any(rights => rights.Distinct().Count() != rights.Count)) throw new InvalidDataException("Invalid delegated company permissions.");
        }
        foreach (var player in s.Players.Where(p => p.CompanyId != null)) if (s.Companies.Count(c => c.CompanyId == player.CompanyId && c.Members.Contains(player.PlayerId)) != 1) throw new InvalidDataException("Ambiguous company membership relation.");
        if (s.MembershipRequests.GroupBy(r => r.RequestId).Any(g => g.Count() != 1) || s.MembershipRequests.Any(r => !s.Players.Any(p => p.PlayerId == r.PlayerId))) throw new InvalidDataException("Invalid durable membership request.");
        foreach (var w in s.Wallets) if (w.Balance < 0) throw new InvalidDataException("Negative wallet balance is not permitted.");
        foreach (var rule in s.LicenseRules) if (rule.BlocksGameplayWhenAbsent) throw new InvalidDataException("Licences cannot be the primary gameplay hard gate.");
        LicenseEconomyValidation.ValidateState(s.LicenseEconomy);
    }
}
