## Run 2: scenario x operation

- Run 2: 2026-11-26T10:03:01.123Z - 2026-11-26T11:49:16.351Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 12.6 ns | - | 160 B | - | |
| E1 | compile | Celly | 8.15 us | 8.16 us | 42.79 KB | 42.95 KB | 3.48 | yes |
| E1 | compile | DotnetCel | 5.84 us | 5.85 us | 26.31 KB | 26.47 KB | 2.49 | yes |
| E1 | compile | Cel.NET | 15.43 us | 15.45 us | 49.86 KB | 50.01 KB | 6.58 | yes |
| E1 | compile | TelusCel | 4.59 us | 4.60 us | 10.33 KB | 10.48 KB | 1.96 | yes |
| E1 | compile | GreatPartsCel | 2.45 us | 2.46 us | 8.84 KB | 9.00 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 2.34 us | 2.36 us | 6.97 KB | 7.13 KB | 1.00 |  |
| E1 | evaluate | Celly | 220.5 ns | - | 832 B | - | 1.41 | yes |
| E1 | evaluate | DotnetCel | 229.5 ns | - | 560 B | - | 1.47 | yes |
| E1 | evaluate | Cel.NET | 285.0 ns | - | 1.52 KB | - | 1.83 | yes |
| E1 | evaluate | TelusCel | 5.51 us | - | 11.16 KB | - | 35.34 | yes |
| E1 | evaluate | GreatPartsCel | 180.4 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 156.0 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 251.2 ns | - | 160 B | - | 1.14 | yes |
| E1 | full cycle | Celly | 7.65 us | 7.66 us | 43.61 KB | 43.77 KB | 2.99 | yes |
| E1 | full cycle | DotnetCel | 7.22 us | 7.23 us | 29.37 KB | 29.52 KB | 2.83 | yes |
| E1 | full cycle | Cel.NET | 15.66 us | 15.67 us | 51.40 KB | 51.55 KB | 6.13 | yes |
| E1 | full cycle | TelusCel | 9.80 us | 9.81 us | 21.49 KB | 21.65 KB | 3.83 | yes |
| E1 | full cycle | GreatPartsCel | 2.87 us | 2.88 us | 9.15 KB | 9.31 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 2.56 us | 2.57 us | 7.28 KB | 7.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 2.92 us | 2.93 us | 7.40 KB | 7.56 KB | 1.00 |  |
| E2/10 | text creation | - | - | 12.6 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 9.97 us | 9.99 us | 54.53 KB | 54.68 KB | 2.71 | yes |
| E2/10 | compile | DotnetCel | 7.72 us | 7.74 us | 32.52 KB | 32.66 KB | 2.10 | yes |
| E2/10 | compile | Cel.NET | 21.94 us | 21.95 us | 71.99 KB | 72.13 KB | 5.97 | yes |
| E2/10 | compile | TelusCel | 6.21 us | 6.22 us | 15.50 KB | 15.65 KB | 1.69 | yes |
| E2/10 | compile | GreatPartsCel | 3.68 us | 3.69 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 3.77 us | 3.78 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 2.09 us | - | 2.96 KB | - | 2.03 | yes |
| E2/10 | evaluate | DotnetCel | 3.01 us | - | 4.07 KB | - | 2.92 | yes |
| E2/10 | evaluate | Cel.NET | 2.36 us | - | 6.15 KB | - | 2.30 | yes |
| E2/10 | evaluate | TelusCel | 48.66 us | - | 111.34 KB | - | 47.34 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.12 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.03 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 1.26 us | - | 160 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 11.94 us | 11.95 us | 57.49 KB | 57.64 KB | 2.31 | yes |
| E2/10 | full cycle | DotnetCel | 13.00 us | 13.01 us | 39.90 KB | 40.05 KB | 2.52 | yes |
| E2/10 | full cycle | Cel.NET | 24.95 us | 24.96 us | 78.15 KB | 78.29 KB | 4.83 | yes |
| E2/10 | full cycle | TelusCel | 54.83 us | 54.85 us | 126.84 KB | 126.98 KB | 10.61 | yes |
| E2/10 | full cycle | GreatPartsCel | 5.17 us | 5.18 us | 12.59 KB | 12.73 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 5.34 us | 5.36 us | 11.93 KB | 12.08 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 5.51 us | 5.52 us | 11.99 KB | 12.14 KB | 1.00 |  |
| E2/100 | text creation | - | - | 13.0 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 9.50 us | 9.52 us | 54.53 KB | 54.68 KB | 2.50 | yes |
| E2/100 | compile | DotnetCel | 7.42 us | 7.43 us | 32.52 KB | 32.66 KB | 1.95 | yes |
| E2/100 | compile | Cel.NET | 22.38 us | 22.40 us | 71.98 KB | 72.13 KB | 5.90 | yes |
| E2/100 | compile | TelusCel | 6.43 us | 6.44 us | 15.50 KB | 15.65 KB | 1.69 | yes |
| E2/100 | compile | GreatPartsCel | 3.80 us | 3.81 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 4.01 us | 4.03 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 15.85 us | - | 21.66 KB | - | 1.75 | yes |
| E2/100 | evaluate | DotnetCel | 27.93 us | - | 31.91 KB | - | 3.09 | yes |
| E2/100 | evaluate | Cel.NET | 22.10 us | - | 54.28 KB | - | 2.44 | yes |
| E2/100 | evaluate | TelusCel | 457.70 us | - | 1.03 MB | - | 50.55 | yes |
| E2/100 | evaluate | GreatPartsCel | 9.90 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 9.05 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 10.16 us | - | 160 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 23.96 us | 23.98 us | 76.19 KB | 76.34 KB | 1.82 | yes |
| E2/100 | full cycle | DotnetCel | 34.87 us | 34.89 us | 67.74 KB | 67.89 KB | 2.65 | yes |
| E2/100 | full cycle | Cel.NET | 42.70 us | 42.71 us | 126.27 KB | 126.42 KB | 3.25 | yes |
| E2/100 | full cycle | TelusCel | 454.70 us | 454.71 us | 1.05 MB | 1.05 MB | 34.59 | yes |
| E2/100 | full cycle | GreatPartsCel | 13.14 us | 13.16 us | 12.58 KB | 12.73 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 13.39 us | 13.40 us | 11.93 KB | 12.08 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 13.87 us | 13.88 us | 11.99 KB | 12.14 KB | 1.00 |  |
| E3/10 | text creation | - | - | 12.9 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 7.80 us | 7.81 us | 44.41 KB | 44.55 KB | 2.35 | yes |
| E3/10 | compile | DotnetCel | 6.77 us | 6.78 us | 30.26 KB | 30.41 KB | 2.04 | yes |
| E3/10 | compile | Cel.NET | 16.01 us | 16.02 us | 53.12 KB | 53.27 KB | 4.82 | yes |
| E3/10 | compile | TelusCel | 4.95 us | 4.96 us | 13.38 KB | 13.52 KB | 1.49 | yes |
| E3/10 | compile | GreatPartsCel | 3.66 us | 3.67 us | 10.81 KB | 10.96 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 3.32 us | 3.33 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 526.3 ns | - | 1.61 KB | - | 2.38 | yes |
| E3/10 | evaluate | DotnetCel | 675.8 ns | - | 1.78 KB | - | 3.06 | yes |
| E3/10 | evaluate | Cel.NET | 559.3 ns | - | 2.07 KB | - | 2.53 | yes |
| E3/10 | evaluate | TelusCel | 30.50 us | - | 62.83 KB | - | 138.16 | yes |
| E3/10 | evaluate | GreatPartsCel | 290.1 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 220.8 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 330.9 ns | - | 160 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 10.03 us | 10.04 us | 45.93 KB | 46.08 KB | 2.70 | yes |
| E3/10 | full cycle | DotnetCel | 9.41 us | 9.42 us | 35.35 KB | 35.50 KB | 2.53 | yes |
| E3/10 | full cycle | Cel.NET | 17.14 us | 17.15 us | 55.19 KB | 55.34 KB | 4.61 | yes |
| E3/10 | full cycle | TelusCel | 32.29 us | 32.30 us | 76.20 KB | 76.35 KB | 8.68 | yes |
| E3/10 | full cycle | GreatPartsCel | 4.11 us | 4.12 us | 11.17 KB | 11.32 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 3.72 us | 3.73 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 3.76 us | 3.78 us | 10.74 KB | 10.89 KB | 1.00 |  |
| E3/100 | text creation | - | - | 12.1 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 8.06 us | 8.08 us | 44.33 KB | 44.48 KB | 2.59 | yes |
| E3/100 | compile | DotnetCel | 6.54 us | 6.55 us | 30.26 KB | 30.41 KB | 2.10 | yes |
| E3/100 | compile | Cel.NET | 15.98 us | 16.00 us | 53.12 KB | 53.27 KB | 5.13 | yes |
| E3/100 | compile | TelusCel | 4.95 us | 4.96 us | 13.38 KB | 13.52 KB | 1.59 | yes |
| E3/100 | compile | GreatPartsCel | 3.63 us | 3.64 us | 10.81 KB | 10.96 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 3.12 us | 3.13 us | 10.31 KB | 10.46 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 2.54 us | - | 6.95 KB | - | 11.97 | yes |
| E3/100 | evaluate | DotnetCel | 2.26 us | - | 7.12 KB | - | 10.62 | yes |
| E3/100 | evaluate | Cel.NET | 1.32 us | - | 8.81 KB | - | 6.22 | yes |
| E3/100 | evaluate | TelusCel | 267.84 us | - | 570.20 KB | - | 1260.59 | yes |
| E3/100 | evaluate | GreatPartsCel | 278.5 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 212.5 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 327.6 ns | - | 160 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 10.25 us | 10.26 us | 51.28 KB | 51.43 KB | 3.03 | yes |
| E3/100 | full cycle | DotnetCel | 11.44 us | 11.45 us | 40.69 KB | 40.84 KB | 3.38 | yes |
| E3/100 | full cycle | Cel.NET | 17.00 us | 17.01 us | 61.93 KB | 62.08 KB | 5.02 | yes |
| E3/100 | full cycle | TelusCel | 282.28 us | 282.29 us | 583.57 KB | 583.72 KB | 83.40 | yes |
| E3/100 | full cycle | GreatPartsCel | 4.17 us | 4.18 us | 11.17 KB | 11.32 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 3.38 us | 3.40 us | 10.69 KB | 10.84 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 3.65 us | 3.67 us | 10.74 KB | 10.89 KB | 1.00 |  |
| E4/10 | text creation | - | - | 14.0 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 16.30 us | 16.32 us | 75.71 KB | 75.89 KB | 2.61 | yes |
| E4/10 | compile | DotnetCel | 10.22 us | 10.23 us | 37.80 KB | 37.98 KB | 1.64 | yes |
| E4/10 | compile | Cel.NET | 46.29 us | 46.31 us | 134.65 KB | 134.83 KB | 7.41 | yes |
| E4/10 | compile | TelusCel | 9.83 us | 9.85 us | 21.26 KB | 21.44 KB | 1.57 | yes |
| E4/10 | compile | GreatPartsCel | 7.24 us | 7.26 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 6.24 us | 6.26 us | 18.46 KB | 18.64 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 2.14 us | - | 4.08 KB | - | 2.74 | yes |
| E4/10 | evaluate | DotnetCel | 2.85 us | - | 5.31 KB | - | 3.64 | yes |
| E4/10 | evaluate | Cel.NET | 2.71 us | - | 7.08 KB | - | 3.47 | yes |
| E4/10 | evaluate | TelusCel | 52.39 us | - | 98.86 KB | - | 67.04 | yes |
| E4/10 | evaluate | GreatPartsCel | 910.3 ns | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 781.4 ns | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 994.4 ns | - | 664 B | - | 1.00 |  |
| E4/10 | full cycle | Celly | 19.46 us | 19.47 us | 79.81 KB | 79.99 KB | 2.73 | yes |
| E4/10 | full cycle | DotnetCel | 14.90 us | 14.91 us | 46.42 KB | 46.60 KB | 2.09 | yes |
| E4/10 | full cycle | Cel.NET | 50.02 us | 50.04 us | 141.72 KB | 141.90 KB | 7.03 | yes |
| E4/10 | full cycle | TelusCel | 59.17 us | 59.18 us | 120.12 KB | 120.30 KB | 8.31 | yes |
| E4/10 | full cycle | GreatPartsCel | 8.35 us | 8.36 us | 19.71 KB | 19.89 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 7.12 us | 7.13 us | 19.31 KB | 19.49 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 8.00 us | 8.02 us | 19.36 KB | 19.54 KB | 1.00 |  |
| E4/100 | text creation | - | - | 14.8 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 16.95 us | 16.97 us | 75.72 KB | 75.90 KB | 2.81 | yes |
| E4/100 | compile | DotnetCel | 10.21 us | 10.23 us | 37.80 KB | 37.98 KB | 1.69 | yes |
| E4/100 | compile | Cel.NET | 46.32 us | 46.33 us | 134.64 KB | 134.82 KB | 7.67 | yes |
| E4/100 | compile | TelusCel | 9.06 us | 9.07 us | 21.26 KB | 21.44 KB | 1.50 | yes |
| E4/100 | compile | GreatPartsCel | 7.27 us | 7.29 us | 18.87 KB | 19.05 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 6.04 us | 6.05 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 23.17 us | - | 42.44 KB | - | 2.58 | yes |
| E4/100 | evaluate | DotnetCel | 32.80 us | - | 70.80 KB | - | 3.65 | yes |
| E4/100 | evaluate | Cel.NET | 31.03 us | - | 96.53 KB | - | 3.45 | yes |
| E4/100 | evaluate | TelusCel | 539.45 us | - | 1.15 MB | - | 60.00 | yes |
| E4/100 | evaluate | GreatPartsCel | 10.25 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 8.99 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 9.62 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 37.08 us | 37.10 us | 118.14 KB | 118.32 KB | 2.29 | yes |
| E4/100 | full cycle | DotnetCel | 45.58 us | 45.59 us | 111.91 KB | 112.09 KB | 2.82 | yes |
| E4/100 | full cycle | Cel.NET | 77.34 us | 77.35 us | 231.17 KB | 231.35 KB | 4.79 | yes |
| E4/100 | full cycle | TelusCel | 630.33 us | 630.35 us | 1.17 MB | 1.17 MB | 39.01 | yes |
| E4/100 | full cycle | GreatPartsCel | 17.69 us | 17.71 us | 28.46 KB | 28.64 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 16.16 us | 16.17 us | 28.05 KB | 28.23 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 17.91 us | 17.92 us | 28.12 KB | 28.29 KB | 1.00 |  |
| E5 | text creation | - | - | 12.7 ns | - | 168 B | - | |
| E5 | compile | Celly | 6.01 us | 6.03 us | 35.55 KB | 35.72 KB | 3.77 | yes |
| E5 | compile | DotnetCel | 5.15 us | 5.16 us | 24.02 KB | 24.19 KB | 3.23 | yes |
| E5 | compile | Cel.NET | 8.85 us | 8.86 us | 30.60 KB | 30.76 KB | 5.55 | yes |
| E5 | compile | TelusCel | 3.07 us | 3.08 us | 8.64 KB | 8.80 KB | 1.93 | yes |
| E5 | compile | GreatPartsCel | 2.16 us | 2.17 us | 6.44 KB | 6.60 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 1.59 us | 1.61 us | 5.50 KB | 5.67 KB | 1.00 |  |
| E5 | evaluate | Celly | 249.6 ns | - | 752 B | - | 1.48 | yes |
| E5 | evaluate | DotnetCel | 295.6 ns | - | 448 B | - | 1.76 | yes |
| E5 | evaluate | Cel.NET | 44.36 us | - | 252.50 KB | - | 263.37 | yes |
| E5 | evaluate | TelusCel | 5.21 us | - | 14.34 KB | - | 30.91 | yes |
| E5 | evaluate | GreatPartsCel | 259.1 ns | - | 288 B | - | 1.04 |  |
| E5 | evaluate | GPCel-Input | 168.4 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 272.5 ns | - | 160 B | - | 1.09 |  |
| E5 | full cycle | Celly | 6.45 us | 6.46 us | 36.27 KB | 36.43 KB | 3.45 | yes |
| E5 | full cycle | DotnetCel | 7.22 us | 7.24 us | 27.60 KB | 27.77 KB | 3.86 | yes |
| E5 | full cycle | Cel.NET | 56.21 us | 56.22 us | 283.09 KB | 283.26 KB | 30.06 | yes |
| E5 | full cycle | TelusCel | 8.89 us | 8.90 us | 22.98 KB | 23.15 KB | 4.75 | yes |
| E5 | full cycle | GreatPartsCel | 2.52 us | 2.54 us | 6.75 KB | 6.91 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 1.87 us | 1.88 us | 5.82 KB | 5.98 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 2.40 us | 2.41 us | 5.93 KB | 6.09 KB | 1.00 |  |
| E6 | text creation | - | - | 14.5 ns | - | 184 B | - | |
| E6 | compile | Celly | 7.76 us | 7.77 us | 42.95 KB | 43.13 KB | 3.24 | yes |
| E6 | compile | Cel.NET | 17.09 us | 17.10 us | 50.46 KB | 50.64 KB | 7.13 | yes |
| E6 | compile | TelusCel | 5.47 us | 5.48 us | 13.97 KB | 14.15 KB | 2.28 | yes |
| E6 | compile | GreatPartsCel | 3.19 us | 3.20 us | 9.21 KB | 9.39 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 2.40 us | 2.41 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 244.5 ns | - | 912 B | - | 1.50 | yes |
| E6 | evaluate | Cel.NET | 274.4 ns | - | 1.57 KB | - | 1.68 | yes |
| E6 | evaluate | TelusCel | 8.69 us | - | 18.30 KB | - | 53.15 | yes |
| E6 | evaluate | GreatPartsCel | 211.1 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 163.5 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 269.9 ns | - | 160 B | - | 1.10 | yes |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 7.90 us | 7.92 us | 43.84 KB | 44.02 KB | 3.52 | yes |
| E6 | full cycle | Cel.NET | 17.56 us | 17.58 us | 52.03 KB | 52.21 KB | 7.83 | yes |
| E6 | full cycle | TelusCel | 13.04 us | 13.06 us | 32.27 KB | 32.45 KB | 5.82 | yes |
| E6 | full cycle | GreatPartsCel | 3.45 us | 3.47 us | 9.52 KB | 9.70 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 2.24 us | 2.26 us | 6.77 KB | 6.95 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 2.92 us | 2.94 us | 6.93 KB | 7.11 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 11.7 ns | - | 128 B | - | |
| E7 | compile | Celly | 7.88 us | 7.89 us | 45.14 KB | 45.26 KB | 3.31 | yes |
| E7 | compile | DotnetCel | 5.49 us | 5.50 us | 25.66 KB | 25.78 KB | 2.31 | yes |
| E7 | compile | Cel.NET | 16.15 us | 16.16 us | 53.69 KB | 53.82 KB | 6.78 | yes |
| E7 | compile | GreatPartsCel | 2.74 us | 2.75 us | 7.52 KB | 7.65 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 2.38 us | 2.39 us | 7.09 KB | 7.21 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 535.2 ns | - | 1.17 KB | - | 3.46 | yes |
| E7 | evaluate | DotnetCel | 235.4 ns | - | 592 B | - | 1.52 | yes |
| E7 | evaluate | Cel.NET | 376.9 ns | - | 1.61 KB | - | 2.44 | yes |
| E7 | evaluate | GreatPartsCel | 216.5 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 154.8 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 245.0 ns | - | 160 B | - | 1.04 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 9.05 us | 9.06 us | 46.32 KB | 46.45 KB | 3.73 | yes |
| E7 | full cycle | DotnetCel | 8.38 us | 8.39 us | 28.74 KB | 28.87 KB | 3.45 | yes |
| E7 | full cycle | Cel.NET | 17.65 us | 17.66 us | 55.29 KB | 55.42 KB | 7.27 | yes |
| E7 | full cycle | GreatPartsCel | 3.06 us | 3.07 us | 7.83 KB | 7.96 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 2.43 us | 2.44 us | 7.40 KB | 7.53 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 2.93 us | 2.95 us | 7.37 KB | 7.50 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 13.8 ns | - | 192 B | - | |
| E8 | compile | Celly | 9.62 us | 9.63 us | 52.40 KB | 52.58 KB | 3.20 | yes |
| E8 | compile | Cel.NET | 34.97 us | 34.98 us | 99.24 KB | 99.43 KB | 11.62 | yes |
| E8 | compile | TelusCel | 7.70 us | 7.71 us | 19.39 KB | 19.58 KB | 2.56 | yes |
| E8 | compile | GreatPartsCel | 4.16 us | 4.17 us | 12.52 KB | 12.70 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 3.01 us | 3.02 us | 10.87 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 530.7 ns | - | 1.66 KB | - | 3.19 | yes |
| E8 | evaluate | Cel.NET | 346.1 ns | - | 1.64 KB | - | 2.08 | yes |
| E8 | evaluate | TelusCel | 6.60 us | - | 16.02 KB | - | 39.65 | yes |
| E8 | evaluate | GreatPartsCel | 236.2 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 166.5 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 270.9 ns | - | 160 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 10.57 us | 10.58 us | 54.17 KB | 54.36 KB | 3.23 | yes |
| E8 | full cycle | Cel.NET | 36.62 us | 36.63 us | 100.90 KB | 101.08 KB | 11.20 | yes |
| E8 | full cycle | TelusCel | 16.02 us | 16.04 us | 35.41 KB | 35.60 KB | 4.90 | yes |
| E8 | full cycle | GreatPartsCel | 4.82 us | 4.83 us | 12.88 KB | 13.07 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 3.27 us | 3.28 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 4.06 us | 4.07 us | 11.31 KB | 11.50 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 13.1 ns | - | 136 B | - | |
| E9 | compile | Celly | 10.21 us | 10.23 us | 52.42 KB | 52.56 KB | 4.56 | yes |
| E9 | compile | DotnetCel | 6.32 us | 6.33 us | 26.69 KB | 26.82 KB | 2.82 | yes |
| E9 | compile | Cel.NET | 23.69 us | 23.70 us | 77.44 KB | 77.57 KB | 10.59 | yes |
| E9 | compile | TelusCel | 4.01 us | 4.02 us | 10.08 KB | 10.21 KB | 1.79 | yes |
| E9 | compile | GreatPartsCel | 3.25 us | 3.27 us | 9.99 KB | 10.12 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 2.24 us | 2.25 us | 8.42 KB | 8.55 KB | 1.00 |  |
| E9 | evaluate | Celly | 259.9 ns | - | 1.11 KB | - | 1.51 | yes |
| E9 | evaluate | DotnetCel | 281.7 ns | - | 808 B | - | 1.64 | yes |
| E9 | evaluate | Cel.NET | 292.1 ns | - | 1.75 KB | - | 1.70 | yes |
| E9 | evaluate | TelusCel | 4.17 us | - | 10.77 KB | - | 24.23 | yes |
| E9 | evaluate | GreatPartsCel | 261.4 ns | - | 472 B | - | 1.01 |  |
| E9 | evaluate | GPCel-Input | 172.3 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 302.3 ns | - | 344 B | - | 1.16 | yes |
| E9 | full cycle | Celly | 9.87 us | 9.89 us | 53.37 KB | 53.50 KB | 3.75 | yes |
| E9 | full cycle | DotnetCel | 8.53 us | 8.55 us | 29.99 KB | 30.12 KB | 3.24 | yes |
| E9 | full cycle | Cel.NET | 23.94 us | 23.95 us | 79.19 KB | 79.32 KB | 9.09 | yes |
| E9 | full cycle | TelusCel | 8.52 us | 8.53 us | 20.85 KB | 20.98 KB | 3.23 | yes |
| E9 | full cycle | GreatPartsCel | 3.61 us | 3.62 us | 10.48 KB | 10.62 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 2.64 us | 2.65 us | 8.91 KB | 9.04 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 3.01 us | 3.02 us | 9.04 KB | 9.17 KB | 1.00 |  |
