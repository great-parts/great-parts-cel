## Run 1: scenario x operation

- Run 1: 2026-11-27T06:36:52.301Z - 2026-11-27T08:00:44.949Z; job: Default; runtime: JIT; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 20.1 ns | - | 160 B | - | |
| E1 | compile | Celly | 12.19 us | 12.21 us | 42.81 KB | 42.96 KB | 5.34 | yes |
| E1 | compile | DotnetCel | 9.14 us | 9.16 us | 26.31 KB | 26.47 KB | 4.00 | yes |
| E1 | compile | Cel.NET | 23.02 us | 23.04 us | 49.86 KB | 50.02 KB | 10.08 | yes |
| E1 | compile | TelusCel | 4.93 us | 4.95 us | 10.33 KB | 10.48 KB | 2.16 | yes |
| E1 | compile | GreatPartsCel | 3.30 us | 3.32 us | 8.98 KB | 9.13 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 2.28 us | 2.30 us | 6.98 KB | 7.14 KB | 1.00 |  |
| E1 | evaluate | Celly | 358.1 ns | - | 832 B | - | 1.96 | yes |
| E1 | evaluate | DotnetCel | 332.3 ns | - | 560 B | - | 1.82 | yes |
| E1 | evaluate | Cel.NET | 415.8 ns | - | 1.52 KB | - | 2.27 | yes |
| E1 | evaluate | TelusCel | 6.30 us | - | 11.16 KB | - | 34.44 | yes |
| E1 | evaluate | GreatPartsCel | 265.2 ns | - | 328 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 183.0 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 269.7 ns | - | 160 B | - | 1.00 |  |
| E1 | full cycle | Celly | 12.26 us | 12.28 us | 43.61 KB | 43.76 KB | 4.62 | yes |
| E1 | full cycle | DotnetCel | 11.74 us | 11.76 us | 29.37 KB | 29.52 KB | 4.42 | yes |
| E1 | full cycle | Cel.NET | 23.69 us | 23.71 us | 51.40 KB | 51.55 KB | 8.93 | yes |
| E1 | full cycle | TelusCel | 11.02 us | 11.04 us | 21.49 KB | 21.65 KB | 4.15 | yes |
| E1 | full cycle | GreatPartsCel | 3.70 us | 3.72 us | 9.30 KB | 9.46 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 2.65 us | 2.67 us | 7.30 KB | 7.46 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 3.13 us | 3.15 us | 7.55 KB | 7.71 KB | 1.00 |  |
| E2/10 | text creation | - | - | 20.4 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 15.76 us | 15.78 us | 54.53 KB | 54.68 KB | 3.39 | yes |
| E2/10 | compile | DotnetCel | 12.24 us | 12.26 us | 32.52 KB | 32.66 KB | 2.64 | yes |
| E2/10 | compile | Cel.NET | 30.72 us | 30.74 us | 71.99 KB | 72.14 KB | 6.61 | yes |
| E2/10 | compile | TelusCel | 7.73 us | 7.75 us | 15.50 KB | 15.65 KB | 1.66 | yes |
| E2/10 | compile | GreatPartsCel | 5.09 us | 5.11 us | 12.36 KB | 12.51 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 4.65 us | 4.67 us | 11.59 KB | 11.73 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 2.92 us | - | 2.96 KB | - | 2.42 | yes |
| E2/10 | evaluate | DotnetCel | 4.08 us | - | 4.07 KB | - | 3.38 | yes |
| E2/10 | evaluate | Cel.NET | 3.36 us | - | 6.15 KB | - | 2.79 | yes |
| E2/10 | evaluate | TelusCel | 62.25 us | - | 111.34 KB | - | 51.61 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.22 us | - | 376 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.21 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 1.28 us | - | 160 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 19.09 us | 19.11 us | 57.47 KB | 57.62 KB | 3.07 | yes |
| E2/10 | full cycle | DotnetCel | 19.39 us | 19.41 us | 39.90 KB | 40.05 KB | 3.12 | yes |
| E2/10 | full cycle | Cel.NET | 35.19 us | 35.21 us | 78.14 KB | 78.29 KB | 5.67 | yes |
| E2/10 | full cycle | TelusCel | 71.15 us | 71.17 us | 126.84 KB | 126.98 KB | 11.45 | yes |
| E2/10 | full cycle | GreatPartsCel | 6.76 us | 6.78 us | 12.72 KB | 12.87 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 6.21 us | 6.23 us | 11.95 KB | 12.10 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 6.34 us | 6.36 us | 12.12 KB | 12.27 KB | 1.00 |  |
| E2/100 | text creation | - | - | 21.3 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 15.83 us | 15.85 us | 54.53 KB | 54.68 KB | 3.42 | yes |
| E2/100 | compile | DotnetCel | 11.96 us | 11.98 us | 32.52 KB | 32.66 KB | 2.58 | yes |
| E2/100 | compile | Cel.NET | 30.58 us | 30.60 us | 71.98 KB | 72.13 KB | 6.60 | yes |
| E2/100 | compile | TelusCel | 7.63 us | 7.66 us | 15.50 KB | 15.65 KB | 1.65 | yes |
| E2/100 | compile | GreatPartsCel | 5.15 us | 5.17 us | 12.36 KB | 12.50 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 4.63 us | 4.65 us | 11.59 KB | 11.74 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 24.52 us | - | 21.66 KB | - | 2.33 | yes |
| E2/100 | evaluate | DotnetCel | 34.96 us | - | 31.91 KB | - | 3.32 | yes |
| E2/100 | evaluate | Cel.NET | 30.79 us | - | 54.28 KB | - | 2.93 | yes |
| E2/100 | evaluate | TelusCel | 624.18 us | - | 1.03 MB | - | 59.36 | yes |
| E2/100 | evaluate | GreatPartsCel | 10.52 us | - | 376 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 11.06 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 11.03 us | - | 160 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 41.81 us | 41.83 us | 76.12 KB | 76.26 KB | 2.73 | yes |
| E2/100 | full cycle | DotnetCel | 51.21 us | 51.23 us | 67.74 KB | 67.88 KB | 3.35 | yes |
| E2/100 | full cycle | Cel.NET | 63.41 us | 63.44 us | 126.26 KB | 126.41 KB | 4.15 | yes |
| E2/100 | full cycle | TelusCel | 579.56 us | 579.58 us | 1.05 MB | 1.05 MB | 37.89 | yes |
| E2/100 | full cycle | GreatPartsCel | 15.92 us | 15.94 us | 12.72 KB | 12.87 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 15.30 us | 15.32 us | 11.95 KB | 12.10 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 16.57 us | 16.59 us | 12.12 KB | 12.27 KB | 1.00 |  |
| E3/10 | text creation | - | - | 20.2 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 12.58 us | 12.60 us | 44.33 KB | 44.48 KB | 3.49 | yes |
| E3/10 | compile | DotnetCel | 11.11 us | 11.13 us | 30.26 KB | 30.41 KB | 3.08 | yes |
| E3/10 | compile | Cel.NET | 22.80 us | 22.82 us | 53.14 KB | 53.29 KB | 6.32 | yes |
| E3/10 | compile | TelusCel | 5.86 us | 5.88 us | 13.38 KB | 13.52 KB | 1.63 | yes |
| E3/10 | compile | GreatPartsCel | 4.16 us | 4.18 us | 10.95 KB | 11.10 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 3.61 us | 3.63 us | 10.34 KB | 10.49 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 766.0 ns | - | 1.61 KB | - | 2.88 | yes |
| E3/10 | evaluate | DotnetCel | 936.5 ns | - | 1.78 KB | - | 3.52 | yes |
| E3/10 | evaluate | Cel.NET | 778.5 ns | - | 2.07 KB | - | 2.92 | yes |
| E3/10 | evaluate | TelusCel | 33.64 us | - | 62.83 KB | - | 126.31 | yes |
| E3/10 | evaluate | GreatPartsCel | 356.8 ns | - | 376 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 266.4 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 333.8 ns | - | 160 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 13.95 us | 13.97 us | 45.94 KB | 46.09 KB | 3.34 | yes |
| E3/10 | full cycle | DotnetCel | 14.83 us | 14.85 us | 35.35 KB | 35.50 KB | 3.55 | yes |
| E3/10 | full cycle | Cel.NET | 23.99 us | 24.01 us | 55.21 KB | 55.36 KB | 5.74 | yes |
| E3/10 | full cycle | TelusCel | 41.45 us | 41.47 us | 76.20 KB | 76.35 KB | 9.92 | yes |
| E3/10 | full cycle | GreatPartsCel | 4.76 us | 4.78 us | 11.31 KB | 11.46 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 4.18 us | 4.20 us | 10.70 KB | 10.85 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 4.50 us | 4.52 us | 10.87 KB | 11.02 KB | 1.00 |  |
| E3/100 | text creation | - | - | 21.2 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 12.86 us | 12.88 us | 44.32 KB | 44.46 KB | 3.53 | yes |
| E3/100 | compile | DotnetCel | 10.51 us | 10.53 us | 30.26 KB | 30.41 KB | 2.88 | yes |
| E3/100 | compile | Cel.NET | 22.63 us | 22.65 us | 53.14 KB | 53.29 KB | 6.20 | yes |
| E3/100 | compile | TelusCel | 5.76 us | 5.78 us | 13.38 KB | 13.52 KB | 1.58 | yes |
| E3/100 | compile | GreatPartsCel | 4.21 us | 4.23 us | 10.94 KB | 11.09 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 3.65 us | 3.67 us | 10.33 KB | 10.48 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 3.74 us | - | 6.95 KB | - | 13.85 | yes |
| E3/100 | evaluate | DotnetCel | 3.14 us | - | 7.12 KB | - | 11.62 | yes |
| E3/100 | evaluate | Cel.NET | 2.05 us | - | 8.81 KB | - | 7.57 | yes |
| E3/100 | evaluate | TelusCel | 302.54 us | - | 570.20 KB | - | 1120.35 | yes |
| E3/100 | evaluate | GreatPartsCel | 333.2 ns | - | 376 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 270.0 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 344.1 ns | - | 160 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 16.98 us | 17.00 us | 51.26 KB | 51.41 KB | 3.92 | yes |
| E3/100 | full cycle | DotnetCel | 16.64 us | 16.66 us | 40.69 KB | 40.84 KB | 3.84 | yes |
| E3/100 | full cycle | Cel.NET | 25.02 us | 25.04 us | 61.93 KB | 62.08 KB | 5.78 | yes |
| E3/100 | full cycle | TelusCel | 347.15 us | 347.17 us | 583.57 KB | 583.72 KB | 80.14 | yes |
| E3/100 | full cycle | GreatPartsCel | 4.75 us | 4.77 us | 11.31 KB | 11.46 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 4.33 us | 4.35 us | 10.70 KB | 10.85 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 4.73 us | 4.76 us | 10.87 KB | 11.02 KB | 1.00 |  |
| E4/10 | text creation | - | - | 21.7 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 24.48 us | 24.50 us | 75.71 KB | 75.89 KB | 2.99 | yes |
| E4/10 | compile | DotnetCel | 14.80 us | 14.83 us | 37.80 KB | 37.98 KB | 1.81 | yes |
| E4/10 | compile | Cel.NET | 71.05 us | 71.08 us | 134.64 KB | 134.82 KB | 8.68 | yes |
| E4/10 | compile | TelusCel | 10.69 us | 10.71 us | 21.26 KB | 21.44 KB | 1.31 | yes |
| E4/10 | compile | GreatPartsCel | 8.64 us | 8.66 us | 19.00 KB | 19.18 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 8.19 us | 8.21 us | 18.46 KB | 18.64 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 2.90 us | - | 4.08 KB | - | 2.75 | yes |
| E4/10 | evaluate | DotnetCel | 3.93 us | - | 5.31 KB | - | 3.72 | yes |
| E4/10 | evaluate | Cel.NET | 3.97 us | - | 7.08 KB | - | 3.76 | yes |
| E4/10 | evaluate | TelusCel | 52.30 us | - | 98.86 KB | - | 49.47 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.13 us | - | 880 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.06 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 1.11 us | - | 664 B | - | 1.00 |  |
| E4/10 | full cycle | Celly | 27.84 us | 27.86 us | 79.79 KB | 79.97 KB | 2.89 | yes |
| E4/10 | full cycle | DotnetCel | 22.70 us | 22.72 us | 46.42 KB | 46.60 KB | 2.36 | yes |
| E4/10 | full cycle | Cel.NET | 75.99 us | 76.01 us | 141.72 KB | 141.90 KB | 7.89 | yes |
| E4/10 | full cycle | TelusCel | 65.05 us | 65.07 us | 120.12 KB | 120.30 KB | 6.75 | yes |
| E4/10 | full cycle | GreatPartsCel | 10.31 us | 10.33 us | 19.86 KB | 20.04 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 9.64 us | 9.66 us | 19.33 KB | 19.51 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 10.28 us | 10.31 us | 19.50 KB | 19.68 KB | 1.00 |  |
| E4/100 | text creation | - | - | 22.2 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 24.36 us | 24.38 us | 75.71 KB | 75.89 KB | 2.99 | yes |
| E4/100 | compile | DotnetCel | 15.64 us | 15.66 us | 37.80 KB | 37.98 KB | 1.92 | yes |
| E4/100 | compile | Cel.NET | 70.00 us | 70.03 us | 134.66 KB | 134.84 KB | 8.59 | yes |
| E4/100 | compile | TelusCel | 10.28 us | 10.31 us | 21.26 KB | 21.44 KB | 1.26 | yes |
| E4/100 | compile | GreatPartsCel | 8.68 us | 8.70 us | 19.00 KB | 19.18 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 8.15 us | 8.17 us | 18.46 KB | 18.64 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 30.18 us | - | 42.44 KB | - | 2.53 | yes |
| E4/100 | evaluate | DotnetCel | 47.87 us | - | 70.80 KB | - | 4.01 | yes |
| E4/100 | evaluate | Cel.NET | 46.90 us | - | 96.53 KB | - | 3.93 | yes |
| E4/100 | evaluate | TelusCel | 647.36 us | - | 1.15 MB | - | 54.18 | yes |
| E4/100 | evaluate | GreatPartsCel | 12.28 us | - | 9.61 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 11.95 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 12.40 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 53.94 us | 53.96 us | 118.16 KB | 118.33 KB | 2.68 | yes |
| E4/100 | full cycle | DotnetCel | 65.18 us | 65.21 us | 111.91 KB | 112.09 KB | 3.24 | yes |
| E4/100 | full cycle | Cel.NET | 119.56 us | 119.58 us | 231.19 KB | 231.37 KB | 5.93 | yes |
| E4/100 | full cycle | TelusCel | 670.01 us | 670.03 us | 1.17 MB | 1.17 MB | 33.25 | yes |
| E4/100 | full cycle | GreatPartsCel | 21.45 us | 21.47 us | 28.61 KB | 28.79 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 20.15 us | 20.17 us | 28.07 KB | 28.25 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 22.95 us | 22.97 us | 28.25 KB | 28.42 KB | 1.00 |  |
| E5 | text creation | - | - | 21.4 ns | - | 168 B | - | |
| E5 | compile | Celly | 9.75 us | 9.77 us | 35.56 KB | 35.73 KB | 5.88 | yes |
| E5 | compile | DotnetCel | 7.88 us | 7.90 us | 24.02 KB | 24.19 KB | 4.76 | yes |
| E5 | compile | Cel.NET | 13.04 us | 13.07 us | 30.60 KB | 30.76 KB | 7.87 | yes |
| E5 | compile | TelusCel | 3.81 us | 3.83 us | 8.64 KB | 8.80 KB | 2.30 | yes |
| E5 | compile | GreatPartsCel | 2.21 us | 2.23 us | 6.57 KB | 6.73 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 1.66 us | 1.68 us | 5.51 KB | 5.68 KB | 1.00 |  |
| E5 | evaluate | Celly | 376.8 ns | - | 752 B | - | 1.83 | yes |
| E5 | evaluate | DotnetCel | 380.6 ns | - | 448 B | - | 1.85 | yes |
| E5 | evaluate | Cel.NET | 71.90 us | - | 252.50 KB | - | 349.18 | yes |
| E5 | evaluate | TelusCel | 7.15 us | - | 14.34 KB | - | 34.73 | yes |
| E5 | evaluate | GreatPartsCel | 284.5 ns | - | 328 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 205.9 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 238.1 ns | - | 160 B | - | 1.00 |  |
| E5 | full cycle | Celly | 10.31 us | 10.33 us | 36.26 KB | 36.43 KB | 4.87 | yes |
| E5 | full cycle | DotnetCel | 10.61 us | 10.63 us | 27.60 KB | 27.77 KB | 5.01 | yes |
| E5 | full cycle | Cel.NET | 89.66 us | 89.68 us | 283.10 KB | 283.26 KB | 42.37 | yes |
| E5 | full cycle | TelusCel | 11.19 us | 11.21 us | 22.98 KB | 23.15 KB | 5.29 | yes |
| E5 | full cycle | GreatPartsCel | 2.73 us | 2.75 us | 6.89 KB | 7.05 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.12 us | 2.14 us | 5.83 KB | 6.00 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 2.60 us | 2.62 us | 6.05 KB | 6.22 KB | 1.00 |  |
| E6 | text creation | - | - | 22.7 ns | - | 184 B | - | |
| E6 | compile | Celly | 12.43 us | 12.46 us | 42.95 KB | 43.13 KB | 5.13 | yes |
| E6 | compile | Cel.NET | 25.27 us | 25.29 us | 50.46 KB | 50.64 KB | 10.42 | yes |
| E6 | compile | TelusCel | 6.63 us | 6.65 us | 13.97 KB | 14.15 KB | 2.73 | yes |
| E6 | compile | GreatPartsCel | 3.67 us | 3.70 us | 9.34 KB | 9.52 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 2.43 us | 2.45 us | 6.48 KB | 6.66 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 363.4 ns | - | 912 B | - | 1.81 | yes |
| E6 | evaluate | Cel.NET | 431.8 ns | - | 1.57 KB | - | 2.16 | yes |
| E6 | evaluate | TelusCel | 9.36 us | - | 18.30 KB | - | 46.73 | yes |
| E6 | evaluate | GreatPartsCel | 257.4 ns | - | 328 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 200.2 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 307.7 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 12.87 us | 12.89 us | 43.86 KB | 44.04 KB | 4.55 | yes |
| E6 | full cycle | Cel.NET | 25.96 us | 25.99 us | 52.03 KB | 52.21 KB | 9.18 | yes |
| E6 | full cycle | TelusCel | 18.38 us | 18.40 us | 32.27 KB | 32.45 KB | 6.50 | yes |
| E6 | full cycle | GreatPartsCel | 4.19 us | 4.21 us | 9.66 KB | 9.84 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 2.83 us | 2.85 us | 6.79 KB | 6.97 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 3.52 us | 3.55 us | 7.08 KB | 7.26 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 21.0 ns | - | 128 B | - | |
| E7 | compile | Celly | 12.86 us | 12.88 us | 45.14 KB | 45.26 KB | 4.72 | yes |
| E7 | compile | DotnetCel | 8.50 us | 8.52 us | 25.66 KB | 25.78 KB | 3.12 | yes |
| E7 | compile | Cel.NET | 24.18 us | 24.20 us | 53.70 KB | 53.83 KB | 8.87 | yes |
| E7 | compile | GreatPartsCel | 3.05 us | 3.08 us | 7.67 KB | 7.79 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 2.73 us | 2.75 us | 7.22 KB | 7.35 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 737.1 ns | - | 1.17 KB | - | 3.48 | yes |
| E7 | evaluate | DotnetCel | 328.4 ns | - | 592 B | - | 1.55 | yes |
| E7 | evaluate | Cel.NET | 527.1 ns | - | 1.61 KB | - | 2.49 | yes |
| E7 | evaluate | GreatPartsCel | 276.0 ns | - | 328 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 211.9 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 246.8 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 13.57 us | 13.59 us | 46.32 KB | 46.45 KB | 4.81 | yes |
| E7 | full cycle | DotnetCel | 10.49 us | 10.51 us | 28.74 KB | 28.87 KB | 3.72 | yes |
| E7 | full cycle | Cel.NET | 25.09 us | 25.11 us | 55.31 KB | 55.44 KB | 8.89 | yes |
| E7 | full cycle | GreatPartsCel | 3.48 us | 3.50 us | 7.99 KB | 8.12 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 2.82 us | 2.84 us | 7.54 KB | 7.66 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 3.59 us | 3.61 us | 7.52 KB | 7.65 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 23.3 ns | - | 192 B | - | |
| E8 | compile | Celly | 15.52 us | 15.54 us | 52.39 KB | 52.58 KB | 4.23 | yes |
| E8 | compile | Cel.NET | 49.56 us | 49.58 us | 99.23 KB | 99.42 KB | 13.51 | yes |
| E8 | compile | TelusCel | 9.48 us | 9.50 us | 19.39 KB | 19.58 KB | 2.58 | yes |
| E8 | compile | GreatPartsCel | 4.64 us | 4.66 us | 12.66 KB | 12.84 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 3.67 us | 3.69 us | 10.89 KB | 11.08 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 781.4 ns | - | 1.66 KB | - | 3.47 | yes |
| E8 | evaluate | Cel.NET | 463.9 ns | - | 1.64 KB | - | 2.06 | yes |
| E8 | evaluate | TelusCel | 7.98 us | - | 16.02 KB | - | 35.49 | yes |
| E8 | evaluate | GreatPartsCel | 293.2 ns | - | 376 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 224.9 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 291.6 ns | - | 160 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 16.73 us | 16.75 us | 54.19 KB | 54.38 KB | 4.18 | yes |
| E8 | full cycle | Cel.NET | 50.23 us | 50.26 us | 100.90 KB | 101.08 KB | 12.54 | yes |
| E8 | full cycle | TelusCel | 17.95 us | 17.97 us | 35.41 KB | 35.60 KB | 4.48 | yes |
| E8 | full cycle | GreatPartsCel | 5.24 us | 5.26 us | 13.02 KB | 13.21 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 4.01 us | 4.03 us | 11.25 KB | 11.44 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 5.26 us | 5.28 us | 11.46 KB | 11.64 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 20.7 ns | - | 136 B | - | |
| E9 | compile | Celly | 14.90 us | 14.92 us | 52.28 KB | 52.41 KB | 5.49 | yes |
| E9 | compile | DotnetCel | 7.88 us | 7.90 us | 26.69 KB | 26.82 KB | 2.90 | yes |
| E9 | compile | Cel.NET | 34.38 us | 34.40 us | 77.43 KB | 77.56 KB | 12.67 | yes |
| E9 | compile | TelusCel | 4.70 us | 4.72 us | 10.08 KB | 10.21 KB | 1.73 | yes |
| E9 | compile | GreatPartsCel | 3.56 us | 3.58 us | 10.12 KB | 10.26 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 2.71 us | 2.73 us | 8.44 KB | 8.57 KB | 1.00 |  |
| E9 | evaluate | Celly | 368.9 ns | - | 1.11 KB | - | 1.57 | yes |
| E9 | evaluate | DotnetCel | 373.8 ns | - | 808 B | - | 1.59 | yes |
| E9 | evaluate | Cel.NET | 444.5 ns | - | 1.75 KB | - | 1.89 | yes |
| E9 | evaluate | TelusCel | 6.20 us | - | 10.77 KB | - | 26.37 | yes |
| E9 | evaluate | GreatPartsCel | 309.8 ns | - | 512 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 235.1 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 335.1 ns | - | 344 B | - | 1.00 |  |
| E9 | full cycle | Celly | 15.47 us | 15.49 us | 53.49 KB | 53.63 KB | 4.72 | yes |
| E9 | full cycle | DotnetCel | 10.38 us | 10.40 us | 29.98 KB | 30.12 KB | 3.17 | yes |
| E9 | full cycle | Cel.NET | 35.56 us | 35.58 us | 79.30 KB | 79.44 KB | 10.85 | yes |
| E9 | full cycle | TelusCel | 10.20 us | 10.22 us | 20.85 KB | 20.98 KB | 3.11 | yes |
| E9 | full cycle | GreatPartsCel | 4.18 us | 4.20 us | 10.62 KB | 10.75 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 3.28 us | 3.30 us | 8.94 KB | 9.07 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 4.08 us | 4.10 us | 9.18 KB | 9.32 KB | 1.00 |  |
