## Run 2: scenario x operation

- Run 2: 2026-11-27T08:00:44.949Z - 2026-11-27T09:29:16.569Z; job: Default; runtime: JIT; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 22.7 ns | - | 160 B | - | |
| E1 | compile | Celly | 11.03 us | 11.05 us | 42.83 KB | 42.99 KB | 4.91 | yes |
| E1 | compile | DotnetCel | 8.84 us | 8.87 us | 26.31 KB | 26.47 KB | 3.94 | yes |
| E1 | compile | Cel.NET | 21.95 us | 21.97 us | 49.87 KB | 50.03 KB | 9.77 | yes |
| E1 | compile | TelusCel | 5.04 us | 5.06 us | 10.33 KB | 10.48 KB | 2.24 | yes |
| E1 | compile | GreatPartsCel | 3.13 us | 3.16 us | 8.97 KB | 9.13 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 2.25 us | 2.27 us | 6.98 KB | 7.14 KB | 1.00 |  |
| E1 | evaluate | Celly | 339.2 ns | - | 832 B | - | 1.65 | yes |
| E1 | evaluate | DotnetCel | 333.8 ns | - | 560 B | - | 1.62 | yes |
| E1 | evaluate | Cel.NET | 384.8 ns | - | 1.52 KB | - | 1.87 | yes |
| E1 | evaluate | TelusCel | 5.81 us | - | 11.16 KB | - | 28.23 | yes |
| E1 | evaluate | GreatPartsCel | 248.1 ns | - | 328 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 206.0 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 272.4 ns | - | 160 B | - | 1.00 |  |
| E1 | full cycle | Celly | 10.28 us | 10.31 us | 43.62 KB | 43.78 KB | 3.79 | yes |
| E1 | full cycle | DotnetCel | 11.49 us | 11.51 us | 29.37 KB | 29.52 KB | 4.24 | yes |
| E1 | full cycle | Cel.NET | 21.41 us | 21.44 us | 51.38 KB | 51.54 KB | 7.89 | yes |
| E1 | full cycle | TelusCel | 11.18 us | 11.20 us | 21.49 KB | 21.65 KB | 4.12 | yes |
| E1 | full cycle | GreatPartsCel | 3.76 us | 3.78 us | 9.30 KB | 9.46 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 2.71 us | 2.74 us | 7.30 KB | 7.46 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 2.94 us | 2.97 us | 7.56 KB | 7.71 KB | 1.00 |  |
| E2/10 | text creation | - | - | 21.8 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 12.87 us | 12.89 us | 54.55 KB | 54.70 KB | 2.81 | yes |
| E2/10 | compile | DotnetCel | 12.00 us | 12.02 us | 32.52 KB | 32.66 KB | 2.62 | yes |
| E2/10 | compile | Cel.NET | 27.96 us | 27.98 us | 71.98 KB | 72.13 KB | 6.11 | yes |
| E2/10 | compile | TelusCel | 7.77 us | 7.79 us | 15.50 KB | 15.65 KB | 1.70 | yes |
| E2/10 | compile | GreatPartsCel | 4.76 us | 4.79 us | 12.36 KB | 12.51 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 4.57 us | 4.60 us | 11.59 KB | 11.73 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 2.73 us | - | 2.96 KB | - | 2.31 | yes |
| E2/10 | evaluate | DotnetCel | 4.07 us | - | 4.07 KB | - | 3.44 | yes |
| E2/10 | evaluate | Cel.NET | 3.24 us | - | 6.15 KB | - | 2.74 | yes |
| E2/10 | evaluate | TelusCel | 62.28 us | - | 111.34 KB | - | 52.68 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.26 us | - | 376 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.18 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 1.28 us | - | 160 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 17.27 us | 17.29 us | 57.48 KB | 57.63 KB | 2.79 | yes |
| E2/10 | full cycle | DotnetCel | 19.26 us | 19.28 us | 39.90 KB | 40.05 KB | 3.11 | yes |
| E2/10 | full cycle | Cel.NET | 30.17 us | 30.19 us | 78.13 KB | 78.28 KB | 4.87 | yes |
| E2/10 | full cycle | TelusCel | 71.90 us | 71.93 us | 126.84 KB | 126.98 KB | 11.61 | yes |
| E2/10 | full cycle | GreatPartsCel | 6.66 us | 6.68 us | 12.73 KB | 12.88 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 6.38 us | 6.40 us | 11.95 KB | 12.10 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 6.20 us | 6.22 us | 12.12 KB | 12.27 KB | 1.00 |  |
| E2/100 | text creation | - | - | 22.6 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 14.23 us | 14.26 us | 54.51 KB | 54.66 KB | 3.01 | yes |
| E2/100 | compile | DotnetCel | 12.14 us | 12.16 us | 32.52 KB | 32.66 KB | 2.57 | yes |
| E2/100 | compile | Cel.NET | 26.69 us | 26.71 us | 71.99 KB | 72.13 KB | 5.65 | yes |
| E2/100 | compile | TelusCel | 7.80 us | 7.82 us | 15.50 KB | 15.65 KB | 1.65 | yes |
| E2/100 | compile | GreatPartsCel | 4.90 us | 4.92 us | 12.36 KB | 12.50 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 4.72 us | 4.75 us | 11.59 KB | 11.73 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 24.40 us | - | 21.66 KB | - | 2.48 | yes |
| E2/100 | evaluate | DotnetCel | 36.31 us | - | 31.91 KB | - | 3.69 | yes |
| E2/100 | evaluate | Cel.NET | 28.69 us | - | 54.28 KB | - | 2.92 | yes |
| E2/100 | evaluate | TelusCel | 588.23 us | - | 1.03 MB | - | 59.84 | yes |
| E2/100 | evaluate | GreatPartsCel | 10.25 us | - | 376 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 9.83 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 10.78 us | - | 160 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 40.43 us | 40.45 us | 76.18 KB | 76.33 KB | 2.56 | yes |
| E2/100 | full cycle | DotnetCel | 49.68 us | 49.70 us | 67.74 KB | 67.88 KB | 3.15 | yes |
| E2/100 | full cycle | Cel.NET | 59.47 us | 59.49 us | 126.27 KB | 126.42 KB | 3.77 | yes |
| E2/100 | full cycle | TelusCel | 610.08 us | 610.10 us | 1.05 MB | 1.05 MB | 38.68 | yes |
| E2/100 | full cycle | GreatPartsCel | 15.98 us | 16.00 us | 12.72 KB | 12.87 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 15.77 us | 15.80 us | 11.95 KB | 12.10 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 15.79 us | 15.81 us | 12.12 KB | 12.27 KB | 1.00 |  |
| E3/10 | text creation | - | - | 20.7 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 10.78 us | 10.81 us | 44.33 KB | 44.48 KB | 2.86 | yes |
| E3/10 | compile | DotnetCel | 11.02 us | 11.04 us | 30.26 KB | 30.41 KB | 2.93 | yes |
| E3/10 | compile | Cel.NET | 20.43 us | 20.45 us | 53.12 KB | 53.27 KB | 5.43 | yes |
| E3/10 | compile | TelusCel | 6.30 us | 6.32 us | 13.38 KB | 13.52 KB | 1.67 | yes |
| E3/10 | compile | GreatPartsCel | 4.06 us | 4.08 us | 10.95 KB | 11.09 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 3.77 us | 3.79 us | 10.33 KB | 10.48 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 700.1 ns | - | 1.61 KB | - | 2.49 | yes |
| E3/10 | evaluate | DotnetCel | 902.6 ns | - | 1.78 KB | - | 3.21 | yes |
| E3/10 | evaluate | Cel.NET | 678.8 ns | - | 2.07 KB | - | 2.41 | yes |
| E3/10 | evaluate | TelusCel | 38.54 us | - | 62.83 KB | - | 136.93 | yes |
| E3/10 | evaluate | GreatPartsCel | 336.1 ns | - | 376 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 281.5 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 338.1 ns | - | 160 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 11.80 us | 11.82 us | 45.93 KB | 46.07 KB | 2.79 | yes |
| E3/10 | full cycle | DotnetCel | 14.79 us | 14.81 us | 35.35 KB | 35.50 KB | 3.50 | yes |
| E3/10 | full cycle | Cel.NET | 22.40 us | 22.42 us | 55.21 KB | 55.36 KB | 5.31 | yes |
| E3/10 | full cycle | TelusCel | 46.37 us | 46.39 us | 76.20 KB | 76.35 KB | 10.98 | yes |
| E3/10 | full cycle | GreatPartsCel | 4.67 us | 4.69 us | 11.31 KB | 11.46 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 4.32 us | 4.34 us | 10.70 KB | 10.85 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 4.22 us | 4.24 us | 10.88 KB | 11.02 KB | 1.00 |  |
| E3/100 | text creation | - | - | 22.4 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 10.75 us | 10.77 us | 44.33 KB | 44.48 KB | 2.90 | yes |
| E3/100 | compile | DotnetCel | 10.17 us | 10.19 us | 30.26 KB | 30.41 KB | 2.74 | yes |
| E3/100 | compile | Cel.NET | 21.89 us | 21.91 us | 53.13 KB | 53.27 KB | 5.90 | yes |
| E3/100 | compile | TelusCel | 6.34 us | 6.36 us | 13.38 KB | 13.52 KB | 1.71 | yes |
| E3/100 | compile | GreatPartsCel | 3.98 us | 4.00 us | 10.94 KB | 11.09 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 3.71 us | 3.73 us | 10.34 KB | 10.49 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 3.32 us | - | 6.95 KB | - | 12.55 | yes |
| E3/100 | evaluate | DotnetCel | 3.37 us | - | 7.12 KB | - | 12.74 | yes |
| E3/100 | evaluate | Cel.NET | 1.93 us | - | 8.81 KB | - | 7.29 | yes |
| E3/100 | evaluate | TelusCel | 326.35 us | - | 570.20 KB | - | 1233.74 | yes |
| E3/100 | evaluate | GreatPartsCel | 341.6 ns | - | 376 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 264.5 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 332.5 ns | - | 160 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 15.46 us | 15.48 us | 51.34 KB | 51.49 KB | 3.69 | yes |
| E3/100 | full cycle | DotnetCel | 17.14 us | 17.16 us | 40.69 KB | 40.84 KB | 4.09 | yes |
| E3/100 | full cycle | Cel.NET | 23.20 us | 23.23 us | 61.94 KB | 62.09 KB | 5.53 | yes |
| E3/100 | full cycle | TelusCel | 368.68 us | 368.70 us | 583.57 KB | 583.72 KB | 87.89 | yes |
| E3/100 | full cycle | GreatPartsCel | 4.64 us | 4.66 us | 11.31 KB | 11.46 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 4.22 us | 4.24 us | 10.70 KB | 10.85 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 4.19 us | 4.22 us | 10.88 KB | 11.02 KB | 1.00 |  |
| E4/10 | text creation | - | - | 24.1 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 21.98 us | 22.01 us | 75.71 KB | 75.89 KB | 2.65 | yes |
| E4/10 | compile | DotnetCel | 15.54 us | 15.57 us | 37.80 KB | 37.98 KB | 1.88 | yes |
| E4/10 | compile | Cel.NET | 67.65 us | 67.68 us | 134.65 KB | 134.83 KB | 8.17 | yes |
| E4/10 | compile | TelusCel | 10.78 us | 10.81 us | 21.26 KB | 21.44 KB | 1.30 | yes |
| E4/10 | compile | GreatPartsCel | 8.62 us | 8.64 us | 19.00 KB | 19.18 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 8.29 us | 8.31 us | 18.46 KB | 18.64 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 2.84 us | - | 4.08 KB | - | 2.68 | yes |
| E4/10 | evaluate | DotnetCel | 3.91 us | - | 5.31 KB | - | 3.69 | yes |
| E4/10 | evaluate | Cel.NET | 3.87 us | - | 7.08 KB | - | 3.65 | yes |
| E4/10 | evaluate | TelusCel | 54.44 us | - | 98.86 KB | - | 51.45 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.11 us | - | 880 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.06 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 1.07 us | - | 664 B | - | 1.00 |  |
| E4/10 | full cycle | Celly | 23.65 us | 23.67 us | 79.79 KB | 79.97 KB | 2.56 | yes |
| E4/10 | full cycle | DotnetCel | 22.80 us | 22.82 us | 46.42 KB | 46.60 KB | 2.47 | yes |
| E4/10 | full cycle | Cel.NET | 71.25 us | 71.27 us | 141.72 KB | 141.90 KB | 7.72 | yes |
| E4/10 | full cycle | TelusCel | 67.31 us | 67.34 us | 120.12 KB | 120.30 KB | 7.29 | yes |
| E4/10 | full cycle | GreatPartsCel | 10.02 us | 10.05 us | 19.86 KB | 20.04 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 9.79 us | 9.82 us | 19.32 KB | 19.50 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 9.23 us | 9.25 us | 19.50 KB | 19.68 KB | 1.00 |  |
| E4/100 | text creation | - | - | 24.7 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 20.75 us | 20.78 us | 75.71 KB | 75.89 KB | 2.63 | yes |
| E4/100 | compile | DotnetCel | 15.73 us | 15.75 us | 37.80 KB | 37.98 KB | 2.00 | yes |
| E4/100 | compile | Cel.NET | 66.99 us | 67.01 us | 134.64 KB | 134.82 KB | 8.51 | yes |
| E4/100 | compile | TelusCel | 10.61 us | 10.64 us | 21.26 KB | 21.44 KB | 1.35 | yes |
| E4/100 | compile | GreatPartsCel | 8.53 us | 8.56 us | 19.00 KB | 19.18 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 7.88 us | 7.90 us | 18.47 KB | 18.65 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 26.16 us | - | 42.44 KB | - | 2.37 | yes |
| E4/100 | evaluate | DotnetCel | 45.98 us | - | 70.80 KB | - | 4.16 | yes |
| E4/100 | evaluate | Cel.NET | 45.51 us | - | 96.53 KB | - | 4.12 | yes |
| E4/100 | evaluate | TelusCel | 658.07 us | - | 1.15 MB | - | 59.57 | yes |
| E4/100 | evaluate | GreatPartsCel | 11.63 us | - | 9.61 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 11.05 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 11.74 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 51.87 us | 51.90 us | 118.14 KB | 118.32 KB | 2.51 | yes |
| E4/100 | full cycle | DotnetCel | 62.64 us | 62.66 us | 111.91 KB | 112.09 KB | 3.03 | yes |
| E4/100 | full cycle | Cel.NET | 119.71 us | 119.73 us | 231.18 KB | 231.36 KB | 5.80 | yes |
| E4/100 | full cycle | TelusCel | 685.69 us | 685.71 us | 1.17 MB | 1.17 MB | 33.20 | yes |
| E4/100 | full cycle | GreatPartsCel | 20.73 us | 20.75 us | 28.61 KB | 28.79 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 20.66 us | 20.68 us | 28.08 KB | 28.26 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 21.43 us | 21.45 us | 28.25 KB | 28.43 KB | 1.00 |  |
| E5 | text creation | - | - | 21.4 ns | - | 168 B | - | |
| E5 | compile | Celly | 7.97 us | 7.99 us | 35.54 KB | 35.71 KB | 4.47 | yes |
| E5 | compile | DotnetCel | 6.99 us | 7.01 us | 24.02 KB | 24.19 KB | 3.92 | yes |
| E5 | compile | Cel.NET | 12.40 us | 12.42 us | 30.60 KB | 30.76 KB | 6.96 | yes |
| E5 | compile | TelusCel | 3.92 us | 3.95 us | 8.64 KB | 8.80 KB | 2.20 | yes |
| E5 | compile | GreatPartsCel | 2.17 us | 2.19 us | 6.57 KB | 6.73 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 1.78 us | 1.80 us | 5.52 KB | 5.68 KB | 1.00 |  |
| E5 | evaluate | Celly | 371.8 ns | - | 752 B | - | 1.83 | yes |
| E5 | evaluate | DotnetCel | 373.5 ns | - | 448 B | - | 1.84 | yes |
| E5 | evaluate | Cel.NET | 61.29 us | - | 252.50 KB | - | 302.03 | yes |
| E5 | evaluate | TelusCel | 7.28 us | - | 14.34 KB | - | 35.89 | yes |
| E5 | evaluate | GreatPartsCel | 282.4 ns | - | 328 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 202.9 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 241.7 ns | - | 160 B | - | 1.00 |  |
| E5 | full cycle | Celly | 9.25 us | 9.27 us | 36.26 KB | 36.43 KB | 4.25 | yes |
| E5 | full cycle | DotnetCel | 10.14 us | 10.16 us | 27.60 KB | 27.77 KB | 4.66 | yes |
| E5 | full cycle | Cel.NET | 76.48 us | 76.50 us | 283.08 KB | 283.24 KB | 35.15 | yes |
| E5 | full cycle | TelusCel | 11.59 us | 11.61 us | 22.98 KB | 23.15 KB | 5.33 | yes |
| E5 | full cycle | GreatPartsCel | 2.67 us | 2.69 us | 6.89 KB | 7.05 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.18 us | 2.20 us | 5.83 KB | 6.00 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 2.33 us | 2.36 us | 6.05 KB | 6.22 KB | 1.00 |  |
| E6 | text creation | - | - | 24.5 ns | - | 184 B | - | |
| E6 | compile | Celly | 11.39 us | 11.42 us | 42.99 KB | 43.17 KB | 4.34 | yes |
| E6 | compile | Cel.NET | 23.94 us | 23.96 us | 50.46 KB | 50.64 KB | 9.12 | yes |
| E6 | compile | TelusCel | 6.72 us | 6.74 us | 13.97 KB | 14.15 KB | 2.56 | yes |
| E6 | compile | GreatPartsCel | 3.71 us | 3.73 us | 9.34 KB | 9.52 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 2.62 us | 2.65 us | 6.47 KB | 6.65 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 350.1 ns | - | 912 B | - | 1.72 | yes |
| E6 | evaluate | Cel.NET | 377.3 ns | - | 1.57 KB | - | 1.85 | yes |
| E6 | evaluate | TelusCel | 9.54 us | - | 18.30 KB | - | 46.75 | yes |
| E6 | evaluate | GreatPartsCel | 257.0 ns | - | 328 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 204.1 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 298.3 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 12.42 us | 12.45 us | 43.84 KB | 44.02 KB | 4.17 | yes |
| E6 | full cycle | Cel.NET | 24.81 us | 24.84 us | 52.04 KB | 52.22 KB | 8.33 | yes |
| E6 | full cycle | TelusCel | 17.51 us | 17.54 us | 32.27 KB | 32.45 KB | 5.88 | yes |
| E6 | full cycle | GreatPartsCel | 4.13 us | 4.15 us | 9.66 KB | 9.84 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 2.98 us | 3.00 us | 6.80 KB | 6.98 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 3.27 us | 3.29 us | 7.08 KB | 7.26 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 20.9 ns | - | 128 B | - | |
| E7 | compile | Celly | 12.61 us | 12.63 us | 45.15 KB | 45.28 KB | 4.73 | yes |
| E7 | compile | DotnetCel | 8.10 us | 8.12 us | 25.66 KB | 25.78 KB | 3.03 | yes |
| E7 | compile | Cel.NET | 23.76 us | 23.78 us | 53.69 KB | 53.81 KB | 8.90 | yes |
| E7 | compile | GreatPartsCel | 3.05 us | 3.07 us | 7.67 KB | 7.79 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 2.67 us | 2.69 us | 7.22 KB | 7.34 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 721.0 ns | - | 1.17 KB | - | 3.24 | yes |
| E7 | evaluate | DotnetCel | 330.4 ns | - | 592 B | - | 1.48 | yes |
| E7 | evaluate | Cel.NET | 483.6 ns | - | 1.61 KB | - | 2.17 | yes |
| E7 | evaluate | GreatPartsCel | 277.4 ns | - | 328 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 222.8 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 244.8 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 13.59 us | 13.61 us | 46.32 KB | 46.45 KB | 4.52 | yes |
| E7 | full cycle | DotnetCel | 11.30 us | 11.32 us | 28.74 KB | 28.87 KB | 3.76 | yes |
| E7 | full cycle | Cel.NET | 22.96 us | 22.98 us | 55.29 KB | 55.42 KB | 7.63 | yes |
| E7 | full cycle | GreatPartsCel | 3.50 us | 3.52 us | 7.99 KB | 8.12 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 3.01 us | 3.03 us | 7.54 KB | 7.66 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 3.36 us | 3.38 us | 7.52 KB | 7.65 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 27.3 ns | - | 192 B | - | |
| E8 | compile | Celly | 15.45 us | 15.48 us | 52.50 KB | 52.69 KB | 4.25 | yes |
| E8 | compile | Cel.NET | 46.39 us | 46.42 us | 99.24 KB | 99.43 KB | 12.75 | yes |
| E8 | compile | TelusCel | 9.29 us | 9.31 us | 19.39 KB | 19.58 KB | 2.55 | yes |
| E8 | compile | GreatPartsCel | 4.84 us | 4.87 us | 12.66 KB | 12.84 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 3.64 us | 3.67 us | 10.89 KB | 11.07 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 775.9 ns | - | 1.66 KB | - | 3.70 | yes |
| E8 | evaluate | Cel.NET | 425.0 ns | - | 1.64 KB | - | 2.03 | yes |
| E8 | evaluate | TelusCel | 8.27 us | - | 16.02 KB | - | 39.47 | yes |
| E8 | evaluate | GreatPartsCel | 292.1 ns | - | 376 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 209.5 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 298.8 ns | - | 160 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 16.54 us | 16.57 us | 54.17 KB | 54.35 KB | 4.20 | yes |
| E8 | full cycle | Cel.NET | 49.03 us | 49.06 us | 100.87 KB | 101.06 KB | 12.44 | yes |
| E8 | full cycle | TelusCel | 19.66 us | 19.69 us | 35.41 KB | 35.60 KB | 4.99 | yes |
| E8 | full cycle | GreatPartsCel | 5.39 us | 5.42 us | 13.02 KB | 13.21 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 3.94 us | 3.97 us | 11.26 KB | 11.45 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 4.92 us | 4.94 us | 11.46 KB | 11.64 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 25.7 ns | - | 136 B | - | |
| E9 | compile | Celly | 14.39 us | 14.41 us | 52.28 KB | 52.41 KB | 5.56 | yes |
| E9 | compile | DotnetCel | 8.65 us | 8.67 us | 26.69 KB | 26.82 KB | 3.34 | yes |
| E9 | compile | Cel.NET | 32.97 us | 32.99 us | 77.57 KB | 77.70 KB | 12.74 | yes |
| E9 | compile | TelusCel | 4.81 us | 4.83 us | 10.08 KB | 10.21 KB | 1.86 | yes |
| E9 | compile | GreatPartsCel | 3.80 us | 3.83 us | 10.13 KB | 10.26 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 2.59 us | 2.61 us | 8.44 KB | 8.57 KB | 1.00 |  |
| E9 | evaluate | Celly | 370.9 ns | - | 1.11 KB | - | 1.74 | yes |
| E9 | evaluate | DotnetCel | 407.3 ns | - | 808 B | - | 1.91 | yes |
| E9 | evaluate | Cel.NET | 414.1 ns | - | 1.75 KB | - | 1.94 | yes |
| E9 | evaluate | TelusCel | 6.14 us | - | 10.77 KB | - | 28.85 | yes |
| E9 | evaluate | GreatPartsCel | 307.4 ns | - | 512 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 212.9 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 315.0 ns | - | 344 B | - | 1.00 |  |
| E9 | full cycle | Celly | 14.28 us | 14.30 us | 53.50 KB | 53.63 KB | 4.76 | yes |
| E9 | full cycle | DotnetCel | 10.84 us | 10.87 us | 29.98 KB | 30.12 KB | 3.61 | yes |
| E9 | full cycle | Cel.NET | 34.21 us | 34.23 us | 79.19 KB | 79.32 KB | 11.40 | yes |
| E9 | full cycle | TelusCel | 10.79 us | 10.81 us | 20.85 KB | 20.98 KB | 3.60 | yes |
| E9 | full cycle | GreatPartsCel | 4.29 us | 4.32 us | 10.63 KB | 10.76 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 3.00 us | 3.03 us | 8.94 KB | 9.07 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 3.99 us | 4.02 us | 9.19 KB | 9.32 KB | 1.00 |  |
