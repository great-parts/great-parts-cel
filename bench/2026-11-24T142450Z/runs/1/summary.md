## Run 1: scenario x operation

- Run 1: 2026-11-24T10:16:26.818Z - 2026-11-24T11:31:45.773Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 25.8 ns | - | 160 B | - | |
| E1 | compile | Celly | 15.64 us | 15.66 us | 42.84 KB | 43.00 KB | 4.23 | yes |
| E1 | compile | DotnetCel | 11.21 us | 11.23 us | 26.67 KB | 26.83 KB | 3.03 | yes |
| E1 | compile | Cel.NET | 34.45 us | 34.47 us | 49.87 KB | 50.03 KB | 9.31 | yes |
| E1 | compile | TelusCel | 7.27 us | 7.30 us | 10.34 KB | 10.50 KB | 1.97 | yes |
| E1 | compile | GreatPartsCel | 4.64 us | 4.67 us | 8.56 KB | 8.72 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.70 us | 3.72 us | 6.97 KB | 7.13 KB | 1.00 |  |
| E1 | evaluate | Celly | 496.5 ns | - | 832 B | - | 1.73 | yes |
| E1 | evaluate | DotnetCel | 454.7 ns | - | 560 B | - | 1.59 | yes |
| E1 | evaluate | Cel.NET | 525.8 ns | - | 1.51 KB | - | 1.83 | yes |
| E1 | evaluate | TelusCel | 7.92 us | - | 11.28 KB | - | 27.64 | yes |
| E1 | evaluate | GreatPartsCel | 392.3 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 286.6 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 2.10 us | - | 7.27 KB | - | 4.62 | yes |
| E1 | full cycle | Celly | 16.73 us | 16.75 us | 43.64 KB | 43.80 KB | 4.17 | yes |
| E1 | full cycle | DotnetCel | 14.74 us | 14.77 us | 29.79 KB | 29.95 KB | 3.67 | yes |
| E1 | full cycle | Cel.NET | 36.14 us | 36.16 us | 51.44 KB | 51.59 KB | 9.00 | yes |
| E1 | full cycle | TelusCel | 16.24 us | 16.26 us | 21.63 KB | 21.78 KB | 4.05 | yes |
| E1 | full cycle | GreatPartsCel | 5.18 us | 5.20 us | 8.87 KB | 9.03 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 4.01 us | 4.04 us | 7.28 KB | 7.43 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 6.56 us | 6.58 us | 14.51 KB | 14.66 KB | 1.00 |  |
| E2/10 | text creation | - | - | 24.7 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 21.74 us | 21.77 us | 54.58 KB | 54.73 KB | 3.25 | yes |
| E2/10 | compile | DotnetCel | 15.23 us | 15.26 us | 32.88 KB | 33.02 KB | 2.28 | yes |
| E2/10 | compile | Cel.NET | 46.33 us | 46.35 us | 71.99 KB | 72.14 KB | 6.93 | yes |
| E2/10 | compile | TelusCel | 11.88 us | 11.90 us | 15.52 KB | 15.66 KB | 1.78 | yes |
| E2/10 | compile | GreatPartsCel | 7.43 us | 7.46 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 6.69 us | 6.71 us | 11.57 KB | 11.71 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 3.82 us | - | 2.96 KB | - | 2.04 | yes |
| E2/10 | evaluate | DotnetCel | 5.65 us | - | 4.07 KB | - | 3.01 | yes |
| E2/10 | evaluate | Cel.NET | 4.82 us | - | 6.47 KB | - | 2.57 | yes |
| E2/10 | evaluate | TelusCel | 87.65 us | - | 112.86 KB | - | 46.73 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.98 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.88 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 5.02 us | - | 7.27 KB | - | 1.31 | yes |
| E2/10 | full cycle | Celly | 26.31 us | 26.33 us | 57.54 KB | 57.69 KB | 2.73 | yes |
| E2/10 | full cycle | DotnetCel | 26.37 us | 26.39 us | 40.35 KB | 40.50 KB | 2.74 | yes |
| E2/10 | full cycle | Cel.NET | 53.75 us | 53.77 us | 78.43 KB | 78.58 KB | 5.57 | yes |
| E2/10 | full cycle | TelusCel | 97.75 us | 97.78 us | 128.38 KB | 128.52 KB | 10.14 | yes |
| E2/10 | full cycle | GreatPartsCel | 9.89 us | 9.92 us | 12.57 KB | 12.72 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 9.64 us | 9.67 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 13.18 us | 13.21 us | 19.24 KB | 19.39 KB | 1.00 |  |
| E2/100 | text creation | - | - | 25.2 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 21.79 us | 21.81 us | 54.57 KB | 54.72 KB | 3.16 | yes |
| E2/100 | compile | DotnetCel | 15.46 us | 15.49 us | 32.88 KB | 33.02 KB | 2.24 | yes |
| E2/100 | compile | Cel.NET | 46.55 us | 46.57 us | 71.99 KB | 72.14 KB | 6.75 | yes |
| E2/100 | compile | TelusCel | 11.68 us | 11.71 us | 15.52 KB | 15.66 KB | 1.69 | yes |
| E2/100 | compile | GreatPartsCel | 7.21 us | 7.23 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 6.90 us | 6.92 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 32.89 us | - | 21.66 KB | - | 1.96 | yes |
| E2/100 | evaluate | DotnetCel | 50.19 us | - | 31.91 KB | - | 3.00 | yes |
| E2/100 | evaluate | Cel.NET | 42.51 us | - | 57.41 KB | - | 2.54 | yes |
| E2/100 | evaluate | TelusCel | 823.34 us | - | 1.05 MB | - | 49.15 | yes |
| E2/100 | evaluate | GreatPartsCel | 16.75 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 17.26 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 39.24 us | - | 52.98 KB | - | 1.19 | yes |
| E2/100 | full cycle | Celly | 53.73 us | 53.75 us | 76.18 KB | 76.33 KB | 2.22 | yes |
| E2/100 | full cycle | DotnetCel | 72.79 us | 72.81 us | 68.20 KB | 68.34 KB | 3.00 | yes |
| E2/100 | full cycle | Cel.NET | 94.32 us | 94.35 us | 129.39 KB | 129.54 KB | 3.89 | yes |
| E2/100 | full cycle | TelusCel | 851.41 us | 851.44 us | 1.06 MB | 1.06 MB | 35.11 | yes |
| E2/100 | full cycle | GreatPartsCel | 24.25 us | 24.28 us | 12.58 KB | 12.72 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 24.40 us | 24.43 us | 11.93 KB | 12.08 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 48.03 us | 48.05 us | 64.94 KB | 65.09 KB | 1.00 |  |
| E3/10 | text creation | - | - | 25.8 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 18.42 us | 18.44 us | 44.29 KB | 44.43 KB | 3.23 | yes |
| E3/10 | compile | DotnetCel | 14.01 us | 14.03 us | 30.62 KB | 30.77 KB | 2.46 | yes |
| E3/10 | compile | Cel.NET | 35.94 us | 35.96 us | 53.07 KB | 53.22 KB | 6.30 | yes |
| E3/10 | compile | TelusCel | 9.41 us | 9.44 us | 13.39 KB | 13.54 KB | 1.65 | yes |
| E3/10 | compile | GreatPartsCel | 6.21 us | 6.24 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 5.70 us | 5.73 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 1.04 us | - | 1.57 KB | - | 2.58 | yes |
| E3/10 | evaluate | DotnetCel | 1.29 us | - | 1.78 KB | - | 3.21 | yes |
| E3/10 | evaluate | Cel.NET | 1.04 us | - | 2.12 KB | - | 2.59 | yes |
| E3/10 | evaluate | TelusCel | 48.80 us | - | 63.65 KB | - | 120.81 | yes |
| E3/10 | evaluate | GreatPartsCel | 499.7 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 403.9 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 2.28 us | - | 7.27 KB | - | 2.19 | yes |
| E3/10 | full cycle | Celly | 20.44 us | 20.47 us | 45.92 KB | 46.07 KB | 3.28 | yes |
| E3/10 | full cycle | DotnetCel | 19.84 us | 19.87 us | 35.80 KB | 35.95 KB | 3.18 | yes |
| E3/10 | full cycle | Cel.NET | 38.37 us | 38.40 us | 55.16 KB | 55.31 KB | 6.15 | yes |
| E3/10 | full cycle | TelusCel | 59.95 us | 59.98 us | 77.04 KB | 77.19 KB | 9.61 | yes |
| E3/10 | full cycle | GreatPartsCel | 6.90 us | 6.92 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 6.24 us | 6.27 us | 10.68 KB | 10.82 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 8.90 us | 8.93 us | 17.69 KB | 17.84 KB | 1.00 |  |
| E3/100 | text creation | - | - | 26.1 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 18.43 us | 18.45 us | 44.34 KB | 44.49 KB | 3.22 | yes |
| E3/100 | compile | DotnetCel | 14.04 us | 14.07 us | 30.62 KB | 30.77 KB | 2.46 | yes |
| E3/100 | compile | Cel.NET | 36.14 us | 36.17 us | 53.08 KB | 53.22 KB | 6.32 | yes |
| E3/100 | compile | TelusCel | 9.38 us | 9.40 us | 13.39 KB | 13.54 KB | 1.64 | yes |
| E3/100 | compile | GreatPartsCel | 6.11 us | 6.13 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 5.72 us | 5.75 us | 10.32 KB | 10.46 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.52 us | - | 6.91 KB | - | 11.00 | yes |
| E3/100 | evaluate | DotnetCel | 4.29 us | - | 7.12 KB | - | 10.45 | yes |
| E3/100 | evaluate | Cel.NET | 2.59 us | - | 8.86 KB | - | 6.31 | yes |
| E3/100 | evaluate | TelusCel | 445.08 us | - | 578.05 KB | - | 1083.42 | yes |
| E3/100 | evaluate | GreatPartsCel | 500.9 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 410.8 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 11.01 us | - | 52.98 KB | - | 4.24 | yes |
| E3/100 | full cycle | Celly | 24.19 us | 24.21 us | 51.20 KB | 51.35 KB | 3.91 | yes |
| E3/100 | full cycle | DotnetCel | 23.20 us | 23.23 us | 41.14 KB | 41.29 KB | 3.75 | yes |
| E3/100 | full cycle | Cel.NET | 40.59 us | 40.62 us | 61.94 KB | 62.09 KB | 6.57 | yes |
| E3/100 | full cycle | TelusCel | 466.80 us | 466.82 us | 591.44 KB | 591.59 KB | 75.52 | yes |
| E3/100 | full cycle | GreatPartsCel | 6.87 us | 6.90 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 6.18 us | 6.21 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 17.76 us | 17.78 us | 63.39 KB | 63.54 KB | 1.00 |  |
| E4/10 | text creation | - | - | 27.3 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 33.81 us | 33.83 us | 76.08 KB | 76.26 KB | 2.86 | yes |
| E4/10 | compile | DotnetCel | 20.02 us | 20.05 us | 38.16 KB | 38.34 KB | 1.70 | yes |
| E4/10 | compile | Cel.NET | 101.49 us | 101.52 us | 134.54 KB | 134.72 KB | 8.60 | yes |
| E4/10 | compile | TelusCel | 16.04 us | 16.07 us | 21.27 KB | 21.45 KB | 1.36 | yes |
| E4/10 | compile | GreatPartsCel | 12.05 us | 12.08 us | 18.72 KB | 18.90 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 11.80 us | 11.83 us | 18.32 KB | 18.50 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 3.82 us | - | 4.05 KB | - | 2.10 | yes |
| E4/10 | evaluate | DotnetCel | 6.63 us | - | 5.31 KB | - | 3.65 | yes |
| E4/10 | evaluate | Cel.NET | 5.24 us | - | 7.38 KB | - | 2.89 | yes |
| E4/10 | evaluate | TelusCel | 77.14 us | - | 99.98 KB | - | 42.47 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.85 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.82 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 4.59 us | - | 7.77 KB | - | 1.20 | yes |
| E4/10 | full cycle | Celly | 39.95 us | 39.98 us | 80.15 KB | 80.33 KB | 2.82 | yes |
| E4/10 | full cycle | DotnetCel | 30.77 us | 30.80 us | 46.87 KB | 47.05 KB | 2.17 | yes |
| E4/10 | full cycle | Cel.NET | 111.65 us | 111.68 us | 141.93 KB | 142.11 KB | 7.89 | yes |
| E4/10 | full cycle | TelusCel | 93.62 us | 93.65 us | 121.26 KB | 121.44 KB | 6.61 | yes |
| E4/10 | full cycle | GreatPartsCel | 14.53 us | 14.55 us | 19.57 KB | 19.75 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 14.16 us | 14.19 us | 19.17 KB | 19.35 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 18.15 us | 18.18 us | 25.29 KB | 25.47 KB | 1.00 |  |
| E4/100 | text creation | - | - | 27.5 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 33.76 us | 33.79 us | 76.07 KB | 76.25 KB | 2.91 | yes |
| E4/100 | compile | DotnetCel | 20.14 us | 20.16 us | 38.16 KB | 38.34 KB | 1.73 | yes |
| E4/100 | compile | Cel.NET | 105.14 us | 105.17 us | 134.78 KB | 134.96 KB | 9.06 | yes |
| E4/100 | compile | TelusCel | 16.00 us | 16.03 us | 21.27 KB | 21.45 KB | 1.38 | yes |
| E4/100 | compile | GreatPartsCel | 12.15 us | 12.18 us | 18.72 KB | 18.90 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 11.61 us | 11.63 us | 18.32 KB | 18.50 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 37.11 us | - | 42.41 KB | - | 1.99 | yes |
| E4/100 | evaluate | DotnetCel | 65.92 us | - | 70.80 KB | - | 3.54 | yes |
| E4/100 | evaluate | Cel.NET | 63.42 us | - | 100.41 KB | - | 3.40 | yes |
| E4/100 | evaluate | TelusCel | 904.72 us | - | 1.16 MB | - | 48.54 | yes |
| E4/100 | evaluate | GreatPartsCel | 19.06 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 18.64 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 41.89 us | - | 62.22 KB | - | 1.13 | yes |
| E4/100 | full cycle | Celly | 72.89 us | 72.92 us | 118.51 KB | 118.69 KB | 2.32 | yes |
| E4/100 | full cycle | DotnetCel | 90.89 us | 90.92 us | 112.36 KB | 112.54 KB | 2.90 | yes |
| E4/100 | full cycle | Cel.NET | 171.01 us | 171.04 us | 234.96 KB | 235.13 KB | 5.45 | yes |
| E4/100 | full cycle | TelusCel | 929.07 us | 929.09 us | 1.18 MB | 1.18 MB | 29.61 | yes |
| E4/100 | full cycle | GreatPartsCel | 31.37 us | 31.40 us | 28.32 KB | 28.50 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 32.98 us | 33.01 us | 27.92 KB | 28.10 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 55.22 us | 55.25 us | 79.74 KB | 79.92 KB | 1.00 |  |
| E5 | text creation | - | - | 26.9 ns | - | 168 B | - | |
| E5 | compile | Celly | 13.09 us | 13.12 us | 35.54 KB | 35.71 KB | 4.81 | yes |
| E5 | compile | DotnetCel | 9.63 us | 9.66 us | 24.38 KB | 24.55 KB | 3.54 | yes |
| E5 | compile | Cel.NET | 20.60 us | 20.63 us | 30.65 KB | 30.81 KB | 7.57 | yes |
| E5 | compile | TelusCel | 5.69 us | 5.72 us | 8.66 KB | 8.82 KB | 2.09 | yes |
| E5 | compile | GreatPartsCel | 3.17 us | 3.19 us | 6.43 KB | 6.60 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.72 us | 2.75 us | 5.50 KB | 5.66 KB | 1.00 |  |
| E5 | evaluate | Celly | 538.5 ns | - | 752 B | - | 1.68 | yes |
| E5 | evaluate | DotnetCel | 570.2 ns | - | 448 B | - | 1.78 | yes |
| E5 | evaluate | Cel.NET | 75.51 us | - | 251.94 KB | - | 235.17 | yes |
| E5 | evaluate | TelusCel | 9.76 us | - | 14.41 KB | - | 30.39 | yes |
| E5 | evaluate | GreatPartsCel | 410.5 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 321.1 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 2.12 us | - | 7.27 KB | - | 3.93 | yes |
| E5 | full cycle | Celly | 14.13 us | 14.16 us | 36.25 KB | 36.42 KB | 4.50 | yes |
| E5 | full cycle | DotnetCel | 14.47 us | 14.49 us | 28.04 KB | 28.20 KB | 4.61 | yes |
| E5 | full cycle | Cel.NET | 115.50 us | 115.53 us | 282.59 KB | 282.75 KB | 36.77 | yes |
| E5 | full cycle | TelusCel | 16.96 us | 16.99 us | 23.06 KB | 23.23 KB | 5.40 | yes |
| E5 | full cycle | GreatPartsCel | 3.84 us | 3.86 us | 6.74 KB | 6.91 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 3.14 us | 3.17 us | 5.81 KB | 5.97 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 5.58 us | 5.61 us | 12.87 KB | 13.04 KB | 1.00 |  |
| E6 | text creation | - | - | 26.5 ns | - | 184 B | - | |
| E6 | compile | Celly | 16.74 us | 16.77 us | 43.04 KB | 43.22 KB | 4.25 | yes |
| E6 | compile | Cel.NET | 37.94 us | 37.97 us | 50.60 KB | 50.78 KB | 9.63 | yes |
| E6 | compile | TelusCel | 10.29 us | 10.31 us | 13.98 KB | 14.16 KB | 2.61 | yes |
| E6 | compile | GreatPartsCel | 5.17 us | 5.19 us | 9.18 KB | 9.36 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.94 us | 3.97 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 549.7 ns | - | 912 B | - | 1.85 | yes |
| E6 | evaluate | Cel.NET | 545.0 ns | - | 1.55 KB | - | 1.84 | yes |
| E6 | evaluate | TelusCel | 13.77 us | - | 18.44 KB | - | 46.41 | yes |
| E6 | evaluate | GreatPartsCel | 380.2 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 296.8 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 2.06 us | - | 7.27 KB | - | 3.78 | yes |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 18.10 us | 18.13 us | 43.94 KB | 44.12 KB | 4.15 | yes |
| E6 | full cycle | Cel.NET | 39.18 us | 39.21 us | 52.15 KB | 52.33 KB | 8.98 | yes |
| E6 | full cycle | TelusCel | 25.17 us | 25.20 us | 32.42 KB | 32.60 KB | 5.77 | yes |
| E6 | full cycle | GreatPartsCel | 5.75 us | 5.78 us | 9.49 KB | 9.67 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 4.37 us | 4.39 us | 6.77 KB | 6.95 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 6.59 us | 6.62 us | 14.01 KB | 14.19 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 24.1 ns | - | 128 B | - | |
| E7 | compile | Celly | 16.92 us | 16.94 us | 45.38 KB | 45.50 KB | 3.99 | yes |
| E7 | compile | DotnetCel | 11.10 us | 11.13 us | 26.02 KB | 26.14 KB | 2.62 | yes |
| E7 | compile | Cel.NET | 36.73 us | 36.76 us | 52.71 KB | 52.84 KB | 8.67 | yes |
| E7 | compile | GreatPartsCel | 4.29 us | 4.32 us | 7.51 KB | 7.63 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 4.23 us | 4.26 us | 7.08 KB | 7.20 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 703.2 ns | - | 1.18 KB | - | 2.59 | yes |
| E7 | evaluate | DotnetCel | 450.4 ns | - | 592 B | - | 1.66 | yes |
| E7 | evaluate | Cel.NET | 735.8 ns | - | 1.59 KB | - | 2.71 | yes |
| E7 | evaluate | GreatPartsCel | 368.9 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 271.9 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 2.11 us | - | 7.27 KB | - | 4.69 | yes |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 18.53 us | 18.55 us | 46.54 KB | 46.66 KB | 4.28 | yes |
| E7 | full cycle | DotnetCel | 14.30 us | 14.32 us | 29.17 KB | 29.29 KB | 3.30 | yes |
| E7 | full cycle | Cel.NET | 39.11 us | 39.13 us | 54.31 KB | 54.44 KB | 9.04 | yes |
| E7 | full cycle | GreatPartsCel | 4.81 us | 4.84 us | 7.82 KB | 7.94 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 4.33 us | 4.35 us | 7.39 KB | 7.52 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 6.90 us | 6.92 us | 14.48 KB | 14.60 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 27.9 ns | - | 192 B | - | |
| E8 | compile | Celly | 21.76 us | 21.78 us | 52.62 KB | 52.81 KB | 3.60 | yes |
| E8 | compile | Cel.NET | 75.10 us | 75.12 us | 99.36 KB | 99.54 KB | 12.43 | yes |
| E8 | compile | TelusCel | 14.62 us | 14.65 us | 19.41 KB | 19.59 KB | 2.42 | yes |
| E8 | compile | GreatPartsCel | 6.85 us | 6.87 us | 12.51 KB | 12.70 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 6.04 us | 6.07 us | 10.88 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 1.08 us | - | 1.66 KB | - | 3.11 | yes |
| E8 | evaluate | Cel.NET | 613.0 ns | - | 1.66 KB | - | 1.77 | yes |
| E8 | evaluate | TelusCel | 11.86 us | - | 16.25 KB | - | 34.21 | yes |
| E8 | evaluate | GreatPartsCel | 442.9 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 346.8 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 2.20 us | - | 7.27 KB | - | 3.59 | yes |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 23.31 us | 23.34 us | 54.15 KB | 54.33 KB | 3.53 | yes |
| E8 | full cycle | Cel.NET | 75.86 us | 75.89 us | 100.98 KB | 101.17 KB | 11.49 | yes |
| E8 | full cycle | TelusCel | 28.34 us | 28.36 us | 35.66 KB | 35.84 KB | 4.29 | yes |
| E8 | full cycle | GreatPartsCel | 7.70 us | 7.73 us | 12.86 KB | 13.05 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 6.60 us | 6.63 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 9.11 us | 9.14 us | 18.41 KB | 18.60 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 24.4 ns | - | 136 B | - | |
| E9 | compile | Celly | 19.23 us | 19.26 us | 52.65 KB | 52.78 KB | 4.42 | yes |
| E9 | compile | DotnetCel | 11.10 us | 11.12 us | 27.05 KB | 27.18 KB | 2.55 | yes |
| E9 | compile | Cel.NET | 48.69 us | 48.72 us | 76.35 KB | 76.48 KB | 11.19 | yes |
| E9 | compile | TelusCel | 7.19 us | 7.22 us | 10.09 KB | 10.23 KB | 1.65 | yes |
| E9 | compile | GreatPartsCel | 5.25 us | 5.27 us | 9.97 KB | 10.11 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 4.35 us | 4.37 us | 8.42 KB | 8.55 KB | 1.00 |  |
| E9 | evaluate | Celly | 506.8 ns | - | 1.11 KB | - | 1.51 | yes |
| E9 | evaluate | DotnetCel | 501.8 ns | - | 808 B | - | 1.50 | yes |
| E9 | evaluate | Cel.NET | 552.7 ns | - | 1.73 KB | - | 1.65 | yes |
| E9 | evaluate | TelusCel | 7.53 us | - | 10.87 KB | - | 22.48 | yes |
| E9 | evaluate | GreatPartsCel | 461.1 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 334.8 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 2.15 us | - | 7.45 KB | - | 4.28 | yes |
| E9 | full cycle | Celly | 20.96 us | 20.99 us | 53.74 KB | 53.88 KB | 4.34 | yes |
| E9 | full cycle | DotnetCel | 14.87 us | 14.89 us | 30.41 KB | 30.54 KB | 3.08 | yes |
| E9 | full cycle | Cel.NET | 49.71 us | 49.74 us | 78.21 KB | 78.34 KB | 10.30 | yes |
| E9 | full cycle | TelusCel | 15.85 us | 15.87 us | 20.96 KB | 21.09 KB | 3.28 | yes |
| E9 | full cycle | GreatPartsCel | 5.85 us | 5.87 us | 10.47 KB | 10.60 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.82 us | 4.85 us | 8.91 KB | 9.05 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 7.38 us | 7.41 us | 16.14 KB | 16.27 KB | 1.00 |  |
