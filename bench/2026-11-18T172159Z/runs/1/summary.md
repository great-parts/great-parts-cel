## Run 1: scenario x operation

- Run 1: 2026-11-18T14:37:21.363Z - 2026-11-18T15:32:44.393Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column; "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 23.1 ns | - | 160 B | - | |
| E1 | compile | Celly | 15.45 us | 15.47 us | 42.83 KB | 42.98 KB | 3.51 | yes |
| E1 | compile | DotnetCel | 11.65 us | 11.67 us | 26.67 KB | 26.83 KB | 2.65 | yes |
| E1 | compile | Cel.NET | 39.62 us | 39.64 us | 49.60 KB | 49.75 KB | 9.00 | yes |
| E1 | compile | TelusCel | 8.10 us | 8.13 us | 10.34 KB | 10.50 KB | 1.84 | yes |
| E1 | compile | GreatPartsCel | 4.40 us | 4.43 us | 7.78 KB | 7.93 KB | 1.00 |  |
| E1 | evaluate | Celly | 457.6 ns | - | 832 B | - | 1.24 | yes |
| E1 | evaluate | DotnetCel | 473.4 ns | - | 560 B | - | 1.28 | yes |
| E1 | evaluate | Cel.NET | 554.6 ns | - | 1.51 KB | - | 1.50 | yes |
| E1 | evaluate | TelusCel | 8.27 us | - | 11.28 KB | - | 22.36 | yes |
| E1 | evaluate | GreatPartsCel | 369.8 ns | - | 648 B | - | 1.00 |  |
| E1 | full cycle | Celly | 16.05 us | 16.08 us | 43.64 KB | 43.80 KB | 3.25 | yes |
| E1 | full cycle | DotnetCel | 15.53 us | 15.56 us | 29.79 KB | 29.95 KB | 3.14 | yes |
| E1 | full cycle | Cel.NET | 42.46 us | 42.48 us | 51.11 KB | 51.26 KB | 8.58 | yes |
| E1 | full cycle | TelusCel | 17.72 us | 17.75 us | 21.63 KB | 21.78 KB | 3.58 | yes |
| E1 | full cycle | GreatPartsCel | 4.95 us | 4.97 us | 8.44 KB | 8.59 KB | 1.00 |  |
| E2/10 | text creation | - | - | 22.0 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 20.28 us | 20.30 us | 54.57 KB | 54.72 KB | 3.02 | yes |
| E2/10 | compile | DotnetCel | 15.69 us | 15.71 us | 32.88 KB | 33.02 KB | 2.33 | yes |
| E2/10 | compile | Cel.NET | 52.98 us | 53.00 us | 72.01 KB | 72.16 KB | 7.89 | yes |
| E2/10 | compile | TelusCel | 12.92 us | 12.94 us | 15.52 KB | 15.66 KB | 1.92 | yes |
| E2/10 | compile | GreatPartsCel | 6.72 us | 6.74 us | 11.04 KB | 11.19 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 3.87 us | - | 2.96 KB | - | 1.86 | yes |
| E2/10 | evaluate | DotnetCel | 6.51 us | - | 4.07 KB | - | 3.13 | yes |
| E2/10 | evaluate | Cel.NET | 4.83 us | - | 6.47 KB | - | 2.33 | yes |
| E2/10 | evaluate | TelusCel | 93.47 us | - | 112.86 KB | - | 44.96 | yes |
| E2/10 | evaluate | GreatPartsCel | 2.08 us | - | 816 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 26.07 us | 26.09 us | 57.56 KB | 57.71 KB | 2.82 | yes |
| E2/10 | full cycle | DotnetCel | 27.01 us | 27.03 us | 40.35 KB | 40.49 KB | 2.92 | yes |
| E2/10 | full cycle | Cel.NET | 63.38 us | 63.40 us | 78.42 KB | 78.57 KB | 6.86 | yes |
| E2/10 | full cycle | TelusCel | 111.03 us | 111.05 us | 128.38 KB | 128.52 KB | 12.02 | yes |
| E2/10 | full cycle | GreatPartsCel | 9.24 us | 9.26 us | 11.86 KB | 12.00 KB | 1.00 |  |
| E2/100 | text creation | - | - | 21.3 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 20.81 us | 20.83 us | 54.57 KB | 54.72 KB | 3.03 | yes |
| E2/100 | compile | DotnetCel | 15.99 us | 16.01 us | 32.88 KB | 33.02 KB | 2.33 | yes |
| E2/100 | compile | Cel.NET | 53.30 us | 53.32 us | 71.98 KB | 72.13 KB | 7.75 | yes |
| E2/100 | compile | TelusCel | 12.38 us | 12.40 us | 15.52 KB | 15.66 KB | 1.80 | yes |
| E2/100 | compile | GreatPartsCel | 6.87 us | 6.90 us | 11.04 KB | 11.19 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 31.35 us | - | 21.66 KB | - | 1.49 | yes |
| E2/100 | evaluate | DotnetCel | 57.19 us | - | 31.91 KB | - | 2.71 | yes |
| E2/100 | evaluate | Cel.NET | 44.99 us | - | 57.41 KB | - | 2.13 | yes |
| E2/100 | evaluate | TelusCel | 904.01 us | - | 1.05 MB | - | 42.85 | yes |
| E2/100 | evaluate | GreatPartsCel | 21.10 us | - | 816 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 60.51 us | 60.53 us | 76.23 KB | 76.38 KB | 2.39 | yes |
| E2/100 | full cycle | DotnetCel | 81.57 us | 81.59 us | 68.20 KB | 68.34 KB | 3.23 | yes |
| E2/100 | full cycle | Cel.NET | 104.25 us | 104.27 us | 129.40 KB | 129.54 KB | 4.12 | yes |
| E2/100 | full cycle | TelusCel | 907.37 us | 907.39 us | 1.06 MB | 1.06 MB | 35.90 | yes |
| E2/100 | full cycle | GreatPartsCel | 25.28 us | 25.30 us | 11.86 KB | 12.01 KB | 1.00 |  |
| E3/10 | text creation | - | - | 23.0 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 17.97 us | 18.00 us | 44.35 KB | 44.50 KB | 3.18 | yes |
| E3/10 | compile | DotnetCel | 14.27 us | 14.29 us | 30.62 KB | 30.77 KB | 2.53 | yes |
| E3/10 | compile | Cel.NET | 39.89 us | 39.92 us | 53.10 KB | 53.25 KB | 7.06 | yes |
| E3/10 | compile | TelusCel | 10.06 us | 10.08 us | 13.39 KB | 13.54 KB | 1.78 | yes |
| E3/10 | compile | GreatPartsCel | 5.65 us | 5.67 us | 9.85 KB | 10.00 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 1.01 us | - | 1.61 KB | - | 1.77 | yes |
| E3/10 | evaluate | DotnetCel | 1.31 us | - | 1.78 KB | - | 2.30 | yes |
| E3/10 | evaluate | Cel.NET | 1.10 us | - | 2.12 KB | - | 1.94 | yes |
| E3/10 | evaluate | TelusCel | 52.74 us | - | 63.65 KB | - | 92.57 | yes |
| E3/10 | evaluate | GreatPartsCel | 569.8 ns | - | 784 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 19.20 us | 19.23 us | 45.89 KB | 46.04 KB | 3.09 | yes |
| E3/10 | full cycle | DotnetCel | 20.39 us | 20.42 us | 35.80 KB | 35.95 KB | 3.28 | yes |
| E3/10 | full cycle | Cel.NET | 45.28 us | 45.30 us | 55.17 KB | 55.32 KB | 7.29 | yes |
| E3/10 | full cycle | TelusCel | 66.60 us | 66.62 us | 77.04 KB | 77.19 KB | 10.72 | yes |
| E3/10 | full cycle | GreatPartsCel | 6.21 us | 6.24 us | 10.64 KB | 10.79 KB | 1.00 |  |
| E3/100 | text creation | - | - | 23.1 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 18.07 us | 18.10 us | 44.34 KB | 44.49 KB | 3.24 | yes |
| E3/100 | compile | DotnetCel | 14.40 us | 14.42 us | 30.62 KB | 30.77 KB | 2.58 | yes |
| E3/100 | compile | Cel.NET | 41.55 us | 41.57 us | 53.07 KB | 53.22 KB | 7.45 | yes |
| E3/100 | compile | TelusCel | 10.14 us | 10.17 us | 13.39 KB | 13.54 KB | 1.82 | yes |
| E3/100 | compile | GreatPartsCel | 5.58 us | 5.60 us | 9.85 KB | 10.00 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.60 us | - | 6.95 KB | - | 8.75 | yes |
| E3/100 | evaluate | DotnetCel | 4.22 us | - | 7.12 KB | - | 8.02 | yes |
| E3/100 | evaluate | Cel.NET | 2.58 us | - | 8.86 KB | - | 4.91 | yes |
| E3/100 | evaluate | TelusCel | 496.73 us | - | 578.05 KB | - | 944.24 | yes |
| E3/100 | evaluate | GreatPartsCel | 526.1 ns | - | 784 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 22.37 us | 22.39 us | 51.23 KB | 51.37 KB | 3.63 | yes |
| E3/100 | full cycle | DotnetCel | 23.22 us | 23.25 us | 41.13 KB | 41.28 KB | 3.77 | yes |
| E3/100 | full cycle | Cel.NET | 46.32 us | 46.35 us | 61.94 KB | 62.09 KB | 7.52 | yes |
| E3/100 | full cycle | TelusCel | 511.45 us | 511.48 us | 591.44 KB | 591.59 KB | 83.04 | yes |
| E3/100 | full cycle | GreatPartsCel | 6.16 us | 6.18 us | 10.64 KB | 10.79 KB | 1.00 |  |
| E4/10 | text creation | - | - | 22.8 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 34.46 us | 34.48 us | 76.08 KB | 76.26 KB | 2.96 | yes |
| E4/10 | compile | DotnetCel | 21.25 us | 21.27 us | 38.16 KB | 38.34 KB | 1.82 | yes |
| E4/10 | compile | Cel.NET | 121.34 us | 121.36 us | 134.78 KB | 134.96 KB | 10.41 | yes |
| E4/10 | compile | TelusCel | 16.99 us | 17.01 us | 21.27 KB | 21.45 KB | 1.46 | yes |
| E4/10 | compile | GreatPartsCel | 11.65 us | 11.68 us | 17.35 KB | 17.53 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 4.09 us | - | 4.08 KB | - | 1.94 | yes |
| E4/10 | evaluate | DotnetCel | 6.04 us | - | 5.31 KB | - | 2.86 | yes |
| E4/10 | evaluate | Cel.NET | 5.31 us | - | 7.38 KB | - | 2.52 | yes |
| E4/10 | evaluate | TelusCel | 81.15 us | - | 99.98 KB | - | 38.50 | yes |
| E4/10 | evaluate | GreatPartsCel | 2.11 us | - | 1.29 KB | - | 1.00 |  |
| E4/10 | full cycle | Celly | 41.96 us | 41.99 us | 80.15 KB | 80.33 KB | 2.94 | yes |
| E4/10 | full cycle | DotnetCel | 33.15 us | 33.18 us | 46.87 KB | 47.05 KB | 2.32 | yes |
| E4/10 | full cycle | Cel.NET | 129.58 us | 129.60 us | 141.94 KB | 142.12 KB | 9.08 | yes |
| E4/10 | full cycle | TelusCel | 103.02 us | 103.04 us | 121.26 KB | 121.44 KB | 7.22 | yes |
| E4/10 | full cycle | GreatPartsCel | 14.28 us | 14.30 us | 18.66 KB | 18.84 KB | 1.00 |  |
| E4/100 | text creation | - | - | 22.8 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 35.10 us | 35.13 us | 76.07 KB | 76.25 KB | 3.01 | yes |
| E4/100 | compile | DotnetCel | 21.71 us | 21.73 us | 38.16 KB | 38.34 KB | 1.86 | yes |
| E4/100 | compile | Cel.NET | 120.57 us | 120.60 us | 134.54 KB | 134.72 KB | 10.32 | yes |
| E4/100 | compile | TelusCel | 16.62 us | 16.64 us | 21.27 KB | 21.45 KB | 1.42 | yes |
| E4/100 | compile | GreatPartsCel | 11.68 us | 11.70 us | 17.35 KB | 17.53 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 40.63 us | - | 42.44 KB | - | 2.11 | yes |
| E4/100 | evaluate | DotnetCel | 68.99 us | - | 70.80 KB | - | 3.58 | yes |
| E4/100 | evaluate | Cel.NET | 61.43 us | - | 100.41 KB | - | 3.19 | yes |
| E4/100 | evaluate | TelusCel | 954.64 us | - | 1.16 MB | - | 49.57 | yes |
| E4/100 | evaluate | GreatPartsCel | 19.26 us | - | 10.04 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 80.42 us | 80.45 us | 118.50 KB | 118.68 KB | 2.33 | yes |
| E4/100 | full cycle | DotnetCel | 97.71 us | 97.73 us | 112.36 KB | 112.54 KB | 2.83 | yes |
| E4/100 | full cycle | Cel.NET | 189.70 us | 189.73 us | 234.96 KB | 235.13 KB | 5.50 | yes |
| E4/100 | full cycle | TelusCel | 1.01 ms | 1.01 ms | 1.18 MB | 1.18 MB | 29.44 | yes |
| E4/100 | full cycle | GreatPartsCel | 34.47 us | 34.49 us | 27.41 KB | 27.59 KB | 1.00 |  |
| E5 | text creation | - | - | 24.2 ns | - | 168 B | - | |
| E5 | compile | Celly | 12.68 us | 12.70 us | 35.52 KB | 35.68 KB | 4.49 | yes |
| E5 | compile | DotnetCel | 10.41 us | 10.44 us | 24.38 KB | 24.55 KB | 3.69 | yes |
| E5 | compile | Cel.NET | 23.11 us | 23.13 us | 30.65 KB | 30.81 KB | 8.19 | yes |
| E5 | compile | TelusCel | 6.20 us | 6.22 us | 8.66 KB | 8.82 KB | 2.20 | yes |
| E5 | compile | GreatPartsCel | 2.82 us | 2.85 us | 5.80 KB | 5.97 KB | 1.00 |  |
| E5 | evaluate | Celly | 538.8 ns | - | 752 B | - | 1.00 |  |
| E5 | evaluate | DotnetCel | 565.1 ns | - | 448 B | - | 1.05 |  |
| E5 | evaluate | Cel.NET | 80.39 us | - | 251.94 KB | - | 149.20 | yes |
| E5 | evaluate | TelusCel | 10.25 us | - | 14.41 KB | - | 19.02 | yes |
| E5 | evaluate | GreatPartsCel | 1.07 us | - | 616 B | - | 1.99 | yes |
| E5 | full cycle | Celly | 13.56 us | 13.58 us | 36.18 KB | 36.35 KB | 3.26 | yes |
| E5 | full cycle | DotnetCel | 14.79 us | 14.82 us | 28.04 KB | 28.20 KB | 3.56 | yes |
| E5 | full cycle | Cel.NET | 134.88 us | 134.91 us | 282.60 KB | 282.76 KB | 32.45 | yes |
| E5 | full cycle | TelusCel | 18.31 us | 18.33 us | 23.06 KB | 23.23 KB | 4.40 | yes |
| E5 | full cycle | GreatPartsCel | 4.16 us | 4.18 us | 6.43 KB | 6.59 KB | 1.00 |  |
| E6 | text creation | - | - | 24.3 ns | - | 184 B | - | |
| E6 | compile | Celly | 16.80 us | 16.82 us | 43.04 KB | 43.22 KB | 3.56 | yes |
| E6 | compile | Cel.NET | 42.86 us | 42.89 us | 50.61 KB | 50.79 KB | 9.09 | yes |
| E6 | compile | TelusCel | 10.45 us | 10.47 us | 13.98 KB | 14.16 KB | 2.22 | yes |
| E6 | compile | GreatPartsCel | 4.72 us | 4.74 us | 8.26 KB | 8.44 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 605.1 ns | - | 912 B | - | 1.75 | yes |
| E6 | evaluate | Cel.NET | 550.9 ns | - | 1.55 KB | - | 1.60 | yes |
| E6 | evaluate | TelusCel | 16.08 us | - | 18.44 KB | - | 46.57 | yes |
| E6 | evaluate | GreatPartsCel | 345.3 ns | - | 616 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 18.06 us | 18.09 us | 43.93 KB | 44.11 KB | 3.46 | yes |
| E6 | full cycle | Cel.NET | 45.43 us | 45.45 us | 51.94 KB | 52.12 KB | 8.69 | yes |
| E6 | full cycle | TelusCel | 27.34 us | 27.36 us | 32.42 KB | 32.60 KB | 5.23 | yes |
| E6 | full cycle | GreatPartsCel | 5.22 us | 5.25 us | 8.88 KB | 9.06 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 21.6 ns | - | 128 B | - | |
| E7 | compile | Celly | 16.98 us | 17.00 us | 45.37 KB | 45.49 KB | 4.08 | yes |
| E7 | compile | DotnetCel | 11.24 us | 11.26 us | 26.02 KB | 26.14 KB | 2.70 | yes |
| E7 | compile | Cel.NET | 42.86 us | 42.89 us | 52.72 KB | 52.84 KB | 10.30 | yes |
| E7 | compile | GreatPartsCel | 4.16 us | 4.19 us | 6.85 KB | 6.97 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 739.0 ns | - | 1.18 KB | - | 1.94 | yes |
| E7 | evaluate | DotnetCel | 447.5 ns | - | 592 B | - | 1.18 | yes |
| E7 | evaluate | Cel.NET | 762.8 ns | - | 1.59 KB | - | 2.01 | yes |
| E7 | evaluate | GreatPartsCel | 380.3 ns | - | 648 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 18.86 us | 18.88 us | 46.55 KB | 46.67 KB | 3.89 | yes |
| E7 | full cycle | DotnetCel | 14.94 us | 14.96 us | 29.17 KB | 29.29 KB | 3.09 | yes |
| E7 | full cycle | Cel.NET | 45.35 us | 45.37 us | 54.32 KB | 54.44 KB | 9.37 | yes |
| E7 | full cycle | GreatPartsCel | 4.84 us | 4.86 us | 7.51 KB | 7.63 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 23.5 ns | - | 192 B | - | |
| E8 | compile | Celly | 23.12 us | 23.15 us | 52.61 KB | 52.80 KB | 3.53 | yes |
| E8 | compile | Cel.NET | 86.71 us | 86.74 us | 99.36 KB | 99.54 KB | 13.23 | yes |
| E8 | compile | TelusCel | 15.09 us | 15.11 us | 19.41 KB | 19.59 KB | 2.30 | yes |
| E8 | compile | GreatPartsCel | 6.55 us | 6.58 us | 11.74 KB | 11.93 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 1.09 us | - | 1.66 KB | - | 1.69 | yes |
| E8 | evaluate | Cel.NET | 674.7 ns | - | 1.66 KB | - | 1.04 |  |
| E8 | evaluate | TelusCel | 12.04 us | - | 16.25 KB | - | 18.58 | yes |
| E8 | evaluate | GreatPartsCel | 647.9 ns | - | 904 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 24.99 us | 25.02 us | 54.15 KB | 54.34 KB | 3.23 | yes |
| E8 | full cycle | Cel.NET | 89.41 us | 89.44 us | 100.98 KB | 101.17 KB | 11.56 | yes |
| E8 | full cycle | TelusCel | 31.30 us | 31.33 us | 35.66 KB | 35.84 KB | 4.05 | yes |
| E8 | full cycle | GreatPartsCel | 7.74 us | 7.76 us | 12.65 KB | 12.83 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 22.8 ns | - | 136 B | - | |
| E9 | compile | Celly | 18.94 us | 18.96 us | 52.64 KB | 52.77 KB | 3.74 | yes |
| E9 | compile | DotnetCel | 11.53 us | 11.55 us | 27.05 KB | 27.18 KB | 2.28 | yes |
| E9 | compile | Cel.NET | 57.50 us | 57.53 us | 76.47 KB | 76.60 KB | 11.37 | yes |
| E9 | compile | TelusCel | 7.50 us | 7.52 us | 10.09 KB | 10.23 KB | 1.48 | yes |
| E9 | compile | GreatPartsCel | 5.06 us | 5.08 us | 9.19 KB | 9.32 KB | 1.00 |  |
| E9 | evaluate | Celly | 499.6 ns | - | 1.11 KB | - | 1.21 | yes |
| E9 | evaluate | DotnetCel | 602.2 ns | - | 808 B | - | 1.46 | yes |
| E9 | evaluate | Cel.NET | 598.3 ns | - | 1.73 KB | - | 1.45 | yes |
| E9 | evaluate | TelusCel | 7.73 us | - | 10.87 KB | - | 18.80 | yes |
| E9 | evaluate | GreatPartsCel | 411.3 ns | - | 800 B | - | 1.00 |  |
| E9 | full cycle | Celly | 19.66 us | 19.68 us | 53.74 KB | 53.87 KB | 3.60 | yes |
| E9 | full cycle | DotnetCel | 15.25 us | 15.27 us | 30.41 KB | 30.54 KB | 2.79 | yes |
| E9 | full cycle | Cel.NET | 58.84 us | 58.86 us | 78.09 KB | 78.22 KB | 10.78 | yes |
| E9 | full cycle | TelusCel | 16.64 us | 16.66 us | 20.96 KB | 21.09 KB | 3.05 | yes |
| E9 | full cycle | GreatPartsCel | 5.46 us | 5.48 us | 10.00 KB | 10.13 KB | 1.00 |  |
