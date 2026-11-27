# Benchmark rig: JIT on .NET 11

- Date and time of the report (UTC): 2026-11-27 11:26:09
- Commit of great-parts/great-parts-cel: [`927d92df05ffb30c4262d719d1960feb1cd08509`](https://github.com/great-parts/great-parts-cel/commit/927d92df05ffb30c4262d719d1960feb1cd08509)
- Runs: 3
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45952887623 (GitHub keeps a run for 90 days; this report does not depend on it)

The summary of the rig follows. The full reports of BenchmarkDotNet are in the artifact `bench-results` of the run, for 90 days.

## Conditions

- Date of the summary: 2026-11-27T11:26:09.683Z
- Commit: 927d92df05ffb30c4262d719d1960feb1cd08509
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45952887623
- Runner: Linux X64, image ubuntu26 20261116.88.1
- Runtime: JIT on .NET 11 (the default job of BenchmarkDotNet)
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

- BenchmarkDotNet 0.16.0-preview.2; OS: Linux Ubuntu 26.04.1 LTS (Resolute Raccoon); processor: Intel Xeon 6975P-C
- Cores: 2 physical, 4 logical; architecture: X64
- Runtime: .NET 11.0.0 (11.0.0, 11.0.26.53018); SDK: 11.0.100; configuration: RELEASE

## Runs

- Run 1: 2026-11-27T06:36:52.301Z - 2026-11-27T08:00:44.949Z; job: Default; runtime: JIT; cases: all; filter: *
- Run 2: 2026-11-27T08:00:44.949Z - 2026-11-27T09:29:16.569Z; job: Default; runtime: JIT; cases: all; filter: *
- Run 3: 2026-11-27T09:29:16.569Z - 2026-11-27T10:54:01.552Z; job: Default; runtime: JIT; cases: all; filter: *

The table of each run is `results/runs/<k>/summary.md`.

## Summary over the runs

The ratio of a row in a run is to the fastest library with a correct result in the same run (for a variant of GreatPartsCel: to the fastest other library or to itself, not to its other variants). R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has 3 runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others. "Allocated" is the median over the runs of the bytes allocated per operation that BenchmarkDotNet reports (MemoryDiagnoser), less the allocation of making the text for compile and full cycle, as the time is.

| Scenario | Operation | Library | Runs | Ratios by run | R | Spread | Fastest by run | Median time over the runs | Allocated | R > 1.1 | Both sides of 1.1 |
|---|---|---|--:|---|--:|--:|---|--:|--:|---|---|
| E1 | compile | Celly | 3 | 1: 5.34; 2: 4.91; 3: 4.47 | 4.91 | 1.19 | GPCel-Input | 11.36 us | 42.81 KB | yes |  |
| E1 | compile | DotnetCel | 3 | 1: 4.00; 2: 3.94; 3: 3.31 | 3.94 | 1.21 | GPCel-Input | 8.84 us | 26.31 KB | yes |  |
| E1 | compile | Cel.NET | 3 | 1: 10.08; 2: 9.77; 3: 8.91 | 9.77 | 1.13 | GPCel-Input | 22.62 us | 49.86 KB | yes |  |
| E1 | compile | TelusCel | 3 | 1: 2.16; 2: 2.24; 3: 1.97 | 2.16 | 1.14 | GPCel-Input | 4.99 us | 10.33 KB | yes |  |
| E1 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.30 us | 8.97 KB | no |  |
| E1 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.28 us | 6.98 KB | no |  |
| E1 | evaluate | Celly | 3 | 1: 1.96; 2: 1.65; 3: 1.70 | 1.70 | 1.19 | GPCel-Input | 339.2 ns | 832 B | yes |  |
| E1 | evaluate | DotnetCel | 3 | 1: 1.82; 2: 1.62; 3: 1.71 | 1.71 | 1.12 | GPCel-Input | 332.3 ns | 560 B | yes |  |
| E1 | evaluate | Cel.NET | 3 | 1: 2.27; 2: 1.87; 3: 2.10 | 2.10 | 1.22 | GPCel-Input | 400.7 ns | 1.52 KB | yes |  |
| E1 | evaluate | TelusCel | 3 | 1: 34.44; 2: 28.23; 3: 33.68 | 33.68 | 1.22 | GPCel-Input | 6.30 us | 11.16 KB | yes |  |
| E1 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 249.6 ns | 328 B | no |  |
| E1 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 191.2 ns | 288 B | no |  |
| E1 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 269.8 ns | 160 B | no |  |
| E1 | full cycle | Celly | 3 | 1: 4.62; 2: 3.79; 3: 4.14 | 4.14 | 1.22 | GPCel-Input | 12.05 us | 43.61 KB | yes |  |
| E1 | full cycle | DotnetCel | 3 | 1: 4.42; 2: 4.24; 3: 3.94 | 4.24 | 1.12 | GPCel-Input | 11.49 us | 29.37 KB | yes |  |
| E1 | full cycle | Cel.NET | 3 | 1: 8.93; 2: 7.89; 3: 7.84 | 7.89 | 1.14 | GPCel-Input | 22.82 us | 51.38 KB | yes |  |
| E1 | full cycle | TelusCel | 3 | 1: 4.15; 2: 4.12; 3: 3.81 | 4.12 | 1.09 | GPCel-Input | 11.11 us | 21.49 KB | yes |  |
| E1 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.76 us | 9.30 KB | no |  |
| E1 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.71 us | 7.30 KB | no |  |
| E1 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.13 us | 7.55 KB | no |  |
| E2/10 | compile | Celly | 3 | 1: 3.39; 2: 2.81; 3: 3.19 | 3.19 | 1.21 | GPCel-Input | 15.45 us | 54.53 KB | yes |  |
| E2/10 | compile | DotnetCel | 3 | 1: 2.64; 2: 2.62; 3: 2.52 | 2.62 | 1.05 | GPCel-Input | 12.18 us | 32.52 KB | yes |  |
| E2/10 | compile | Cel.NET | 3 | 1: 6.61; 2: 6.11; 3: 6.25 | 6.25 | 1.08 | GPCel-Input | 30.21 us | 71.98 KB | yes |  |
| E2/10 | compile | TelusCel | 3 | 1: 1.66; 2: 1.70; 3: 1.58 | 1.66 | 1.08 | GPCel-Input | 7.73 us | 15.50 KB | yes |  |
| E2/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.09 us | 12.36 KB | no |  |
| E2/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.65 us | 11.59 KB | no |  |
| E2/10 | evaluate | Celly | 3 | 1: 2.42; 2: 2.31; 3: 2.49 | 2.42 | 1.08 | GPCel-Input | 2.92 us | 2.96 KB | yes |  |
| E2/10 | evaluate | DotnetCel | 3 | 1: 3.38; 2: 3.44; 3: 3.29 | 3.38 | 1.05 | GPCel-Input | 4.07 us | 4.07 KB | yes |  |
| E2/10 | evaluate | Cel.NET | 3 | 1: 2.79; 2: 2.74; 3: 2.91 | 2.79 | 1.06 | GPCel-Input | 3.36 us | 6.15 KB | yes |  |
| E2/10 | evaluate | TelusCel | 3 | 1: 51.61; 2: 52.68; 3: 51.60 | 51.61 | 1.02 | GPCel-Input | 62.25 us | 111.34 KB | yes |  |
| E2/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.23 us | 376 B | no |  |
| E2/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.18 us | 336 B | no |  |
| E2/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 1.28 us | 160 B | no |  |
| E2/10 | full cycle | Celly | 3 | 1: 3.07; 2: 2.79; 3: 2.94 | 2.94 | 1.10 | GPCel-Input, GPCel-Simple | 18.45 us | 57.47 KB | yes |  |
| E2/10 | full cycle | DotnetCel | 3 | 1: 3.12; 2: 3.11; 3: 3.08 | 3.11 | 1.01 | GPCel-Input, GPCel-Simple | 19.38 us | 39.90 KB | yes |  |
| E2/10 | full cycle | Cel.NET | 3 | 1: 5.67; 2: 4.87; 3: 5.52 | 5.52 | 1.16 | GPCel-Input, GPCel-Simple | 34.71 us | 78.14 KB | yes |  |
| E2/10 | full cycle | TelusCel | 3 | 1: 11.45; 2: 11.61; 3: 11.36 | 11.45 | 1.02 | GPCel-Input, GPCel-Simple | 71.36 us | 126.84 KB | yes |  |
| E2/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.76 us | 12.73 KB | no |  |
| E2/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.28 us | 11.95 KB | no |  |
| E2/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 6.34 us | 12.12 KB | no |  |
| E2/100 | compile | Celly | 3 | 1: 3.42; 2: 3.01; 3: 3.14 | 3.14 | 1.13 | GPCel-Input | 15.38 us | 54.53 KB | yes |  |
| E2/100 | compile | DotnetCel | 3 | 1: 2.58; 2: 2.57; 3: 2.41 | 2.57 | 1.07 | GPCel-Input | 11.96 us | 32.52 KB | yes |  |
| E2/100 | compile | Cel.NET | 3 | 1: 6.60; 2: 5.65; 3: 5.89 | 5.89 | 1.17 | GPCel-Input | 28.88 us | 71.99 KB | yes |  |
| E2/100 | compile | TelusCel | 3 | 1: 1.65; 2: 1.65; 3: 1.58 | 1.65 | 1.05 | GPCel-Input | 7.73 us | 15.50 KB | yes |  |
| E2/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.14 us | 12.36 KB | no |  |
| E2/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.72 us | 11.59 KB | no |  |
| E2/100 | evaluate | Celly | 3 | 1: 2.33; 2: 2.48; 3: 2.34 | 2.34 | 1.06 | GreatPartsCel, GPCel-Input | 24.40 us | 21.66 KB | yes |  |
| E2/100 | evaluate | DotnetCel | 3 | 1: 3.32; 2: 3.69; 3: 3.53 | 3.53 | 1.11 | GreatPartsCel, GPCel-Input | 34.96 us | 31.91 KB | yes |  |
| E2/100 | evaluate | Cel.NET | 3 | 1: 2.93; 2: 2.92; 3: 3.18 | 2.93 | 1.09 | GreatPartsCel, GPCel-Input | 30.79 us | 54.28 KB | yes |  |
| E2/100 | evaluate | TelusCel | 3 | 1: 59.36; 2: 59.84; 3: 60.63 | 59.84 | 1.02 | GreatPartsCel, GPCel-Input | 599.08 us | 1.03 MB | yes |  |
| E2/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 10.26 us | 376 B | no |  |
| E2/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 9.88 us | 336 B | no |  |
| E2/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 11.03 us | 160 B | no |  |
| E2/100 | full cycle | Celly | 3 | 1: 2.73; 2: 2.56; 3: 2.60 | 2.60 | 1.07 | GPCel-Input | 40.43 us | 76.17 KB | yes |  |
| E2/100 | full cycle | DotnetCel | 3 | 1: 3.35; 2: 3.15; 3: 3.33 | 3.33 | 1.06 | GPCel-Input | 50.81 us | 67.74 KB | yes |  |
| E2/100 | full cycle | Cel.NET | 3 | 1: 4.15; 2: 3.77; 3: 4.07 | 4.07 | 1.10 | GPCel-Input | 62.18 us | 126.27 KB | yes |  |
| E2/100 | full cycle | TelusCel | 3 | 1: 37.89; 2: 38.68; 3: 45.15 | 38.68 | 1.19 | GPCel-Input | 610.08 us | 1.05 MB | yes |  |
| E2/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 15.95 us | 12.72 KB | no |  |
| E2/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 15.30 us | 11.95 KB | no |  |
| E2/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 16.57 us | 12.12 KB | no |  |
| E3/10 | compile | Celly | 3 | 1: 3.49; 2: 2.86; 3: 3.27 | 3.27 | 1.22 | GPCel-Input | 12.58 us | 44.33 KB | yes |  |
| E3/10 | compile | DotnetCel | 3 | 1: 3.08; 2: 2.93; 3: 2.81 | 2.93 | 1.10 | GPCel-Input | 11.09 us | 30.26 KB | yes |  |
| E3/10 | compile | Cel.NET | 3 | 1: 6.32; 2: 5.43; 3: 5.50 | 5.50 | 1.17 | GPCel-Input | 21.73 us | 53.14 KB | yes |  |
| E3/10 | compile | TelusCel | 3 | 1: 1.63; 2: 1.67; 3: 1.58 | 1.63 | 1.06 | GPCel-Input | 6.25 us | 13.38 KB | yes |  |
| E3/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.16 us | 10.95 KB | no |  |
| E3/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.77 us | 10.33 KB | no |  |
| E3/10 | evaluate | Celly | 3 | 1: 2.88; 2: 2.49; 3: 2.82 | 2.82 | 1.16 | GPCel-Input | 766.0 ns | 1.61 KB | yes |  |
| E3/10 | evaluate | DotnetCel | 3 | 1: 3.52; 2: 3.21; 3: 3.44 | 3.44 | 1.10 | GPCel-Input | 936.5 ns | 1.78 KB | yes |  |
| E3/10 | evaluate | Cel.NET | 3 | 1: 2.92; 2: 2.41; 3: 2.73 | 2.73 | 1.21 | GPCel-Input | 747.5 ns | 2.07 KB | yes |  |
| E3/10 | evaluate | TelusCel | 3 | 1: 126.31; 2: 136.93; 3: 127.11 | 127.11 | 1.08 | GPCel-Input | 34.80 us | 62.83 KB | yes |  |
| E3/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 346.7 ns | 376 B | no |  |
| E3/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 273.8 ns | 336 B | no |  |
| E3/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 338.1 ns | 160 B | no |  |
| E3/10 | full cycle | Celly | 3 | 1: 3.34; 2: 2.79; 3: 3.07 | 3.07 | 1.19 | GPCel-Input, GPCel-Simple | 13.50 us | 45.94 KB | yes |  |
| E3/10 | full cycle | DotnetCel | 3 | 1: 3.55; 2: 3.50; 3: 3.44 | 3.50 | 1.03 | GPCel-Input, GPCel-Simple | 14.83 us | 35.35 KB | yes |  |
| E3/10 | full cycle | Cel.NET | 3 | 1: 5.74; 2: 5.31; 3: 5.27 | 5.31 | 1.09 | GPCel-Input, GPCel-Simple | 23.17 us | 55.21 KB | yes |  |
| E3/10 | full cycle | TelusCel | 3 | 1: 9.92; 2: 10.98; 3: 9.75 | 9.92 | 1.13 | GPCel-Input, GPCel-Simple | 42.87 us | 76.20 KB | yes |  |
| E3/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.76 us | 11.31 KB | no |  |
| E3/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.32 us | 10.70 KB | no |  |
| E3/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.50 us | 10.87 KB | no |  |
| E3/100 | compile | Celly | 3 | 1: 3.53; 2: 2.90; 3: 3.17 | 3.17 | 1.22 | GPCel-Input | 12.51 us | 44.33 KB | yes |  |
| E3/100 | compile | DotnetCel | 3 | 1: 2.88; 2: 2.74; 3: 2.75 | 2.75 | 1.05 | GPCel-Input | 10.51 us | 30.26 KB | yes |  |
| E3/100 | compile | Cel.NET | 3 | 1: 6.20; 2: 5.90; 3: 5.45 | 5.90 | 1.14 | GPCel-Input | 21.89 us | 53.14 KB | yes |  |
| E3/100 | compile | TelusCel | 3 | 1: 1.58; 2: 1.71; 3: 1.62 | 1.62 | 1.08 | GPCel-Input | 6.34 us | 13.38 KB | yes |  |
| E3/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.19 us | 10.94 KB | no |  |
| E3/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.71 us | 10.34 KB | no |  |
| E3/100 | evaluate | Celly | 3 | 1: 13.85; 2: 12.55; 3: 13.51 | 13.51 | 1.10 | GPCel-Input | 3.63 us | 6.95 KB | yes |  |
| E3/100 | evaluate | DotnetCel | 3 | 1: 11.62; 2: 12.74; 3: 11.83 | 11.83 | 1.10 | GPCel-Input | 3.18 us | 7.12 KB | yes |  |
| E3/100 | evaluate | Cel.NET | 3 | 1: 7.57; 2: 7.29; 3: 7.04 | 7.29 | 1.08 | GPCel-Input | 1.93 us | 8.81 KB | yes |  |
| E3/100 | evaluate | TelusCel | 3 | 1: 1120.35; 2: 1233.74; 3: 1182.25 | 1182.25 | 1.10 | GPCel-Input | 317.27 us | 570.20 KB | yes |  |
| E3/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 333.2 ns | 376 B | no |  |
| E3/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 268.4 ns | 336 B | no |  |
| E3/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 338.3 ns | 160 B | no |  |
| E3/100 | full cycle | Celly | 3 | 1: 3.92; 2: 3.69; 3: 3.85 | 3.85 | 1.06 | GPCel-Input, GPCel-Simple | 16.80 us | 51.27 KB | yes |  |
| E3/100 | full cycle | DotnetCel | 3 | 1: 3.84; 2: 4.09; 3: 4.02 | 4.02 | 1.06 | GPCel-Input, GPCel-Simple | 17.14 us | 40.69 KB | yes |  |
| E3/100 | full cycle | Cel.NET | 3 | 1: 5.78; 2: 5.53; 3: 5.74 | 5.74 | 1.04 | GPCel-Input, GPCel-Simple | 25.02 us | 61.94 KB | yes |  |
| E3/100 | full cycle | TelusCel | 3 | 1: 80.14; 2: 87.89; 3: 76.21 | 80.14 | 1.15 | GPCel-Input, GPCel-Simple | 347.15 us | 583.57 KB | yes |  |
| E3/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.75 us | 11.31 KB | no |  |
| E3/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.33 us | 10.70 KB | no |  |
| E3/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.73 us | 10.88 KB | no |  |
| E4/10 | compile | Celly | 3 | 1: 2.99; 2: 2.65; 3: 2.78 | 2.78 | 1.13 | GPCel-Input | 23.40 us | 75.71 KB | yes |  |
| E4/10 | compile | DotnetCel | 3 | 1: 1.81; 2: 1.88; 3: 1.90 | 1.88 | 1.05 | GPCel-Input | 15.54 us | 37.80 KB | yes |  |
| E4/10 | compile | Cel.NET | 3 | 1: 8.68; 2: 8.17; 3: 8.22 | 8.22 | 1.06 | GPCel-Input | 69.15 us | 134.65 KB | yes |  |
| E4/10 | compile | TelusCel | 3 | 1: 1.31; 2: 1.30; 3: 1.27 | 1.30 | 1.03 | GPCel-Input | 10.71 us | 21.26 KB | yes |  |
| E4/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 8.62 us | 19.00 KB | no |  |
| E4/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 8.29 us | 18.46 KB | no |  |
| E4/10 | evaluate | Celly | 3 | 1: 2.75; 2: 2.68; 3: 2.58 | 2.68 | 1.06 | GPCel-Input | 2.84 us | 4.08 KB | yes |  |
| E4/10 | evaluate | DotnetCel | 3 | 1: 3.72; 2: 3.69; 3: 3.75 | 3.72 | 1.02 | GPCel-Input | 3.93 us | 5.31 KB | yes |  |
| E4/10 | evaluate | Cel.NET | 3 | 1: 3.76; 2: 3.65; 3: 3.83 | 3.76 | 1.05 | GPCel-Input | 3.97 us | 7.08 KB | yes |  |
| E4/10 | evaluate | TelusCel | 3 | 1: 49.47; 2: 51.45; 3: 52.33 | 51.45 | 1.06 | GPCel-Input | 54.44 us | 98.86 KB | yes |  |
| E4/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.11 us | 880 B | no |  |
| E4/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.06 us | 840 B | no |  |
| E4/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 1.11 us | 664 B | no |  |
| E4/10 | full cycle | Celly | 3 | 1: 2.89; 2: 2.56; 3: 2.77 | 2.77 | 1.13 | GPCel-Input, GPCel-Simple | 27.21 us | 79.79 KB | yes |  |
| E4/10 | full cycle | DotnetCel | 3 | 1: 2.36; 2: 2.47; 3: 2.35 | 2.36 | 1.05 | GPCel-Input, GPCel-Simple | 22.80 us | 46.42 KB | yes |  |
| E4/10 | full cycle | Cel.NET | 3 | 1: 7.89; 2: 7.72; 3: 7.62 | 7.72 | 1.04 | GPCel-Input, GPCel-Simple | 74.74 us | 141.72 KB | yes |  |
| E4/10 | full cycle | TelusCel | 3 | 1: 6.75; 2: 7.29; 3: 7.59 | 7.29 | 1.12 | GPCel-Input, GPCel-Simple | 67.31 us | 120.12 KB | yes |  |
| E4/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 10.12 us | 19.86 KB | no |  |
| E4/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 9.79 us | 19.32 KB | no |  |
| E4/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 10.28 us | 19.50 KB | no |  |
| E4/100 | compile | Celly | 3 | 1: 2.99; 2: 2.63; 3: 2.79 | 2.79 | 1.13 | GPCel-Input | 23.38 us | 75.71 KB | yes |  |
| E4/100 | compile | DotnetCel | 3 | 1: 1.92; 2: 2.00; 3: 1.88 | 1.92 | 1.06 | GPCel-Input | 15.69 us | 37.80 KB | yes |  |
| E4/100 | compile | Cel.NET | 3 | 1: 8.59; 2: 8.51; 3: 8.43 | 8.51 | 1.02 | GPCel-Input | 70.00 us | 134.64 KB | yes |  |
| E4/100 | compile | TelusCel | 3 | 1: 1.26; 2: 1.35; 3: 1.27 | 1.27 | 1.07 | GPCel-Input | 10.61 us | 21.26 KB | yes |  |
| E4/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 8.57 us | 19.00 KB | no |  |
| E4/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 8.15 us | 18.46 KB | no |  |
| E4/100 | evaluate | Celly | 3 | 1: 2.53; 2: 2.37; 3: 2.38 | 2.38 | 1.07 | GPCel-Input, GreatPartsCel | 28.62 us | 42.44 KB | yes |  |
| E4/100 | evaluate | DotnetCel | 3 | 1: 4.01; 2: 4.16; 3: 4.06 | 4.06 | 1.04 | GPCel-Input, GreatPartsCel | 47.87 us | 70.80 KB | yes |  |
| E4/100 | evaluate | Cel.NET | 3 | 1: 3.93; 2: 4.12; 3: 3.93 | 3.93 | 1.05 | GPCel-Input, GreatPartsCel | 46.90 us | 96.53 KB | yes |  |
| E4/100 | evaluate | TelusCel | 3 | 1: 54.18; 2: 59.57; 3: 55.89 | 55.89 | 1.10 | GPCel-Input, GreatPartsCel | 658.07 us | 1.15 MB | yes |  |
| E4/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 12.02 us | 9.61 KB | no |  |
| E4/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 11.95 us | 9.57 KB | no |  |
| E4/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 12.40 us | 9.40 KB | no |  |
| E4/100 | full cycle | Celly | 3 | 1: 2.68; 2: 2.51; 3: 2.71 | 2.68 | 1.08 | GPCel-Input | 53.94 us | 118.16 KB | yes |  |
| E4/100 | full cycle | DotnetCel | 3 | 1: 3.24; 2: 3.03; 3: 3.36 | 3.24 | 1.11 | GPCel-Input | 65.18 us | 111.91 KB | yes |  |
| E4/100 | full cycle | Cel.NET | 3 | 1: 5.93; 2: 5.80; 3: 5.95 | 5.93 | 1.03 | GPCel-Input | 119.56 us | 231.18 KB | yes |  |
| E4/100 | full cycle | TelusCel | 3 | 1: 33.25; 2: 33.20; 3: 34.00 | 33.25 | 1.02 | GPCel-Input | 681.39 us | 1.17 MB | yes |  |
| E4/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 21.08 us | 28.61 KB | no |  |
| E4/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 20.15 us | 28.07 KB | no |  |
| E4/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 22.83 us | 28.25 KB | no |  |
| E5 | compile | Celly | 3 | 1: 5.88; 2: 4.47; 3: 4.87 | 4.87 | 1.32 | GPCel-Input | 9.18 us | 35.54 KB | yes |  |
| E5 | compile | DotnetCel | 3 | 1: 4.76; 2: 3.92; 3: 4.19 | 4.19 | 1.21 | GPCel-Input | 7.88 us | 24.02 KB | yes |  |
| E5 | compile | Cel.NET | 3 | 1: 7.87; 2: 6.96; 3: 6.69 | 6.96 | 1.18 | GPCel-Input | 12.61 us | 30.60 KB | yes |  |
| E5 | compile | TelusCel | 3 | 1: 2.30; 2: 2.20; 3: 2.10 | 2.20 | 1.10 | GPCel-Input | 3.92 us | 8.64 KB | yes |  |
| E5 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.19 us | 6.57 KB | no |  |
| E5 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.78 us | 5.51 KB | no |  |
| E5 | evaluate | Celly | 3 | 1: 1.83; 2: 1.83; 3: 1.72 | 1.83 | 1.06 | GPCel-Input | 376.8 ns | 752 B | yes |  |
| E5 | evaluate | DotnetCel | 3 | 1: 1.85; 2: 1.84; 3: 1.75 | 1.84 | 1.05 | GPCel-Input | 380.6 ns | 448 B | yes |  |
| E5 | evaluate | Cel.NET | 3 | 1: 349.18; 2: 302.03; 3: 303.19 | 303.19 | 1.16 | GPCel-Input | 66.36 us | 252.50 KB | yes |  |
| E5 | evaluate | TelusCel | 3 | 1: 34.73; 2: 35.89; 3: 37.46 | 35.89 | 1.08 | GPCel-Input | 7.28 us | 14.34 KB | yes |  |
| E5 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 282.4 ns | 328 B | no |  |
| E5 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 205.9 ns | 288 B | no |  |
| E5 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 241.7 ns | 160 B | no |  |
| E5 | full cycle | Celly | 3 | 1: 4.87; 2: 4.25; 3: 4.29 | 4.29 | 1.15 | GPCel-Input | 9.79 us | 36.26 KB | yes |  |
| E5 | full cycle | DotnetCel | 3 | 1: 5.01; 2: 4.66; 3: 4.92 | 4.92 | 1.07 | GPCel-Input | 10.61 us | 27.60 KB | yes |  |
| E5 | full cycle | Cel.NET | 3 | 1: 42.37; 2: 35.15; 3: 37.01 | 37.01 | 1.21 | GPCel-Input | 84.33 us | 283.09 KB | yes |  |
| E5 | full cycle | TelusCel | 3 | 1: 5.29; 2: 5.33; 3: 5.16 | 5.29 | 1.03 | GPCel-Input | 11.59 us | 22.98 KB | yes |  |
| E5 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.73 us | 6.89 KB | no |  |
| E5 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.18 us | 5.83 KB | no |  |
| E5 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 2.60 us | 6.05 KB | no |  |
| E6 | compile | Celly | 3 | 1: 5.13; 2: 4.34; 3: 4.33 | 4.34 | 1.18 | GPCel-Input | 11.56 us | 42.96 KB | yes |  |
| E6 | compile | Cel.NET | 3 | 1: 10.42; 2: 9.12; 3: 9.38 | 9.38 | 1.14 | GPCel-Input | 25.02 us | 50.46 KB | yes |  |
| E6 | compile | TelusCel | 3 | 1: 2.73; 2: 2.56; 3: 2.56 | 2.56 | 1.07 | GPCel-Input | 6.72 us | 13.97 KB | yes |  |
| E6 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.68 us | 9.34 KB | no |  |
| E6 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.62 us | 6.47 KB | no |  |
| E6 | evaluate | Celly | 3 | 1: 1.81; 2: 1.72; 3: 1.72 | 1.72 | 1.06 | GPCel-Input | 353.7 ns | 912 B | yes |  |
| E6 | evaluate | Cel.NET | 3 | 1: 2.16; 2: 1.85; 3: 2.02 | 2.02 | 1.17 | GPCel-Input | 416.5 ns | 1.57 KB | yes |  |
| E6 | evaluate | TelusCel | 3 | 1: 46.73; 2: 46.75; 3: 45.28 | 46.73 | 1.03 | GPCel-Input | 9.36 us | 18.30 KB | yes |  |
| E6 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 257.4 ns | 328 B | no |  |
| E6 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 204.1 ns | 288 B | no |  |
| E6 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 300.1 ns | 160 B | no |  |
| E6 | full cycle | Celly | 3 | 1: 4.55; 2: 4.17; 3: 3.78 | 4.17 | 1.20 | GPCel-Input | 12.42 us | 43.84 KB | yes |  |
| E6 | full cycle | Cel.NET | 3 | 1: 9.18; 2: 8.33; 3: 8.22 | 8.33 | 1.12 | GPCel-Input | 25.35 us | 52.03 KB | yes |  |
| E6 | full cycle | TelusCel | 3 | 1: 6.50; 2: 5.88; 3: 5.49 | 5.88 | 1.18 | GPCel-Input | 17.51 us | 32.27 KB | yes |  |
| E6 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.13 us | 9.66 KB | no |  |
| E6 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.98 us | 6.79 KB | no |  |
| E6 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.52 us | 7.08 KB | no |  |
| E7 | compile | Celly | 3 | 1: 4.72; 2: 4.73; 3: 3.92 | 4.72 | 1.20 | GPCel-Input | 12.61 us | 45.15 KB | yes |  |
| E7 | compile | DotnetCel | 3 | 1: 3.12; 2: 3.03; 3: 3.13 | 3.12 | 1.03 | GPCel-Input | 8.50 us | 25.66 KB | yes |  |
| E7 | compile | Cel.NET | 3 | 1: 8.87; 2: 8.90; 3: 8.44 | 8.87 | 1.05 | GPCel-Input | 23.76 us | 53.69 KB | yes |  |
| E7 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.05 us | 7.67 KB | no |  |
| E7 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.73 us | 7.22 KB | no |  |
| E7 | evaluate | Celly | 3 | 1: 3.48; 2: 3.24; 3: 3.59 | 3.48 | 1.11 | GPCel-Input | 721.0 ns | 1.17 KB | yes |  |
| E7 | evaluate | DotnetCel | 3 | 1: 1.55; 2: 1.48; 3: 1.67 | 1.55 | 1.13 | GPCel-Input | 330.4 ns | 592 B | yes |  |
| E7 | evaluate | Cel.NET | 3 | 1: 2.49; 2: 2.17; 3: 2.43 | 2.43 | 1.15 | GPCel-Input | 487.5 ns | 1.61 KB | yes |  |
| E7 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 277.4 ns | 328 B | no |  |
| E7 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 211.9 ns | 288 B | no |  |
| E7 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 246.8 ns | 160 B | no |  |
| E7 | full cycle | Celly | 3 | 1: 4.81; 2: 4.52; 3: 3.84 | 4.52 | 1.25 | GPCel-Input | 13.57 us | 46.32 KB | yes |  |
| E7 | full cycle | DotnetCel | 3 | 1: 3.72; 2: 3.76; 3: 3.59 | 3.72 | 1.05 | GPCel-Input | 11.30 us | 28.74 KB | yes |  |
| E7 | full cycle | Cel.NET | 3 | 1: 8.89; 2: 7.63; 3: 7.61 | 7.63 | 1.17 | GPCel-Input | 24.51 us | 55.29 KB | yes |  |
| E7 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.48 us | 7.99 KB | no |  |
| E7 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.01 us | 7.54 KB | no |  |
| E7 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.55 us | 7.52 KB | no |  |
| E8 | compile | Celly | 3 | 1: 4.23; 2: 4.25; 3: 3.21 | 4.23 | 1.32 | GPCel-Input | 15.45 us | 52.50 KB | yes |  |
| E8 | compile | Cel.NET | 3 | 1: 13.51; 2: 12.75; 3: 11.48 | 12.75 | 1.18 | GPCel-Input | 49.05 us | 99.24 KB | yes |  |
| E8 | compile | TelusCel | 3 | 1: 2.58; 2: 2.55; 3: 2.21 | 2.55 | 1.17 | GPCel-Input | 9.45 us | 19.39 KB | yes |  |
| E8 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.80 us | 12.66 KB | no |  |
| E8 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.67 us | 10.89 KB | no |  |
| E8 | evaluate | Celly | 3 | 1: 3.47; 2: 3.70; 3: 3.11 | 3.47 | 1.19 | GPCel-Input | 775.9 ns | 1.66 KB | yes |  |
| E8 | evaluate | Cel.NET | 3 | 1: 2.06; 2: 2.03; 3: 1.84 | 2.03 | 1.12 | GPCel-Input | 441.2 ns | 1.64 KB | yes |  |
| E8 | evaluate | TelusCel | 3 | 1: 35.49; 2: 39.47; 3: 35.27 | 35.49 | 1.12 | GPCel-Input | 8.27 us | 16.02 KB | yes |  |
| E8 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 293.2 ns | 376 B | no |  |
| E8 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 224.9 ns | 336 B | no |  |
| E8 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 298.8 ns | 160 B | no |  |
| E8 | full cycle | Celly | 3 | 1: 4.18; 2: 4.20; 3: 3.26 | 4.18 | 1.29 | GPCel-Input | 16.54 us | 54.17 KB | yes |  |
| E8 | full cycle | Cel.NET | 3 | 1: 12.54; 2: 12.44; 3: 10.74 | 12.44 | 1.17 | GPCel-Input | 49.76 us | 100.87 KB | yes |  |
| E8 | full cycle | TelusCel | 3 | 1: 4.48; 2: 4.99; 3: 4.26 | 4.48 | 1.17 | GPCel-Input | 19.66 us | 35.41 KB | yes |  |
| E8 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.35 us | 13.02 KB | no |  |
| E8 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.01 us | 11.25 KB | no |  |
| E8 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 5.12 us | 11.46 KB | no |  |
| E9 | compile | Celly | 3 | 1: 5.49; 2: 5.56; 3: 4.55 | 5.49 | 1.22 | GPCel-Input | 14.39 us | 52.28 KB | yes |  |
| E9 | compile | DotnetCel | 3 | 1: 2.90; 2: 3.34; 3: 2.82 | 2.90 | 1.18 | GPCel-Input | 8.65 us | 26.69 KB | yes |  |
| E9 | compile | Cel.NET | 3 | 1: 12.67; 2: 12.74; 3: 10.82 | 12.67 | 1.18 | GPCel-Input | 33.84 us | 77.45 KB | yes |  |
| E9 | compile | TelusCel | 3 | 1: 1.73; 2: 1.86; 3: 1.55 | 1.73 | 1.20 | GPCel-Input | 4.81 us | 10.08 KB | yes |  |
| E9 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.75 us | 10.12 KB | no |  |
| E9 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.71 us | 8.44 KB | no |  |
| E9 | evaluate | Celly | 3 | 1: 1.57; 2: 1.74; 3: 1.45 | 1.57 | 1.20 | GPCel-Input | 368.9 ns | 1.11 KB | yes |  |
| E9 | evaluate | DotnetCel | 3 | 1: 1.59; 2: 1.91; 3: 1.64 | 1.64 | 1.20 | GPCel-Input | 401.0 ns | 808 B | yes |  |
| E9 | evaluate | Cel.NET | 3 | 1: 1.89; 2: 1.94; 3: 1.82 | 1.89 | 1.07 | GPCel-Input | 443.8 ns | 1.75 KB | yes |  |
| E9 | evaluate | TelusCel | 3 | 1: 26.37; 2: 28.85; 3: 22.28 | 26.37 | 1.29 | GPCel-Input | 6.14 us | 10.77 KB | yes |  |
| E9 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 307.4 ns | 512 B | no |  |
| E9 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 235.1 ns | 472 B | no |  |
| E9 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 333.7 ns | 344 B | no |  |
| E9 | full cycle | Celly | 3 | 1: 4.72; 2: 4.76; 3: 3.67 | 4.72 | 1.30 | GPCel-Input | 14.28 us | 53.50 KB | yes |  |
| E9 | full cycle | DotnetCel | 3 | 1: 3.17; 2: 3.61; 3: 3.24 | 3.24 | 1.14 | GPCel-Input | 10.84 us | 29.98 KB | yes |  |
| E9 | full cycle | Cel.NET | 3 | 1: 10.85; 2: 11.40; 3: 9.84 | 10.85 | 1.16 | GPCel-Input | 34.94 us | 79.19 KB | yes |  |
| E9 | full cycle | TelusCel | 3 | 1: 3.11; 2: 3.60; 3: 3.20 | 3.20 | 1.16 | GPCel-Input | 10.79 us | 20.85 KB | yes |  |
| E9 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.26 us | 10.63 KB | no |  |
| E9 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.28 us | 8.94 KB | no |  |
| E9 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.05 us | 9.19 KB | no |  |

R above 1.1 (all libraries): E1, compile, Celly: 4.91; E1, compile, DotnetCel: 3.94; E1, compile, Cel.NET: 9.77; E1, compile, TelusCel: 2.16; E1, evaluate, Celly: 1.70; E1, evaluate, DotnetCel: 1.71; E1, evaluate, Cel.NET: 2.10; E1, evaluate, TelusCel: 33.68; E1, full cycle, Celly: 4.14; E1, full cycle, DotnetCel: 4.24; E1, full cycle, Cel.NET: 7.89; E1, full cycle, TelusCel: 4.12; E2/10, compile, Celly: 3.19; E2/10, compile, DotnetCel: 2.62; E2/10, compile, Cel.NET: 6.25; E2/10, compile, TelusCel: 1.66; E2/10, evaluate, Celly: 2.42; E2/10, evaluate, DotnetCel: 3.38; E2/10, evaluate, Cel.NET: 2.79; E2/10, evaluate, TelusCel: 51.61; E2/10, full cycle, Celly: 2.94; E2/10, full cycle, DotnetCel: 3.11; E2/10, full cycle, Cel.NET: 5.52; E2/10, full cycle, TelusCel: 11.45; E2/100, compile, Celly: 3.14; E2/100, compile, DotnetCel: 2.57; E2/100, compile, Cel.NET: 5.89; E2/100, compile, TelusCel: 1.65; E2/100, evaluate, Celly: 2.34; E2/100, evaluate, DotnetCel: 3.53; E2/100, evaluate, Cel.NET: 2.93; E2/100, evaluate, TelusCel: 59.84; E2/100, full cycle, Celly: 2.60; E2/100, full cycle, DotnetCel: 3.33; E2/100, full cycle, Cel.NET: 4.07; E2/100, full cycle, TelusCel: 38.68; E3/10, compile, Celly: 3.27; E3/10, compile, DotnetCel: 2.93; E3/10, compile, Cel.NET: 5.50; E3/10, compile, TelusCel: 1.63; E3/10, evaluate, Celly: 2.82; E3/10, evaluate, DotnetCel: 3.44; E3/10, evaluate, Cel.NET: 2.73; E3/10, evaluate, TelusCel: 127.11; E3/10, full cycle, Celly: 3.07; E3/10, full cycle, DotnetCel: 3.50; E3/10, full cycle, Cel.NET: 5.31; E3/10, full cycle, TelusCel: 9.92; E3/100, compile, Celly: 3.17; E3/100, compile, DotnetCel: 2.75; E3/100, compile, Cel.NET: 5.90; E3/100, compile, TelusCel: 1.62; E3/100, evaluate, Celly: 13.51; E3/100, evaluate, DotnetCel: 11.83; E3/100, evaluate, Cel.NET: 7.29; E3/100, evaluate, TelusCel: 1182.25; E3/100, full cycle, Celly: 3.85; E3/100, full cycle, DotnetCel: 4.02; E3/100, full cycle, Cel.NET: 5.74; E3/100, full cycle, TelusCel: 80.14; E4/10, compile, Celly: 2.78; E4/10, compile, DotnetCel: 1.88; E4/10, compile, Cel.NET: 8.22; E4/10, compile, TelusCel: 1.30; E4/10, evaluate, Celly: 2.68; E4/10, evaluate, DotnetCel: 3.72; E4/10, evaluate, Cel.NET: 3.76; E4/10, evaluate, TelusCel: 51.45; E4/10, full cycle, Celly: 2.77; E4/10, full cycle, DotnetCel: 2.36; E4/10, full cycle, Cel.NET: 7.72; E4/10, full cycle, TelusCel: 7.29; E4/100, compile, Celly: 2.79; E4/100, compile, DotnetCel: 1.92; E4/100, compile, Cel.NET: 8.51; E4/100, compile, TelusCel: 1.27; E4/100, evaluate, Celly: 2.38; E4/100, evaluate, DotnetCel: 4.06; E4/100, evaluate, Cel.NET: 3.93; E4/100, evaluate, TelusCel: 55.89; E4/100, full cycle, Celly: 2.68; E4/100, full cycle, DotnetCel: 3.24; E4/100, full cycle, Cel.NET: 5.93; E4/100, full cycle, TelusCel: 33.25; E5, compile, Celly: 4.87; E5, compile, DotnetCel: 4.19; E5, compile, Cel.NET: 6.96; E5, compile, TelusCel: 2.20; E5, evaluate, Celly: 1.83; E5, evaluate, DotnetCel: 1.84; E5, evaluate, Cel.NET: 303.19; E5, evaluate, TelusCel: 35.89; E5, full cycle, Celly: 4.29; E5, full cycle, DotnetCel: 4.92; E5, full cycle, Cel.NET: 37.01; E5, full cycle, TelusCel: 5.29; E6, compile, Celly: 4.34; E6, compile, Cel.NET: 9.38; E6, compile, TelusCel: 2.56; E6, evaluate, Celly: 1.72; E6, evaluate, Cel.NET: 2.02; E6, evaluate, TelusCel: 46.73; E6, full cycle, Celly: 4.17; E6, full cycle, Cel.NET: 8.33; E6, full cycle, TelusCel: 5.88; E7, compile, Celly: 4.72; E7, compile, DotnetCel: 3.12; E7, compile, Cel.NET: 8.87; E7, evaluate, Celly: 3.48; E7, evaluate, DotnetCel: 1.55; E7, evaluate, Cel.NET: 2.43; E7, full cycle, Celly: 4.52; E7, full cycle, DotnetCel: 3.72; E7, full cycle, Cel.NET: 7.63; E8, compile, Celly: 4.23; E8, compile, Cel.NET: 12.75; E8, compile, TelusCel: 2.55; E8, evaluate, Celly: 3.47; E8, evaluate, Cel.NET: 2.03; E8, evaluate, TelusCel: 35.49; E8, full cycle, Celly: 4.18; E8, full cycle, Cel.NET: 12.44; E8, full cycle, TelusCel: 4.48; E9, compile, Celly: 5.49; E9, compile, DotnetCel: 2.90; E9, compile, Cel.NET: 12.67; E9, compile, TelusCel: 1.73; E9, evaluate, Celly: 1.57; E9, evaluate, DotnetCel: 1.64; E9, evaluate, Cel.NET: 1.89; E9, evaluate, TelusCel: 26.37; E9, full cycle, Celly: 4.72; E9, full cycle, DotnetCel: 3.24; E9, full cycle, Cel.NET: 10.85; E9, full cycle, TelusCel: 3.20.

**Rows of GreatPartsCel.**

- R above 1.1: none.
- Ratios on both sides of 1.1: none.

## Limits of the speed-ups

Job: Default; started 2026-11-27T10:54:05.581Z, finished 2026-11-27T11:26:09.632Z. Medians of BenchmarkDotNet on the code of this commit; the reports are in `results/thresholds/bdn/results`.

| Limit | Rule | Verdict |
|---|---|---|
| hybrid search of the literal prefix of matches, 64 code units | .NET at M = 64 is not slower than Knuth-Morris-Pratt; otherwise the limit is 32 | holds: 64 |
| build of the automaton of the pattern E5 | the median of the build is at most 2000 ns | holds: 1094 ns |
| index of the values of a list (distinct, sets): scan or buckets, the limit of the library is 64 values | a diagnostic without a verdict: the threshold by the rule is the largest N at which, and at every smaller N measured (8, 16, 24, 32, 48, 64), scan / buckets is at most 1.0 for distinct and for sets.contains; 8 when the scan is slower already at N = 8 | no verdict; threshold by the rule: 32 (the limit of the library: 64) |
| substring search: time per cost unit | a diagnostic without a verdict: a rise of the time per unit above 2 times from M = 64 to M = 1024 is the input of the decision on the divisor 64 | no verdict; time per unit from M = 64 to M = 1024: 0.06 times for a literal, 0.44 times for the data |
| simple entry: evaluate and data | a diagnostic without a verdict: the time of the call on a prepared anonymous object, and the time of building that object | no verdict; scalars: evaluate 246.9 ns, data 14.3 ns; lines: evaluate 1.25 us, data 15.5 ns |
| CelMap.Create of the row of an order | a diagnostic without a verdict: the time and the allocation of a map of three entries made with a collection expression | no verdict; 46.7 ns, 248 B |

### Search of the literal prefix of matches, the text (ab)^5000

M is the length of the pattern, the slowest case for the search of .NET. "Library" is the search as it runs: .NET up to the limit, Knuth-Morris-Pratt above. Net / KMP below 1 means .NET is faster.

| M | KMP | .NET | Library | Net / KMP |
|--:|--:|--:|--:|--:|
| 48 | 11.21 us | 9.99 us | 9.83 us | 0.89 |
| 64 | 11.16 us | 9.91 us | 10.01 us | 0.89 |
| 65 | 11.18 us | 461.8 ns | 11.28 us | 0.04 |
| 80 | 11.20 us | 13.90 us | 11.26 us | 1.24 |

### Search of a substring by the functions of strings: time per cost unit

contains on the text (ab)^5000 (10000 code units) and a pattern of M code units, the slowest case for the search of .NET. "Literal" is a pattern that is a literal of the expression (SearchValues<string>), "data" a pattern read from a variable (the ordinal search); the time is that of the whole evaluation. The cost is 1 + ceil((n + M) / 10) * q, q = max(1, ceil(M / 64)), without the reads of the variables. No verdict.

| M | Cost | Literal | Literal per unit | Data | Data per unit |
|--:|--:|--:|--:|--:|--:|
| 64 | 1008 | 509.4 ns | 0.5 ns | 11.47 us | 11.4 ns |
| 256 | 4105 | 574.6 ns | 0.1 ns | 25.18 us | 6.1 ns |
| 1024 | 17649 | 534.2 ns | 0.0 ns | 88.94 us | 5.0 ns |

### Build of the automaton

| Pattern | Parse and program | Automaton |
|---|--:|--:|
| E5 | 690.8 ns | 1.09 us |
| UUID | 1.41 us | 3.01 us |

### Index of the values: the lists of N integers

"Library" is the index as the library makes it (the scan up to 64 values, the buckets above); "scan" and "buckets" force that way at any N; "one hash" gives every value the same hash; "pairwise" are integers of 2^53 and more, compared with each other in order. Scan / buckets below 1 means the scan is faster. No verdict: the threshold by the rule is 32; the limit of the library is set by a decision on this table.

| Operation | N | Library | Scan | Buckets | One hash | Pairwise | Scan / buckets |
|---|--:|--:|--:|--:|--:|--:|--:|
| distinct | 8 | 222.8 ns | 213.8 ns | 290.0 ns | 336.9 ns | 316.4 ns | 0.74 |
| distinct | 16 | 395.0 ns | 370.2 ns | 443.5 ns | 890.3 ns | 695.5 ns | 0.83 |
| distinct | 24 | 557.2 ns | 523.7 ns | 576.3 ns | 1.89 us | 1.27 us | 0.91 |
| distinct | 32 | 671.3 ns | 681.9 ns | 766.0 ns | 3.32 us | 2.21 us | 0.89 |
| distinct | 48 | 1.06 us | 982.4 ns | 1.08 us | 7.63 us | 4.28 us | 0.91 |
| distinct | 64 | 1.32 us | 1.30 us | 1.40 us | 14.08 us | 7.06 us | 0.93 |
| sets.contains | 8 | 359.7 ns | 348.2 ns | 418.7 ns | 546.7 ns | 426.0 ns | 0.83 |
| sets.contains | 16 | 625.8 ns | 635.9 ns | 696.7 ns | 1.26 us | 950.8 ns | 0.91 |
| sets.contains | 24 | 910.8 ns | 921.8 ns | 1.02 us | 2.54 us | 1.71 us | 0.90 |
| sets.contains | 32 | 1.23 us | 1.20 us | 1.31 us | 4.36 us | 2.99 us | 0.92 |
| sets.contains | 48 | 1.77 us | 1.86 us | 1.85 us | 9.76 us | 5.92 us | 1.01 |
| sets.contains | 64 | 2.38 us | 2.48 us | 2.50 us | 17.40 us | 8.65 us | 0.99 |

### distinct and sets.contains through the public API

The evaluation of the program on a list of N integers; the time per element shows a jump at the limit, if there is one.

| N | distinct | per element | sets.contains | per element |
|--:|--:|--:|--:|--:|
| 16 | 568.0 ns | 35.5 ns | 1.13 us | 70.3 ns |
| 32 | 911.3 ns | 28.5 ns | 1.64 us | 51.3 ns |
| 33 | 964.1 ns | 29.2 ns | 1.74 us | 52.6 ns |
| 48 | 1.29 us | 26.8 ns | 2.36 us | 49.2 ns |
| 64 | 1.67 us | 26.0 ns | 3.11 us | 48.6 ns |

### Simple entry: evaluate and data

The call `CelExpression.Evaluate` on an anonymous object with the scalars of the order, the customer and the lines as properties, as the variant of the rig passes them. "Evaluate" is the call on an object that was built before the measurement, with the text of the variant with the input of the environment ("scalars" is the text of E1, "lines" the text of E2 over 10 lines); "data" is the building of that object for one call. No verdict.

| Text | Evaluate | Allocated | Data | Allocated |
|---|--:|--:|--:|--:|
| scalars | 246.9 ns | 48 B | 14.3 ns | 112 B |
| lines | 1.25 us | 48 B | 15.5 ns | 112 B |

### CelMap.Create of the row of an order

A map of three entries (`sku`, `qty`, `price`) made with a collection expression. No verdict.

| Time | Allocated |
|--:|--:|
| 46.7 ns | 248 B |
