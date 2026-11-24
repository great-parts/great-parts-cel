## Run 3: scenario x operation

- Run 3: 2026-11-24T12:47:33.161Z - 2026-11-24T14:02:46.641Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 27.8 ns | - | 160 B | - | |
| E1 | compile | Celly | 15.72 us | 15.75 us | 42.82 KB | 42.98 KB | 4.26 | yes |
| E1 | compile | DotnetCel | 11.38 us | 11.41 us | 26.67 KB | 26.83 KB | 3.08 | yes |
| E1 | compile | Cel.NET | 35.01 us | 35.03 us | 49.59 KB | 49.74 KB | 9.48 | yes |
| E1 | compile | TelusCel | 7.37 us | 7.40 us | 10.34 KB | 10.50 KB | 1.99 | yes |
| E1 | compile | GreatPartsCel | 4.54 us | 4.57 us | 8.56 KB | 8.72 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.69 us | 3.72 us | 6.97 KB | 7.12 KB | 1.00 |  |
| E1 | evaluate | Celly | 454.7 ns | - | 832 B | - | 1.61 | yes |
| E1 | evaluate | DotnetCel | 458.5 ns | - | 560 B | - | 1.62 | yes |
| E1 | evaluate | Cel.NET | 513.7 ns | - | 1.51 KB | - | 1.82 | yes |
| E1 | evaluate | TelusCel | 7.77 us | - | 11.28 KB | - | 27.48 | yes |
| E1 | evaluate | GreatPartsCel | 383.1 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 282.9 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 2.26 us | - | 7.27 KB | - | 4.98 | yes |
| E1 | full cycle | Celly | 16.40 us | 16.43 us | 43.66 KB | 43.81 KB | 3.99 | yes |
| E1 | full cycle | DotnetCel | 15.26 us | 15.29 us | 29.79 KB | 29.95 KB | 3.71 | yes |
| E1 | full cycle | Cel.NET | 36.02 us | 36.05 us | 51.44 KB | 51.59 KB | 8.77 | yes |
| E1 | full cycle | TelusCel | 16.40 us | 16.42 us | 21.63 KB | 21.78 KB | 3.99 | yes |
| E1 | full cycle | GreatPartsCel | 5.14 us | 5.17 us | 8.87 KB | 9.03 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 4.11 us | 4.14 us | 7.28 KB | 7.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 6.54 us | 6.57 us | 14.51 KB | 14.66 KB | 1.00 |  |
| E2/10 | text creation | - | - | 26.9 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 21.92 us | 21.95 us | 54.57 KB | 54.72 KB | 3.16 | yes |
| E2/10 | compile | DotnetCel | 15.59 us | 15.62 us | 32.88 KB | 33.02 KB | 2.25 | yes |
| E2/10 | compile | Cel.NET | 45.77 us | 45.80 us | 71.99 KB | 72.14 KB | 6.59 | yes |
| E2/10 | compile | TelusCel | 11.67 us | 11.70 us | 15.52 KB | 15.66 KB | 1.68 | yes |
| E2/10 | compile | GreatPartsCel | 7.36 us | 7.39 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 6.94 us | 6.97 us | 11.57 KB | 11.71 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 3.80 us | - | 2.96 KB | - | 2.06 | yes |
| E2/10 | evaluate | DotnetCel | 5.69 us | - | 4.07 KB | - | 3.09 | yes |
| E2/10 | evaluate | Cel.NET | 4.75 us | - | 6.47 KB | - | 2.57 | yes |
| E2/10 | evaluate | TelusCel | 87.25 us | - | 112.86 KB | - | 47.28 | yes |
| E2/10 | evaluate | GreatPartsCel | 2.21 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.85 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 5.13 us | - | 7.27 KB | - | 1.35 | yes |
| E2/10 | full cycle | Celly | 26.54 us | 26.56 us | 57.54 KB | 57.69 KB | 2.83 | yes |
| E2/10 | full cycle | DotnetCel | 26.81 us | 26.83 us | 40.35 KB | 40.50 KB | 2.86 | yes |
| E2/10 | full cycle | Cel.NET | 53.27 us | 53.30 us | 78.43 KB | 78.58 KB | 5.69 | yes |
| E2/10 | full cycle | TelusCel | 99.41 us | 99.43 us | 128.38 KB | 128.52 KB | 10.62 | yes |
| E2/10 | full cycle | GreatPartsCel | 9.76 us | 9.78 us | 12.57 KB | 12.72 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 9.36 us | 9.39 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 13.39 us | 13.42 us | 19.24 KB | 19.39 KB | 1.00 |  |
| E2/100 | text creation | - | - | 26.3 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 21.98 us | 22.01 us | 54.51 KB | 54.66 KB | 3.22 | yes |
| E2/100 | compile | DotnetCel | 15.80 us | 15.83 us | 32.88 KB | 33.02 KB | 2.31 | yes |
| E2/100 | compile | Cel.NET | 46.17 us | 46.20 us | 71.98 KB | 72.13 KB | 6.76 | yes |
| E2/100 | compile | TelusCel | 11.67 us | 11.69 us | 15.52 KB | 15.66 KB | 1.71 | yes |
| E2/100 | compile | GreatPartsCel | 7.22 us | 7.24 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 6.83 us | 6.86 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 32.59 us | - | 21.66 KB | - | 2.07 | yes |
| E2/100 | evaluate | DotnetCel | 50.90 us | - | 31.91 KB | - | 3.23 | yes |
| E2/100 | evaluate | Cel.NET | 42.56 us | - | 57.41 KB | - | 2.70 | yes |
| E2/100 | evaluate | TelusCel | 836.63 us | - | 1.05 MB | - | 53.04 | yes |
| E2/100 | evaluate | GreatPartsCel | 16.20 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 15.77 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 39.86 us | - | 52.98 KB | - | 1.22 | yes |
| E2/100 | full cycle | Celly | 55.51 us | 55.53 us | 76.24 KB | 76.39 KB | 2.31 | yes |
| E2/100 | full cycle | DotnetCel | 72.62 us | 72.64 us | 68.20 KB | 68.34 KB | 3.03 | yes |
| E2/100 | full cycle | Cel.NET | 92.78 us | 92.80 us | 129.40 KB | 129.54 KB | 3.87 | yes |
| E2/100 | full cycle | TelusCel | 861.64 us | 861.67 us | 1.06 MB | 1.06 MB | 35.91 | yes |
| E2/100 | full cycle | GreatPartsCel | 24.93 us | 24.95 us | 12.58 KB | 12.72 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 24.00 us | 24.02 us | 11.92 KB | 12.07 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 48.46 us | 48.48 us | 64.94 KB | 65.09 KB | 1.00 |  |
| E3/10 | text creation | - | - | 27.2 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 18.25 us | 18.27 us | 44.35 KB | 44.50 KB | 3.31 | yes |
| E3/10 | compile | DotnetCel | 14.26 us | 14.29 us | 30.62 KB | 30.77 KB | 2.59 | yes |
| E3/10 | compile | Cel.NET | 35.79 us | 35.82 us | 53.07 KB | 53.22 KB | 6.49 | yes |
| E3/10 | compile | TelusCel | 9.51 us | 9.54 us | 13.39 KB | 13.54 KB | 1.73 | yes |
| E3/10 | compile | GreatPartsCel | 6.06 us | 6.09 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 5.51 us | 5.54 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 1.05 us | - | 1.57 KB | - | 2.54 | yes |
| E3/10 | evaluate | DotnetCel | 1.30 us | - | 1.78 KB | - | 3.13 | yes |
| E3/10 | evaluate | Cel.NET | 1.06 us | - | 2.12 KB | - | 2.56 | yes |
| E3/10 | evaluate | TelusCel | 49.48 us | - | 63.65 KB | - | 119.38 | yes |
| E3/10 | evaluate | GreatPartsCel | 513.3 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 414.4 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 2.32 us | - | 7.27 KB | - | 2.20 | yes |
| E3/10 | full cycle | Celly | 20.22 us | 20.24 us | 45.85 KB | 46.00 KB | 3.36 | yes |
| E3/10 | full cycle | DotnetCel | 20.13 us | 20.16 us | 35.80 KB | 35.95 KB | 3.35 | yes |
| E3/10 | full cycle | Cel.NET | 38.20 us | 38.23 us | 55.27 KB | 55.42 KB | 6.35 | yes |
| E3/10 | full cycle | TelusCel | 59.80 us | 59.82 us | 77.04 KB | 77.19 KB | 9.94 | yes |
| E3/10 | full cycle | GreatPartsCel | 6.98 us | 7.01 us | 11.17 KB | 11.32 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 6.01 us | 6.04 us | 10.68 KB | 10.82 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 8.84 us | 8.87 us | 17.69 KB | 17.84 KB | 1.00 |  |
| E3/100 | text creation | - | - | 25.3 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 17.96 us | 17.98 us | 44.34 KB | 44.49 KB | 3.18 | yes |
| E3/100 | compile | DotnetCel | 13.68 us | 13.70 us | 30.62 KB | 30.77 KB | 2.42 | yes |
| E3/100 | compile | Cel.NET | 35.57 us | 35.60 us | 53.07 KB | 53.22 KB | 6.30 | yes |
| E3/100 | compile | TelusCel | 9.34 us | 9.36 us | 13.39 KB | 13.54 KB | 1.65 | yes |
| E3/100 | compile | GreatPartsCel | 6.15 us | 6.18 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 5.65 us | 5.67 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.60 us | - | 6.95 KB | - | 11.23 | yes |
| E3/100 | evaluate | DotnetCel | 4.42 us | - | 7.12 KB | - | 10.79 | yes |
| E3/100 | evaluate | Cel.NET | 2.56 us | - | 8.86 KB | - | 6.24 | yes |
| E3/100 | evaluate | TelusCel | 453.38 us | - | 578.05 KB | - | 1106.62 | yes |
| E3/100 | evaluate | GreatPartsCel | 490.7 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 409.7 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 11.76 us | - | 52.98 KB | - | 4.60 | yes |
| E3/100 | full cycle | Celly | 23.88 us | 23.91 us | 51.23 KB | 51.38 KB | 3.79 | yes |
| E3/100 | full cycle | DotnetCel | 23.05 us | 23.07 us | 41.14 KB | 41.29 KB | 3.66 | yes |
| E3/100 | full cycle | Cel.NET | 40.24 us | 40.26 us | 61.93 KB | 62.08 KB | 6.39 | yes |
| E3/100 | full cycle | TelusCel | 450.87 us | 450.90 us | 591.44 KB | 591.59 KB | 71.63 | yes |
| E3/100 | full cycle | GreatPartsCel | 6.92 us | 6.95 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 6.29 us | 6.32 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 17.83 us | 17.86 us | 63.40 KB | 63.54 KB | 1.00 |  |
| E4/10 | text creation | - | - | 26.9 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 33.99 us | 34.02 us | 76.10 KB | 76.28 KB | 2.92 | yes |
| E4/10 | compile | DotnetCel | 20.05 us | 20.08 us | 38.16 KB | 38.34 KB | 1.72 | yes |
| E4/10 | compile | Cel.NET | 101.05 us | 101.07 us | 134.54 KB | 134.72 KB | 8.67 | yes |
| E4/10 | compile | TelusCel | 16.15 us | 16.17 us | 21.27 KB | 21.45 KB | 1.38 | yes |
| E4/10 | compile | GreatPartsCel | 12.35 us | 12.37 us | 18.72 KB | 18.90 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 11.66 us | 11.69 us | 18.32 KB | 18.50 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 3.89 us | - | 4.08 KB | - | 2.18 | yes |
| E4/10 | evaluate | DotnetCel | 5.63 us | - | 5.31 KB | - | 3.15 | yes |
| E4/10 | evaluate | Cel.NET | 5.25 us | - | 7.38 KB | - | 2.94 | yes |
| E4/10 | evaluate | TelusCel | 75.37 us | - | 99.98 KB | - | 42.24 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.91 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.78 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 4.68 us | - | 7.77 KB | - | 1.20 | yes |
| E4/10 | full cycle | Celly | 39.45 us | 39.47 us | 80.15 KB | 80.33 KB | 2.80 | yes |
| E4/10 | full cycle | DotnetCel | 31.35 us | 31.37 us | 46.87 KB | 47.05 KB | 2.22 | yes |
| E4/10 | full cycle | Cel.NET | 111.83 us | 111.85 us | 141.93 KB | 142.11 KB | 7.94 | yes |
| E4/10 | full cycle | TelusCel | 95.16 us | 95.18 us | 121.26 KB | 121.44 KB | 6.75 | yes |
| E4/10 | full cycle | GreatPartsCel | 15.05 us | 15.07 us | 19.57 KB | 19.75 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 14.09 us | 14.12 us | 19.17 KB | 19.35 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 17.89 us | 17.92 us | 25.29 KB | 25.47 KB | 1.00 |  |
| E4/100 | text creation | - | - | 26.8 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 33.76 us | 33.79 us | 76.10 KB | 76.28 KB | 2.84 | yes |
| E4/100 | compile | DotnetCel | 20.82 us | 20.84 us | 38.16 KB | 38.34 KB | 1.75 | yes |
| E4/100 | compile | Cel.NET | 102.29 us | 102.32 us | 134.54 KB | 134.72 KB | 8.61 | yes |
| E4/100 | compile | TelusCel | 16.60 us | 16.63 us | 21.27 KB | 21.45 KB | 1.40 | yes |
| E4/100 | compile | GreatPartsCel | 12.23 us | 12.26 us | 18.73 KB | 18.91 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 11.88 us | 11.90 us | 18.32 KB | 18.50 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 38.52 us | - | 42.44 KB | - | 2.18 | yes |
| E4/100 | evaluate | DotnetCel | 65.56 us | - | 70.80 KB | - | 3.71 | yes |
| E4/100 | evaluate | Cel.NET | 62.01 us | - | 100.41 KB | - | 3.51 | yes |
| E4/100 | evaluate | TelusCel | 924.56 us | - | 1.16 MB | - | 52.36 | yes |
| E4/100 | evaluate | GreatPartsCel | 19.07 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 17.66 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 40.55 us | - | 62.22 KB | - | 1.05 |  |
| E4/100 | full cycle | Celly | 73.67 us | 73.69 us | 118.50 KB | 118.68 KB | 2.41 | yes |
| E4/100 | full cycle | DotnetCel | 92.48 us | 92.51 us | 112.36 KB | 112.54 KB | 3.03 | yes |
| E4/100 | full cycle | Cel.NET | 172.91 us | 172.94 us | 234.95 KB | 235.13 KB | 5.67 | yes |
| E4/100 | full cycle | TelusCel | 941.31 us | 941.34 us | 1.18 MB | 1.18 MB | 30.84 | yes |
| E4/100 | full cycle | GreatPartsCel | 32.45 us | 32.48 us | 28.32 KB | 28.50 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 30.52 us | 30.55 us | 27.92 KB | 28.10 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 53.26 us | 53.29 us | 79.74 KB | 79.92 KB | 1.00 |  |
| E5 | text creation | - | - | 27.7 ns | - | 168 B | - | |
| E5 | compile | Celly | 13.00 us | 13.03 us | 35.52 KB | 35.68 KB | 4.73 | yes |
| E5 | compile | DotnetCel | 9.73 us | 9.76 us | 24.38 KB | 24.55 KB | 3.54 | yes |
| E5 | compile | Cel.NET | 20.60 us | 20.63 us | 30.65 KB | 30.81 KB | 7.50 | yes |
| E5 | compile | TelusCel | 5.67 us | 5.70 us | 8.66 KB | 8.82 KB | 2.07 | yes |
| E5 | compile | GreatPartsCel | 3.14 us | 3.17 us | 6.43 KB | 6.59 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.75 us | 2.77 us | 5.50 KB | 5.66 KB | 1.00 |  |
| E5 | evaluate | Celly | 529.4 ns | - | 752 B | - | 1.66 | yes |
| E5 | evaluate | DotnetCel | 567.4 ns | - | 448 B | - | 1.78 | yes |
| E5 | evaluate | Cel.NET | 75.49 us | - | 251.94 KB | - | 237.23 | yes |
| E5 | evaluate | TelusCel | 9.87 us | - | 14.41 KB | - | 31.00 | yes |
| E5 | evaluate | GreatPartsCel | 403.3 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 318.2 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 2.14 us | - | 7.27 KB | - | 4.05 | yes |
| E5 | full cycle | Celly | 13.90 us | 13.93 us | 36.14 KB | 36.31 KB | 4.44 | yes |
| E5 | full cycle | DotnetCel | 14.48 us | 14.50 us | 28.04 KB | 28.20 KB | 4.63 | yes |
| E5 | full cycle | Cel.NET | 114.16 us | 114.19 us | 282.58 KB | 282.75 KB | 36.49 | yes |
| E5 | full cycle | TelusCel | 17.71 us | 17.74 us | 23.06 KB | 23.23 KB | 5.66 | yes |
| E5 | full cycle | GreatPartsCel | 3.76 us | 3.79 us | 6.74 KB | 6.91 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 3.13 us | 3.16 us | 5.81 KB | 5.98 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 5.55 us | 5.57 us | 12.87 KB | 13.04 KB | 1.00 |  |
| E6 | text creation | - | - | 28.3 ns | - | 184 B | - | |
| E6 | compile | Celly | 16.92 us | 16.95 us | 43.04 KB | 43.22 KB | 4.30 | yes |
| E6 | compile | Cel.NET | 37.82 us | 37.85 us | 50.61 KB | 50.79 KB | 9.61 | yes |
| E6 | compile | TelusCel | 10.13 us | 10.16 us | 13.98 KB | 14.16 KB | 2.57 | yes |
| E6 | compile | GreatPartsCel | 5.22 us | 5.24 us | 9.18 KB | 9.36 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.94 us | 3.97 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 521.0 ns | - | 912 B | - | 1.77 | yes |
| E6 | evaluate | Cel.NET | 541.5 ns | - | 1.55 KB | - | 1.84 | yes |
| E6 | evaluate | TelusCel | 13.11 us | - | 18.44 KB | - | 44.59 | yes |
| E6 | evaluate | GreatPartsCel | 375.8 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 294.0 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 2.14 us | - | 7.27 KB | - | 4.10 | yes |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 18.11 us | 18.14 us | 43.93 KB | 44.11 KB | 4.15 | yes |
| E6 | full cycle | Cel.NET | 39.19 us | 39.22 us | 51.94 KB | 52.12 KB | 8.99 | yes |
| E6 | full cycle | TelusCel | 24.82 us | 24.85 us | 32.42 KB | 32.60 KB | 5.69 | yes |
| E6 | full cycle | GreatPartsCel | 5.82 us | 5.85 us | 9.49 KB | 9.67 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 4.36 us | 4.39 us | 6.77 KB | 6.95 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 6.63 us | 6.66 us | 14.01 KB | 14.19 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 24.1 ns | - | 128 B | - | |
| E7 | compile | Celly | 16.86 us | 16.88 us | 45.37 KB | 45.49 KB | 4.29 | yes |
| E7 | compile | DotnetCel | 10.77 us | 10.80 us | 26.02 KB | 26.14 KB | 2.74 | yes |
| E7 | compile | Cel.NET | 36.79 us | 36.82 us | 52.71 KB | 52.84 KB | 9.37 | yes |
| E7 | compile | GreatPartsCel | 4.43 us | 4.45 us | 7.50 KB | 7.63 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 3.93 us | 3.95 us | 7.07 KB | 7.20 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 708.7 ns | - | 1.18 KB | - | 2.52 | yes |
| E7 | evaluate | DotnetCel | 461.0 ns | - | 592 B | - | 1.64 | yes |
| E7 | evaluate | Cel.NET | 746.9 ns | - | 1.59 KB | - | 2.66 | yes |
| E7 | evaluate | GreatPartsCel | 379.3 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 281.0 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 2.09 us | - | 7.27 KB | - | 4.53 | yes |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 18.51 us | 18.54 us | 46.55 KB | 46.67 KB | 4.32 | yes |
| E7 | full cycle | DotnetCel | 14.53 us | 14.55 us | 29.17 KB | 29.29 KB | 3.39 | yes |
| E7 | full cycle | Cel.NET | 39.13 us | 39.15 us | 54.32 KB | 54.44 KB | 9.13 | yes |
| E7 | full cycle | GreatPartsCel | 5.01 us | 5.04 us | 7.82 KB | 7.94 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 4.28 us | 4.31 us | 7.39 KB | 7.51 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 6.91 us | 6.93 us | 14.48 KB | 14.60 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 29.5 ns | - | 192 B | - | |
| E8 | compile | Celly | 21.72 us | 21.75 us | 52.63 KB | 52.82 KB | 3.64 | yes |
| E8 | compile | Cel.NET | 72.92 us | 72.95 us | 99.35 KB | 99.54 KB | 12.21 | yes |
| E8 | compile | TelusCel | 14.50 us | 14.53 us | 19.41 KB | 19.59 KB | 2.43 | yes |
| E8 | compile | GreatPartsCel | 6.99 us | 7.02 us | 12.51 KB | 12.70 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 5.97 us | 6.00 us | 10.88 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 1.08 us | - | 1.66 KB | - | 3.21 | yes |
| E8 | evaluate | Cel.NET | 621.3 ns | - | 1.66 KB | - | 1.84 | yes |
| E8 | evaluate | TelusCel | 11.89 us | - | 16.25 KB | - | 35.17 | yes |
| E8 | evaluate | GreatPartsCel | 460.5 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 338.2 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 2.18 us | - | 7.27 KB | - | 3.51 | yes |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 24.06 us | 24.09 us | 54.15 KB | 54.34 KB | 3.70 | yes |
| E8 | full cycle | Cel.NET | 75.81 us | 75.84 us | 100.98 KB | 101.17 KB | 11.66 | yes |
| E8 | full cycle | TelusCel | 28.14 us | 28.17 us | 35.66 KB | 35.84 KB | 4.33 | yes |
| E8 | full cycle | GreatPartsCel | 7.62 us | 7.65 us | 12.86 KB | 13.05 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 6.50 us | 6.53 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 9.03 us | 9.06 us | 18.41 KB | 18.60 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 24.8 ns | - | 136 B | - | |
| E9 | compile | Celly | 18.96 us | 18.98 us | 52.63 KB | 52.77 KB | 4.35 | yes |
| E9 | compile | DotnetCel | 11.34 us | 11.37 us | 27.05 KB | 27.18 KB | 2.60 | yes |
| E9 | compile | Cel.NET | 49.56 us | 49.58 us | 76.47 KB | 76.61 KB | 11.38 | yes |
| E9 | compile | TelusCel | 7.43 us | 7.46 us | 10.09 KB | 10.23 KB | 1.71 | yes |
| E9 | compile | GreatPartsCel | 5.36 us | 5.38 us | 9.98 KB | 10.11 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 4.36 us | 4.38 us | 8.42 KB | 8.55 KB | 1.00 |  |
| E9 | evaluate | Celly | 494.5 ns | - | 1.11 KB | - | 1.43 | yes |
| E9 | evaluate | DotnetCel | 501.5 ns | - | 808 B | - | 1.46 | yes |
| E9 | evaluate | Cel.NET | 559.4 ns | - | 1.73 KB | - | 1.62 | yes |
| E9 | evaluate | TelusCel | 7.61 us | - | 10.87 KB | - | 22.08 | yes |
| E9 | evaluate | GreatPartsCel | 431.1 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 344.6 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 2.13 us | - | 7.45 KB | - | 4.30 | yes |
| E9 | full cycle | Celly | 20.49 us | 20.52 us | 53.74 KB | 53.87 KB | 4.20 | yes |
| E9 | full cycle | DotnetCel | 14.82 us | 14.84 us | 30.41 KB | 30.54 KB | 3.03 | yes |
| E9 | full cycle | Cel.NET | 50.71 us | 50.74 us | 78.09 KB | 78.22 KB | 10.38 | yes |
| E9 | full cycle | TelusCel | 16.41 us | 16.44 us | 20.96 KB | 21.09 KB | 3.36 | yes |
| E9 | full cycle | GreatPartsCel | 6.07 us | 6.09 us | 10.47 KB | 10.60 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.88 us | 4.91 us | 8.91 KB | 9.04 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 7.23 us | 7.26 us | 16.14 KB | 16.27 KB | 1.00 |  |
