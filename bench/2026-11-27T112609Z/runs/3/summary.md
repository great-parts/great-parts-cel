## Run 3: scenario x operation

- Run 3: 2026-11-27T09:29:16.569Z - 2026-11-27T10:54:01.552Z; job: Default; runtime: JIT; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 22.4 ns | - | 160 B | - | |
| E1 | compile | Celly | 11.36 us | 11.38 us | 42.81 KB | 42.96 KB | 4.47 | yes |
| E1 | compile | DotnetCel | 8.41 us | 8.43 us | 26.31 KB | 26.47 KB | 3.31 | yes |
| E1 | compile | Cel.NET | 22.62 us | 22.65 us | 49.86 KB | 50.02 KB | 8.91 | yes |
| E1 | compile | TelusCel | 4.99 us | 5.02 us | 10.33 KB | 10.48 KB | 1.97 | yes |
| E1 | compile | GreatPartsCel | 3.33 us | 3.35 us | 8.97 KB | 9.13 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 2.54 us | 2.56 us | 6.98 KB | 7.14 KB | 1.00 |  |
| E1 | evaluate | Celly | 325.6 ns | - | 832 B | - | 1.70 | yes |
| E1 | evaluate | DotnetCel | 327.5 ns | - | 560 B | - | 1.71 | yes |
| E1 | evaluate | Cel.NET | 400.7 ns | - | 1.52 KB | - | 2.10 | yes |
| E1 | evaluate | TelusCel | 6.44 us | - | 11.16 KB | - | 33.68 | yes |
| E1 | evaluate | GreatPartsCel | 249.6 ns | - | 328 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 191.2 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 269.8 ns | - | 160 B | - | 1.00 |  |
| E1 | full cycle | Celly | 12.05 us | 12.07 us | 43.48 KB | 43.64 KB | 4.14 | yes |
| E1 | full cycle | DotnetCel | 11.48 us | 11.50 us | 29.37 KB | 29.52 KB | 3.94 | yes |
| E1 | full cycle | Cel.NET | 22.82 us | 22.84 us | 51.38 KB | 51.54 KB | 7.84 | yes |
| E1 | full cycle | TelusCel | 11.11 us | 11.13 us | 21.49 KB | 21.65 KB | 3.81 | yes |
| E1 | full cycle | GreatPartsCel | 3.77 us | 3.79 us | 9.29 KB | 9.45 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 2.91 us | 2.93 us | 7.30 KB | 7.46 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 3.47 us | 3.49 us | 7.55 KB | 7.71 KB | 1.00 |  |
| E2/10 | text creation | - | - | 20.7 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 15.45 us | 15.47 us | 54.46 KB | 54.61 KB | 3.19 | yes |
| E2/10 | compile | DotnetCel | 12.18 us | 12.20 us | 32.52 KB | 32.66 KB | 2.52 | yes |
| E2/10 | compile | Cel.NET | 30.21 us | 30.23 us | 71.98 KB | 72.13 KB | 6.25 | yes |
| E2/10 | compile | TelusCel | 7.63 us | 7.65 us | 15.50 KB | 15.65 KB | 1.58 | yes |
| E2/10 | compile | GreatPartsCel | 5.23 us | 5.25 us | 12.36 KB | 12.50 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 4.84 us | 4.86 us | 11.58 KB | 11.73 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 2.94 us | - | 2.96 KB | - | 2.49 | yes |
| E2/10 | evaluate | DotnetCel | 3.87 us | - | 4.07 KB | - | 3.29 | yes |
| E2/10 | evaluate | Cel.NET | 3.43 us | - | 6.15 KB | - | 2.91 | yes |
| E2/10 | evaluate | TelusCel | 60.80 us | - | 111.34 KB | - | 51.60 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.23 us | - | 376 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.18 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 1.31 us | - | 160 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 18.45 us | 18.47 us | 57.47 KB | 57.62 KB | 2.94 | yes |
| E2/10 | full cycle | DotnetCel | 19.38 us | 19.40 us | 39.90 KB | 40.05 KB | 3.08 | yes |
| E2/10 | full cycle | Cel.NET | 34.71 us | 34.73 us | 78.14 KB | 78.29 KB | 5.52 | yes |
| E2/10 | full cycle | TelusCel | 71.36 us | 71.38 us | 126.84 KB | 126.98 KB | 11.36 | yes |
| E2/10 | full cycle | GreatPartsCel | 6.78 us | 6.80 us | 12.73 KB | 12.88 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 6.28 us | 6.30 us | 11.95 KB | 12.10 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 6.98 us | 7.00 us | 12.13 KB | 12.27 KB | 1.00 |  |
| E2/100 | text creation | - | - | 21.1 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 15.38 us | 15.40 us | 54.53 KB | 54.68 KB | 3.14 | yes |
| E2/100 | compile | DotnetCel | 11.84 us | 11.86 us | 32.52 KB | 32.66 KB | 2.41 | yes |
| E2/100 | compile | Cel.NET | 28.88 us | 28.90 us | 71.99 KB | 72.13 KB | 5.89 | yes |
| E2/100 | compile | TelusCel | 7.73 us | 7.75 us | 15.50 KB | 15.65 KB | 1.58 | yes |
| E2/100 | compile | GreatPartsCel | 5.14 us | 5.16 us | 12.36 KB | 12.51 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 4.90 us | 4.92 us | 11.59 KB | 11.73 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 23.16 us | - | 21.66 KB | - | 2.34 | yes |
| E2/100 | evaluate | DotnetCel | 34.87 us | - | 31.91 KB | - | 3.53 | yes |
| E2/100 | evaluate | Cel.NET | 31.45 us | - | 54.28 KB | - | 3.18 | yes |
| E2/100 | evaluate | TelusCel | 599.08 us | - | 1.03 MB | - | 60.63 | yes |
| E2/100 | evaluate | GreatPartsCel | 10.26 us | - | 376 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 9.88 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 11.31 us | - | 160 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 39.74 us | 39.76 us | 76.17 KB | 76.31 KB | 2.60 | yes |
| E2/100 | full cycle | DotnetCel | 50.81 us | 50.84 us | 67.74 KB | 67.88 KB | 3.33 | yes |
| E2/100 | full cycle | Cel.NET | 62.18 us | 62.20 us | 126.28 KB | 126.42 KB | 4.07 | yes |
| E2/100 | full cycle | TelusCel | 689.81 us | 689.83 us | 1.05 MB | 1.05 MB | 45.15 | yes |
| E2/100 | full cycle | GreatPartsCel | 15.95 us | 15.97 us | 12.72 KB | 12.87 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 15.28 us | 15.30 us | 11.95 KB | 12.10 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 17.03 us | 17.05 us | 12.12 KB | 12.27 KB | 1.00 |  |
| E3/10 | text creation | - | - | 20.2 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 12.91 us | 12.93 us | 44.35 KB | 44.50 KB | 3.27 | yes |
| E3/10 | compile | DotnetCel | 11.09 us | 11.11 us | 30.26 KB | 30.41 KB | 2.81 | yes |
| E3/10 | compile | Cel.NET | 21.73 us | 21.75 us | 53.14 KB | 53.29 KB | 5.50 | yes |
| E3/10 | compile | TelusCel | 6.25 us | 6.27 us | 13.38 KB | 13.52 KB | 1.58 | yes |
| E3/10 | compile | GreatPartsCel | 4.22 us | 4.24 us | 10.94 KB | 11.09 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 3.95 us | 3.97 us | 10.33 KB | 10.48 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 771.1 ns | - | 1.61 KB | - | 2.82 | yes |
| E3/10 | evaluate | DotnetCel | 942.4 ns | - | 1.78 KB | - | 3.44 | yes |
| E3/10 | evaluate | Cel.NET | 747.5 ns | - | 2.07 KB | - | 2.73 | yes |
| E3/10 | evaluate | TelusCel | 34.80 us | - | 62.83 KB | - | 127.11 | yes |
| E3/10 | evaluate | GreatPartsCel | 346.7 ns | - | 376 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 273.8 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 341.6 ns | - | 160 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 13.50 us | 13.52 us | 45.94 KB | 46.09 KB | 3.07 | yes |
| E3/10 | full cycle | DotnetCel | 15.13 us | 15.15 us | 35.35 KB | 35.50 KB | 3.44 | yes |
| E3/10 | full cycle | Cel.NET | 23.17 us | 23.19 us | 55.19 KB | 55.34 KB | 5.27 | yes |
| E3/10 | full cycle | TelusCel | 42.87 us | 42.89 us | 76.20 KB | 76.35 KB | 9.75 | yes |
| E3/10 | full cycle | GreatPartsCel | 4.76 us | 4.78 us | 11.31 KB | 11.46 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 4.40 us | 4.42 us | 10.70 KB | 10.85 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 4.96 us | 4.98 us | 10.87 KB | 11.02 KB | 1.00 |  |
| E3/100 | text creation | - | - | 21.6 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 12.51 us | 12.53 us | 44.39 KB | 44.54 KB | 3.17 | yes |
| E3/100 | compile | DotnetCel | 10.83 us | 10.86 us | 30.26 KB | 30.41 KB | 2.75 | yes |
| E3/100 | compile | Cel.NET | 21.50 us | 21.52 us | 53.14 KB | 53.29 KB | 5.45 | yes |
| E3/100 | compile | TelusCel | 6.41 us | 6.43 us | 13.38 KB | 13.52 KB | 1.62 | yes |
| E3/100 | compile | GreatPartsCel | 4.19 us | 4.22 us | 10.95 KB | 11.09 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 3.94 us | 3.97 us | 10.34 KB | 10.48 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 3.63 us | - | 6.95 KB | - | 13.51 | yes |
| E3/100 | evaluate | DotnetCel | 3.18 us | - | 7.12 KB | - | 11.83 | yes |
| E3/100 | evaluate | Cel.NET | 1.89 us | - | 8.81 KB | - | 7.04 | yes |
| E3/100 | evaluate | TelusCel | 317.27 us | - | 570.20 KB | - | 1182.25 | yes |
| E3/100 | evaluate | GreatPartsCel | 331.5 ns | - | 376 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 268.4 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 338.3 ns | - | 160 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 16.80 us | 16.82 us | 51.27 KB | 51.42 KB | 3.85 | yes |
| E3/100 | full cycle | DotnetCel | 17.54 us | 17.56 us | 40.69 KB | 40.84 KB | 4.02 | yes |
| E3/100 | full cycle | Cel.NET | 25.05 us | 25.07 us | 61.94 KB | 62.09 KB | 5.74 | yes |
| E3/100 | full cycle | TelusCel | 332.55 us | 332.57 us | 583.57 KB | 583.72 KB | 76.21 | yes |
| E3/100 | full cycle | GreatPartsCel | 4.77 us | 4.79 us | 11.31 KB | 11.46 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 4.36 us | 4.39 us | 10.70 KB | 10.85 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 4.89 us | 4.91 us | 10.88 KB | 11.02 KB | 1.00 |  |
| E4/10 | text creation | - | - | 23.7 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 23.40 us | 23.42 us | 75.71 KB | 75.89 KB | 2.78 | yes |
| E4/10 | compile | DotnetCel | 15.95 us | 15.97 us | 37.80 KB | 37.98 KB | 1.90 | yes |
| E4/10 | compile | Cel.NET | 69.15 us | 69.17 us | 134.66 KB | 134.84 KB | 8.22 | yes |
| E4/10 | compile | TelusCel | 10.71 us | 10.73 us | 21.26 KB | 21.44 KB | 1.27 | yes |
| E4/10 | compile | GreatPartsCel | 8.59 us | 8.61 us | 19.00 KB | 19.18 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 8.42 us | 8.44 us | 18.46 KB | 18.64 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 2.70 us | - | 4.08 KB | - | 2.58 | yes |
| E4/10 | evaluate | DotnetCel | 3.93 us | - | 5.31 KB | - | 3.75 | yes |
| E4/10 | evaluate | Cel.NET | 4.01 us | - | 7.08 KB | - | 3.83 | yes |
| E4/10 | evaluate | TelusCel | 54.80 us | - | 98.86 KB | - | 52.33 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.07 us | - | 880 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.05 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 1.11 us | - | 664 B | - | 1.00 |  |
| E4/10 | full cycle | Celly | 27.21 us | 27.23 us | 79.78 KB | 79.96 KB | 2.77 | yes |
| E4/10 | full cycle | DotnetCel | 23.09 us | 23.11 us | 46.42 KB | 46.60 KB | 2.35 | yes |
| E4/10 | full cycle | Cel.NET | 74.74 us | 74.77 us | 141.74 KB | 141.92 KB | 7.62 | yes |
| E4/10 | full cycle | TelusCel | 74.49 us | 74.51 us | 120.12 KB | 120.30 KB | 7.59 | yes |
| E4/10 | full cycle | GreatPartsCel | 10.12 us | 10.15 us | 19.86 KB | 20.04 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 9.81 us | 9.83 us | 19.32 KB | 19.50 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 10.65 us | 10.68 us | 19.50 KB | 19.68 KB | 1.00 |  |
| E4/100 | text creation | - | - | 22.8 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 23.38 us | 23.40 us | 75.71 KB | 75.89 KB | 2.79 | yes |
| E4/100 | compile | DotnetCel | 15.69 us | 15.71 us | 37.80 KB | 37.98 KB | 1.88 | yes |
| E4/100 | compile | Cel.NET | 70.51 us | 70.54 us | 134.64 KB | 134.82 KB | 8.43 | yes |
| E4/100 | compile | TelusCel | 10.64 us | 10.66 us | 21.26 KB | 21.44 KB | 1.27 | yes |
| E4/100 | compile | GreatPartsCel | 8.57 us | 8.59 us | 19.00 KB | 19.18 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 8.37 us | 8.39 us | 18.46 KB | 18.64 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 28.62 us | - | 42.44 KB | - | 2.38 | yes |
| E4/100 | evaluate | DotnetCel | 48.84 us | - | 70.80 KB | - | 4.06 | yes |
| E4/100 | evaluate | Cel.NET | 47.27 us | - | 96.53 KB | - | 3.93 | yes |
| E4/100 | evaluate | TelusCel | 671.75 us | - | 1.15 MB | - | 55.89 | yes |
| E4/100 | evaluate | GreatPartsCel | 12.02 us | - | 9.61 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 13.24 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 13.04 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 54.33 us | 54.35 us | 118.18 KB | 118.36 KB | 2.71 | yes |
| E4/100 | full cycle | DotnetCel | 67.43 us | 67.45 us | 111.91 KB | 112.09 KB | 3.36 | yes |
| E4/100 | full cycle | Cel.NET | 119.25 us | 119.28 us | 231.17 KB | 231.35 KB | 5.95 | yes |
| E4/100 | full cycle | TelusCel | 681.39 us | 681.41 us | 1.17 MB | 1.17 MB | 34.00 | yes |
| E4/100 | full cycle | GreatPartsCel | 21.08 us | 21.10 us | 28.61 KB | 28.79 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 20.04 us | 20.06 us | 28.07 KB | 28.25 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 22.83 us | 22.85 us | 28.25 KB | 28.43 KB | 1.00 |  |
| E5 | text creation | - | - | 23.7 ns | - | 168 B | - | |
| E5 | compile | Celly | 9.18 us | 9.21 us | 35.54 KB | 35.71 KB | 4.87 | yes |
| E5 | compile | DotnetCel | 7.89 us | 7.92 us | 24.02 KB | 24.19 KB | 4.19 | yes |
| E5 | compile | Cel.NET | 12.61 us | 12.63 us | 30.60 KB | 30.76 KB | 6.69 | yes |
| E5 | compile | TelusCel | 3.95 us | 3.97 us | 8.64 KB | 8.80 KB | 2.10 | yes |
| E5 | compile | GreatPartsCel | 2.19 us | 2.22 us | 6.57 KB | 6.73 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 1.88 us | 1.91 us | 5.51 KB | 5.68 KB | 1.00 |  |
| E5 | evaluate | Celly | 377.1 ns | - | 752 B | - | 1.72 | yes |
| E5 | evaluate | DotnetCel | 383.9 ns | - | 448 B | - | 1.75 | yes |
| E5 | evaluate | Cel.NET | 66.36 us | - | 252.50 KB | - | 303.19 | yes |
| E5 | evaluate | TelusCel | 8.20 us | - | 14.34 KB | - | 37.46 | yes |
| E5 | evaluate | GreatPartsCel | 276.8 ns | - | 328 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 218.9 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 252.5 ns | - | 160 B | - | 1.00 |  |
| E5 | full cycle | Celly | 9.79 us | 9.81 us | 36.26 KB | 36.43 KB | 4.29 | yes |
| E5 | full cycle | DotnetCel | 11.21 us | 11.23 us | 27.60 KB | 27.77 KB | 4.92 | yes |
| E5 | full cycle | Cel.NET | 84.33 us | 84.36 us | 283.09 KB | 283.25 KB | 37.01 | yes |
| E5 | full cycle | TelusCel | 11.76 us | 11.78 us | 22.98 KB | 23.15 KB | 5.16 | yes |
| E5 | full cycle | GreatPartsCel | 2.74 us | 2.77 us | 6.89 KB | 7.05 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.28 us | 2.30 us | 5.83 KB | 6.00 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 2.73 us | 2.75 us | 6.05 KB | 6.22 KB | 1.00 |  |
| E6 | text creation | - | - | 22.5 ns | - | 184 B | - | |
| E6 | compile | Celly | 11.56 us | 11.58 us | 42.96 KB | 43.14 KB | 4.33 | yes |
| E6 | compile | Cel.NET | 25.02 us | 25.04 us | 50.46 KB | 50.64 KB | 9.38 | yes |
| E6 | compile | TelusCel | 6.84 us | 6.86 us | 13.97 KB | 14.15 KB | 2.56 | yes |
| E6 | compile | GreatPartsCel | 3.68 us | 3.70 us | 9.35 KB | 9.53 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 2.67 us | 2.69 us | 6.47 KB | 6.65 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 353.7 ns | - | 912 B | - | 1.72 | yes |
| E6 | evaluate | Cel.NET | 416.5 ns | - | 1.57 KB | - | 2.02 | yes |
| E6 | evaluate | TelusCel | 9.32 us | - | 18.30 KB | - | 45.28 | yes |
| E6 | evaluate | GreatPartsCel | 266.6 ns | - | 328 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 205.9 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 300.1 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 11.65 us | 11.67 us | 43.84 KB | 44.02 KB | 3.78 | yes |
| E6 | full cycle | Cel.NET | 25.35 us | 25.37 us | 52.03 KB | 52.21 KB | 8.22 | yes |
| E6 | full cycle | TelusCel | 16.94 us | 16.96 us | 32.27 KB | 32.45 KB | 5.49 | yes |
| E6 | full cycle | GreatPartsCel | 4.10 us | 4.12 us | 9.66 KB | 9.84 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 3.08 us | 3.11 us | 6.79 KB | 6.97 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 3.57 us | 3.59 us | 7.08 KB | 7.26 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 21.5 ns | - | 128 B | - | |
| E7 | compile | Celly | 11.03 us | 11.05 us | 45.25 KB | 45.37 KB | 3.92 | yes |
| E7 | compile | DotnetCel | 8.79 us | 8.81 us | 25.66 KB | 25.78 KB | 3.13 | yes |
| E7 | compile | Cel.NET | 23.71 us | 23.73 us | 53.68 KB | 53.81 KB | 8.44 | yes |
| E7 | compile | GreatPartsCel | 3.02 us | 3.04 us | 7.67 KB | 7.79 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 2.81 us | 2.83 us | 7.22 KB | 7.34 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 720.0 ns | - | 1.17 KB | - | 3.59 | yes |
| E7 | evaluate | DotnetCel | 335.1 ns | - | 592 B | - | 1.67 | yes |
| E7 | evaluate | Cel.NET | 487.5 ns | - | 1.61 KB | - | 2.43 | yes |
| E7 | evaluate | GreatPartsCel | 278.7 ns | - | 328 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 200.8 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 250.5 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 12.38 us | 12.40 us | 46.32 KB | 46.45 KB | 3.84 | yes |
| E7 | full cycle | DotnetCel | 11.57 us | 11.59 us | 28.74 KB | 28.87 KB | 3.59 | yes |
| E7 | full cycle | Cel.NET | 24.51 us | 24.53 us | 55.29 KB | 55.42 KB | 7.61 | yes |
| E7 | full cycle | GreatPartsCel | 3.48 us | 3.50 us | 7.99 KB | 8.12 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 3.22 us | 3.24 us | 7.54 KB | 7.66 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 3.55 us | 3.57 us | 7.53 KB | 7.65 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 24.2 ns | - | 192 B | - | |
| E8 | compile | Celly | 13.69 us | 13.71 us | 52.52 KB | 52.71 KB | 3.21 | yes |
| E8 | compile | Cel.NET | 49.05 us | 49.07 us | 99.24 KB | 99.43 KB | 11.48 | yes |
| E8 | compile | TelusCel | 9.45 us | 9.47 us | 19.39 KB | 19.58 KB | 2.21 | yes |
| E8 | compile | GreatPartsCel | 4.80 us | 4.83 us | 12.65 KB | 12.84 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 4.27 us | 4.30 us | 10.89 KB | 11.07 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 745.4 ns | - | 1.66 KB | - | 3.11 | yes |
| E8 | evaluate | Cel.NET | 441.2 ns | - | 1.64 KB | - | 1.84 | yes |
| E8 | evaluate | TelusCel | 8.45 us | - | 16.02 KB | - | 35.27 | yes |
| E8 | evaluate | GreatPartsCel | 296.8 ns | - | 376 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 239.7 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 300.5 ns | - | 160 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 15.08 us | 15.10 us | 54.17 KB | 54.36 KB | 3.26 | yes |
| E8 | full cycle | Cel.NET | 49.76 us | 49.78 us | 100.87 KB | 101.06 KB | 10.74 | yes |
| E8 | full cycle | TelusCel | 19.74 us | 19.77 us | 35.41 KB | 35.60 KB | 4.26 | yes |
| E8 | full cycle | GreatPartsCel | 5.35 us | 5.38 us | 13.02 KB | 13.21 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 4.63 us | 4.66 us | 11.25 KB | 11.44 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 5.12 us | 5.14 us | 11.46 KB | 11.65 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 23.5 ns | - | 136 B | - | |
| E9 | compile | Celly | 14.22 us | 14.24 us | 52.40 KB | 52.54 KB | 4.55 | yes |
| E9 | compile | DotnetCel | 8.83 us | 8.85 us | 26.69 KB | 26.82 KB | 2.82 | yes |
| E9 | compile | Cel.NET | 33.84 us | 33.86 us | 77.45 KB | 77.58 KB | 10.82 | yes |
| E9 | compile | TelusCel | 4.84 us | 4.86 us | 10.08 KB | 10.21 KB | 1.55 | yes |
| E9 | compile | GreatPartsCel | 3.75 us | 3.77 us | 10.12 KB | 10.26 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 3.13 us | 3.15 us | 8.44 KB | 8.57 KB | 1.00 |  |
| E9 | evaluate | Celly | 354.5 ns | - | 1.11 KB | - | 1.45 | yes |
| E9 | evaluate | DotnetCel | 401.0 ns | - | 808 B | - | 1.64 | yes |
| E9 | evaluate | Cel.NET | 443.8 ns | - | 1.75 KB | - | 1.82 | yes |
| E9 | evaluate | TelusCel | 5.43 us | - | 10.77 KB | - | 22.28 | yes |
| E9 | evaluate | GreatPartsCel | 307.3 ns | - | 512 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 243.9 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 333.7 ns | - | 344 B | - | 1.00 |  |
| E9 | full cycle | Celly | 13.04 us | 13.07 us | 53.51 KB | 53.64 KB | 3.67 | yes |
| E9 | full cycle | DotnetCel | 11.51 us | 11.53 us | 29.98 KB | 30.12 KB | 3.24 | yes |
| E9 | full cycle | Cel.NET | 34.94 us | 34.96 us | 79.19 KB | 79.32 KB | 9.84 | yes |
| E9 | full cycle | TelusCel | 11.34 us | 11.37 us | 20.85 KB | 20.98 KB | 3.20 | yes |
| E9 | full cycle | GreatPartsCel | 4.26 us | 4.28 us | 10.63 KB | 10.76 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 3.55 us | 3.57 us | 8.94 KB | 9.07 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 4.05 us | 4.07 us | 9.19 KB | 9.32 KB | 1.00 |  |
