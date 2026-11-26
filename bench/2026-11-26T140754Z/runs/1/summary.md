## Run 1: scenario x operation

- Run 1: 2026-11-26T08:13:25.977Z - 2026-11-26T10:03:01.122Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 12.8 ns | - | 160 B | - | |
| E1 | compile | Celly | 8.65 us | 8.66 us | 42.81 KB | 42.96 KB | 3.82 | yes |
| E1 | compile | DotnetCel | 6.70 us | 6.71 us | 26.31 KB | 26.47 KB | 2.96 | yes |
| E1 | compile | Cel.NET | 16.48 us | 16.50 us | 49.87 KB | 50.02 KB | 7.28 | yes |
| E1 | compile | TelusCel | 4.25 us | 4.26 us | 10.33 KB | 10.48 KB | 1.88 | yes |
| E1 | compile | GreatPartsCel | 2.88 us | 2.89 us | 8.85 KB | 9.00 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 2.26 us | 2.28 us | 6.97 KB | 7.12 KB | 1.00 |  |
| E1 | evaluate | Celly | 247.1 ns | - | 832 B | - | 1.67 | yes |
| E1 | evaluate | DotnetCel | 247.7 ns | - | 560 B | - | 1.67 | yes |
| E1 | evaluate | Cel.NET | 297.0 ns | - | 1.52 KB | - | 2.00 | yes |
| E1 | evaluate | TelusCel | 4.41 us | - | 11.16 KB | - | 29.76 | yes |
| E1 | evaluate | GreatPartsCel | 194.5 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 148.4 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 261.4 ns | - | 160 B | - | 1.06 |  |
| E1 | full cycle | Celly | 9.71 us | 9.72 us | 43.62 KB | 43.78 KB | 3.89 | yes |
| E1 | full cycle | DotnetCel | 8.45 us | 8.46 us | 29.37 KB | 29.52 KB | 3.38 | yes |
| E1 | full cycle | Cel.NET | 17.25 us | 17.26 us | 51.40 KB | 51.55 KB | 6.91 | yes |
| E1 | full cycle | TelusCel | 9.84 us | 9.86 us | 21.49 KB | 21.65 KB | 3.94 | yes |
| E1 | full cycle | GreatPartsCel | 3.21 us | 3.22 us | 9.17 KB | 9.32 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 2.50 us | 2.51 us | 7.28 KB | 7.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 3.12 us | 3.13 us | 7.40 KB | 7.56 KB | 1.00 |  |
| E2/10 | text creation | - | - | 13.0 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 12.09 us | 12.10 us | 54.53 KB | 54.68 KB | 2.99 | yes |
| E2/10 | compile | DotnetCel | 8.87 us | 8.88 us | 32.52 KB | 32.66 KB | 2.20 | yes |
| E2/10 | compile | Cel.NET | 22.95 us | 22.96 us | 71.99 KB | 72.14 KB | 5.68 | yes |
| E2/10 | compile | TelusCel | 6.22 us | 6.24 us | 15.50 KB | 15.65 KB | 1.54 | yes |
| E2/10 | compile | GreatPartsCel | 4.04 us | 4.06 us | 12.23 KB | 12.38 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 4.04 us | 4.05 us | 11.56 KB | 11.71 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 2.10 us | - | 2.96 KB | - | 2.00 | yes |
| E2/10 | evaluate | DotnetCel | 3.14 us | - | 4.07 KB | - | 2.98 | yes |
| E2/10 | evaluate | Cel.NET | 2.48 us | - | 6.15 KB | - | 2.36 | yes |
| E2/10 | evaluate | TelusCel | 51.77 us | - | 111.34 KB | - | 49.23 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.11 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.05 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 1.24 us | - | 160 B | - | 1.00 |  |
| E2/10 | full cycle | Celly | 14.30 us | 14.31 us | 57.51 KB | 57.66 KB | 2.83 | yes |
| E2/10 | full cycle | DotnetCel | 13.53 us | 13.54 us | 39.90 KB | 40.05 KB | 2.68 | yes |
| E2/10 | full cycle | Cel.NET | 25.42 us | 25.44 us | 78.13 KB | 78.28 KB | 5.03 | yes |
| E2/10 | full cycle | TelusCel | 57.71 us | 57.72 us | 126.84 KB | 126.98 KB | 11.42 | yes |
| E2/10 | full cycle | GreatPartsCel | 5.46 us | 5.47 us | 12.58 KB | 12.73 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 5.05 us | 5.07 us | 11.93 KB | 12.08 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 5.82 us | 5.84 us | 11.99 KB | 12.14 KB | 1.00 |  |
| E2/100 | text creation | - | - | 13.4 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 12.36 us | 12.37 us | 54.55 KB | 54.70 KB | 3.21 | yes |
| E2/100 | compile | DotnetCel | 7.45 us | 7.47 us | 32.52 KB | 32.66 KB | 1.94 | yes |
| E2/100 | compile | Cel.NET | 21.75 us | 21.77 us | 71.98 KB | 72.13 KB | 5.65 | yes |
| E2/100 | compile | TelusCel | 6.55 us | 6.57 us | 15.50 KB | 15.65 KB | 1.70 | yes |
| E2/100 | compile | GreatPartsCel | 4.14 us | 4.16 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 3.85 us | 3.87 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 16.74 us | - | 21.66 KB | - | 1.83 | yes |
| E2/100 | evaluate | DotnetCel | 26.61 us | - | 31.91 KB | - | 2.91 | yes |
| E2/100 | evaluate | Cel.NET | 21.40 us | - | 54.28 KB | - | 2.34 | yes |
| E2/100 | evaluate | TelusCel | 545.18 us | - | 1.03 MB | - | 59.54 | yes |
| E2/100 | evaluate | GreatPartsCel | 9.20 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 9.16 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 10.04 us | - | 160 B | - | 1.00 |  |
| E2/100 | full cycle | Celly | 30.09 us | 30.11 us | 76.17 KB | 76.32 KB | 2.27 | yes |
| E2/100 | full cycle | DotnetCel | 36.04 us | 36.05 us | 67.74 KB | 67.89 KB | 2.72 | yes |
| E2/100 | full cycle | Cel.NET | 45.76 us | 45.78 us | 126.26 KB | 126.41 KB | 3.45 | yes |
| E2/100 | full cycle | TelusCel | 507.84 us | 507.85 us | 1.05 MB | 1.05 MB | 38.30 | yes |
| E2/100 | full cycle | GreatPartsCel | 14.06 us | 14.07 us | 12.58 KB | 12.73 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 13.26 us | 13.27 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 14.77 us | 14.78 us | 11.98 KB | 12.13 KB | 1.00 |  |
| E3/10 | text creation | - | - | 12.4 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 9.05 us | 9.06 us | 44.33 KB | 44.48 KB | 2.95 | yes |
| E3/10 | compile | DotnetCel | 6.84 us | 6.85 us | 30.26 KB | 30.41 KB | 2.23 | yes |
| E3/10 | compile | Cel.NET | 17.38 us | 17.40 us | 53.13 KB | 53.27 KB | 5.66 | yes |
| E3/10 | compile | TelusCel | 5.61 us | 5.62 us | 13.38 KB | 13.52 KB | 1.83 | yes |
| E3/10 | compile | GreatPartsCel | 3.42 us | 3.43 us | 10.81 KB | 10.96 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 3.07 us | 3.08 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 567.1 ns | - | 1.61 KB | - | 2.63 | yes |
| E3/10 | evaluate | DotnetCel | 655.1 ns | - | 1.78 KB | - | 3.04 | yes |
| E3/10 | evaluate | Cel.NET | 610.3 ns | - | 2.07 KB | - | 2.83 | yes |
| E3/10 | evaluate | TelusCel | 29.02 us | - | 62.83 KB | - | 134.78 | yes |
| E3/10 | evaluate | GreatPartsCel | 283.8 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 215.3 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 311.9 ns | - | 160 B | - | 1.00 |  |
| E3/10 | full cycle | Celly | 9.37 us | 9.38 us | 45.94 KB | 46.09 KB | 2.73 | yes |
| E3/10 | full cycle | DotnetCel | 9.38 us | 9.39 us | 35.35 KB | 35.50 KB | 2.74 | yes |
| E3/10 | full cycle | Cel.NET | 18.19 us | 18.20 us | 55.20 KB | 55.34 KB | 5.30 | yes |
| E3/10 | full cycle | TelusCel | 34.40 us | 34.41 us | 76.20 KB | 76.35 KB | 10.03 | yes |
| E3/10 | full cycle | GreatPartsCel | 3.54 us | 3.56 us | 11.17 KB | 11.32 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 3.43 us | 3.44 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 3.89 us | 3.90 us | 10.73 KB | 10.88 KB | 1.00 |  |
| E3/100 | text creation | - | - | 12.2 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 8.70 us | 8.71 us | 44.32 KB | 44.47 KB | 2.86 | yes |
| E3/100 | compile | DotnetCel | 6.94 us | 6.95 us | 30.26 KB | 30.41 KB | 2.28 | yes |
| E3/100 | compile | Cel.NET | 17.92 us | 17.94 us | 53.14 KB | 53.29 KB | 5.90 | yes |
| E3/100 | compile | TelusCel | 5.53 us | 5.54 us | 13.38 KB | 13.52 KB | 1.82 | yes |
| E3/100 | compile | GreatPartsCel | 3.06 us | 3.07 us | 10.81 KB | 10.96 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 3.04 us | 3.05 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 2.73 us | - | 6.95 KB | - | 12.65 | yes |
| E3/100 | evaluate | DotnetCel | 2.36 us | - | 7.12 KB | - | 10.94 | yes |
| E3/100 | evaluate | Cel.NET | 1.42 us | - | 8.81 KB | - | 6.57 | yes |
| E3/100 | evaluate | TelusCel | 266.01 us | - | 570.20 KB | - | 1232.41 | yes |
| E3/100 | evaluate | GreatPartsCel | 267.9 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 215.8 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 327.8 ns | - | 160 B | - | 1.00 |  |
| E3/100 | full cycle | Celly | 11.50 us | 11.51 us | 51.28 KB | 51.43 KB | 3.42 | yes |
| E3/100 | full cycle | DotnetCel | 11.46 us | 11.47 us | 40.69 KB | 40.84 KB | 3.41 | yes |
| E3/100 | full cycle | Cel.NET | 19.44 us | 19.45 us | 61.93 KB | 62.08 KB | 5.78 | yes |
| E3/100 | full cycle | TelusCel | 313.04 us | 313.05 us | 583.57 KB | 583.72 KB | 93.15 | yes |
| E3/100 | full cycle | GreatPartsCel | 3.47 us | 3.48 us | 11.17 KB | 11.32 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 3.36 us | 3.37 us | 10.69 KB | 10.84 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 3.85 us | 3.86 us | 10.74 KB | 10.89 KB | 1.00 |  |
| E4/10 | text creation | - | - | 12.9 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 16.82 us | 16.83 us | 75.71 KB | 75.88 KB | 2.79 | yes |
| E4/10 | compile | DotnetCel | 10.61 us | 10.62 us | 37.80 KB | 37.98 KB | 1.76 | yes |
| E4/10 | compile | Cel.NET | 51.16 us | 51.17 us | 134.65 KB | 134.83 KB | 8.50 | yes |
| E4/10 | compile | TelusCel | 9.29 us | 9.30 us | 21.26 KB | 21.44 KB | 1.54 | yes |
| E4/10 | compile | GreatPartsCel | 6.02 us | 6.03 us | 18.87 KB | 19.05 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 6.27 us | 6.28 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 2.18 us | - | 4.08 KB | - | 2.70 | yes |
| E4/10 | evaluate | DotnetCel | 2.95 us | - | 5.31 KB | - | 3.66 | yes |
| E4/10 | evaluate | Cel.NET | 2.79 us | - | 7.08 KB | - | 3.46 | yes |
| E4/10 | evaluate | TelusCel | 53.32 us | - | 98.86 KB | - | 66.02 | yes |
| E4/10 | evaluate | GreatPartsCel | 842.2 ns | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 807.7 ns | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 1.01 us | - | 664 B | - | 1.00 |  |
| E4/10 | full cycle | Celly | 19.63 us | 19.64 us | 79.81 KB | 79.99 KB | 2.86 | yes |
| E4/10 | full cycle | DotnetCel | 15.77 us | 15.79 us | 46.42 KB | 46.60 KB | 2.30 | yes |
| E4/10 | full cycle | Cel.NET | 51.08 us | 51.09 us | 141.75 KB | 141.93 KB | 7.44 | yes |
| E4/10 | full cycle | TelusCel | 54.50 us | 54.51 us | 120.12 KB | 120.30 KB | 7.94 | yes |
| E4/10 | full cycle | GreatPartsCel | 6.86 us | 6.88 us | 19.72 KB | 19.90 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 7.55 us | 7.57 us | 19.31 KB | 19.49 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 7.99 us | 8.00 us | 19.37 KB | 19.55 KB | 1.00 |  |
| E4/100 | text creation | - | - | 13.7 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 17.68 us | 17.69 us | 75.71 KB | 75.89 KB | 2.98 | yes |
| E4/100 | compile | DotnetCel | 10.04 us | 10.05 us | 37.80 KB | 37.98 KB | 1.69 | yes |
| E4/100 | compile | Cel.NET | 50.00 us | 50.02 us | 134.69 KB | 134.87 KB | 8.43 | yes |
| E4/100 | compile | TelusCel | 9.30 us | 9.31 us | 21.26 KB | 21.44 KB | 1.57 | yes |
| E4/100 | compile | GreatPartsCel | 5.93 us | 5.94 us | 18.86 KB | 19.04 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 6.53 us | 6.55 us | 18.45 KB | 18.63 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 23.05 us | - | 42.44 KB | - | 2.41 | yes |
| E4/100 | evaluate | DotnetCel | 33.05 us | - | 70.80 KB | - | 3.45 | yes |
| E4/100 | evaluate | Cel.NET | 31.89 us | - | 96.53 KB | - | 3.33 | yes |
| E4/100 | evaluate | TelusCel | 554.82 us | - | 1.15 MB | - | 57.97 | yes |
| E4/100 | evaluate | GreatPartsCel | 9.57 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 9.81 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 10.00 us | - | 9.40 KB | - | 1.00 |  |
| E4/100 | full cycle | Celly | 42.28 us | 42.29 us | 118.18 KB | 118.36 KB | 2.73 | yes |
| E4/100 | full cycle | DotnetCel | 45.50 us | 45.51 us | 111.91 KB | 112.09 KB | 2.93 | yes |
| E4/100 | full cycle | Cel.NET | 81.63 us | 81.64 us | 231.19 KB | 231.37 KB | 5.26 | yes |
| E4/100 | full cycle | TelusCel | 563.38 us | 563.40 us | 1.17 MB | 1.17 MB | 36.31 | yes |
| E4/100 | full cycle | GreatPartsCel | 15.87 us | 15.89 us | 28.47 KB | 28.65 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 15.52 us | 15.53 us | 28.05 KB | 28.23 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 17.91 us | 17.92 us | 28.11 KB | 28.29 KB | 1.00 |  |
| E5 | text creation | - | - | 13.1 ns | - | 168 B | - | |
| E5 | compile | Celly | 6.15 us | 6.16 us | 35.54 KB | 35.71 KB | 3.90 | yes |
| E5 | compile | DotnetCel | 5.11 us | 5.12 us | 24.02 KB | 24.19 KB | 3.24 | yes |
| E5 | compile | Cel.NET | 8.98 us | 8.99 us | 30.58 KB | 30.75 KB | 5.69 | yes |
| E5 | compile | TelusCel | 3.44 us | 3.45 us | 8.64 KB | 8.80 KB | 2.18 | yes |
| E5 | compile | GreatPartsCel | 1.70 us | 1.72 us | 6.44 KB | 6.60 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 1.58 us | 1.59 us | 5.50 KB | 5.67 KB | 1.00 |  |
| E5 | evaluate | Celly | 247.9 ns | - | 752 B | - | 1.49 | yes |
| E5 | evaluate | DotnetCel | 281.4 ns | - | 448 B | - | 1.69 | yes |
| E5 | evaluate | Cel.NET | 43.66 us | - | 252.50 KB | - | 262.36 | yes |
| E5 | evaluate | TelusCel | 5.94 us | - | 14.34 KB | - | 35.69 | yes |
| E5 | evaluate | GreatPartsCel | 221.7 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 166.4 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 257.7 ns | - | 160 B | - | 1.04 |  |
| E5 | full cycle | Celly | 6.23 us | 6.24 us | 36.26 KB | 36.43 KB | 3.49 | yes |
| E5 | full cycle | DotnetCel | 6.92 us | 6.93 us | 27.60 KB | 27.77 KB | 3.87 | yes |
| E5 | full cycle | Cel.NET | 54.44 us | 54.45 us | 283.09 KB | 283.26 KB | 30.46 | yes |
| E5 | full cycle | TelusCel | 9.65 us | 9.66 us | 22.98 KB | 23.15 KB | 5.40 | yes |
| E5 | full cycle | GreatPartsCel | 2.03 us | 2.04 us | 6.75 KB | 6.92 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 1.79 us | 1.80 us | 5.82 KB | 5.98 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 2.37 us | 2.38 us | 5.92 KB | 6.08 KB | 1.00 |  |
| E6 | text creation | - | - | 15.2 ns | - | 184 B | - | |
| E6 | compile | Celly | 7.73 us | 7.74 us | 43.00 KB | 43.18 KB | 3.69 | yes |
| E6 | compile | Cel.NET | 18.51 us | 18.53 us | 50.48 KB | 50.66 KB | 8.83 | yes |
| E6 | compile | TelusCel | 6.12 us | 6.13 us | 13.97 KB | 14.15 KB | 2.92 | yes |
| E6 | compile | GreatPartsCel | 2.63 us | 2.65 us | 9.21 KB | 9.39 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 2.10 us | 2.11 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 312.6 ns | - | 912 B | - | 1.92 | yes |
| E6 | evaluate | Cel.NET | 297.8 ns | - | 1.57 KB | - | 1.83 | yes |
| E6 | evaluate | TelusCel | 8.15 us | - | 18.30 KB | - | 50.04 | yes |
| E6 | evaluate | GreatPartsCel | 204.6 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 162.8 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 258.4 ns | - | 160 B | - | 1.00 |  |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 8.06 us | 8.08 us | 43.85 KB | 44.03 KB | 3.32 | yes |
| E6 | full cycle | Cel.NET | 19.34 us | 19.35 us | 52.03 KB | 52.21 KB | 7.97 | yes |
| E6 | full cycle | TelusCel | 14.87 us | 14.88 us | 32.27 KB | 32.45 KB | 6.12 | yes |
| E6 | full cycle | GreatPartsCel | 2.97 us | 2.98 us | 9.52 KB | 9.70 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 2.43 us | 2.44 us | 6.78 KB | 6.96 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 2.96 us | 2.98 us | 6.93 KB | 7.11 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 12.6 ns | - | 128 B | - | |
| E7 | compile | Celly | 8.09 us | 8.10 us | 45.17 KB | 45.30 KB | 3.51 | yes |
| E7 | compile | DotnetCel | 5.93 us | 5.94 us | 25.66 KB | 25.78 KB | 2.57 | yes |
| E7 | compile | Cel.NET | 18.48 us | 18.49 us | 53.69 KB | 53.82 KB | 8.02 | yes |
| E7 | compile | GreatPartsCel | 2.30 us | 2.32 us | 7.52 KB | 7.65 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 2.33 us | 2.34 us | 7.08 KB | 7.21 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 525.2 ns | - | 1.17 KB | - | 3.42 | yes |
| E7 | evaluate | DotnetCel | 242.8 ns | - | 592 B | - | 1.58 | yes |
| E7 | evaluate | Cel.NET | 415.6 ns | - | 1.61 KB | - | 2.71 | yes |
| E7 | evaluate | GreatPartsCel | 207.8 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 153.6 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 247.9 ns | - | 160 B | - | 1.02 |  |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 8.94 us | 8.95 us | 46.31 KB | 46.43 KB | 3.37 | yes |
| E7 | full cycle | DotnetCel | 7.95 us | 7.96 us | 28.74 KB | 28.87 KB | 2.99 | yes |
| E7 | full cycle | Cel.NET | 18.89 us | 18.90 us | 55.29 KB | 55.42 KB | 7.12 | yes |
| E7 | full cycle | GreatPartsCel | 2.65 us | 2.67 us | 7.83 KB | 7.96 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 2.67 us | 2.68 us | 7.40 KB | 7.53 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 3.07 us | 3.08 us | 7.38 KB | 7.50 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 13.7 ns | - | 192 B | - | |
| E8 | compile | Celly | 10.41 us | 10.42 us | 52.50 KB | 52.69 KB | 2.95 | yes |
| E8 | compile | Cel.NET | 37.60 us | 37.62 us | 99.23 KB | 99.42 KB | 10.65 | yes |
| E8 | compile | TelusCel | 8.91 us | 8.93 us | 19.39 KB | 19.58 KB | 2.52 | yes |
| E8 | compile | GreatPartsCel | 3.53 us | 3.55 us | 12.52 KB | 12.71 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 3.61 us | 3.62 us | 10.87 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 542.5 ns | - | 1.66 KB | - | 3.11 | yes |
| E8 | evaluate | Cel.NET | 347.5 ns | - | 1.64 KB | - | 1.99 | yes |
| E8 | evaluate | TelusCel | 7.45 us | - | 16.02 KB | - | 42.68 | yes |
| E8 | evaluate | GreatPartsCel | 230.2 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 174.6 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 263.1 ns | - | 160 B | - | 1.00 |  |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 11.58 us | 11.60 us | 54.18 KB | 54.37 KB | 3.04 | yes |
| E8 | full cycle | Cel.NET | 37.97 us | 37.98 us | 100.88 KB | 101.07 KB | 9.95 | yes |
| E8 | full cycle | TelusCel | 16.25 us | 16.26 us | 35.41 KB | 35.60 KB | 4.26 | yes |
| E8 | full cycle | GreatPartsCel | 4.11 us | 4.12 us | 12.88 KB | 13.06 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 3.81 us | 3.83 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 4.26 us | 4.28 us | 11.30 KB | 11.49 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 11.8 ns | - | 136 B | - | |
| E9 | compile | Celly | 10.89 us | 10.90 us | 52.28 KB | 52.41 KB | 3.99 | yes |
| E9 | compile | DotnetCel | 6.23 us | 6.25 us | 26.69 KB | 26.82 KB | 2.28 | yes |
| E9 | compile | Cel.NET | 25.75 us | 25.76 us | 77.44 KB | 77.58 KB | 9.43 | yes |
| E9 | compile | TelusCel | 4.30 us | 4.31 us | 10.08 KB | 10.21 KB | 1.57 | yes |
| E9 | compile | GreatPartsCel | 2.93 us | 2.94 us | 9.99 KB | 10.12 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 2.73 us | 2.74 us | 8.42 KB | 8.55 KB | 1.00 |  |
| E9 | evaluate | Celly | 262.8 ns | - | 1.11 KB | - | 1.34 | yes |
| E9 | evaluate | DotnetCel | 290.2 ns | - | 808 B | - | 1.48 | yes |
| E9 | evaluate | Cel.NET | 305.7 ns | - | 1.75 KB | - | 1.55 | yes |
| E9 | evaluate | TelusCel | 4.64 us | - | 10.77 KB | - | 23.60 | yes |
| E9 | evaluate | GreatPartsCel | 244.7 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 196.6 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 312.4 ns | - | 344 B | - | 1.19 | yes |
| E9 | full cycle | Celly | 11.22 us | 11.23 us | 53.51 KB | 53.64 KB | 3.72 | yes |
| E9 | full cycle | DotnetCel | 8.40 us | 8.41 us | 29.99 KB | 30.12 KB | 2.78 | yes |
| E9 | full cycle | Cel.NET | 25.22 us | 25.24 us | 79.18 KB | 79.31 KB | 8.35 | yes |
| E9 | full cycle | TelusCel | 8.95 us | 8.96 us | 20.85 KB | 20.98 KB | 2.96 | yes |
| E9 | full cycle | GreatPartsCel | 3.25 us | 3.26 us | 10.48 KB | 10.62 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 3.02 us | 3.03 us | 8.91 KB | 9.04 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 3.47 us | 3.48 us | 9.04 KB | 9.17 KB | 1.00 |  |
