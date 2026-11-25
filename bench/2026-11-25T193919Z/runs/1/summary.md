## Run 1: scenario x operation

- Run 1: 2026-11-25T15:01:42.681Z - 2026-11-25T16:23:02.896Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 32.7 ns | - | 160 B | - | |
| E1 | compile | Celly | 14.59 us | 14.62 us | 42.81 KB | 42.96 KB | 4.56 | yes |
| E1 | compile | DotnetCel | 10.94 us | 10.97 us | 26.31 KB | 26.47 KB | 3.42 | yes |
| E1 | compile | Cel.NET | 28.73 us | 28.76 us | 49.87 KB | 50.03 KB | 8.98 | yes |
| E1 | compile | TelusCel | 6.28 us | 6.31 us | 10.33 KB | 10.48 KB | 1.96 | yes |
| E1 | compile | GreatPartsCel | 4.08 us | 4.11 us | 8.82 KB | 8.98 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.20 us | 3.23 us | 6.97 KB | 7.12 KB | 1.00 |  |
| E1 | evaluate | Celly | 462.6 ns | - | 832 B | - | 1.87 | yes |
| E1 | evaluate | DotnetCel | 434.5 ns | - | 560 B | - | 1.76 | yes |
| E1 | evaluate | Cel.NET | 513.4 ns | - | 1.52 KB | - | 2.08 | yes |
| E1 | evaluate | TelusCel | 7.21 us | - | 11.16 KB | - | 29.17 | yes |
| E1 | evaluate | GreatPartsCel | 293.4 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 247.0 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 399.0 ns | - | 160 B | - | 1.00 |  |
| E1 | full cycle | Celly | 15.11 us | 15.14 us | 43.62 KB | 43.78 KB | 4.25 | yes |
| E1 | full cycle | DotnetCel | 14.15 us | 14.18 us | 29.37 KB | 29.52 KB | 3.97 | yes |
| E1 | full cycle | Cel.NET | 28.69 us | 28.73 us | 51.40 KB | 51.55 KB | 8.06 | yes |
| E1 | full cycle | TelusCel | 14.27 us | 14.30 us | 21.49 KB | 21.65 KB | 4.01 | yes |
| E1 | full cycle | GreatPartsCel | 4.65 us | 4.68 us | 9.14 KB | 9.30 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 3.56 us | 3.59 us | 7.28 KB | 7.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 4.24 us | 4.27 us | 7.39 KB | 7.54 KB | 1.00 |  |
| E2/10 | text creation | - | - | 32.1 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 19.09 us | 19.13 us | 54.52 KB | 54.66 KB | 3.20 | yes |
| E2/10 | compile | DotnetCel | 14.73 us | 14.76 us | 32.52 KB | 32.66 KB | 2.47 | yes |
| E2/10 | compile | Cel.NET | 37.77 us | 37.80 us | 71.99 KB | 72.14 KB | 6.32 | yes |
| E2/10 | compile | TelusCel | 9.68 us | 9.71 us | 15.50 KB | 15.65 KB | 1.62 | yes |
| E2/10 | compile | GreatPartsCel | 6.27 us | 6.31 us | 12.21 KB | 12.36 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 5.97 us | 6.01 us | 11.57 KB | 11.71 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 3.80 us | - | 2.96 KB | - | 2.47 | yes |
| E2/10 | evaluate | DotnetCel | 5.10 us | - | 4.07 KB | - | 3.32 | yes |
| E2/10 | evaluate | Cel.NET | 4.53 us | - | 6.15 KB | - | 2.95 | yes |
| E2/10 | evaluate | TelusCel | 79.20 us | - | 111.34 KB | - | 51.60 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.57 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.53 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 2.20 us | - | 800 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 23.20 us | 23.23 us | 57.48 KB | 57.63 KB | 2.94 | yes |
| E2/10 | full cycle | DotnetCel | 23.66 us | 23.69 us | 39.90 KB | 40.05 KB | 3.00 | yes |
| E2/10 | full cycle | Cel.NET | 43.21 us | 43.25 us | 78.13 KB | 78.28 KB | 5.48 | yes |
| E2/10 | full cycle | TelusCel | 99.41 us | 99.44 us | 126.84 KB | 126.98 KB | 12.60 | yes |
| E2/10 | full cycle | GreatPartsCel | 8.24 us | 8.27 us | 12.57 KB | 12.72 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 7.89 us | 7.92 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 9.42 us | 9.46 us | 12.61 KB | 12.76 KB | 1.00 |  |
| E2/100 | text creation | - | - | 30.4 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 19.06 us | 19.09 us | 54.51 KB | 54.66 KB | 3.17 | yes |
| E2/100 | compile | DotnetCel | 14.84 us | 14.87 us | 32.52 KB | 32.66 KB | 2.47 | yes |
| E2/100 | compile | Cel.NET | 38.09 us | 38.12 us | 71.98 KB | 72.13 KB | 6.33 | yes |
| E2/100 | compile | TelusCel | 9.78 us | 9.81 us | 15.50 KB | 15.65 KB | 1.63 | yes |
| E2/100 | compile | GreatPartsCel | 6.28 us | 6.31 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 6.01 us | 6.04 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 32.01 us | - | 21.66 KB | - | 2.54 | yes |
| E2/100 | evaluate | DotnetCel | 46.99 us | - | 31.91 KB | - | 3.72 | yes |
| E2/100 | evaluate | Cel.NET | 38.68 us | - | 54.28 KB | - | 3.06 | yes |
| E2/100 | evaluate | TelusCel | 770.70 us | - | 1.03 MB | - | 61.07 | yes |
| E2/100 | evaluate | GreatPartsCel | 13.05 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 12.62 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 18.22 us | - | 6.41 KB | - | 1.00 |  |
| E2/100 | full cycle | Celly | 55.22 us | 55.25 us | 76.17 KB | 76.31 KB | 2.90 | yes |
| E2/100 | full cycle | DotnetCel | 64.24 us | 64.27 us | 67.74 KB | 67.88 KB | 3.38 | yes |
| E2/100 | full cycle | Cel.NET | 77.37 us | 77.40 us | 126.28 KB | 126.42 KB | 4.07 | yes |
| E2/100 | full cycle | TelusCel | 866.74 us | 866.77 us | 1.05 MB | 1.05 MB | 45.55 | yes |
| E2/100 | full cycle | GreatPartsCel | 19.79 us | 19.82 us | 12.57 KB | 12.72 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 19.03 us | 19.06 us | 11.93 KB | 12.08 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 24.46 us | 24.49 us | 18.23 KB | 18.38 KB | 1.00 |  |
| E3/10 | text creation | - | - | 29.6 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 15.54 us | 15.57 us | 44.31 KB | 44.46 KB | 3.22 | yes |
| E3/10 | compile | DotnetCel | 13.35 us | 13.38 us | 30.26 KB | 30.41 KB | 2.76 | yes |
| E3/10 | compile | Cel.NET | 28.52 us | 28.55 us | 53.12 KB | 53.27 KB | 5.90 | yes |
| E3/10 | compile | TelusCel | 7.91 us | 7.94 us | 13.38 KB | 13.52 KB | 1.64 | yes |
| E3/10 | compile | GreatPartsCel | 5.12 us | 5.15 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 4.83 us | 4.86 us | 10.32 KB | 10.46 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 962.7 ns | - | 1.61 KB | - | 2.79 | yes |
| E3/10 | evaluate | DotnetCel | 1.23 us | - | 1.78 KB | - | 3.55 | yes |
| E3/10 | evaluate | Cel.NET | 964.0 ns | - | 2.07 KB | - | 2.79 | yes |
| E3/10 | evaluate | TelusCel | 44.35 us | - | 62.83 KB | - | 128.34 | yes |
| E3/10 | evaluate | GreatPartsCel | 431.5 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 345.6 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 835.0 ns | - | 544 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 16.73 us | 16.76 us | 45.92 KB | 46.07 KB | 3.13 | yes |
| E3/10 | full cycle | DotnetCel | 18.30 us | 18.33 us | 35.35 KB | 35.50 KB | 3.42 | yes |
| E3/10 | full cycle | Cel.NET | 29.07 us | 29.10 us | 55.19 KB | 55.34 KB | 5.44 | yes |
| E3/10 | full cycle | TelusCel | 54.44 us | 54.47 us | 76.20 KB | 76.35 KB | 10.18 | yes |
| E3/10 | full cycle | GreatPartsCel | 5.74 us | 5.77 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 5.35 us | 5.38 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 6.43 us | 6.46 us | 11.11 KB | 11.26 KB | 1.00 |  |
| E3/100 | text creation | - | - | 30.7 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 15.58 us | 15.61 us | 44.34 KB | 44.49 KB | 3.25 | yes |
| E3/100 | compile | DotnetCel | 13.31 us | 13.34 us | 30.26 KB | 30.41 KB | 2.78 | yes |
| E3/100 | compile | Cel.NET | 28.24 us | 28.27 us | 53.14 KB | 53.29 KB | 5.89 | yes |
| E3/100 | compile | TelusCel | 7.95 us | 7.98 us | 13.38 KB | 13.52 KB | 1.66 | yes |
| E3/100 | compile | GreatPartsCel | 5.12 us | 5.15 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 4.79 us | 4.83 us | 10.32 KB | 10.46 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.37 us | - | 6.95 KB | - | 12.50 | yes |
| E3/100 | evaluate | DotnetCel | 3.94 us | - | 7.12 KB | - | 11.26 | yes |
| E3/100 | evaluate | Cel.NET | 2.53 us | - | 8.81 KB | - | 7.21 | yes |
| E3/100 | evaluate | TelusCel | 405.74 us | - | 570.20 KB | - | 1159.07 | yes |
| E3/100 | evaluate | GreatPartsCel | 463.0 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 350.1 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 3.44 us | - | 3.34 KB | - | 1.36 | yes |
| E3/100 | full cycle | Celly | 20.30 us | 20.34 us | 51.28 KB | 51.43 KB | 3.74 | yes |
| E3/100 | full cycle | DotnetCel | 21.61 us | 21.64 us | 40.69 KB | 40.84 KB | 3.98 | yes |
| E3/100 | full cycle | Cel.NET | 31.23 us | 31.26 us | 61.93 KB | 62.08 KB | 5.76 | yes |
| E3/100 | full cycle | TelusCel | 419.86 us | 419.89 us | 583.57 KB | 583.72 KB | 77.41 | yes |
| E3/100 | full cycle | GreatPartsCel | 5.75 us | 5.78 us | 11.17 KB | 11.31 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 5.42 us | 5.45 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 9.05 us | 9.08 us | 13.92 KB | 14.07 KB | 1.00 |  |
| E4/10 | text creation | - | - | 35.0 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 29.57 us | 29.61 us | 75.71 KB | 75.89 KB | 2.87 | yes |
| E4/10 | compile | DotnetCel | 18.86 us | 18.90 us | 37.80 KB | 37.98 KB | 1.83 | yes |
| E4/10 | compile | Cel.NET | 85.64 us | 85.67 us | 134.66 KB | 134.84 KB | 8.30 | yes |
| E4/10 | compile | TelusCel | 13.53 us | 13.56 us | 21.26 KB | 21.44 KB | 1.31 | yes |
| E4/10 | compile | GreatPartsCel | 10.51 us | 10.54 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 10.31 us | 10.35 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 3.62 us | - | 4.08 KB | - | 2.87 | yes |
| E4/10 | evaluate | DotnetCel | 5.05 us | - | 5.31 KB | - | 4.00 | yes |
| E4/10 | evaluate | Cel.NET | 4.88 us | - | 7.08 KB | - | 3.87 | yes |
| E4/10 | evaluate | TelusCel | 71.35 us | - | 98.86 KB | - | 56.58 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.41 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.26 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 2.02 us | - | 1.27 KB | - | 1.00 |  |
| E4/10 | full cycle | Celly | 33.73 us | 33.76 us | 79.82 KB | 80.00 KB | 2.80 | yes |
| E4/10 | full cycle | DotnetCel | 28.51 us | 28.54 us | 46.42 KB | 46.60 KB | 2.37 | yes |
| E4/10 | full cycle | Cel.NET | 92.34 us | 92.37 us | 141.72 KB | 141.90 KB | 7.66 | yes |
| E4/10 | full cycle | TelusCel | 88.80 us | 88.83 us | 120.12 KB | 120.30 KB | 7.37 | yes |
| E4/10 | full cycle | GreatPartsCel | 12.46 us | 12.49 us | 19.71 KB | 19.89 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 12.05 us | 12.08 us | 19.30 KB | 19.48 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 13.51 us | 13.54 us | 19.98 KB | 20.16 KB | 1.00 |  |
| E4/100 | text creation | - | - | 34.2 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 29.48 us | 29.51 us | 75.70 KB | 75.88 KB | 2.85 | yes |
| E4/100 | compile | DotnetCel | 19.17 us | 19.20 us | 37.80 KB | 37.98 KB | 1.85 | yes |
| E4/100 | compile | Cel.NET | 86.88 us | 86.91 us | 134.65 KB | 134.83 KB | 8.40 | yes |
| E4/100 | compile | TelusCel | 13.48 us | 13.51 us | 21.26 KB | 21.44 KB | 1.30 | yes |
| E4/100 | compile | GreatPartsCel | 10.41 us | 10.45 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 10.34 us | 10.38 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 36.23 us | - | 42.44 KB | - | 2.36 | yes |
| E4/100 | evaluate | DotnetCel | 60.99 us | - | 70.80 KB | - | 3.97 | yes |
| E4/100 | evaluate | Cel.NET | 62.15 us | - | 96.53 KB | - | 4.05 | yes |
| E4/100 | evaluate | TelusCel | 851.87 us | - | 1.15 MB | - | 55.49 | yes |
| E4/100 | evaluate | GreatPartsCel | 15.59 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 15.35 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 19.70 us | - | 15.65 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 68.80 us | 68.83 us | 118.14 KB | 118.32 KB | 2.68 | yes |
| E4/100 | full cycle | DotnetCel | 82.84 us | 82.87 us | 111.91 KB | 112.09 KB | 3.23 | yes |
| E4/100 | full cycle | Cel.NET | 149.48 us | 149.51 us | 231.17 KB | 231.35 KB | 5.83 | yes |
| E4/100 | full cycle | TelusCel | 863.74 us | 863.78 us | 1.17 MB | 1.17 MB | 33.66 | yes |
| E4/100 | full cycle | GreatPartsCel | 25.66 us | 25.69 us | 28.46 KB | 28.64 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 27.23 us | 27.27 us | 28.05 KB | 28.23 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 34.22 us | 34.25 us | 34.35 KB | 34.53 KB | 1.00 |  |
| E5 | text creation | - | - | 32.1 ns | - | 168 B | - | |
| E5 | compile | Celly | 11.84 us | 11.87 us | 35.56 KB | 35.73 KB | 5.10 | yes |
| E5 | compile | DotnetCel | 9.54 us | 9.57 us | 24.02 KB | 24.19 KB | 4.11 | yes |
| E5 | compile | Cel.NET | 16.33 us | 16.37 us | 30.58 KB | 30.75 KB | 7.03 | yes |
| E5 | compile | TelusCel | 4.87 us | 4.90 us | 8.64 KB | 8.80 KB | 2.10 | yes |
| E5 | compile | GreatPartsCel | 2.72 us | 2.75 us | 6.43 KB | 6.60 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.32 us | 2.35 us | 5.50 KB | 5.66 KB | 1.00 |  |
| E5 | evaluate | Celly | 470.8 ns | - | 752 B | - | 1.84 | yes |
| E5 | evaluate | DotnetCel | 501.8 ns | - | 448 B | - | 1.96 | yes |
| E5 | evaluate | Cel.NET | 86.60 us | - | 252.50 KB | - | 338.40 | yes |
| E5 | evaluate | TelusCel | 9.24 us | - | 14.34 KB | - | 36.09 | yes |
| E5 | evaluate | GreatPartsCel | 355.2 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 255.9 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 394.0 ns | - | 160 B | - | 1.00 |  |
| E5 | full cycle | Celly | 12.34 us | 12.37 us | 36.28 KB | 36.44 KB | 4.49 | yes |
| E5 | full cycle | DotnetCel | 13.46 us | 13.49 us | 27.60 KB | 27.77 KB | 4.90 | yes |
| E5 | full cycle | Cel.NET | 107.52 us | 107.55 us | 283.10 KB | 283.26 KB | 39.11 | yes |
| E5 | full cycle | TelusCel | 15.73 us | 15.76 us | 22.98 KB | 23.15 KB | 5.72 | yes |
| E5 | full cycle | GreatPartsCel | 3.26 us | 3.29 us | 6.74 KB | 6.90 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.75 us | 2.78 us | 5.81 KB | 5.97 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 3.42 us | 3.46 us | 5.91 KB | 6.08 KB | 1.00 |  |
| E6 | text creation | - | - | 34.6 ns | - | 184 B | - | |
| E6 | compile | Celly | 14.85 us | 14.89 us | 42.96 KB | 43.14 KB | 4.47 | yes |
| E6 | compile | Cel.NET | 31.33 us | 31.36 us | 50.47 KB | 50.65 KB | 9.44 | yes |
| E6 | compile | TelusCel | 8.53 us | 8.57 us | 13.97 KB | 14.15 KB | 2.57 | yes |
| E6 | compile | GreatPartsCel | 4.48 us | 4.51 us | 9.18 KB | 9.36 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.32 us | 3.35 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 444.5 ns | - | 912 B | - | 1.74 | yes |
| E6 | evaluate | Cel.NET | 528.3 ns | - | 1.57 KB | - | 2.07 | yes |
| E6 | evaluate | TelusCel | 12.42 us | - | 18.30 KB | - | 48.69 | yes |
| E6 | evaluate | GreatPartsCel | 305.1 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 255.0 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 420.1 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 15.49 us | 15.53 us | 43.84 KB | 44.02 KB | 4.12 | yes |
| E6 | full cycle | Cel.NET | 31.98 us | 32.02 us | 52.03 KB | 52.21 KB | 8.51 | yes |
| E6 | full cycle | TelusCel | 21.39 us | 21.42 us | 32.27 KB | 32.45 KB | 5.69 | yes |
| E6 | full cycle | GreatPartsCel | 4.96 us | 4.99 us | 9.49 KB | 9.67 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 3.76 us | 3.79 us | 6.78 KB | 6.96 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 4.39 us | 4.42 us | 6.90 KB | 7.08 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 29.4 ns | - | 128 B | - | |
| E7 | compile | Celly | 15.36 us | 15.39 us | 45.14 KB | 45.27 KB | 4.54 | yes |
| E7 | compile | DotnetCel | 10.48 us | 10.51 us | 25.66 KB | 25.78 KB | 3.09 | yes |
| E7 | compile | Cel.NET | 29.90 us | 29.93 us | 53.68 KB | 53.81 KB | 8.83 | yes |
| E7 | compile | GreatPartsCel | 3.68 us | 3.71 us | 7.51 KB | 7.63 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 3.39 us | 3.41 us | 7.08 KB | 7.20 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 877.8 ns | - | 1.17 KB | - | 3.45 | yes |
| E7 | evaluate | DotnetCel | 427.2 ns | - | 592 B | - | 1.68 | yes |
| E7 | evaluate | Cel.NET | 661.3 ns | - | 1.61 KB | - | 2.60 | yes |
| E7 | evaluate | GreatPartsCel | 314.8 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 254.3 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 364.6 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 16.57 us | 16.60 us | 46.31 KB | 46.43 KB | 4.33 | yes |
| E7 | full cycle | DotnetCel | 13.71 us | 13.74 us | 28.74 KB | 28.87 KB | 3.59 | yes |
| E7 | full cycle | Cel.NET | 30.78 us | 30.81 us | 55.30 KB | 55.43 KB | 8.05 | yes |
| E7 | full cycle | GreatPartsCel | 4.19 us | 4.22 us | 7.82 KB | 7.95 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 3.82 us | 3.85 us | 7.39 KB | 7.52 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 4.41 us | 4.44 us | 7.36 KB | 7.48 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 35.1 ns | - | 192 B | - | |
| E8 | compile | Celly | 19.04 us | 19.07 us | 52.38 KB | 52.57 KB | 3.68 | yes |
| E8 | compile | Cel.NET | 60.58 us | 60.61 us | 99.23 KB | 99.42 KB | 11.71 | yes |
| E8 | compile | TelusCel | 12.31 us | 12.34 us | 19.39 KB | 19.58 KB | 2.38 | yes |
| E8 | compile | GreatPartsCel | 5.93 us | 5.96 us | 12.51 KB | 12.70 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 5.17 us | 5.21 us | 10.87 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 998.6 ns | - | 1.66 KB | - | 3.47 | yes |
| E8 | evaluate | Cel.NET | 574.9 ns | - | 1.64 KB | - | 2.00 | yes |
| E8 | evaluate | TelusCel | 12.69 us | - | 16.02 KB | - | 44.14 | yes |
| E8 | evaluate | GreatPartsCel | 370.8 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 287.5 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 765.7 ns | - | 480 B | - | 1.33 | yes |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 20.33 us | 20.36 us | 54.17 KB | 54.35 KB | 3.59 | yes |
| E8 | full cycle | Cel.NET | 61.98 us | 62.01 us | 100.88 KB | 101.07 KB | 10.94 | yes |
| E8 | full cycle | TelusCel | 25.19 us | 25.22 us | 35.41 KB | 35.60 KB | 4.44 | yes |
| E8 | full cycle | GreatPartsCel | 6.53 us | 6.56 us | 12.87 KB | 13.05 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 5.67 us | 5.70 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 6.82 us | 6.86 us | 11.61 KB | 11.80 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 30.1 ns | - | 136 B | - | |
| E9 | compile | Celly | 18.06 us | 18.09 us | 52.38 KB | 52.52 KB | 4.67 | yes |
| E9 | compile | DotnetCel | 11.01 us | 11.04 us | 26.69 KB | 26.82 KB | 2.85 | yes |
| E9 | compile | Cel.NET | 41.81 us | 41.84 us | 77.57 KB | 77.70 KB | 10.81 | yes |
| E9 | compile | TelusCel | 6.10 us | 6.13 us | 10.08 KB | 10.21 KB | 1.58 | yes |
| E9 | compile | GreatPartsCel | 4.64 us | 4.67 us | 9.98 KB | 10.11 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 3.87 us | 3.90 us | 8.42 KB | 8.55 KB | 1.00 |  |
| E9 | evaluate | Celly | 475.3 ns | - | 1.11 KB | - | 1.62 | yes |
| E9 | evaluate | DotnetCel | 510.1 ns | - | 808 B | - | 1.74 | yes |
| E9 | evaluate | Cel.NET | 558.7 ns | - | 1.75 KB | - | 1.91 | yes |
| E9 | evaluate | TelusCel | 7.18 us | - | 10.77 KB | - | 24.48 | yes |
| E9 | evaluate | GreatPartsCel | 382.9 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 293.1 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 468.5 ns | - | 344 B | - | 1.00 |  |
| E9 | full cycle | Celly | 18.59 us | 18.62 us | 53.37 KB | 53.50 KB | 4.28 | yes |
| E9 | full cycle | DotnetCel | 14.13 us | 14.16 us | 29.98 KB | 30.12 KB | 3.25 | yes |
| E9 | full cycle | Cel.NET | 42.89 us | 42.92 us | 79.19 KB | 79.32 KB | 9.87 | yes |
| E9 | full cycle | TelusCel | 13.67 us | 13.70 us | 20.85 KB | 20.98 KB | 3.15 | yes |
| E9 | full cycle | GreatPartsCel | 5.24 us | 5.27 us | 10.46 KB | 10.60 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.35 us | 4.38 us | 8.91 KB | 9.04 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 5.07 us | 5.10 us | 9.03 KB | 9.16 KB | 1.00 |  |
