## Run 1: scenario x operation

- Run 1: 2026-11-26T15:02:21.231Z - 2026-11-26T16:03:21.575Z; job: Default; runtime: Native AOT; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 18.0 ns | - | 160 B | - | |
| E1 | compile | Cel.NET | 32.01 us | 32.03 us | 54.62 KB | 54.78 KB | 10.55 | yes |
| E1 | compile | TelusCel | 7.00 us | 7.01 us | 10.31 KB | 10.47 KB | 2.31 | yes |
| E1 | compile | GreatPartsCel | 3.80 us | 3.82 us | 8.97 KB | 9.13 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.03 us | 3.05 us | 7.10 KB | 7.25 KB | 1.00 |  |
| E1 | compile | Celly | - | - | - | - | excluded | |
| E1 | compile | DotnetCel | - | - | - | - | excluded | |
| E1 | evaluate | Cel.NET | 572.7 ns | - | 1.51 KB | - | 1.76 | yes |
| E1 | evaluate | TelusCel | 6.80 us | - | 11.13 KB | - | 20.94 | yes |
| E1 | evaluate | GreatPartsCel | 387.3 ns | - | 320 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 324.6 ns | - | 320 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 538.4 ns | - | 160 B | - | 1.00 |  |
| E1 | evaluate | Celly | - | - | - | - | excluded | |
| E1 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E1 | full cycle | Cel.NET | 32.61 us | 32.63 us | 56.13 KB | 56.29 KB | 9.35 | yes |
| E1 | full cycle | TelusCel | 14.58 us | 14.60 us | 21.44 KB | 21.59 KB | 4.18 | yes |
| E1 | full cycle | GreatPartsCel | 4.39 us | 4.41 us | 9.28 KB | 9.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 3.49 us | 3.51 us | 7.40 KB | 7.56 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 4.29 us | 4.31 us | 7.53 KB | 7.69 KB | 1.00 |  |
| E1 | full cycle | Celly | - | - | - | - | excluded | |
| E1 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E2/10 | text creation | - | - | 17.8 ns | - | 152 B | - | |
| E2/10 | compile | Cel.NET | 40.35 us | 40.37 us | 78.12 KB | 78.27 KB | 7.60 | yes |
| E2/10 | compile | TelusCel | 11.09 us | 11.10 us | 15.48 KB | 15.63 KB | 2.09 | yes |
| E2/10 | compile | GreatPartsCel | 5.83 us | 5.85 us | 12.38 KB | 12.53 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 5.31 us | 5.32 us | 11.73 KB | 11.88 KB | 1.00 |  |
| E2/10 | compile | Celly | - | - | - | - | excluded | |
| E2/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E2/10 | evaluate | Cel.NET | 4.83 us | - | 5.83 KB | - | 2.02 | yes |
| E2/10 | evaluate | TelusCel | 73.40 us | - | 111.01 KB | - | 30.71 | yes |
| E2/10 | evaluate | GreatPartsCel | 2.55 us | - | 368 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 2.39 us | - | 368 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 2.72 us | - | 160 B | - | 1.00 |  |
| E2/10 | evaluate | Celly | - | - | - | - | excluded | |
| E2/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E2/10 | full cycle | Cel.NET | 44.19 us | 44.21 us | 83.97 KB | 84.12 KB | 5.43 | yes |
| E2/10 | full cycle | TelusCel | 90.30 us | 90.32 us | 126.49 KB | 126.64 KB | 11.10 | yes |
| E2/10 | full cycle | GreatPartsCel | 8.98 us | 9.00 us | 12.74 KB | 12.89 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 8.14 us | 8.15 us | 12.08 KB | 12.23 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 9.31 us | 9.33 us | 12.15 KB | 12.30 KB | 1.00 |  |
| E2/10 | full cycle | Celly | - | - | - | - | excluded | |
| E2/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E2/100 | text creation | - | - | 17.6 ns | - | 152 B | - | |
| E2/100 | compile | Cel.NET | 39.21 us | 39.23 us | 78.11 KB | 78.26 KB | 7.20 | yes |
| E2/100 | compile | TelusCel | 11.01 us | 11.02 us | 15.48 KB | 15.63 KB | 2.02 | yes |
| E2/100 | compile | GreatPartsCel | 5.96 us | 5.98 us | 12.38 KB | 12.53 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 5.44 us | 5.46 us | 11.73 KB | 11.88 KB | 1.00 |  |
| E2/100 | compile | Celly | - | - | - | - | excluded | |
| E2/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E2/100 | evaluate | Cel.NET | 44.55 us | - | 51.15 KB | - | 2.02 | yes |
| E2/100 | evaluate | TelusCel | 730.86 us | - | 1.03 MB | - | 33.09 | yes |
| E2/100 | evaluate | GreatPartsCel | 22.33 us | - | 368 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 22.09 us | - | 368 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 22.99 us | - | 160 B | - | 1.00 |  |
| E2/100 | evaluate | Celly | - | - | - | - | excluded | |
| E2/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E2/100 | full cycle | Cel.NET | 86.87 us | 86.88 us | 129.27 KB | 129.42 KB | 3.11 | yes |
| E2/100 | full cycle | TelusCel | 734.31 us | 734.33 us | 1.04 MB | 1.04 MB | 26.31 | yes |
| E2/100 | full cycle | GreatPartsCel | 28.79 us | 28.81 us | 12.74 KB | 12.89 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 27.91 us | 27.93 us | 12.08 KB | 12.23 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 30.21 us | 30.23 us | 12.15 KB | 12.30 KB | 1.00 |  |
| E2/100 | full cycle | Celly | - | - | - | - | excluded | |
| E2/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E3/10 | text creation | - | - | 18.0 ns | - | 152 B | - | |
| E3/10 | compile | Cel.NET | 28.71 us | 28.72 us | 57.47 KB | 57.62 KB | 6.87 | yes |
| E3/10 | compile | TelusCel | 8.82 us | 8.83 us | 13.36 KB | 13.51 KB | 2.11 | yes |
| E3/10 | compile | GreatPartsCel | 4.91 us | 4.93 us | 11.00 KB | 11.15 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 4.18 us | 4.20 us | 10.51 KB | 10.66 KB | 1.00 |  |
| E3/10 | compile | Celly | - | - | - | - | excluded | |
| E3/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E3/10 | evaluate | Cel.NET | 1.06 us | - | 2.02 KB | - | 2.26 | yes |
| E3/10 | evaluate | TelusCel | 43.54 us | - | 64.45 KB | - | 93.07 | yes |
| E3/10 | evaluate | GreatPartsCel | 536.3 ns | - | 368 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 467.8 ns | - | 368 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 670.4 ns | - | 160 B | - | 1.00 |  |
| E3/10 | evaluate | Celly | - | - | - | - | excluded | |
| E3/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E3/10 | full cycle | Cel.NET | 30.58 us | 30.60 us | 59.51 KB | 59.65 KB | 6.29 | yes |
| E3/10 | full cycle | TelusCel | 53.43 us | 53.45 us | 77.81 KB | 77.96 KB | 10.99 | yes |
| E3/10 | full cycle | GreatPartsCel | 5.51 us | 5.53 us | 11.36 KB | 11.51 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 4.86 us | 4.88 us | 10.87 KB | 11.02 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 5.88 us | 5.90 us | 10.93 KB | 11.08 KB | 1.00 |  |
| E3/10 | full cycle | Celly | - | - | - | - | excluded | |
| E3/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E3/100 | text creation | - | - | 18.1 ns | - | 152 B | - | |
| E3/100 | compile | Cel.NET | 28.16 us | 28.18 us | 57.48 KB | 57.63 KB | 6.75 | yes |
| E3/100 | compile | TelusCel | 8.82 us | 8.84 us | 13.36 KB | 13.51 KB | 2.11 | yes |
| E3/100 | compile | GreatPartsCel | 4.65 us | 4.67 us | 11.00 KB | 11.14 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 4.17 us | 4.19 us | 10.51 KB | 10.66 KB | 1.00 |  |
| E3/100 | compile | Celly | - | - | - | - | excluded | |
| E3/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E3/100 | evaluate | Cel.NET | 2.20 us | - | 8.77 KB | - | 4.87 | yes |
| E3/100 | evaluate | TelusCel | 400.22 us | - | 585.88 KB | - | 886.37 | yes |
| E3/100 | evaluate | GreatPartsCel | 546.8 ns | - | 368 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 451.5 ns | - | 368 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 664.1 ns | - | 160 B | - | 1.00 |  |
| E3/100 | evaluate | Celly | - | - | - | - | excluded | |
| E3/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E3/100 | full cycle | Cel.NET | 31.57 us | 31.59 us | 66.23 KB | 66.38 KB | 6.57 | yes |
| E3/100 | full cycle | TelusCel | 447.67 us | 447.68 us | 599.24 KB | 599.39 KB | 93.22 | yes |
| E3/100 | full cycle | GreatPartsCel | 5.79 us | 5.80 us | 11.36 KB | 11.51 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 4.80 us | 4.82 us | 10.86 KB | 11.01 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 5.98 us | 6.00 us | 10.93 KB | 11.08 KB | 1.00 |  |
| E3/100 | full cycle | Celly | - | - | - | - | excluded | |
| E3/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E4/10 | text creation | - | - | 19.1 ns | - | 184 B | - | |
| E4/10 | compile | Cel.NET | 99.71 us | 99.73 us | 145.79 KB | 145.97 KB | 10.93 | yes |
| E4/10 | compile | TelusCel | 15.05 us | 15.07 us | 21.24 KB | 21.42 KB | 1.65 | yes |
| E4/10 | compile | GreatPartsCel | 9.62 us | 9.64 us | 19.08 KB | 19.26 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 9.13 us | 9.14 us | 18.67 KB | 18.85 KB | 1.00 |  |
| E4/10 | compile | Celly | - | - | - | - | excluded | |
| E4/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E4/10 | evaluate | Cel.NET | 5.19 us | - | 6.74 KB | - | 3.02 | yes |
| E4/10 | evaluate | TelusCel | 68.22 us | - | 99.07 KB | - | 39.61 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.83 us | - | 872 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.72 us | - | 872 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 2.05 us | - | 664 B | - | 1.00 |  |
| E4/10 | evaluate | Celly | - | - | - | - | excluded | |
| E4/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E4/10 | full cycle | Cel.NET | 108.37 us | 108.39 us | 152.52 KB | 152.70 KB | 9.38 | yes |
| E4/10 | full cycle | TelusCel | 84.49 us | 84.51 us | 120.31 KB | 120.49 KB | 7.31 | yes |
| E4/10 | full cycle | GreatPartsCel | 12.35 us | 12.37 us | 19.93 KB | 20.11 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 11.55 us | 11.57 us | 19.52 KB | 19.70 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 12.82 us | 12.84 us | 19.58 KB | 19.76 KB | 1.00 |  |
| E4/10 | full cycle | Celly | - | - | - | - | excluded | |
| E4/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E4/100 | text creation | - | - | 19.0 ns | - | 184 B | - | |
| E4/100 | compile | Cel.NET | 98.34 us | 98.36 us | 145.78 KB | 145.96 KB | 10.43 | yes |
| E4/100 | compile | TelusCel | 14.96 us | 14.98 us | 21.24 KB | 21.42 KB | 1.59 | yes |
| E4/100 | compile | GreatPartsCel | 9.83 us | 9.85 us | 19.08 KB | 19.26 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 9.43 us | 9.45 us | 18.67 KB | 18.85 KB | 1.00 |  |
| E4/100 | compile | Celly | - | - | - | - | excluded | |
| E4/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E4/100 | evaluate | Cel.NET | 60.48 us | - | 92.62 KB | - | 3.24 | yes |
| E4/100 | evaluate | TelusCel | 848.36 us | - | 1.15 MB | - | 45.44 | yes |
| E4/100 | evaluate | GreatPartsCel | 19.78 us | - | 9.60 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 18.67 us | - | 9.60 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 22.50 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | evaluate | Celly | - | - | - | - | excluded | |
| E4/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E4/100 | full cycle | Cel.NET | 162.81 us | 162.83 us | 238.40 KB | 238.58 KB | 5.65 | yes |
| E4/100 | full cycle | TelusCel | 861.60 us | 861.62 us | 1.17 MB | 1.17 MB | 29.91 | yes |
| E4/100 | full cycle | GreatPartsCel | 30.18 us | 30.20 us | 28.69 KB | 28.87 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 28.81 us | 28.83 us | 28.27 KB | 28.45 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 31.53 us | 31.55 us | 28.33 KB | 28.51 KB | 1.00 |  |
| E4/100 | full cycle | Celly | - | - | - | - | excluded | |
| E4/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E5 | text creation | - | - | 18.0 ns | - | 168 B | - | |
| E5 | compile | Cel.NET | 15.18 us | 15.20 us | 32.81 KB | 32.98 KB | 6.92 | yes |
| E5 | compile | TelusCel | 5.47 us | 5.49 us | 8.63 KB | 8.79 KB | 2.50 | yes |
| E5 | compile | GreatPartsCel | 2.54 us | 2.56 us | 6.53 KB | 6.70 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.19 us | 2.21 us | 5.59 KB | 5.76 KB | 1.00 |  |
| E5 | compile | Celly | - | - | - | - | excluded | |
| E5 | compile | DotnetCel | - | - | - | - | excluded | |
| E5 | evaluate | Cel.NET | 67.00 us | - | 252.48 KB | - | 207.26 | yes |
| E5 | evaluate | TelusCel | 9.03 us | - | 14.50 KB | - | 27.95 | yes |
| E5 | evaluate | GreatPartsCel | 418.1 ns | - | 320 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 323.2 ns | - | 320 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 519.3 ns | - | 160 B | - | 1.00 |  |
| E5 | evaluate | Celly | - | - | - | - | excluded | |
| E5 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E5 | full cycle | Cel.NET | 86.40 us | 86.42 us | 285.28 KB | 285.45 KB | 33.06 | yes |
| E5 | full cycle | TelusCel | 15.03 us | 15.05 us | 23.13 KB | 23.29 KB | 5.75 | yes |
| E5 | full cycle | GreatPartsCel | 3.22 us | 3.24 us | 6.84 KB | 7.01 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.61 us | 2.63 us | 5.91 KB | 6.07 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 3.33 us | 3.35 us | 6.01 KB | 6.18 KB | 1.00 |  |
| E5 | full cycle | Celly | - | - | - | - | excluded | |
| E5 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E6 | text creation | - | - | 19.1 ns | - | 184 B | - | |
| E6 | compile | Cel.NET | 32.98 us | 33.00 us | 55.60 KB | 55.78 KB | 10.41 | yes |
| E6 | compile | TelusCel | 9.85 us | 9.87 us | 13.95 KB | 14.13 KB | 3.11 | yes |
| E6 | compile | GreatPartsCel | 4.54 us | 4.56 us | 9.30 KB | 9.48 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.17 us | 3.19 us | 6.55 KB | 6.73 KB | 1.00 |  |
| E6 | compile | Celly | - | - | - | - | excluded | |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Cel.NET | 638.8 ns | - | 1.58 KB | - | 1.87 | yes |
| E6 | evaluate | TelusCel | 12.12 us | - | 18.42 KB | - | 35.53 | yes |
| E6 | evaluate | GreatPartsCel | 383.3 ns | - | 320 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 341.1 ns | - | 320 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 539.8 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | Celly | - | - | - | - | excluded | |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Cel.NET | 35.45 us | 35.47 us | 57.17 KB | 57.35 KB | 9.46 | yes |
| E6 | full cycle | TelusCel | 23.22 us | 23.24 us | 32.38 KB | 32.55 KB | 6.20 | yes |
| E6 | full cycle | GreatPartsCel | 4.97 us | 4.98 us | 9.62 KB | 9.80 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 3.75 us | 3.77 us | 6.87 KB | 7.04 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 4.52 us | 4.54 us | 7.02 KB | 7.20 KB | 1.00 |  |
| E6 | full cycle | Celly | - | - | - | - | excluded | |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 16.8 ns | - | 128 B | - | |
| E7 | compile | Cel.NET | 31.55 us | 31.56 us | 58.25 KB | 58.37 KB | 9.37 | yes |
| E7 | compile | GreatPartsCel | 3.54 us | 3.56 us | 7.68 KB | 7.80 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 3.37 us | 3.38 us | 7.24 KB | 7.37 KB | 1.00 |  |
| E7 | compile | Celly | - | - | - | - | excluded | |
| E7 | compile | DotnetCel | - | - | - | - | excluded | |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Cel.NET | 736.1 ns | - | 1.63 KB | - | 2.52 | yes |
| E7 | evaluate | GreatPartsCel | 386.6 ns | - | 320 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 292.6 ns | - | 320 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 494.9 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | Celly | - | - | - | - | excluded | |
| E7 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Cel.NET | 33.98 us | 34.00 us | 59.87 KB | 60.00 KB | 9.05 | yes |
| E7 | full cycle | GreatPartsCel | 4.03 us | 4.05 us | 7.99 KB | 8.12 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 3.76 us | 3.77 us | 7.55 KB | 7.68 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 4.49 us | 4.51 us | 7.53 KB | 7.65 KB | 1.00 |  |
| E7 | full cycle | Celly | - | - | - | - | excluded | |
| E7 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 19.3 ns | - | 192 B | - | |
| E8 | compile | Cel.NET | 74.09 us | 74.11 us | 110.92 KB | 111.10 KB | 15.24 | yes |
| E8 | compile | TelusCel | 13.28 us | 13.30 us | 19.38 KB | 19.56 KB | 2.73 | yes |
| E8 | compile | GreatPartsCel | 5.36 us | 5.38 us | 12.74 KB | 12.93 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 4.86 us | 4.88 us | 11.09 KB | 11.28 KB | 1.00 |  |
| E8 | compile | Celly | - | - | - | - | excluded | |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Cel.NET | 626.7 ns | - | 1.63 KB | - | 1.60 | yes |
| E8 | evaluate | TelusCel | 10.63 us | - | 15.89 KB | - | 27.19 | yes |
| E8 | evaluate | GreatPartsCel | 507.2 ns | - | 368 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 390.9 ns | - | 368 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 583.2 ns | - | 160 B | - | 1.00 |  |
| E8 | evaluate | Celly | - | - | - | - | excluded | |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Cel.NET | 76.20 us | 76.22 us | 112.55 KB | 112.74 KB | 13.64 | yes |
| E8 | full cycle | TelusCel | 24.80 us | 24.82 us | 35.27 KB | 35.45 KB | 4.44 | yes |
| E8 | full cycle | GreatPartsCel | 6.17 us | 6.19 us | 13.10 KB | 13.29 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 5.59 us | 5.60 us | 11.45 KB | 11.64 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 6.22 us | 6.24 us | 11.52 KB | 11.71 KB | 1.00 |  |
| E8 | full cycle | Celly | - | - | - | - | excluded | |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 17.1 ns | - | 136 B | - | |
| E9 | compile | Cel.NET | 50.06 us | 50.08 us | 83.86 KB | 83.99 KB | 13.53 | yes |
| E9 | compile | TelusCel | 6.92 us | 6.93 us | 10.06 KB | 10.20 KB | 1.87 | yes |
| E9 | compile | GreatPartsCel | 4.39 us | 4.41 us | 10.12 KB | 10.25 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 3.70 us | 3.72 us | 8.54 KB | 8.68 KB | 1.00 |  |
| E9 | compile | Celly | - | - | - | - | excluded | |
| E9 | compile | DotnetCel | - | - | - | - | excluded | |
| E9 | evaluate | Cel.NET | 578.1 ns | - | 1.73 KB | - | 1.66 | yes |
| E9 | evaluate | TelusCel | 6.65 us | - | 10.99 KB | - | 19.14 | yes |
| E9 | evaluate | GreatPartsCel | 433.4 ns | - | 504 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 347.4 ns | - | 504 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 567.0 ns | - | 344 B | - | 1.00 |  |
| E9 | evaluate | Celly | - | - | - | - | excluded | |
| E9 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E9 | full cycle | Cel.NET | 50.21 us | 50.22 us | 85.60 KB | 85.73 KB | 12.01 | yes |
| E9 | full cycle | TelusCel | 13.91 us | 13.93 us | 21.05 KB | 21.19 KB | 3.33 | yes |
| E9 | full cycle | GreatPartsCel | 4.80 us | 4.81 us | 10.61 KB | 10.74 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.18 us | 4.20 us | 9.04 KB | 9.17 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 4.66 us | 4.68 us | 9.17 KB | 9.30 KB | 1.00 |  |
| E9 | full cycle | Celly | - | - | - | - | excluded | |
| E9 | full cycle | DotnetCel | - | - | - | - | excluded | |
