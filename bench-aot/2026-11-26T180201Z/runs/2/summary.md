## Run 2: scenario x operation

- Run 2: 2026-11-26T16:03:21.575Z - 2026-11-26T17:03:07.610Z; job: Default; runtime: Native AOT; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 18.8 ns | - | 160 B | - | |
| E1 | compile | Cel.NET | 30.06 us | 30.08 us | 54.62 KB | 54.77 KB | 9.91 | yes |
| E1 | compile | TelusCel | 7.05 us | 7.07 us | 10.31 KB | 10.47 KB | 2.32 | yes |
| E1 | compile | GreatPartsCel | 3.90 us | 3.92 us | 8.96 KB | 9.12 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.03 us | 3.05 us | 7.09 KB | 7.25 KB | 1.00 |  |
| E1 | compile | Celly | - | - | - | - | excluded | |
| E1 | compile | DotnetCel | - | - | - | - | excluded | |
| E1 | evaluate | Cel.NET | 507.2 ns | - | 1.51 KB | - | 1.62 | yes |
| E1 | evaluate | TelusCel | 6.79 us | - | 11.13 KB | - | 21.72 | yes |
| E1 | evaluate | GreatPartsCel | 384.9 ns | - | 320 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 312.6 ns | - | 320 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 533.6 ns | - | 160 B | - | 1.05 |  |
| E1 | evaluate | Celly | - | - | - | - | excluded | |
| E1 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E1 | full cycle | Cel.NET | 30.04 us | 30.06 us | 56.12 KB | 56.28 KB | 8.19 | yes |
| E1 | full cycle | TelusCel | 14.52 us | 14.54 us | 21.44 KB | 21.59 KB | 3.96 | yes |
| E1 | full cycle | GreatPartsCel | 4.47 us | 4.49 us | 9.28 KB | 9.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 3.67 us | 3.69 us | 7.40 KB | 7.56 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 4.09 us | 4.11 us | 7.53 KB | 7.68 KB | 1.00 |  |
| E1 | full cycle | Celly | - | - | - | - | excluded | |
| E1 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E2/10 | text creation | - | - | 18.6 ns | - | 152 B | - | |
| E2/10 | compile | Cel.NET | 35.94 us | 35.96 us | 78.12 KB | 78.27 KB | 6.29 | yes |
| E2/10 | compile | TelusCel | 10.85 us | 10.87 us | 15.48 KB | 15.63 KB | 1.90 | yes |
| E2/10 | compile | GreatPartsCel | 5.72 us | 5.74 us | 12.38 KB | 12.53 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 5.84 us | 5.86 us | 11.73 KB | 11.88 KB | 1.00 |  |
| E2/10 | compile | Celly | - | - | - | - | excluded | |
| E2/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E2/10 | evaluate | Cel.NET | 4.58 us | - | 5.83 KB | - | 1.89 | yes |
| E2/10 | evaluate | TelusCel | 73.71 us | - | 111.01 KB | - | 30.38 | yes |
| E2/10 | evaluate | GreatPartsCel | 2.50 us | - | 368 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 2.43 us | - | 368 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 2.74 us | - | 160 B | - | 1.00 |  |
| E2/10 | evaluate | Celly | - | - | - | - | excluded | |
| E2/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E2/10 | full cycle | Cel.NET | 41.32 us | 41.34 us | 83.95 KB | 84.10 KB | 4.82 | yes |
| E2/10 | full cycle | TelusCel | 87.03 us | 87.05 us | 126.49 KB | 126.64 KB | 10.14 | yes |
| E2/10 | full cycle | GreatPartsCel | 8.64 us | 8.66 us | 12.74 KB | 12.89 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 8.58 us | 8.60 us | 12.08 KB | 12.23 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 8.89 us | 8.91 us | 12.14 KB | 12.29 KB | 1.00 |  |
| E2/10 | full cycle | Celly | - | - | - | - | excluded | |
| E2/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E2/100 | text creation | - | - | 18.1 ns | - | 152 B | - | |
| E2/100 | compile | Cel.NET | 35.86 us | 35.88 us | 78.12 KB | 78.27 KB | 6.19 | yes |
| E2/100 | compile | TelusCel | 11.01 us | 11.03 us | 15.48 KB | 15.63 KB | 1.90 | yes |
| E2/100 | compile | GreatPartsCel | 5.82 us | 5.83 us | 12.38 KB | 12.53 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 5.79 us | 5.81 us | 11.72 KB | 11.87 KB | 1.00 |  |
| E2/100 | compile | Celly | - | - | - | - | excluded | |
| E2/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E2/100 | evaluate | Cel.NET | 45.02 us | - | 51.15 KB | - | 2.04 | yes |
| E2/100 | evaluate | TelusCel | 768.51 us | - | 1.03 MB | - | 34.83 | yes |
| E2/100 | evaluate | GreatPartsCel | 22.06 us | - | 368 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 22.56 us | - | 368 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 23.86 us | - | 160 B | - | 1.00 |  |
| E2/100 | evaluate | Celly | - | - | - | - | excluded | |
| E2/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E2/100 | full cycle | Cel.NET | 79.49 us | 79.51 us | 129.26 KB | 129.41 KB | 2.80 | yes |
| E2/100 | full cycle | TelusCel | 799.97 us | 799.99 us | 1.04 MB | 1.04 MB | 28.23 | yes |
| E2/100 | full cycle | GreatPartsCel | 28.40 us | 28.42 us | 12.74 KB | 12.89 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 28.34 us | 28.36 us | 12.08 KB | 12.23 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 30.76 us | 30.78 us | 12.14 KB | 12.29 KB | 1.00 |  |
| E2/100 | full cycle | Celly | - | - | - | - | excluded | |
| E2/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E3/10 | text creation | - | - | 17.8 ns | - | 152 B | - | |
| E3/10 | compile | Cel.NET | 27.08 us | 27.10 us | 57.47 KB | 57.62 KB | 6.04 | yes |
| E3/10 | compile | TelusCel | 8.68 us | 8.70 us | 13.36 KB | 13.51 KB | 1.94 | yes |
| E3/10 | compile | GreatPartsCel | 4.48 us | 4.50 us | 11.00 KB | 11.15 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 4.61 us | 4.63 us | 10.51 KB | 10.66 KB | 1.00 |  |
| E3/10 | compile | Celly | - | - | - | - | excluded | |
| E3/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E3/10 | evaluate | Cel.NET | 1.02 us | - | 2.02 KB | - | 2.20 | yes |
| E3/10 | evaluate | TelusCel | 42.94 us | - | 64.45 KB | - | 92.54 | yes |
| E3/10 | evaluate | GreatPartsCel | 551.5 ns | - | 368 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 464.0 ns | - | 368 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 662.8 ns | - | 160 B | - | 1.00 |  |
| E3/10 | evaluate | Celly | - | - | - | - | excluded | |
| E3/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E3/10 | full cycle | Cel.NET | 29.74 us | 29.76 us | 59.51 KB | 59.65 KB | 5.71 | yes |
| E3/10 | full cycle | TelusCel | 53.48 us | 53.49 us | 77.81 KB | 77.96 KB | 10.27 | yes |
| E3/10 | full cycle | GreatPartsCel | 5.44 us | 5.45 us | 11.36 KB | 11.50 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 5.21 us | 5.23 us | 10.87 KB | 11.02 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 5.46 us | 5.48 us | 10.93 KB | 11.07 KB | 1.00 |  |
| E3/10 | full cycle | Celly | - | - | - | - | excluded | |
| E3/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E3/100 | text creation | - | - | 17.9 ns | - | 152 B | - | |
| E3/100 | compile | Cel.NET | 28.95 us | 28.97 us | 57.47 KB | 57.62 KB | 6.32 | yes |
| E3/100 | compile | TelusCel | 8.94 us | 8.95 us | 13.36 KB | 13.51 KB | 1.95 | yes |
| E3/100 | compile | GreatPartsCel | 4.58 us | 4.60 us | 11.00 KB | 11.15 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 4.67 us | 4.69 us | 10.51 KB | 10.66 KB | 1.00 |  |
| E3/100 | compile | Celly | - | - | - | - | excluded | |
| E3/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E3/100 | evaluate | Cel.NET | 2.20 us | - | 8.77 KB | - | 4.81 | yes |
| E3/100 | evaluate | TelusCel | 426.29 us | - | 585.88 KB | - | 929.77 | yes |
| E3/100 | evaluate | GreatPartsCel | 522.7 ns | - | 368 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 458.5 ns | - | 368 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 657.2 ns | - | 160 B | - | 1.00 |  |
| E3/100 | evaluate | Celly | - | - | - | - | excluded | |
| E3/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E3/100 | full cycle | Cel.NET | 31.30 us | 31.32 us | 66.23 KB | 66.38 KB | 6.16 | yes |
| E3/100 | full cycle | TelusCel | 421.98 us | 422.00 us | 599.24 KB | 599.39 KB | 83.07 | yes |
| E3/100 | full cycle | GreatPartsCel | 5.19 us | 5.21 us | 11.36 KB | 11.51 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 5.08 us | 5.10 us | 10.86 KB | 11.01 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 5.87 us | 5.89 us | 10.94 KB | 11.08 KB | 1.00 |  |
| E3/100 | full cycle | Celly | - | - | - | - | excluded | |
| E3/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E4/10 | text creation | - | - | 19.6 ns | - | 184 B | - | |
| E4/10 | compile | Cel.NET | 97.50 us | 97.51 us | 145.78 KB | 145.96 KB | 10.43 | yes |
| E4/10 | compile | TelusCel | 15.16 us | 15.18 us | 21.24 KB | 21.42 KB | 1.62 | yes |
| E4/10 | compile | GreatPartsCel | 9.35 us | 9.37 us | 19.08 KB | 19.26 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 9.85 us | 9.87 us | 18.67 KB | 18.85 KB | 1.00 |  |
| E4/10 | compile | Celly | - | - | - | - | excluded | |
| E4/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E4/10 | evaluate | Cel.NET | 5.36 us | - | 6.74 KB | - | 3.09 | yes |
| E4/10 | evaluate | TelusCel | 72.26 us | - | 99.07 KB | - | 41.61 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.81 us | - | 872 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.74 us | - | 872 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 2.03 us | - | 664 B | - | 1.00 |  |
| E4/10 | evaluate | Celly | - | - | - | - | excluded | |
| E4/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E4/10 | full cycle | Cel.NET | 108.30 us | 108.32 us | 152.52 KB | 152.70 KB | 9.60 | yes |
| E4/10 | full cycle | TelusCel | 86.64 us | 86.66 us | 120.31 KB | 120.49 KB | 7.68 | yes |
| E4/10 | full cycle | GreatPartsCel | 11.89 us | 11.91 us | 19.93 KB | 20.11 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 11.29 us | 11.30 us | 19.52 KB | 19.70 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 12.73 us | 12.75 us | 19.58 KB | 19.76 KB | 1.00 |  |
| E4/10 | full cycle | Celly | - | - | - | - | excluded | |
| E4/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E4/100 | text creation | - | - | 19.5 ns | - | 184 B | - | |
| E4/100 | compile | Cel.NET | 95.65 us | 95.67 us | 145.78 KB | 145.96 KB | 10.36 | yes |
| E4/100 | compile | TelusCel | 15.20 us | 15.22 us | 21.24 KB | 21.42 KB | 1.65 | yes |
| E4/100 | compile | GreatPartsCel | 9.62 us | 9.64 us | 19.08 KB | 19.26 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 9.23 us | 9.25 us | 18.67 KB | 18.85 KB | 1.00 |  |
| E4/100 | compile | Celly | - | - | - | - | excluded | |
| E4/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E4/100 | evaluate | Cel.NET | 60.83 us | - | 92.62 KB | - | 3.29 | yes |
| E4/100 | evaluate | TelusCel | 827.01 us | - | 1.15 MB | - | 44.68 | yes |
| E4/100 | evaluate | GreatPartsCel | 20.81 us | - | 9.60 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 18.51 us | - | 9.60 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 20.39 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | evaluate | Celly | - | - | - | - | excluded | |
| E4/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E4/100 | full cycle | Cel.NET | 159.63 us | 159.64 us | 238.42 KB | 238.60 KB | 5.62 | yes |
| E4/100 | full cycle | TelusCel | 853.19 us | 853.21 us | 1.17 MB | 1.17 MB | 30.02 | yes |
| E4/100 | full cycle | GreatPartsCel | 29.08 us | 29.10 us | 28.69 KB | 28.87 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 28.42 us | 28.44 us | 28.27 KB | 28.45 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 30.82 us | 30.84 us | 28.33 KB | 28.51 KB | 1.00 |  |
| E4/100 | full cycle | Celly | - | - | - | - | excluded | |
| E4/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E5 | text creation | - | - | 19.0 ns | - | 168 B | - | |
| E5 | compile | Cel.NET | 15.30 us | 15.32 us | 32.82 KB | 32.98 KB | 7.38 | yes |
| E5 | compile | TelusCel | 5.57 us | 5.59 us | 8.63 KB | 8.79 KB | 2.69 | yes |
| E5 | compile | GreatPartsCel | 2.50 us | 2.52 us | 6.53 KB | 6.69 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.07 us | 2.09 us | 5.59 KB | 5.76 KB | 1.00 |  |
| E5 | compile | Celly | - | - | - | - | excluded | |
| E5 | compile | DotnetCel | - | - | - | - | excluded | |
| E5 | evaluate | Cel.NET | 67.51 us | - | 252.48 KB | - | 226.52 | yes |
| E5 | evaluate | TelusCel | 8.78 us | - | 14.50 KB | - | 29.46 | yes |
| E5 | evaluate | GreatPartsCel | 409.9 ns | - | 320 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 298.0 ns | - | 320 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 516.9 ns | - | 160 B | - | 1.00 |  |
| E5 | evaluate | Celly | - | - | - | - | excluded | |
| E5 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E5 | full cycle | Cel.NET | 86.10 us | 86.12 us | 285.29 KB | 285.45 KB | 33.94 | yes |
| E5 | full cycle | TelusCel | 15.13 us | 15.15 us | 23.13 KB | 23.29 KB | 5.97 | yes |
| E5 | full cycle | GreatPartsCel | 3.11 us | 3.13 us | 6.84 KB | 7.01 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.54 us | 2.56 us | 5.90 KB | 6.07 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 3.31 us | 3.33 us | 6.01 KB | 6.18 KB | 1.00 |  |
| E5 | full cycle | Celly | - | - | - | - | excluded | |
| E5 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E6 | text creation | - | - | 19.6 ns | - | 184 B | - | |
| E6 | compile | Cel.NET | 32.27 us | 32.29 us | 55.59 KB | 55.77 KB | 10.15 | yes |
| E6 | compile | TelusCel | 10.02 us | 10.04 us | 13.95 KB | 14.13 KB | 3.15 | yes |
| E6 | compile | GreatPartsCel | 4.40 us | 4.42 us | 9.30 KB | 9.48 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.18 us | 3.20 us | 6.55 KB | 6.73 KB | 1.00 |  |
| E6 | compile | Celly | - | - | - | - | excluded | |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Cel.NET | 601.0 ns | - | 1.58 KB | - | 1.80 | yes |
| E6 | evaluate | TelusCel | 12.43 us | - | 18.42 KB | - | 37.15 | yes |
| E6 | evaluate | GreatPartsCel | 413.0 ns | - | 320 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 334.4 ns | - | 320 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 527.6 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | Celly | - | - | - | - | excluded | |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Cel.NET | 35.09 us | 35.11 us | 57.17 KB | 57.35 KB | 9.57 | yes |
| E6 | full cycle | TelusCel | 23.93 us | 23.95 us | 32.38 KB | 32.55 KB | 6.53 | yes |
| E6 | full cycle | GreatPartsCel | 4.85 us | 4.86 us | 9.62 KB | 9.80 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 3.67 us | 3.69 us | 6.87 KB | 7.04 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 4.32 us | 4.34 us | 7.02 KB | 7.20 KB | 1.00 |  |
| E6 | full cycle | Celly | - | - | - | - | excluded | |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 17.0 ns | - | 128 B | - | |
| E7 | compile | Cel.NET | 31.75 us | 31.76 us | 58.25 KB | 58.37 KB | 9.94 | yes |
| E7 | compile | GreatPartsCel | 3.57 us | 3.59 us | 7.68 KB | 7.80 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 3.19 us | 3.21 us | 7.24 KB | 7.37 KB | 1.00 |  |
| E7 | compile | Celly | - | - | - | - | excluded | |
| E7 | compile | DotnetCel | - | - | - | - | excluded | |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Cel.NET | 727.5 ns | - | 1.63 KB | - | 2.58 | yes |
| E7 | evaluate | GreatPartsCel | 389.9 ns | - | 320 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 281.7 ns | - | 320 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 489.2 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | Celly | - | - | - | - | excluded | |
| E7 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Cel.NET | 33.57 us | 33.58 us | 59.87 KB | 60.00 KB | 9.43 | yes |
| E7 | full cycle | GreatPartsCel | 4.20 us | 4.22 us | 7.99 KB | 8.12 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 3.56 us | 3.58 us | 7.55 KB | 7.68 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 4.24 us | 4.26 us | 7.53 KB | 7.66 KB | 1.00 |  |
| E7 | full cycle | Celly | - | - | - | - | excluded | |
| E7 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 20.0 ns | - | 192 B | - | |
| E8 | compile | Cel.NET | 73.07 us | 73.09 us | 110.91 KB | 111.10 KB | 14.75 | yes |
| E8 | compile | TelusCel | 13.38 us | 13.40 us | 19.38 KB | 19.56 KB | 2.70 | yes |
| E8 | compile | GreatPartsCel | 5.65 us | 5.67 us | 12.74 KB | 12.93 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 4.96 us | 4.98 us | 11.09 KB | 11.27 KB | 1.00 |  |
| E8 | compile | Celly | - | - | - | - | excluded | |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Cel.NET | 590.0 ns | - | 1.63 KB | - | 1.60 | yes |
| E8 | evaluate | TelusCel | 10.60 us | - | 15.89 KB | - | 28.73 | yes |
| E8 | evaluate | GreatPartsCel | 468.7 ns | - | 368 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 368.9 ns | - | 368 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 604.6 ns | - | 160 B | - | 1.02 |  |
| E8 | evaluate | Celly | - | - | - | - | excluded | |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Cel.NET | 72.72 us | 72.74 us | 112.54 KB | 112.72 KB | 13.02 | yes |
| E8 | full cycle | TelusCel | 25.77 us | 25.79 us | 35.27 KB | 35.45 KB | 4.61 | yes |
| E8 | full cycle | GreatPartsCel | 6.17 us | 6.19 us | 13.10 KB | 13.29 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 5.59 us | 5.61 us | 11.45 KB | 11.64 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 6.03 us | 6.05 us | 11.53 KB | 11.72 KB | 1.00 |  |
| E8 | full cycle | Celly | - | - | - | - | excluded | |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 17.8 ns | - | 136 B | - | |
| E9 | compile | Cel.NET | 47.19 us | 47.21 us | 83.86 KB | 83.99 KB | 12.74 | yes |
| E9 | compile | TelusCel | 6.91 us | 6.93 us | 10.06 KB | 10.20 KB | 1.87 | yes |
| E9 | compile | GreatPartsCel | 4.19 us | 4.21 us | 10.11 KB | 10.25 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 3.70 us | 3.72 us | 8.54 KB | 8.68 KB | 1.00 |  |
| E9 | compile | Celly | - | - | - | - | excluded | |
| E9 | compile | DotnetCel | - | - | - | - | excluded | |
| E9 | evaluate | Cel.NET | 564.5 ns | - | 1.73 KB | - | 1.54 | yes |
| E9 | evaluate | TelusCel | 6.81 us | - | 10.99 KB | - | 18.58 | yes |
| E9 | evaluate | GreatPartsCel | 426.6 ns | - | 504 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 366.5 ns | - | 504 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 559.5 ns | - | 344 B | - | 1.00 |  |
| E9 | evaluate | Celly | - | - | - | - | excluded | |
| E9 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E9 | full cycle | Cel.NET | 47.66 us | 47.67 us | 85.59 KB | 85.72 KB | 11.31 | yes |
| E9 | full cycle | TelusCel | 14.36 us | 14.38 us | 21.05 KB | 21.19 KB | 3.41 | yes |
| E9 | full cycle | GreatPartsCel | 4.88 us | 4.90 us | 10.61 KB | 10.74 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.22 us | 4.23 us | 9.03 KB | 9.16 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 4.74 us | 4.76 us | 9.16 KB | 9.29 KB | 1.00 |  |
| E9 | full cycle | Celly | - | - | - | - | excluded | |
| E9 | full cycle | DotnetCel | - | - | - | - | excluded | |
