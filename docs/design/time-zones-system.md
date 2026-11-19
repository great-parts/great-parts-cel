# Time zones of the operating system: `CelTimeZoneDatabase.FromSystem()`

The second kind of time zone database: the zones of the machine, read by .NET through `TimeZoneInfo`. The database from TZif bytes ([time-zones](time-zones.md)) does not change. The limits are in `docs/limits.md` ("Time zones of the operating system"), the cost `Z` in `docs/cost.md`; the error is `TimeZoneNotFound` (3022).

| File | Content | Core |
| :- | :- | :- |
| `src/Great.Parts.Cel/Values/CelTimeZoneDatabase.cs` | `FromSystem()`: one instance with no data of its own, marked `IsSystem` | no |
| `src/Great.Parts.Cel/Values/SystemZoneSource.cs` | the internal static class of the zones of the machine: the only file that uses `TimeZoneInfo`, with the suppression of RS0030 and its reason; the constant `Cost` | yes |
| `src/Great.Parts.Cel/Values/TimeZoneNameRules.cs` | the check of a name by the IANA grammar | yes |
| `src/Great.Parts.Cel/Values/TimeZoneRules.cs` | the resolution: step 4 asks `SystemZoneSource` when the database `IsSystem` | yes |

## The instance

`FromSystem()` returns one instance per process (a static readonly field of `CelTimeZoneDatabase`; no lock of its own). `Version` is `null`, `ZoneIds` is empty, `Contains(name)` is the check and the search below and is `true` only for a zone that is found. `UseTimeZones(CelTimeZoneDatabase.FromSystem())` twice is the same instance and so one database; with another database it is `TimeZoneDatabaseConflict` (4012), as for any two databases.

## Resolving a name

Steps 1–3 of [time-zones](time-zones.md#resolving-a-zone-name) (`UTC`, a fixed offset, an invalid offset) run first and do not reach the source. Step 4 with the database of the system:

1. **The grammar** (`TimeZoneNameRules`): length 1 to 64 UTF-16 code units; parts split by `/`, each non-empty, of ASCII letters, digits, `_`, `-`, `+`; the first part is not `posix` and not `right` (ordinal, case-sensitive, as tzdata names its directories). Anything else — `../etc/passwd`, `Europe//Moscow`, `Europe/Zürich`, `Russian Standard Time` (the space) — is `TimeZoneNotFound` without a call to the platform.
2. **The found zones**: a `ConcurrentDictionary<string, Zone>` by ordinal name, looked up by the span of the name without a string; a hit returns the zone.
3. **The platform**: `TimeZoneInfo.FindSystemTimeZoneById(name)` inside a `try`: `TimeZoneNotFoundException`, `InvalidTimeZoneException` and `SecurityException` are a miss; the result is accepted only when `HasIanaId` is `true`, otherwise a miss (a Windows id that passes the grammar, such as `UTC-02`, which .NET can convert on Linux too, stays a miss on every OS). An accepted zone is added with `TryAdd` and returned. A miss is not stored.
4. A miss is `TimeZoneNotFound` with the name; the text is the form "without a version" of [time-zones](time-zones.md#resolving-a-zone-name).

`Local`, the empty name and names of the case that the OS would accept but tzdata does not have (`europe/moscow` on a case-insensitive file system) are left to the platform after the grammar: `Local` passes the grammar and is not an IANA zone, so `FindSystemTimeZoneById` either fails or returns a zone without `HasIanaId`; the tests check that `Local` is `TimeZoneNotFound`. A case-folded name that the platform finds is accepted only if it has an IANA id; the library does not compare the case of the name with the id of the zone.

## The offset

A zone of the system (`SystemZoneSource.Zone`) holds a `TimeZoneInfo`. The offset at an instant `t` (Unix seconds) is `zone.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(t))` in whole seconds; the instants of `timestamp` are inside the range of `DateTimeOffset`, so no clamp is needed. The calendar of the library turns the instant and the offset into the fields, as for a TZif zone: `TimeZoneInfo` gives the offset and nothing else. So the offsets of this database depend on the machine: the time zone data of the OS, and ICU on Windows.

## Cost

The receivers with a zone charge `1 + ⌈L/10⌉`, and in an environment whose database is the one of the system `+ Z` for each name that reaches step 4, whether it is found, stored or missed and whether it passed the grammar. The charge comes before the search. A constant zone name is not resolved at planning with this database (the compilation does not read the OS): the planner keeps the name and the evaluation resolves it each time. `Z` is the constant `SystemZoneSource.Cost`, 2 000 units: the median of a miss of `FindSystemTimeZoneById` measured on Linux in the container of the build is 12–19 µs, 1 200–1 900 units of 10 ns, and `Z` follows the typical time of the step, as the other units of the cost do, not the tail and not the first cold call.

## Threads and memory

Reads and `TryAdd` of the dictionary are safe from any number of threads. The dictionary grows only by zones that the platform found, so it is bounded by the zones of the OS and not by the names of untrusted expressions.

## Tests

The examples (`Examples/Values/SystemTimeZoneTests.cs`) fix the names that never reach the platform, `HasIanaId`, the offset against `TimeZoneInfo` of the same machine (on Linux), the constant cost and the one instance. The developer tests take the rest: the grammar row by row (length 64 and 65, each allowed character, `+` and `-` in parts), `Contains`, the conflict with another database, and a counter of the calls to the platform (a thread-static counter of `SystemZoneSource`, compiled in all builds, so that a test sees only its own calls). The run on `browser-wasm`, where the runtime has no zones of the OS, is a check of the conformance application ([conformance](conformance.md#browser-wasm)): it has the runtime option `System.TimeZoneInfo.Invariant`, `Program.cs` first asks `TimeZoneInfo` for `Europe/Moscow` and requires that it throws `TimeZoneNotFoundException`, so the premise holds, and then requires that `FromSystem()` behaves as a machine without zones: `Contains` is `false`, an expression with the name is `TimeZoneNotFound` with the name as the subject, and `UTC` and a fixed offset still give their hours.

Related pages: [time-zones](time-zones.md), [library](library.md), [facade](facade.md).
