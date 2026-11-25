## Run 3: scenario x operation

- Run 3: 2026-11-25T17:44:13.322Z - 2026-11-25T19:04:46.022Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 31.9 ns | - | 160 B | - | |
| E1 | compile | Celly | 14.56 us | 14.59 us | 42.83 KB | 42.99 KB | 4.56 | yes |
| E1 | compile | DotnetCel | 10.94 us | 10.97 us | 26.31 KB | 26.47 KB | 3.43 | yes |
| E1 | compile | Cel.NET | 28.46 us | 28.49 us | 49.86 KB | 50.01 KB | 8.92 | yes |
| E1 | compile | TelusCel | 6.34 us | 6.38 us | 10.33 KB | 10.48 KB | 1.99 | yes |
| E1 | compile | GreatPartsCel | 4.08 us | 4.11 us | 8.82 KB | 8.98 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.19 us | 3.22 us | 6.97 KB | 7.12 KB | 1.00 |  |
| E1 | evaluate | Celly | 456.9 ns | - | 832 B | - | 1.88 | yes |
| E1 | evaluate | DotnetCel | 429.5 ns | - | 560 B | - | 1.76 | yes |
| E1 | evaluate | Cel.NET | 529.7 ns | - | 1.52 KB | - | 2.17 | yes |
| E1 | evaluate | TelusCel | 8.02 us | - | 11.16 KB | - | 32.92 | yes |
| E1 | evaluate | GreatPartsCel | 292.6 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 243.6 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 385.8 ns | - | 160 B | - | 1.00 |  |
| E1 | full cycle | Celly | 15.17 us | 15.20 us | 43.48 KB | 43.64 KB | 4.24 | yes |
| E1 | full cycle | DotnetCel | 14.14 us | 14.18 us | 29.37 KB | 29.52 KB | 3.96 | yes |
| E1 | full cycle | Cel.NET | 29.22 us | 29.25 us | 51.38 KB | 51.54 KB | 8.17 | yes |
| E1 | full cycle | TelusCel | 14.13 us | 14.16 us | 21.49 KB | 21.65 KB | 3.95 | yes |
| E1 | full cycle | GreatPartsCel | 4.68 us | 4.71 us | 9.14 KB | 9.30 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 3.58 us | 3.61 us | 7.28 KB | 7.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 4.28 us | 4.31 us | 7.39 KB | 7.55 KB | 1.00 |  |
| E2/10 | text creation | - | - | 32.4 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 19.23 us | 19.26 us | 54.52 KB | 54.66 KB | 3.18 | yes |
| E2/10 | compile | DotnetCel | 14.98 us | 15.01 us | 32.52 KB | 32.66 KB | 2.48 | yes |
| E2/10 | compile | Cel.NET | 38.00 us | 38.03 us | 71.99 KB | 72.14 KB | 6.29 | yes |
| E2/10 | compile | TelusCel | 9.76 us | 9.80 us | 15.50 KB | 15.65 KB | 1.62 | yes |
| E2/10 | compile | GreatPartsCel | 6.25 us | 6.28 us | 12.21 KB | 12.36 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 6.04 us | 6.07 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 3.66 us | - | 2.96 KB | - | 2.54 | yes |
| E2/10 | evaluate | DotnetCel | 5.32 us | - | 4.07 KB | - | 3.70 | yes |
| E2/10 | evaluate | Cel.NET | 4.36 us | - | 6.15 KB | - | 3.03 | yes |
| E2/10 | evaluate | TelusCel | 79.49 us | - | 111.34 KB | - | 55.17 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.56 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.44 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 2.09 us | - | 800 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 23.50 us | 23.53 us | 57.41 KB | 57.56 KB | 2.97 | yes |
| E2/10 | full cycle | DotnetCel | 24.24 us | 24.27 us | 39.90 KB | 40.05 KB | 3.06 | yes |
| E2/10 | full cycle | Cel.NET | 42.98 us | 43.01 us | 78.14 KB | 78.29 KB | 5.43 | yes |
| E2/10 | full cycle | TelusCel | 92.06 us | 92.10 us | 126.84 KB | 126.98 KB | 11.63 | yes |
| E2/10 | full cycle | GreatPartsCel | 8.31 us | 8.34 us | 12.57 KB | 12.72 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 7.91 us | 7.95 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 9.13 us | 9.16 us | 12.61 KB | 12.75 KB | 1.00 |  |
| E2/100 | text creation | - | - | 32.0 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 19.09 us | 19.12 us | 54.53 KB | 54.68 KB | 3.18 | yes |
| E2/100 | compile | DotnetCel | 14.99 us | 15.02 us | 32.52 KB | 32.66 KB | 2.50 | yes |
| E2/100 | compile | Cel.NET | 37.80 us | 37.83 us | 71.99 KB | 72.14 KB | 6.30 | yes |
| E2/100 | compile | TelusCel | 9.78 us | 9.81 us | 15.50 KB | 15.65 KB | 1.63 | yes |
| E2/100 | compile | GreatPartsCel | 6.27 us | 6.30 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 6.00 us | 6.03 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 32.29 us | - | 21.66 KB | - | 2.55 | yes |
| E2/100 | evaluate | DotnetCel | 47.58 us | - | 31.91 KB | - | 3.76 | yes |
| E2/100 | evaluate | Cel.NET | 38.97 us | - | 54.28 KB | - | 3.08 | yes |
| E2/100 | evaluate | TelusCel | 782.14 us | - | 1.03 MB | - | 61.79 | yes |
| E2/100 | evaluate | GreatPartsCel | 12.66 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 12.95 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 18.34 us | - | 6.41 KB | - | 1.00 |  |
| E2/100 | full cycle | Celly | 52.11 us | 52.15 us | 76.18 KB | 76.33 KB | 2.75 | yes |
| E2/100 | full cycle | DotnetCel | 65.83 us | 65.86 us | 67.74 KB | 67.88 KB | 3.47 | yes |
| E2/100 | full cycle | Cel.NET | 79.87 us | 79.90 us | 126.26 KB | 126.41 KB | 4.21 | yes |
| E2/100 | full cycle | TelusCel | 799.78 us | 799.81 us | 1.05 MB | 1.05 MB | 42.14 | yes |
| E2/100 | full cycle | GreatPartsCel | 19.92 us | 19.96 us | 12.58 KB | 12.73 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 18.98 us | 19.01 us | 11.93 KB | 12.08 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 24.99 us | 25.02 us | 18.23 KB | 18.38 KB | 1.00 |  |
| E3/10 | text creation | - | - | 32.8 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 15.89 us | 15.92 us | 44.34 KB | 44.49 KB | 3.28 | yes |
| E3/10 | compile | DotnetCel | 13.52 us | 13.55 us | 30.26 KB | 30.41 KB | 2.79 | yes |
| E3/10 | compile | Cel.NET | 27.96 us | 27.99 us | 53.14 KB | 53.29 KB | 5.77 | yes |
| E3/10 | compile | TelusCel | 7.92 us | 7.95 us | 13.38 KB | 13.52 KB | 1.64 | yes |
| E3/10 | compile | GreatPartsCel | 5.09 us | 5.12 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 4.84 us | 4.87 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 972.8 ns | - | 1.61 KB | - | 3.00 | yes |
| E3/10 | evaluate | DotnetCel | 1.23 us | - | 1.78 KB | - | 3.80 | yes |
| E3/10 | evaluate | Cel.NET | 977.3 ns | - | 2.07 KB | - | 3.01 | yes |
| E3/10 | evaluate | TelusCel | 45.43 us | - | 62.83 KB | - | 140.07 | yes |
| E3/10 | evaluate | GreatPartsCel | 440.6 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 324.3 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 859.1 ns | - | 544 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 16.72 us | 16.75 us | 45.92 KB | 46.07 KB | 3.12 | yes |
| E3/10 | full cycle | DotnetCel | 18.71 us | 18.74 us | 35.35 KB | 35.50 KB | 3.49 | yes |
| E3/10 | full cycle | Cel.NET | 29.73 us | 29.76 us | 55.20 KB | 55.34 KB | 5.55 | yes |
| E3/10 | full cycle | TelusCel | 54.10 us | 54.13 us | 76.20 KB | 76.35 KB | 10.10 | yes |
| E3/10 | full cycle | GreatPartsCel | 5.73 us | 5.76 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 5.36 us | 5.39 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 6.43 us | 6.46 us | 11.11 KB | 11.26 KB | 1.00 |  |
| E3/100 | text creation | - | - | 32.0 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 15.87 us | 15.91 us | 44.38 KB | 44.52 KB | 3.32 | yes |
| E3/100 | compile | DotnetCel | 13.50 us | 13.53 us | 30.26 KB | 30.41 KB | 2.82 | yes |
| E3/100 | compile | Cel.NET | 28.05 us | 28.09 us | 53.14 KB | 53.29 KB | 5.86 | yes |
| E3/100 | compile | TelusCel | 7.93 us | 7.96 us | 13.38 KB | 13.52 KB | 1.66 | yes |
| E3/100 | compile | GreatPartsCel | 5.05 us | 5.09 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 4.79 us | 4.82 us | 10.32 KB | 10.46 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.61 us | - | 6.95 KB | - | 13.48 | yes |
| E3/100 | evaluate | DotnetCel | 4.26 us | - | 7.12 KB | - | 12.46 | yes |
| E3/100 | evaluate | Cel.NET | 2.68 us | - | 8.81 KB | - | 7.84 | yes |
| E3/100 | evaluate | TelusCel | 401.52 us | - | 570.20 KB | - | 1174.05 | yes |
| E3/100 | evaluate | GreatPartsCel | 424.4 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 342.0 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 3.41 us | - | 3.34 KB | - | 1.27 | yes |
| E3/100 | full cycle | Celly | 20.70 us | 20.73 us | 51.27 KB | 51.41 KB | 3.85 | yes |
| E3/100 | full cycle | DotnetCel | 21.70 us | 21.73 us | 40.69 KB | 40.84 KB | 4.04 | yes |
| E3/100 | full cycle | Cel.NET | 31.19 us | 31.22 us | 61.94 KB | 62.09 KB | 5.81 | yes |
| E3/100 | full cycle | TelusCel | 467.13 us | 467.16 us | 583.57 KB | 583.72 KB | 86.96 | yes |
| E3/100 | full cycle | GreatPartsCel | 5.84 us | 5.87 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 5.37 us | 5.40 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 9.07 us | 9.10 us | 13.92 KB | 14.07 KB | 1.00 |  |
| E4/10 | text creation | - | - | 34.0 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 29.47 us | 29.51 us | 75.73 KB | 75.91 KB | 2.88 | yes |
| E4/10 | compile | DotnetCel | 19.03 us | 19.07 us | 37.80 KB | 37.98 KB | 1.86 | yes |
| E4/10 | compile | Cel.NET | 85.94 us | 85.97 us | 134.64 KB | 134.82 KB | 8.38 | yes |
| E4/10 | compile | TelusCel | 13.45 us | 13.48 us | 21.26 KB | 21.44 KB | 1.31 | yes |
| E4/10 | compile | GreatPartsCel | 10.48 us | 10.51 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 10.25 us | 10.28 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 4.25 us | - | 4.08 KB | - | 3.31 | yes |
| E4/10 | evaluate | DotnetCel | 5.09 us | - | 5.31 KB | - | 3.97 | yes |
| E4/10 | evaluate | Cel.NET | 5.03 us | - | 7.08 KB | - | 3.92 | yes |
| E4/10 | evaluate | TelusCel | 80.74 us | - | 98.86 KB | - | 62.93 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.39 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.28 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 1.96 us | - | 1.27 KB | - | 1.00 |  |
| E4/10 | full cycle | Celly | 34.12 us | 34.16 us | 79.80 KB | 79.98 KB | 2.84 | yes |
| E4/10 | full cycle | DotnetCel | 28.47 us | 28.50 us | 46.42 KB | 46.60 KB | 2.37 | yes |
| E4/10 | full cycle | Cel.NET | 92.93 us | 92.96 us | 141.72 KB | 141.90 KB | 7.72 | yes |
| E4/10 | full cycle | TelusCel | 84.41 us | 84.44 us | 120.12 KB | 120.30 KB | 7.02 | yes |
| E4/10 | full cycle | GreatPartsCel | 12.40 us | 12.43 us | 19.71 KB | 19.89 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 12.03 us | 12.06 us | 19.30 KB | 19.48 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 13.48 us | 13.51 us | 19.98 KB | 20.16 KB | 1.00 |  |
| E4/100 | text creation | - | - | 33.9 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 29.80 us | 29.83 us | 75.70 KB | 75.88 KB | 2.92 | yes |
| E4/100 | compile | DotnetCel | 19.10 us | 19.13 us | 37.80 KB | 37.98 KB | 1.87 | yes |
| E4/100 | compile | Cel.NET | 86.27 us | 86.30 us | 134.64 KB | 134.82 KB | 8.45 | yes |
| E4/100 | compile | TelusCel | 13.59 us | 13.63 us | 21.26 KB | 21.44 KB | 1.33 | yes |
| E4/100 | compile | GreatPartsCel | 10.58 us | 10.62 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 10.21 us | 10.25 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 38.90 us | - | 42.44 KB | - | 2.58 | yes |
| E4/100 | evaluate | DotnetCel | 58.00 us | - | 70.80 KB | - | 3.85 | yes |
| E4/100 | evaluate | Cel.NET | 58.94 us | - | 96.53 KB | - | 3.91 | yes |
| E4/100 | evaluate | TelusCel | 963.02 us | - | 1.15 MB | - | 63.92 | yes |
| E4/100 | evaluate | GreatPartsCel | 15.07 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 15.07 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 20.01 us | - | 15.65 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 71.85 us | 71.88 us | 118.15 KB | 118.33 KB | 2.85 | yes |
| E4/100 | full cycle | DotnetCel | 79.95 us | 79.99 us | 111.91 KB | 112.09 KB | 3.18 | yes |
| E4/100 | full cycle | Cel.NET | 149.39 us | 149.42 us | 231.17 KB | 231.35 KB | 5.93 | yes |
| E4/100 | full cycle | TelusCel | 856.00 us | 856.03 us | 1.17 MB | 1.17 MB | 33.99 | yes |
| E4/100 | full cycle | GreatPartsCel | 25.18 us | 25.22 us | 28.46 KB | 28.64 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 26.81 us | 26.85 us | 28.05 KB | 28.23 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 32.29 us | 32.32 us | 34.36 KB | 34.54 KB | 1.00 |  |
| E5 | text creation | - | - | 33.8 ns | - | 168 B | - | |
| E5 | compile | Celly | 11.86 us | 11.89 us | 35.54 KB | 35.71 KB | 5.13 | yes |
| E5 | compile | DotnetCel | 9.55 us | 9.59 us | 24.02 KB | 24.19 KB | 4.13 | yes |
| E5 | compile | Cel.NET | 16.40 us | 16.43 us | 30.58 KB | 30.75 KB | 7.09 | yes |
| E5 | compile | TelusCel | 4.91 us | 4.94 us | 8.64 KB | 8.80 KB | 2.12 | yes |
| E5 | compile | GreatPartsCel | 2.72 us | 2.75 us | 6.43 KB | 6.59 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.31 us | 2.35 us | 5.50 KB | 5.66 KB | 1.00 |  |
| E5 | evaluate | Celly | 494.1 ns | - | 752 B | - | 1.94 | yes |
| E5 | evaluate | DotnetCel | 500.6 ns | - | 448 B | - | 1.96 | yes |
| E5 | evaluate | Cel.NET | 86.05 us | - | 252.50 KB | - | 337.73 | yes |
| E5 | evaluate | TelusCel | 9.15 us | - | 14.34 KB | - | 35.93 | yes |
| E5 | evaluate | GreatPartsCel | 362.9 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 254.8 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 396.8 ns | - | 160 B | - | 1.00 |  |
| E5 | full cycle | Celly | 12.54 us | 12.57 us | 36.28 KB | 36.44 KB | 4.57 | yes |
| E5 | full cycle | DotnetCel | 13.70 us | 13.73 us | 27.60 KB | 27.77 KB | 4.99 | yes |
| E5 | full cycle | Cel.NET | 108.25 us | 108.28 us | 283.10 KB | 283.26 KB | 39.47 | yes |
| E5 | full cycle | TelusCel | 15.53 us | 15.56 us | 22.98 KB | 23.15 KB | 5.66 | yes |
| E5 | full cycle | GreatPartsCel | 3.28 us | 3.31 us | 6.74 KB | 6.90 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 2.74 us | 2.78 us | 5.81 KB | 5.97 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 3.40 us | 3.44 us | 5.91 KB | 6.08 KB | 1.00 |  |
| E6 | text creation | - | - | 35.6 ns | - | 184 B | - | |
| E6 | compile | Celly | 14.82 us | 14.86 us | 42.99 KB | 43.17 KB | 4.46 | yes |
| E6 | compile | Cel.NET | 31.26 us | 31.29 us | 50.47 KB | 50.65 KB | 9.41 | yes |
| E6 | compile | TelusCel | 8.58 us | 8.61 us | 13.97 KB | 14.15 KB | 2.58 | yes |
| E6 | compile | GreatPartsCel | 4.56 us | 4.59 us | 9.18 KB | 9.36 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.32 us | 3.36 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 452.3 ns | - | 912 B | - | 1.82 | yes |
| E6 | evaluate | Cel.NET | 546.2 ns | - | 1.57 KB | - | 2.20 | yes |
| E6 | evaluate | TelusCel | 12.32 us | - | 18.30 KB | - | 49.56 | yes |
| E6 | evaluate | GreatPartsCel | 307.3 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 248.5 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 417.3 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 15.28 us | 15.32 us | 43.86 KB | 44.04 KB | 4.09 | yes |
| E6 | full cycle | Cel.NET | 31.82 us | 31.86 us | 52.04 KB | 52.22 KB | 8.51 | yes |
| E6 | full cycle | TelusCel | 21.54 us | 21.57 us | 32.27 KB | 32.45 KB | 5.76 | yes |
| E6 | full cycle | GreatPartsCel | 5.00 us | 5.03 us | 9.49 KB | 9.67 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 3.74 us | 3.77 us | 6.77 KB | 6.95 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 4.42 us | 4.45 us | 6.90 KB | 7.08 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 29.1 ns | - | 128 B | - | |
| E7 | compile | Celly | 15.37 us | 15.40 us | 45.14 KB | 45.27 KB | 4.54 | yes |
| E7 | compile | DotnetCel | 10.63 us | 10.66 us | 25.66 KB | 25.78 KB | 3.14 | yes |
| E7 | compile | Cel.NET | 29.82 us | 29.85 us | 53.68 KB | 53.81 KB | 8.80 | yes |
| E7 | compile | GreatPartsCel | 3.76 us | 3.79 us | 7.51 KB | 7.63 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 3.39 us | 3.42 us | 7.08 KB | 7.20 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 888.0 ns | - | 1.17 KB | - | 3.47 | yes |
| E7 | evaluate | DotnetCel | 428.5 ns | - | 592 B | - | 1.68 | yes |
| E7 | evaluate | Cel.NET | 652.0 ns | - | 1.61 KB | - | 2.55 | yes |
| E7 | evaluate | GreatPartsCel | 317.3 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 255.6 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 405.2 ns | - | 160 B | - | 1.00 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 16.51 us | 16.54 us | 46.33 KB | 46.45 KB | 4.30 | yes |
| E7 | full cycle | DotnetCel | 13.98 us | 14.01 us | 28.74 KB | 28.87 KB | 3.64 | yes |
| E7 | full cycle | Cel.NET | 31.03 us | 31.06 us | 55.30 KB | 55.43 KB | 8.07 | yes |
| E7 | full cycle | GreatPartsCel | 4.25 us | 4.28 us | 7.82 KB | 7.94 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 3.84 us | 3.87 us | 7.39 KB | 7.52 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 4.46 us | 4.49 us | 7.36 KB | 7.48 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 32.5 ns | - | 192 B | - | |
| E8 | compile | Celly | 19.01 us | 19.04 us | 52.50 KB | 52.69 KB | 3.67 | yes |
| E8 | compile | Cel.NET | 60.60 us | 60.64 us | 99.23 KB | 99.42 KB | 11.71 | yes |
| E8 | compile | TelusCel | 12.20 us | 12.24 us | 19.39 KB | 19.58 KB | 2.36 | yes |
| E8 | compile | GreatPartsCel | 5.94 us | 5.98 us | 12.51 KB | 12.70 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 5.18 us | 5.21 us | 10.87 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 1.02 us | - | 1.66 KB | - | 3.55 | yes |
| E8 | evaluate | Cel.NET | 572.3 ns | - | 1.64 KB | - | 1.99 | yes |
| E8 | evaluate | TelusCel | 10.80 us | - | 16.02 KB | - | 37.61 | yes |
| E8 | evaluate | GreatPartsCel | 371.7 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 287.2 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 753.6 ns | - | 480 B | - | 1.32 | yes |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 20.13 us | 20.16 us | 54.04 KB | 54.23 KB | 3.51 | yes |
| E8 | full cycle | Cel.NET | 61.83 us | 61.87 us | 100.88 KB | 101.07 KB | 10.80 | yes |
| E8 | full cycle | TelusCel | 24.28 us | 24.32 us | 35.41 KB | 35.60 KB | 4.24 | yes |
| E8 | full cycle | GreatPartsCel | 6.57 us | 6.60 us | 12.86 KB | 13.05 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 5.73 us | 5.76 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 6.79 us | 6.82 us | 11.61 KB | 11.79 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 30.6 ns | - | 136 B | - | |
| E9 | compile | Celly | 18.04 us | 18.07 us | 52.38 KB | 52.52 KB | 4.68 | yes |
| E9 | compile | DotnetCel | 10.88 us | 10.91 us | 26.69 KB | 26.82 KB | 2.82 | yes |
| E9 | compile | Cel.NET | 42.34 us | 42.37 us | 77.55 KB | 77.69 KB | 10.99 | yes |
| E9 | compile | TelusCel | 6.08 us | 6.11 us | 10.08 KB | 10.21 KB | 1.58 | yes |
| E9 | compile | GreatPartsCel | 4.67 us | 4.71 us | 9.98 KB | 10.11 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 3.85 us | 3.88 us | 8.42 KB | 8.56 KB | 1.00 |  |
| E9 | evaluate | Celly | 466.1 ns | - | 1.11 KB | - | 1.47 | yes |
| E9 | evaluate | DotnetCel | 513.1 ns | - | 808 B | - | 1.62 | yes |
| E9 | evaluate | Cel.NET | 541.9 ns | - | 1.75 KB | - | 1.71 | yes |
| E9 | evaluate | TelusCel | 7.95 us | - | 10.77 KB | - | 25.09 | yes |
| E9 | evaluate | GreatPartsCel | 398.2 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 317.1 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 466.8 ns | - | 344 B | - | 1.00 |  |
| E9 | full cycle | Celly | 18.48 us | 18.51 us | 53.50 KB | 53.63 KB | 4.25 | yes |
| E9 | full cycle | DotnetCel | 14.23 us | 14.26 us | 29.98 KB | 30.12 KB | 3.27 | yes |
| E9 | full cycle | Cel.NET | 43.51 us | 43.54 us | 79.30 KB | 79.44 KB | 10.00 | yes |
| E9 | full cycle | TelusCel | 13.81 us | 13.84 us | 20.85 KB | 20.98 KB | 3.17 | yes |
| E9 | full cycle | GreatPartsCel | 5.21 us | 5.24 us | 10.47 KB | 10.60 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.35 us | 4.38 us | 8.91 KB | 9.05 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 5.05 us | 5.08 us | 9.02 KB | 9.16 KB | 1.00 |  |
