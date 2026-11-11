# Fitness Club Client Management System (CRM)

Console application in C# for the Introduction to Functional Programming final project.
Clients buy subscriptions, visit the gym, renew, and the manager gets reports.

- **Repository:** <paste URL here>
- **Branch:** <branch>
- **Final commit:** <commit id>
- **Team:** <names, groups>

## AI use acknowledgement
<Write here, in your own words: the instructor permitted AI use for this project; which parts
were generated with AI (Claude by Anthropic), what you changed or wrote yourself, and the date.>

## Requirements
- .NET SDK 8.0 (check with `dotnet --version`). For a newer SDK change `TargetFramework`
  in both `.csproj` files, e.g. `net10.0`.

## Commands
One-time (creates the solution file):
```
dotnet new sln -n FitnessClub
dotnet sln add FitnessCrm/FitnessCrm.csproj FitnessCrm.Tests/FitnessCrm.Tests.csproj
```
Then:
```
dotnet build
dotnet run --project FitnessCrm
dotnet test
```
Optional argument: path to a data folder, e.g. `dotnet run --project FitnessCrm -- ./mydata`.
Default data folder is `data/` next to the built program (the sample files are copied there).
Saved data and `report.txt` are written to that same folder.
The sample data is built around the date 2026-11-07. Use menu item 11 to set that
working date if your real date is different.

## Files
| File | Content |
|---|---|
| `Models.cs` | Immutable records `Client`, `Subscription`, `Invoice`; `ValidatedSubscription` (private constructor + `Create`) |
| `Result.cs` | `Result<T>` (`Map`, `Bind`, `Match`) and `Maybe<T>` |
| `FitnessLogic.cs` | Pure validation, pricing, discount rules, visit and renewal logic, pattern matching |
| `Pipeline.cs` | 4 pipeline stages and 4 workflows |
| `Extensions.cs` | Custom extension methods (`IsActiveOn`, `ToMoney`, `FindClient`, ...) |
| `Reports.cs` | 2 LINQ reports, lazy `StreamExpiring` with `yield return` |
| `FileStorage.cs` | JSON load/save/export, error handling |
| `Program.cs` | Console menu |
| `PureFunctionTests.cs`, `WorkflowTests.cs`, `FileTests.cs` | xUnit tests |

## Requirement map
| Requirement | Where |
|---|---|
| Immutable models | `Models.cs` (records, `with` copies in `FitnessLogic.RecordVisit`) |
| Pattern matching | `FitnessLogic.GetMonthlyPrice`, `GetVisitLimit`, `LoyaltyDiscount`, `GetStatus` |
| Pipeline (4 stages) | `Pipeline.SellSubscription` |
| Higher-order function | `Func<Client, decimal> discountRule` in `Pipeline.DiscountStage` |
| Safe operation sequence | `ValidatedSubscription.Create`; lookup -> overlap check -> pipeline in `Pipeline.BuySubscription` |
| Result, Map, Bind | `Result.cs`, used in `Pipeline.cs` |
| Maybe | `Extensions.FindClient` and similar, `Maybe.ToResult` |
| Extension methods | `Extensions.cs` |
| LINQ reports | `Reports.RevenueByPlan` (GroupBy), `Reports.ExpiringSoon` (filter, Join, OrderBy/ThenBy) |
| Lazy processing | `Reports.StreamExpiring`, `Program.LazyDemo` |
| Persistence | `FileStorage.cs`, `data/*.json` |
