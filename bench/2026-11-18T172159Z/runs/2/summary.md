## Run 2: scenario x operation

- Run 2: 2026-11-18T15:32:44.395Z - 2026-11-18T16:27:19.725Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column; "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 24.9 ns | - | 160 B | - | |
| E1 | compile | Celly | 15.62 us | 15.64 us | 42.82 KB | 42.98 KB | 3.62 | yes |
| E1 | compile | DotnetCel | 11.66 us | 11.68 us | 26.67 KB | 26.83 KB | 2.70 | yes |
| E1 | compile | Cel.NET | 39.51 us | 39.53 us | 49.59 KB | 49.75 KB | 9.15 | yes |
| E1 | compile | TelusCel | 8.26 us | 8.29 us | 10.34 KB | 10.50 KB | 1.91 | yes |
| E1 | compile | GreatPartsCel | 4.32 us | 4.34 us | 7.78 KB | 7.94 KB | 1.00 |  |
| E1 | evaluate | Celly | 482.1 ns | - | 832 B | - | 1.32 | yes |
| E1 | evaluate | DotnetCel | 484.7 ns | - | 560 B | - | 1.33 | yes |
| E1 | evaluate | Cel.NET | 564.7 ns | - | 1.51 KB | - | 1.55 | yes |
| E1 | evaluate | TelusCel | 7.97 us | - | 11.28 KB | - | 21.87 | yes |
| E1 | evaluate | GreatPartsCel | 364.5 ns | - | 648 B | - | 1.00 |  |
| E1 | full cycle | Celly | 16.45 us | 16.48 us | 43.66 KB | 43.81 KB | 3.41 | yes |
| E1 | full cycle | DotnetCel | 15.24 us | 15.26 us | 29.79 KB | 29.95 KB | 3.15 | yes |
| E1 | full cycle | Cel.NET | 42.45 us | 42.47 us | 51.42 KB | 51.58 KB | 8.79 | yes |
| E1 | full cycle | TelusCel | 17.49 us | 17.52 us | 21.63 KB | 21.78 KB | 3.62 | yes |
| E1 | full cycle | GreatPartsCel | 4.83 us | 4.86 us | 8.44 KB | 8.59 KB | 1.00 |  |
| E2/10 | text creation | - | - | 24.1 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 20.76 us | 20.78 us | 54.51 KB | 54.66 KB | 3.13 | yes |
| E2/10 | compile | DotnetCel | 15.51 us | 15.53 us | 32.88 KB | 33.02 KB | 2.34 | yes |
| E2/10 | compile | Cel.NET | 52.81 us | 52.83 us | 71.99 KB | 72.14 KB | 7.96 | yes |
| E2/10 | compile | TelusCel | 12.44 us | 12.46 us | 15.52 KB | 15.66 KB | 1.88 | yes |
| E2/10 | compile | GreatPartsCel | 6.63 us | 6.66 us | 11.04 KB | 11.19 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 4.39 us | - | 2.96 KB | - | 2.07 | yes |
| E2/10 | evaluate | DotnetCel | 6.64 us | - | 4.07 KB | - | 3.13 | yes |
| E2/10 | evaluate | Cel.NET | 4.86 us | - | 6.47 KB | - | 2.29 | yes |
| E2/10 | evaluate | TelusCel | 90.92 us | - | 112.86 KB | - | 42.88 | yes |
| E2/10 | evaluate | GreatPartsCel | 2.12 us | - | 816 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 27.23 us | 27.26 us | 57.56 KB | 57.71 KB | 2.95 | yes |
| E2/10 | full cycle | DotnetCel | 27.95 us | 27.97 us | 40.35 KB | 40.50 KB | 3.02 | yes |
| E2/10 | full cycle | Cel.NET | 62.81 us | 62.84 us | 78.42 KB | 78.56 KB | 6.79 | yes |
| E2/10 | full cycle | TelusCel | 110.48 us | 110.50 us | 128.38 KB | 128.52 KB | 11.95 | yes |
| E2/10 | full cycle | GreatPartsCel | 9.25 us | 9.27 us | 11.86 KB | 12.00 KB | 1.00 |  |
| E2/100 | text creation | - | - | 24.1 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 21.32 us | 21.34 us | 54.57 KB | 54.72 KB | 3.22 | yes |
| E2/100 | compile | DotnetCel | 15.73 us | 15.76 us | 32.88 KB | 33.02 KB | 2.38 | yes |
| E2/100 | compile | Cel.NET | 53.04 us | 53.06 us | 71.99 KB | 72.14 KB | 8.02 | yes |
| E2/100 | compile | TelusCel | 12.23 us | 12.26 us | 15.52 KB | 15.66 KB | 1.85 | yes |
| E2/100 | compile | GreatPartsCel | 6.61 us | 6.64 us | 11.04 KB | 11.19 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 34.90 us | - | 21.66 KB | - | 1.57 | yes |
| E2/100 | evaluate | DotnetCel | 58.27 us | - | 31.91 KB | - | 2.63 | yes |
| E2/100 | evaluate | Cel.NET | 43.50 us | - | 57.41 KB | - | 1.96 | yes |
| E2/100 | evaluate | TelusCel | 887.16 us | - | 1.05 MB | - | 40.02 | yes |
| E2/100 | evaluate | GreatPartsCel | 22.17 us | - | 816 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 56.94 us | 56.97 us | 76.24 KB | 76.39 KB | 2.27 | yes |
| E2/100 | full cycle | DotnetCel | 83.67 us | 83.70 us | 68.18 KB | 68.33 KB | 3.33 | yes |
| E2/100 | full cycle | Cel.NET | 102.73 us | 102.75 us | 129.40 KB | 129.55 KB | 4.09 | yes |
| E2/100 | full cycle | TelusCel | 898.04 us | 898.06 us | 1.06 MB | 1.06 MB | 35.73 | yes |
| E2/100 | full cycle | GreatPartsCel | 25.14 us | 25.16 us | 11.85 KB | 12.00 KB | 1.00 |  |
| E3/10 | text creation | - | - | 23.4 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 17.85 us | 17.87 us | 44.35 KB | 44.50 KB | 3.38 | yes |
| E3/10 | compile | DotnetCel | 14.35 us | 14.37 us | 30.62 KB | 30.77 KB | 2.71 | yes |
| E3/10 | compile | Cel.NET | 40.76 us | 40.78 us | 53.08 KB | 53.23 KB | 7.71 | yes |
| E3/10 | compile | TelusCel | 10.12 us | 10.14 us | 13.39 KB | 13.54 KB | 1.91 | yes |
| E3/10 | compile | GreatPartsCel | 5.29 us | 5.31 us | 9.85 KB | 10.00 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 1.02 us | - | 1.61 KB | - | 1.94 | yes |
| E3/10 | evaluate | DotnetCel | 1.42 us | - | 1.78 KB | - | 2.69 | yes |
| E3/10 | evaluate | Cel.NET | 1.15 us | - | 2.12 KB | - | 2.18 | yes |
| E3/10 | evaluate | TelusCel | 53.13 us | - | 63.65 KB | - | 100.96 | yes |
| E3/10 | evaluate | GreatPartsCel | 526.2 ns | - | 784 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 19.81 us | 19.83 us | 45.86 KB | 46.01 KB | 3.23 | yes |
| E3/10 | full cycle | DotnetCel | 21.09 us | 21.11 us | 35.80 KB | 35.95 KB | 3.44 | yes |
| E3/10 | full cycle | Cel.NET | 44.32 us | 44.34 us | 55.19 KB | 55.33 KB | 7.23 | yes |
| E3/10 | full cycle | TelusCel | 66.22 us | 66.25 us | 77.04 KB | 77.19 KB | 10.80 | yes |
| E3/10 | full cycle | GreatPartsCel | 6.13 us | 6.15 us | 10.64 KB | 10.79 KB | 1.00 |  |
| E3/100 | text creation | - | - | 23.9 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 17.82 us | 17.85 us | 44.35 KB | 44.50 KB | 3.31 | yes |
| E3/100 | compile | DotnetCel | 14.52 us | 14.54 us | 30.62 KB | 30.77 KB | 2.69 | yes |
| E3/100 | compile | Cel.NET | 40.03 us | 40.05 us | 53.08 KB | 53.23 KB | 7.43 | yes |
| E3/100 | compile | TelusCel | 9.97 us | 10.00 us | 13.39 KB | 13.54 KB | 1.85 | yes |
| E3/100 | compile | GreatPartsCel | 5.39 us | 5.41 us | 9.85 KB | 10.00 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.57 us | - | 6.95 KB | - | 8.75 | yes |
| E3/100 | evaluate | DotnetCel | 4.65 us | - | 7.12 KB | - | 8.90 | yes |
| E3/100 | evaluate | Cel.NET | 2.56 us | - | 8.86 KB | - | 4.90 | yes |
| E3/100 | evaluate | TelusCel | 524.12 us | - | 578.05 KB | - | 1002.34 | yes |
| E3/100 | evaluate | GreatPartsCel | 522.9 ns | - | 784 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 23.31 us | 23.33 us | 51.17 KB | 51.31 KB | 3.80 | yes |
| E3/100 | full cycle | DotnetCel | 24.38 us | 24.40 us | 41.14 KB | 41.29 KB | 3.98 | yes |
| E3/100 | full cycle | Cel.NET | 45.71 us | 45.74 us | 61.93 KB | 62.08 KB | 7.46 | yes |
| E3/100 | full cycle | TelusCel | 505.49 us | 505.52 us | 591.44 KB | 591.59 KB | 82.44 | yes |
| E3/100 | full cycle | GreatPartsCel | 6.13 us | 6.16 us | 10.64 KB | 10.79 KB | 1.00 |  |
| E4/10 | text creation | - | - | 25.2 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 34.24 us | 34.26 us | 76.08 KB | 76.26 KB | 3.05 | yes |
| E4/10 | compile | DotnetCel | 21.21 us | 21.24 us | 38.16 KB | 38.34 KB | 1.89 | yes |
| E4/10 | compile | Cel.NET | 116.28 us | 116.30 us | 134.54 KB | 134.72 KB | 10.35 | yes |
| E4/10 | compile | TelusCel | 17.11 us | 17.13 us | 21.27 KB | 21.45 KB | 1.52 | yes |
| E4/10 | compile | GreatPartsCel | 11.24 us | 11.26 us | 17.35 KB | 17.53 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 3.89 us | - | 4.08 KB | - | 2.05 | yes |
| E4/10 | evaluate | DotnetCel | 6.09 us | - | 5.31 KB | - | 3.21 | yes |
| E4/10 | evaluate | Cel.NET | 5.47 us | - | 7.38 KB | - | 2.89 | yes |
| E4/10 | evaluate | TelusCel | 82.46 us | - | 99.98 KB | - | 43.51 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.90 us | - | 1.29 KB | - | 1.00 |  |
| E4/10 | full cycle | Celly | 41.21 us | 41.24 us | 80.16 KB | 80.34 KB | 2.91 | yes |
| E4/10 | full cycle | DotnetCel | 32.87 us | 32.89 us | 46.87 KB | 47.05 KB | 2.32 | yes |
| E4/10 | full cycle | Cel.NET | 128.23 us | 128.25 us | 141.92 KB | 142.10 KB | 9.05 | yes |
| E4/10 | full cycle | TelusCel | 102.74 us | 102.76 us | 121.26 KB | 121.44 KB | 7.25 | yes |
| E4/10 | full cycle | GreatPartsCel | 14.16 us | 14.19 us | 18.66 KB | 18.84 KB | 1.00 |  |
| E4/100 | text creation | - | - | 25.6 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 33.66 us | 33.68 us | 76.08 KB | 76.26 KB | 2.95 | yes |
| E4/100 | compile | DotnetCel | 21.44 us | 21.46 us | 38.16 KB | 38.34 KB | 1.88 | yes |
| E4/100 | compile | Cel.NET | 119.14 us | 119.16 us | 134.54 KB | 134.72 KB | 10.42 | yes |
| E4/100 | compile | TelusCel | 17.50 us | 17.53 us | 21.27 KB | 21.45 KB | 1.53 | yes |
| E4/100 | compile | GreatPartsCel | 11.43 us | 11.45 us | 17.35 KB | 17.53 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 36.72 us | - | 42.44 KB | - | 1.76 | yes |
| E4/100 | evaluate | DotnetCel | 70.70 us | - | 70.80 KB | - | 3.39 | yes |
| E4/100 | evaluate | Cel.NET | 64.46 us | - | 100.41 KB | - | 3.09 | yes |
| E4/100 | evaluate | TelusCel | 992.39 us | - | 1.16 MB | - | 47.60 | yes |
| E4/100 | evaluate | GreatPartsCel | 20.85 us | - | 10.04 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 81.38 us | 81.40 us | 118.50 KB | 118.68 KB | 2.67 | yes |
| E4/100 | full cycle | DotnetCel | 97.69 us | 97.72 us | 112.36 KB | 112.54 KB | 3.21 | yes |
| E4/100 | full cycle | Cel.NET | 188.86 us | 188.88 us | 234.96 KB | 235.13 KB | 6.21 | yes |
| E4/100 | full cycle | TelusCel | 1.02 ms | 1.02 ms | 1.18 MB | 1.18 MB | 33.50 | yes |
| E4/100 | full cycle | GreatPartsCel | 30.43 us | 30.45 us | 27.41 KB | 27.59 KB | 1.00 |  |
| E5 | text creation | - | - | 25.0 ns | - | 168 B | - | |
| E5 | compile | Celly | 12.67 us | 12.70 us | 35.54 KB | 35.71 KB | 4.62 | yes |
| E5 | compile | DotnetCel | 9.95 us | 9.98 us | 24.38 KB | 24.55 KB | 3.62 | yes |
| E5 | compile | Cel.NET | 21.64 us | 21.67 us | 30.65 KB | 30.81 KB | 7.88 | yes |
| E5 | compile | TelusCel | 6.54 us | 6.57 us | 8.66 KB | 8.82 KB | 2.38 | yes |
| E5 | compile | GreatPartsCel | 2.75 us | 2.77 us | 5.80 KB | 5.97 KB | 1.00 |  |
| E5 | evaluate | Celly | 602.1 ns | - | 752 B | - | 1.00 |  |
| E5 | evaluate | DotnetCel | 618.6 ns | - | 448 B | - | 1.03 |  |
| E5 | evaluate | Cel.NET | 80.72 us | - | 251.94 KB | - | 134.07 | yes |
| E5 | evaluate | TelusCel | 10.78 us | - | 14.41 KB | - | 17.90 | yes |
| E5 | evaluate | GreatPartsCel | 1.12 us | - | 616 B | - | 1.86 | yes |
| E5 | full cycle | Celly | 13.52 us | 13.55 us | 36.14 KB | 36.31 KB | 3.32 | yes |
| E5 | full cycle | DotnetCel | 14.42 us | 14.44 us | 28.04 KB | 28.20 KB | 3.54 | yes |
| E5 | full cycle | Cel.NET | 131.44 us | 131.47 us | 282.58 KB | 282.75 KB | 32.23 | yes |
| E5 | full cycle | TelusCel | 18.41 us | 18.44 us | 23.06 KB | 23.23 KB | 4.52 | yes |
| E5 | full cycle | GreatPartsCel | 4.08 us | 4.10 us | 6.43 KB | 6.59 KB | 1.00 |  |
| E6 | text creation | - | - | 24.7 ns | - | 184 B | - | |
| E6 | compile | Celly | 16.96 us | 16.98 us | 43.05 KB | 43.23 KB | 3.72 | yes |
| E6 | compile | Cel.NET | 43.48 us | 43.51 us | 50.60 KB | 50.78 KB | 9.55 | yes |
| E6 | compile | TelusCel | 11.44 us | 11.46 us | 13.98 KB | 14.16 KB | 2.51 | yes |
| E6 | compile | GreatPartsCel | 4.55 us | 4.58 us | 8.26 KB | 8.44 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 564.2 ns | - | 912 B | - | 1.62 | yes |
| E6 | evaluate | Cel.NET | 577.7 ns | - | 1.55 KB | - | 1.66 | yes |
| E6 | evaluate | TelusCel | 14.83 us | - | 18.44 KB | - | 42.49 | yes |
| E6 | evaluate | GreatPartsCel | 348.9 ns | - | 616 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 18.11 us | 18.13 us | 43.93 KB | 44.11 KB | 3.54 | yes |
| E6 | full cycle | Cel.NET | 44.12 us | 44.15 us | 52.16 KB | 52.34 KB | 8.62 | yes |
| E6 | full cycle | TelusCel | 27.78 us | 27.81 us | 32.42 KB | 32.60 KB | 5.43 | yes |
| E6 | full cycle | GreatPartsCel | 5.12 us | 5.14 us | 8.88 KB | 9.06 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 22.2 ns | - | 128 B | - | |
| E7 | compile | Celly | 16.73 us | 16.75 us | 45.37 KB | 45.49 KB | 4.11 | yes |
| E7 | compile | DotnetCel | 11.33 us | 11.35 us | 26.02 KB | 26.14 KB | 2.78 | yes |
| E7 | compile | Cel.NET | 41.19 us | 41.21 us | 52.71 KB | 52.84 KB | 10.13 | yes |
| E7 | compile | GreatPartsCel | 4.07 us | 4.09 us | 6.86 KB | 6.98 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 753.7 ns | - | 1.18 KB | - | 2.16 | yes |
| E7 | evaluate | DotnetCel | 444.4 ns | - | 592 B | - | 1.27 | yes |
| E7 | evaluate | Cel.NET | 713.7 ns | - | 1.59 KB | - | 2.05 | yes |
| E7 | evaluate | GreatPartsCel | 348.8 ns | - | 648 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 17.93 us | 17.96 us | 46.55 KB | 46.67 KB | 3.90 | yes |
| E7 | full cycle | DotnetCel | 14.50 us | 14.52 us | 29.17 KB | 29.29 KB | 3.15 | yes |
| E7 | full cycle | Cel.NET | 44.65 us | 44.67 us | 54.32 KB | 54.44 KB | 9.71 | yes |
| E7 | full cycle | GreatPartsCel | 4.60 us | 4.62 us | 7.50 KB | 7.63 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 25.2 ns | - | 192 B | - | |
| E8 | compile | Celly | 21.72 us | 21.75 us | 52.61 KB | 52.80 KB | 3.34 | yes |
| E8 | compile | Cel.NET | 88.30 us | 88.33 us | 99.36 KB | 99.54 KB | 13.57 | yes |
| E8 | compile | TelusCel | 15.30 us | 15.32 us | 19.41 KB | 19.59 KB | 2.35 | yes |
| E8 | compile | GreatPartsCel | 6.51 us | 6.53 us | 11.74 KB | 11.93 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 1.09 us | - | 1.66 KB | - | 1.83 | yes |
| E8 | evaluate | Cel.NET | 650.1 ns | - | 1.66 KB | - | 1.09 |  |
| E8 | evaluate | TelusCel | 12.54 us | - | 16.25 KB | - | 21.06 | yes |
| E8 | evaluate | GreatPartsCel | 595.4 ns | - | 904 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 24.33 us | 24.36 us | 54.15 KB | 54.34 KB | 3.29 | yes |
| E8 | full cycle | Cel.NET | 89.34 us | 89.37 us | 100.98 KB | 101.17 KB | 12.08 | yes |
| E8 | full cycle | TelusCel | 30.72 us | 30.75 us | 35.66 KB | 35.84 KB | 4.15 | yes |
| E8 | full cycle | GreatPartsCel | 7.40 us | 7.42 us | 12.65 KB | 12.84 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 23.0 ns | - | 136 B | - | |
| E9 | compile | Celly | 18.48 us | 18.50 us | 52.64 KB | 52.77 KB | 4.00 | yes |
| E9 | compile | DotnetCel | 11.54 us | 11.57 us | 27.05 KB | 27.18 KB | 2.50 | yes |
| E9 | compile | Cel.NET | 54.74 us | 54.77 us | 76.49 KB | 76.62 KB | 11.85 | yes |
| E9 | compile | TelusCel | 8.01 us | 8.03 us | 10.09 KB | 10.23 KB | 1.73 | yes |
| E9 | compile | GreatPartsCel | 4.62 us | 4.64 us | 9.19 KB | 9.32 KB | 1.00 |  |
| E9 | evaluate | Celly | 490.2 ns | - | 1.11 KB | - | 1.20 | yes |
| E9 | evaluate | DotnetCel | 552.0 ns | - | 808 B | - | 1.35 | yes |
| E9 | evaluate | Cel.NET | 642.8 ns | - | 1.73 KB | - | 1.57 | yes |
| E9 | evaluate | TelusCel | 8.27 us | - | 10.87 KB | - | 20.20 | yes |
| E9 | evaluate | GreatPartsCel | 409.3 ns | - | 800 B | - | 1.00 |  |
| E9 | full cycle | Celly | 19.59 us | 19.61 us | 53.74 KB | 53.88 KB | 3.66 | yes |
| E9 | full cycle | DotnetCel | 15.19 us | 15.21 us | 30.41 KB | 30.54 KB | 2.83 | yes |
| E9 | full cycle | Cel.NET | 57.15 us | 57.18 us | 78.21 KB | 78.34 KB | 10.67 | yes |
| E9 | full cycle | TelusCel | 17.49 us | 17.52 us | 20.96 KB | 21.09 KB | 3.26 | yes |
| E9 | full cycle | GreatPartsCel | 5.36 us | 5.38 us | 10.00 KB | 10.13 KB | 1.00 |  |
