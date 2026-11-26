# Benchmark rig

- Date and time of the report (UTC): 2026-11-26 14:07:54
- Commit of great-parts/great-parts-cel: [`494b71f8cee1f54e080f1179f695ce502daad419`](https://github.com/great-parts/great-parts-cel/commit/494b71f8cee1f54e080f1179f695ce502daad419)
- Runs: 3
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45781257503 (GitHub keeps a run for 90 days; this report does not depend on it)

The summary of the rig follows. The full reports of BenchmarkDotNet are in the artifact `bench-results` of the run, for 90 days.

## Conditions

- Date of the summary: 2026-11-26T14:07:54.575Z
- Commit: 494b71f8cee1f54e080f1179f695ce502daad419
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45781257503
- Runner: Linux X64, image ubuntu26 20261122.9.1
- Great.Parts.Cel: built from the sources of this commit
- Packages: BenchmarkDotNet 0.16.0-preview.2; Celly 1.2.0; DotnetCel 1.0.7; Cel.NET 2.3.1; Cel 0.3.3; Grpc.Tools 2.85.0

## Verification

Correct: all three variants gave the expected value: the original text (compile and evaluate), the text with a tail (compile and evaluate), the text with a tail (full cycle).

| Scenario | Expression | Lines | Expected | Celly | DotnetCel | Cel.NET | TelusCel | GreatPartsCel | GPCel-Input | GPCel-Simple |
|---|---|--:|---|---|---|---|---|---|---|---|
| E1 | `order.total > 1000.0 && order.status == 'approved'` | 10 | bool:true | correct | correct | correct | correct | correct | correct | correct |
| E2/10 | `order.lines.all(l, l.qty > 0 && l.price >= 0.0)` | 10 | bool:true | correct | correct | correct | correct | correct | correct | correct |
| E2/100 | `order.lines.all(l, l.qty > 0 && l.price >= 0.0)` | 100 | bool:true | correct | correct | correct | correct | correct | correct | correct |
| E3/10 | `order.lines.exists(l, l.sku.startsWith('SKU-'))` | 10 | bool:true | correct | correct | correct | correct | correct | correct | correct |
| E3/100 | `order.lines.exists(l, l.sku.startsWith('SKU-'))` | 100 | bool:true | correct | correct | correct | correct | correct | correct | correct |
| E4/10 | `order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)` | 10 | [double:208,double:280,double:361] | correct | correct | correct | correct | correct | correct | correct |
| E4/100 | `order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)` | 100 | [double:208,double:280,double:361,double... | correct | correct | correct | correct | correct | correct | correct |
| E5 | `order.customer.email.matches('^[^@]+@[^@]+[.][a-z]+$')` | 10 | bool:true | correct | correct | correct | correct | correct | correct | correct |
| E6 | `has(order.discount) ? order.total - order.discount : order.total` | 10 | double:1400 | correct | wrong (3 of 3): ERROR(CelEvaluationException: runtime type mismatch in subtract_... | correct | correct | correct | correct | correct |
| E7 | `order.created + duration('72h') > now` | 10 | bool:true | correct | correct | correct | wrong (3 of 3): ERROR(CelNoSuchOverloadException: No overload exists to ADD type... | correct | correct | correct |
| E8 | `size(order.lines) <= 100 && order.currency in ['RUB', 'USD', 'EUR']` | 10 | bool:true | correct | wrong (3 of 3): ERROR(CelEvaluationException: runtime type mismatch in size_stri... | correct | correct | correct | correct | correct |
| E9 | `'Order ' + order.id + ': ' + order.status` | 10 | string:'Order ORD-0001: approved' | correct | correct | correct | correct | correct | correct | correct |

Excluded from the measurement (wrong result): DotnetCel - E6; TelusCel - E7; DotnetCel - E8.

## Measurement environment

- BenchmarkDotNet 0.16.0-preview.2; OS: Linux Ubuntu 26.04.1 LTS (Resolute Raccoon); processor: AMD EPYC 9V45
- Cores: 2 physical, 4 logical; architecture: X64
- Runtime: .NET 11.0.0 (11.0.0, 11.0.26.53018); SDK: 11.0.100; configuration: RELEASE

## Runs

- Run 1: 2026-11-26T08:13:25.977Z - 2026-11-26T10:03:01.122Z; job: Default; cases: all; filter: *
- Run 2: 2026-11-26T10:03:01.123Z - 2026-11-26T11:49:16.351Z; job: Default; cases: all; filter: *
- Run 3: 2026-11-26T11:49:16.351Z - 2026-11-26T13:35:09.382Z; job: Default; cases: all; filter: *

The table of each run is `results/runs/<k>/summary.md`.

## Summary over the runs

The ratio of a row in a run is to the fastest library with a correct result in the same run (for a variant of GreatPartsCel: to the fastest other library or to itself, not to its other variants). R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has 3 runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others. "Allocated" is the median over the runs of the bytes allocated per operation that BenchmarkDotNet reports (MemoryDiagnoser), less the allocation of making the text for compile and full cycle, as the time is.

| Scenario | Operation | Library | Runs | Ratios by run | R | Spread | Fastest by run | Median time over the runs | Allocated | R > 1.1 | Both sides of 1.1 |
|---|---|---|--:|---|--:|--:|---|--:|--:|---|---|
| E1 | compile | Celly | 3 | 1: 3.82; 2: 3.48; 3: 3.58 | 3.58 | 1.10 | GPCel-Input | 8.28 us | 42.81 KB | yes |  |
| E1 | compile | DotnetCel | 3 | 1: 2.96; 2: 2.49; 3: 2.42 | 2.49 | 1.22 | GPCel-Input | 5.84 us | 26.31 KB | yes |  |
| E1 | compile | Cel.NET | 3 | 1: 7.28; 2: 6.58; 3: 7.07 | 7.07 | 1.11 | GPCel-Input | 16.36 us | 49.87 KB | yes |  |
| E1 | compile | TelusCel | 3 | 1: 1.88; 2: 1.96; 3: 1.84 | 1.88 | 1.06 | GPCel-Input | 4.27 us | 10.33 KB | yes |  |
| E1 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.76 us | 8.85 KB | no |  |
| E1 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.31 us | 6.97 KB | no |  |
| E1 | evaluate | Celly | 3 | 1: 1.67; 2: 1.41; 3: 1.61 | 1.61 | 1.18 | GPCel-Input | 241.7 ns | 832 B | yes |  |
| E1 | evaluate | DotnetCel | 3 | 1: 1.67; 2: 1.47; 3: 1.62 | 1.62 | 1.14 | GPCel-Input | 242.0 ns | 560 B | yes |  |
| E1 | evaluate | Cel.NET | 3 | 1: 2.00; 2: 1.83; 3: 1.91 | 1.91 | 1.10 | GPCel-Input | 286.6 ns | 1.52 KB | yes |  |
| E1 | evaluate | TelusCel | 3 | 1: 29.76; 2: 35.34; 3: 30.80 | 30.80 | 1.19 | GPCel-Input | 4.61 us | 11.16 KB | yes |  |
| E1 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 194.5 ns | 288 B | no |  |
| E1 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 149.7 ns | 288 B | no |  |
| E1 | evaluate | GPCel-Simple | 3 | 1: 1.06; 2: 1.14; 3: 1.03 | 1.06 | 1.10 | Celly | 251.2 ns | 160 B | no | yes |
| E1 | full cycle | Celly | 3 | 1: 3.89; 2: 2.99; 3: 3.44 | 3.44 | 1.30 | GPCel-Input | 8.65 us | 43.62 KB | yes |  |
| E1 | full cycle | DotnetCel | 3 | 1: 3.38; 2: 2.83; 3: 2.96 | 2.96 | 1.20 | GPCel-Input | 7.45 us | 29.37 KB | yes |  |
| E1 | full cycle | Cel.NET | 3 | 1: 6.91; 2: 6.13; 3: 6.24 | 6.24 | 1.13 | GPCel-Input | 15.68 us | 51.40 KB | yes |  |
| E1 | full cycle | TelusCel | 3 | 1: 3.94; 2: 3.83; 3: 3.59 | 3.83 | 1.10 | GPCel-Input | 9.80 us | 21.49 KB | yes |  |
| E1 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.21 us | 9.15 KB | no |  |
| E1 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.51 us | 7.28 KB | no |  |
| E1 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 2.92 us | 7.40 KB | no |  |
| E2/10 | compile | Celly | 3 | 1: 2.99; 2: 2.71; 3: 2.70 | 2.71 | 1.11 | GPCel-Input, GreatPartsCel | 10.44 us | 54.53 KB | yes |  |
| E2/10 | compile | DotnetCel | 3 | 1: 2.20; 2: 2.10; 3: 1.95 | 2.10 | 1.13 | GPCel-Input, GreatPartsCel | 7.72 us | 32.52 KB | yes |  |
| E2/10 | compile | Cel.NET | 3 | 1: 5.68; 2: 5.97; 3: 5.57 | 5.68 | 1.07 | GPCel-Input, GreatPartsCel | 21.94 us | 71.99 KB | yes |  |
| E2/10 | compile | TelusCel | 3 | 1: 1.54; 2: 1.69; 3: 1.73 | 1.69 | 1.12 | GPCel-Input, GreatPartsCel | 6.22 us | 15.50 KB | yes |  |
| E2/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.04 us | 12.23 KB | no |  |
| E2/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.87 us | 11.57 KB | no |  |
| E2/10 | evaluate | Celly | 3 | 1: 2.00; 2: 2.03; 3: 2.01 | 2.01 | 1.01 | GPCel-Input | 2.09 us | 2.96 KB | yes |  |
| E2/10 | evaluate | DotnetCel | 3 | 1: 2.98; 2: 2.92; 3: 2.91 | 2.92 | 1.02 | GPCel-Input | 3.01 us | 4.07 KB | yes |  |
| E2/10 | evaluate | Cel.NET | 3 | 1: 2.36; 2: 2.30; 3: 2.32 | 2.32 | 1.03 | GPCel-Input | 2.37 us | 6.15 KB | yes |  |
| E2/10 | evaluate | TelusCel | 3 | 1: 49.23; 2: 47.34; 3: 57.70 | 49.23 | 1.22 | GPCel-Input | 51.77 us | 111.34 KB | yes |  |
| E2/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.11 us | 336 B | no |  |
| E2/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.03 us | 336 B | no |  |
| E2/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 1.24 us | 160 B | no |  |
| E2/10 | full cycle | Celly | 3 | 1: 2.83; 2: 2.31; 3: 2.32 | 2.32 | 1.22 | GPCel-Input, GreatPartsCel | 12.10 us | 57.49 KB | yes |  |
| E2/10 | full cycle | DotnetCel | 3 | 1: 2.68; 2: 2.52; 3: 2.40 | 2.52 | 1.11 | GPCel-Input, GreatPartsCel | 13.00 us | 39.90 KB | yes |  |
| E2/10 | full cycle | Cel.NET | 3 | 1: 5.03; 2: 4.83; 3: 4.54 | 4.83 | 1.11 | GPCel-Input, GreatPartsCel | 24.95 us | 78.13 KB | yes |  |
| E2/10 | full cycle | TelusCel | 3 | 1: 11.42; 2: 10.61; 3: 11.03 | 11.03 | 1.08 | GPCel-Input, GreatPartsCel | 57.51 us | 126.84 KB | yes |  |
| E2/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.46 us | 12.59 KB | no |  |
| E2/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.21 us | 11.93 KB | no |  |
| E2/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 5.51 us | 11.99 KB | no |  |
| E2/100 | compile | Celly | 3 | 1: 3.21; 2: 2.50; 3: 2.58 | 2.58 | 1.28 | GPCel-Input, GreatPartsCel | 10.02 us | 54.53 KB | yes |  |
| E2/100 | compile | DotnetCel | 3 | 1: 1.94; 2: 1.95; 3: 2.00 | 1.95 | 1.03 | GPCel-Input, GreatPartsCel | 7.45 us | 32.52 KB | yes |  |
| E2/100 | compile | Cel.NET | 3 | 1: 5.65; 2: 5.90; 3: 5.45 | 5.65 | 1.08 | GPCel-Input, GreatPartsCel | 21.75 us | 71.98 KB | yes |  |
| E2/100 | compile | TelusCel | 3 | 1: 1.70; 2: 1.69; 3: 1.74 | 1.70 | 1.03 | GPCel-Input, GreatPartsCel | 6.55 us | 15.50 KB | yes |  |
| E2/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.14 us | 12.22 KB | no |  |
| E2/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.88 us | 11.57 KB | no |  |
| E2/100 | evaluate | Celly | 3 | 1: 1.83; 2: 1.75; 3: 2.00 | 1.83 | 1.14 | GPCel-Input | 16.74 us | 21.66 KB | yes |  |
| E2/100 | evaluate | DotnetCel | 3 | 1: 2.91; 2: 3.09; 3: 2.88 | 2.91 | 1.07 | GPCel-Input | 26.61 us | 31.91 KB | yes |  |
| E2/100 | evaluate | Cel.NET | 3 | 1: 2.34; 2: 2.44; 3: 2.33 | 2.34 | 1.05 | GPCel-Input | 21.40 us | 54.28 KB | yes |  |
| E2/100 | evaluate | TelusCel | 3 | 1: 59.54; 2: 50.55; 3: 53.03 | 53.03 | 1.18 | GPCel-Input | 472.69 us | 1.03 MB | yes |  |
| E2/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 9.49 us | 336 B | no |  |
| E2/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 9.05 us | 336 B | no |  |
| E2/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 10.04 us | 160 B | no |  |
| E2/100 | full cycle | Celly | 3 | 1: 2.27; 2: 1.82; 3: 2.02 | 2.02 | 1.24 | GPCel-Input, GreatPartsCel | 27.52 us | 76.19 KB | yes |  |
| E2/100 | full cycle | DotnetCel | 3 | 1: 2.72; 2: 2.65; 3: 2.58 | 2.65 | 1.05 | GPCel-Input, GreatPartsCel | 35.23 us | 67.74 KB | yes |  |
| E2/100 | full cycle | Cel.NET | 3 | 1: 3.45; 2: 3.25; 3: 3.25 | 3.25 | 1.06 | GPCel-Input, GreatPartsCel | 44.39 us | 126.26 KB | yes |  |
| E2/100 | full cycle | TelusCel | 3 | 1: 38.30; 2: 34.59; 3: 39.26 | 38.30 | 1.13 | GPCel-Input, GreatPartsCel | 507.84 us | 1.05 MB | yes |  |
| E2/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 13.64 us | 12.58 KB | no |  |
| E2/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 13.39 us | 11.93 KB | no |  |
| E2/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 14.62 us | 11.99 KB | no |  |
| E3/10 | compile | Celly | 3 | 1: 2.95; 2: 2.35; 3: 2.84 | 2.84 | 1.26 | GPCel-Input | 9.05 us | 44.35 KB | yes |  |
| E3/10 | compile | DotnetCel | 3 | 1: 2.23; 2: 2.04; 3: 2.18 | 2.18 | 1.09 | GPCel-Input | 6.84 us | 30.26 KB | yes |  |
| E3/10 | compile | Cel.NET | 3 | 1: 5.66; 2: 4.82; 3: 5.37 | 5.37 | 1.18 | GPCel-Input | 17.38 us | 53.13 KB | yes |  |
| E3/10 | compile | TelusCel | 3 | 1: 1.83; 2: 1.49; 3: 1.61 | 1.61 | 1.23 | GPCel-Input | 5.22 us | 13.38 KB | yes |  |
| E3/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.49 us | 10.81 KB | no |  |
| E3/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.24 us | 10.32 KB | no |  |
| E3/10 | evaluate | Celly | 3 | 1: 2.63; 2: 2.38; 3: 2.50 | 2.50 | 1.10 | GPCel-Input | 545.5 ns | 1.61 KB | yes |  |
| E3/10 | evaluate | DotnetCel | 3 | 1: 3.04; 2: 3.06; 3: 2.97 | 3.04 | 1.03 | GPCel-Input | 655.1 ns | 1.78 KB | yes |  |
| E3/10 | evaluate | Cel.NET | 3 | 1: 2.83; 2: 2.53; 3: 2.79 | 2.79 | 1.12 | GPCel-Input | 609.0 ns | 2.07 KB | yes |  |
| E3/10 | evaluate | TelusCel | 3 | 1: 134.78; 2: 138.16; 3: 122.00 | 134.78 | 1.13 | GPCel-Input | 29.02 us | 62.83 KB | yes |  |
| E3/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 283.8 ns | 336 B | no |  |
| E3/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 218.1 ns | 336 B | no |  |
| E3/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 330.3 ns | 160 B | no |  |
| E3/10 | full cycle | Celly | 3 | 1: 2.73; 2: 2.70; 3: 2.59 | 2.70 | 1.05 | GPCel-Input, GPCel-Simple | 9.44 us | 45.93 KB | yes |  |
| E3/10 | full cycle | DotnetCel | 3 | 1: 2.74; 2: 2.53; 3: 2.59 | 2.59 | 1.08 | GPCel-Input, GPCel-Simple | 9.41 us | 35.35 KB | yes |  |
| E3/10 | full cycle | Cel.NET | 3 | 1: 5.30; 2: 4.61; 3: 5.05 | 5.05 | 1.15 | GPCel-Input, GPCel-Simple | 18.19 us | 55.20 KB | yes |  |
| E3/10 | full cycle | TelusCel | 3 | 1: 10.03; 2: 8.68; 3: 9.06 | 9.06 | 1.16 | GPCel-Input, GPCel-Simple | 32.99 us | 76.20 KB | yes |  |
| E3/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.98 us | 11.17 KB | no |  |
| E3/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.72 us | 10.68 KB | no |  |
| E3/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.76 us | 10.74 KB | no |  |
| E3/100 | compile | Celly | 3 | 1: 2.86; 2: 2.59; 3: 2.79 | 2.79 | 1.11 | GPCel-Input | 8.70 us | 44.33 KB | yes |  |
| E3/100 | compile | DotnetCel | 3 | 1: 2.28; 2: 2.10; 3: 2.10 | 2.10 | 1.09 | GPCel-Input | 6.70 us | 30.26 KB | yes |  |
| E3/100 | compile | Cel.NET | 3 | 1: 5.90; 2: 5.13; 3: 5.65 | 5.65 | 1.15 | GPCel-Input | 17.92 us | 53.12 KB | yes |  |
| E3/100 | compile | TelusCel | 3 | 1: 1.82; 2: 1.59; 3: 1.57 | 1.59 | 1.16 | GPCel-Input | 5.01 us | 13.38 KB | yes |  |
| E3/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.46 us | 10.81 KB | no |  |
| E3/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.12 us | 10.32 KB | no |  |
| E3/100 | evaluate | Celly | 3 | 1: 12.65; 2: 11.97; 3: 12.38 | 12.38 | 1.06 | GPCel-Input | 2.67 us | 6.95 KB | yes |  |
| E3/100 | evaluate | DotnetCel | 3 | 1: 10.94; 2: 10.62; 3: 11.19 | 10.94 | 1.05 | GPCel-Input | 2.36 us | 7.12 KB | yes |  |
| E3/100 | evaluate | Cel.NET | 3 | 1: 6.57; 2: 6.22; 3: 7.54 | 6.57 | 1.21 | GPCel-Input | 1.42 us | 8.81 KB | yes |  |
| E3/100 | evaluate | TelusCel | 3 | 1: 1232.41; 2: 1260.59; 3: 1137.27 | 1232.41 | 1.11 | GPCel-Input | 266.01 us | 570.20 KB | yes |  |
| E3/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 270.4 ns | 336 B | no |  |
| E3/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 215.5 ns | 336 B | no |  |
| E3/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 327.8 ns | 160 B | no |  |
| E3/100 | full cycle | Celly | 3 | 1: 3.42; 2: 3.03; 3: 3.28 | 3.28 | 1.13 | GPCel-Input | 10.92 us | 51.28 KB | yes |  |
| E3/100 | full cycle | DotnetCel | 3 | 1: 3.41; 2: 3.38; 3: 3.27 | 3.38 | 1.04 | GPCel-Input | 11.44 us | 40.69 KB | yes |  |
| E3/100 | full cycle | Cel.NET | 3 | 1: 5.78; 2: 5.02; 3: 6.01 | 5.78 | 1.20 | GPCel-Input | 19.44 us | 61.93 KB | yes |  |
| E3/100 | full cycle | TelusCel | 3 | 1: 93.15; 2: 83.40; 3: 77.83 | 83.40 | 1.20 | GPCel-Input | 282.28 us | 583.57 KB | yes |  |
| E3/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.94 us | 11.17 KB | no |  |
| E3/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.36 us | 10.69 KB | no |  |
| E3/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.65 us | 10.74 KB | no |  |
| E4/10 | compile | Celly | 3 | 1: 2.79; 2: 2.61; 3: 2.53 | 2.61 | 1.11 | GreatPartsCel, GPCel-Input | 16.30 us | 75.71 KB | yes |  |
| E4/10 | compile | DotnetCel | 3 | 1: 1.76; 2: 1.64; 3: 1.62 | 1.64 | 1.09 | GreatPartsCel, GPCel-Input | 10.22 us | 37.80 KB | yes |  |
| E4/10 | compile | Cel.NET | 3 | 1: 8.50; 2: 7.41; 3: 8.48 | 8.48 | 1.15 | GreatPartsCel, GPCel-Input | 51.16 us | 134.65 KB | yes |  |
| E4/10 | compile | TelusCel | 3 | 1: 1.54; 2: 1.57; 3: 1.42 | 1.54 | 1.11 | GreatPartsCel, GPCel-Input | 9.29 us | 21.26 KB | yes |  |
| E4/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.89 us | 18.86 KB | no |  |
| E4/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.27 us | 18.45 KB | no |  |
| E4/10 | evaluate | Celly | 3 | 1: 2.70; 2: 2.74; 3: 2.57 | 2.70 | 1.07 | GPCel-Input | 2.14 us | 4.08 KB | yes |  |
| E4/10 | evaluate | DotnetCel | 3 | 1: 3.66; 2: 3.64; 3: 3.67 | 3.66 | 1.01 | GPCel-Input | 2.85 us | 5.31 KB | yes |  |
| E4/10 | evaluate | Cel.NET | 3 | 1: 3.46; 2: 3.47; 3: 3.85 | 3.47 | 1.11 | GPCel-Input | 2.79 us | 7.08 KB | yes |  |
| E4/10 | evaluate | TelusCel | 3 | 1: 66.02; 2: 67.04; 3: 53.27 | 66.02 | 1.26 | GPCel-Input | 52.39 us | 98.86 KB | yes |  |
| E4/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 910.3 ns | 840 B | no |  |
| E4/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 781.4 ns | 840 B | no |  |
| E4/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 994.4 ns | 664 B | no |  |
| E4/10 | full cycle | Celly | 3 | 1: 2.86; 2: 2.73; 3: 2.63 | 2.73 | 1.09 | GreatPartsCel, GPCel-Input | 19.46 us | 79.81 KB | yes |  |
| E4/10 | full cycle | DotnetCel | 3 | 1: 2.30; 2: 2.09; 3: 2.20 | 2.20 | 1.10 | GreatPartsCel, GPCel-Input | 14.98 us | 46.42 KB | yes |  |
| E4/10 | full cycle | Cel.NET | 3 | 1: 7.44; 2: 7.03; 3: 8.09 | 7.44 | 1.15 | GreatPartsCel, GPCel-Input | 51.08 us | 141.73 KB | yes |  |
| E4/10 | full cycle | TelusCel | 3 | 1: 7.94; 2: 8.31; 3: 8.21 | 8.21 | 1.05 | GreatPartsCel, GPCel-Input | 55.79 us | 120.12 KB | yes |  |
| E4/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 8.19 us | 19.71 KB | no |  |
| E4/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 7.12 us | 19.31 KB | no |  |
| E4/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 7.99 us | 19.37 KB | no |  |
| E4/100 | compile | Celly | 3 | 1: 2.98; 2: 2.81; 3: 2.68 | 2.81 | 1.11 | GreatPartsCel, GPCel-Input | 16.95 us | 75.72 KB | yes |  |
| E4/100 | compile | DotnetCel | 3 | 1: 1.69; 2: 1.69; 3: 1.75 | 1.69 | 1.04 | GreatPartsCel, GPCel-Input | 10.09 us | 37.80 KB | yes |  |
| E4/100 | compile | Cel.NET | 3 | 1: 8.43; 2: 7.67; 3: 8.57 | 8.43 | 1.12 | GreatPartsCel, GPCel-Input | 49.28 us | 134.67 KB | yes |  |
| E4/100 | compile | TelusCel | 3 | 1: 1.57; 2: 1.50; 3: 1.70 | 1.57 | 1.13 | GreatPartsCel, GPCel-Input | 9.30 us | 21.26 KB | yes |  |
| E4/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 7.02 us | 18.87 KB | no |  |
| E4/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.04 us | 18.45 KB | no |  |
| E4/100 | evaluate | Celly | 3 | 1: 2.41; 2: 2.58; 3: 2.50 | 2.50 | 1.07 | GreatPartsCel, GPCel-Input | 23.17 us | 42.44 KB | yes |  |
| E4/100 | evaluate | DotnetCel | 3 | 1: 3.45; 2: 3.65; 3: 3.54 | 3.54 | 1.06 | GreatPartsCel, GPCel-Input | 33.05 us | 70.80 KB | yes |  |
| E4/100 | evaluate | Cel.NET | 3 | 1: 3.33; 2: 3.45; 3: 3.54 | 3.45 | 1.06 | GreatPartsCel, GPCel-Input | 31.89 us | 96.53 KB | yes |  |
| E4/100 | evaluate | TelusCel | 3 | 1: 57.97; 2: 60.00; 3: 68.41 | 60.00 | 1.18 | GreatPartsCel, GPCel-Input | 554.82 us | 1.15 MB | yes |  |
| E4/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 10.25 us | 9.57 KB | no |  |
| E4/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 9.35 us | 9.57 KB | no |  |
| E4/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 9.99 us | 9.40 KB | no |  |
| E4/100 | full cycle | Celly | 3 | 1: 2.73; 2: 2.29; 3: 2.57 | 2.57 | 1.19 | GPCel-Input | 38.85 us | 118.14 KB | yes |  |
| E4/100 | full cycle | DotnetCel | 3 | 1: 2.93; 2: 2.82; 3: 2.94 | 2.93 | 1.04 | GPCel-Input | 45.50 us | 111.91 KB | yes |  |
| E4/100 | full cycle | Cel.NET | 3 | 1: 5.26; 2: 4.79; 3: 5.54 | 5.26 | 1.16 | GPCel-Input | 81.63 us | 231.19 KB | yes |  |
| E4/100 | full cycle | TelusCel | 3 | 1: 36.31; 2: 39.01; 3: 34.06 | 36.31 | 1.15 | GPCel-Input | 563.38 us | 1.17 MB | yes |  |
| E4/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 16.93 us | 28.46 KB | no |  |
| E4/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 15.52 us | 28.05 KB | no |  |
| E4/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 17.91 us | 28.12 KB | no |  |
| E5 | compile | Celly | 3 | 1: 3.90; 2: 3.77; 3: 4.82 | 3.90 | 1.28 | GPCel-Input | 6.15 us | 35.55 KB | yes |  |
| E5 | compile | DotnetCel | 3 | 1: 3.24; 2: 3.23; 3: 3.35 | 3.24 | 1.04 | GPCel-Input | 5.11 us | 24.02 KB | yes |  |
| E5 | compile | Cel.NET | 3 | 1: 5.69; 2: 5.55; 3: 6.66 | 5.69 | 1.20 | GPCel-Input | 8.98 us | 30.58 KB | yes |  |
| E5 | compile | TelusCel | 3 | 1: 2.18; 2: 1.93; 3: 2.16 | 2.16 | 1.13 | GPCel-Input | 3.18 us | 8.64 KB | yes |  |
| E5 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.96 us | 6.44 KB | no |  |
| E5 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.58 us | 5.50 KB | no |  |
| E5 | evaluate | Celly | 3 | 1: 1.49; 2: 1.48; 3: 1.71 | 1.49 | 1.15 | GPCel-Input | 249.6 ns | 752 B | yes |  |
| E5 | evaluate | DotnetCel | 3 | 1: 1.69; 2: 1.76; 3: 1.78 | 1.76 | 1.05 | GPCel-Input | 287.5 ns | 448 B | yes |  |
| E5 | evaluate | Cel.NET | 3 | 1: 262.36; 2: 263.37; 3: 285.64 | 263.37 | 1.09 | GPCel-Input | 44.36 us | 252.50 KB | yes |  |
| E5 | evaluate | TelusCel | 3 | 1: 35.69; 2: 30.91; 3: 34.03 | 34.03 | 1.15 | GPCel-Input | 5.50 us | 14.34 KB | yes |  |
| E5 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.04; 3: 1.00 | 1.00 | 1.04 | GreatPartsCel, Celly | 223.7 ns | 288 B | no |  |
| E5 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 166.4 ns | 288 B | no |  |
| E5 | evaluate | GPCel-Simple | 3 | 1: 1.04; 2: 1.09; 3: 1.00 | 1.04 | 1.09 | Celly, GPCel-Simple | 270.0 ns | 160 B | no |  |
| E5 | full cycle | Celly | 3 | 1: 3.49; 2: 3.45; 3: 4.10 | 3.49 | 1.19 | GPCel-Input | 6.45 us | 36.27 KB | yes |  |
| E5 | full cycle | DotnetCel | 3 | 1: 3.87; 2: 3.86; 3: 4.06 | 3.87 | 1.05 | GPCel-Input | 7.01 us | 27.60 KB | yes |  |
| E5 | full cycle | Cel.NET | 3 | 1: 30.46; 2: 30.06; 3: 31.84 | 30.46 | 1.06 | GPCel-Input | 55.02 us | 283.09 KB | yes |  |
| E5 | full cycle | TelusCel | 3 | 1: 5.40; 2: 4.75; 3: 5.75 | 5.40 | 1.21 | GPCel-Input | 9.65 us | 22.98 KB | yes |  |
| E5 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.30 us | 6.75 KB | no |  |
| E5 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.79 us | 5.82 KB | no |  |
| E5 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 2.40 us | 5.92 KB | no |  |
| E6 | compile | Celly | 3 | 1: 3.69; 2: 3.24; 3: 3.72 | 3.69 | 1.15 | GPCel-Input | 7.73 us | 42.96 KB | yes |  |
| E6 | compile | Cel.NET | 3 | 1: 8.83; 2: 7.13; 3: 8.47 | 8.47 | 1.24 | GPCel-Input | 17.35 us | 50.47 KB | yes |  |
| E6 | compile | TelusCel | 3 | 1: 2.92; 2: 2.28; 3: 2.92 | 2.92 | 1.28 | GPCel-Input | 5.99 us | 13.97 KB | yes |  |
| E6 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.95 us | 9.21 KB | no |  |
| E6 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.10 us | 6.46 KB | no |  |
| E6 | evaluate | Celly | 3 | 1: 1.92; 2: 1.50; 3: 1.51 | 1.51 | 1.28 | GPCel-Input | 248.0 ns | 912 B | yes |  |
| E6 | evaluate | Cel.NET | 3 | 1: 1.83; 2: 1.68; 3: 1.71 | 1.71 | 1.09 | GPCel-Input | 281.2 ns | 1.57 KB | yes |  |
| E6 | evaluate | TelusCel | 3 | 1: 50.04; 2: 53.15; 3: 52.19 | 52.19 | 1.06 | GPCel-Input | 8.58 us | 18.30 KB | yes |  |
| E6 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 205.3 ns | 288 B | no |  |
| E6 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 163.5 ns | 288 B | no |  |
| E6 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.10; 3: 1.09 | 1.09 | 1.10 | GPCel-Simple, Celly | 269.2 ns | 160 B | no | yes |
| E6 | full cycle | Celly | 3 | 1: 3.32; 2: 3.52; 3: 3.45 | 3.45 | 1.06 | GPCel-Input | 7.90 us | 43.85 KB | yes |  |
| E6 | full cycle | Cel.NET | 3 | 1: 7.97; 2: 7.83; 3: 7.94 | 7.94 | 1.02 | GPCel-Input | 17.80 us | 52.03 KB | yes |  |
| E6 | full cycle | TelusCel | 3 | 1: 6.12; 2: 5.82; 3: 6.32 | 6.12 | 1.09 | GPCel-Input | 14.17 us | 32.27 KB | yes |  |
| E6 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.30 us | 9.52 KB | no |  |
| E6 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.24 us | 6.77 KB | no |  |
| E6 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 2.96 us | 6.93 KB | no |  |
| E7 | compile | Celly | 3 | 1: 3.51; 2: 3.31; 3: 3.61 | 3.51 | 1.09 | GreatPartsCel, GPCel-Input | 7.92 us | 45.17 KB | yes |  |
| E7 | compile | DotnetCel | 3 | 1: 2.57; 2: 2.31; 3: 2.43 | 2.43 | 1.12 | GreatPartsCel, GPCel-Input | 5.49 us | 25.66 KB | yes |  |
| E7 | compile | Cel.NET | 3 | 1: 8.02; 2: 6.78; 3: 7.42 | 7.42 | 1.18 | GreatPartsCel, GPCel-Input | 16.26 us | 53.69 KB | yes |  |
| E7 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.65 us | 7.52 KB | no |  |
| E7 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.33 us | 7.08 KB | no |  |
| E7 | evaluate | Celly | 3 | 1: 3.42; 2: 3.46; 3: 3.43 | 3.43 | 1.01 | GPCel-Input | 532.8 ns | 1.17 KB | yes |  |
| E7 | evaluate | DotnetCel | 3 | 1: 1.58; 2: 1.52; 3: 1.48 | 1.52 | 1.07 | GPCel-Input | 235.4 ns | 592 B | yes |  |
| E7 | evaluate | Cel.NET | 3 | 1: 2.71; 2: 2.44; 3: 2.41 | 2.44 | 1.12 | GPCel-Input | 376.9 ns | 1.61 KB | yes |  |
| E7 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 216.5 ns | 288 B | no |  |
| E7 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 154.8 ns | 288 B | no |  |
| E7 | evaluate | GPCel-Simple | 3 | 1: 1.02; 2: 1.04; 3: 1.03 | 1.03 | 1.02 | DotnetCel | 245.0 ns | 160 B | no |  |
| E7 | full cycle | Celly | 3 | 1: 3.37; 2: 3.73; 3: 3.37 | 3.37 | 1.11 | GreatPartsCel, GPCel-Input | 8.94 us | 46.32 KB | yes |  |
| E7 | full cycle | DotnetCel | 3 | 1: 2.99; 2: 3.45; 3: 2.73 | 2.99 | 1.26 | GreatPartsCel, GPCel-Input | 7.95 us | 28.74 KB | yes |  |
| E7 | full cycle | Cel.NET | 3 | 1: 7.12; 2: 7.27; 3: 6.52 | 7.12 | 1.12 | GreatPartsCel, GPCel-Input | 17.65 us | 55.29 KB | yes |  |
| E7 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.87 us | 7.83 KB | no |  |
| E7 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.64 us | 7.40 KB | no |  |
| E7 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.07 us | 7.37 KB | no |  |
| E8 | compile | Celly | 3 | 1: 2.95; 2: 3.20; 3: 3.04 | 3.04 | 1.08 | GreatPartsCel, GPCel-Input | 10.41 us | 52.40 KB | yes |  |
| E8 | compile | Cel.NET | 3 | 1: 10.65; 2: 11.62; 3: 9.73 | 10.65 | 1.19 | GreatPartsCel, GPCel-Input | 34.97 us | 99.24 KB | yes |  |
| E8 | compile | TelusCel | 3 | 1: 2.52; 2: 2.56; 3: 2.48 | 2.52 | 1.03 | GreatPartsCel, GPCel-Input | 8.74 us | 19.39 KB | yes |  |
| E8 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.99 us | 12.52 KB | no |  |
| E8 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.52 us | 10.87 KB | no |  |
| E8 | evaluate | Celly | 3 | 1: 3.11; 2: 3.19; 3: 3.22 | 3.19 | 1.04 | GPCel-Input | 542.5 ns | 1.66 KB | yes |  |
| E8 | evaluate | Cel.NET | 3 | 1: 1.99; 2: 2.08; 3: 1.81 | 1.99 | 1.15 | GPCel-Input | 346.1 ns | 1.64 KB | yes |  |
| E8 | evaluate | TelusCel | 3 | 1: 42.68; 2: 39.65; 3: 41.92 | 41.92 | 1.08 | GPCel-Input | 7.13 us | 16.02 KB | yes |  |
| E8 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 230.2 ns | 336 B | no |  |
| E8 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 170.1 ns | 336 B | no |  |
| E8 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 270.9 ns | 160 B | no |  |
| E8 | full cycle | Celly | 3 | 1: 3.04; 2: 3.23; 3: 2.66 | 3.04 | 1.21 | GPCel-Input | 10.57 us | 54.18 KB | yes |  |
| E8 | full cycle | Cel.NET | 3 | 1: 9.95; 2: 11.20; 3: 8.99 | 9.95 | 1.25 | GPCel-Input | 36.62 us | 100.88 KB | yes |  |
| E8 | full cycle | TelusCel | 3 | 1: 4.26; 2: 4.90; 3: 4.37 | 4.37 | 1.15 | GPCel-Input | 16.25 us | 35.41 KB | yes |  |
| E8 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.11 us | 12.88 KB | no |  |
| E8 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.81 us | 11.23 KB | no |  |
| E8 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.26 us | 11.31 KB | no |  |
| E9 | compile | Celly | 3 | 1: 3.99; 2: 4.56; 3: 3.67 | 3.99 | 1.24 | GPCel-Input | 10.21 us | 52.39 KB | yes |  |
| E9 | compile | DotnetCel | 3 | 1: 2.28; 2: 2.82; 3: 2.14 | 2.28 | 1.32 | GPCel-Input | 6.23 us | 26.69 KB | yes |  |
| E9 | compile | Cel.NET | 3 | 1: 9.43; 2: 10.59; 3: 9.75 | 9.75 | 1.12 | GPCel-Input | 24.93 us | 77.44 KB | yes |  |
| E9 | compile | TelusCel | 3 | 1: 1.57; 2: 1.79; 3: 1.70 | 1.70 | 1.14 | GPCel-Input | 4.30 us | 10.08 KB | yes |  |
| E9 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.93 us | 9.99 KB | no |  |
| E9 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.56 us | 8.42 KB | no |  |
| E9 | evaluate | Celly | 3 | 1: 1.34; 2: 1.51; 3: 1.33 | 1.34 | 1.14 | GPCel-Input | 259.9 ns | 1.11 KB | yes |  |
| E9 | evaluate | DotnetCel | 3 | 1: 1.48; 2: 1.64; 3: 1.58 | 1.58 | 1.11 | GPCel-Input | 286.7 ns | 808 B | yes |  |
| E9 | evaluate | Cel.NET | 3 | 1: 1.55; 2: 1.70; 3: 1.81 | 1.70 | 1.16 | GPCel-Input | 305.7 ns | 1.75 KB | yes |  |
| E9 | evaluate | TelusCel | 3 | 1: 23.60; 2: 24.23; 3: 26.19 | 24.23 | 1.11 | GPCel-Input | 4.64 us | 10.77 KB | yes |  |
| E9 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.01; 3: 1.02 | 1.01 | 1.02 | GreatPartsCel, Celly | 245.1 ns | 472 B | no |  |
| E9 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 181.2 ns | 472 B | no |  |
| E9 | evaluate | GPCel-Simple | 3 | 1: 1.19; 2: 1.16; 3: 1.34 | 1.19 | 1.16 | Celly | 312.4 ns | 344 B | yes |  |
| E9 | full cycle | Celly | 3 | 1: 3.72; 2: 3.75; 3: 3.56 | 3.72 | 1.05 | GPCel-Input | 9.87 us | 53.41 KB | yes |  |
| E9 | full cycle | DotnetCel | 3 | 1: 2.78; 2: 3.24; 3: 3.16 | 3.16 | 1.16 | GPCel-Input | 8.40 us | 29.99 KB | yes |  |
| E9 | full cycle | Cel.NET | 3 | 1: 8.35; 2: 9.09; 3: 9.23 | 9.09 | 1.11 | GPCel-Input | 23.95 us | 79.18 KB | yes |  |
| E9 | full cycle | TelusCel | 3 | 1: 2.96; 2: 3.23; 3: 3.50 | 3.23 | 1.18 | GPCel-Input | 8.95 us | 20.85 KB | yes |  |
| E9 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.25 us | 10.48 KB | no |  |
| E9 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.64 us | 8.91 KB | no |  |
| E9 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.31 us | 9.04 KB | no |  |

R above 1.1 (all libraries): E1, compile, Celly: 3.58; E1, compile, DotnetCel: 2.49; E1, compile, Cel.NET: 7.07; E1, compile, TelusCel: 1.88; E1, evaluate, Celly: 1.61; E1, evaluate, DotnetCel: 1.62; E1, evaluate, Cel.NET: 1.91; E1, evaluate, TelusCel: 30.80; E1, full cycle, Celly: 3.44; E1, full cycle, DotnetCel: 2.96; E1, full cycle, Cel.NET: 6.24; E1, full cycle, TelusCel: 3.83; E2/10, compile, Celly: 2.71; E2/10, compile, DotnetCel: 2.10; E2/10, compile, Cel.NET: 5.68; E2/10, compile, TelusCel: 1.69; E2/10, evaluate, Celly: 2.01; E2/10, evaluate, DotnetCel: 2.92; E2/10, evaluate, Cel.NET: 2.32; E2/10, evaluate, TelusCel: 49.23; E2/10, full cycle, Celly: 2.32; E2/10, full cycle, DotnetCel: 2.52; E2/10, full cycle, Cel.NET: 4.83; E2/10, full cycle, TelusCel: 11.03; E2/100, compile, Celly: 2.58; E2/100, compile, DotnetCel: 1.95; E2/100, compile, Cel.NET: 5.65; E2/100, compile, TelusCel: 1.70; E2/100, evaluate, Celly: 1.83; E2/100, evaluate, DotnetCel: 2.91; E2/100, evaluate, Cel.NET: 2.34; E2/100, evaluate, TelusCel: 53.03; E2/100, full cycle, Celly: 2.02; E2/100, full cycle, DotnetCel: 2.65; E2/100, full cycle, Cel.NET: 3.25; E2/100, full cycle, TelusCel: 38.30; E3/10, compile, Celly: 2.84; E3/10, compile, DotnetCel: 2.18; E3/10, compile, Cel.NET: 5.37; E3/10, compile, TelusCel: 1.61; E3/10, evaluate, Celly: 2.50; E3/10, evaluate, DotnetCel: 3.04; E3/10, evaluate, Cel.NET: 2.79; E3/10, evaluate, TelusCel: 134.78; E3/10, full cycle, Celly: 2.70; E3/10, full cycle, DotnetCel: 2.59; E3/10, full cycle, Cel.NET: 5.05; E3/10, full cycle, TelusCel: 9.06; E3/100, compile, Celly: 2.79; E3/100, compile, DotnetCel: 2.10; E3/100, compile, Cel.NET: 5.65; E3/100, compile, TelusCel: 1.59; E3/100, evaluate, Celly: 12.38; E3/100, evaluate, DotnetCel: 10.94; E3/100, evaluate, Cel.NET: 6.57; E3/100, evaluate, TelusCel: 1232.41; E3/100, full cycle, Celly: 3.28; E3/100, full cycle, DotnetCel: 3.38; E3/100, full cycle, Cel.NET: 5.78; E3/100, full cycle, TelusCel: 83.40; E4/10, compile, Celly: 2.61; E4/10, compile, DotnetCel: 1.64; E4/10, compile, Cel.NET: 8.48; E4/10, compile, TelusCel: 1.54; E4/10, evaluate, Celly: 2.70; E4/10, evaluate, DotnetCel: 3.66; E4/10, evaluate, Cel.NET: 3.47; E4/10, evaluate, TelusCel: 66.02; E4/10, full cycle, Celly: 2.73; E4/10, full cycle, DotnetCel: 2.20; E4/10, full cycle, Cel.NET: 7.44; E4/10, full cycle, TelusCel: 8.21; E4/100, compile, Celly: 2.81; E4/100, compile, DotnetCel: 1.69; E4/100, compile, Cel.NET: 8.43; E4/100, compile, TelusCel: 1.57; E4/100, evaluate, Celly: 2.50; E4/100, evaluate, DotnetCel: 3.54; E4/100, evaluate, Cel.NET: 3.45; E4/100, evaluate, TelusCel: 60.00; E4/100, full cycle, Celly: 2.57; E4/100, full cycle, DotnetCel: 2.93; E4/100, full cycle, Cel.NET: 5.26; E4/100, full cycle, TelusCel: 36.31; E5, compile, Celly: 3.90; E5, compile, DotnetCel: 3.24; E5, compile, Cel.NET: 5.69; E5, compile, TelusCel: 2.16; E5, evaluate, Celly: 1.49; E5, evaluate, DotnetCel: 1.76; E5, evaluate, Cel.NET: 263.37; E5, evaluate, TelusCel: 34.03; E5, full cycle, Celly: 3.49; E5, full cycle, DotnetCel: 3.87; E5, full cycle, Cel.NET: 30.46; E5, full cycle, TelusCel: 5.40; E6, compile, Celly: 3.69; E6, compile, Cel.NET: 8.47; E6, compile, TelusCel: 2.92; E6, evaluate, Celly: 1.51; E6, evaluate, Cel.NET: 1.71; E6, evaluate, TelusCel: 52.19; E6, full cycle, Celly: 3.45; E6, full cycle, Cel.NET: 7.94; E6, full cycle, TelusCel: 6.12; E7, compile, Celly: 3.51; E7, compile, DotnetCel: 2.43; E7, compile, Cel.NET: 7.42; E7, evaluate, Celly: 3.43; E7, evaluate, DotnetCel: 1.52; E7, evaluate, Cel.NET: 2.44; E7, full cycle, Celly: 3.37; E7, full cycle, DotnetCel: 2.99; E7, full cycle, Cel.NET: 7.12; E8, compile, Celly: 3.04; E8, compile, Cel.NET: 10.65; E8, compile, TelusCel: 2.52; E8, evaluate, Celly: 3.19; E8, evaluate, Cel.NET: 1.99; E8, evaluate, TelusCel: 41.92; E8, full cycle, Celly: 3.04; E8, full cycle, Cel.NET: 9.95; E8, full cycle, TelusCel: 4.37; E9, compile, Celly: 3.99; E9, compile, DotnetCel: 2.28; E9, compile, Cel.NET: 9.75; E9, compile, TelusCel: 1.70; E9, evaluate, Celly: 1.34; E9, evaluate, DotnetCel: 1.58; E9, evaluate, Cel.NET: 1.70; E9, evaluate, TelusCel: 24.23; E9, evaluate, GPCel-Simple: 1.19; E9, full cycle, Celly: 3.72; E9, full cycle, DotnetCel: 3.16; E9, full cycle, Cel.NET: 9.09; E9, full cycle, TelusCel: 3.23.

**Rows of GreatPartsCel.**

- R above 1.1: E9, evaluate, GPCel-Simple: 1.19.
- Ratios on both sides of 1.1: E1, evaluate, GPCel-Simple: 1.06; E6, evaluate, GPCel-Simple: 1.09.

## Limits of the speed-ups

Job: Default; started 2026-11-26T13:35:14.028Z, finished 2026-11-26T14:07:54.525Z. Medians of BenchmarkDotNet on the code of this commit; the reports are in `results/thresholds/bdn/results`.

| Limit | Rule | Verdict |
|---|---|---|
| hybrid search of the literal prefix of matches, 64 code units | .NET at M = 64 is not slower than Knuth-Morris-Pratt; otherwise the limit is 32 | holds: 64 |
| build of the automaton of the pattern E5 | the median of the build is at most 2000 ns | holds: 1005 ns |
| index of the values of a list (distinct, sets): scan or buckets, the limit of the library is 64 values | a diagnostic without a verdict: the threshold by the rule is the largest N at which, and at every smaller N measured (8, 16, 24, 32, 48, 64), scan / buckets is at most 1.0 for distinct and for sets.contains; 8 when the scan is slower already at N = 8 | no verdict; threshold by the rule: 32 (the limit of the library: 64) |
| substring search: time per cost unit | a diagnostic without a verdict: a rise of the time per unit above 2 times from M = 64 to M = 1024 is the input of the decision on the divisor 64 | no verdict; time per unit from M = 64 to M = 1024: 0.06 times for a literal, 0.58 times for the data |
| simple entry: evaluate and data | a diagnostic without a verdict: the time of the call on a prepared anonymous object, and the time of building that object | no verdict; scalars: evaluate 235.7 ns, data 10.6 ns; lines: evaluate 1.22 us, data 10.9 ns |
| CelMap.Create of the row of an order | a diagnostic without a verdict: the time and the allocation of a map of three entries made with a collection expression | no verdict; 33.1 ns, 248 B |

### Search of the literal prefix of matches, the text (ab)^5000

M is the length of the pattern, the slowest case for the search of .NET. "Library" is the search as it runs: .NET up to the limit, Knuth-Morris-Pratt above. Net / KMP below 1 means .NET is faster.

| M | KMP | .NET | Library | Net / KMP |
|--:|--:|--:|--:|--:|
| 48 | 9.55 us | 7.02 us | 7.09 us | 0.74 |
| 64 | 9.15 us | 6.67 us | 6.91 us | 0.73 |
| 65 | 9.52 us | 416.5 ns | 9.10 us | 0.04 |
| 80 | 9.41 us | 8.16 us | 9.18 us | 0.87 |

### Search of a substring by the functions of strings: time per cost unit

contains on the text (ab)^5000 (10000 code units) and a pattern of M code units, the slowest case for the search of .NET. "Literal" is a pattern that is a literal of the expression (SearchValues<string>), "data" a pattern read from a variable (the ordinal search); the time is that of the whole evaluation. The cost is 1 + ceil((n + M) / 10) * q, q = max(1, ceil(M / 64)), without the reads of the variables. No verdict.

| M | Cost | Literal | Literal per unit | Data | Data per unit |
|--:|--:|--:|--:|--:|--:|
| 64 | 1008 | 371.8 ns | 0.4 ns | 6.50 us | 6.4 ns |
| 256 | 4105 | 418.8 ns | 0.1 ns | 19.31 us | 4.7 ns |
| 1024 | 17649 | 403.8 ns | 0.0 ns | 65.57 us | 3.7 ns |

### Build of the automaton

| Pattern | Parse and program | Automaton |
|---|--:|--:|
| E5 | 580.2 ns | 1.01 us |
| UUID | 1.13 us | 2.59 us |

### Index of the values: the lists of N integers

"Library" is the index as the library makes it (the scan up to 64 values, the buckets above); "scan" and "buckets" force that way at any N; "one hash" gives every value the same hash; "pairwise" are integers of 2^53 and more, compared with each other in order. Scan / buckets below 1 means the scan is faster. No verdict: the threshold by the rule is 32; the limit of the library is set by a decision on this table.

| Operation | N | Library | Scan | Buckets | One hash | Pairwise | Scan / buckets |
|---|--:|--:|--:|--:|--:|--:|--:|
| distinct | 8 | 196.1 ns | 192.0 ns | 278.1 ns | 329.1 ns | 319.1 ns | 0.69 |
| distinct | 16 | 290.9 ns | 289.8 ns | 429.0 ns | 786.7 ns | 872.1 ns | 0.68 |
| distinct | 24 | 414.0 ns | 420.5 ns | 585.9 ns | 1.95 us | 1.30 us | 0.72 |
| distinct | 32 | 543.0 ns | 533.4 ns | 709.5 ns | 2.96 us | 1.74 us | 0.75 |
| distinct | 48 | 932.7 ns | 756.5 ns | 964.0 ns | 6.26 us | 3.51 us | 0.78 |
| distinct | 64 | 1.01 us | 1.01 us | 1.24 us | 12.00 us | 5.84 us | 0.81 |
| sets.contains | 8 | 289.8 ns | 288.3 ns | 393.4 ns | 408.9 ns | 426.9 ns | 0.73 |
| sets.contains | 16 | 543.2 ns | 550.1 ns | 636.6 ns | 1.04 us | 793.2 ns | 0.86 |
| sets.contains | 24 | 801.4 ns | 844.1 ns | 855.6 ns | 2.09 us | 1.61 us | 0.99 |
| sets.contains | 32 | 1.03 us | 1.05 us | 1.08 us | 3.79 us | 2.07 us | 0.97 |
| sets.contains | 48 | 1.48 us | 1.58 us | 1.47 us | 7.46 us | 4.59 us | 1.08 |
| sets.contains | 64 | 1.87 us | 1.92 us | 1.98 us | 13.06 us | 6.48 us | 0.97 |

### distinct and sets.contains through the public API

The evaluation of the program on a list of N integers; the time per element shows a jump at the limit, if there is one.

| N | distinct | per element | sets.contains | per element |
|--:|--:|--:|--:|--:|
| 16 | 461.3 ns | 28.8 ns | 822.6 ns | 51.4 ns |
| 32 | 797.7 ns | 24.9 ns | 1.46 us | 45.6 ns |
| 33 | 795.9 ns | 24.1 ns | 1.59 us | 48.2 ns |
| 48 | 1.11 us | 23.0 ns | 2.22 us | 46.3 ns |
| 64 | 1.76 us | 27.5 ns | 2.91 us | 45.5 ns |

### Simple entry: evaluate and data

The call `CelExpression.Evaluate` on an anonymous object with the scalars of the order, the customer and the lines as properties, as the variant of the rig passes them. "Evaluate" is the call on an object that was built before the measurement, with the text of the variant with the input of the environment ("scalars" is the text of E1, "lines" the text of E2 over 10 lines); "data" is the building of that object for one call. No verdict.

| Text | Evaluate | Allocated | Data | Allocated |
|---|--:|--:|--:|--:|
| scalars | 235.7 ns | 48 B | 10.6 ns | 112 B |
| lines | 1.22 us | 48 B | 10.9 ns | 112 B |

### CelMap.Create of the row of an order

A map of three entries (`sku`, `qty`, `price`) made with a collection expression. No verdict.

| Time | Allocated |
|--:|--:|
| 33.1 ns | 248 B |
