# Benchmark rig: Native AOT on .NET 11

- Date and time of the report (UTC): 2026-11-26 18:02:01
- Commit of great-parts/great-parts-cel: [`5781cafa559d36cb30b413f2a23bded8eb62464c`](https://github.com/great-parts/great-parts-cel/commit/5781cafa559d36cb30b413f2a23bded8eb62464c)
- Runs: 3
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45844760833 (GitHub keeps a run for 90 days; this report does not depend on it)

The summary of the rig follows. The full reports of BenchmarkDotNet are in the artifact `bench-results` of the run, for 90 days.

## Conditions

- Date of the summary: 2026-11-26T18:02:01.283Z
- Commit: 5781cafa559d36cb30b413f2a23bded8eb62464c
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45844760833
- Runner: Linux X64, image ubuntu26 20261122.9.1
- Runtime: Native AOT on .NET 11 (BenchmarkDotNet toolchain nativeaot11.0, runtime identifier linux-x64); the code of every library and of the rig is compiled ahead of time by the ILCompiler of the .NET SDK
- Limits of the speed-ups: not measured under Native AOT (their rules are stated for JIT)
- Great.Parts.Cel: built from the sources of this commit
- Packages: BenchmarkDotNet 0.16.0-preview.2; Celly 1.2.0; DotnetCel 1.0.7; Cel.NET 2.3.1; Cel 0.3.3; Grpc.Tools 2.85.0

## Verification

Correct: all three variants gave the expected value: the original text (compile and evaluate), the text with a tail (compile and evaluate), the text with a tail (full cycle).

| Scenario | Expression | Lines | Expected | Celly | DotnetCel | Cel.NET | TelusCel | GreatPartsCel | GPCel-Input | GPCel-Simple |
|---|---|--:|---|---|---|---|---|---|---|---|
| E1 | `order.total > 1000.0 && order.status == 'approved'` | 10 | bool:true | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: total) | correct | correct | correct | correct | correct |
| E2/10 | `order.lines.all(l, l.qty > 0 && l.price >= 0.0)` | 10 | bool:true | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: lines) | correct | correct | correct | correct | correct |
| E2/100 | `order.lines.all(l, l.qty > 0 && l.price >= 0.0)` | 100 | bool:true | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: lines) | correct | correct | correct | correct | correct |
| E3/10 | `order.lines.exists(l, l.sku.startsWith('SKU-'))` | 10 | bool:true | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: lines) | correct | correct | correct | correct | correct |
| E3/100 | `order.lines.exists(l, l.sku.startsWith('SKU-'))` | 100 | bool:true | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: lines) | correct | correct | correct | correct | correct |
| E4/10 | `order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)` | 10 | [double:208,double:280,double:361] | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: lines) | correct | correct | correct | correct | correct |
| E4/100 | `order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)` | 100 | [double:208,double:280,double:361,double... | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: lines) | correct | correct | correct | correct | correct |
| E5 | `order.customer.email.matches('^[^@]+@[^@]+[.][a-z]+$')` | 10 | bool:true | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: customer) | correct | correct | correct | correct | correct |
| E6 | `has(order.discount) ? order.total - order.discount : order.total` | 10 | double:1400 | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:1: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: total) | correct | correct | correct | correct | correct |
| E7 | `order.created + duration('72h') > now` | 10 | bool:true | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:6: undefined fi... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: created) | correct | wrong (3 of 3): ERROR(CelNoSuchOverloadException: No overload exists to ADD type... | correct | correct | correct |
| E8 | `size(order.lines) <= 100 && order.currency in ['RUB', 'USD', 'EUR']` | 10 | bool:true | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:11: undefined f... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: lines) | correct | correct | correct | correct | correct |
| E9 | `'Order ' + order.id + ': ' + order.status` | 10 | string:'Order ORD-0001: approved' | wrong (3 of 3): ERROR(InvalidOperationException: check: ERROR: 1:17: undefined f... | wrong (3 of 3): ERROR(CelEvaluationException: no such field: id) | correct | correct | correct | correct | correct |

The verification ran under Native AOT: the rig was published with PublishAot and every library ran in a native process of its own. A library whose result is wrong or an error in a Native AOT process is excluded, like a library whose result is wrong under JIT.

Excluded from the measurement under Native AOT (wrong result or error): Celly - E1; DotnetCel - E1; Celly - E2/10; DotnetCel - E2/10; Celly - E2/100; DotnetCel - E2/100; Celly - E3/10; DotnetCel - E3/10; Celly - E3/100; DotnetCel - E3/100; Celly - E4/10; DotnetCel - E4/10; Celly - E4/100; DotnetCel - E4/100; Celly - E5; DotnetCel - E5; Celly - E6; DotnetCel - E6; Celly - E7; DotnetCel - E7; TelusCel - E7; Celly - E8; DotnetCel - E8; Celly - E9; DotnetCel - E9.

- Celly, 12 of 12 scenarios, the first: E1, ERROR(InvalidOperationException: check: ERROR: 1:6: undefined field 'total'; ERROR: 1:30: undefined field 'status')

- DotnetCel, 12 of 12 scenarios, the first: E1, ERROR(CelEvaluationException: no such field: total)

- TelusCel, 1 of 12 scenarios, the first: E7, ERROR(CelNoSuchOverloadException: No overload exists to ADD types 'Google.Protobuf.WellKnownTypes.Timestamp' and 'Google.Protobuf.WellKnownTypes.Duration'.)

## Measurement environment

- BenchmarkDotNet 0.16.0-preview.2; OS: Linux Ubuntu 26.04.1 LTS (Resolute Raccoon); processor: Intel Xeon 6975P-C
- Cores: 2 physical, 4 logical; architecture: X64
- Runtime of the measurement: Native AOT on .NET 11; runtime of the host of BenchmarkDotNet (not measured): .NET 11.0.0 (11.0.0, 11.0.26.53018); SDK: 11.0.100; configuration: RELEASE

## Runs

- Run 1: 2026-11-26T15:02:21.231Z - 2026-11-26T16:03:21.575Z; job: Default; runtime: Native AOT; cases: all; filter: *
- Run 2: 2026-11-26T16:03:21.575Z - 2026-11-26T17:03:07.610Z; job: Default; runtime: Native AOT; cases: all; filter: *
- Run 3: 2026-11-26T17:03:07.610Z - 2026-11-26T18:02:01.242Z; job: Default; runtime: Native AOT; cases: all; filter: *

The table of each run is `results/runs/<k>/summary.md`.

## Summary over the runs

The ratio of a row in a run is to the fastest library with a correct result in the same run (for a variant of GreatPartsCel: to the fastest other library or to itself, not to its other variants). R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has 3 runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others. "Allocated" is the median over the runs of the bytes allocated per operation that BenchmarkDotNet reports (MemoryDiagnoser), less the allocation of making the text for compile and full cycle, as the time is.

| Scenario | Operation | Library | Runs | Ratios by run | R | Spread | Fastest by run | Median time over the runs | Allocated | R > 1.1 | Both sides of 1.1 |
|---|---|---|--:|---|--:|--:|---|--:|--:|---|---|
| E1 | compile | Cel.NET | 3 | 1: 10.55; 2: 9.91; 3: 8.90 | 9.91 | 1.19 | GPCel-Input | 30.06 us | 54.62 KB | yes |  |
| E1 | compile | TelusCel | 3 | 1: 2.31; 2: 2.32; 3: 2.17 | 2.31 | 1.07 | GPCel-Input | 7.02 us | 10.31 KB | yes |  |
| E1 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.87 us | 8.96 KB | no |  |
| E1 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.03 us | 7.09 KB | no |  |
| E1 | evaluate | Cel.NET | 3 | 1: 1.76; 2: 1.62; 3: 1.56 | 1.62 | 1.13 | GPCel-Input | 507.2 ns | 1.51 KB | yes |  |
| E1 | evaluate | TelusCel | 3 | 1: 20.94; 2: 21.72; 3: 21.18 | 21.18 | 1.04 | GPCel-Input | 6.79 us | 11.13 KB | yes |  |
| E1 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 384.9 ns | 320 B | no |  |
| E1 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 319.7 ns | 320 B | no |  |
| E1 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.05; 3: 1.08 | 1.05 | 1.08 | GPCel-Simple, Cel.NET | 537.7 ns | 160 B | no |  |
| E1 | full cycle | Cel.NET | 3 | 1: 9.35; 2: 8.19; 3: 7.80 | 8.19 | 1.20 | GPCel-Input | 30.04 us | 56.13 KB | yes |  |
| E1 | full cycle | TelusCel | 3 | 1: 4.18; 2: 3.96; 3: 3.88 | 3.96 | 1.08 | GPCel-Input | 14.58 us | 21.44 KB | yes |  |
| E1 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.39 us | 9.28 KB | no |  |
| E1 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.67 us | 7.40 KB | no |  |
| E1 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.29 us | 7.53 KB | no |  |
| E2/10 | compile | Cel.NET | 3 | 1: 7.60; 2: 6.29; 3: 6.37 | 6.37 | 1.21 | GPCel-Input, GreatPartsCel | 36.07 us | 78.12 KB | yes |  |
| E2/10 | compile | TelusCel | 3 | 1: 2.09; 2: 1.90; 3: 1.92 | 1.92 | 1.10 | GPCel-Input, GreatPartsCel | 10.90 us | 15.48 KB | yes |  |
| E2/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.72 us | 12.38 KB | no |  |
| E2/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.81 us | 11.73 KB | no |  |
| E2/10 | evaluate | Cel.NET | 3 | 1: 2.02; 2: 1.89; 3: 2.00 | 2.00 | 1.07 | GPCel-Input | 4.82 us | 5.83 KB | yes |  |
| E2/10 | evaluate | TelusCel | 3 | 1: 30.71; 2: 30.38; 3: 32.88 | 30.71 | 1.08 | GPCel-Input | 73.71 us | 111.01 KB | yes |  |
| E2/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.54 us | 368 B | no |  |
| E2/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.41 us | 368 B | no |  |
| E2/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 2.72 us | 160 B | no |  |
| E2/10 | full cycle | Cel.NET | 3 | 1: 5.43; 2: 4.82; 3: 5.02 | 5.02 | 1.13 | GPCel-Input | 42.45 us | 83.95 KB | yes |  |
| E2/10 | full cycle | TelusCel | 3 | 1: 11.10; 2: 10.14; 3: 10.34 | 10.34 | 1.09 | GPCel-Input | 87.51 us | 126.49 KB | yes |  |
| E2/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 8.64 us | 12.74 KB | no |  |
| E2/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 8.46 us | 12.08 KB | no |  |
| E2/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 9.01 us | 12.14 KB | no |  |
| E2/100 | compile | Cel.NET | 3 | 1: 7.20; 2: 6.19; 3: 6.79 | 6.79 | 1.16 | GPCel-Input, GreatPartsCel | 37.99 us | 78.11 KB | yes |  |
| E2/100 | compile | TelusCel | 3 | 1: 2.02; 2: 1.90; 3: 1.96 | 1.96 | 1.06 | GPCel-Input, GreatPartsCel | 11.01 us | 15.48 KB | yes |  |
| E2/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.82 us | 12.38 KB | no |  |
| E2/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.73 us | 11.72 KB | no |  |
| E2/100 | evaluate | Cel.NET | 3 | 1: 2.02; 2: 2.04; 3: 1.99 | 2.02 | 1.03 | GPCel-Input, GreatPartsCel | 44.55 us | 51.15 KB | yes |  |
| E2/100 | evaluate | TelusCel | 3 | 1: 33.09; 2: 34.83; 3: 33.95 | 33.95 | 1.05 | GPCel-Input, GreatPartsCel | 746.17 us | 1.03 MB | yes |  |
| E2/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 22.10 us | 368 B | no |  |
| E2/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 22.09 us | 368 B | no |  |
| E2/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 23.19 us | 160 B | no |  |
| E2/100 | full cycle | Cel.NET | 3 | 1: 3.11; 2: 2.80; 3: 2.86 | 2.86 | 1.11 | GPCel-Input, GreatPartsCel | 80.33 us | 129.26 KB | yes |  |
| E2/100 | full cycle | TelusCel | 3 | 1: 26.31; 2: 28.23; 3: 24.71 | 26.31 | 1.14 | GPCel-Input, GreatPartsCel | 734.31 us | 1.04 MB | yes |  |
| E2/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 28.40 us | 12.74 KB | no |  |
| E2/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 28.08 us | 12.08 KB | no |  |
| E2/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 30.21 us | 12.15 KB | no |  |
| E3/10 | compile | Cel.NET | 3 | 1: 6.87; 2: 6.04; 3: 5.99 | 6.04 | 1.15 | GPCel-Input, GreatPartsCel | 27.08 us | 57.47 KB | yes |  |
| E3/10 | compile | TelusCel | 3 | 1: 2.11; 2: 1.94; 3: 1.92 | 1.94 | 1.10 | GPCel-Input, GreatPartsCel | 8.68 us | 13.36 KB | yes |  |
| E3/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.50 us | 11.00 KB | no |  |
| E3/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.61 us | 10.51 KB | no |  |
| E3/10 | evaluate | Cel.NET | 3 | 1: 2.26; 2: 2.20; 3: 2.19 | 2.20 | 1.03 | GPCel-Input | 1.02 us | 2.02 KB | yes |  |
| E3/10 | evaluate | TelusCel | 3 | 1: 93.07; 2: 92.54; 3: 90.69 | 92.54 | 1.03 | GPCel-Input | 42.94 us | 64.45 KB | yes |  |
| E3/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 536.3 ns | 368 B | no |  |
| E3/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 464.0 ns | 368 B | no |  |
| E3/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 662.8 ns | 160 B | no |  |
| E3/10 | full cycle | Cel.NET | 3 | 1: 6.29; 2: 5.71; 3: 5.58 | 5.71 | 1.13 | GPCel-Input, GreatPartsCel | 29.74 us | 59.51 KB | yes |  |
| E3/10 | full cycle | TelusCel | 3 | 1: 10.99; 2: 10.27; 3: 9.74 | 10.27 | 1.13 | GPCel-Input, GreatPartsCel | 53.43 us | 77.81 KB | yes |  |
| E3/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.44 us | 11.36 KB | no |  |
| E3/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.21 us | 10.87 KB | no |  |
| E3/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 5.74 us | 10.93 KB | no |  |
| E3/100 | compile | Cel.NET | 3 | 1: 6.75; 2: 6.32; 3: 6.45 | 6.45 | 1.07 | GPCel-Input, GreatPartsCel | 28.75 us | 57.47 KB | yes |  |
| E3/100 | compile | TelusCel | 3 | 1: 2.11; 2: 1.95; 3: 1.91 | 1.95 | 1.11 | GPCel-Input, GreatPartsCel | 8.82 us | 13.36 KB | yes |  |
| E3/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.58 us | 11.00 KB | no |  |
| E3/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.67 us | 10.51 KB | no |  |
| E3/100 | evaluate | Cel.NET | 3 | 1: 4.87; 2: 4.81; 3: 4.93 | 4.87 | 1.03 | GPCel-Input | 2.20 us | 8.77 KB | yes |  |
| E3/100 | evaluate | TelusCel | 3 | 1: 886.37; 2: 929.77; 3: 816.30 | 886.37 | 1.14 | GPCel-Input | 400.22 us | 585.88 KB | yes |  |
| E3/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 535.6 ns | 368 B | no |  |
| E3/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 458.5 ns | 368 B | no |  |
| E3/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 660.3 ns | 160 B | no |  |
| E3/100 | full cycle | Cel.NET | 3 | 1: 6.57; 2: 6.16; 3: 6.11 | 6.16 | 1.08 | GPCel-Input, GreatPartsCel | 31.57 us | 66.23 KB | yes |  |
| E3/100 | full cycle | TelusCel | 3 | 1: 93.22; 2: 83.07; 3: 75.08 | 83.07 | 1.24 | GPCel-Input, GreatPartsCel | 421.98 us | 599.24 KB | yes |  |
| E3/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.19 us | 11.36 KB | no |  |
| E3/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.08 us | 10.86 KB | no |  |
| E3/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 5.87 us | 10.93 KB | no |  |
| E4/10 | compile | Cel.NET | 3 | 1: 10.93; 2: 10.43; 3: 10.14 | 10.43 | 1.08 | GPCel-Input, GreatPartsCel | 97.50 us | 145.79 KB | yes |  |
| E4/10 | compile | TelusCel | 3 | 1: 1.65; 2: 1.62; 3: 1.54 | 1.62 | 1.07 | GPCel-Input, GreatPartsCel | 15.05 us | 21.24 KB | yes |  |
| E4/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 9.46 us | 19.08 KB | no |  |
| E4/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 9.85 us | 18.67 KB | no |  |
| E4/10 | evaluate | Cel.NET | 3 | 1: 3.02; 2: 3.09; 3: 2.79 | 3.02 | 1.11 | GPCel-Input, GreatPartsCel | 5.19 us | 6.74 KB | yes |  |
| E4/10 | evaluate | TelusCel | 3 | 1: 39.61; 2: 41.61; 3: 35.55 | 39.61 | 1.17 | GPCel-Input, GreatPartsCel | 68.22 us | 99.07 KB | yes |  |
| E4/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.81 us | 872 B | no |  |
| E4/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.74 us | 872 B | no |  |
| E4/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 2.05 us | 664 B | no |  |
| E4/10 | full cycle | Cel.NET | 3 | 1: 9.38; 2: 9.60; 3: 9.03 | 9.38 | 1.06 | GPCel-Input, GreatPartsCel | 108.30 us | 152.52 KB | yes |  |
| E4/10 | full cycle | TelusCel | 3 | 1: 7.31; 2: 7.68; 3: 6.78 | 7.31 | 1.13 | GPCel-Input, GreatPartsCel | 84.49 us | 120.31 KB | yes |  |
| E4/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 11.89 us | 19.93 KB | no |  |
| E4/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 11.55 us | 19.52 KB | no |  |
| E4/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 12.73 us | 19.58 KB | no |  |
| E4/100 | compile | Cel.NET | 3 | 1: 10.43; 2: 10.36; 3: 10.36 | 10.36 | 1.01 | GPCel-Input, GreatPartsCel | 98.34 us | 145.78 KB | yes |  |
| E4/100 | compile | TelusCel | 3 | 1: 1.59; 2: 1.65; 3: 1.53 | 1.59 | 1.08 | GPCel-Input, GreatPartsCel | 14.96 us | 21.24 KB | yes |  |
| E4/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 9.62 us | 19.08 KB | no |  |
| E4/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 9.43 us | 18.67 KB | no |  |
| E4/100 | evaluate | Cel.NET | 3 | 1: 3.24; 2: 3.29; 3: 3.22 | 3.24 | 1.02 | GPCel-Input | 60.48 us | 92.62 KB | yes |  |
| E4/100 | evaluate | TelusCel | 3 | 1: 45.44; 2: 44.68; 3: 42.12 | 44.68 | 1.08 | GPCel-Input | 827.01 us | 1.15 MB | yes |  |
| E4/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 19.78 us | 9.60 KB | no |  |
| E4/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 18.67 us | 9.60 KB | no |  |
| E4/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 20.39 us | 9.40 KB | no |  |
| E4/100 | full cycle | Cel.NET | 3 | 1: 5.65; 2: 5.62; 3: 5.74 | 5.65 | 1.02 | GPCel-Input, GreatPartsCel | 162.81 us | 238.40 KB | yes |  |
| E4/100 | full cycle | TelusCel | 3 | 1: 29.91; 2: 30.02; 3: 27.80 | 29.91 | 1.08 | GPCel-Input, GreatPartsCel | 853.19 us | 1.17 MB | yes |  |
| E4/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 29.34 us | 28.69 KB | no |  |
| E4/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 28.81 us | 28.27 KB | no |  |
| E4/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 30.82 us | 28.33 KB | no |  |
| E5 | compile | Cel.NET | 3 | 1: 6.92; 2: 7.38; 3: 7.03 | 7.03 | 1.07 | GPCel-Input | 15.30 us | 32.81 KB | yes |  |
| E5 | compile | TelusCel | 3 | 1: 2.50; 2: 2.69; 3: 2.34 | 2.50 | 1.15 | GPCel-Input | 5.47 us | 8.63 KB | yes |  |
| E5 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.50 us | 6.53 KB | no |  |
| E5 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.19 us | 5.59 KB | no |  |
| E5 | evaluate | Cel.NET | 3 | 1: 207.26; 2: 226.52; 3: 229.42 | 226.52 | 1.11 | GPCel-Input | 67.51 us | 252.48 KB | yes |  |
| E5 | evaluate | TelusCel | 3 | 1: 27.95; 2: 29.46; 3: 26.97 | 27.95 | 1.09 | GPCel-Input | 8.78 us | 14.50 KB | yes |  |
| E5 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 412.2 ns | 320 B | no |  |
| E5 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 310.1 ns | 320 B | no |  |
| E5 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 516.9 ns | 160 B | no |  |
| E5 | full cycle | Cel.NET | 3 | 1: 33.06; 2: 33.94; 3: 35.21 | 33.94 | 1.07 | GPCel-Input | 86.40 us | 285.29 KB | yes |  |
| E5 | full cycle | TelusCel | 3 | 1: 5.75; 2: 5.97; 3: 5.52 | 5.75 | 1.08 | GPCel-Input | 15.03 us | 23.13 KB | yes |  |
| E5 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.11 us | 6.84 KB | no |  |
| E5 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.58 us | 5.90 KB | no |  |
| E5 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.31 us | 6.01 KB | no |  |
| E6 | compile | Cel.NET | 3 | 1: 10.41; 2: 10.15; 3: 10.39 | 10.39 | 1.02 | GPCel-Input | 32.98 us | 55.59 KB | yes |  |
| E6 | compile | TelusCel | 3 | 1: 3.11; 2: 3.15; 3: 2.92 | 3.11 | 1.08 | GPCel-Input | 9.85 us | 13.95 KB | yes |  |
| E6 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.44 us | 9.30 KB | no |  |
| E6 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.18 us | 6.55 KB | no |  |
| E6 | evaluate | Cel.NET | 3 | 1: 1.87; 2: 1.80; 3: 1.89 | 1.87 | 1.05 | GPCel-Input | 637.3 ns | 1.58 KB | yes |  |
| E6 | evaluate | TelusCel | 3 | 1: 35.53; 2: 37.15; 3: 35.68 | 35.68 | 1.05 | GPCel-Input | 12.12 us | 18.42 KB | yes |  |
| E6 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 383.3 ns | 320 B | no |  |
| E6 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 336.7 ns | 320 B | no |  |
| E6 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 531.3 ns | 160 B | no |  |
| E6 | full cycle | Cel.NET | 3 | 1: 9.46; 2: 9.57; 3: 9.19 | 9.46 | 1.04 | GPCel-Input | 35.09 us | 57.17 KB | yes |  |
| E6 | full cycle | TelusCel | 3 | 1: 6.20; 2: 6.53; 3: 6.06 | 6.20 | 1.08 | GPCel-Input | 23.22 us | 32.38 KB | yes |  |
| E6 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.85 us | 9.62 KB | no |  |
| E6 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.75 us | 6.87 KB | no |  |
| E6 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.32 us | 7.02 KB | no |  |
| E7 | compile | Cel.NET | 3 | 1: 9.37; 2: 9.94; 3: 10.12 | 9.94 | 1.08 | GPCel-Input | 31.75 us | 58.25 KB | yes |  |
| E7 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.54 us | 7.68 KB | no |  |
| E7 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.19 us | 7.24 KB | no |  |
| E7 | evaluate | Cel.NET | 3 | 1: 2.52; 2: 2.58; 3: 2.57 | 2.57 | 1.03 | GPCel-Input | 727.5 ns | 1.63 KB | yes |  |
| E7 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 386.6 ns | 320 B | no |  |
| E7 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 282.8 ns | 320 B | no |  |
| E7 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 489.2 ns | 160 B | no |  |
| E7 | full cycle | Cel.NET | 3 | 1: 9.05; 2: 9.43; 3: 9.34 | 9.34 | 1.04 | GPCel-Input | 33.68 us | 59.87 KB | yes |  |
| E7 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.20 us | 7.99 KB | no |  |
| E7 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.61 us | 7.55 KB | no |  |
| E7 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.24 us | 7.53 KB | no |  |
| E8 | compile | Cel.NET | 3 | 1: 15.24; 2: 14.75; 3: 15.52 | 15.24 | 1.05 | GPCel-Input | 73.35 us | 110.92 KB | yes |  |
| E8 | compile | TelusCel | 3 | 1: 2.73; 2: 2.70; 3: 2.67 | 2.70 | 1.02 | GPCel-Input | 13.28 us | 19.38 KB | yes |  |
| E8 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.62 us | 12.74 KB | no |  |
| E8 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.86 us | 11.09 KB | no |  |
| E8 | evaluate | Cel.NET | 3 | 1: 1.60; 2: 1.60; 3: 1.69 | 1.60 | 1.06 | GPCel-Input | 612.3 ns | 1.63 KB | yes |  |
| E8 | evaluate | TelusCel | 3 | 1: 27.19; 2: 28.73; 3: 27.49 | 27.49 | 1.06 | GPCel-Input | 10.60 us | 15.89 KB | yes |  |
| E8 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 474.3 ns | 368 B | no |  |
| E8 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 368.9 ns | 368 B | no |  |
| E8 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.02; 3: 1.00 | 1.00 | 1.02 | GPCel-Simple, Cel.NET | 583.2 ns | 160 B | no |  |
| E8 | full cycle | Cel.NET | 3 | 1: 13.64; 2: 13.02; 3: 14.19 | 13.64 | 1.09 | GPCel-Input | 74.81 us | 112.54 KB | yes |  |
| E8 | full cycle | TelusCel | 3 | 1: 4.44; 2: 4.61; 3: 4.54 | 4.54 | 1.04 | GPCel-Input | 24.80 us | 35.27 KB | yes |  |
| E8 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.17 us | 13.10 KB | no |  |
| E8 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.59 us | 11.45 KB | no |  |
| E8 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 6.03 us | 11.53 KB | no |  |
| E9 | compile | Cel.NET | 3 | 1: 13.53; 2: 12.74; 3: 13.83 | 13.53 | 1.09 | GPCel-Input | 49.09 us | 83.86 KB | yes |  |
| E9 | compile | TelusCel | 3 | 1: 1.87; 2: 1.87; 3: 1.91 | 1.87 | 1.02 | GPCel-Input | 6.91 us | 10.06 KB | yes |  |
| E9 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.39 us | 10.12 KB | no |  |
| E9 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.70 us | 8.54 KB | no |  |
| E9 | evaluate | Cel.NET | 3 | 1: 1.66; 2: 1.54; 3: 1.68 | 1.66 | 1.09 | GPCel-Input | 578.1 ns | 1.73 KB | yes |  |
| E9 | evaluate | TelusCel | 3 | 1: 19.14; 2: 18.58; 3: 20.57 | 19.14 | 1.11 | GPCel-Input | 6.81 us | 10.99 KB | yes |  |
| E9 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 433.4 ns | 504 B | no |  |
| E9 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 347.4 ns | 504 B | no |  |
| E9 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 563.3 ns | 344 B | no |  |
| E9 | full cycle | Cel.NET | 3 | 1: 12.01; 2: 11.31; 3: 12.27 | 12.01 | 1.09 | GPCel-Input | 50.11 us | 85.60 KB | yes |  |
| E9 | full cycle | TelusCel | 3 | 1: 3.33; 2: 3.41; 3: 3.42 | 3.41 | 1.03 | GPCel-Input | 13.97 us | 21.05 KB | yes |  |
| E9 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.88 us | 10.61 KB | no |  |
| E9 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.18 us | 9.04 KB | no |  |
| E9 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.70 us | 9.16 KB | no |  |

R above 1.1 (all libraries): E1, compile, Cel.NET: 9.91; E1, compile, TelusCel: 2.31; E1, evaluate, Cel.NET: 1.62; E1, evaluate, TelusCel: 21.18; E1, full cycle, Cel.NET: 8.19; E1, full cycle, TelusCel: 3.96; E2/10, compile, Cel.NET: 6.37; E2/10, compile, TelusCel: 1.92; E2/10, evaluate, Cel.NET: 2.00; E2/10, evaluate, TelusCel: 30.71; E2/10, full cycle, Cel.NET: 5.02; E2/10, full cycle, TelusCel: 10.34; E2/100, compile, Cel.NET: 6.79; E2/100, compile, TelusCel: 1.96; E2/100, evaluate, Cel.NET: 2.02; E2/100, evaluate, TelusCel: 33.95; E2/100, full cycle, Cel.NET: 2.86; E2/100, full cycle, TelusCel: 26.31; E3/10, compile, Cel.NET: 6.04; E3/10, compile, TelusCel: 1.94; E3/10, evaluate, Cel.NET: 2.20; E3/10, evaluate, TelusCel: 92.54; E3/10, full cycle, Cel.NET: 5.71; E3/10, full cycle, TelusCel: 10.27; E3/100, compile, Cel.NET: 6.45; E3/100, compile, TelusCel: 1.95; E3/100, evaluate, Cel.NET: 4.87; E3/100, evaluate, TelusCel: 886.37; E3/100, full cycle, Cel.NET: 6.16; E3/100, full cycle, TelusCel: 83.07; E4/10, compile, Cel.NET: 10.43; E4/10, compile, TelusCel: 1.62; E4/10, evaluate, Cel.NET: 3.02; E4/10, evaluate, TelusCel: 39.61; E4/10, full cycle, Cel.NET: 9.38; E4/10, full cycle, TelusCel: 7.31; E4/100, compile, Cel.NET: 10.36; E4/100, compile, TelusCel: 1.59; E4/100, evaluate, Cel.NET: 3.24; E4/100, evaluate, TelusCel: 44.68; E4/100, full cycle, Cel.NET: 5.65; E4/100, full cycle, TelusCel: 29.91; E5, compile, Cel.NET: 7.03; E5, compile, TelusCel: 2.50; E5, evaluate, Cel.NET: 226.52; E5, evaluate, TelusCel: 27.95; E5, full cycle, Cel.NET: 33.94; E5, full cycle, TelusCel: 5.75; E6, compile, Cel.NET: 10.39; E6, compile, TelusCel: 3.11; E6, evaluate, Cel.NET: 1.87; E6, evaluate, TelusCel: 35.68; E6, full cycle, Cel.NET: 9.46; E6, full cycle, TelusCel: 6.20; E7, compile, Cel.NET: 9.94; E7, evaluate, Cel.NET: 2.57; E7, full cycle, Cel.NET: 9.34; E8, compile, Cel.NET: 15.24; E8, compile, TelusCel: 2.70; E8, evaluate, Cel.NET: 1.60; E8, evaluate, TelusCel: 27.49; E8, full cycle, Cel.NET: 13.64; E8, full cycle, TelusCel: 4.54; E9, compile, Cel.NET: 13.53; E9, compile, TelusCel: 1.87; E9, evaluate, Cel.NET: 1.66; E9, evaluate, TelusCel: 19.14; E9, full cycle, Cel.NET: 12.01; E9, full cycle, TelusCel: 3.41.

**Rows of GreatPartsCel.**

- R above 1.1: none.
- Ratios on both sides of 1.1: none.
