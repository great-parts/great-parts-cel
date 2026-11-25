## Run 2: scenario x operation

- Run 2: 2026-11-25T16:23:02.897Z - 2026-11-25T17:44:13.322Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 33.3 ns | - | 160 B | - | |
| E1 | compile | Celly | 14.62 us | 14.65 us | 42.79 KB | 42.95 KB | 4.63 | yes |
| E1 | compile | DotnetCel | 10.88 us | 10.91 us | 26.31 KB | 26.47 KB | 3.44 | yes |
| E1 | compile | Cel.NET | 28.26 us | 28.30 us | 49.87 KB | 50.03 KB | 8.94 | yes |
| E1 | compile | TelusCel | 6.38 us | 6.41 us | 10.33 KB | 10.48 KB | 2.02 | yes |
| E1 | compile | GreatPartsCel | 4.11 us | 4.15 us | 8.83 KB | 8.98 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.16 us | 3.19 us | 6.97 KB | 7.12 KB | 1.00 |  |
| E1 | evaluate | Celly | 459.0 ns | - | 832 B | - | 2.01 | yes |
| E1 | evaluate | DotnetCel | 431.5 ns | - | 560 B | - | 1.88 | yes |
| E1 | evaluate | Cel.NET | 516.7 ns | - | 1.52 KB | - | 2.26 | yes |
| E1 | evaluate | TelusCel | 7.15 us | - | 11.16 KB | - | 31.22 | yes |
| E1 | evaluate | GreatPartsCel | 292.4 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 228.9 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 415.4 ns | - | 160 B | - | 1.00 |  |
| E1 | full cycle | Celly | 15.11 us | 15.15 us | 43.60 KB | 43.76 KB | 4.17 | yes |
| E1 | full cycle | DotnetCel | 14.11 us | 14.14 us | 29.37 KB | 29.52 KB | 3.89 | yes |
| E1 | full cycle | Cel.NET | 29.11 us | 29.14 us | 51.38 KB | 51.54 KB | 8.03 | yes |
| E1 | full cycle | TelusCel | 14.16 us | 14.19 us | 21.49 KB | 21.65 KB | 3.91 | yes |
| E1 | full cycle | GreatPartsCel | 4.61 us | 4.64 us | 9.14 KB | 9.30 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 3.63 us | 3.66 us | 7.28 KB | 7.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 4.26 us | 4.30 us | 7.39 KB | 7.55 KB | 1.00 |  |
| E2/10 | text creation | - | - | 31.9 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 19.07 us | 19.11 us | 54.45 KB | 54.60 KB | 3.20 | yes |
| E2/10 | compile | DotnetCel | 14.89 us | 14.92 us | 32.52 KB | 32.66 KB | 2.50 | yes |
| E2/10 | compile | Cel.NET | 38.05 us | 38.08 us | 71.98 KB | 72.13 KB | 6.39 | yes |
| E2/10 | compile | TelusCel | 9.79 us | 9.83 us | 15.50 KB | 15.65 KB | 1.64 | yes |
| E2/10 | compile | GreatPartsCel | 6.27 us | 6.31 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 5.96 us | 5.99 us | 11.57 KB | 11.71 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 3.55 us | - | 2.96 KB | - | 2.48 | yes |
| E2/10 | evaluate | DotnetCel | 5.20 us | - | 4.07 KB | - | 3.63 | yes |
| E2/10 | evaluate | Cel.NET | 4.37 us | - | 6.15 KB | - | 3.05 | yes |
| E2/10 | evaluate | TelusCel | 81.19 us | - | 111.34 KB | - | 56.65 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.62 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.43 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 2.08 us | - | 800 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 23.60 us | 23.63 us | 57.47 KB | 57.62 KB | 3.01 | yes |
| E2/10 | full cycle | DotnetCel | 23.93 us | 23.96 us | 39.90 KB | 40.05 KB | 3.05 | yes |
| E2/10 | full cycle | Cel.NET | 43.14 us | 43.17 us | 78.14 KB | 78.29 KB | 5.50 | yes |
| E2/10 | full cycle | TelusCel | 102.62 us | 102.65 us | 126.84 KB | 126.98 KB | 13.08 | yes |
| E2/10 | full cycle | GreatPartsCel | 8.34 us | 8.37 us | 12.57 KB | 12.72 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 7.84 us | 7.88 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 9.04 us | 9.07 us | 12.61 KB | 12.75 KB | 1.00 |  |
| E2/100 | text creation | - | - | 32.8 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 18.98 us | 19.01 us | 54.45 KB | 54.59 KB | 3.19 | yes |
| E2/100 | compile | DotnetCel | 14.65 us | 14.68 us | 32.52 KB | 32.66 KB | 2.46 | yes |
| E2/100 | compile | Cel.NET | 37.97 us | 38.01 us | 71.99 KB | 72.14 KB | 6.38 | yes |
| E2/100 | compile | TelusCel | 9.78 us | 9.81 us | 15.50 KB | 15.65 KB | 1.64 | yes |
| E2/100 | compile | GreatPartsCel | 6.27 us | 6.31 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 5.95 us | 5.98 us | 11.57 KB | 11.71 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 33.15 us | - | 21.66 KB | - | 2.60 | yes |
| E2/100 | evaluate | DotnetCel | 47.53 us | - | 31.91 KB | - | 3.72 | yes |
| E2/100 | evaluate | Cel.NET | 38.97 us | - | 54.28 KB | - | 3.05 | yes |
| E2/100 | evaluate | TelusCel | 770.86 us | - | 1.03 MB | - | 60.40 | yes |
| E2/100 | evaluate | GreatPartsCel | 12.81 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 12.76 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 20.75 us | - | 6.41 KB | - | 1.00 |  |
| E2/100 | full cycle | Celly | 52.53 us | 52.57 us | 76.12 KB | 76.26 KB | 2.71 | yes |
| E2/100 | full cycle | DotnetCel | 65.27 us | 65.31 us | 67.74 KB | 67.88 KB | 3.37 | yes |
| E2/100 | full cycle | Cel.NET | 79.36 us | 79.39 us | 126.28 KB | 126.42 KB | 4.09 | yes |
| E2/100 | full cycle | TelusCel | 865.10 us | 865.13 us | 1.05 MB | 1.05 MB | 44.62 | yes |
| E2/100 | full cycle | GreatPartsCel | 19.39 us | 19.42 us | 12.58 KB | 12.72 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 19.46 us | 19.49 us | 11.93 KB | 12.08 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 24.65 us | 24.68 us | 18.23 KB | 18.38 KB | 1.00 |  |
| E3/10 | text creation | - | - | 32.6 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 15.57 us | 15.61 us | 44.31 KB | 44.46 KB | 3.26 | yes |
| E3/10 | compile | DotnetCel | 13.29 us | 13.32 us | 30.26 KB | 30.41 KB | 2.78 | yes |
| E3/10 | compile | Cel.NET | 28.07 us | 28.10 us | 53.13 KB | 53.27 KB | 5.88 | yes |
| E3/10 | compile | TelusCel | 7.94 us | 7.98 us | 13.38 KB | 13.52 KB | 1.66 | yes |
| E3/10 | compile | GreatPartsCel | 5.12 us | 5.15 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 4.77 us | 4.81 us | 10.32 KB | 10.46 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 956.6 ns | - | 1.61 KB | - | 2.83 | yes |
| E3/10 | evaluate | DotnetCel | 1.20 us | - | 1.78 KB | - | 3.56 | yes |
| E3/10 | evaluate | Cel.NET | 987.7 ns | - | 2.07 KB | - | 2.92 | yes |
| E3/10 | evaluate | TelusCel | 45.35 us | - | 62.83 KB | - | 134.30 | yes |
| E3/10 | evaluate | GreatPartsCel | 429.6 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 337.7 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 855.5 ns | - | 544 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 16.71 us | 16.74 us | 45.93 KB | 46.08 KB | 3.14 | yes |
| E3/10 | full cycle | DotnetCel | 18.33 us | 18.36 us | 35.35 KB | 35.50 KB | 3.44 | yes |
| E3/10 | full cycle | Cel.NET | 29.55 us | 29.58 us | 55.20 KB | 55.34 KB | 5.55 | yes |
| E3/10 | full cycle | TelusCel | 59.30 us | 59.33 us | 76.20 KB | 76.35 KB | 11.13 | yes |
| E3/10 | full cycle | GreatPartsCel | 5.74 us | 5.77 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 5.33 us | 5.36 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 6.49 us | 6.52 us | 11.11 KB | 11.26 KB | 1.00 |  |
| E3/100 | text creation | - | - | 33.1 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 15.82 us | 15.85 us | 44.40 KB | 44.55 KB | 3.31 | yes |
| E3/100 | compile | DotnetCel | 13.48 us | 13.51 us | 30.26 KB | 30.41 KB | 2.82 | yes |
| E3/100 | compile | Cel.NET | 29.25 us | 29.28 us | 53.12 KB | 53.27 KB | 6.11 | yes |
| E3/100 | compile | TelusCel | 7.89 us | 7.93 us | 13.38 KB | 13.52 KB | 1.65 | yes |
| E3/100 | compile | GreatPartsCel | 5.17 us | 5.20 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 4.79 us | 4.82 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.41 us | - | 6.95 KB | - | 13.12 | yes |
| E3/100 | evaluate | DotnetCel | 4.17 us | - | 7.12 KB | - | 12.39 | yes |
| E3/100 | evaluate | Cel.NET | 2.44 us | - | 8.81 KB | - | 7.26 | yes |
| E3/100 | evaluate | TelusCel | 410.82 us | - | 570.20 KB | - | 1221.07 | yes |
| E3/100 | evaluate | GreatPartsCel | 440.0 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 336.4 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 3.40 us | - | 3.34 KB | - | 1.39 | yes |
| E3/100 | full cycle | Celly | 20.48 us | 20.52 us | 51.27 KB | 51.41 KB | 3.84 | yes |
| E3/100 | full cycle | DotnetCel | 21.65 us | 21.68 us | 40.69 KB | 40.84 KB | 4.06 | yes |
| E3/100 | full cycle | Cel.NET | 31.21 us | 31.25 us | 61.94 KB | 62.09 KB | 5.85 | yes |
| E3/100 | full cycle | TelusCel | 416.76 us | 416.80 us | 583.57 KB | 583.72 KB | 78.15 | yes |
| E3/100 | full cycle | GreatPartsCel | 5.72 us | 5.75 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 5.33 us | 5.37 us | 10.68 KB | 10.82 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 9.32 us | 9.35 us | 13.92 KB | 14.07 KB | 1.00 |  |
| E4/10 | text creation | - | - | 36.1 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 29.44 us | 29.48 us | 75.71 KB | 75.89 KB | 2.87 | yes |
| E4/10 | compile | DotnetCel | 19.35 us | 19.39 us | 37.80 KB | 37.98 KB | 1.89 | yes |
| E4/10 | compile | Cel.NET | 87.05 us | 87.08 us | 134.64 KB | 134.82 KB | 8.49 | yes |
| E4/10 | compile | TelusCel | 13.73 us | 13.76 us | 21.26 KB | 21.44 KB | 1.34 | yes |
| E4/10 | compile | GreatPartsCel | 10.58 us | 10.61 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 10.26 us | 10.29 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 3.63 us | - | 4.08 KB | - | 2.84 | yes |
| E4/10 | evaluate | DotnetCel | 5.11 us | - | 5.31 KB | - | 4.00 | yes |
| E4/10 | evaluate | Cel.NET | 5.09 us | - | 7.08 KB | - | 3.98 | yes |
| E4/10 | evaluate | TelusCel | 70.46 us | - | 98.86 KB | - | 55.15 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.41 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.28 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 1.98 us | - | 1.27 KB | - | 1.00 |  |
| E4/10 | full cycle | Celly | 34.61 us | 34.65 us | 79.80 KB | 79.98 KB | 2.88 | yes |
| E4/10 | full cycle | DotnetCel | 28.35 us | 28.39 us | 46.42 KB | 46.60 KB | 2.36 | yes |
| E4/10 | full cycle | Cel.NET | 92.40 us | 92.44 us | 141.74 KB | 141.92 KB | 7.70 | yes |
| E4/10 | full cycle | TelusCel | 86.58 us | 86.61 us | 120.12 KB | 120.30 KB | 7.21 | yes |
| E4/10 | full cycle | GreatPartsCel | 12.36 us | 12.40 us | 19.71 KB | 19.89 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 12.00 us | 12.04 us | 19.30 KB | 19.48 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 13.47 us | 13.50 us | 19.98 KB | 20.16 KB | 1.00 |  |
| E4/100 | text creation | - | - | 35.8 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 29.26 us | 29.30 us | 75.71 KB | 75.89 KB | 2.86 | yes |
| E4/100 | compile | DotnetCel | 18.92 us | 18.95 us | 37.80 KB | 37.98 KB | 1.85 | yes |
| E4/100 | compile | Cel.NET | 86.69 us | 86.72 us | 134.64 KB | 134.82 KB | 8.47 | yes |
| E4/100 | compile | TelusCel | 13.51 us | 13.54 us | 21.26 KB | 21.44 KB | 1.32 | yes |
| E4/100 | compile | GreatPartsCel | 10.46 us | 10.50 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 10.23 us | 10.27 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 39.60 us | - | 42.44 KB | - | 2.67 | yes |
| E4/100 | evaluate | DotnetCel | 59.43 us | - | 70.80 KB | - | 4.01 | yes |
| E4/100 | evaluate | Cel.NET | 59.24 us | - | 96.53 KB | - | 3.99 | yes |
| E4/100 | evaluate | TelusCel | 852.70 us | - | 1.15 MB | - | 57.49 | yes |
| E4/100 | evaluate | GreatPartsCel | 14.83 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 15.45 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 19.44 us | - | 15.65 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 69.50 us | 69.54 us | 118.16 KB | 118.34 KB | 2.67 | yes |
| E4/100 | full cycle | DotnetCel | 82.20 us | 82.23 us | 111.91 KB | 112.09 KB | 3.16 | yes |
| E4/100 | full cycle | Cel.NET | 152.20 us | 152.23 us | 231.18 KB | 231.36 KB | 5.84 | yes |
| E4/100 | full cycle | TelusCel | 984.69 us | 984.72 us | 1.17 MB | 1.17 MB | 37.81 | yes |
| E4/100 | full cycle | GreatPartsCel | 26.76 us | 26.80 us | 28.46 KB | 28.64 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 26.04 us | 26.08 us | 28.05 KB | 28.23 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 35.18 us | 35.21 us | 34.35 KB | 34.53 KB | 1.00 |  |
| E5 | text creation | - | - | 33.7 ns | - | 168 B | - | |
| E5 | compile | Celly | 11.77 us | 11.81 us | 35.54 KB | 35.71 KB | 5.06 | yes |
| E5 | compile | DotnetCel | 9.48 us | 9.51 us | 24.02 KB | 24.19 KB | 4.07 | yes |
| E5 | compile | Cel.NET | 16.33 us | 16.37 us | 30.60 KB | 30.76 KB | 7.01 | yes |
| E5 | compile | TelusCel | 4.90 us | 4.94 us | 8.64 KB | 8.80 KB | 2.11 | yes |
| E5 | compile | GreatPartsCel | 2.72 us | 2.75 us | 6.43 KB | 6.59 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.33 us | 2.36 us | 5.50 KB | 5.66 KB | 1.00 |  |
| E5 | evaluate | Celly | 487.1 ns | - | 752 B | - | 1.75 | yes |
| E5 | evaluate | DotnetCel | 492.4 ns | - | 448 B | - | 1.76 | yes |
| E5 | evaluate | Cel.NET | 86.95 us | - | 252.50 KB | - | 311.57 | yes |
| E5 | evaluate | TelusCel | 9.25 us | - | 14.34 KB | - | 33.16 | yes |
| E5 | evaluate | GreatPartsCel | 349.5 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 279.1 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 392.0 ns | - | 160 B | - | 1.00 |  |
| E5 | full cycle | Celly | 12.45 us | 12.48 us | 36.26 KB | 36.43 KB | 4.48 | yes |
| E5 | full cycle | DotnetCel | 13.24 us | 13.27 us | 27.60 KB | 27.77 KB | 4.77 | yes |
| E5 | full cycle | Cel.NET | 107.08 us | 107.12 us | 283.08 KB | 283.25 KB | 38.57 | yes |
| E5 | full cycle | TelusCel | 14.77 us | 14.80 us | 22.98 KB | 23.15 KB | 5.32 | yes |
| E5 | full cycle | GreatPartsCel | 3.27 us | 3.31 us | 6.74 KB | 6.90 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.78 us | 2.81 us | 5.81 KB | 5.97 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 3.41 us | 3.44 us | 5.91 KB | 6.08 KB | 1.00 |  |
| E6 | text creation | - | - | 36.3 ns | - | 184 B | - | |
| E6 | compile | Celly | 14.70 us | 14.74 us | 42.96 KB | 43.14 KB | 4.42 | yes |
| E6 | compile | Cel.NET | 31.32 us | 31.36 us | 50.46 KB | 50.64 KB | 9.42 | yes |
| E6 | compile | TelusCel | 8.59 us | 8.63 us | 13.97 KB | 14.15 KB | 2.58 | yes |
| E6 | compile | GreatPartsCel | 4.51 us | 4.54 us | 9.18 KB | 9.36 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.33 us | 3.36 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 447.8 ns | - | 912 B | - | 1.75 | yes |
| E6 | evaluate | Cel.NET | 527.7 ns | - | 1.57 KB | - | 2.06 | yes |
| E6 | evaluate | TelusCel | 12.16 us | - | 18.30 KB | - | 47.55 | yes |
| E6 | evaluate | GreatPartsCel | 309.2 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 255.7 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 417.8 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 15.25 us | 15.28 us | 43.84 KB | 44.02 KB | 4.05 | yes |
| E6 | full cycle | Cel.NET | 31.95 us | 31.98 us | 52.04 KB | 52.22 KB | 8.48 | yes |
| E6 | full cycle | TelusCel | 21.63 us | 21.66 us | 32.27 KB | 32.45 KB | 5.74 | yes |
| E6 | full cycle | GreatPartsCel | 5.06 us | 5.10 us | 9.49 KB | 9.67 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 3.77 us | 3.80 us | 6.77 KB | 6.95 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 4.41 us | 4.45 us | 6.90 KB | 7.08 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 28.9 ns | - | 128 B | - | |
| E7 | compile | Celly | 15.48 us | 15.51 us | 45.23 KB | 45.36 KB | 4.51 | yes |
| E7 | compile | DotnetCel | 10.64 us | 10.67 us | 25.66 KB | 25.78 KB | 3.10 | yes |
| E7 | compile | Cel.NET | 29.90 us | 29.93 us | 53.70 KB | 53.83 KB | 8.72 | yes |
| E7 | compile | GreatPartsCel | 3.71 us | 3.74 us | 7.51 KB | 7.63 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 3.43 us | 3.46 us | 7.08 KB | 7.20 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 885.9 ns | - | 1.17 KB | - | 3.44 | yes |
| E7 | evaluate | DotnetCel | 429.9 ns | - | 592 B | - | 1.67 | yes |
| E7 | evaluate | Cel.NET | 760.8 ns | - | 1.61 KB | - | 2.95 | yes |
| E7 | evaluate | GreatPartsCel | 339.4 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 257.5 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 377.6 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 16.46 us | 16.49 us | 46.31 KB | 46.44 KB | 4.29 | yes |
| E7 | full cycle | DotnetCel | 13.82 us | 13.85 us | 28.74 KB | 28.87 KB | 3.60 | yes |
| E7 | full cycle | Cel.NET | 30.83 us | 30.86 us | 55.32 KB | 55.44 KB | 8.04 | yes |
| E7 | full cycle | GreatPartsCel | 4.27 us | 4.30 us | 7.82 KB | 7.95 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 3.84 us | 3.87 us | 7.39 KB | 7.51 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 4.38 us | 4.41 us | 7.36 KB | 7.48 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 36.4 ns | - | 192 B | - | |
| E8 | compile | Celly | 18.86 us | 18.90 us | 52.38 KB | 52.56 KB | 3.66 | yes |
| E8 | compile | Cel.NET | 60.82 us | 60.86 us | 99.24 KB | 99.43 KB | 11.79 | yes |
| E8 | compile | TelusCel | 12.27 us | 12.31 us | 19.39 KB | 19.58 KB | 2.38 | yes |
| E8 | compile | GreatPartsCel | 6.02 us | 6.06 us | 12.50 KB | 12.69 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 5.16 us | 5.19 us | 10.87 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 980.9 ns | - | 1.66 KB | - | 3.33 | yes |
| E8 | evaluate | Cel.NET | 583.8 ns | - | 1.64 KB | - | 1.98 | yes |
| E8 | evaluate | TelusCel | 11.08 us | - | 16.02 KB | - | 37.56 | yes |
| E8 | evaluate | GreatPartsCel | 375.7 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 294.9 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 758.4 ns | - | 480 B | - | 1.30 | yes |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 20.28 us | 20.31 us | 54.06 KB | 54.25 KB | 3.56 | yes |
| E8 | full cycle | Cel.NET | 61.97 us | 62.01 us | 100.87 KB | 101.06 KB | 10.89 | yes |
| E8 | full cycle | TelusCel | 24.20 us | 24.24 us | 35.41 KB | 35.60 KB | 4.25 | yes |
| E8 | full cycle | GreatPartsCel | 6.58 us | 6.62 us | 12.87 KB | 13.05 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 5.69 us | 5.73 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 6.74 us | 6.78 us | 11.61 KB | 11.79 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 30.1 ns | - | 136 B | - | |
| E9 | compile | Celly | 17.78 us | 17.81 us | 52.26 KB | 52.39 KB | 4.58 | yes |
| E9 | compile | DotnetCel | 10.96 us | 10.99 us | 26.69 KB | 26.82 KB | 2.83 | yes |
| E9 | compile | Cel.NET | 41.77 us | 41.80 us | 77.43 KB | 77.56 KB | 10.77 | yes |
| E9 | compile | TelusCel | 6.11 us | 6.14 us | 10.08 KB | 10.21 KB | 1.57 | yes |
| E9 | compile | GreatPartsCel | 4.66 us | 4.69 us | 9.97 KB | 10.11 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 3.88 us | 3.91 us | 8.42 KB | 8.55 KB | 1.00 |  |
| E9 | evaluate | Celly | 480.1 ns | - | 1.11 KB | - | 1.51 | yes |
| E9 | evaluate | DotnetCel | 515.8 ns | - | 808 B | - | 1.62 | yes |
| E9 | evaluate | Cel.NET | 558.8 ns | - | 1.75 KB | - | 1.76 | yes |
| E9 | evaluate | TelusCel | 7.12 us | - | 10.77 KB | - | 22.41 | yes |
| E9 | evaluate | GreatPartsCel | 390.0 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 317.5 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 497.0 ns | - | 344 B | - | 1.04 |  |
| E9 | full cycle | Celly | 18.69 us | 18.72 us | 53.52 KB | 53.65 KB | 4.21 | yes |
| E9 | full cycle | DotnetCel | 13.96 us | 13.99 us | 29.98 KB | 30.12 KB | 3.15 | yes |
| E9 | full cycle | Cel.NET | 43.20 us | 43.23 us | 79.32 KB | 79.45 KB | 9.74 | yes |
| E9 | full cycle | TelusCel | 13.82 us | 13.85 us | 20.85 KB | 20.98 KB | 3.12 | yes |
| E9 | full cycle | GreatPartsCel | 5.28 us | 5.31 us | 10.47 KB | 10.60 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.44 us | 4.47 us | 8.91 KB | 9.05 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 5.04 us | 5.07 us | 9.02 KB | 9.15 KB | 1.00 |  |
