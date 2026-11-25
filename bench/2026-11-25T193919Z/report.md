# Benchmark rig

- Date and time of the report (UTC): 2026-11-25 19:39:19
- Commit of great-parts/great-parts-cel: [`3292809e6c361befcf8367805f0f60d67f52517d`](https://github.com/great-parts/great-parts-cel/commit/3292809e6c361befcf8367805f0f60d67f52517d)
- Runs: 3
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45640347087 (GitHub keeps a run for 90 days; this report does not depend on it)

The summary of the rig follows. The full reports of BenchmarkDotNet are in the artifact `bench-results` of the run, for 90 days.

## Conditions

- Date of the summary: 2026-11-25T19:39:19.772Z
- Commit: 3292809e6c361befcf8367805f0f60d67f52517d
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45640347087
- Runner: Linux X64, image ubuntu26 20261116.202.1
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

- BenchmarkDotNet 0.16.0-preview.2; OS: Linux Ubuntu 26.04.1 LTS (Resolute Raccoon); processor: INTEL XEON PLATINUM 8573C
- Cores: 2 physical, 4 logical; architecture: X64
- Runtime: .NET 11.0.0 (11.0.0, 11.0.26.53018); SDK: 11.0.100; configuration: RELEASE

## Runs

- Run 1: 2026-11-25T15:01:42.681Z - 2026-11-25T16:23:02.896Z; job: Default; cases: all; filter: *
- Run 2: 2026-11-25T16:23:02.897Z - 2026-11-25T17:44:13.322Z; job: Default; cases: all; filter: *
- Run 3: 2026-11-25T17:44:13.322Z - 2026-11-25T19:04:46.022Z; job: Default; cases: all; filter: *

The table of each run is `results/runs/<k>/summary.md`.

## Summary over the runs

The ratio of a row in a run is to the fastest library with a correct result in the same run (for a variant of GreatPartsCel: to the fastest other library or to itself, not to its other variants). R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has 3 runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others. "Allocated" is the median over the runs of the bytes allocated per operation that BenchmarkDotNet reports (MemoryDiagnoser), less the allocation of making the text for compile and full cycle, as the time is.

| Scenario | Operation | Library | Runs | Ratios by run | R | Spread | Fastest by run | Median time over the runs | Allocated | R > 1.1 | Both sides of 1.1 |
|---|---|---|--:|---|--:|--:|---|--:|--:|---|---|
| E1 | compile | Celly | 3 | 1: 4.56; 2: 4.63; 3: 4.56 | 4.56 | 1.01 | GPCel-Input | 14.59 us | 42.81 KB | yes |  |
| E1 | compile | DotnetCel | 3 | 1: 3.42; 2: 3.44; 3: 3.43 | 3.43 | 1.01 | GPCel-Input | 10.94 us | 26.31 KB | yes |  |
| E1 | compile | Cel.NET | 3 | 1: 8.98; 2: 8.94; 3: 8.92 | 8.94 | 1.01 | GPCel-Input | 28.46 us | 49.87 KB | yes |  |
| E1 | compile | TelusCel | 3 | 1: 1.96; 2: 2.02; 3: 1.99 | 1.99 | 1.03 | GPCel-Input | 6.34 us | 10.33 KB | yes |  |
| E1 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.08 us | 8.82 KB | no |  |
| E1 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.19 us | 6.97 KB | no |  |
| E1 | evaluate | Celly | 3 | 1: 1.87; 2: 2.01; 3: 1.88 | 1.88 | 1.07 | GPCel-Input | 459.0 ns | 832 B | yes |  |
| E1 | evaluate | DotnetCel | 3 | 1: 1.76; 2: 1.88; 3: 1.76 | 1.76 | 1.07 | GPCel-Input | 431.5 ns | 560 B | yes |  |
| E1 | evaluate | Cel.NET | 3 | 1: 2.08; 2: 2.26; 3: 2.17 | 2.17 | 1.09 | GPCel-Input | 516.7 ns | 1.52 KB | yes |  |
| E1 | evaluate | TelusCel | 3 | 1: 29.17; 2: 31.22; 3: 32.92 | 31.22 | 1.13 | GPCel-Input | 7.21 us | 11.16 KB | yes |  |
| E1 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 292.6 ns | 288 B | no |  |
| E1 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 243.6 ns | 288 B | no |  |
| E1 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 399.0 ns | 160 B | no |  |
| E1 | full cycle | Celly | 3 | 1: 4.25; 2: 4.17; 3: 4.24 | 4.24 | 1.02 | GPCel-Input | 15.11 us | 43.60 KB | yes |  |
| E1 | full cycle | DotnetCel | 3 | 1: 3.97; 2: 3.89; 3: 3.96 | 3.96 | 1.02 | GPCel-Input | 14.14 us | 29.37 KB | yes |  |
| E1 | full cycle | Cel.NET | 3 | 1: 8.06; 2: 8.03; 3: 8.17 | 8.06 | 1.02 | GPCel-Input | 29.11 us | 51.38 KB | yes |  |
| E1 | full cycle | TelusCel | 3 | 1: 4.01; 2: 3.91; 3: 3.95 | 3.95 | 1.03 | GPCel-Input | 14.16 us | 21.49 KB | yes |  |
| E1 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.65 us | 9.14 KB | no |  |
| E1 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.58 us | 7.28 KB | no |  |
| E1 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.26 us | 7.39 KB | no |  |
| E2/10 | compile | Celly | 3 | 1: 3.20; 2: 3.20; 3: 3.18 | 3.20 | 1.01 | GPCel-Input | 19.09 us | 54.52 KB | yes |  |
| E2/10 | compile | DotnetCel | 3 | 1: 2.47; 2: 2.50; 3: 2.48 | 2.48 | 1.01 | GPCel-Input | 14.89 us | 32.52 KB | yes |  |
| E2/10 | compile | Cel.NET | 3 | 1: 6.32; 2: 6.39; 3: 6.29 | 6.32 | 1.02 | GPCel-Input | 38.00 us | 71.99 KB | yes |  |
| E2/10 | compile | TelusCel | 3 | 1: 1.62; 2: 1.64; 3: 1.62 | 1.62 | 1.02 | GPCel-Input | 9.76 us | 15.50 KB | yes |  |
| E2/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.27 us | 12.21 KB | no |  |
| E2/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.97 us | 11.57 KB | no |  |
| E2/10 | evaluate | Celly | 3 | 1: 2.47; 2: 2.48; 3: 2.54 | 2.48 | 1.03 | GPCel-Input | 3.66 us | 2.96 KB | yes |  |
| E2/10 | evaluate | DotnetCel | 3 | 1: 3.32; 2: 3.63; 3: 3.70 | 3.63 | 1.11 | GPCel-Input | 5.20 us | 4.07 KB | yes |  |
| E2/10 | evaluate | Cel.NET | 3 | 1: 2.95; 2: 3.05; 3: 3.03 | 3.03 | 1.04 | GPCel-Input | 4.37 us | 6.15 KB | yes |  |
| E2/10 | evaluate | TelusCel | 3 | 1: 51.60; 2: 56.65; 3: 55.17 | 55.17 | 1.10 | GPCel-Input | 79.49 us | 111.34 KB | yes |  |
| E2/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.57 us | 336 B | no |  |
| E2/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.44 us | 336 B | no |  |
| E2/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 2.09 us | 800 B | no |  |
| E2/10 | full cycle | Celly | 3 | 1: 2.94; 2: 3.01; 3: 2.97 | 2.97 | 1.02 | GPCel-Input | 23.50 us | 57.47 KB | yes |  |
| E2/10 | full cycle | DotnetCel | 3 | 1: 3.00; 2: 3.05; 3: 3.06 | 3.05 | 1.02 | GPCel-Input | 23.93 us | 39.90 KB | yes |  |
| E2/10 | full cycle | Cel.NET | 3 | 1: 5.48; 2: 5.50; 3: 5.43 | 5.48 | 1.01 | GPCel-Input | 43.14 us | 78.14 KB | yes |  |
| E2/10 | full cycle | TelusCel | 3 | 1: 12.60; 2: 13.08; 3: 11.63 | 12.60 | 1.12 | GPCel-Input | 99.41 us | 126.84 KB | yes |  |
| E2/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 8.31 us | 12.57 KB | no |  |
| E2/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 7.89 us | 11.93 KB | no |  |
| E2/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 9.13 us | 12.61 KB | no |  |
| E2/100 | compile | Celly | 3 | 1: 3.17; 2: 3.19; 3: 3.18 | 3.18 | 1.01 | GPCel-Input | 19.06 us | 54.51 KB | yes |  |
| E2/100 | compile | DotnetCel | 3 | 1: 2.47; 2: 2.46; 3: 2.50 | 2.47 | 1.02 | GPCel-Input | 14.84 us | 32.52 KB | yes |  |
| E2/100 | compile | Cel.NET | 3 | 1: 6.33; 2: 6.38; 3: 6.30 | 6.33 | 1.01 | GPCel-Input | 37.97 us | 71.99 KB | yes |  |
| E2/100 | compile | TelusCel | 3 | 1: 1.63; 2: 1.64; 3: 1.63 | 1.63 | 1.01 | GPCel-Input | 9.78 us | 15.50 KB | yes |  |
| E2/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.27 us | 12.22 KB | no |  |
| E2/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.00 us | 11.57 KB | no |  |
| E2/100 | evaluate | Celly | 3 | 1: 2.54; 2: 2.60; 3: 2.55 | 2.55 | 1.02 | GPCel-Input, GreatPartsCel | 32.29 us | 21.66 KB | yes |  |
| E2/100 | evaluate | DotnetCel | 3 | 1: 3.72; 2: 3.72; 3: 3.76 | 3.72 | 1.01 | GPCel-Input, GreatPartsCel | 47.53 us | 31.91 KB | yes |  |
| E2/100 | evaluate | Cel.NET | 3 | 1: 3.06; 2: 3.05; 3: 3.08 | 3.06 | 1.01 | GPCel-Input, GreatPartsCel | 38.97 us | 54.28 KB | yes |  |
| E2/100 | evaluate | TelusCel | 3 | 1: 61.07; 2: 60.40; 3: 61.79 | 61.07 | 1.02 | GPCel-Input, GreatPartsCel | 770.86 us | 1.03 MB | yes |  |
| E2/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 12.81 us | 336 B | no |  |
| E2/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 12.76 us | 336 B | no |  |
| E2/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 18.34 us | 6.41 KB | no |  |
| E2/100 | full cycle | Celly | 3 | 1: 2.90; 2: 2.71; 3: 2.75 | 2.75 | 1.07 | GPCel-Input, GreatPartsCel | 52.53 us | 76.17 KB | yes |  |
| E2/100 | full cycle | DotnetCel | 3 | 1: 3.38; 2: 3.37; 3: 3.47 | 3.38 | 1.03 | GPCel-Input, GreatPartsCel | 65.27 us | 67.74 KB | yes |  |
| E2/100 | full cycle | Cel.NET | 3 | 1: 4.07; 2: 4.09; 3: 4.21 | 4.09 | 1.04 | GPCel-Input, GreatPartsCel | 79.36 us | 126.28 KB | yes |  |
| E2/100 | full cycle | TelusCel | 3 | 1: 45.55; 2: 44.62; 3: 42.14 | 44.62 | 1.08 | GPCel-Input, GreatPartsCel | 865.10 us | 1.05 MB | yes |  |
| E2/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 19.79 us | 12.58 KB | no |  |
| E2/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 19.03 us | 11.93 KB | no |  |
| E2/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 24.65 us | 18.23 KB | no |  |
| E3/10 | compile | Celly | 3 | 1: 3.22; 2: 3.26; 3: 3.28 | 3.26 | 1.02 | GPCel-Input | 15.57 us | 44.31 KB | yes |  |
| E3/10 | compile | DotnetCel | 3 | 1: 2.76; 2: 2.78; 3: 2.79 | 2.78 | 1.01 | GPCel-Input | 13.35 us | 30.26 KB | yes |  |
| E3/10 | compile | Cel.NET | 3 | 1: 5.90; 2: 5.88; 3: 5.77 | 5.88 | 1.02 | GPCel-Input | 28.07 us | 53.13 KB | yes |  |
| E3/10 | compile | TelusCel | 3 | 1: 1.64; 2: 1.66; 3: 1.64 | 1.64 | 1.02 | GPCel-Input | 7.92 us | 13.38 KB | yes |  |
| E3/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.12 us | 10.80 KB | no |  |
| E3/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.83 us | 10.32 KB | no |  |
| E3/10 | evaluate | Celly | 3 | 1: 2.79; 2: 2.83; 3: 3.00 | 2.83 | 1.08 | GPCel-Input | 962.7 ns | 1.61 KB | yes |  |
| E3/10 | evaluate | DotnetCel | 3 | 1: 3.55; 2: 3.56; 3: 3.80 | 3.56 | 1.07 | GPCel-Input | 1.23 us | 1.78 KB | yes |  |
| E3/10 | evaluate | Cel.NET | 3 | 1: 2.79; 2: 2.92; 3: 3.01 | 2.92 | 1.08 | GPCel-Input | 977.3 ns | 2.07 KB | yes |  |
| E3/10 | evaluate | TelusCel | 3 | 1: 128.34; 2: 134.30; 3: 140.07 | 134.30 | 1.09 | GPCel-Input | 45.35 us | 62.83 KB | yes |  |
| E3/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 431.5 ns | 336 B | no |  |
| E3/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 337.7 ns | 336 B | no |  |
| E3/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 855.5 ns | 544 B | no |  |
| E3/10 | full cycle | Celly | 3 | 1: 3.13; 2: 3.14; 3: 3.12 | 3.13 | 1.01 | GPCel-Input | 16.72 us | 45.92 KB | yes |  |
| E3/10 | full cycle | DotnetCel | 3 | 1: 3.42; 2: 3.44; 3: 3.49 | 3.44 | 1.02 | GPCel-Input | 18.33 us | 35.35 KB | yes |  |
| E3/10 | full cycle | Cel.NET | 3 | 1: 5.44; 2: 5.55; 3: 5.55 | 5.55 | 1.02 | GPCel-Input | 29.55 us | 55.20 KB | yes |  |
| E3/10 | full cycle | TelusCel | 3 | 1: 10.18; 2: 11.13; 3: 10.10 | 10.18 | 1.10 | GPCel-Input | 54.44 us | 76.20 KB | yes |  |
| E3/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.74 us | 11.16 KB | no |  |
| E3/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.35 us | 10.68 KB | no |  |
| E3/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 6.43 us | 11.11 KB | no |  |
| E3/100 | compile | Celly | 3 | 1: 3.25; 2: 3.31; 3: 3.32 | 3.31 | 1.02 | GPCel-Input | 15.82 us | 44.38 KB | yes |  |
| E3/100 | compile | DotnetCel | 3 | 1: 2.78; 2: 2.82; 3: 2.82 | 2.82 | 1.02 | GPCel-Input | 13.48 us | 30.26 KB | yes |  |
| E3/100 | compile | Cel.NET | 3 | 1: 5.89; 2: 6.11; 3: 5.86 | 5.89 | 1.04 | GPCel-Input | 28.24 us | 53.14 KB | yes |  |
| E3/100 | compile | TelusCel | 3 | 1: 1.66; 2: 1.65; 3: 1.66 | 1.66 | 1.01 | GPCel-Input | 7.93 us | 13.38 KB | yes |  |
| E3/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.12 us | 10.80 KB | no |  |
| E3/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.79 us | 10.32 KB | no |  |
| E3/100 | evaluate | Celly | 3 | 1: 12.50; 2: 13.12; 3: 13.48 | 13.12 | 1.08 | GPCel-Input | 4.41 us | 6.95 KB | yes |  |
| E3/100 | evaluate | DotnetCel | 3 | 1: 11.26; 2: 12.39; 3: 12.46 | 12.39 | 1.11 | GPCel-Input | 4.17 us | 7.12 KB | yes |  |
| E3/100 | evaluate | Cel.NET | 3 | 1: 7.21; 2: 7.26; 3: 7.84 | 7.26 | 1.09 | GPCel-Input | 2.53 us | 8.81 KB | yes |  |
| E3/100 | evaluate | TelusCel | 3 | 1: 1159.07; 2: 1221.07; 3: 1174.05 | 1174.05 | 1.05 | GPCel-Input | 405.74 us | 570.20 KB | yes |  |
| E3/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 440.0 ns | 336 B | no |  |
| E3/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 342.0 ns | 336 B | no |  |
| E3/100 | evaluate | GPCel-Simple | 3 | 1: 1.36; 2: 1.39; 3: 1.27 | 1.36 | 1.10 | Cel.NET | 3.41 us | 3.34 KB | yes |  |
| E3/100 | full cycle | Celly | 3 | 1: 3.74; 2: 3.84; 3: 3.85 | 3.84 | 1.03 | GPCel-Input | 20.48 us | 51.27 KB | yes |  |
| E3/100 | full cycle | DotnetCel | 3 | 1: 3.98; 2: 4.06; 3: 4.04 | 4.04 | 1.02 | GPCel-Input | 21.65 us | 40.69 KB | yes |  |
| E3/100 | full cycle | Cel.NET | 3 | 1: 5.76; 2: 5.85; 3: 5.81 | 5.81 | 1.02 | GPCel-Input | 31.21 us | 61.94 KB | yes |  |
| E3/100 | full cycle | TelusCel | 3 | 1: 77.41; 2: 78.15; 3: 86.96 | 78.15 | 1.12 | GPCel-Input | 419.86 us | 583.57 KB | yes |  |
| E3/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.75 us | 11.16 KB | no |  |
| E3/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.37 us | 10.68 KB | no |  |
| E3/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 9.07 us | 13.92 KB | no |  |
| E4/10 | compile | Celly | 3 | 1: 2.87; 2: 2.87; 3: 2.88 | 2.87 | 1.00 | GPCel-Input | 29.47 us | 75.71 KB | yes |  |
| E4/10 | compile | DotnetCel | 3 | 1: 1.83; 2: 1.89; 3: 1.86 | 1.86 | 1.03 | GPCel-Input | 19.03 us | 37.80 KB | yes |  |
| E4/10 | compile | Cel.NET | 3 | 1: 8.30; 2: 8.49; 3: 8.38 | 8.38 | 1.02 | GPCel-Input | 85.94 us | 134.64 KB | yes |  |
| E4/10 | compile | TelusCel | 3 | 1: 1.31; 2: 1.34; 3: 1.31 | 1.31 | 1.02 | GPCel-Input | 13.53 us | 21.26 KB | yes |  |
| E4/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 10.51 us | 18.86 KB | no |  |
| E4/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 10.26 us | 18.45 KB | no |  |
| E4/10 | evaluate | Celly | 3 | 1: 2.87; 2: 2.84; 3: 3.31 | 2.87 | 1.17 | GPCel-Input | 3.63 us | 4.08 KB | yes |  |
| E4/10 | evaluate | DotnetCel | 3 | 1: 4.00; 2: 4.00; 3: 3.97 | 4.00 | 1.01 | GPCel-Input | 5.09 us | 5.31 KB | yes |  |
| E4/10 | evaluate | Cel.NET | 3 | 1: 3.87; 2: 3.98; 3: 3.92 | 3.92 | 1.03 | GPCel-Input | 5.03 us | 7.08 KB | yes |  |
| E4/10 | evaluate | TelusCel | 3 | 1: 56.58; 2: 55.15; 3: 62.93 | 56.58 | 1.14 | GPCel-Input | 71.35 us | 98.86 KB | yes |  |
| E4/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.41 us | 840 B | no |  |
| E4/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.28 us | 840 B | no |  |
| E4/10 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 1.98 us | 1.27 KB | no |  |
| E4/10 | full cycle | Celly | 3 | 1: 2.80; 2: 2.88; 3: 2.84 | 2.84 | 1.03 | GPCel-Input | 34.12 us | 79.80 KB | yes |  |
| E4/10 | full cycle | DotnetCel | 3 | 1: 2.37; 2: 2.36; 3: 2.37 | 2.37 | 1.00 | GPCel-Input | 28.47 us | 46.42 KB | yes |  |
| E4/10 | full cycle | Cel.NET | 3 | 1: 7.66; 2: 7.70; 3: 7.72 | 7.70 | 1.01 | GPCel-Input | 92.40 us | 141.72 KB | yes |  |
| E4/10 | full cycle | TelusCel | 3 | 1: 7.37; 2: 7.21; 3: 7.02 | 7.21 | 1.05 | GPCel-Input | 86.58 us | 120.12 KB | yes |  |
| E4/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 12.40 us | 19.71 KB | no |  |
| E4/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 12.03 us | 19.30 KB | no |  |
| E4/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 13.48 us | 19.98 KB | no |  |
| E4/100 | compile | Celly | 3 | 1: 2.85; 2: 2.86; 3: 2.92 | 2.86 | 1.02 | GPCel-Input | 29.48 us | 75.70 KB | yes |  |
| E4/100 | compile | DotnetCel | 3 | 1: 1.85; 2: 1.85; 3: 1.87 | 1.85 | 1.01 | GPCel-Input | 19.10 us | 37.80 KB | yes |  |
| E4/100 | compile | Cel.NET | 3 | 1: 8.40; 2: 8.47; 3: 8.45 | 8.45 | 1.01 | GPCel-Input | 86.69 us | 134.64 KB | yes |  |
| E4/100 | compile | TelusCel | 3 | 1: 1.30; 2: 1.32; 3: 1.33 | 1.32 | 1.02 | GPCel-Input | 13.51 us | 21.26 KB | yes |  |
| E4/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 10.46 us | 18.86 KB | no |  |
| E4/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 10.23 us | 18.45 KB | no |  |
| E4/100 | evaluate | Celly | 3 | 1: 2.36; 2: 2.67; 3: 2.58 | 2.58 | 1.13 | GPCel-Input, GreatPartsCel | 38.90 us | 42.44 KB | yes |  |
| E4/100 | evaluate | DotnetCel | 3 | 1: 3.97; 2: 4.01; 3: 3.85 | 3.97 | 1.04 | GPCel-Input, GreatPartsCel | 59.43 us | 70.80 KB | yes |  |
| E4/100 | evaluate | Cel.NET | 3 | 1: 4.05; 2: 3.99; 3: 3.91 | 3.99 | 1.03 | GPCel-Input, GreatPartsCel | 59.24 us | 96.53 KB | yes |  |
| E4/100 | evaluate | TelusCel | 3 | 1: 55.49; 2: 57.49; 3: 63.92 | 57.49 | 1.15 | GPCel-Input, GreatPartsCel | 852.70 us | 1.15 MB | yes |  |
| E4/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 15.07 us | 9.57 KB | no |  |
| E4/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 15.35 us | 9.57 KB | no |  |
| E4/100 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 19.70 us | 15.65 KB | no |  |
| E4/100 | full cycle | Celly | 3 | 1: 2.68; 2: 2.67; 3: 2.85 | 2.68 | 1.07 | GreatPartsCel, GPCel-Input | 69.50 us | 118.15 KB | yes |  |
| E4/100 | full cycle | DotnetCel | 3 | 1: 3.23; 2: 3.16; 3: 3.18 | 3.18 | 1.02 | GreatPartsCel, GPCel-Input | 82.20 us | 111.91 KB | yes |  |
| E4/100 | full cycle | Cel.NET | 3 | 1: 5.83; 2: 5.84; 3: 5.93 | 5.84 | 1.02 | GreatPartsCel, GPCel-Input | 149.48 us | 231.17 KB | yes |  |
| E4/100 | full cycle | TelusCel | 3 | 1: 33.66; 2: 37.81; 3: 33.99 | 33.99 | 1.12 | GreatPartsCel, GPCel-Input | 863.74 us | 1.17 MB | yes |  |
| E4/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 25.66 us | 28.46 KB | no |  |
| E4/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 26.81 us | 28.05 KB | no |  |
| E4/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 34.22 us | 34.35 KB | no |  |
| E5 | compile | Celly | 3 | 1: 5.10; 2: 5.06; 3: 5.13 | 5.10 | 1.01 | GPCel-Input | 11.84 us | 35.54 KB | yes |  |
| E5 | compile | DotnetCel | 3 | 1: 4.11; 2: 4.07; 3: 4.13 | 4.11 | 1.01 | GPCel-Input | 9.54 us | 24.02 KB | yes |  |
| E5 | compile | Cel.NET | 3 | 1: 7.03; 2: 7.01; 3: 7.09 | 7.03 | 1.01 | GPCel-Input | 16.33 us | 30.58 KB | yes |  |
| E5 | compile | TelusCel | 3 | 1: 2.10; 2: 2.11; 3: 2.12 | 2.11 | 1.01 | GPCel-Input | 4.90 us | 8.64 KB | yes |  |
| E5 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.72 us | 6.43 KB | no |  |
| E5 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.32 us | 5.50 KB | no |  |
| E5 | evaluate | Celly | 3 | 1: 1.84; 2: 1.75; 3: 1.94 | 1.84 | 1.11 | GPCel-Input | 487.1 ns | 752 B | yes |  |
| E5 | evaluate | DotnetCel | 3 | 1: 1.96; 2: 1.76; 3: 1.96 | 1.96 | 1.11 | GPCel-Input | 500.6 ns | 448 B | yes |  |
| E5 | evaluate | Cel.NET | 3 | 1: 338.40; 2: 311.57; 3: 337.73 | 337.73 | 1.09 | GPCel-Input | 86.60 us | 252.50 KB | yes |  |
| E5 | evaluate | TelusCel | 3 | 1: 36.09; 2: 33.16; 3: 35.93 | 35.93 | 1.09 | GPCel-Input | 9.24 us | 14.34 KB | yes |  |
| E5 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 355.2 ns | 288 B | no |  |
| E5 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 255.9 ns | 288 B | no |  |
| E5 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 394.0 ns | 160 B | no |  |
| E5 | full cycle | Celly | 3 | 1: 4.49; 2: 4.48; 3: 4.57 | 4.49 | 1.02 | GPCel-Input | 12.45 us | 36.28 KB | yes |  |
| E5 | full cycle | DotnetCel | 3 | 1: 4.90; 2: 4.77; 3: 4.99 | 4.90 | 1.05 | GPCel-Input | 13.46 us | 27.60 KB | yes |  |
| E5 | full cycle | Cel.NET | 3 | 1: 39.11; 2: 38.57; 3: 39.47 | 39.11 | 1.02 | GPCel-Input | 107.52 us | 283.10 KB | yes |  |
| E5 | full cycle | TelusCel | 3 | 1: 5.72; 2: 5.32; 3: 5.66 | 5.66 | 1.08 | GPCel-Input | 15.53 us | 22.98 KB | yes |  |
| E5 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.27 us | 6.74 KB | no |  |
| E5 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.75 us | 5.81 KB | no |  |
| E5 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 3.41 us | 5.91 KB | no |  |
| E6 | compile | Celly | 3 | 1: 4.47; 2: 4.42; 3: 4.46 | 4.46 | 1.01 | GPCel-Input | 14.82 us | 42.96 KB | yes |  |
| E6 | compile | Cel.NET | 3 | 1: 9.44; 2: 9.42; 3: 9.41 | 9.42 | 1.00 | GPCel-Input | 31.32 us | 50.47 KB | yes |  |
| E6 | compile | TelusCel | 3 | 1: 2.57; 2: 2.58; 3: 2.58 | 2.58 | 1.00 | GPCel-Input | 8.58 us | 13.97 KB | yes |  |
| E6 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.51 us | 9.18 KB | no |  |
| E6 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.32 us | 6.46 KB | no |  |
| E6 | evaluate | Celly | 3 | 1: 1.74; 2: 1.75; 3: 1.82 | 1.75 | 1.04 | GPCel-Input | 447.8 ns | 912 B | yes |  |
| E6 | evaluate | Cel.NET | 3 | 1: 2.07; 2: 2.06; 3: 2.20 | 2.07 | 1.06 | GPCel-Input | 528.3 ns | 1.57 KB | yes |  |
| E6 | evaluate | TelusCel | 3 | 1: 48.69; 2: 47.55; 3: 49.56 | 48.69 | 1.04 | GPCel-Input | 12.32 us | 18.30 KB | yes |  |
| E6 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 307.3 ns | 288 B | no |  |
| E6 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 255.0 ns | 288 B | no |  |
| E6 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 417.8 ns | 160 B | no |  |
| E6 | full cycle | Celly | 3 | 1: 4.12; 2: 4.05; 3: 4.09 | 4.09 | 1.02 | GPCel-Input | 15.28 us | 43.84 KB | yes |  |
| E6 | full cycle | Cel.NET | 3 | 1: 8.51; 2: 8.48; 3: 8.51 | 8.51 | 1.00 | GPCel-Input | 31.95 us | 52.04 KB | yes |  |
| E6 | full cycle | TelusCel | 3 | 1: 5.69; 2: 5.74; 3: 5.76 | 5.74 | 1.01 | GPCel-Input | 21.54 us | 32.27 KB | yes |  |
| E6 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.00 us | 9.49 KB | no |  |
| E6 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.76 us | 6.77 KB | no |  |
| E6 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.41 us | 6.90 KB | no |  |
| E7 | compile | Celly | 3 | 1: 4.54; 2: 4.51; 3: 4.54 | 4.54 | 1.01 | GPCel-Input | 15.37 us | 45.14 KB | yes |  |
| E7 | compile | DotnetCel | 3 | 1: 3.09; 2: 3.10; 3: 3.14 | 3.10 | 1.01 | GPCel-Input | 10.63 us | 25.66 KB | yes |  |
| E7 | compile | Cel.NET | 3 | 1: 8.83; 2: 8.72; 3: 8.80 | 8.80 | 1.01 | GPCel-Input | 29.90 us | 53.68 KB | yes |  |
| E7 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.71 us | 7.51 KB | no |  |
| E7 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.39 us | 7.08 KB | no |  |
| E7 | evaluate | Celly | 3 | 1: 3.45; 2: 3.44; 3: 3.47 | 3.45 | 1.01 | GPCel-Input | 885.9 ns | 1.17 KB | yes |  |
| E7 | evaluate | DotnetCel | 3 | 1: 1.68; 2: 1.67; 3: 1.68 | 1.68 | 1.01 | GPCel-Input | 428.5 ns | 592 B | yes |  |
| E7 | evaluate | Cel.NET | 3 | 1: 2.60; 2: 2.95; 3: 2.55 | 2.60 | 1.16 | GPCel-Input | 661.3 ns | 1.61 KB | yes |  |
| E7 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 317.3 ns | 288 B | no |  |
| E7 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 255.6 ns | 288 B | no |  |
| E7 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 377.6 ns | 160 B | no |  |
| E7 | full cycle | Celly | 3 | 1: 4.33; 2: 4.29; 3: 4.30 | 4.30 | 1.01 | GPCel-Input | 16.51 us | 46.31 KB | yes |  |
| E7 | full cycle | DotnetCel | 3 | 1: 3.59; 2: 3.60; 3: 3.64 | 3.60 | 1.01 | GPCel-Input | 13.82 us | 28.74 KB | yes |  |
| E7 | full cycle | Cel.NET | 3 | 1: 8.05; 2: 8.04; 3: 8.07 | 8.05 | 1.00 | GPCel-Input | 30.83 us | 55.30 KB | yes |  |
| E7 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.25 us | 7.82 KB | no |  |
| E7 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.84 us | 7.39 KB | no |  |
| E7 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 4.41 us | 7.36 KB | no |  |
| E8 | compile | Celly | 3 | 1: 3.68; 2: 3.66; 3: 3.67 | 3.67 | 1.01 | GPCel-Input | 19.01 us | 52.38 KB | yes |  |
| E8 | compile | Cel.NET | 3 | 1: 11.71; 2: 11.79; 3: 11.71 | 11.71 | 1.01 | GPCel-Input | 60.60 us | 99.23 KB | yes |  |
| E8 | compile | TelusCel | 3 | 1: 2.38; 2: 2.38; 3: 2.36 | 2.38 | 1.01 | GPCel-Input | 12.27 us | 19.39 KB | yes |  |
| E8 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.94 us | 12.51 KB | no |  |
| E8 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.17 us | 10.87 KB | no |  |
| E8 | evaluate | Celly | 3 | 1: 3.47; 2: 3.33; 3: 3.55 | 3.47 | 1.07 | GPCel-Input | 998.6 ns | 1.66 KB | yes |  |
| E8 | evaluate | Cel.NET | 3 | 1: 2.00; 2: 1.98; 3: 1.99 | 1.99 | 1.01 | GPCel-Input | 574.9 ns | 1.64 KB | yes |  |
| E8 | evaluate | TelusCel | 3 | 1: 44.14; 2: 37.56; 3: 37.61 | 37.61 | 1.18 | GPCel-Input | 11.08 us | 16.02 KB | yes |  |
| E8 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 371.7 ns | 336 B | no |  |
| E8 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 287.5 ns | 336 B | no |  |
| E8 | evaluate | GPCel-Simple | 3 | 1: 1.33; 2: 1.30; 3: 1.32 | 1.32 | 1.03 | Cel.NET | 758.4 ns | 480 B | yes |  |
| E8 | full cycle | Celly | 3 | 1: 3.59; 2: 3.56; 3: 3.51 | 3.56 | 1.02 | GPCel-Input | 20.28 us | 54.06 KB | yes |  |
| E8 | full cycle | Cel.NET | 3 | 1: 10.94; 2: 10.89; 3: 10.80 | 10.89 | 1.01 | GPCel-Input | 61.97 us | 100.88 KB | yes |  |
| E8 | full cycle | TelusCel | 3 | 1: 4.44; 2: 4.25; 3: 4.24 | 4.25 | 1.05 | GPCel-Input | 24.28 us | 35.41 KB | yes |  |
| E8 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.57 us | 12.87 KB | no |  |
| E8 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.69 us | 11.23 KB | no |  |
| E8 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 6.79 us | 11.61 KB | no |  |
| E9 | compile | Celly | 3 | 1: 4.67; 2: 4.58; 3: 4.68 | 4.67 | 1.02 | GPCel-Input | 18.04 us | 52.38 KB | yes |  |
| E9 | compile | DotnetCel | 3 | 1: 2.85; 2: 2.83; 3: 2.82 | 2.83 | 1.01 | GPCel-Input | 10.96 us | 26.69 KB | yes |  |
| E9 | compile | Cel.NET | 3 | 1: 10.81; 2: 10.77; 3: 10.99 | 10.81 | 1.02 | GPCel-Input | 41.81 us | 77.55 KB | yes |  |
| E9 | compile | TelusCel | 3 | 1: 1.58; 2: 1.57; 3: 1.58 | 1.58 | 1.00 | GPCel-Input | 6.10 us | 10.08 KB | yes |  |
| E9 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.66 us | 9.98 KB | no |  |
| E9 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.87 us | 8.42 KB | no |  |
| E9 | evaluate | Celly | 3 | 1: 1.62; 2: 1.51; 3: 1.47 | 1.51 | 1.10 | GPCel-Input | 475.3 ns | 1.11 KB | yes |  |
| E9 | evaluate | DotnetCel | 3 | 1: 1.74; 2: 1.62; 3: 1.62 | 1.62 | 1.08 | GPCel-Input | 513.1 ns | 808 B | yes |  |
| E9 | evaluate | Cel.NET | 3 | 1: 1.91; 2: 1.76; 3: 1.71 | 1.76 | 1.12 | GPCel-Input | 558.7 ns | 1.75 KB | yes |  |
| E9 | evaluate | TelusCel | 3 | 1: 24.48; 2: 22.41; 3: 25.09 | 24.48 | 1.12 | GPCel-Input | 7.18 us | 10.77 KB | yes |  |
| E9 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 390.0 ns | 472 B | no |  |
| E9 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 317.1 ns | 472 B | no |  |
| E9 | evaluate | GPCel-Simple | 3 | 1: 1.00; 2: 1.04; 3: 1.00 | 1.00 | 1.04 | GPCel-Simple, Celly | 468.5 ns | 344 B | no |  |
| E9 | full cycle | Celly | 3 | 1: 4.28; 2: 4.21; 3: 4.25 | 4.25 | 1.01 | GPCel-Input | 18.59 us | 53.50 KB | yes |  |
| E9 | full cycle | DotnetCel | 3 | 1: 3.25; 2: 3.15; 3: 3.27 | 3.25 | 1.04 | GPCel-Input | 14.13 us | 29.98 KB | yes |  |
| E9 | full cycle | Cel.NET | 3 | 1: 9.87; 2: 9.74; 3: 10.00 | 9.87 | 1.03 | GPCel-Input | 43.20 us | 79.30 KB | yes |  |
| E9 | full cycle | TelusCel | 3 | 1: 3.15; 2: 3.12; 3: 3.17 | 3.15 | 1.02 | GPCel-Input | 13.81 us | 20.85 KB | yes |  |
| E9 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.24 us | 10.47 KB | no |  |
| E9 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.35 us | 8.91 KB | no |  |
| E9 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 5.05 us | 9.02 KB | no |  |

R above 1.1 (all libraries): E1, compile, Celly: 4.56; E1, compile, DotnetCel: 3.43; E1, compile, Cel.NET: 8.94; E1, compile, TelusCel: 1.99; E1, evaluate, Celly: 1.88; E1, evaluate, DotnetCel: 1.76; E1, evaluate, Cel.NET: 2.17; E1, evaluate, TelusCel: 31.22; E1, full cycle, Celly: 4.24; E1, full cycle, DotnetCel: 3.96; E1, full cycle, Cel.NET: 8.06; E1, full cycle, TelusCel: 3.95; E2/10, compile, Celly: 3.20; E2/10, compile, DotnetCel: 2.48; E2/10, compile, Cel.NET: 6.32; E2/10, compile, TelusCel: 1.62; E2/10, evaluate, Celly: 2.48; E2/10, evaluate, DotnetCel: 3.63; E2/10, evaluate, Cel.NET: 3.03; E2/10, evaluate, TelusCel: 55.17; E2/10, full cycle, Celly: 2.97; E2/10, full cycle, DotnetCel: 3.05; E2/10, full cycle, Cel.NET: 5.48; E2/10, full cycle, TelusCel: 12.60; E2/100, compile, Celly: 3.18; E2/100, compile, DotnetCel: 2.47; E2/100, compile, Cel.NET: 6.33; E2/100, compile, TelusCel: 1.63; E2/100, evaluate, Celly: 2.55; E2/100, evaluate, DotnetCel: 3.72; E2/100, evaluate, Cel.NET: 3.06; E2/100, evaluate, TelusCel: 61.07; E2/100, full cycle, Celly: 2.75; E2/100, full cycle, DotnetCel: 3.38; E2/100, full cycle, Cel.NET: 4.09; E2/100, full cycle, TelusCel: 44.62; E3/10, compile, Celly: 3.26; E3/10, compile, DotnetCel: 2.78; E3/10, compile, Cel.NET: 5.88; E3/10, compile, TelusCel: 1.64; E3/10, evaluate, Celly: 2.83; E3/10, evaluate, DotnetCel: 3.56; E3/10, evaluate, Cel.NET: 2.92; E3/10, evaluate, TelusCel: 134.30; E3/10, full cycle, Celly: 3.13; E3/10, full cycle, DotnetCel: 3.44; E3/10, full cycle, Cel.NET: 5.55; E3/10, full cycle, TelusCel: 10.18; E3/100, compile, Celly: 3.31; E3/100, compile, DotnetCel: 2.82; E3/100, compile, Cel.NET: 5.89; E3/100, compile, TelusCel: 1.66; E3/100, evaluate, Celly: 13.12; E3/100, evaluate, DotnetCel: 12.39; E3/100, evaluate, Cel.NET: 7.26; E3/100, evaluate, TelusCel: 1174.05; E3/100, evaluate, GPCel-Simple: 1.36; E3/100, full cycle, Celly: 3.84; E3/100, full cycle, DotnetCel: 4.04; E3/100, full cycle, Cel.NET: 5.81; E3/100, full cycle, TelusCel: 78.15; E4/10, compile, Celly: 2.87; E4/10, compile, DotnetCel: 1.86; E4/10, compile, Cel.NET: 8.38; E4/10, compile, TelusCel: 1.31; E4/10, evaluate, Celly: 2.87; E4/10, evaluate, DotnetCel: 4.00; E4/10, evaluate, Cel.NET: 3.92; E4/10, evaluate, TelusCel: 56.58; E4/10, full cycle, Celly: 2.84; E4/10, full cycle, DotnetCel: 2.37; E4/10, full cycle, Cel.NET: 7.70; E4/10, full cycle, TelusCel: 7.21; E4/100, compile, Celly: 2.86; E4/100, compile, DotnetCel: 1.85; E4/100, compile, Cel.NET: 8.45; E4/100, compile, TelusCel: 1.32; E4/100, evaluate, Celly: 2.58; E4/100, evaluate, DotnetCel: 3.97; E4/100, evaluate, Cel.NET: 3.99; E4/100, evaluate, TelusCel: 57.49; E4/100, full cycle, Celly: 2.68; E4/100, full cycle, DotnetCel: 3.18; E4/100, full cycle, Cel.NET: 5.84; E4/100, full cycle, TelusCel: 33.99; E5, compile, Celly: 5.10; E5, compile, DotnetCel: 4.11; E5, compile, Cel.NET: 7.03; E5, compile, TelusCel: 2.11; E5, evaluate, Celly: 1.84; E5, evaluate, DotnetCel: 1.96; E5, evaluate, Cel.NET: 337.73; E5, evaluate, TelusCel: 35.93; E5, full cycle, Celly: 4.49; E5, full cycle, DotnetCel: 4.90; E5, full cycle, Cel.NET: 39.11; E5, full cycle, TelusCel: 5.66; E6, compile, Celly: 4.46; E6, compile, Cel.NET: 9.42; E6, compile, TelusCel: 2.58; E6, evaluate, Celly: 1.75; E6, evaluate, Cel.NET: 2.07; E6, evaluate, TelusCel: 48.69; E6, full cycle, Celly: 4.09; E6, full cycle, Cel.NET: 8.51; E6, full cycle, TelusCel: 5.74; E7, compile, Celly: 4.54; E7, compile, DotnetCel: 3.10; E7, compile, Cel.NET: 8.80; E7, evaluate, Celly: 3.45; E7, evaluate, DotnetCel: 1.68; E7, evaluate, Cel.NET: 2.60; E7, full cycle, Celly: 4.30; E7, full cycle, DotnetCel: 3.60; E7, full cycle, Cel.NET: 8.05; E8, compile, Celly: 3.67; E8, compile, Cel.NET: 11.71; E8, compile, TelusCel: 2.38; E8, evaluate, Celly: 3.47; E8, evaluate, Cel.NET: 1.99; E8, evaluate, TelusCel: 37.61; E8, evaluate, GPCel-Simple: 1.32; E8, full cycle, Celly: 3.56; E8, full cycle, Cel.NET: 10.89; E8, full cycle, TelusCel: 4.25; E9, compile, Celly: 4.67; E9, compile, DotnetCel: 2.83; E9, compile, Cel.NET: 10.81; E9, compile, TelusCel: 1.58; E9, evaluate, Celly: 1.51; E9, evaluate, DotnetCel: 1.62; E9, evaluate, Cel.NET: 1.76; E9, evaluate, TelusCel: 24.48; E9, full cycle, Celly: 4.25; E9, full cycle, DotnetCel: 3.25; E9, full cycle, Cel.NET: 9.87; E9, full cycle, TelusCel: 3.15.

**Rows of GreatPartsCel.**

- R above 1.1: E3/100, evaluate, GPCel-Simple: 1.36; E8, evaluate, GPCel-Simple: 1.32.
- Ratios on both sides of 1.1: none.

## Limits of the speed-ups

Job: Default; started 2026-11-25T19:04:52.550Z, finished 2026-11-25T19:39:19.713Z. Medians of BenchmarkDotNet on the code of this commit; the reports are in `results/thresholds/bdn/results`.

| Limit | Rule | Verdict |
|---|---|---|
| hybrid search of the literal prefix of matches, 64 code units | .NET at M = 64 is not slower than Knuth-Morris-Pratt; otherwise the limit is 32 | holds: 64 |
| build of the automaton of the pattern E5 | the median of the build is at most 2000 ns | holds: 1385 ns |
| index of the values of a list (distinct, sets): scan or buckets, the limit of the library is 32 values | a diagnostic without a verdict: the threshold by the rule is the largest N at which, and at every smaller N measured (8, 16, 24, 32, 48, 64), scan / buckets is at most 1.0 for distinct and for sets.contains; 8 when the scan is slower already at N = 8 | no verdict; threshold by the rule: 64 (the limit of the library: 32) |
| substring search: time per cost unit | a diagnostic without a verdict: a rise of the time per unit above 2 times from M = 64 to M = 1024 is the input of the decision on the divisor 64 | no verdict; time per unit from M = 64 to M = 1024: 0.05 times for a literal, 0.49 times for the data |
| simple entry: evaluate and data | a diagnostic without a verdict: the time of the call on a prepared anonymous object, and the time of building that object | no verdict; scalars: evaluate 363.7 ns, data 22.4 ns; lines: evaluate 2.06 us, data 22.5 ns |
| CelMap.Create of the row of an order | a diagnostic without a verdict: the time and the allocation of a map of three entries made with a collection expression | no verdict; 65.4 ns, 248 B |

### Search of the literal prefix of matches, the text (ab)^5000

M is the length of the pattern, the slowest case for the search of .NET. "Library" is the search as it runs: .NET up to the limit, Knuth-Morris-Pratt above. Net / KMP below 1 means .NET is faster.

| M | KMP | .NET | Library | Net / KMP |
|--:|--:|--:|--:|--:|
| 48 | 21.36 us | 12.42 us | 12.34 us | 0.58 |
| 64 | 18.74 us | 12.46 us | 12.47 us | 0.66 |
| 65 | 23.47 us | 566.1 ns | 21.75 us | 0.02 |
| 80 | 18.92 us | 15.73 us | 20.80 us | 0.83 |

### Search of a substring by the functions of strings: time per cost unit

contains on the text (ab)^5000 (10000 code units) and a pattern of M code units, the slowest case for the search of .NET. "Literal" is a pattern that is a literal of the expression (SearchValues<string>), "data" a pattern read from a variable (the ordinal search); the time is that of the whole evaluation. The cost is 1 + ceil((n + M) / 10) * q, q = max(1, ceil(M / 64)), without the reads of the variables. No verdict.

| M | Cost | Literal | Literal per unit | Data | Data per unit |
|--:|--:|--:|--:|--:|--:|
| 64 | 1008 | 727.0 ns | 0.7 ns | 12.55 us | 12.5 ns |
| 256 | 4105 | 704.5 ns | 0.2 ns | 30.54 us | 7.4 ns |
| 1024 | 17649 | 658.9 ns | 0.0 ns | 108.11 us | 6.1 ns |

### Build of the automaton

| Pattern | Parse and program | Automaton |
|---|--:|--:|
| E5 | 893.9 ns | 1.39 us |
| UUID | 1.81 us | 3.83 us |

### Index of the values: the lists of N integers

"Library" is the index as the library makes it (the scan up to 32 values, the buckets above); "scan" and "buckets" force that way at any N; "one hash" gives every value the same hash; "pairwise" are integers of 2^53 and more, compared with each other in order. Scan / buckets below 1 means the scan is faster. No verdict: the threshold by the rule is 64; the limit of the library is set by a decision on this table.

| Operation | N | Library | Scan | Buckets | One hash | Pairwise | Scan / buckets |
|---|--:|--:|--:|--:|--:|--:|--:|
| distinct | 8 | 285.2 ns | 280.6 ns | 396.6 ns | 479.5 ns | 393.4 ns | 0.71 |
| distinct | 16 | 466.5 ns | 509.2 ns | 571.1 ns | 1.22 us | 869.3 ns | 0.89 |
| distinct | 24 | 681.5 ns | 659.6 ns | 747.0 ns | 2.45 us | 1.54 us | 0.88 |
| distinct | 32 | 853.9 ns | 935.0 ns | 1.01 us | 4.46 us | 3.05 us | 0.92 |
| distinct | 48 | 1.31 us | 1.25 us | 1.29 us | 9.03 us | 6.50 us | 0.97 |
| distinct | 64 | 1.62 us | 1.68 us | 1.70 us | 13.06 us | 8.22 us | 0.99 |
| sets.contains | 8 | 461.2 ns | 493.5 ns | 538.8 ns | 619.6 ns | 537.0 ns | 0.92 |
| sets.contains | 16 | 820.6 ns | 788.4 ns | 903.9 ns | 1.53 us | 1.18 us | 0.87 |
| sets.contains | 24 | 1.17 us | 1.17 us | 1.27 us | 3.50 us | 1.99 us | 0.92 |
| sets.contains | 32 | 1.62 us | 1.56 us | 1.63 us | 6.19 us | 4.08 us | 0.96 |
| sets.contains | 48 | 2.33 us | 2.26 us | 2.36 us | 7.62 us | 7.88 us | 0.96 |
| sets.contains | 64 | 3.18 us | 3.02 us | 3.11 us | 12.49 us | 12.41 us | 0.97 |

### distinct and sets.contains through the public API

The evaluation of the program on a list of N integers; the time per element shows a jump at the limit, if there is one.

| N | distinct | per element | sets.contains | per element |
|--:|--:|--:|--:|--:|
| 16 | 681.0 ns | 42.6 ns | 1.17 us | 72.8 ns |
| 32 | 1.13 us | 35.3 ns | 2.13 us | 66.4 ns |
| 33 | 1.31 us | 39.8 ns | 2.22 us | 67.2 ns |
| 48 | 1.69 us | 35.3 ns | 3.07 us | 64.0 ns |
| 64 | 2.20 us | 34.4 ns | 3.98 us | 62.3 ns |

### Simple entry: evaluate and data

The call `CelExpression.Evaluate` on an anonymous object with the scalars of the order, the customer and the lines as properties, as the variant of the rig passes them. "Evaluate" is the call on an object that was built before the measurement, with the text of the variant with the input of the environment ("scalars" is the text of E1, "lines" the text of E2 over 10 lines); "data" is the building of that object for one call. No verdict.

| Text | Evaluate | Allocated | Data | Allocated |
|---|--:|--:|--:|--:|
| scalars | 363.7 ns | 48 B | 22.4 ns | 112 B |
| lines | 2.06 us | 688 B | 22.5 ns | 112 B |

### CelMap.Create of the row of an order

A map of three entries (`sku`, `qty`, `price`) made with a collection expression. No verdict.

| Time | Allocated |
|--:|--:|
| 65.4 ns | 248 B |
