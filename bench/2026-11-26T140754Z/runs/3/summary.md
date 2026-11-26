## Run 3: scenario x operation

- Run 3: 2026-11-26T11:49:16.351Z - 2026-11-26T13:35:09.382Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 14.5 ns | - | 160 B | - | |
| E1 | compile | Celly | 8.28 us | 8.30 us | 42.82 KB | 42.98 KB | 3.58 | yes |
| E1 | compile | DotnetCel | 5.60 us | 5.62 us | 26.31 KB | 26.47 KB | 2.42 | yes |
| E1 | compile | Cel.NET | 16.36 us | 16.37 us | 49.87 KB | 50.03 KB | 7.07 | yes |
| E1 | compile | TelusCel | 4.27 us | 4.28 us | 10.33 KB | 10.48 KB | 1.84 | yes |
| E1 | compile | GreatPartsCel | 2.76 us | 2.77 us | 8.85 KB | 9.00 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 2.31 us | 2.33 us | 6.97 KB | 7.12 KB | 1.00 |  |
| E1 | evaluate | Celly | 241.7 ns | - | 832 B | - | 1.61 | yes |
| E1 | evaluate | DotnetCel | 242.0 ns | - | 560 B | - | 1.62 | yes |
| E1 | evaluate | Cel.NET | 286.6 ns | - | 1.52 KB | - | 1.91 | yes |
| E1 | evaluate | TelusCel | 4.61 us | - | 11.16 KB | - | 30.80 | yes |
| E1 | evaluate | GreatPartsCel | 196.2 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 149.7 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 249.8 ns | - | 160 B | - | 1.03 |  |
| E1 | full cycle | Celly | 8.65 us | 8.67 us | 43.64 KB | 43.80 KB | 3.44 | yes |
| E1 | full cycle | DotnetCel | 7.45 us | 7.46 us | 29.37 KB | 29.52 KB | 2.96 | yes |
| E1 | full cycle | Cel.NET | 15.68 us | 15.69 us | 51.38 KB | 51.54 KB | 6.24 | yes |
| E1 | full cycle | TelusCel | 9.03 us | 9.05 us | 21.49 KB | 21.65 KB | 3.59 | yes |
| E1 | full cycle | GreatPartsCel | 3.30 us | 3.32 us | 9.15 KB | 9.31 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 2.51 us | 2.53 us | 7.28 KB | 7.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 2.71 us | 2.73 us | 7.40 KB | 7.56 KB | 1.00 |  |
| E2/10 | text creation | - | - | 13.9 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 10.44 us | 10.45 us | 54.53 KB | 54.68 KB | 2.70 | yes |
| E2/10 | compile | DotnetCel | 7.55 us | 7.56 us | 32.52 KB | 32.66 KB | 1.95 | yes |
| E2/10 | compile | Cel.NET | 21.56 us | 21.58 us | 71.99 KB | 72.13 KB | 5.57 | yes |
| E2/10 | compile | TelusCel | 6.68 us | 6.70 us | 15.50 KB | 15.65 KB | 1.73 | yes |
| E2/10 | compile | GreatPartsCel | 4.35 us | 4.37 us | 12.23 KB | 12.38 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 3.87 us | 3.88 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 2.06 us | - | 2.96 KB | - | 2.01 | yes |
| E2/10 | evaluate | DotnetCel | 2.98 us | - | 4.07 KB | - | 2.91 | yes |
| E2/10 | evaluate | Cel.NET | 2.37 us | - | 6.15 KB | - | 2.32 | yes |
| E2/10 | evaluate | TelusCel | 58.98 us | - | 111.34 KB | - | 57.70 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.11 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.02 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 1.21 us | - | 160 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 12.10 us | 12.12 us | 57.41 KB | 57.56 KB | 2.32 | yes |
| E2/10 | full cycle | DotnetCel | 12.53 us | 12.55 us | 39.90 KB | 40.05 KB | 2.40 | yes |
| E2/10 | full cycle | Cel.NET | 23.69 us | 23.70 us | 78.13 KB | 78.28 KB | 4.54 | yes |
| E2/10 | full cycle | TelusCel | 57.51 us | 57.53 us | 126.84 KB | 126.98 KB | 11.03 | yes |
| E2/10 | full cycle | GreatPartsCel | 5.63 us | 5.65 us | 12.59 KB | 12.73 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 5.21 us | 5.23 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 5.29 us | 5.30 us | 11.99 KB | 12.14 KB | 1.00 |  |
| E2/100 | text creation | - | - | 14.5 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 10.02 us | 10.03 us | 54.53 KB | 54.68 KB | 2.58 | yes |
| E2/100 | compile | DotnetCel | 7.76 us | 7.78 us | 32.52 KB | 32.66 KB | 2.00 | yes |
| E2/100 | compile | Cel.NET | 21.15 us | 21.17 us | 71.99 KB | 72.14 KB | 5.45 | yes |
| E2/100 | compile | TelusCel | 6.77 us | 6.79 us | 15.50 KB | 15.65 KB | 1.74 | yes |
| E2/100 | compile | GreatPartsCel | 4.21 us | 4.23 us | 12.23 KB | 12.38 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 3.88 us | 3.90 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 17.80 us | - | 21.66 KB | - | 2.00 | yes |
| E2/100 | evaluate | DotnetCel | 25.72 us | - | 31.91 KB | - | 2.88 | yes |
| E2/100 | evaluate | Cel.NET | 20.78 us | - | 54.28 KB | - | 2.33 | yes |
| E2/100 | evaluate | TelusCel | 472.69 us | - | 1.03 MB | - | 53.03 | yes |
| E2/100 | evaluate | GreatPartsCel | 9.49 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 8.91 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 9.91 us | - | 160 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 27.52 us | 27.54 us | 76.20 KB | 76.35 KB | 2.02 | yes |
| E2/100 | full cycle | DotnetCel | 35.23 us | 35.24 us | 67.74 KB | 67.89 KB | 2.58 | yes |
| E2/100 | full cycle | Cel.NET | 44.39 us | 44.40 us | 126.26 KB | 126.41 KB | 3.25 | yes |
| E2/100 | full cycle | TelusCel | 535.55 us | 535.57 us | 1.05 MB | 1.05 MB | 39.26 | yes |
| E2/100 | full cycle | GreatPartsCel | 13.64 us | 13.66 us | 12.58 KB | 12.73 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 13.71 us | 13.72 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 14.62 us | 14.63 us | 11.99 KB | 12.14 KB | 1.00 |  |
| E3/10 | text creation | - | - | 13.0 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 9.20 us | 9.21 us | 44.35 KB | 44.50 KB | 2.84 | yes |
| E3/10 | compile | DotnetCel | 7.05 us | 7.06 us | 30.26 KB | 30.41 KB | 2.18 | yes |
| E3/10 | compile | Cel.NET | 17.38 us | 17.39 us | 53.14 KB | 53.29 KB | 5.37 | yes |
| E3/10 | compile | TelusCel | 5.22 us | 5.24 us | 13.38 KB | 13.52 KB | 1.61 | yes |
| E3/10 | compile | GreatPartsCel | 3.49 us | 3.50 us | 10.81 KB | 10.96 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 3.24 us | 3.25 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 545.5 ns | - | 1.61 KB | - | 2.50 | yes |
| E3/10 | evaluate | DotnetCel | 648.0 ns | - | 1.78 KB | - | 2.97 | yes |
| E3/10 | evaluate | Cel.NET | 609.0 ns | - | 2.07 KB | - | 2.79 | yes |
| E3/10 | evaluate | TelusCel | 26.61 us | - | 62.83 KB | - | 122.00 | yes |
| E3/10 | evaluate | GreatPartsCel | 282.9 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 218.1 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 330.3 ns | - | 160 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 9.44 us | 9.46 us | 45.93 KB | 46.08 KB | 2.59 | yes |
| E3/10 | full cycle | DotnetCel | 9.43 us | 9.44 us | 35.35 KB | 35.50 KB | 2.59 | yes |
| E3/10 | full cycle | Cel.NET | 18.37 us | 18.39 us | 55.21 KB | 55.36 KB | 5.05 | yes |
| E3/10 | full cycle | TelusCel | 32.99 us | 33.00 us | 76.20 KB | 76.35 KB | 9.06 | yes |
| E3/10 | full cycle | GreatPartsCel | 3.98 us | 3.99 us | 11.17 KB | 11.32 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 3.72 us | 3.73 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 3.64 us | 3.65 us | 10.74 KB | 10.89 KB | 1.00 |  |
| E3/100 | text creation | - | - | 12.6 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 8.91 us | 8.92 us | 44.33 KB | 44.48 KB | 2.79 | yes |
| E3/100 | compile | DotnetCel | 6.70 us | 6.71 us | 30.26 KB | 30.41 KB | 2.10 | yes |
| E3/100 | compile | Cel.NET | 18.04 us | 18.05 us | 53.12 KB | 53.27 KB | 5.65 | yes |
| E3/100 | compile | TelusCel | 5.01 us | 5.02 us | 13.38 KB | 13.52 KB | 1.57 | yes |
| E3/100 | compile | GreatPartsCel | 3.46 us | 3.47 us | 10.81 KB | 10.96 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 3.19 us | 3.21 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 2.67 us | - | 6.95 KB | - | 12.38 | yes |
| E3/100 | evaluate | DotnetCel | 2.41 us | - | 7.12 KB | - | 11.19 | yes |
| E3/100 | evaluate | Cel.NET | 1.62 us | - | 8.81 KB | - | 7.54 | yes |
| E3/100 | evaluate | TelusCel | 245.03 us | - | 570.20 KB | - | 1137.27 | yes |
| E3/100 | evaluate | GreatPartsCel | 270.4 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 215.5 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 338.3 ns | - | 160 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 10.92 us | 10.94 us | 51.34 KB | 51.49 KB | 3.28 | yes |
| E3/100 | full cycle | DotnetCel | 10.92 us | 10.93 us | 40.69 KB | 40.84 KB | 3.27 | yes |
| E3/100 | full cycle | Cel.NET | 20.03 us | 20.04 us | 61.93 KB | 62.08 KB | 6.01 | yes |
| E3/100 | full cycle | TelusCel | 259.43 us | 259.45 us | 583.57 KB | 583.72 KB | 77.83 | yes |
| E3/100 | full cycle | GreatPartsCel | 3.94 us | 3.95 us | 11.17 KB | 11.32 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 3.33 us | 3.35 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 3.61 us | 3.63 us | 10.75 KB | 10.90 KB | 1.00 |  |
| E4/10 | text creation | - | - | 13.5 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 15.85 us | 15.87 us | 75.71 KB | 75.89 KB | 2.53 | yes |
| E4/10 | compile | DotnetCel | 10.15 us | 10.16 us | 37.80 KB | 37.98 KB | 1.62 | yes |
| E4/10 | compile | Cel.NET | 53.19 us | 53.20 us | 134.64 KB | 134.82 KB | 8.48 | yes |
| E4/10 | compile | TelusCel | 8.88 us | 8.90 us | 21.26 KB | 21.44 KB | 1.42 | yes |
| E4/10 | compile | GreatPartsCel | 6.89 us | 6.90 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 6.27 us | 6.29 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 1.99 us | - | 4.08 KB | - | 2.57 | yes |
| E4/10 | evaluate | DotnetCel | 2.83 us | - | 5.31 KB | - | 3.67 | yes |
| E4/10 | evaluate | Cel.NET | 2.97 us | - | 7.08 KB | - | 3.85 | yes |
| E4/10 | evaluate | TelusCel | 41.13 us | - | 98.86 KB | - | 53.27 | yes |
| E4/10 | evaluate | GreatPartsCel | 923.9 ns | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 772.0 ns | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 946.5 ns | - | 664 B | - | 1.00 |  |
| E4/10 | full cycle | Celly | 17.91 us | 17.92 us | 79.81 KB | 79.99 KB | 2.63 | yes |
| E4/10 | full cycle | DotnetCel | 14.98 us | 14.99 us | 46.42 KB | 46.60 KB | 2.20 | yes |
| E4/10 | full cycle | Cel.NET | 54.97 us | 54.99 us | 141.73 KB | 141.91 KB | 8.09 | yes |
| E4/10 | full cycle | TelusCel | 55.79 us | 55.81 us | 120.12 KB | 120.30 KB | 8.21 | yes |
| E4/10 | full cycle | GreatPartsCel | 8.19 us | 8.21 us | 19.71 KB | 19.89 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 6.80 us | 6.81 us | 19.30 KB | 19.48 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 7.58 us | 7.59 us | 19.37 KB | 19.55 KB | 1.00 |  |
| E4/100 | text creation | - | - | 14.8 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 15.42 us | 15.44 us | 75.73 KB | 75.91 KB | 2.68 | yes |
| E4/100 | compile | DotnetCel | 10.09 us | 10.10 us | 37.80 KB | 37.98 KB | 1.75 | yes |
| E4/100 | compile | Cel.NET | 49.28 us | 49.30 us | 134.67 KB | 134.85 KB | 8.57 | yes |
| E4/100 | compile | TelusCel | 9.77 us | 9.78 us | 21.26 KB | 21.44 KB | 1.70 | yes |
| E4/100 | compile | GreatPartsCel | 7.02 us | 7.04 us | 18.87 KB | 19.05 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 5.75 us | 5.76 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 23.38 us | - | 42.44 KB | - | 2.50 | yes |
| E4/100 | evaluate | DotnetCel | 33.05 us | - | 70.80 KB | - | 3.54 | yes |
| E4/100 | evaluate | Cel.NET | 33.06 us | - | 96.53 KB | - | 3.54 | yes |
| E4/100 | evaluate | TelusCel | 639.36 us | - | 1.15 MB | - | 68.41 | yes |
| E4/100 | evaluate | GreatPartsCel | 10.29 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 9.35 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 9.99 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 38.85 us | 38.86 us | 118.14 KB | 118.32 KB | 2.57 | yes |
| E4/100 | full cycle | DotnetCel | 44.38 us | 44.40 us | 111.91 KB | 112.09 KB | 2.94 | yes |
| E4/100 | full cycle | Cel.NET | 83.65 us | 83.67 us | 231.19 KB | 231.37 KB | 5.54 | yes |
| E4/100 | full cycle | TelusCel | 514.52 us | 514.54 us | 1.17 MB | 1.17 MB | 34.06 | yes |
| E4/100 | full cycle | GreatPartsCel | 16.93 us | 16.94 us | 28.46 KB | 28.64 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 15.11 us | 15.12 us | 28.04 KB | 28.22 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 18.09 us | 18.11 us | 28.12 KB | 28.29 KB | 1.00 |  |
| E5 | text creation | - | - | 13.7 ns | - | 168 B | - | |
| E5 | compile | Celly | 7.07 us | 7.09 us | 35.55 KB | 35.72 KB | 4.82 | yes |
| E5 | compile | DotnetCel | 4.92 us | 4.94 us | 24.02 KB | 24.19 KB | 3.35 | yes |
| E5 | compile | Cel.NET | 9.78 us | 9.79 us | 30.58 KB | 30.75 KB | 6.66 | yes |
| E5 | compile | TelusCel | 3.18 us | 3.19 us | 8.64 KB | 8.80 KB | 2.16 | yes |
| E5 | compile | GreatPartsCel | 1.96 us | 1.98 us | 6.44 KB | 6.60 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 1.47 us | 1.48 us | 5.50 KB | 5.67 KB | 1.00 |  |
| E5 | evaluate | Celly | 275.7 ns | - | 752 B | - | 1.71 | yes |
| E5 | evaluate | DotnetCel | 287.5 ns | - | 448 B | - | 1.78 | yes |
| E5 | evaluate | Cel.NET | 46.16 us | - | 252.50 KB | - | 285.64 | yes |
| E5 | evaluate | TelusCel | 5.50 us | - | 14.34 KB | - | 34.03 | yes |
| E5 | evaluate | GreatPartsCel | 223.7 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 161.6 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 270.0 ns | - | 160 B | - | 1.00 |  |
| E5 | full cycle | Celly | 7.08 us | 7.09 us | 36.29 KB | 36.45 KB | 4.10 | yes |
| E5 | full cycle | DotnetCel | 7.01 us | 7.02 us | 27.60 KB | 27.77 KB | 4.06 | yes |
| E5 | full cycle | Cel.NET | 55.02 us | 55.03 us | 283.09 KB | 283.26 KB | 31.84 | yes |
| E5 | full cycle | TelusCel | 9.94 us | 9.95 us | 22.98 KB | 23.15 KB | 5.75 | yes |
| E5 | full cycle | GreatPartsCel | 2.30 us | 2.31 us | 6.75 KB | 6.91 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 1.73 us | 1.74 us | 5.82 KB | 5.98 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 2.40 us | 2.41 us | 5.92 KB | 6.08 KB | 1.00 |  |
| E6 | text creation | - | - | 14.9 ns | - | 184 B | - | |
| E6 | compile | Celly | 7.62 us | 7.64 us | 42.96 KB | 43.14 KB | 3.72 | yes |
| E6 | compile | Cel.NET | 17.35 us | 17.37 us | 50.47 KB | 50.65 KB | 8.47 | yes |
| E6 | compile | TelusCel | 5.99 us | 6.01 us | 13.97 KB | 14.15 KB | 2.92 | yes |
| E6 | compile | GreatPartsCel | 2.95 us | 2.96 us | 9.21 KB | 9.39 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 2.05 us | 2.06 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 248.0 ns | - | 912 B | - | 1.51 | yes |
| E6 | evaluate | Cel.NET | 281.2 ns | - | 1.57 KB | - | 1.71 | yes |
| E6 | evaluate | TelusCel | 8.58 us | - | 18.30 KB | - | 52.19 | yes |
| E6 | evaluate | GreatPartsCel | 205.3 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 164.4 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 269.2 ns | - | 160 B | - | 1.09 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 7.73 us | 7.74 us | 43.88 KB | 44.06 KB | 3.45 | yes |
| E6 | full cycle | Cel.NET | 17.80 us | 17.82 us | 52.04 KB | 52.22 KB | 7.94 | yes |
| E6 | full cycle | TelusCel | 14.17 us | 14.19 us | 32.27 KB | 32.45 KB | 6.32 | yes |
| E6 | full cycle | GreatPartsCel | 3.30 us | 3.32 us | 9.52 KB | 9.70 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 2.24 us | 2.26 us | 6.77 KB | 6.95 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 2.96 us | 2.98 us | 6.93 KB | 7.11 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 12.9 ns | - | 128 B | - | |
| E7 | compile | Celly | 7.92 us | 7.93 us | 45.25 KB | 45.37 KB | 3.61 | yes |
| E7 | compile | DotnetCel | 5.33 us | 5.34 us | 25.66 KB | 25.78 KB | 2.43 | yes |
| E7 | compile | Cel.NET | 16.26 us | 16.27 us | 53.70 KB | 53.83 KB | 7.42 | yes |
| E7 | compile | GreatPartsCel | 2.65 us | 2.66 us | 7.52 KB | 7.65 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 2.19 us | 2.20 us | 7.08 KB | 7.21 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 532.8 ns | - | 1.17 KB | - | 3.43 | yes |
| E7 | evaluate | DotnetCel | 229.5 ns | - | 592 B | - | 1.48 | yes |
| E7 | evaluate | Cel.NET | 374.1 ns | - | 1.61 KB | - | 2.41 | yes |
| E7 | evaluate | GreatPartsCel | 219.1 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 155.2 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 237.0 ns | - | 160 B | - | 1.03 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 8.89 us | 8.90 us | 46.40 KB | 46.53 KB | 3.37 | yes |
| E7 | full cycle | DotnetCel | 7.21 us | 7.22 us | 28.74 KB | 28.87 KB | 2.73 | yes |
| E7 | full cycle | Cel.NET | 17.19 us | 17.21 us | 55.30 KB | 55.42 KB | 6.52 | yes |
| E7 | full cycle | GreatPartsCel | 2.87 us | 2.88 us | 7.83 KB | 7.96 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 2.64 us | 2.65 us | 7.40 KB | 7.53 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 3.11 us | 3.13 us | 7.37 KB | 7.50 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 15.7 ns | - | 192 B | - | |
| E8 | compile | Celly | 10.73 us | 10.74 us | 52.40 KB | 52.58 KB | 3.04 | yes |
| E8 | compile | Cel.NET | 34.28 us | 34.29 us | 99.24 KB | 99.43 KB | 9.73 | yes |
| E8 | compile | TelusCel | 8.74 us | 8.75 us | 19.39 KB | 19.58 KB | 2.48 | yes |
| E8 | compile | GreatPartsCel | 3.99 us | 4.01 us | 12.52 KB | 12.70 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 3.52 us | 3.54 us | 10.87 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 547.2 ns | - | 1.66 KB | - | 3.22 | yes |
| E8 | evaluate | Cel.NET | 307.3 ns | - | 1.64 KB | - | 1.81 | yes |
| E8 | evaluate | TelusCel | 7.13 us | - | 16.02 KB | - | 41.92 | yes |
| E8 | evaluate | GreatPartsCel | 224.4 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 170.1 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 280.9 ns | - | 160 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 10.38 us | 10.40 us | 54.18 KB | 54.37 KB | 2.66 | yes |
| E8 | full cycle | Cel.NET | 35.02 us | 35.03 us | 100.88 KB | 101.07 KB | 8.99 | yes |
| E8 | full cycle | TelusCel | 17.02 us | 17.03 us | 35.41 KB | 35.60 KB | 4.37 | yes |
| E8 | full cycle | GreatPartsCel | 4.07 us | 4.09 us | 12.89 KB | 13.08 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 3.90 us | 3.91 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 4.27 us | 4.29 us | 11.31 KB | 11.50 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 12.2 ns | - | 136 B | - | |
| E9 | compile | Celly | 9.40 us | 9.41 us | 52.39 KB | 52.52 KB | 3.67 | yes |
| E9 | compile | DotnetCel | 5.48 us | 5.49 us | 26.69 KB | 26.82 KB | 2.14 | yes |
| E9 | compile | Cel.NET | 24.93 us | 24.94 us | 77.55 KB | 77.69 KB | 9.75 | yes |
| E9 | compile | TelusCel | 4.36 us | 4.37 us | 10.08 KB | 10.21 KB | 1.70 | yes |
| E9 | compile | GreatPartsCel | 2.78 us | 2.79 us | 9.99 KB | 10.12 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 2.56 us | 2.57 us | 8.43 KB | 8.56 KB | 1.00 |  |
| E9 | evaluate | Celly | 240.3 ns | - | 1.11 KB | - | 1.33 | yes |
| E9 | evaluate | DotnetCel | 286.7 ns | - | 808 B | - | 1.58 | yes |
| E9 | evaluate | Cel.NET | 327.4 ns | - | 1.75 KB | - | 1.81 | yes |
| E9 | evaluate | TelusCel | 4.74 us | - | 10.77 KB | - | 26.19 | yes |
| E9 | evaluate | GreatPartsCel | 245.1 ns | - | 472 B | - | 1.02 |  |
| E9 | evaluate | GPCel-Input | 181.2 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 322.9 ns | - | 344 B | - | 1.34 | yes |
| E9 | full cycle | Celly | 9.22 us | 9.24 us | 53.41 KB | 53.54 KB | 3.56 | yes |
| E9 | full cycle | DotnetCel | 8.18 us | 8.20 us | 29.99 KB | 30.12 KB | 3.16 | yes |
| E9 | full cycle | Cel.NET | 23.95 us | 23.96 us | 79.18 KB | 79.31 KB | 9.23 | yes |
| E9 | full cycle | TelusCel | 9.08 us | 9.09 us | 20.85 KB | 20.98 KB | 3.50 | yes |
| E9 | full cycle | GreatPartsCel | 3.21 us | 3.22 us | 10.48 KB | 10.62 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 2.59 us | 2.61 us | 8.91 KB | 9.04 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 3.31 us | 3.32 us | 9.04 KB | 9.17 KB | 1.00 |  |
