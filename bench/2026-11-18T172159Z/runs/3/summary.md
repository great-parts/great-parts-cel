## Run 3: scenario x operation

- Run 3: 2026-11-18T16:27:19.725Z - 2026-11-18T17:21:59.222Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column; "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 21.8 ns | - | 160 B | - | |
| E1 | compile | Celly | 15.57 us | 15.59 us | 42.84 KB | 43.00 KB | 3.72 | yes |
| E1 | compile | DotnetCel | 11.46 us | 11.48 us | 26.67 KB | 26.83 KB | 2.74 | yes |
| E1 | compile | Cel.NET | 40.18 us | 40.20 us | 49.93 KB | 50.08 KB | 9.62 | yes |
| E1 | compile | TelusCel | 7.81 us | 7.84 us | 10.34 KB | 10.50 KB | 1.87 | yes |
| E1 | compile | GreatPartsCel | 4.18 us | 4.20 us | 7.78 KB | 7.94 KB | 1.00 |  |
| E1 | evaluate | Celly | 487.5 ns | - | 832 B | - | 1.37 | yes |
| E1 | evaluate | DotnetCel | 444.5 ns | - | 560 B | - | 1.25 | yes |
| E1 | evaluate | Cel.NET | 563.8 ns | - | 1.51 KB | - | 1.58 | yes |
| E1 | evaluate | TelusCel | 8.67 us | - | 11.28 KB | - | 24.35 | yes |
| E1 | evaluate | GreatPartsCel | 356.2 ns | - | 648 B | - | 1.00 |  |
| E1 | full cycle | Celly | 16.66 us | 16.68 us | 43.64 KB | 43.79 KB | 3.47 | yes |
| E1 | full cycle | DotnetCel | 15.41 us | 15.43 us | 29.79 KB | 29.95 KB | 3.21 | yes |
| E1 | full cycle | Cel.NET | 42.50 us | 42.52 us | 51.43 KB | 51.58 KB | 8.86 | yes |
| E1 | full cycle | TelusCel | 18.30 us | 18.32 us | 21.63 KB | 21.78 KB | 3.81 | yes |
| E1 | full cycle | GreatPartsCel | 4.80 us | 4.82 us | 8.43 KB | 8.59 KB | 1.00 |  |
| E2/10 | text creation | - | - | 21.9 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 21.19 us | 21.21 us | 54.51 KB | 54.66 KB | 3.21 | yes |
| E2/10 | compile | DotnetCel | 16.16 us | 16.19 us | 32.88 KB | 33.02 KB | 2.45 | yes |
| E2/10 | compile | Cel.NET | 52.13 us | 52.15 us | 71.99 KB | 72.14 KB | 7.89 | yes |
| E2/10 | compile | TelusCel | 12.47 us | 12.49 us | 15.52 KB | 15.66 KB | 1.89 | yes |
| E2/10 | compile | GreatPartsCel | 6.61 us | 6.63 us | 11.04 KB | 11.18 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 3.84 us | - | 2.96 KB | - | 1.85 | yes |
| E2/10 | evaluate | DotnetCel | 6.06 us | - | 4.07 KB | - | 2.92 | yes |
| E2/10 | evaluate | Cel.NET | 4.76 us | - | 6.47 KB | - | 2.29 | yes |
| E2/10 | evaluate | TelusCel | 92.16 us | - | 112.86 KB | - | 44.37 | yes |
| E2/10 | evaluate | GreatPartsCel | 2.08 us | - | 816 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 27.10 us | 27.13 us | 57.50 KB | 57.65 KB | 2.99 | yes |
| E2/10 | full cycle | DotnetCel | 27.48 us | 27.50 us | 40.35 KB | 40.49 KB | 3.03 | yes |
| E2/10 | full cycle | Cel.NET | 61.57 us | 61.59 us | 78.42 KB | 78.56 KB | 6.79 | yes |
| E2/10 | full cycle | TelusCel | 109.85 us | 109.87 us | 128.38 KB | 128.52 KB | 12.11 | yes |
| E2/10 | full cycle | GreatPartsCel | 9.07 us | 9.09 us | 11.86 KB | 12.00 KB | 1.00 |  |
| E2/100 | text creation | - | - | 21.0 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 21.02 us | 21.04 us | 54.57 KB | 54.72 KB | 3.24 | yes |
| E2/100 | compile | DotnetCel | 15.39 us | 15.41 us | 32.88 KB | 33.02 KB | 2.37 | yes |
| E2/100 | compile | Cel.NET | 53.31 us | 53.33 us | 71.98 KB | 72.13 KB | 8.21 | yes |
| E2/100 | compile | TelusCel | 12.51 us | 12.53 us | 15.52 KB | 15.66 KB | 1.93 | yes |
| E2/100 | compile | GreatPartsCel | 6.49 us | 6.52 us | 11.04 KB | 11.18 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 34.11 us | - | 21.66 KB | - | 1.90 | yes |
| E2/100 | evaluate | DotnetCel | 56.80 us | - | 31.91 KB | - | 3.17 | yes |
| E2/100 | evaluate | Cel.NET | 45.21 us | - | 57.41 KB | - | 2.52 | yes |
| E2/100 | evaluate | TelusCel | 892.69 us | - | 1.05 MB | - | 49.76 | yes |
| E2/100 | evaluate | GreatPartsCel | 17.94 us | - | 816 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 59.64 us | 59.66 us | 76.23 KB | 76.38 KB | 2.39 | yes |
| E2/100 | full cycle | DotnetCel | 80.45 us | 80.47 us | 68.18 KB | 68.33 KB | 3.23 | yes |
| E2/100 | full cycle | Cel.NET | 103.16 us | 103.18 us | 129.40 KB | 129.55 KB | 4.14 | yes |
| E2/100 | full cycle | TelusCel | 917.11 us | 917.14 us | 1.06 MB | 1.06 MB | 36.77 | yes |
| E2/100 | full cycle | GreatPartsCel | 24.94 us | 24.96 us | 11.86 KB | 12.01 KB | 1.00 |  |
| E3/10 | text creation | - | - | 21.0 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 17.93 us | 17.95 us | 44.34 KB | 44.49 KB | 3.42 | yes |
| E3/10 | compile | DotnetCel | 14.88 us | 14.91 us | 30.62 KB | 30.77 KB | 2.84 | yes |
| E3/10 | compile | Cel.NET | 40.51 us | 40.53 us | 53.10 KB | 53.25 KB | 7.74 | yes |
| E3/10 | compile | TelusCel | 10.80 us | 10.82 us | 13.39 KB | 13.54 KB | 2.06 | yes |
| E3/10 | compile | GreatPartsCel | 5.24 us | 5.26 us | 9.85 KB | 10.00 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 1.00 us | - | 1.57 KB | - | 1.88 | yes |
| E3/10 | evaluate | DotnetCel | 1.34 us | - | 1.78 KB | - | 2.51 | yes |
| E3/10 | evaluate | Cel.NET | 1.08 us | - | 2.12 KB | - | 2.03 | yes |
| E3/10 | evaluate | TelusCel | 54.37 us | - | 63.65 KB | - | 102.08 | yes |
| E3/10 | evaluate | GreatPartsCel | 532.6 ns | - | 784 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 19.88 us | 19.90 us | 45.87 KB | 46.02 KB | 3.27 | yes |
| E3/10 | full cycle | DotnetCel | 20.52 us | 20.54 us | 35.80 KB | 35.95 KB | 3.38 | yes |
| E3/10 | full cycle | Cel.NET | 44.38 us | 44.40 us | 55.16 KB | 55.31 KB | 7.30 | yes |
| E3/10 | full cycle | TelusCel | 65.94 us | 65.96 us | 77.04 KB | 77.19 KB | 10.85 | yes |
| E3/10 | full cycle | GreatPartsCel | 6.08 us | 6.10 us | 10.64 KB | 10.79 KB | 1.00 |  |
| E3/100 | text creation | - | - | 21.7 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 17.72 us | 17.74 us | 44.36 KB | 44.51 KB | 3.38 | yes |
| E3/100 | compile | DotnetCel | 14.34 us | 14.36 us | 30.62 KB | 30.77 KB | 2.73 | yes |
| E3/100 | compile | Cel.NET | 39.63 us | 39.65 us | 53.08 KB | 53.23 KB | 7.55 | yes |
| E3/100 | compile | TelusCel | 10.05 us | 10.07 us | 13.39 KB | 13.54 KB | 1.91 | yes |
| E3/100 | compile | GreatPartsCel | 5.25 us | 5.27 us | 9.85 KB | 10.00 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.45 us | - | 6.91 KB | - | 8.52 | yes |
| E3/100 | evaluate | DotnetCel | 4.43 us | - | 7.12 KB | - | 8.49 | yes |
| E3/100 | evaluate | Cel.NET | 2.51 us | - | 8.86 KB | - | 4.80 | yes |
| E3/100 | evaluate | TelusCel | 487.58 us | - | 578.05 KB | - | 933.98 | yes |
| E3/100 | evaluate | GreatPartsCel | 522.1 ns | - | 784 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 23.26 us | 23.29 us | 51.23 KB | 51.37 KB | 3.75 | yes |
| E3/100 | full cycle | DotnetCel | 24.63 us | 24.65 us | 41.13 KB | 41.28 KB | 3.97 | yes |
| E3/100 | full cycle | Cel.NET | 46.76 us | 46.78 us | 61.96 KB | 62.11 KB | 7.54 | yes |
| E3/100 | full cycle | TelusCel | 512.97 us | 513.00 us | 591.44 KB | 591.59 KB | 82.76 | yes |
| E3/100 | full cycle | GreatPartsCel | 6.20 us | 6.22 us | 10.64 KB | 10.79 KB | 1.00 |  |
| E4/10 | text creation | - | - | 22.2 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 35.40 us | 35.42 us | 76.10 KB | 76.28 KB | 3.03 | yes |
| E4/10 | compile | DotnetCel | 21.13 us | 21.15 us | 38.16 KB | 38.34 KB | 1.81 | yes |
| E4/10 | compile | Cel.NET | 118.94 us | 118.97 us | 134.77 KB | 134.95 KB | 10.18 | yes |
| E4/10 | compile | TelusCel | 17.01 us | 17.03 us | 21.27 KB | 21.45 KB | 1.46 | yes |
| E4/10 | compile | GreatPartsCel | 11.69 us | 11.71 us | 17.35 KB | 17.53 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 4.04 us | - | 4.05 KB | - | 2.03 | yes |
| E4/10 | evaluate | DotnetCel | 5.94 us | - | 5.31 KB | - | 2.98 | yes |
| E4/10 | evaluate | Cel.NET | 5.32 us | - | 7.38 KB | - | 2.67 | yes |
| E4/10 | evaluate | TelusCel | 85.80 us | - | 99.98 KB | - | 43.12 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.99 us | - | 1.29 KB | - | 1.00 |  |
| E4/10 | full cycle | Celly | 40.37 us | 40.39 us | 80.18 KB | 80.36 KB | 2.76 | yes |
| E4/10 | full cycle | DotnetCel | 33.71 us | 33.73 us | 46.87 KB | 47.05 KB | 2.31 | yes |
| E4/10 | full cycle | Cel.NET | 129.30 us | 129.32 us | 141.92 KB | 142.10 KB | 8.85 | yes |
| E4/10 | full cycle | TelusCel | 102.80 us | 102.83 us | 121.26 KB | 121.44 KB | 7.04 | yes |
| E4/10 | full cycle | GreatPartsCel | 14.61 us | 14.63 us | 18.66 KB | 18.84 KB | 1.00 |  |
| E4/100 | text creation | - | - | 22.6 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 34.89 us | 34.92 us | 76.07 KB | 76.25 KB | 2.96 | yes |
| E4/100 | compile | DotnetCel | 20.82 us | 20.84 us | 38.16 KB | 38.34 KB | 1.77 | yes |
| E4/100 | compile | Cel.NET | 117.48 us | 117.50 us | 134.54 KB | 134.72 KB | 9.96 | yes |
| E4/100 | compile | TelusCel | 17.05 us | 17.08 us | 21.27 KB | 21.45 KB | 1.45 | yes |
| E4/100 | compile | GreatPartsCel | 11.79 us | 11.82 us | 17.35 KB | 17.53 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 38.93 us | - | 42.41 KB | - | 1.99 | yes |
| E4/100 | evaluate | DotnetCel | 66.82 us | - | 70.80 KB | - | 3.42 | yes |
| E4/100 | evaluate | Cel.NET | 62.37 us | - | 100.41 KB | - | 3.19 | yes |
| E4/100 | evaluate | TelusCel | 1.03 ms | - | 1.16 MB | - | 52.59 | yes |
| E4/100 | evaluate | GreatPartsCel | 19.52 us | - | 10.04 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 77.76 us | 77.79 us | 118.51 KB | 118.69 KB | 2.26 | yes |
| E4/100 | full cycle | DotnetCel | 101.24 us | 101.26 us | 112.37 KB | 112.55 KB | 2.94 | yes |
| E4/100 | full cycle | Cel.NET | 192.34 us | 192.37 us | 234.96 KB | 235.13 KB | 5.59 | yes |
| E4/100 | full cycle | TelusCel | 1.00 ms | 1.00 ms | 1.18 MB | 1.18 MB | 29.08 | yes |
| E4/100 | full cycle | GreatPartsCel | 34.41 us | 34.43 us | 27.41 KB | 27.59 KB | 1.00 |  |
| E5 | text creation | - | - | 22.6 ns | - | 168 B | - | |
| E5 | compile | Celly | 12.20 us | 12.22 us | 35.50 KB | 35.67 KB | 4.26 | yes |
| E5 | compile | DotnetCel | 9.74 us | 9.76 us | 24.38 KB | 24.55 KB | 3.40 | yes |
| E5 | compile | Cel.NET | 22.57 us | 22.60 us | 30.64 KB | 30.81 KB | 7.89 | yes |
| E5 | compile | TelusCel | 6.09 us | 6.12 us | 8.66 KB | 8.82 KB | 2.13 | yes |
| E5 | compile | GreatPartsCel | 2.86 us | 2.88 us | 5.80 KB | 5.97 KB | 1.00 |  |
| E5 | evaluate | Celly | 531.9 ns | - | 752 B | - | 1.00 |  |
| E5 | evaluate | DotnetCel | 595.7 ns | - | 448 B | - | 1.12 | yes |
| E5 | evaluate | Cel.NET | 79.49 us | - | 251.94 KB | - | 149.45 | yes |
| E5 | evaluate | TelusCel | 10.28 us | - | 14.41 KB | - | 19.33 | yes |
| E5 | evaluate | GreatPartsCel | 1.08 us | - | 616 B | - | 2.02 | yes |
| E5 | full cycle | Celly | 13.17 us | 13.19 us | 36.16 KB | 36.32 KB | 3.10 | yes |
| E5 | full cycle | DotnetCel | 14.13 us | 14.15 us | 28.04 KB | 28.20 KB | 3.32 | yes |
| E5 | full cycle | Cel.NET | 134.08 us | 134.10 us | 282.58 KB | 282.75 KB | 31.52 | yes |
| E5 | full cycle | TelusCel | 18.18 us | 18.20 us | 23.06 KB | 23.23 KB | 4.27 | yes |
| E5 | full cycle | GreatPartsCel | 4.25 us | 4.28 us | 6.43 KB | 6.59 KB | 1.00 |  |
| E6 | text creation | - | - | 22.6 ns | - | 184 B | - | |
| E6 | compile | Celly | 16.39 us | 16.41 us | 43.06 KB | 43.24 KB | 3.34 | yes |
| E6 | compile | Cel.NET | 42.00 us | 42.03 us | 50.61 KB | 50.79 KB | 8.57 | yes |
| E6 | compile | TelusCel | 10.52 us | 10.55 us | 13.98 KB | 14.16 KB | 2.15 | yes |
| E6 | compile | GreatPartsCel | 4.90 us | 4.92 us | 8.26 KB | 8.44 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 520.9 ns | - | 912 B | - | 1.51 | yes |
| E6 | evaluate | Cel.NET | 542.7 ns | - | 1.55 KB | - | 1.57 | yes |
| E6 | evaluate | TelusCel | 14.04 us | - | 18.44 KB | - | 40.71 | yes |
| E6 | evaluate | GreatPartsCel | 344.8 ns | - | 616 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 17.34 us | 17.37 us | 43.95 KB | 44.13 KB | 3.31 | yes |
| E6 | full cycle | Cel.NET | 44.70 us | 44.73 us | 51.93 KB | 52.11 KB | 8.54 | yes |
| E6 | full cycle | TelusCel | 25.93 us | 25.95 us | 32.42 KB | 32.60 KB | 4.96 | yes |
| E6 | full cycle | GreatPartsCel | 5.23 us | 5.26 us | 8.88 KB | 9.06 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 20.7 ns | - | 128 B | - | |
| E7 | compile | Celly | 16.03 us | 16.05 us | 45.36 KB | 45.49 KB | 3.86 | yes |
| E7 | compile | DotnetCel | 10.81 us | 10.83 us | 26.02 KB | 26.14 KB | 2.61 | yes |
| E7 | compile | Cel.NET | 41.17 us | 41.19 us | 52.74 KB | 52.87 KB | 9.92 | yes |
| E7 | compile | GreatPartsCel | 4.15 us | 4.17 us | 6.85 KB | 6.98 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 712.8 ns | - | 1.18 KB | - | 1.98 | yes |
| E7 | evaluate | DotnetCel | 439.4 ns | - | 592 B | - | 1.22 | yes |
| E7 | evaluate | Cel.NET | 716.7 ns | - | 1.59 KB | - | 1.99 | yes |
| E7 | evaluate | GreatPartsCel | 359.6 ns | - | 648 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 17.99 us | 18.01 us | 46.56 KB | 46.69 KB | 3.96 | yes |
| E7 | full cycle | DotnetCel | 14.52 us | 14.54 us | 29.17 KB | 29.29 KB | 3.19 | yes |
| E7 | full cycle | Cel.NET | 44.88 us | 44.90 us | 54.32 KB | 54.44 KB | 9.87 | yes |
| E7 | full cycle | GreatPartsCel | 4.55 us | 4.57 us | 7.51 KB | 7.63 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 22.9 ns | - | 192 B | - | |
| E8 | compile | Celly | 20.65 us | 20.68 us | 52.62 KB | 52.81 KB | 3.21 | yes |
| E8 | compile | Cel.NET | 86.34 us | 86.36 us | 99.38 KB | 99.56 KB | 13.44 | yes |
| E8 | compile | TelusCel | 15.02 us | 15.04 us | 19.41 KB | 19.59 KB | 2.34 | yes |
| E8 | compile | GreatPartsCel | 6.43 us | 6.45 us | 11.74 KB | 11.93 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 998.1 ns | - | 1.66 KB | - | 1.70 | yes |
| E8 | evaluate | Cel.NET | 649.9 ns | - | 1.66 KB | - | 1.11 | yes |
| E8 | evaluate | TelusCel | 12.21 us | - | 16.25 KB | - | 20.83 | yes |
| E8 | evaluate | GreatPartsCel | 586.1 ns | - | 904 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 23.17 us | 23.19 us | 54.15 KB | 54.34 KB | 3.15 | yes |
| E8 | full cycle | Cel.NET | 90.45 us | 90.47 us | 100.98 KB | 101.17 KB | 12.28 | yes |
| E8 | full cycle | TelusCel | 29.54 us | 29.56 us | 35.66 KB | 35.84 KB | 4.01 | yes |
| E8 | full cycle | GreatPartsCel | 7.37 us | 7.39 us | 12.65 KB | 12.84 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 21.1 ns | - | 136 B | - | |
| E9 | compile | Celly | 18.24 us | 18.26 us | 52.63 KB | 52.76 KB | 3.85 | yes |
| E9 | compile | DotnetCel | 11.39 us | 11.41 us | 27.05 KB | 27.18 KB | 2.41 | yes |
| E9 | compile | Cel.NET | 54.52 us | 54.54 us | 76.47 KB | 76.61 KB | 11.52 | yes |
| E9 | compile | TelusCel | 7.69 us | 7.71 us | 10.09 KB | 10.23 KB | 1.63 | yes |
| E9 | compile | GreatPartsCel | 4.73 us | 4.75 us | 9.20 KB | 9.33 KB | 1.00 |  |
| E9 | evaluate | Celly | 547.4 ns | - | 1.11 KB | - | 1.26 | yes |
| E9 | evaluate | DotnetCel | 566.1 ns | - | 808 B | - | 1.30 | yes |
| E9 | evaluate | Cel.NET | 558.8 ns | - | 1.73 KB | - | 1.29 | yes |
| E9 | evaluate | TelusCel | 7.66 us | - | 10.87 KB | - | 17.66 | yes |
| E9 | evaluate | GreatPartsCel | 433.8 ns | - | 800 B | - | 1.00 |  |
| E9 | full cycle | Celly | 18.63 us | 18.66 us | 53.76 KB | 53.89 KB | 3.46 | yes |
| E9 | full cycle | DotnetCel | 14.66 us | 14.68 us | 30.41 KB | 30.54 KB | 2.72 | yes |
| E9 | full cycle | Cel.NET | 58.40 us | 58.42 us | 78.20 KB | 78.33 KB | 10.83 | yes |
| E9 | full cycle | TelusCel | 17.01 us | 17.03 us | 20.96 KB | 21.09 KB | 3.16 | yes |
| E9 | full cycle | GreatPartsCel | 5.39 us | 5.41 us | 10.00 KB | 10.13 KB | 1.00 |  |
