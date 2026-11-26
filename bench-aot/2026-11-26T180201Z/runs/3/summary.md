## Run 3: scenario x operation

- Run 3: 2026-11-26T17:03:07.610Z - 2026-11-26T18:02:01.242Z; job: Default; runtime: Native AOT; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 17.8 ns | - | 160 B | - | |
| E1 | compile | Cel.NET | 28.78 us | 28.80 us | 54.62 KB | 54.77 KB | 8.90 | yes |
| E1 | compile | TelusCel | 7.02 us | 7.04 us | 10.31 KB | 10.47 KB | 2.17 | yes |
| E1 | compile | GreatPartsCel | 3.87 us | 3.89 us | 8.96 KB | 9.12 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.23 us | 3.25 us | 7.09 KB | 7.25 KB | 1.00 |  |
| E1 | compile | Celly | - | - | - | - | excluded | |
| E1 | compile | DotnetCel | - | - | - | - | excluded | |
| E1 | evaluate | Cel.NET | 499.9 ns | - | 1.51 KB | - | 1.56 | yes |
| E1 | evaluate | TelusCel | 6.77 us | - | 11.13 KB | - | 21.18 | yes |
| E1 | evaluate | GreatPartsCel | 378.7 ns | - | 320 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 319.7 ns | - | 320 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 537.7 ns | - | 160 B | - | 1.08 |  |
| E1 | evaluate | Celly | - | - | - | - | excluded | |
| E1 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E1 | full cycle | Cel.NET | 29.37 us | 29.39 us | 56.13 KB | 56.28 KB | 7.80 | yes |
| E1 | full cycle | TelusCel | 14.61 us | 14.63 us | 21.44 KB | 21.59 KB | 3.88 | yes |
| E1 | full cycle | GreatPartsCel | 4.36 us | 4.37 us | 9.28 KB | 9.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 3.77 us | 3.78 us | 7.41 KB | 7.56 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 4.31 us | 4.33 us | 7.53 KB | 7.69 KB | 1.00 |  |
| E1 | full cycle | Celly | - | - | - | - | excluded | |
| E1 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E2/10 | text creation | - | - | 17.4 ns | - | 152 B | - | |
| E2/10 | compile | Cel.NET | 36.07 us | 36.09 us | 78.12 KB | 78.27 KB | 6.37 | yes |
| E2/10 | compile | TelusCel | 10.90 us | 10.92 us | 15.48 KB | 15.63 KB | 1.92 | yes |
| E2/10 | compile | GreatPartsCel | 5.67 us | 5.68 us | 12.38 KB | 12.53 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 5.81 us | 5.83 us | 11.73 KB | 11.88 KB | 1.00 |  |
| E2/10 | compile | Celly | - | - | - | - | excluded | |
| E2/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E2/10 | evaluate | Cel.NET | 4.82 us | - | 5.83 KB | - | 2.00 | yes |
| E2/10 | evaluate | TelusCel | 79.30 us | - | 111.01 KB | - | 32.88 | yes |
| E2/10 | evaluate | GreatPartsCel | 2.54 us | - | 368 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 2.41 us | - | 368 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 2.70 us | - | 160 B | - | 1.00 |  |
| E2/10 | evaluate | Celly | - | - | - | - | excluded | |
| E2/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E2/10 | full cycle | Cel.NET | 42.45 us | 42.47 us | 83.95 KB | 84.10 KB | 5.02 | yes |
| E2/10 | full cycle | TelusCel | 87.51 us | 87.52 us | 126.49 KB | 126.64 KB | 10.34 | yes |
| E2/10 | full cycle | GreatPartsCel | 8.62 us | 8.63 us | 12.74 KB | 12.89 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 8.46 us | 8.48 us | 12.08 KB | 12.23 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 9.01 us | 9.03 us | 12.14 KB | 12.29 KB | 1.00 |  |
| E2/10 | full cycle | Celly | - | - | - | - | excluded | |
| E2/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E2/100 | text creation | - | - | 17.5 ns | - | 152 B | - | |
| E2/100 | compile | Cel.NET | 37.99 us | 38.00 us | 78.11 KB | 78.26 KB | 6.79 | yes |
| E2/100 | compile | TelusCel | 10.96 us | 10.98 us | 15.48 KB | 15.63 KB | 1.96 | yes |
| E2/100 | compile | GreatPartsCel | 5.59 us | 5.61 us | 12.38 KB | 12.53 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 5.73 us | 5.75 us | 11.72 KB | 11.87 KB | 1.00 |  |
| E2/100 | compile | Celly | - | - | - | - | excluded | |
| E2/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E2/100 | evaluate | Cel.NET | 43.65 us | - | 51.15 KB | - | 1.99 | yes |
| E2/100 | evaluate | TelusCel | 746.17 us | - | 1.03 MB | - | 33.95 | yes |
| E2/100 | evaluate | GreatPartsCel | 22.10 us | - | 368 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 21.98 us | - | 368 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 23.19 us | - | 160 B | - | 1.00 |  |
| E2/100 | evaluate | Celly | - | - | - | - | excluded | |
| E2/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E2/100 | full cycle | Cel.NET | 80.33 us | 80.35 us | 129.26 KB | 129.41 KB | 2.86 | yes |
| E2/100 | full cycle | TelusCel | 693.31 us | 693.33 us | 1.04 MB | 1.04 MB | 24.71 | yes |
| E2/100 | full cycle | GreatPartsCel | 28.06 us | 28.07 us | 12.74 KB | 12.89 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 28.08 us | 28.10 us | 12.08 KB | 12.23 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 29.89 us | 29.91 us | 12.15 KB | 12.29 KB | 1.00 |  |
| E2/100 | full cycle | Celly | - | - | - | - | excluded | |
| E2/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E3/10 | text creation | - | - | 17.5 ns | - | 152 B | - | |
| E3/10 | compile | Cel.NET | 26.93 us | 26.95 us | 57.47 KB | 57.62 KB | 5.99 | yes |
| E3/10 | compile | TelusCel | 8.62 us | 8.64 us | 13.36 KB | 13.51 KB | 1.92 | yes |
| E3/10 | compile | GreatPartsCel | 4.50 us | 4.51 us | 11.00 KB | 11.15 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 4.73 us | 4.75 us | 10.51 KB | 10.66 KB | 1.00 |  |
| E3/10 | compile | Celly | - | - | - | - | excluded | |
| E3/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E3/10 | evaluate | Cel.NET | 1.00 us | - | 2.02 KB | - | 2.19 | yes |
| E3/10 | evaluate | TelusCel | 41.64 us | - | 64.45 KB | - | 90.69 | yes |
| E3/10 | evaluate | GreatPartsCel | 525.3 ns | - | 368 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 459.2 ns | - | 368 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 649.7 ns | - | 160 B | - | 1.00 |  |
| E3/10 | evaluate | Celly | - | - | - | - | excluded | |
| E3/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E3/10 | full cycle | Cel.NET | 29.03 us | 29.05 us | 59.49 KB | 59.64 KB | 5.58 | yes |
| E3/10 | full cycle | TelusCel | 50.67 us | 50.69 us | 77.81 KB | 77.96 KB | 9.74 | yes |
| E3/10 | full cycle | GreatPartsCel | 5.20 us | 5.22 us | 11.36 KB | 11.50 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 5.31 us | 5.32 us | 10.87 KB | 11.02 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 5.74 us | 5.75 us | 10.93 KB | 11.08 KB | 1.00 |  |
| E3/10 | full cycle | Celly | - | - | - | - | excluded | |
| E3/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E3/100 | text creation | - | - | 17.6 ns | - | 152 B | - | |
| E3/100 | compile | Cel.NET | 28.75 us | 28.77 us | 57.47 KB | 57.62 KB | 6.45 | yes |
| E3/100 | compile | TelusCel | 8.50 us | 8.52 us | 13.36 KB | 13.51 KB | 1.91 | yes |
| E3/100 | compile | GreatPartsCel | 4.46 us | 4.48 us | 11.00 KB | 11.14 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 4.68 us | 4.70 us | 10.51 KB | 10.66 KB | 1.00 |  |
| E3/100 | compile | Celly | - | - | - | - | excluded | |
| E3/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E3/100 | evaluate | Cel.NET | 2.28 us | - | 8.77 KB | - | 4.93 | yes |
| E3/100 | evaluate | TelusCel | 377.89 us | - | 585.88 KB | - | 816.30 | yes |
| E3/100 | evaluate | GreatPartsCel | 535.6 ns | - | 368 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 462.9 ns | - | 368 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 660.3 ns | - | 160 B | - | 1.00 |  |
| E3/100 | evaluate | Celly | - | - | - | - | excluded | |
| E3/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E3/100 | full cycle | Cel.NET | 31.67 us | 31.69 us | 66.23 KB | 66.38 KB | 6.11 | yes |
| E3/100 | full cycle | TelusCel | 389.04 us | 389.06 us | 599.24 KB | 599.39 KB | 75.08 | yes |
| E3/100 | full cycle | GreatPartsCel | 5.18 us | 5.20 us | 11.36 KB | 11.50 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 5.30 us | 5.32 us | 10.86 KB | 11.01 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 5.57 us | 5.59 us | 10.93 KB | 11.08 KB | 1.00 |  |
| E3/100 | full cycle | Celly | - | - | - | - | excluded | |
| E3/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E4/10 | text creation | - | - | 19.1 ns | - | 184 B | - | |
| E4/10 | compile | Cel.NET | 95.92 us | 95.94 us | 145.79 KB | 145.97 KB | 10.14 | yes |
| E4/10 | compile | TelusCel | 14.57 us | 14.59 us | 21.24 KB | 21.42 KB | 1.54 | yes |
| E4/10 | compile | GreatPartsCel | 9.46 us | 9.48 us | 19.08 KB | 19.26 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 9.93 us | 9.95 us | 18.67 KB | 18.85 KB | 1.00 |  |
| E4/10 | compile | Celly | - | - | - | - | excluded | |
| E4/10 | compile | DotnetCel | - | - | - | - | excluded | |
| E4/10 | evaluate | Cel.NET | 5.03 us | - | 6.74 KB | - | 2.79 | yes |
| E4/10 | evaluate | TelusCel | 64.16 us | - | 99.07 KB | - | 35.55 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.80 us | - | 872 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.81 us | - | 872 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 2.07 us | - | 664 B | - | 1.00 |  |
| E4/10 | evaluate | Celly | - | - | - | - | excluded | |
| E4/10 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E4/10 | full cycle | Cel.NET | 106.38 us | 106.40 us | 152.52 KB | 152.70 KB | 9.03 | yes |
| E4/10 | full cycle | TelusCel | 79.90 us | 79.92 us | 120.31 KB | 120.49 KB | 6.78 | yes |
| E4/10 | full cycle | GreatPartsCel | 11.78 us | 11.80 us | 19.93 KB | 20.11 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 12.05 us | 12.07 us | 19.52 KB | 19.70 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 11.83 us | 11.85 us | 19.58 KB | 19.76 KB | 1.00 |  |
| E4/10 | full cycle | Celly | - | - | - | - | excluded | |
| E4/10 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E4/100 | text creation | - | - | 19.0 ns | - | 184 B | - | |
| E4/100 | compile | Cel.NET | 98.41 us | 98.43 us | 145.78 KB | 145.96 KB | 10.36 | yes |
| E4/100 | compile | TelusCel | 14.54 us | 14.56 us | 21.24 KB | 21.42 KB | 1.53 | yes |
| E4/100 | compile | GreatPartsCel | 9.49 us | 9.51 us | 19.08 KB | 19.26 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 10.02 us | 10.04 us | 18.67 KB | 18.85 KB | 1.00 |  |
| E4/100 | compile | Celly | - | - | - | - | excluded | |
| E4/100 | compile | DotnetCel | - | - | - | - | excluded | |
| E4/100 | evaluate | Cel.NET | 60.19 us | - | 92.62 KB | - | 3.22 | yes |
| E4/100 | evaluate | TelusCel | 787.80 us | - | 1.15 MB | - | 42.12 | yes |
| E4/100 | evaluate | GreatPartsCel | 19.30 us | - | 9.60 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 18.70 us | - | 9.60 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 19.55 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | evaluate | Celly | - | - | - | - | excluded | |
| E4/100 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E4/100 | full cycle | Cel.NET | 168.39 us | 168.41 us | 238.40 KB | 238.58 KB | 5.74 | yes |
| E4/100 | full cycle | TelusCel | 815.71 us | 815.73 us | 1.17 MB | 1.17 MB | 27.80 | yes |
| E4/100 | full cycle | GreatPartsCel | 29.34 us | 29.36 us | 28.69 KB | 28.87 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 30.09 us | 30.11 us | 28.27 KB | 28.45 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 30.12 us | 30.14 us | 28.33 KB | 28.51 KB | 1.00 |  |
| E4/100 | full cycle | Celly | - | - | - | - | excluded | |
| E4/100 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E5 | text creation | - | - | 18.2 ns | - | 168 B | - | |
| E5 | compile | Cel.NET | 15.96 us | 15.98 us | 32.80 KB | 32.96 KB | 7.03 | yes |
| E5 | compile | TelusCel | 5.31 us | 5.32 us | 8.63 KB | 8.79 KB | 2.34 | yes |
| E5 | compile | GreatPartsCel | 2.48 us | 2.50 us | 6.53 KB | 6.69 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.27 us | 2.29 us | 5.59 KB | 5.76 KB | 1.00 |  |
| E5 | compile | Celly | - | - | - | - | excluded | |
| E5 | compile | DotnetCel | - | - | - | - | excluded | |
| E5 | evaluate | Cel.NET | 71.13 us | - | 252.48 KB | - | 229.42 | yes |
| E5 | evaluate | TelusCel | 8.36 us | - | 14.50 KB | - | 26.97 | yes |
| E5 | evaluate | GreatPartsCel | 412.2 ns | - | 320 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 310.1 ns | - | 320 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 512.7 ns | - | 160 B | - | 1.00 |  |
| E5 | evaluate | Celly | - | - | - | - | excluded | |
| E5 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E5 | full cycle | Cel.NET | 90.94 us | 90.96 us | 285.30 KB | 285.46 KB | 35.21 | yes |
| E5 | full cycle | TelusCel | 14.27 us | 14.28 us | 23.13 KB | 23.29 KB | 5.52 | yes |
| E5 | full cycle | GreatPartsCel | 3.09 us | 3.11 us | 6.84 KB | 7.01 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.58 us | 2.60 us | 5.90 KB | 6.07 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 3.12 us | 3.14 us | 6.01 KB | 6.18 KB | 1.00 |  |
| E5 | full cycle | Celly | - | - | - | - | excluded | |
| E5 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E6 | text creation | - | - | 19.2 ns | - | 184 B | - | |
| E6 | compile | Cel.NET | 34.05 us | 34.07 us | 55.59 KB | 55.77 KB | 10.39 | yes |
| E6 | compile | TelusCel | 9.56 us | 9.58 us | 13.95 KB | 14.13 KB | 2.92 | yes |
| E6 | compile | GreatPartsCel | 4.44 us | 4.46 us | 9.30 KB | 9.48 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.28 us | 3.30 us | 6.55 KB | 6.73 KB | 1.00 |  |
| E6 | compile | Celly | - | - | - | - | excluded | |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Cel.NET | 637.3 ns | - | 1.58 KB | - | 1.89 | yes |
| E6 | evaluate | TelusCel | 12.02 us | - | 18.42 KB | - | 35.68 | yes |
| E6 | evaluate | GreatPartsCel | 375.3 ns | - | 320 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 336.7 ns | - | 320 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 531.3 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | Celly | - | - | - | - | excluded | |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Cel.NET | 34.61 us | 34.63 us | 57.19 KB | 57.37 KB | 9.19 | yes |
| E6 | full cycle | TelusCel | 22.82 us | 22.84 us | 32.38 KB | 32.55 KB | 6.06 | yes |
| E6 | full cycle | GreatPartsCel | 4.75 us | 4.77 us | 9.62 KB | 9.80 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 3.77 us | 3.79 us | 6.87 KB | 7.05 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 4.13 us | 4.15 us | 7.02 KB | 7.20 KB | 1.00 |  |
| E6 | full cycle | Celly | - | - | - | - | excluded | |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 16.6 ns | - | 128 B | - | |
| E7 | compile | Cel.NET | 32.12 us | 32.13 us | 58.26 KB | 58.39 KB | 10.12 | yes |
| E7 | compile | GreatPartsCel | 3.50 us | 3.51 us | 7.68 KB | 7.80 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 3.17 us | 3.19 us | 7.24 KB | 7.37 KB | 1.00 |  |
| E7 | compile | Celly | - | - | - | - | excluded | |
| E7 | compile | DotnetCel | - | - | - | - | excluded | |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Cel.NET | 726.8 ns | - | 1.63 KB | - | 2.57 | yes |
| E7 | evaluate | GreatPartsCel | 381.4 ns | - | 320 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 282.8 ns | - | 320 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 479.9 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | Celly | - | - | - | - | excluded | |
| E7 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Cel.NET | 33.68 us | 33.69 us | 59.88 KB | 60.01 KB | 9.34 | yes |
| E7 | full cycle | GreatPartsCel | 4.29 us | 4.31 us | 7.99 KB | 8.11 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 3.61 us | 3.62 us | 7.55 KB | 7.68 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 4.10 us | 4.12 us | 7.53 KB | 7.65 KB | 1.00 |  |
| E7 | full cycle | Celly | - | - | - | - | excluded | |
| E7 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 19.2 ns | - | 192 B | - | |
| E8 | compile | Cel.NET | 73.35 us | 73.37 us | 110.92 KB | 111.11 KB | 15.52 | yes |
| E8 | compile | TelusCel | 12.63 us | 12.65 us | 19.38 KB | 19.56 KB | 2.67 | yes |
| E8 | compile | GreatPartsCel | 5.62 us | 5.64 us | 12.74 KB | 12.93 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 4.73 us | 4.75 us | 11.09 KB | 11.28 KB | 1.00 |  |
| E8 | compile | Celly | - | - | - | - | excluded | |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Cel.NET | 612.3 ns | - | 1.63 KB | - | 1.69 | yes |
| E8 | evaluate | TelusCel | 9.96 us | - | 15.89 KB | - | 27.49 | yes |
| E8 | evaluate | GreatPartsCel | 474.3 ns | - | 368 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 362.2 ns | - | 368 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 580.5 ns | - | 160 B | - | 1.00 |  |
| E8 | evaluate | Celly | - | - | - | - | excluded | |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Cel.NET | 74.81 us | 74.83 us | 112.53 KB | 112.72 KB | 14.19 | yes |
| E8 | full cycle | TelusCel | 23.96 us | 23.98 us | 35.27 KB | 35.45 KB | 4.54 | yes |
| E8 | full cycle | GreatPartsCel | 6.32 us | 6.34 us | 13.10 KB | 13.29 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 5.27 us | 5.29 us | 11.45 KB | 11.64 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 5.98 us | 6.00 us | 11.54 KB | 11.73 KB | 1.00 |  |
| E8 | full cycle | Celly | - | - | - | - | excluded | |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 16.9 ns | - | 136 B | - | |
| E9 | compile | Cel.NET | 49.09 us | 49.11 us | 83.86 KB | 83.99 KB | 13.83 | yes |
| E9 | compile | TelusCel | 6.79 us | 6.80 us | 10.06 KB | 10.20 KB | 1.91 | yes |
| E9 | compile | GreatPartsCel | 4.60 us | 4.62 us | 10.12 KB | 10.25 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 3.55 us | 3.57 us | 8.54 KB | 8.68 KB | 1.00 |  |
| E9 | compile | Celly | - | - | - | - | excluded | |
| E9 | compile | DotnetCel | - | - | - | - | excluded | |
| E9 | evaluate | Cel.NET | 579.7 ns | - | 1.73 KB | - | 1.68 | yes |
| E9 | evaluate | TelusCel | 7.10 us | - | 10.99 KB | - | 20.57 | yes |
| E9 | evaluate | GreatPartsCel | 439.9 ns | - | 504 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 345.4 ns | - | 504 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 563.3 ns | - | 344 B | - | 1.00 |  |
| E9 | evaluate | Celly | - | - | - | - | excluded | |
| E9 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E9 | full cycle | Cel.NET | 50.11 us | 50.13 us | 85.60 KB | 85.73 KB | 12.27 | yes |
| E9 | full cycle | TelusCel | 13.97 us | 13.99 us | 21.05 KB | 21.19 KB | 3.42 | yes |
| E9 | full cycle | GreatPartsCel | 4.88 us | 4.90 us | 10.61 KB | 10.74 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.08 us | 4.10 us | 9.04 KB | 9.17 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 4.70 us | 4.71 us | 9.16 KB | 9.30 KB | 1.00 |  |
| E9 | full cycle | Celly | - | - | - | - | excluded | |
| E9 | full cycle | DotnetCel | - | - | - | - | excluded | |
