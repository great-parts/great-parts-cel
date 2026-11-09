# Time zones: the database, the offset and the calendar

`CelTimeZoneDatabase` holds the time zones that the application gives to an environment as TZif data (RFC 9636). This page covers how the data is read and checked, how a zone name is resolved, how the offset of a zone at an instant is found, and the library's own calendar that turns an instant plus an offset into the fields of a date. The functions that use it are in [library](library.md), the value kinds in [values](values.md), the environment member `UseTimeZones` in [facade](facade.md).

| File | Content | Core |
| :- | :- | :- |
| `src/Great.Parts.Cel/Values/CelTimeZoneDatabase.cs` | the public class: `Empty`, `Version`, `ZoneIds`, `Create`, `Contains`; it holds the sorted identifiers and one `ZoneData` per identifier | no |
| `src/Great.Parts.Cel/Values/ZoneData.cs` | the parsed zone: arrays and the rule, no logic | no |
| `src/Great.Parts.Cel/Values/TimeZoneRules.cs` | the resolution of a zone name; the offset of a zone at an instant | yes |
| `src/Great.Parts.Cel/Values/TimeZoneRules.Tzif.cs` | reading and checking TZif | yes |
| `src/Great.Parts.Cel/Values/TimeZoneRules.Posix.cs` | reading and applying a POSIX TZ rule | yes |
| `src/Great.Parts.Cel/Values/CalendarRules.cs` | the proleptic Gregorian calendar | yes |

The library never reads the operating system, files or environment variables for a zone, and uses neither `TimeZoneInfo` nor `DateTime`/`DateTimeOffset` members that depend on the clock or the zone of the process: the result of every expression depends on the bytes the application gave and nothing else.

## Create

`CelTimeZoneDatabase.Create(zones, version)` takes pairs of an identifier and the bytes of its TZif file. It reads and checks every pair at once, copies what it keeps (the bytes are not kept), sorts the identifiers by `string.CompareOrdinal` and returns a new object; two calls with equal data give two objects, which matters for the environment ([facade](facade.md), "Environment members"). `Empty` is one instance for the process: no zones, `Version` `null`. A link of the zoneinfo directory is a pair of its own with the bytes of its target; the database does not know it is a link and may share the parsed zone between entries whose bytes are equal.

The checks run per pair in the order of `zones`; the first failure throws and names the identifier. Every failure is `ArgumentException` with `ParamName` `zones`, except a `null` collection or identifier, which is `ArgumentNullException`:

| Failure | Message |
| :- | :- |
| the identifier is empty | `a time zone identifier is empty` |
| the identifier has `:` | `time zone identifier '{id}' has ':', so the name would read as a fixed offset` |
| the bytes are not TZif of version 2 or later: no `TZif` magic, a version byte other than `2`, `3`, `4`, version 1 (byte 0), data that ends early, a counter that does not fit the data, transitions that are not in increasing order, a type index out of range, no local time type, a footer that is not `\n` rule `\n` at the very end, a rule that does not read | `time zone '{id}': the data is not valid TZif: {detail}`; `{detail}` is one of `bad magic`, `unsupported version`, `version 1 has no 64-bit data`, `the data is cut`, `transitions are not in increasing order`, `a transition has an unknown local time type`, `no local time type`, `the footer is missing`, `the footer is not a POSIX TZ rule` |
| leap second records | `time zone '{id}': the data has leap second records, which CEL time does not have (files of the right/ directory are not supported)` |
| the same identifier with different bytes | `time zone '{id}' is given twice with different data`; the same identifier with equal bytes is one zone |

**No leap seconds** is checked on the header: a count of leap second records other than 0 in either block header. A file of a `right/` directory has them; a file of the `posix/` kind, which every other zoneinfo directory holds, does not. The library does not need to read the records, so it does not.

## Reading TZif

All numbers are big-endian. A block header is 44 bytes: `TZif`, a version byte, 15 reserved bytes, and six 4-byte counts in this order: UT/local indicators, standard/wall indicators, leap second records, transitions, local time types, abbreviation characters. The first block is version 1 data with 4-byte transition times; the reader does not use it and skips its body, whose length is `timecnt · 4 + timecnt + typecnt · 6 + charcnt + leapcnt · 8 + isstdcnt + isutcnt` (computed in `long`, compared with what is left). The second block has its own header with its own counts and the body `timecnt · 8 + timecnt + typecnt · 6 + charcnt + leapcnt · 12 + isstdcnt + isutcnt`; the reader keeps:

