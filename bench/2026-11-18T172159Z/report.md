# Benchmark rig

- Date and time of the report (UTC): 2026-11-18 17:21:59
- Commit of great-parts/great-parts-cel: [`515566235b023389b3437b4a2e6b7e56bd769406`](https://github.com/great-parts/great-parts-cel/commit/515566235b023389b3437b4a2e6b7e56bd769406)
- Runs: 3
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/44326172200 (GitHub keeps a run for 90 days; this report does not depend on it)

The summary of the rig follows. The full reports of BenchmarkDotNet are in the artifact `bench-results` of the run, for 90 days.

## Conditions

- Date of the summary: 2026-11-18T17:21:59.285Z
- Commit: 515566235b023389b3437b4a2e6b7e56bd769406
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/44326172200
- Runner: Linux X64, image ubuntu26 20261111.196.1
- Packages: BenchmarkDotNet 0.15.8; Celly 1.2.0; DotnetCel 1.0.7; Cel.NET 2.3.1; Cel 0.3.3; Great.Parts.Cel 1.0.1; Grpc.Tools 2.85.0

## Verification

Correct: all three variants gave the expected value: the original text (compile and evaluate), the text with a tail (compile and evaluate), the text with a tail (full cycle).

| Scenario | Expression | Lines | Expected | Celly | DotnetCel | Cel.NET | TelusCel | GreatPartsCel |
|---|---|--:|---|---|---|---|---|---|
| E1 | `order.total > 1000.0 && order.status == 'approved'` | 10 | bool:true | correct | correct | correct | correct | correct |
| E2/10 | `order.lines.all(l, l.qty > 0 && l.price >= 0.0)` | 10 | bool:true | correct | correct | correct | correct | correct |
| E2/100 | `order.lines.all(l, l.qty > 0 && l.price >= 0.0)` | 100 | bool:true | correct | correct | correct | correct | correct |
| E3/10 | `order.lines.exists(l, l.sku.startsWith('SKU-'))` | 10 | bool:true | correct | correct | correct | correct | correct |
| E3/100 | `order.lines.exists(l, l.sku.startsWith('SKU-'))` | 100 | bool:true | correct | correct | correct | correct | correct |
| E4/10 | `order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)` | 10 | [double:208,double:280,double:361] | correct | correct | correct | correct | correct |
| E4/100 | `order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)` | 100 | [double:208,double:280,double:361,double... | correct | correct | correct | correct | correct |
| E5 | `order.customer.email.matches('^[^@]+@[^@]+[.][a-z]+$')` | 10 | bool:true | correct | correct | correct | correct | correct |
| E6 | `has(order.discount) ? order.total - order.discount : order.total` | 10 | double:1400 | correct | wrong (3 of 3): ERROR(CelEvaluationException: runtime type mismatch in subtract_... | correct | correct | correct |
| E7 | `order.created + duration('72h') > now` | 10 | bool:true | correct | correct | correct | wrong (3 of 3): ERROR(CelNoSuchOverloadException: No overload exists to ADD type... | correct |
| E8 | `size(order.lines) <= 100 && order.currency in ['RUB', 'USD', 'EUR']` | 10 | bool:true | correct | wrong (3 of 3): ERROR(CelEvaluationException: runtime type mismatch in size_stri... | correct | correct | correct |
| E9 | `'Order ' + order.id + ': ' + order.status` | 10 | string:'Order ORD-0001: approved' | correct | correct | correct | correct | correct |

Excluded from the measurement (wrong result): DotnetCel - E6; TelusCel - E7; DotnetCel - E8.

## Measurement environment

- BenchmarkDotNet 0.15.8; OS: Linux Ubuntu 26.04.1 LTS (Resolute Raccoon); processor: AMD EPYC 7763
- Cores: 2 physical, 4 logical; architecture: X64
- Runtime: .NET 10.0.15 (10.0.15, 10.0.1526.53008); SDK: 11.0.100; configuration: RELEASE

## Runs

- Run 1: 2026-11-18T14:37:21.363Z - 2026-11-18T15:32:44.393Z; job: Default; cases: all; filter: *
- Run 2: 2026-11-18T15:32:44.395Z - 2026-11-18T16:27:19.725Z; job: Default; cases: all; filter: *
- Run 3: 2026-11-18T16:27:19.725Z - 2026-11-18T17:21:59.222Z; job: Default; cases: all; filter: *

The table of each run is `results/runs/<k>/summary.md`.

## Summary over the runs

The ratio of a row in a run is to the fastest library with a correct result in the same run. R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has 3 runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others.

| Scenario | Operation | Library | Runs | Ratios by run | R | Spread | Fastest by run | Median time over the runs | R > 1.1 | Both sides of 1.1 |
|---|---|---|--:|---|--:|--:|---|--:|---|---|
| E1 | compile | Celly | 3 | 1: 3.51; 2: 3.62; 3: 3.72 | 3.62 | 1.06 | GreatPartsCel | 15.57 us | yes |  |
| E1 | compile | DotnetCel | 3 | 1: 2.65; 2: 2.70; 3: 2.74 | 2.70 | 1.04 | GreatPartsCel | 11.65 us | yes |  |
| E1 | compile | Cel.NET | 3 | 1: 9.00; 2: 9.15; 3: 9.62 | 9.15 | 1.07 | GreatPartsCel | 39.62 us | yes |  |
| E1 | compile | TelusCel | 3 | 1: 1.84; 2: 1.91; 3: 1.87 | 1.87 | 1.04 | GreatPartsCel | 8.10 us | yes |  |
| E1 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.32 us | no |  |
| E1 | evaluate | Celly | 3 | 1: 1.24; 2: 1.32; 3: 1.37 | 1.32 | 1.11 | GreatPartsCel | 482.1 ns | yes |  |
| E1 | evaluate | DotnetCel | 3 | 1: 1.28; 2: 1.33; 3: 1.25 | 1.28 | 1.07 | GreatPartsCel | 473.4 ns | yes |  |
| E1 | evaluate | Cel.NET | 3 | 1: 1.50; 2: 1.55; 3: 1.58 | 1.55 | 1.06 | GreatPartsCel | 563.8 ns | yes |  |
| E1 | evaluate | TelusCel | 3 | 1: 22.36; 2: 21.87; 3: 24.35 | 22.36 | 1.11 | GreatPartsCel | 8.27 us | yes |  |
| E1 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 364.5 ns | no |  |
| E1 | full cycle | Celly | 3 | 1: 3.25; 2: 3.41; 3: 3.47 | 3.41 | 1.07 | GreatPartsCel | 16.45 us | yes |  |
| E1 | full cycle | DotnetCel | 3 | 1: 3.14; 2: 3.15; 3: 3.21 | 3.15 | 1.02 | GreatPartsCel | 15.41 us | yes |  |
| E1 | full cycle | Cel.NET | 3 | 1: 8.58; 2: 8.79; 3: 8.86 | 8.79 | 1.03 | GreatPartsCel | 42.46 us | yes |  |
| E1 | full cycle | TelusCel | 3 | 1: 3.58; 2: 3.62; 3: 3.81 | 3.62 | 1.06 | GreatPartsCel | 17.72 us | yes |  |
| E1 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.83 us | no |  |
| E2/10 | compile | Celly | 3 | 1: 3.02; 2: 3.13; 3: 3.21 | 3.13 | 1.06 | GreatPartsCel | 20.76 us | yes |  |
| E2/10 | compile | DotnetCel | 3 | 1: 2.33; 2: 2.34; 3: 2.45 | 2.34 | 1.05 | GreatPartsCel | 15.69 us | yes |  |
| E2/10 | compile | Cel.NET | 3 | 1: 7.89; 2: 7.96; 3: 7.89 | 7.89 | 1.01 | GreatPartsCel | 52.81 us | yes |  |
| E2/10 | compile | TelusCel | 3 | 1: 1.92; 2: 1.88; 3: 1.89 | 1.89 | 1.03 | GreatPartsCel | 12.47 us | yes |  |
| E2/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.63 us | no |  |
| E2/10 | evaluate | Celly | 3 | 1: 1.86; 2: 2.07; 3: 1.85 | 1.86 | 1.12 | GreatPartsCel | 3.87 us | yes |  |
| E2/10 | evaluate | DotnetCel | 3 | 1: 3.13; 2: 3.13; 3: 2.92 | 3.13 | 1.07 | GreatPartsCel | 6.51 us | yes |  |
| E2/10 | evaluate | Cel.NET | 3 | 1: 2.33; 2: 2.29; 3: 2.29 | 2.29 | 1.02 | GreatPartsCel | 4.83 us | yes |  |
| E2/10 | evaluate | TelusCel | 3 | 1: 44.96; 2: 42.88; 3: 44.37 | 44.37 | 1.05 | GreatPartsCel | 92.16 us | yes |  |
| E2/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.08 us | no |  |
| E2/10 | full cycle | Celly | 3 | 1: 2.82; 2: 2.95; 3: 2.99 | 2.95 | 1.06 | GreatPartsCel | 27.10 us | yes |  |
| E2/10 | full cycle | DotnetCel | 3 | 1: 2.92; 2: 3.02; 3: 3.03 | 3.02 | 1.04 | GreatPartsCel | 27.48 us | yes |  |
| E2/10 | full cycle | Cel.NET | 3 | 1: 6.86; 2: 6.79; 3: 6.79 | 6.79 | 1.01 | GreatPartsCel | 62.81 us | yes |  |
| E2/10 | full cycle | TelusCel | 3 | 1: 12.02; 2: 11.95; 3: 12.11 | 12.02 | 1.01 | GreatPartsCel | 110.48 us | yes |  |
| E2/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 9.24 us | no |  |
| E2/100 | compile | Celly | 3 | 1: 3.03; 2: 3.22; 3: 3.24 | 3.22 | 1.07 | GreatPartsCel | 21.02 us | yes |  |
| E2/100 | compile | DotnetCel | 3 | 1: 2.33; 2: 2.38; 3: 2.37 | 2.37 | 1.02 | GreatPartsCel | 15.73 us | yes |  |
| E2/100 | compile | Cel.NET | 3 | 1: 7.75; 2: 8.02; 3: 8.21 | 8.02 | 1.06 | GreatPartsCel | 53.30 us | yes |  |
| E2/100 | compile | TelusCel | 3 | 1: 1.80; 2: 1.85; 3: 1.93 | 1.85 | 1.07 | GreatPartsCel | 12.38 us | yes |  |
| E2/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.61 us | no |  |
| E2/100 | evaluate | Celly | 3 | 1: 1.49; 2: 1.57; 3: 1.90 | 1.57 | 1.28 | GreatPartsCel | 34.11 us | yes |  |
| E2/100 | evaluate | DotnetCel | 3 | 1: 2.71; 2: 2.63; 3: 3.17 | 2.71 | 1.20 | GreatPartsCel | 57.19 us | yes |  |
| E2/100 | evaluate | Cel.NET | 3 | 1: 2.13; 2: 1.96; 3: 2.52 | 2.13 | 1.28 | GreatPartsCel | 44.99 us | yes |  |
| E2/100 | evaluate | TelusCel | 3 | 1: 42.85; 2: 40.02; 3: 49.76 | 42.85 | 1.24 | GreatPartsCel | 892.69 us | yes |  |
| E2/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 21.10 us | no |  |
| E2/100 | full cycle | Celly | 3 | 1: 2.39; 2: 2.27; 3: 2.39 | 2.39 | 1.06 | GreatPartsCel | 59.64 us | yes |  |
| E2/100 | full cycle | DotnetCel | 3 | 1: 3.23; 2: 3.33; 3: 3.23 | 3.23 | 1.03 | GreatPartsCel | 81.57 us | yes |  |
| E2/100 | full cycle | Cel.NET | 3 | 1: 4.12; 2: 4.09; 3: 4.14 | 4.12 | 1.01 | GreatPartsCel | 103.16 us | yes |  |
| E2/100 | full cycle | TelusCel | 3 | 1: 35.90; 2: 35.73; 3: 36.77 | 35.90 | 1.03 | GreatPartsCel | 907.37 us | yes |  |
| E2/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 25.14 us | no |  |
| E3/10 | compile | Celly | 3 | 1: 3.18; 2: 3.38; 3: 3.42 | 3.38 | 1.08 | GreatPartsCel | 17.93 us | yes |  |
| E3/10 | compile | DotnetCel | 3 | 1: 2.53; 2: 2.71; 3: 2.84 | 2.71 | 1.13 | GreatPartsCel | 14.35 us | yes |  |
| E3/10 | compile | Cel.NET | 3 | 1: 7.06; 2: 7.71; 3: 7.74 | 7.71 | 1.10 | GreatPartsCel | 40.51 us | yes |  |
| E3/10 | compile | TelusCel | 3 | 1: 1.78; 2: 1.91; 3: 2.06 | 1.91 | 1.16 | GreatPartsCel | 10.12 us | yes |  |
| E3/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.29 us | no |  |
| E3/10 | evaluate | Celly | 3 | 1: 1.77; 2: 1.94; 3: 1.88 | 1.88 | 1.10 | GreatPartsCel | 1.01 us | yes |  |
| E3/10 | evaluate | DotnetCel | 3 | 1: 2.30; 2: 2.69; 3: 2.51 | 2.51 | 1.17 | GreatPartsCel | 1.34 us | yes |  |
| E3/10 | evaluate | Cel.NET | 3 | 1: 1.94; 2: 2.18; 3: 2.03 | 2.03 | 1.12 | GreatPartsCel | 1.10 us | yes |  |
| E3/10 | evaluate | TelusCel | 3 | 1: 92.57; 2: 100.96; 3: 102.08 | 100.96 | 1.10 | GreatPartsCel | 53.13 us | yes |  |
| E3/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 532.6 ns | no |  |
| E3/10 | full cycle | Celly | 3 | 1: 3.09; 2: 3.23; 3: 3.27 | 3.23 | 1.06 | GreatPartsCel | 19.81 us | yes |  |
| E3/10 | full cycle | DotnetCel | 3 | 1: 3.28; 2: 3.44; 3: 3.38 | 3.38 | 1.05 | GreatPartsCel | 20.52 us | yes |  |
| E3/10 | full cycle | Cel.NET | 3 | 1: 7.29; 2: 7.23; 3: 7.30 | 7.29 | 1.01 | GreatPartsCel | 44.38 us | yes |  |
| E3/10 | full cycle | TelusCel | 3 | 1: 10.72; 2: 10.80; 3: 10.85 | 10.80 | 1.01 | GreatPartsCel | 66.22 us | yes |  |
| E3/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.13 us | no |  |
| E3/100 | compile | Celly | 3 | 1: 3.24; 2: 3.31; 3: 3.38 | 3.31 | 1.04 | GreatPartsCel | 17.82 us | yes |  |
| E3/100 | compile | DotnetCel | 3 | 1: 2.58; 2: 2.69; 3: 2.73 | 2.69 | 1.06 | GreatPartsCel | 14.40 us | yes |  |
| E3/100 | compile | Cel.NET | 3 | 1: 7.45; 2: 7.43; 3: 7.55 | 7.45 | 1.02 | GreatPartsCel | 40.03 us | yes |  |
| E3/100 | compile | TelusCel | 3 | 1: 1.82; 2: 1.85; 3: 1.91 | 1.85 | 1.05 | GreatPartsCel | 10.05 us | yes |  |
| E3/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.39 us | no |  |
| E3/100 | evaluate | Celly | 3 | 1: 8.75; 2: 8.75; 3: 8.52 | 8.75 | 1.03 | GreatPartsCel | 4.57 us | yes |  |
| E3/100 | evaluate | DotnetCel | 3 | 1: 8.02; 2: 8.90; 3: 8.49 | 8.49 | 1.11 | GreatPartsCel | 4.43 us | yes |  |
| E3/100 | evaluate | Cel.NET | 3 | 1: 4.91; 2: 4.90; 3: 4.80 | 4.90 | 1.02 | GreatPartsCel | 2.56 us | yes |  |
| E3/100 | evaluate | TelusCel | 3 | 1: 944.24; 2: 1002.34; 3: 933.98 | 944.24 | 1.07 | GreatPartsCel | 496.73 us | yes |  |
| E3/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 522.9 ns | no |  |
| E3/100 | full cycle | Celly | 3 | 1: 3.63; 2: 3.80; 3: 3.75 | 3.75 | 1.05 | GreatPartsCel | 23.26 us | yes |  |
| E3/100 | full cycle | DotnetCel | 3 | 1: 3.77; 2: 3.98; 3: 3.97 | 3.97 | 1.05 | GreatPartsCel | 24.38 us | yes |  |
| E3/100 | full cycle | Cel.NET | 3 | 1: 7.52; 2: 7.46; 3: 7.54 | 7.52 | 1.01 | GreatPartsCel | 46.32 us | yes |  |
| E3/100 | full cycle | TelusCel | 3 | 1: 83.04; 2: 82.44; 3: 82.76 | 82.76 | 1.01 | GreatPartsCel | 511.45 us | yes |  |
| E3/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.16 us | no |  |
| E4/10 | compile | Celly | 3 | 1: 2.96; 2: 3.05; 3: 3.03 | 3.03 | 1.03 | GreatPartsCel | 34.46 us | yes |  |
| E4/10 | compile | DotnetCel | 3 | 1: 1.82; 2: 1.89; 3: 1.81 | 1.82 | 1.04 | GreatPartsCel | 21.21 us | yes |  |
| E4/10 | compile | Cel.NET | 3 | 1: 10.41; 2: 10.35; 3: 10.18 | 10.35 | 1.02 | GreatPartsCel | 118.94 us | yes |  |
| E4/10 | compile | TelusCel | 3 | 1: 1.46; 2: 1.52; 3: 1.46 | 1.46 | 1.05 | GreatPartsCel | 17.01 us | yes |  |
| E4/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 11.65 us | no |  |
| E4/10 | evaluate | Celly | 3 | 1: 1.94; 2: 2.05; 3: 2.03 | 2.03 | 1.06 | GreatPartsCel | 4.04 us | yes |  |
| E4/10 | evaluate | DotnetCel | 3 | 1: 2.86; 2: 3.21; 3: 2.98 | 2.98 | 1.12 | GreatPartsCel | 6.04 us | yes |  |
| E4/10 | evaluate | Cel.NET | 3 | 1: 2.52; 2: 2.89; 3: 2.67 | 2.67 | 1.15 | GreatPartsCel | 5.32 us | yes |  |
| E4/10 | evaluate | TelusCel | 3 | 1: 38.50; 2: 43.51; 3: 43.12 | 43.12 | 1.13 | GreatPartsCel | 82.46 us | yes |  |
| E4/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.99 us | no |  |
| E4/10 | full cycle | Celly | 3 | 1: 2.94; 2: 2.91; 3: 2.76 | 2.91 | 1.06 | GreatPartsCel | 41.21 us | yes |  |
| E4/10 | full cycle | DotnetCel | 3 | 1: 2.32; 2: 2.32; 3: 2.31 | 2.32 | 1.01 | GreatPartsCel | 33.15 us | yes |  |
| E4/10 | full cycle | Cel.NET | 3 | 1: 9.08; 2: 9.05; 3: 8.85 | 9.05 | 1.03 | GreatPartsCel | 129.30 us | yes |  |
| E4/10 | full cycle | TelusCel | 3 | 1: 7.22; 2: 7.25; 3: 7.04 | 7.22 | 1.03 | GreatPartsCel | 102.80 us | yes |  |
| E4/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 14.28 us | no |  |
| E4/100 | compile | Celly | 3 | 1: 3.01; 2: 2.95; 3: 2.96 | 2.96 | 1.02 | GreatPartsCel | 34.89 us | yes |  |
| E4/100 | compile | DotnetCel | 3 | 1: 1.86; 2: 1.88; 3: 1.77 | 1.86 | 1.06 | GreatPartsCel | 21.44 us | yes |  |
| E4/100 | compile | Cel.NET | 3 | 1: 10.32; 2: 10.42; 3: 9.96 | 10.32 | 1.05 | GreatPartsCel | 119.14 us | yes |  |
| E4/100 | compile | TelusCel | 3 | 1: 1.42; 2: 1.53; 3: 1.45 | 1.45 | 1.08 | GreatPartsCel | 17.05 us | yes |  |
| E4/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 11.68 us | no |  |
| E4/100 | evaluate | Celly | 3 | 1: 2.11; 2: 1.76; 3: 1.99 | 1.99 | 1.20 | GreatPartsCel | 38.93 us | yes |  |
| E4/100 | evaluate | DotnetCel | 3 | 1: 3.58; 2: 3.39; 3: 3.42 | 3.42 | 1.06 | GreatPartsCel | 68.99 us | yes |  |
| E4/100 | evaluate | Cel.NET | 3 | 1: 3.19; 2: 3.09; 3: 3.19 | 3.19 | 1.03 | GreatPartsCel | 62.37 us | yes |  |
| E4/100 | evaluate | TelusCel | 3 | 1: 49.57; 2: 47.60; 3: 52.59 | 49.57 | 1.10 | GreatPartsCel | 992.39 us | yes |  |
| E4/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 19.52 us | no |  |
| E4/100 | full cycle | Celly | 3 | 1: 2.33; 2: 2.67; 3: 2.26 | 2.33 | 1.18 | GreatPartsCel | 80.42 us | yes |  |
| E4/100 | full cycle | DotnetCel | 3 | 1: 2.83; 2: 3.21; 3: 2.94 | 2.94 | 1.13 | GreatPartsCel | 97.71 us | yes |  |
| E4/100 | full cycle | Cel.NET | 3 | 1: 5.50; 2: 6.21; 3: 5.59 | 5.59 | 1.13 | GreatPartsCel | 189.70 us | yes |  |
| E4/100 | full cycle | TelusCel | 3 | 1: 29.44; 2: 33.50; 3: 29.08 | 29.44 | 1.15 | GreatPartsCel | 1.01 ms | yes |  |
| E4/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 34.41 us | no |  |
| E5 | compile | Celly | 3 | 1: 4.49; 2: 4.62; 3: 4.26 | 4.49 | 1.08 | GreatPartsCel | 12.67 us | yes |  |
| E5 | compile | DotnetCel | 3 | 1: 3.69; 2: 3.62; 3: 3.40 | 3.62 | 1.08 | GreatPartsCel | 9.95 us | yes |  |
| E5 | compile | Cel.NET | 3 | 1: 8.19; 2: 7.88; 3: 7.89 | 7.89 | 1.04 | GreatPartsCel | 22.57 us | yes |  |
| E5 | compile | TelusCel | 3 | 1: 2.20; 2: 2.38; 3: 2.13 | 2.20 | 1.12 | GreatPartsCel | 6.20 us | yes |  |
| E5 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 2.82 us | no |  |
| E5 | evaluate | Celly | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | Celly | 538.8 ns | no |  |
| E5 | evaluate | DotnetCel | 3 | 1: 1.05; 2: 1.03; 3: 1.12 | 1.05 | 1.09 | Celly | 595.7 ns | no | yes |
| E5 | evaluate | Cel.NET | 3 | 1: 149.20; 2: 134.07; 3: 149.45 | 149.20 | 1.11 | Celly | 80.39 us | yes |  |
| E5 | evaluate | TelusCel | 3 | 1: 19.02; 2: 17.90; 3: 19.33 | 19.02 | 1.08 | Celly | 10.28 us | yes |  |
| E5 | evaluate | GreatPartsCel | 3 | 1: 1.99; 2: 1.86; 3: 2.02 | 1.99 | 1.09 | Celly | 1.08 us | yes |  |
| E5 | full cycle | Celly | 3 | 1: 3.26; 2: 3.32; 3: 3.10 | 3.26 | 1.07 | GreatPartsCel | 13.52 us | yes |  |
| E5 | full cycle | DotnetCel | 3 | 1: 3.56; 2: 3.54; 3: 3.32 | 3.54 | 1.07 | GreatPartsCel | 14.42 us | yes |  |
| E5 | full cycle | Cel.NET | 3 | 1: 32.45; 2: 32.23; 3: 31.52 | 32.23 | 1.03 | GreatPartsCel | 134.08 us | yes |  |
| E5 | full cycle | TelusCel | 3 | 1: 4.40; 2: 4.52; 3: 4.27 | 4.40 | 1.06 | GreatPartsCel | 18.31 us | yes |  |
| E5 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.16 us | no |  |
| E6 | compile | Celly | 3 | 1: 3.56; 2: 3.72; 3: 3.34 | 3.56 | 1.11 | GreatPartsCel | 16.80 us | yes |  |
| E6 | compile | Cel.NET | 3 | 1: 9.09; 2: 9.55; 3: 8.57 | 9.09 | 1.11 | GreatPartsCel | 42.86 us | yes |  |
| E6 | compile | TelusCel | 3 | 1: 2.22; 2: 2.51; 3: 2.15 | 2.22 | 1.17 | GreatPartsCel | 10.52 us | yes |  |
| E6 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.72 us | no |  |
| E6 | evaluate | Celly | 3 | 1: 1.75; 2: 1.62; 3: 1.51 | 1.62 | 1.16 | GreatPartsCel | 564.2 ns | yes |  |
| E6 | evaluate | Cel.NET | 3 | 1: 1.60; 2: 1.66; 3: 1.57 | 1.60 | 1.05 | GreatPartsCel | 550.9 ns | yes |  |
| E6 | evaluate | TelusCel | 3 | 1: 46.57; 2: 42.49; 3: 40.71 | 42.49 | 1.14 | GreatPartsCel | 14.83 us | yes |  |
| E6 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 345.3 ns | no |  |
| E6 | full cycle | Celly | 3 | 1: 3.46; 2: 3.54; 3: 3.31 | 3.46 | 1.07 | GreatPartsCel | 18.06 us | yes |  |
| E6 | full cycle | Cel.NET | 3 | 1: 8.69; 2: 8.62; 3: 8.54 | 8.62 | 1.02 | GreatPartsCel | 44.70 us | yes |  |
| E6 | full cycle | TelusCel | 3 | 1: 5.23; 2: 5.43; 3: 4.96 | 5.23 | 1.10 | GreatPartsCel | 27.34 us | yes |  |
| E6 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.22 us | no |  |
| E7 | compile | Celly | 3 | 1: 4.08; 2: 4.11; 3: 3.86 | 4.08 | 1.07 | GreatPartsCel | 16.73 us | yes |  |
| E7 | compile | DotnetCel | 3 | 1: 2.70; 2: 2.78; 3: 2.61 | 2.70 | 1.07 | GreatPartsCel | 11.24 us | yes |  |
| E7 | compile | Cel.NET | 3 | 1: 10.30; 2: 10.13; 3: 9.92 | 10.13 | 1.04 | GreatPartsCel | 41.19 us | yes |  |
| E7 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.15 us | no |  |
| E7 | evaluate | Celly | 3 | 1: 1.94; 2: 2.16; 3: 1.98 | 1.98 | 1.11 | GreatPartsCel | 739.0 ns | yes |  |
| E7 | evaluate | DotnetCel | 3 | 1: 1.18; 2: 1.27; 3: 1.22 | 1.22 | 1.08 | GreatPartsCel | 444.4 ns | yes |  |
| E7 | evaluate | Cel.NET | 3 | 1: 2.01; 2: 2.05; 3: 1.99 | 2.01 | 1.03 | GreatPartsCel | 716.7 ns | yes |  |
| E7 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 359.6 ns | no |  |
| E7 | full cycle | Celly | 3 | 1: 3.89; 2: 3.90; 3: 3.96 | 3.90 | 1.02 | GreatPartsCel | 17.99 us | yes |  |
| E7 | full cycle | DotnetCel | 3 | 1: 3.09; 2: 3.15; 3: 3.19 | 3.15 | 1.04 | GreatPartsCel | 14.52 us | yes |  |
| E7 | full cycle | Cel.NET | 3 | 1: 9.37; 2: 9.71; 3: 9.87 | 9.71 | 1.05 | GreatPartsCel | 44.88 us | yes |  |
| E7 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.60 us | no |  |
| E8 | compile | Celly | 3 | 1: 3.53; 2: 3.34; 3: 3.21 | 3.34 | 1.10 | GreatPartsCel | 21.72 us | yes |  |
| E8 | compile | Cel.NET | 3 | 1: 13.23; 2: 13.57; 3: 13.44 | 13.44 | 1.03 | GreatPartsCel | 86.71 us | yes |  |
| E8 | compile | TelusCel | 3 | 1: 2.30; 2: 2.35; 3: 2.34 | 2.34 | 1.02 | GreatPartsCel | 15.09 us | yes |  |
| E8 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.51 us | no |  |
| E8 | evaluate | Celly | 3 | 1: 1.69; 2: 1.83; 3: 1.70 | 1.70 | 1.09 | GreatPartsCel | 1.09 us | yes |  |
| E8 | evaluate | Cel.NET | 3 | 1: 1.04; 2: 1.09; 3: 1.11 | 1.09 | 1.06 | GreatPartsCel | 650.1 ns | no | yes |
| E8 | evaluate | TelusCel | 3 | 1: 18.58; 2: 21.06; 3: 20.83 | 20.83 | 1.13 | GreatPartsCel | 12.21 us | yes |  |
| E8 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 595.4 ns | no |  |
| E8 | full cycle | Celly | 3 | 1: 3.23; 2: 3.29; 3: 3.15 | 3.23 | 1.05 | GreatPartsCel | 24.33 us | yes |  |
| E8 | full cycle | Cel.NET | 3 | 1: 11.56; 2: 12.08; 3: 12.28 | 12.08 | 1.06 | GreatPartsCel | 89.41 us | yes |  |
| E8 | full cycle | TelusCel | 3 | 1: 4.05; 2: 4.15; 3: 4.01 | 4.05 | 1.04 | GreatPartsCel | 30.72 us | yes |  |
| E8 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 7.40 us | no |  |
| E9 | compile | Celly | 3 | 1: 3.74; 2: 4.00; 3: 3.85 | 3.85 | 1.07 | GreatPartsCel | 18.48 us | yes |  |
| E9 | compile | DotnetCel | 3 | 1: 2.28; 2: 2.50; 3: 2.41 | 2.41 | 1.10 | GreatPartsCel | 11.53 us | yes |  |
| E9 | compile | Cel.NET | 3 | 1: 11.37; 2: 11.85; 3: 11.52 | 11.52 | 1.04 | GreatPartsCel | 54.74 us | yes |  |
| E9 | compile | TelusCel | 3 | 1: 1.48; 2: 1.73; 3: 1.63 | 1.63 | 1.17 | GreatPartsCel | 7.69 us | yes |  |
| E9 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.73 us | no |  |
| E9 | evaluate | Celly | 3 | 1: 1.21; 2: 1.20; 3: 1.26 | 1.21 | 1.05 | GreatPartsCel | 499.6 ns | yes |  |
| E9 | evaluate | DotnetCel | 3 | 1: 1.46; 2: 1.35; 3: 1.30 | 1.35 | 1.12 | GreatPartsCel | 566.1 ns | yes |  |
| E9 | evaluate | Cel.NET | 3 | 1: 1.45; 2: 1.57; 3: 1.29 | 1.45 | 1.22 | GreatPartsCel | 598.3 ns | yes |  |
| E9 | evaluate | TelusCel | 3 | 1: 18.80; 2: 20.20; 3: 17.66 | 18.80 | 1.14 | GreatPartsCel | 7.73 us | yes |  |
| E9 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 411.3 ns | no |  |
| E9 | full cycle | Celly | 3 | 1: 3.60; 2: 3.66; 3: 3.46 | 3.60 | 1.06 | GreatPartsCel | 19.59 us | yes |  |
| E9 | full cycle | DotnetCel | 3 | 1: 2.79; 2: 2.83; 3: 2.72 | 2.79 | 1.04 | GreatPartsCel | 15.19 us | yes |  |
| E9 | full cycle | Cel.NET | 3 | 1: 10.78; 2: 10.67; 3: 10.83 | 10.78 | 1.02 | GreatPartsCel | 58.40 us | yes |  |
| E9 | full cycle | TelusCel | 3 | 1: 3.05; 2: 3.26; 3: 3.16 | 3.16 | 1.07 | GreatPartsCel | 17.01 us | yes |  |
| E9 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.39 us | no |  |

R above 1.1 (all libraries): E1, compile, Celly: 3.62; E1, compile, DotnetCel: 2.70; E1, compile, Cel.NET: 9.15; E1, compile, TelusCel: 1.87; E1, evaluate, Celly: 1.32; E1, evaluate, DotnetCel: 1.28; E1, evaluate, Cel.NET: 1.55; E1, evaluate, TelusCel: 22.36; E1, full cycle, Celly: 3.41; E1, full cycle, DotnetCel: 3.15; E1, full cycle, Cel.NET: 8.79; E1, full cycle, TelusCel: 3.62; E2/10, compile, Celly: 3.13; E2/10, compile, DotnetCel: 2.34; E2/10, compile, Cel.NET: 7.89; E2/10, compile, TelusCel: 1.89; E2/10, evaluate, Celly: 1.86; E2/10, evaluate, DotnetCel: 3.13; E2/10, evaluate, Cel.NET: 2.29; E2/10, evaluate, TelusCel: 44.37; E2/10, full cycle, Celly: 2.95; E2/10, full cycle, DotnetCel: 3.02; E2/10, full cycle, Cel.NET: 6.79; E2/10, full cycle, TelusCel: 12.02; E2/100, compile, Celly: 3.22; E2/100, compile, DotnetCel: 2.37; E2/100, compile, Cel.NET: 8.02; E2/100, compile, TelusCel: 1.85; E2/100, evaluate, Celly: 1.57; E2/100, evaluate, DotnetCel: 2.71; E2/100, evaluate, Cel.NET: 2.13; E2/100, evaluate, TelusCel: 42.85; E2/100, full cycle, Celly: 2.39; E2/100, full cycle, DotnetCel: 3.23; E2/100, full cycle, Cel.NET: 4.12; E2/100, full cycle, TelusCel: 35.90; E3/10, compile, Celly: 3.38; E3/10, compile, DotnetCel: 2.71; E3/10, compile, Cel.NET: 7.71; E3/10, compile, TelusCel: 1.91; E3/10, evaluate, Celly: 1.88; E3/10, evaluate, DotnetCel: 2.51; E3/10, evaluate, Cel.NET: 2.03; E3/10, evaluate, TelusCel: 100.96; E3/10, full cycle, Celly: 3.23; E3/10, full cycle, DotnetCel: 3.38; E3/10, full cycle, Cel.NET: 7.29; E3/10, full cycle, TelusCel: 10.80; E3/100, compile, Celly: 3.31; E3/100, compile, DotnetCel: 2.69; E3/100, compile, Cel.NET: 7.45; E3/100, compile, TelusCel: 1.85; E3/100, evaluate, Celly: 8.75; E3/100, evaluate, DotnetCel: 8.49; E3/100, evaluate, Cel.NET: 4.90; E3/100, evaluate, TelusCel: 944.24; E3/100, full cycle, Celly: 3.75; E3/100, full cycle, DotnetCel: 3.97; E3/100, full cycle, Cel.NET: 7.52; E3/100, full cycle, TelusCel: 82.76; E4/10, compile, Celly: 3.03; E4/10, compile, DotnetCel: 1.82; E4/10, compile, Cel.NET: 10.35; E4/10, compile, TelusCel: 1.46; E4/10, evaluate, Celly: 2.03; E4/10, evaluate, DotnetCel: 2.98; E4/10, evaluate, Cel.NET: 2.67; E4/10, evaluate, TelusCel: 43.12; E4/10, full cycle, Celly: 2.91; E4/10, full cycle, DotnetCel: 2.32; E4/10, full cycle, Cel.NET: 9.05; E4/10, full cycle, TelusCel: 7.22; E4/100, compile, Celly: 2.96; E4/100, compile, DotnetCel: 1.86; E4/100, compile, Cel.NET: 10.32; E4/100, compile, TelusCel: 1.45; E4/100, evaluate, Celly: 1.99; E4/100, evaluate, DotnetCel: 3.42; E4/100, evaluate, Cel.NET: 3.19; E4/100, evaluate, TelusCel: 49.57; E4/100, full cycle, Celly: 2.33; E4/100, full cycle, DotnetCel: 2.94; E4/100, full cycle, Cel.NET: 5.59; E4/100, full cycle, TelusCel: 29.44; E5, compile, Celly: 4.49; E5, compile, DotnetCel: 3.62; E5, compile, Cel.NET: 7.89; E5, compile, TelusCel: 2.20; E5, evaluate, Cel.NET: 149.20; E5, evaluate, TelusCel: 19.02; E5, evaluate, GreatPartsCel: 1.99; E5, full cycle, Celly: 3.26; E5, full cycle, DotnetCel: 3.54; E5, full cycle, Cel.NET: 32.23; E5, full cycle, TelusCel: 4.40; E6, compile, Celly: 3.56; E6, compile, Cel.NET: 9.09; E6, compile, TelusCel: 2.22; E6, evaluate, Celly: 1.62; E6, evaluate, Cel.NET: 1.60; E6, evaluate, TelusCel: 42.49; E6, full cycle, Celly: 3.46; E6, full cycle, Cel.NET: 8.62; E6, full cycle, TelusCel: 5.23; E7, compile, Celly: 4.08; E7, compile, DotnetCel: 2.70; E7, compile, Cel.NET: 10.13; E7, evaluate, Celly: 1.98; E7, evaluate, DotnetCel: 1.22; E7, evaluate, Cel.NET: 2.01; E7, full cycle, Celly: 3.90; E7, full cycle, DotnetCel: 3.15; E7, full cycle, Cel.NET: 9.71; E8, compile, Celly: 3.34; E8, compile, Cel.NET: 13.44; E8, compile, TelusCel: 2.34; E8, evaluate, Celly: 1.70; E8, evaluate, TelusCel: 20.83; E8, full cycle, Celly: 3.23; E8, full cycle, Cel.NET: 12.08; E8, full cycle, TelusCel: 4.05; E9, compile, Celly: 3.85; E9, compile, DotnetCel: 2.41; E9, compile, Cel.NET: 11.52; E9, compile, TelusCel: 1.63; E9, evaluate, Celly: 1.21; E9, evaluate, DotnetCel: 1.35; E9, evaluate, Cel.NET: 1.45; E9, evaluate, TelusCel: 18.80; E9, full cycle, Celly: 3.60; E9, full cycle, DotnetCel: 2.79; E9, full cycle, Cel.NET: 10.78; E9, full cycle, TelusCel: 3.16.

**Rows of GreatPartsCel.**

- R above 1.1: E5, evaluate, GreatPartsCel: 1.99.
- Ratios on both sides of 1.1: none.
