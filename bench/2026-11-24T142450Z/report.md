# Benchmark rig

- Date and time of the report (UTC): 2026-11-24 14:24:50
- Commit of great-parts/great-parts-cel: [`80a2e35063a37a7f7bd0e1b811dc489379a7c621`](https://github.com/great-parts/great-parts-cel/commit/80a2e35063a37a7f7bd0e1b811dc489379a7c621)
- Runs: 3
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45373031074 (GitHub keeps a run for 90 days; this report does not depend on it)

The summary of the rig follows. The full reports of BenchmarkDotNet are in the artifact `bench-results` of the run, for 90 days.

## Conditions

- Date of the summary: 2026-11-24T14:24:50.656Z
- Commit: 80a2e35063a37a7f7bd0e1b811dc489379a7c621
- Run: https://github.com/great-parts/great-parts-cel/actions/runs/45373031074
- Runner: Linux X64, image ubuntu26 20261116.202.1
- Great.Parts.Cel: built from the sources of this commit
- Packages: BenchmarkDotNet 0.15.8; Celly 1.2.0; DotnetCel 1.0.7; Cel.NET 2.3.1; Cel 0.3.3; Grpc.Tools 2.85.0

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

- BenchmarkDotNet 0.15.8; OS: Linux Ubuntu 26.04.1 LTS (Resolute Raccoon); processor: Intel Xeon Platinum 8370C CPU 2.80GHz
- Cores: 2 physical, 4 logical; architecture: X64
- Runtime: .NET 10.0.15 (10.0.15, 10.0.1526.53008); SDK: 11.0.100; configuration: RELEASE

## Runs

- Run 1: 2026-11-24T10:16:26.818Z - 2026-11-24T11:31:45.773Z; job: Default; cases: all; filter: *
- Run 2: 2026-11-24T11:31:45.783Z - 2026-11-24T12:47:33.161Z; job: Default; cases: all; filter: *
- Run 3: 2026-11-24T12:47:33.161Z - 2026-11-24T14:02:46.641Z; job: Default; cases: all; filter: *

The table of each run is `results/runs/<k>/summary.md`.

## Summary over the runs

The ratio of a row in a run is to the fastest library with a correct result in the same run (for a variant of GreatPartsCel: to the fastest other library or to itself, not to its other variants). R is the median of the ratios over the runs; the column "R > 1.1" is filled when the row has 3 runs or more; "both sides" means that the ratio is not above 1.1 in some runs and above it in others.

| Scenario | Operation | Library | Runs | Ratios by run | R | Spread | Fastest by run | Median time over the runs | R > 1.1 | Both sides of 1.1 |
|---|---|---|--:|---|--:|--:|---|--:|---|---|
| E1 | compile | Celly | 3 | 1: 4.23; 2: 4.24; 3: 4.26 | 4.24 | 1.01 | GPCel-Input | 15.72 us | yes |  |
| E1 | compile | DotnetCel | 3 | 1: 3.03; 2: 2.98; 3: 3.08 | 3.03 | 1.03 | GPCel-Input | 11.21 us | yes |  |
| E1 | compile | Cel.NET | 3 | 1: 9.31; 2: 9.40; 3: 9.48 | 9.40 | 1.02 | GPCel-Input | 34.99 us | yes |  |
| E1 | compile | TelusCel | 3 | 1: 1.97; 2: 2.03; 3: 1.99 | 1.99 | 1.03 | GPCel-Input | 7.37 us | yes |  |
| E1 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.57 us | no |  |
| E1 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.70 us | no |  |
| E1 | evaluate | Celly | 3 | 1: 1.73; 2: 1.63; 3: 1.61 | 1.63 | 1.08 | GPCel-Input | 482.2 ns | yes |  |
| E1 | evaluate | DotnetCel | 3 | 1: 1.59; 2: 1.54; 3: 1.62 | 1.59 | 1.05 | GPCel-Input | 454.9 ns | yes |  |
| E1 | evaluate | Cel.NET | 3 | 1: 1.83; 2: 1.74; 3: 1.82 | 1.82 | 1.06 | GPCel-Input | 513.7 ns | yes |  |
| E1 | evaluate | TelusCel | 3 | 1: 27.64; 2: 27.37; 3: 27.48 | 27.48 | 1.01 | GPCel-Input | 7.92 us | yes |  |
| E1 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 392.3 ns | no |  |
| E1 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 286.6 ns | no |  |
| E1 | evaluate | GPCel-Simple | 3 | 1: 4.62; 2: 4.81; 3: 4.98 | 4.81 | 1.08 | DotnetCel, Celly | 2.19 us | yes |  |
| E1 | full cycle | Celly | 3 | 1: 4.17; 2: 4.10; 3: 3.99 | 4.10 | 1.04 | GPCel-Input | 16.73 us | yes |  |
| E1 | full cycle | DotnetCel | 3 | 1: 3.67; 2: 3.62; 3: 3.71 | 3.67 | 1.03 | GPCel-Input | 14.85 us | yes |  |
| E1 | full cycle | Cel.NET | 3 | 1: 9.00; 2: 8.69; 3: 8.77 | 8.77 | 1.04 | GPCel-Input | 36.02 us | yes |  |
| E1 | full cycle | TelusCel | 3 | 1: 4.05; 2: 4.06; 3: 3.99 | 4.05 | 1.02 | GPCel-Input | 16.40 us | yes |  |
| E1 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.14 us | no |  |
| E1 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.10 us | no |  |
| E1 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 6.56 us | no |  |
| E2/10 | compile | Celly | 3 | 1: 3.25; 2: 3.10; 3: 3.16 | 3.16 | 1.05 | GPCel-Input | 21.74 us | yes |  |
| E2/10 | compile | DotnetCel | 3 | 1: 2.28; 2: 2.17; 3: 2.25 | 2.25 | 1.05 | GPCel-Input | 15.23 us | yes |  |
| E2/10 | compile | Cel.NET | 3 | 1: 6.93; 2: 6.57; 3: 6.59 | 6.59 | 1.06 | GPCel-Input | 45.96 us | yes |  |
| E2/10 | compile | TelusCel | 3 | 1: 1.78; 2: 1.67; 3: 1.68 | 1.68 | 1.06 | GPCel-Input | 11.71 us | yes |  |
| E2/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 7.36 us | no |  |
| E2/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.94 us | no |  |
| E2/10 | evaluate | Celly | 3 | 1: 2.04; 2: 2.04; 3: 2.06 | 2.04 | 1.01 | GPCel-Input | 3.82 us | yes |  |
| E2/10 | evaluate | DotnetCel | 3 | 1: 3.01; 2: 3.00; 3: 3.09 | 3.01 | 1.03 | GPCel-Input | 5.69 us | yes |  |
| E2/10 | evaluate | Cel.NET | 3 | 1: 2.57; 2: 2.52; 3: 2.57 | 2.57 | 1.02 | GPCel-Input | 4.78 us | yes |  |
| E2/10 | evaluate | TelusCel | 3 | 1: 46.73; 2: 46.73; 3: 47.28 | 46.73 | 1.01 | GPCel-Input | 87.65 us | yes |  |
| E2/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.99 us | no |  |
| E2/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.88 us | no |  |
| E2/10 | evaluate | GPCel-Simple | 3 | 1: 1.31; 2: 1.31; 3: 1.35 | 1.31 | 1.03 | Celly | 5.07 us | yes |  |
| E2/10 | full cycle | Celly | 3 | 1: 2.73; 2: 2.78; 3: 2.83 | 2.78 | 1.04 | GPCel-Input | 26.31 us | yes |  |
| E2/10 | full cycle | DotnetCel | 3 | 1: 2.74; 2: 2.87; 3: 2.86 | 2.86 | 1.05 | GPCel-Input | 26.38 us | yes |  |
| E2/10 | full cycle | Cel.NET | 3 | 1: 5.57; 2: 5.70; 3: 5.69 | 5.69 | 1.02 | GPCel-Input | 53.27 us | yes |  |
| E2/10 | full cycle | TelusCel | 3 | 1: 10.14; 2: 10.88; 3: 10.62 | 10.62 | 1.07 | GPCel-Input | 99.41 us | yes |  |
| E2/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 9.76 us | no |  |
| E2/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 9.36 us | no |  |
| E2/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 13.27 us | no |  |
| E2/100 | compile | Celly | 3 | 1: 3.16; 2: 3.14; 3: 3.22 | 3.16 | 1.02 | GPCel-Input | 21.98 us | yes |  |
| E2/100 | compile | DotnetCel | 3 | 1: 2.24; 2: 2.23; 3: 2.31 | 2.24 | 1.04 | GPCel-Input | 15.67 us | yes |  |
| E2/100 | compile | Cel.NET | 3 | 1: 6.75; 2: 6.41; 3: 6.76 | 6.75 | 1.05 | GPCel-Input | 46.17 us | yes |  |
| E2/100 | compile | TelusCel | 3 | 1: 1.69; 2: 1.65; 3: 1.71 | 1.69 | 1.04 | GPCel-Input | 11.67 us | yes |  |
| E2/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 7.22 us | no |  |
| E2/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.90 us | no |  |
| E2/100 | evaluate | Celly | 3 | 1: 1.96; 2: 1.88; 3: 2.07 | 1.96 | 1.10 | GreatPartsCel, GPCel-Input | 32.59 us | yes |  |
| E2/100 | evaluate | DotnetCel | 3 | 1: 3.00; 2: 3.05; 3: 3.23 | 3.05 | 1.08 | GreatPartsCel, GPCel-Input | 50.90 us | yes |  |
| E2/100 | evaluate | Cel.NET | 3 | 1: 2.54; 2: 2.50; 3: 2.70 | 2.54 | 1.08 | GreatPartsCel, GPCel-Input | 42.51 us | yes |  |
| E2/100 | evaluate | TelusCel | 3 | 1: 49.15; 2: 48.79; 3: 53.04 | 49.15 | 1.09 | GreatPartsCel, GPCel-Input | 823.34 us | yes |  |
| E2/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 16.75 us | no |  |
| E2/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 16.86 us | no |  |
| E2/100 | evaluate | GPCel-Simple | 3 | 1: 1.19; 2: 1.26; 3: 1.22 | 1.22 | 1.06 | Celly | 39.86 us | yes |  |
| E2/100 | full cycle | Celly | 3 | 1: 2.22; 2: 2.37; 3: 2.31 | 2.31 | 1.07 | GreatPartsCel, GPCel-Input | 55.51 us | yes |  |
| E2/100 | full cycle | DotnetCel | 3 | 1: 3.00; 2: 2.97; 3: 3.03 | 3.00 | 1.02 | GreatPartsCel, GPCel-Input | 72.79 us | yes |  |
| E2/100 | full cycle | Cel.NET | 3 | 1: 3.89; 2: 3.77; 3: 3.87 | 3.87 | 1.03 | GreatPartsCel, GPCel-Input | 92.78 us | yes |  |
| E2/100 | full cycle | TelusCel | 3 | 1: 35.11; 2: 36.35; 3: 35.91 | 35.91 | 1.04 | GreatPartsCel, GPCel-Input | 861.64 us | yes |  |
| E2/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 24.48 us | no |  |
| E2/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 24.40 us | no |  |
| E2/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 48.46 us | no |  |
| E3/10 | compile | Celly | 3 | 1: 3.23; 2: 3.22; 3: 3.31 | 3.23 | 1.03 | GPCel-Input | 18.25 us | yes |  |
| E3/10 | compile | DotnetCel | 3 | 1: 2.46; 2: 2.45; 3: 2.59 | 2.46 | 1.06 | GPCel-Input | 14.01 us | yes |  |
| E3/10 | compile | Cel.NET | 3 | 1: 6.30; 2: 6.38; 3: 6.49 | 6.38 | 1.03 | GPCel-Input | 35.79 us | yes |  |
| E3/10 | compile | TelusCel | 3 | 1: 1.65; 2: 1.68; 3: 1.73 | 1.68 | 1.05 | GPCel-Input | 9.41 us | yes |  |
| E3/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.06 us | no |  |
| E3/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.60 us | no |  |
| E3/10 | evaluate | Celly | 3 | 1: 2.58; 2: 2.41; 3: 2.54 | 2.54 | 1.07 | GPCel-Input | 1.04 us | yes |  |
| E3/10 | evaluate | DotnetCel | 3 | 1: 3.21; 2: 3.18; 3: 3.13 | 3.18 | 1.03 | GPCel-Input | 1.30 us | yes |  |
| E3/10 | evaluate | Cel.NET | 3 | 1: 2.59; 2: 2.48; 3: 2.56 | 2.56 | 1.04 | GPCel-Input | 1.04 us | yes |  |
| E3/10 | evaluate | TelusCel | 3 | 1: 120.81; 2: 118.08; 3: 119.38 | 119.38 | 1.02 | GPCel-Input | 49.09 us | yes |  |
| E3/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 512.0 ns | no |  |
| E3/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 414.4 ns | no |  |
| E3/10 | evaluate | GPCel-Simple | 3 | 1: 2.19; 2: 2.26; 3: 2.20 | 2.20 | 1.03 | Celly | 2.28 us | yes |  |
| E3/10 | full cycle | Celly | 3 | 1: 3.28; 2: 3.25; 3: 3.36 | 3.28 | 1.04 | GPCel-Input | 20.22 us | yes |  |
| E3/10 | full cycle | DotnetCel | 3 | 1: 3.18; 2: 3.23; 3: 3.35 | 3.23 | 1.05 | GPCel-Input | 19.84 us | yes |  |
| E3/10 | full cycle | Cel.NET | 3 | 1: 6.15; 2: 6.18; 3: 6.35 | 6.18 | 1.03 | GPCel-Input | 38.20 us | yes |  |
| E3/10 | full cycle | TelusCel | 3 | 1: 9.61; 2: 9.58; 3: 9.94 | 9.61 | 1.04 | GPCel-Input | 59.80 us | yes |  |
| E3/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.90 us | no |  |
| E3/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.14 us | no |  |
| E3/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 8.90 us | no |  |
| E3/100 | compile | Celly | 3 | 1: 3.22; 2: 3.23; 3: 3.18 | 3.22 | 1.02 | GPCel-Input | 18.04 us | yes |  |
| E3/100 | compile | DotnetCel | 3 | 1: 2.46; 2: 2.52; 3: 2.42 | 2.46 | 1.04 | GPCel-Input | 14.04 us | yes |  |
| E3/100 | compile | Cel.NET | 3 | 1: 6.32; 2: 6.50; 3: 6.30 | 6.32 | 1.03 | GPCel-Input | 36.14 us | yes |  |
| E3/100 | compile | TelusCel | 3 | 1: 1.64; 2: 1.66; 3: 1.65 | 1.65 | 1.01 | GPCel-Input | 9.34 us | yes |  |
| E3/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.11 us | no |  |
| E3/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.65 us | no |  |
| E3/100 | evaluate | Celly | 3 | 1: 11.00; 2: 11.10; 3: 11.23 | 11.10 | 1.02 | GPCel-Input | 4.52 us | yes |  |
| E3/100 | evaluate | DotnetCel | 3 | 1: 10.45; 2: 10.28; 3: 10.79 | 10.45 | 1.05 | GPCel-Input | 4.29 us | yes |  |
| E3/100 | evaluate | Cel.NET | 3 | 1: 6.31; 2: 6.10; 3: 6.24 | 6.24 | 1.04 | GPCel-Input | 2.56 us | yes |  |
| E3/100 | evaluate | TelusCel | 3 | 1: 1083.42; 2: 1074.77; 3: 1106.62 | 1083.42 | 1.03 | GPCel-Input | 445.08 us | yes |  |
| E3/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 496.1 ns | no |  |
| E3/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 409.7 ns | no |  |
| E3/100 | evaluate | GPCel-Simple | 3 | 1: 4.24; 2: 4.57; 3: 4.60 | 4.57 | 1.08 | Cel.NET | 11.35 us | yes |  |
| E3/100 | full cycle | Celly | 3 | 1: 3.91; 2: 3.78; 3: 3.79 | 3.79 | 1.03 | GPCel-Input | 23.88 us | yes |  |
| E3/100 | full cycle | DotnetCel | 3 | 1: 3.75; 2: 3.75; 3: 3.66 | 3.75 | 1.03 | GPCel-Input | 23.20 us | yes |  |
| E3/100 | full cycle | Cel.NET | 3 | 1: 6.57; 2: 6.53; 3: 6.39 | 6.53 | 1.03 | GPCel-Input | 40.48 us | yes |  |
| E3/100 | full cycle | TelusCel | 3 | 1: 75.52; 2: 74.02; 3: 71.63 | 74.02 | 1.05 | GPCel-Input | 458.80 us | yes |  |
| E3/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.87 us | no |  |
| E3/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.20 us | no |  |
| E3/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 17.83 us | no |  |
| E4/10 | compile | Celly | 3 | 1: 2.86; 2: 2.80; 3: 2.92 | 2.86 | 1.04 | GPCel-Input, GreatPartsCel | 33.81 us | yes |  |
| E4/10 | compile | DotnetCel | 3 | 1: 1.70; 2: 1.73; 3: 1.72 | 1.72 | 1.02 | GPCel-Input, GreatPartsCel | 20.05 us | yes |  |
| E4/10 | compile | Cel.NET | 3 | 1: 8.60; 2: 8.58; 3: 8.67 | 8.60 | 1.01 | GPCel-Input, GreatPartsCel | 101.49 us | yes |  |
| E4/10 | compile | TelusCel | 3 | 1: 1.36; 2: 1.37; 3: 1.38 | 1.37 | 1.02 | GPCel-Input, GreatPartsCel | 16.15 us | yes |  |
| E4/10 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 12.05 us | no |  |
| E4/10 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 11.80 us | no |  |
| E4/10 | evaluate | Celly | 3 | 1: 2.10; 2: 2.01; 3: 2.18 | 2.10 | 1.08 | GPCel-Input | 3.82 us | yes |  |
| E4/10 | evaluate | DotnetCel | 3 | 1: 3.65; 2: 3.12; 3: 3.15 | 3.15 | 1.17 | GPCel-Input | 5.63 us | yes |  |
| E4/10 | evaluate | Cel.NET | 3 | 1: 2.89; 2: 2.95; 3: 2.94 | 2.94 | 1.02 | GPCel-Input | 5.25 us | yes |  |
| E4/10 | evaluate | TelusCel | 3 | 1: 42.47; 2: 44.20; 3: 42.24 | 42.47 | 1.05 | GPCel-Input | 77.14 us | yes |  |
| E4/10 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 1.85 us | no |  |
| E4/10 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 1.80 us | no |  |
| E4/10 | evaluate | GPCel-Simple | 3 | 1: 1.20; 2: 1.26; 3: 1.20 | 1.20 | 1.05 | Celly | 4.59 us | yes |  |
| E4/10 | full cycle | Celly | 3 | 1: 2.82; 2: 2.76; 3: 2.80 | 2.80 | 1.02 | GPCel-Input | 39.45 us | yes |  |
| E4/10 | full cycle | DotnetCel | 3 | 1: 2.17; 2: 2.19; 3: 2.22 | 2.19 | 1.02 | GPCel-Input | 31.15 us | yes |  |
| E4/10 | full cycle | Cel.NET | 3 | 1: 7.89; 2: 8.02; 3: 7.94 | 7.94 | 1.02 | GPCel-Input | 111.83 us | yes |  |
| E4/10 | full cycle | TelusCel | 3 | 1: 6.61; 2: 6.74; 3: 6.75 | 6.74 | 1.02 | GPCel-Input | 95.16 us | yes |  |
| E4/10 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 14.53 us | no |  |
| E4/10 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 14.16 us | no |  |
| E4/10 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 18.01 us | no |  |
| E4/100 | compile | Celly | 3 | 1: 2.91; 2: 2.75; 3: 2.84 | 2.84 | 1.06 | GPCel-Input | 33.76 us | yes |  |
| E4/100 | compile | DotnetCel | 3 | 1: 1.73; 2: 1.71; 3: 1.75 | 1.73 | 1.03 | GPCel-Input | 20.58 us | yes |  |
| E4/100 | compile | Cel.NET | 3 | 1: 9.06; 2: 8.50; 3: 8.61 | 8.61 | 1.07 | GPCel-Input | 102.37 us | yes |  |
| E4/100 | compile | TelusCel | 3 | 1: 1.38; 2: 1.35; 3: 1.40 | 1.38 | 1.03 | GPCel-Input | 16.27 us | yes |  |
| E4/100 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 12.23 us | no |  |
| E4/100 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 11.88 us | no |  |
| E4/100 | evaluate | Celly | 3 | 1: 1.99; 2: 2.36; 3: 2.18 | 2.18 | 1.18 | GPCel-Input, GreatPartsCel | 38.52 us | yes |  |
| E4/100 | evaluate | DotnetCel | 3 | 1: 3.54; 2: 3.82; 3: 3.71 | 3.71 | 1.08 | GPCel-Input, GreatPartsCel | 65.56 us | yes |  |
| E4/100 | evaluate | Cel.NET | 3 | 1: 3.40; 2: 3.68; 3: 3.51 | 3.51 | 1.08 | GPCel-Input, GreatPartsCel | 63.20 us | yes |  |
| E4/100 | evaluate | TelusCel | 3 | 1: 48.54; 2: 52.51; 3: 52.36 | 52.36 | 1.08 | GPCel-Input, GreatPartsCel | 904.72 us | yes |  |
| E4/100 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 19.06 us | no |  |
| E4/100 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 18.64 us | no |  |
| E4/100 | evaluate | GPCel-Simple | 3 | 1: 1.13; 2: 1.05; 3: 1.05 | 1.05 | 1.08 | Celly | 41.89 us | no | yes |
| E4/100 | full cycle | Celly | 3 | 1: 2.32; 2: 2.44; 3: 2.41 | 2.41 | 1.05 | GreatPartsCel, GPCel-Input | 73.67 us | yes |  |
| E4/100 | full cycle | DotnetCel | 3 | 1: 2.90; 2: 2.84; 3: 3.03 | 2.90 | 1.07 | GreatPartsCel, GPCel-Input | 91.32 us | yes |  |
| E4/100 | full cycle | Cel.NET | 3 | 1: 5.45; 2: 5.32; 3: 5.67 | 5.45 | 1.06 | GreatPartsCel, GPCel-Input | 171.31 us | yes |  |
| E4/100 | full cycle | TelusCel | 3 | 1: 29.61; 2: 29.66; 3: 30.84 | 29.66 | 1.04 | GreatPartsCel, GPCel-Input | 941.31 us | yes |  |
| E4/100 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 32.45 us | no |  |
| E4/100 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 32.19 us | no |  |
| E4/100 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 55.22 us | no |  |
| E5 | compile | Celly | 3 | 1: 4.81; 2: 4.91; 3: 4.73 | 4.81 | 1.04 | GPCel-Input | 13.00 us | yes |  |
| E5 | compile | DotnetCel | 3 | 1: 3.54; 2: 3.73; 3: 3.54 | 3.54 | 1.05 | GPCel-Input | 9.73 us | yes |  |
| E5 | compile | Cel.NET | 3 | 1: 7.57; 2: 7.94; 3: 7.50 | 7.57 | 1.06 | GPCel-Input | 20.60 us | yes |  |
| E5 | compile | TelusCel | 3 | 1: 2.09; 2: 2.14; 3: 2.07 | 2.09 | 1.04 | GPCel-Input | 5.67 us | yes |  |
| E5 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.17 us | no |  |
| E5 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 2.72 us | no |  |
| E5 | evaluate | Celly | 3 | 1: 1.68; 2: 1.75; 3: 1.66 | 1.68 | 1.05 | GPCel-Input | 538.5 ns | yes |  |
| E5 | evaluate | DotnetCel | 3 | 1: 1.78; 2: 1.81; 3: 1.78 | 1.78 | 1.02 | GPCel-Input | 567.4 ns | yes |  |
| E5 | evaluate | Cel.NET | 3 | 1: 235.17; 2: 250.26; 3: 237.23 | 237.23 | 1.06 | GPCel-Input | 75.51 us | yes |  |
| E5 | evaluate | TelusCel | 3 | 1: 30.39; 2: 31.65; 3: 31.00 | 31.00 | 1.04 | GPCel-Input | 9.87 us | yes |  |
| E5 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 407.9 ns | no |  |
| E5 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 318.2 ns | no |  |
| E5 | evaluate | GPCel-Simple | 3 | 1: 3.93; 2: 3.94; 3: 4.05 | 3.94 | 1.03 | Celly | 2.14 us | yes |  |
| E5 | full cycle | Celly | 3 | 1: 4.50; 2: 4.43; 3: 4.44 | 4.44 | 1.02 | GPCel-Input | 13.96 us | yes |  |
| E5 | full cycle | DotnetCel | 3 | 1: 4.61; 2: 4.56; 3: 4.63 | 4.61 | 1.01 | GPCel-Input | 14.47 us | yes |  |
| E5 | full cycle | Cel.NET | 3 | 1: 36.77; 2: 36.46; 3: 36.49 | 36.49 | 1.01 | GPCel-Input | 115.00 us | yes |  |
| E5 | full cycle | TelusCel | 3 | 1: 5.40; 2: 5.35; 3: 5.66 | 5.40 | 1.06 | GPCel-Input | 16.96 us | yes |  |
| E5 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 3.81 us | no |  |
| E5 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.14 us | no |  |
| E5 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 5.55 us | no |  |
| E6 | compile | Celly | 3 | 1: 4.25; 2: 4.46; 3: 4.30 | 4.30 | 1.05 | GPCel-Input | 16.88 us | yes |  |
| E6 | compile | Cel.NET | 3 | 1: 9.63; 2: 10.02; 3: 9.61 | 9.63 | 1.04 | GPCel-Input | 37.91 us | yes |  |
| E6 | compile | TelusCel | 3 | 1: 2.61; 2: 2.73; 3: 2.57 | 2.61 | 1.06 | GPCel-Input | 10.29 us | yes |  |
| E6 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.17 us | no |  |
| E6 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 3.94 us | no |  |
| E6 | evaluate | Celly | 3 | 1: 1.85; 2: 1.79; 3: 1.77 | 1.79 | 1.05 | GPCel-Input | 525.4 ns | yes |  |
| E6 | evaluate | Cel.NET | 3 | 1: 1.84; 2: 1.82; 3: 1.84 | 1.84 | 1.01 | GPCel-Input | 541.5 ns | yes |  |
| E6 | evaluate | TelusCel | 3 | 1: 46.41; 2: 43.07; 3: 44.59 | 44.59 | 1.08 | GPCel-Input | 13.11 us | yes |  |
| E6 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 375.8 ns | no |  |
| E6 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 294.0 ns | no |  |
| E6 | evaluate | GPCel-Simple | 3 | 1: 3.78; 2: 4.06; 3: 4.10 | 4.06 | 1.09 | Cel.NET, Celly | 2.13 us | yes |  |
| E6 | full cycle | Celly | 3 | 1: 4.15; 2: 4.16; 3: 4.15 | 4.15 | 1.00 | GPCel-Input | 18.10 us | yes |  |
| E6 | full cycle | Cel.NET | 3 | 1: 8.98; 2: 9.14; 3: 8.99 | 8.99 | 1.02 | GPCel-Input | 39.18 us | yes |  |
| E6 | full cycle | TelusCel | 3 | 1: 5.77; 2: 5.72; 3: 5.69 | 5.72 | 1.01 | GPCel-Input | 24.82 us | yes |  |
| E6 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.80 us | no |  |
| E6 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.36 us | no |  |
| E6 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 6.63 us | no |  |
| E7 | compile | Celly | 3 | 1: 3.99; 2: 4.14; 3: 4.29 | 4.14 | 1.07 | GPCel-Input | 16.86 us | yes |  |
| E7 | compile | DotnetCel | 3 | 1: 2.62; 2: 2.77; 3: 2.74 | 2.74 | 1.06 | GPCel-Input | 11.10 us | yes |  |
| E7 | compile | Cel.NET | 3 | 1: 8.67; 2: 9.05; 3: 9.37 | 9.05 | 1.08 | GPCel-Input | 36.73 us | yes |  |
| E7 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.42 us | no |  |
| E7 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.01 us | no |  |
| E7 | evaluate | Celly | 3 | 1: 2.59; 2: 2.61; 3: 2.52 | 2.59 | 1.03 | GPCel-Input | 708.7 ns | yes |  |
| E7 | evaluate | DotnetCel | 3 | 1: 1.66; 2: 1.64; 3: 1.64 | 1.64 | 1.01 | GPCel-Input | 451.8 ns | yes |  |
| E7 | evaluate | Cel.NET | 3 | 1: 2.71; 2: 2.69; 3: 2.66 | 2.69 | 1.02 | GPCel-Input | 740.1 ns | yes |  |
| E7 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 378.2 ns | no |  |
| E7 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 275.4 ns | no |  |
| E7 | evaluate | GPCel-Simple | 3 | 1: 4.69; 2: 4.58; 3: 4.53 | 4.58 | 1.04 | DotnetCel | 2.09 us | yes |  |
| E7 | full cycle | Celly | 3 | 1: 4.28; 2: 4.15; 3: 4.32 | 4.28 | 1.04 | GPCel-Input | 18.51 us | yes |  |
| E7 | full cycle | DotnetCel | 3 | 1: 3.30; 2: 3.46; 3: 3.39 | 3.39 | 1.05 | GPCel-Input | 14.53 us | yes |  |
| E7 | full cycle | Cel.NET | 3 | 1: 9.04; 2: 8.77; 3: 9.13 | 9.04 | 1.04 | GPCel-Input | 39.11 us | yes |  |
| E7 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 4.93 us | no |  |
| E7 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.33 us | no |  |
| E7 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 6.90 us | no |  |
| E8 | compile | Celly | 3 | 1: 3.60; 2: 3.68; 3: 3.64 | 3.64 | 1.02 | GPCel-Input | 21.72 us | yes |  |
| E8 | compile | Cel.NET | 3 | 1: 12.43; 2: 12.53; 3: 12.21 | 12.43 | 1.03 | GPCel-Input | 73.88 us | yes |  |
| E8 | compile | TelusCel | 3 | 1: 2.42; 2: 2.46; 3: 2.43 | 2.43 | 1.02 | GPCel-Input | 14.51 us | yes |  |
| E8 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 6.88 us | no |  |
| E8 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 5.97 us | no |  |
| E8 | evaluate | Celly | 3 | 1: 3.11; 2: 3.05; 3: 3.21 | 3.11 | 1.05 | GPCel-Input | 1.08 us | yes |  |
| E8 | evaluate | Cel.NET | 3 | 1: 1.77; 2: 1.79; 3: 1.84 | 1.79 | 1.04 | GPCel-Input | 621.3 ns | yes |  |
| E8 | evaluate | TelusCel | 3 | 1: 34.21; 2: 33.43; 3: 35.17 | 34.21 | 1.05 | GPCel-Input | 11.86 us | yes |  |
| E8 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 460.5 ns | no |  |
| E8 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 346.8 ns | no |  |
| E8 | evaluate | GPCel-Simple | 3 | 1: 3.59; 2: 3.65; 3: 3.51 | 3.59 | 1.04 | Cel.NET | 2.20 us | yes |  |
| E8 | full cycle | Celly | 3 | 1: 3.53; 2: 3.77; 3: 3.70 | 3.70 | 1.07 | GPCel-Input | 24.06 us | yes |  |
| E8 | full cycle | Cel.NET | 3 | 1: 11.49; 2: 11.98; 3: 11.66 | 11.66 | 1.04 | GPCel-Input | 75.86 us | yes |  |
| E8 | full cycle | TelusCel | 3 | 1: 4.29; 2: 4.37; 3: 4.33 | 4.33 | 1.02 | GPCel-Input | 28.14 us | yes |  |
| E8 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 7.66 us | no |  |
| E8 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 6.50 us | no |  |
| E8 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 9.11 us | no |  |
| E9 | compile | Celly | 3 | 1: 4.42; 2: 4.40; 3: 4.35 | 4.40 | 1.02 | GPCel-Input | 19.19 us | yes |  |
| E9 | compile | DotnetCel | 3 | 1: 2.55; 2: 2.63; 3: 2.60 | 2.60 | 1.03 | GPCel-Input | 11.34 us | yes |  |
| E9 | compile | Cel.NET | 3 | 1: 11.19; 2: 11.15; 3: 11.38 | 11.19 | 1.02 | GPCel-Input | 48.69 us | yes |  |
| E9 | compile | TelusCel | 3 | 1: 1.65; 2: 1.66; 3: 1.71 | 1.66 | 1.03 | GPCel-Input | 7.23 us | yes |  |
| E9 | compile | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.35 us | no |  |
| E9 | compile | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.36 us | no |  |
| E9 | evaluate | Celly | 3 | 1: 1.51; 2: 1.40; 3: 1.43 | 1.43 | 1.08 | GPCel-Input | 494.5 ns | yes |  |
| E9 | evaluate | DotnetCel | 3 | 1: 1.50; 2: 1.46; 3: 1.46 | 1.46 | 1.03 | GPCel-Input | 501.8 ns | yes |  |
| E9 | evaluate | Cel.NET | 3 | 1: 1.65; 2: 1.65; 3: 1.62 | 1.65 | 1.02 | GPCel-Input | 559.4 ns | yes |  |
| E9 | evaluate | TelusCel | 3 | 1: 22.48; 2: 21.73; 3: 22.08 | 22.08 | 1.03 | GPCel-Input | 7.53 us | yes |  |
| E9 | evaluate | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 442.7 ns | no |  |
| E9 | evaluate | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 344.6 ns | no |  |
| E9 | evaluate | GPCel-Simple | 3 | 1: 4.28; 2: 4.63; 3: 4.30 | 4.30 | 1.08 | DotnetCel, Celly | 2.15 us | yes |  |
| E9 | full cycle | Celly | 3 | 1: 4.34; 2: 4.14; 3: 4.20 | 4.20 | 1.05 | GPCel-Input | 20.49 us | yes |  |
| E9 | full cycle | DotnetCel | 3 | 1: 3.08; 2: 3.10; 3: 3.03 | 3.08 | 1.02 | GPCel-Input | 14.87 us | yes |  |
| E9 | full cycle | Cel.NET | 3 | 1: 10.30; 2: 10.32; 3: 10.38 | 10.32 | 1.01 | GPCel-Input | 49.96 us | yes |  |
| E9 | full cycle | TelusCel | 3 | 1: 3.28; 2: 3.21; 3: 3.36 | 3.28 | 1.05 | GPCel-Input | 15.85 us | yes |  |
| E9 | full cycle | GreatPartsCel | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GreatPartsCel | 5.93 us | no |  |
| E9 | full cycle | GPCel-Input | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Input | 4.84 us | no |  |
| E9 | full cycle | GPCel-Simple | 3 | 1: 1.00; 2: 1.00; 3: 1.00 | 1.00 | 1.00 | GPCel-Simple | 7.38 us | no |  |

R above 1.1 (all libraries): E1, compile, Celly: 4.24; E1, compile, DotnetCel: 3.03; E1, compile, Cel.NET: 9.40; E1, compile, TelusCel: 1.99; E1, evaluate, Celly: 1.63; E1, evaluate, DotnetCel: 1.59; E1, evaluate, Cel.NET: 1.82; E1, evaluate, TelusCel: 27.48; E1, evaluate, GPCel-Simple: 4.81; E1, full cycle, Celly: 4.10; E1, full cycle, DotnetCel: 3.67; E1, full cycle, Cel.NET: 8.77; E1, full cycle, TelusCel: 4.05; E2/10, compile, Celly: 3.16; E2/10, compile, DotnetCel: 2.25; E2/10, compile, Cel.NET: 6.59; E2/10, compile, TelusCel: 1.68; E2/10, evaluate, Celly: 2.04; E2/10, evaluate, DotnetCel: 3.01; E2/10, evaluate, Cel.NET: 2.57; E2/10, evaluate, TelusCel: 46.73; E2/10, evaluate, GPCel-Simple: 1.31; E2/10, full cycle, Celly: 2.78; E2/10, full cycle, DotnetCel: 2.86; E2/10, full cycle, Cel.NET: 5.69; E2/10, full cycle, TelusCel: 10.62; E2/100, compile, Celly: 3.16; E2/100, compile, DotnetCel: 2.24; E2/100, compile, Cel.NET: 6.75; E2/100, compile, TelusCel: 1.69; E2/100, evaluate, Celly: 1.96; E2/100, evaluate, DotnetCel: 3.05; E2/100, evaluate, Cel.NET: 2.54; E2/100, evaluate, TelusCel: 49.15; E2/100, evaluate, GPCel-Simple: 1.22; E2/100, full cycle, Celly: 2.31; E2/100, full cycle, DotnetCel: 3.00; E2/100, full cycle, Cel.NET: 3.87; E2/100, full cycle, TelusCel: 35.91; E3/10, compile, Celly: 3.23; E3/10, compile, DotnetCel: 2.46; E3/10, compile, Cel.NET: 6.38; E3/10, compile, TelusCel: 1.68; E3/10, evaluate, Celly: 2.54; E3/10, evaluate, DotnetCel: 3.18; E3/10, evaluate, Cel.NET: 2.56; E3/10, evaluate, TelusCel: 119.38; E3/10, evaluate, GPCel-Simple: 2.20; E3/10, full cycle, Celly: 3.28; E3/10, full cycle, DotnetCel: 3.23; E3/10, full cycle, Cel.NET: 6.18; E3/10, full cycle, TelusCel: 9.61; E3/100, compile, Celly: 3.22; E3/100, compile, DotnetCel: 2.46; E3/100, compile, Cel.NET: 6.32; E3/100, compile, TelusCel: 1.65; E3/100, evaluate, Celly: 11.10; E3/100, evaluate, DotnetCel: 10.45; E3/100, evaluate, Cel.NET: 6.24; E3/100, evaluate, TelusCel: 1083.42; E3/100, evaluate, GPCel-Simple: 4.57; E3/100, full cycle, Celly: 3.79; E3/100, full cycle, DotnetCel: 3.75; E3/100, full cycle, Cel.NET: 6.53; E3/100, full cycle, TelusCel: 74.02; E4/10, compile, Celly: 2.86; E4/10, compile, DotnetCel: 1.72; E4/10, compile, Cel.NET: 8.60; E4/10, compile, TelusCel: 1.37; E4/10, evaluate, Celly: 2.10; E4/10, evaluate, DotnetCel: 3.15; E4/10, evaluate, Cel.NET: 2.94; E4/10, evaluate, TelusCel: 42.47; E4/10, evaluate, GPCel-Simple: 1.20; E4/10, full cycle, Celly: 2.80; E4/10, full cycle, DotnetCel: 2.19; E4/10, full cycle, Cel.NET: 7.94; E4/10, full cycle, TelusCel: 6.74; E4/100, compile, Celly: 2.84; E4/100, compile, DotnetCel: 1.73; E4/100, compile, Cel.NET: 8.61; E4/100, compile, TelusCel: 1.38; E4/100, evaluate, Celly: 2.18; E4/100, evaluate, DotnetCel: 3.71; E4/100, evaluate, Cel.NET: 3.51; E4/100, evaluate, TelusCel: 52.36; E4/100, full cycle, Celly: 2.41; E4/100, full cycle, DotnetCel: 2.90; E4/100, full cycle, Cel.NET: 5.45; E4/100, full cycle, TelusCel: 29.66; E5, compile, Celly: 4.81; E5, compile, DotnetCel: 3.54; E5, compile, Cel.NET: 7.57; E5, compile, TelusCel: 2.09; E5, evaluate, Celly: 1.68; E5, evaluate, DotnetCel: 1.78; E5, evaluate, Cel.NET: 237.23; E5, evaluate, TelusCel: 31.00; E5, evaluate, GPCel-Simple: 3.94; E5, full cycle, Celly: 4.44; E5, full cycle, DotnetCel: 4.61; E5, full cycle, Cel.NET: 36.49; E5, full cycle, TelusCel: 5.40; E6, compile, Celly: 4.30; E6, compile, Cel.NET: 9.63; E6, compile, TelusCel: 2.61; E6, evaluate, Celly: 1.79; E6, evaluate, Cel.NET: 1.84; E6, evaluate, TelusCel: 44.59; E6, evaluate, GPCel-Simple: 4.06; E6, full cycle, Celly: 4.15; E6, full cycle, Cel.NET: 8.99; E6, full cycle, TelusCel: 5.72; E7, compile, Celly: 4.14; E7, compile, DotnetCel: 2.74; E7, compile, Cel.NET: 9.05; E7, evaluate, Celly: 2.59; E7, evaluate, DotnetCel: 1.64; E7, evaluate, Cel.NET: 2.69; E7, evaluate, GPCel-Simple: 4.58; E7, full cycle, Celly: 4.28; E7, full cycle, DotnetCel: 3.39; E7, full cycle, Cel.NET: 9.04; E8, compile, Celly: 3.64; E8, compile, Cel.NET: 12.43; E8, compile, TelusCel: 2.43; E8, evaluate, Celly: 3.11; E8, evaluate, Cel.NET: 1.79; E8, evaluate, TelusCel: 34.21; E8, evaluate, GPCel-Simple: 3.59; E8, full cycle, Celly: 3.70; E8, full cycle, Cel.NET: 11.66; E8, full cycle, TelusCel: 4.33; E9, compile, Celly: 4.40; E9, compile, DotnetCel: 2.60; E9, compile, Cel.NET: 11.19; E9, compile, TelusCel: 1.66; E9, evaluate, Celly: 1.43; E9, evaluate, DotnetCel: 1.46; E9, evaluate, Cel.NET: 1.65; E9, evaluate, TelusCel: 22.08; E9, evaluate, GPCel-Simple: 4.30; E9, full cycle, Celly: 4.20; E9, full cycle, DotnetCel: 3.08; E9, full cycle, Cel.NET: 10.32; E9, full cycle, TelusCel: 3.28.

**Rows of GreatPartsCel.**

- R above 1.1: E1, evaluate, GPCel-Simple: 4.81; E2/10, evaluate, GPCel-Simple: 1.31; E2/100, evaluate, GPCel-Simple: 1.22; E3/10, evaluate, GPCel-Simple: 2.20; E3/100, evaluate, GPCel-Simple: 4.57; E4/10, evaluate, GPCel-Simple: 1.20; E5, evaluate, GPCel-Simple: 3.94; E6, evaluate, GPCel-Simple: 4.06; E7, evaluate, GPCel-Simple: 4.58; E8, evaluate, GPCel-Simple: 3.59; E9, evaluate, GPCel-Simple: 4.30.
- Ratios on both sides of 1.1: E4/100, evaluate, GPCel-Simple: 1.05.

## Limits of the speed-ups

Job: Default; started 2026-11-24T14:02:51.807Z, finished 2026-11-24T14:24:50.485Z. Medians of BenchmarkDotNet on the code of this commit; the reports are in `results/thresholds/bdn/results`.

| Limit | Rule | Verdict |
|---|---|---|
| hybrid search of a substring, 64 code units | .NET at M = 64 is not slower than Knuth-Morris-Pratt; otherwise the limit is 32 | holds: 64 |
| build of the automaton of the pattern E5 | the median of the build is at most 1000 ns | does not hold: 1731 ns |
| index of the values of a list (distinct, sets), 24 values | the scan (the library's index at N up to 24) is not slower than the buckets at N = 8, 16 and 24, for distinct and for sets.contains | does not hold, the scan is slower than the buckets (ratio scan / buckets): distinct N = 16: 1.03; contains N = 16: 1.10; distinct N = 24: 1.16; contains N = 24: 1.30 |

### Search of a substring, the text (ab)^5000

M is the length of the pattern, the slowest case for the search of .NET. "Library" is the search as it runs: .NET up to the limit, Knuth-Morris-Pratt above. Net / KMP below 1 means .NET is faster.

| M | KMP | .NET | Library | Net / KMP |
|--:|--:|--:|--:|--:|
| 48 | 29.35 us | 12.06 us | 12.04 us | 0.41 |
| 64 | 29.32 us | 11.97 us | 12.22 us | 0.41 |
| 65 | 29.46 us | 660.6 ns | 29.75 us | 0.02 |
| 80 | 30.01 us | 17.09 us | 30.17 us | 0.57 |

### Build of the automaton

| Pattern | Parse and program | Automaton |
|---|--:|--:|
| E5 | 1.08 us | 1.73 us |
| UUID | 2.11 us | 4.58 us |

### Index of the values: the lists of N integers

"Library" is the index as the library makes it (the scan up to 24 values, the buckets above); "buckets" forces the buckets at any N; "one hash" gives every value the same hash; "pairwise" are integers of 2^53 and more, compared with each other in order. Scan / buckets below 1 means the scan is faster; at N above 24 the library and the buckets are the same index.

| Operation | N | Library | Buckets | One hash | Pairwise | Scan / buckets |
|---|--:|--:|--:|--:|--:|--:|
| distinct | 8 | 290.7 ns | 352.7 ns | 441.7 ns | 543.1 ns | 0.82 |
| distinct | 16 | 610.6 ns | 591.4 ns | 1.14 us | 1.46 us | 1.03 |
| distinct | 24 | 986.8 ns | 853.2 ns | 2.08 us | 2.72 us | 1.16 |
| distinct | 32 | 1.08 us | 1.11 us | 4.20 us | 4.54 us | - |
| distinct | 48 | 2.21 us | 1.66 us | 9.03 us | 9.30 us | - |
| sets.contains | 8 | 557.7 ns | 570.5 ns | 947.5 ns | 925.2 ns | 0.98 |
| sets.contains | 16 | 1.16 us | 1.05 us | 2.85 us | 2.77 us | 1.10 |
| sets.contains | 24 | 1.82 us | 1.40 us | 5.75 us | 5.25 us | 1.30 |
| sets.contains | 32 | 2.16 us | 1.98 us | 10.00 us | 8.70 us | - |
| sets.contains | 48 | 3.16 us | 2.78 us | 20.28 us | 18.54 us | - |

### distinct and sets.contains through the public API

The evaluation of the program on a list of N integers; the time per element shows a jump at the limit, if there is one.

| N | distinct | per element | sets.contains | per element |
|--:|--:|--:|--:|--:|
| 16 | 900.6 ns | 56.3 ns | 1.62 us | 101.4 ns |
| 24 | 1.42 us | 59.2 ns | 2.46 us | 102.7 ns |
| 25 | 1.09 us | 43.5 ns | 2.44 us | 97.5 ns |
| 32 | 1.36 us | 42.4 ns | 2.91 us | 90.8 ns |
| 48 | 1.95 us | 40.7 ns | 4.27 us | 89.0 ns |