- the transition times, 8-byte, which must increase strictly;
- one type index per transition, below `typecnt`;
- per type, the 4-byte signed offset in seconds east of UTC; the DST flag and the abbreviation are not read, since the offset alone gives the local time;
- the footer: after the second body a line feed, the POSIX TZ rule (empty for a zone without a rule) and a line feed end the data.

Offsets keep their seconds: `America/New_York` before 1883 is -04:56:02, and the fields of a date follow it to the second.

## Resolving a zone name

A name is resolved for each call, by this order, without allocation:

1. `UTC`, ordinal: UTC, offset 0, whatever the database holds; a database entry of that identifier is never used.
2. A fixed offset: `[+-]?HH:MM` of 5 or 6 characters — an optional sign, two ASCII digits 00–23, `:`, two ASCII digits 00–59; without a sign it is `+`. The offset is `±(HH · 3600 + MM · 60)` seconds; `-00:00` is 0.
3. A name that has `:` and is no fixed offset (`+1:5`, `+24:00`, `+03:60`, `Europe:Moscow`, `:`): invalid, `InvalidTimeZone`.
4. Any other name: a binary search in the sorted identifiers by `string.CompareOrdinal`; found, the zone; not found — the case counts, `""` and `Local` included — or no database in the environment: `TimeZoneNotFound`.

The three results — a fixed offset, a zone, or one of the two failures — are what the receivers of `timestamp` use ([library](library.md), "Receivers"). The text of `TimeZoneNotFound` has three forms: `time zone '{zone}' is not found in the time zone database of the environment (version {version})` for a database with a version; the same without the parenthesis for a database without one, and for `Empty`; `time zone '{zone}' is not found: the environment has no time zone database` for an environment that never called `UseTimeZones`. The text of `InvalidTimeZone` is `invalid time zone '{zone}': expected 'UTC', an IANA time zone identifier or a fixed offset [+-]HH:MM`.

## The offset of a zone at an instant

For a zone, an instant `t` in Unix seconds, `ZoneData` with `n` transitions, the local time types and the rule `R` of the footer (`null` when empty):

1. `n = 0`: `R` gives the offset when there is one, otherwise type 0. Go gives the same: it reads such a file with one transition to type 0 at the start of time, and that transition is the last one, so the rule decides when there is one.
2. `t` before the first transition: type 0, whatever the types after it are, as RFC 9636 says. Go 1.27.3 chooses by the heuristic of tzcode 2013g (`lookupFirstZone`): type 0 when no transition uses it; else, when the type of the first transition is DST, the nearest type below it that is not DST; else the first type that is not DST; else type 0. On the test archive both choose the same type: of its 598 zones, the 553 that have transitions all get type 0 from the heuristic too, so the differential run sees no difference there; for other data a difference is possible, and the library keeps type 0 by design.
3. Otherwise `i` is the last transition with time ≤ `t`, found by binary search over the `long` array. When `i` is the last transition and `R` is not `null`, `R` gives the offset; else the offset of the type of transition `i`.

The search allocates nothing and reads arrays only; the offset is an `int` of seconds.

**A POSIX TZ rule** is `std offset [dst [offset] [,start,end]]`, as RFC 9636 and the POSIX TZ string define it. The library reads it exactly as `tzset` of Go 1.27.3 reads it, which accepts every rule of RFC 9636 and a little more, so `Create` refuses only a rule that Go cannot apply either, and a rule that reads gives the offsets of Go. The forms:

- **Name:** either `<` any characters but `>` `>`, or at least three characters up to the first digit, `,`, `+` or `-`. The names are not used; they are read to find where the rule goes on.
- **Number:** one or more ASCII digits, leading zeros allowed; reading fails as soon as the value passes the bound of its place.
- **Offset:** `[+-]hh[:mm[:ss]]`, hours 0–168 (the bound 24 · 7 of Go and tzcode; RFC 9636 names 167), minutes and seconds 0–59. The number is the time that is added to local time to get UTC, so the offset east of UTC is its negation: `<+0545>-5:45` is +05:45.
- **No DST name:** the rule ends after the standard offset, or a `,` follows it; the offset is `std` at every instant, and what follows the `,` is not read: `EST5,M3.2.0,M11.1.0` has no DST, as in Go.
- **A DST name without an offset:** `std` plus one hour. **No rule after the DST name:** `,M3.2.0,M11.1.0`. A rule is `,start,end`; the separator before `start` may also be `;` (`EST5EDT4;M3.2.0,M11.1.0`), which tzcode and Go accept and POSIX does not mention; the one before `end` is only `,`, and nothing may follow `end`.
- **A date:** `Jn`, `n` from 1 to 365 — the day of the year counted without 29 February, so `J59` is 28 February and `J60` is 1 March in every year; `n`, 0 to 365 — counted with 29 February, zero-based: 59 is 29 February in a leap year and 1 March otherwise; `Mm.w.d`, month 1–12, week 1–5, day 0–6 with 0 Sunday: the day `d` of week `w` of the month, where week 1 holds the first such day and week 5 is the last such day of the month, even when the month has only four.
- **Time of the date:** `/time` with the offset form above, which can be negative and can be 24 hours or more: `M3.5.0/-1` is one hour before midnight of the last Sunday, `M10.5.0/25` is 01:00 of the day after. The default is 02:00:00.

