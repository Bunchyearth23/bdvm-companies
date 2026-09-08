# BDVM - Companies

`BDVM.Companies` owns the economic identity of players and companies: personal wallets, company accounts, membership, permissions, ledger history and company dissolution.

## Status

| Property | Value |
| --- | --- |
| Module kind | Economy feature |
| Target framework | .NET Framework 4.8 (`net48`) |
| Required module | `BDVM.Common` |
| Standalone install | Not yet |
| Current runtime host | `BDVM.Full` |

The company starts at zero. Members fund and operate it; every player keeps a separate personal wallet. A player may remain independent or belong to a company.

## Responsibilities

- Create and persist player and company economic state.
- Support free company creation with configurable membership policy: application with approval by default, invitation-only or open.
- Grant scoped permissions such as member, fund and fleet management without making every member an owner.
- Record contributions, withdrawals, salaries, reimbursements, operating revenue, costs and external wallet synchronization in an auditable ledger.
- Make commands idempotent so retries cannot charge or credit twice.
- Coordinate dissolution: cancel active company contracts, sell company assets, deduct debt and penalties, then distribute any positive remainder equally among members.
- Apply the configured liquidation value: 50% of reference value at perfect condition, linearly decreasing to a 15% minimum as condition deteriorates.
- Refuse partial liquidation states that would create an ambiguous or unrecoverable economy.

## Key surfaces

`CompanyEconomyEngine` is the primary command engine. `CompanyEconomySnapshot` and `CompanyEconomyPersistence` carry durable state. `CompanyLiquidationEngine` coordinates cancellation, asset realization and final distribution through explicit ports. Core models include `CompanyState`, `PlayerEconomicState`, `Wallet`, `LedgerEntry`, `CompanyPermission` and `MembershipPolicy`.

## Boundaries

This module does not discover Unity players, directly edit the vanilla wallet, sell world vehicles or cancel another module's contracts itself. Those effects pass through adapters and cancellation ports. It also does not implement dedicated-server transport.

## Dependencies and composition

`BDVM.Common` is the only declared project dependency. Fleet, Market and Operations consume Companies, not the reverse. During the migration, `Domain/` remains owned here but is linked into `BDVM.Full`; the small module assembly currently contains the module marker only.

External dependencies: none. Vanilla wallet and Multiplayer behavior enter through Core and runtime adapters rather than direct references from Companies.

## Build

With sibling repositories under `src/`:

```powershell
dotnet build .\BDVM.Companies.csproj -c Release
```

Build `BDVM.Full` to compile and exercise the domain sources in the current game composition.

## Testing and installation

Domain validation covers membership permissions, wallet isolation, idempotency, ledger balance, cancellation and liquidation edge cases. This repository is not yet distributed as a standalone Unity Mod Manager package. Install the matching `BDVM.Full` build for in-game testing.

## Compatibility

All authoritative changes must run on the host. Persistent IDs, command IDs and ledger references are stable data; changing their meaning requires an explicit migration. Legacy `DVCompany` state is not imported.

## License

Licensed under the Apache License, Version 2.0. See [LICENSE](LICENSE) and the applied copyright [NOTICE](NOTICE).
