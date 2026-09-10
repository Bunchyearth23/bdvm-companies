using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace BDVM.Domain;

public enum ExternalWalletMirrorAction
{
    None,
    ImportExternal,
    CreditExternal,
    DebitExternal,
    Conflict
}

[DataContract]
public sealed class ExternalWalletMirrorRecord
{
    [DataMember(Name = "playerId", Order = 1)] public string PlayerId { get; set; } = "";
    [DataMember(Name = "lastSynchronizedBalance", Order = 2)] public long LastSynchronizedBalance { get; set; }
    [DataMember(Name = "lastOperationId", Order = 3)] public string LastOperationId { get; set; } = "";
    [DataMember(Name = "version", Order = 4)] public long Version { get; set; }
}

public sealed class ExternalWalletMirrorPlan
{
    public ExternalWalletMirrorAction Action { get; set; }
    public string PlayerId { get; set; } = "";
    public string OperationId { get; set; } = "";
    public long InternalBalance { get; set; }
    public long ExternalBalance { get; set; }
    public long LastSynchronizedBalance { get; set; }
    public long Amount { get; set; }
}

public sealed class ExternalWalletMirrorEngine
{
    private readonly CompanyEconomySnapshot state;

    public ExternalWalletMirrorEngine(CompanyEconomySnapshot state)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        CompanyEconomyPersistence.Validate(state);
    }

    public ExternalWalletMirrorPlan Plan(string playerId, long externalBalance, string operationId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("A persistent player ID is required.", nameof(playerId));
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 160) throw new ArgumentException("A bounded operation ID is required.", nameof(operationId));
        if (externalBalance < 0) throw new ArgumentOutOfRangeException(nameof(externalBalance));
        var wallet = state.Wallets.SingleOrDefault(value => value.Account.Kind == AccountKind.Player && value.Account.OwnerId == playerId)
            ?? throw new InvalidOperationException("The persistent player wallet is unavailable.");
        var mirror = state.ExternalWalletMirrors.SingleOrDefault(value => value.PlayerId == playerId);
        if (mirror == null)
        {
            return new ExternalWalletMirrorPlan
            {
                Action = wallet.Balance == externalBalance ? ExternalWalletMirrorAction.None : ExternalWalletMirrorAction.ImportExternal,
                PlayerId = playerId,
                OperationId = operationId,
                InternalBalance = wallet.Balance,
                ExternalBalance = externalBalance,
                LastSynchronizedBalance = externalBalance,
                Amount = wallet.Balance == externalBalance ? 0 : Math.Abs(checked(wallet.Balance - externalBalance))
            };
        }

        var internalChanged = wallet.Balance != mirror.LastSynchronizedBalance;
        var externalChanged = externalBalance != mirror.LastSynchronizedBalance;
        var action = ExternalWalletMirrorAction.None;
        if (internalChanged && externalChanged && wallet.Balance != externalBalance) action = ExternalWalletMirrorAction.Conflict;
        else if (internalChanged && !externalChanged) action = wallet.Balance > externalBalance ? ExternalWalletMirrorAction.CreditExternal : ExternalWalletMirrorAction.DebitExternal;
        else if (!internalChanged && externalChanged) action = ExternalWalletMirrorAction.ImportExternal;

        return new ExternalWalletMirrorPlan
        {
            Action = action,
            PlayerId = playerId,
            OperationId = operationId,
            InternalBalance = wallet.Balance,
            ExternalBalance = externalBalance,
            LastSynchronizedBalance = mirror.LastSynchronizedBalance,
            Amount = wallet.Balance == externalBalance ? 0 : Math.Abs(checked(wallet.Balance - externalBalance))
        };
    }

    public ExternalWalletMirrorRecord Complete(string playerId, long synchronizedBalance, string operationId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) throw new ArgumentException("A persistent player ID is required.", nameof(playerId));
        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 160) throw new ArgumentException("A bounded operation ID is required.", nameof(operationId));
        if (synchronizedBalance < 0) throw new ArgumentOutOfRangeException(nameof(synchronizedBalance));
        var wallet = state.Wallets.SingleOrDefault(value => value.Account.Kind == AccountKind.Player && value.Account.OwnerId == playerId)
            ?? throw new InvalidOperationException("The persistent player wallet is unavailable.");
        if (wallet.Balance != synchronizedBalance) throw new InvalidOperationException("The BDVM and external player wallets are not synchronized.");
        var mirror = state.ExternalWalletMirrors.SingleOrDefault(value => value.PlayerId == playerId);
        if (mirror == null)
        {
            mirror = new ExternalWalletMirrorRecord { PlayerId = playerId };
            state.ExternalWalletMirrors.Add(mirror);
        }
        if (mirror.LastSynchronizedBalance == synchronizedBalance && string.Equals(mirror.LastOperationId, operationId, StringComparison.Ordinal)) return mirror;
        mirror.LastSynchronizedBalance = synchronizedBalance;
        mirror.LastOperationId = operationId;
        mirror.Version++;
        return mirror;
    }

    public static void Validate(CompanyEconomySnapshot state)
    {
        state.ExternalWalletMirrors = state.ExternalWalletMirrors ?? new System.Collections.Generic.List<ExternalWalletMirrorRecord>();
        if (state.ExternalWalletMirrors.GroupBy(value => value.PlayerId, StringComparer.Ordinal).Any(group => group.Count() != 1))
            throw new InvalidDataException("Duplicate external wallet mirror identity.");
        foreach (var mirror in state.ExternalWalletMirrors)
            if (mirror == null || string.IsNullOrWhiteSpace(mirror.PlayerId) || mirror.LastSynchronizedBalance < 0 || mirror.Version < 0 ||
                (mirror.LastOperationId ?? "").Length > 160 || !state.Players.Any(player => player.PlayerId == mirror.PlayerId))
                throw new InvalidDataException("Invalid external wallet mirror record.");
    }
}