The day of the date of a year `y` as seconds from the start of the year: `Jn` — `(n − 1) · 86400`, plus 86400 when `y` is a leap year and `n ≥ 60`; `n` — `n · 86400`; `Mm.w.d` — with `f` the weekday of the first of the month (`CalendarRules`), the first day `d` of the month is `(d − f + 7) mod 7` counted from 0, each further week adds 7 while the result plus 7 is still below the length of the month, and then the days before the month are added, one more for a leap year after February; the sum times 86400.

**Applying the rule.** With `S` the standard offset and `D` the DST offset (both east of UTC), and `y` the year of `t` in UTC, `T0` the Unix seconds of 00:00 UTC on 1 January of `y`:

```text
a = T0 + dayOffset(start, y) + start.time − S      // the instant DST starts
b = T0 + dayOffset(end, y)   + end.time   − D      // the instant DST ends
a ≤ b : offset is D for a ≤ t < b, otherwise S
a > b : offset is S for b ≤ t < a, otherwise D     // the southern hemisphere
```

The year is the year of the instant in UTC, as in Go. The instants of the rule are measured from the start of that UTC year, so a rule that puts a transition into the next year is not applied to it. Go computes the second of the year of a negative instant wrongly, so for an instant before 1970 that a rule governs — a zone whose last transition is earlier and whose footer has a DST rule — the library gives the correct offset and Go may not; the files of the pinned Go are not expected to have such a zone, and a difference there is a finding of the differential run.

## The calendar

`CalendarRules` is the library's own proleptic Gregorian calendar over days counted from 1970-01-01, with astronomical years: year 0 exists and is a leap year (a year is leap when it is divisible by 4 and not by 100, or by 400). It does not use `DateTime`, `DateOnly` or `TimeZoneInfo`, so the platform and its calendar data do not change a result.

- `DaysFromCivil(year, month, day)` and `CivilFromDays(days)` are the integer algorithms of days-from-civil and civil-from-days, over `long` days, with floor division, valid for every `long` the library can pass (the largest is about 3 · 10¹¹ seconds, 4 · 10⁶ days).
- `DayOfWeek(days)` is `(days + 4) mod 7` with floor modulo: 0 is Sunday, and 1970-01-01 is a Thursday, 4.
- `DayOfYear(year, month, day)`, zero-based, is `days − DaysFromCivil(year, 1, 1)`; `DaysInMonth(year, month)` and `IsLeap(year)` as above.
- **Local fields.** For `t` in Unix seconds and an offset `o` in seconds, `L = t + o`, `days = floor(L / 86400)`, `rem = L − days · 86400`; the date fields come from `CivilFromDays(days)` and the time fields from `rem`. The fields are not clamped to the range of `timestamp`: for a moment at the edge, a zone can give year 0 (`0001-01-01T00:00:00Z` at `-05:00` is 0000-12-31 19:00, day of the year 365, since year 0 is a leap year) or year 10000 (`9999-12-31T23:59:59Z` at `+05:00` is 10000-01-01 04:59:59). Go gives the same.

## Tests of the page

The rows of the examples read small TZif files written byte by byte. The developer tests add: the whole archive of the test zone data reads without an exception ([tests](tests.md), "Time zone data"); every explicit transition of each zone of the archive: the offset at its instant is the offset of its type and one second before it the offset of the previous type, or type 0 for the first; for three zones of each hemisphere with a DST rule, the offsets at the second before and at the transition instants of each year from the first year after the last explicit transition of all six up to 2100 equal those of a day-by-day search over the rule of the footer written in the test (no formula of this page). The files of the archive are slim: their explicit transitions end at the last change of the rule (`America/New_York` in 2007), and later instants have only the rule, so the rule is checked against that search and not against a table of the file; at the last explicit transition the item before checks the rule against the table, since RFC 9636 asks the rule to agree with it there. Rules that Go reads beyond RFC 9636 — a `,` after the standard offset, `;` before the dates, 168 hours — read, and 169 hours does not; the calendar over a range of days against `DateTime` in the test, which is allowed there; malformed files, one test per row of the `Create` table.
