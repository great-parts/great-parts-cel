# The `calendar` extension: `date` and `time_of_day`

From version 2.0 the set `calendar` adds two abstract types: `date`, a calendar date from 0001-01-01 to 9999-12-31, and `time_of_day`, a time of day with a precision of 100 ns. They are the CEL side of the .NET types `DateOnly` and `TimeOnly`. The overloads and their errors are on the page `docs/extensions.md` ("calendar"); the costs on `docs/cost.md`. This page is how they are built.

## Values

| | `date` | `time_of_day` |
| :- | :- | :- |
| `CelValueKind` | `Date` (17) | `TimeOfDay` (18) |
| `CelTypeKind`, `CelType` | `Date` (20), `CelType.Date` (`"date"`) | `TimeOfDay` (21), `CelType.TimeOfDay` (`"time_of_day"`) |
| Payload of `CelValue` | `DateOnly.DayNumber` (an `int`, 0 for 0001-01-01) | `TimeOnly.Ticks` (a `long`, 0 to 863 999 999 999) |
| Equality, order, hash | by the number; only with the same kind (`dyn` with another kind: not equal, order is `no_matching_overload`) | the same |

No memory is allocated for a value. `CelValue.FromDate`, `FromTimeOfDay`, `TryGetDate`, `TryGetTimeOfDay` and the protected `CelObjectType.FromDate`, `FromTimeOfDay` of generated code convert exactly. `ValueEquality`, `ValueOrdering`, `ValueHasher`, `ValueText` (`date("2026-11-19")`, `time_of_day("09:30:00")`) and `ValueTypeNames` get one row per kind; a `switch` over the kinds keeps its default branch. The kinds 17 and 18 do not fit the four bits in which the failed overload of `NoMatchingOverload` packs the kinds of the arguments, so their type names are written beside the packing, as for `Abstract`. `OrderingRules` and the sets of the application order the two kinds: an `ISet<DateOnly>` in a result is a sorted list, as for `decimal`; there is no `sort()` of a list of dates.

## The set

`Library/Time/CalendarExtension.cs` defines version 1 the way `Library/Decimals/DecimalExtension.cs` defines `decimal`: the abstract type declarations, the table of 36 overload bindings and the descriptions of the functions; `CalendarFunctions.cs` has the implementations, `CalendarCosts.cs` the cost functions and `CalendarOverloadIds.cs` the identifiers. `CelExtensions.Calendar(uint)` returns version 1 or a set with an unknown version, which the build rejects (`UnknownExtensionVersion`). The set is not in `ExtensionCatalog`: cel-go has no such set, and an environment description in JSON cannot name it (error 4009). The code lives in the layer `Great.Parts.Cel.Library.Time`, next to the functions of timestamps; `eng/layers.txt` does not change.

**Without the set** the declaration checks reject `date` and `time_of_day` as they reject `decimal`: a variable is `TypeNotDeclared` with a hint, a member of a type is `FieldNotAccessible` naming the set, and a boxed `DateOnly` in the place of `dyn` is `TypeNotAccessible`; `TypeIndex.CalendarDeclared` tells the conversion rows which case holds. **Names:** a variable whose name is that of an abstract type of a connected set — `date`, `time_of_day`, `decimal`, `net.IP`, `net.CIDR` — is `DuplicateType` (`DeclarationChecks.CheckVariableName`), as a type of the application with that name already is; the exact name is checked, so `acme.date` is free.

## Text

`Values/CalendarTextRules.cs` reads and writes the text without culture; `DateOnly.Parse`, `TryParse`, `ParseExact`, `TryParseExact`, `ToString`, `TryFormat` and the same members of `TimeOnly` are in `eng/BannedSymbols.txt`, and a test compares the list with the overloads that the running target has.

- `date(string)`: exactly `YYYY-MM-DD`, ASCII digits, year 0001–9999, a day that exists in the month; anything else is `InvalidArgument` (3008) with the text.
- `time_of_day(string)`: `HH:MM`, `HH:MM:SS` or `HH:MM:SS.` and 1 to 7 digits; hours 00–23, minutes and seconds 00–59.
- `string(date)` is `YYYY-MM-DD`; `string(time_of_day)` is `HH:MM:SS`, with a fraction of up to 7 digits without trailing zeros when it is not zero, as `string(timestamp)` writes its fraction.

## Dates and instants

The time zones are those of the getters of a timestamp: `TimeZoneRules.Resolve` with the database of the environment, `UTC` and fixed offsets without one; the errors and the cost `Z` of the database of the operating system are the same (`time-zones.md`, `time-zones-system.md`).

- `date(t)`, `time_of_day(t)`: the date and the time of day of the instant at the offset 0; `date(t, z)`, `time_of_day(t, z)`: at the offset of `z` at that instant. The time of day keeps the ticks of the instant: `nanos / 100`, the rest dropped. An instant at the edge of the range of `timestamp` whose date in `z` falls outside 0001-01-01…9999-12-31 is `Overflow` (3006).
- `timestamp(d, x)`: the instant of the local date and time at the offset 0. `timestamp(d, x, z)`: with the local seconds `L`, take the offsets `oA` of `z` at the instant `L − 18 h` and `oB` at `L + 18 h`. If the offset of `z` at `L − oA` is `oA`, the result is `L − oA` — the only instant, or the earlier one of an overlap. Otherwise, if the offset at `L − oB` is `oB`, the result is `L − oB`. Otherwise `L` falls in a gap, and the result is `L − oA`: the local time moved forward by the length of the gap. With one transition within 36 hours this is `L − o0` when `L` is before `t + max(o0, o1)` and `L − o1` after it, for the transition at `t` from the offset `o0` to `o1`; a property test checks it at every transition of the test database in three zones. A result outside the range of `timestamp` is `Overflow` (3006).

## Arithmetic

- `addDays(n)`: `DayNumber + n` in `long`, then the range check — `Overflow` outside 0001-01-01…9999-12-31.
- `addMonths(n)`, `addYears(n)`: the month index `year * 12 + month − 1 + n` (`12 * n` for years) in `long`, the range check, then the day clamped to the length of the target month (`DateOnly.AddMonths` gives the same; it is the reference of the property test, not the code, because it throws instead of returning an error).
- `d1 - d2`: `DayNumber₁ − DayNumber₂` as `int`. `x1 - x2`: the difference of the ticks as a `duration` (ticks × 100 ns).
- The getters use the proleptic Gregorian calendar of `Values/CalendarRules.cs`, shared with the getters of a timestamp: `getMonth` and `getDayOfMonth` count from 0, `getDate` from 1, `getDayOfWeek` is 0 for Sunday.

## Data of the application

The source generator stops excluding members of `DateOnly`, `TimeOnly` and their `Nullable<>`: it reads them through `FromDate` and `FromTimeOfDay` and gives them the CEL types `date` and `time_of_day`. Bindings, the result rows (`DateOnly`, `TimeOnly`, `object`) and the typed delegates use the same conversion rows (`ConversionRules.Calendar.cs`), and the one-step lists of the generic inputs get the two types. The simple entry does not read them: its hidden environment has no extension, so they are `TypeNotDeclared`, as `decimal` is.
