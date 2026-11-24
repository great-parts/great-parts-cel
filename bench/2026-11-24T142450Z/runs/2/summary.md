## Run 2: scenario x operation

- Run 2: 2026-11-24T11:31:45.783Z - 2026-11-24T12:47:33.161Z; job: Default; cases: all; filter: *

Median time and allocated memory per operation. The decisive column is "minus text": for compile and full cycle the median of the text creation (the row "text creation") is subtracted, evaluation creates no text. The ratio is to the fastest library with a correct result in this run by the decisive column (for a variant of ${SUBJECT}: to the fastest other library or to itself); "> 1.1" marks a ratio above the threshold in this run. The summary over all runs is in `results/summary.md`.

| Scenario | Operation | Library | Time minus text | Time | Memory minus text | Memory | Ratio | > 1.1 |
|---|---|---|--:|--:|--:|--:|--:|---|
| E1 | text creation | - | - | 26.3 ns | - | 160 B | - | |
| E1 | compile | Celly | 15.78 us | 15.81 us | 42.83 KB | 42.98 KB | 4.24 | yes |
| E1 | compile | DotnetCel | 11.11 us | 11.13 us | 26.67 KB | 26.83 KB | 2.98 | yes |
| E1 | compile | Cel.NET | 34.99 us | 35.02 us | 49.59 KB | 49.75 KB | 9.40 | yes |
| E1 | compile | TelusCel | 7.56 us | 7.58 us | 10.34 KB | 10.50 KB | 2.03 | yes |
| E1 | compile | GreatPartsCel | 4.57 us | 4.60 us | 8.56 KB | 8.72 KB | 1.00 |  |
| E1 | compile | GPCel-Input | 3.72 us | 3.75 us | 6.97 KB | 7.12 KB | 1.00 |  |
| E1 | evaluate | Celly | 482.2 ns | - | 832 B | - | 1.63 | yes |
| E1 | evaluate | DotnetCel | 454.9 ns | - | 560 B | - | 1.54 | yes |
| E1 | evaluate | Cel.NET | 513.7 ns | - | 1.51 KB | - | 1.74 | yes |
| E1 | evaluate | TelusCel | 8.09 us | - | 11.28 KB | - | 27.37 | yes |
| E1 | evaluate | GreatPartsCel | 395.2 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Input | 295.8 ns | - | 288 B | - | 1.00 |  |
| E1 | evaluate | GPCel-Simple | 2.19 us | - | 7.27 KB | - | 4.81 | yes |
| E1 | full cycle | Celly | 16.81 us | 16.84 us | 43.63 KB | 43.79 KB | 4.10 | yes |
| E1 | full cycle | DotnetCel | 14.85 us | 14.88 us | 29.79 KB | 29.95 KB | 3.62 | yes |
| E1 | full cycle | Cel.NET | 35.66 us | 35.68 us | 51.42 KB | 51.58 KB | 8.69 | yes |
| E1 | full cycle | TelusCel | 16.67 us | 16.70 us | 21.63 KB | 21.78 KB | 4.06 | yes |
| E1 | full cycle | GreatPartsCel | 5.13 us | 5.15 us | 8.87 KB | 9.03 KB | 1.00 |  |
| E1 | full cycle | GPCel-Input | 4.10 us | 4.13 us | 7.28 KB | 7.44 KB | 1.00 |  |
| E1 | full cycle | GPCel-Simple | 6.64 us | 6.67 us | 14.50 KB | 14.66 KB | 1.00 |  |
| E2/10 | text creation | - | - | 26.2 ns | - | 152 B | - | |
| E2/10 | compile | Celly | 21.69 us | 21.72 us | 54.57 KB | 54.72 KB | 3.10 | yes |
| E2/10 | compile | DotnetCel | 15.15 us | 15.18 us | 32.88 KB | 33.02 KB | 2.17 | yes |
| E2/10 | compile | Cel.NET | 45.96 us | 45.98 us | 71.98 KB | 72.13 KB | 6.57 | yes |
| E2/10 | compile | TelusCel | 11.71 us | 11.74 us | 15.52 KB | 15.66 KB | 1.67 | yes |
| E2/10 | compile | GreatPartsCel | 7.20 us | 7.23 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/10 | compile | GPCel-Input | 7.00 us | 7.03 us | 11.57 KB | 11.71 KB | 1.00 |  |
| E2/10 | evaluate | Celly | 3.88 us | - | 2.96 KB | - | 2.04 | yes |
| E2/10 | evaluate | DotnetCel | 5.70 us | - | 4.07 KB | - | 3.00 | yes |
| E2/10 | evaluate | Cel.NET | 4.78 us | - | 6.47 KB | - | 2.52 | yes |
| E2/10 | evaluate | TelusCel | 88.67 us | - | 112.86 KB | - | 46.73 | yes |
| E2/10 | evaluate | GreatPartsCel | 1.99 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Input | 1.90 us | - | 336 B | - | 1.00 |  |
| E2/10 | evaluate | GPCel-Simple | 5.07 us | - | 7.27 KB | - | 1.31 | yes |
| E2/10 | full cycle | Celly | 25.56 us | 25.58 us | 57.53 KB | 57.68 KB | 2.78 | yes |
| E2/10 | full cycle | DotnetCel | 26.38 us | 26.40 us | 40.35 KB | 40.50 KB | 2.87 | yes |
| E2/10 | full cycle | Cel.NET | 52.32 us | 52.34 us | 78.43 KB | 78.58 KB | 5.70 | yes |
| E2/10 | full cycle | TelusCel | 99.96 us | 99.99 us | 128.38 KB | 128.52 KB | 10.88 | yes |
| E2/10 | full cycle | GreatPartsCel | 9.64 us | 9.67 us | 12.57 KB | 12.72 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Input | 9.19 us | 9.21 us | 11.93 KB | 12.07 KB | 1.00 |  |
| E2/10 | full cycle | GPCel-Simple | 13.27 us | 13.29 us | 19.24 KB | 19.39 KB | 1.00 |  |
| E2/100 | text creation | - | - | 26.2 ns | - | 152 B | - | |
| E2/100 | compile | Celly | 22.01 us | 22.04 us | 54.57 KB | 54.72 KB | 3.14 | yes |
| E2/100 | compile | DotnetCel | 15.67 us | 15.69 us | 32.88 KB | 33.02 KB | 2.23 | yes |
| E2/100 | compile | Cel.NET | 44.96 us | 44.98 us | 71.98 KB | 72.13 KB | 6.41 | yes |
| E2/100 | compile | TelusCel | 11.56 us | 11.58 us | 15.52 KB | 15.66 KB | 1.65 | yes |
| E2/100 | compile | GreatPartsCel | 7.38 us | 7.40 us | 12.22 KB | 12.37 KB | 1.00 |  |
| E2/100 | compile | GPCel-Input | 7.01 us | 7.04 us | 11.57 KB | 11.72 KB | 1.00 |  |
| E2/100 | evaluate | Celly | 31.71 us | - | 21.66 KB | - | 1.88 | yes |
| E2/100 | evaluate | DotnetCel | 51.37 us | - | 31.91 KB | - | 3.05 | yes |
| E2/100 | evaluate | Cel.NET | 42.10 us | - | 57.41 KB | - | 2.50 | yes |
| E2/100 | evaluate | TelusCel | 822.53 us | - | 1.05 MB | - | 48.79 | yes |
| E2/100 | evaluate | GreatPartsCel | 16.93 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Input | 16.86 us | - | 336 B | - | 1.00 |  |
| E2/100 | evaluate | GPCel-Simple | 40.07 us | - | 52.98 KB | - | 1.26 | yes |
| E2/100 | full cycle | Celly | 58.13 us | 58.15 us | 76.17 KB | 76.31 KB | 2.37 | yes |
| E2/100 | full cycle | DotnetCel | 72.82 us | 72.84 us | 68.20 KB | 68.34 KB | 2.97 | yes |
| E2/100 | full cycle | Cel.NET | 92.23 us | 92.25 us | 129.40 KB | 129.54 KB | 3.77 | yes |
| E2/100 | full cycle | TelusCel | 890.03 us | 890.05 us | 1.06 MB | 1.06 MB | 36.35 | yes |
| E2/100 | full cycle | GreatPartsCel | 24.48 us | 24.51 us | 12.58 KB | 12.72 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Input | 24.77 us | 24.80 us | 11.92 KB | 12.07 KB | 1.00 |  |
| E2/100 | full cycle | GPCel-Simple | 48.59 us | 48.61 us | 64.94 KB | 65.09 KB | 1.00 |  |
| E3/10 | text creation | - | - | 26.2 ns | - | 152 B | - | |
| E3/10 | compile | Celly | 18.02 us | 18.04 us | 44.35 KB | 44.50 KB | 3.22 | yes |
| E3/10 | compile | DotnetCel | 13.72 us | 13.75 us | 30.62 KB | 30.77 KB | 2.45 | yes |
| E3/10 | compile | Cel.NET | 35.70 us | 35.73 us | 53.07 KB | 53.22 KB | 6.38 | yes |
| E3/10 | compile | TelusCel | 9.41 us | 9.44 us | 13.39 KB | 13.54 KB | 1.68 | yes |
| E3/10 | compile | GreatPartsCel | 5.94 us | 5.97 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/10 | compile | GPCel-Input | 5.60 us | 5.62 us | 10.32 KB | 10.46 KB | 1.00 |  |
| E3/10 | evaluate | Celly | 1.00 us | - | 1.61 KB | - | 2.41 | yes |
| E3/10 | evaluate | DotnetCel | 1.32 us | - | 1.78 KB | - | 3.18 | yes |
| E3/10 | evaluate | Cel.NET | 1.03 us | - | 2.12 KB | - | 2.48 | yes |
| E3/10 | evaluate | TelusCel | 49.09 us | - | 63.65 KB | - | 118.08 | yes |
| E3/10 | evaluate | GreatPartsCel | 512.0 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Input | 415.8 ns | - | 336 B | - | 1.00 |  |
| E3/10 | evaluate | GPCel-Simple | 2.27 us | - | 7.27 KB | - | 2.26 | yes |
| E3/10 | full cycle | Celly | 19.93 us | 19.96 us | 45.92 KB | 46.06 KB | 3.25 | yes |
| E3/10 | full cycle | DotnetCel | 19.80 us | 19.83 us | 35.80 KB | 35.95 KB | 3.23 | yes |
| E3/10 | full cycle | Cel.NET | 37.95 us | 37.97 us | 55.17 KB | 55.32 KB | 6.18 | yes |
| E3/10 | full cycle | TelusCel | 58.83 us | 58.85 us | 77.04 KB | 77.19 KB | 9.58 | yes |
| E3/10 | full cycle | GreatPartsCel | 6.65 us | 6.68 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Input | 6.14 us | 6.17 us | 10.68 KB | 10.83 KB | 1.00 |  |
| E3/10 | full cycle | GPCel-Simple | 8.91 us | 8.94 us | 17.70 KB | 17.84 KB | 1.00 |  |
| E3/100 | text creation | - | - | 25.6 ns | - | 152 B | - | |
| E3/100 | compile | Celly | 18.04 us | 18.07 us | 44.36 KB | 44.51 KB | 3.23 | yes |
| E3/100 | compile | DotnetCel | 14.09 us | 14.11 us | 30.62 KB | 30.77 KB | 2.52 | yes |
| E3/100 | compile | Cel.NET | 36.29 us | 36.31 us | 53.08 KB | 53.23 KB | 6.50 | yes |
| E3/100 | compile | TelusCel | 9.27 us | 9.30 us | 13.39 KB | 13.54 KB | 1.66 | yes |
| E3/100 | compile | GreatPartsCel | 6.02 us | 6.04 us | 10.80 KB | 10.95 KB | 1.00 |  |
| E3/100 | compile | GPCel-Input | 5.58 us | 5.61 us | 10.32 KB | 10.47 KB | 1.00 |  |
| E3/100 | evaluate | Celly | 4.52 us | - | 6.95 KB | - | 11.10 | yes |
| E3/100 | evaluate | DotnetCel | 4.19 us | - | 7.12 KB | - | 10.28 | yes |
| E3/100 | evaluate | Cel.NET | 2.48 us | - | 8.86 KB | - | 6.10 | yes |
| E3/100 | evaluate | TelusCel | 437.67 us | - | 578.05 KB | - | 1074.77 | yes |
| E3/100 | evaluate | GreatPartsCel | 496.1 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Input | 407.2 ns | - | 336 B | - | 1.00 |  |
| E3/100 | evaluate | GPCel-Simple | 11.35 us | - | 52.98 KB | - | 4.57 | yes |
| E3/100 | full cycle | Celly | 23.44 us | 23.46 us | 51.24 KB | 51.38 KB | 3.78 | yes |
| E3/100 | full cycle | DotnetCel | 23.24 us | 23.26 us | 41.14 KB | 41.29 KB | 3.75 | yes |
| E3/100 | full cycle | Cel.NET | 40.48 us | 40.50 us | 61.93 KB | 62.08 KB | 6.53 | yes |
| E3/100 | full cycle | TelusCel | 458.80 us | 458.83 us | 591.44 KB | 591.59 KB | 74.02 | yes |
| E3/100 | full cycle | GreatPartsCel | 6.78 us | 6.81 us | 11.16 KB | 11.31 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Input | 6.20 us | 6.22 us | 10.68 KB | 10.82 KB | 1.00 |  |
| E3/100 | full cycle | GPCel-Simple | 17.94 us | 17.97 us | 63.40 KB | 63.54 KB | 1.00 |  |
| E4/10 | text creation | - | - | 28.1 ns | - | 184 B | - | |
| E4/10 | compile | Celly | 33.53 us | 33.56 us | 76.08 KB | 76.26 KB | 2.80 | yes |
| E4/10 | compile | DotnetCel | 20.74 us | 20.77 us | 38.16 KB | 38.34 KB | 1.73 | yes |
| E4/10 | compile | Cel.NET | 102.69 us | 102.72 us | 134.54 KB | 134.72 KB | 8.58 | yes |
| E4/10 | compile | TelusCel | 16.44 us | 16.47 us | 21.27 KB | 21.45 KB | 1.37 | yes |
| E4/10 | compile | GreatPartsCel | 11.97 us | 11.99 us | 18.72 KB | 18.90 KB | 1.00 |  |
| E4/10 | compile | GPCel-Input | 12.24 us | 12.27 us | 18.32 KB | 18.50 KB | 1.00 |  |
| E4/10 | evaluate | Celly | 3.63 us | - | 4.05 KB | - | 2.01 | yes |
| E4/10 | evaluate | DotnetCel | 5.62 us | - | 5.31 KB | - | 3.12 | yes |
| E4/10 | evaluate | Cel.NET | 5.32 us | - | 7.38 KB | - | 2.95 | yes |
| E4/10 | evaluate | TelusCel | 79.60 us | - | 99.98 KB | - | 44.20 | yes |
| E4/10 | evaluate | GreatPartsCel | 1.82 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Input | 1.80 us | - | 840 B | - | 1.00 |  |
| E4/10 | evaluate | GPCel-Simple | 4.57 us | - | 7.77 KB | - | 1.26 | yes |
| E4/10 | full cycle | Celly | 39.26 us | 39.29 us | 80.12 KB | 80.30 KB | 2.76 | yes |
| E4/10 | full cycle | DotnetCel | 31.15 us | 31.18 us | 46.87 KB | 47.05 KB | 2.19 | yes |
| E4/10 | full cycle | Cel.NET | 113.98 us | 114.01 us | 141.92 KB | 142.10 KB | 8.02 | yes |
| E4/10 | full cycle | TelusCel | 95.76 us | 95.79 us | 121.26 KB | 121.44 KB | 6.74 | yes |
| E4/10 | full cycle | GreatPartsCel | 14.49 us | 14.52 us | 19.57 KB | 19.75 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Input | 14.21 us | 14.24 us | 19.17 KB | 19.35 KB | 1.00 |  |
| E4/10 | full cycle | GPCel-Simple | 18.01 us | 18.04 us | 25.29 KB | 25.47 KB | 1.00 |  |
| E4/100 | text creation | - | - | 27.4 ns | - | 184 B | - | |
| E4/100 | compile | Celly | 33.12 us | 33.15 us | 76.07 KB | 76.25 KB | 2.75 | yes |
| E4/100 | compile | DotnetCel | 20.58 us | 20.61 us | 38.16 KB | 38.34 KB | 1.71 | yes |
| E4/100 | compile | Cel.NET | 102.37 us | 102.40 us | 134.54 KB | 134.72 KB | 8.50 | yes |
| E4/100 | compile | TelusCel | 16.27 us | 16.30 us | 21.27 KB | 21.45 KB | 1.35 | yes |
| E4/100 | compile | GreatPartsCel | 12.23 us | 12.26 us | 18.72 KB | 18.90 KB | 1.00 |  |
| E4/100 | compile | GPCel-Input | 12.04 us | 12.07 us | 18.32 KB | 18.50 KB | 1.00 |  |
| E4/100 | evaluate | Celly | 40.44 us | - | 42.44 KB | - | 2.36 | yes |
| E4/100 | evaluate | DotnetCel | 65.54 us | - | 70.80 KB | - | 3.82 | yes |
| E4/100 | evaluate | Cel.NET | 63.20 us | - | 100.41 KB | - | 3.68 | yes |
| E4/100 | evaluate | TelusCel | 901.20 us | - | 1.16 MB | - | 52.51 | yes |
| E4/100 | evaluate | GreatPartsCel | 17.16 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Input | 18.80 us | - | 9.57 KB | - | 1.00 |  |
| E4/100 | evaluate | GPCel-Simple | 42.34 us | - | 62.22 KB | - | 1.05 |  |
| E4/100 | full cycle | Celly | 78.52 us | 78.55 us | 118.47 KB | 118.65 KB | 2.44 | yes |
| E4/100 | full cycle | DotnetCel | 91.32 us | 91.35 us | 112.36 KB | 112.54 KB | 2.84 | yes |
| E4/100 | full cycle | Cel.NET | 171.31 us | 171.34 us | 234.96 KB | 235.14 KB | 5.32 | yes |
| E4/100 | full cycle | TelusCel | 954.65 us | 954.67 us | 1.18 MB | 1.18 MB | 29.66 | yes |
| E4/100 | full cycle | GreatPartsCel | 33.34 us | 33.37 us | 28.32 KB | 28.50 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Input | 32.19 us | 32.21 us | 27.92 KB | 28.10 KB | 1.00 |  |
| E4/100 | full cycle | GPCel-Simple | 55.23 us | 55.26 us | 79.74 KB | 79.92 KB | 1.00 |  |
| E5 | text creation | - | - | 27.0 ns | - | 168 B | - | |
| E5 | compile | Celly | 12.81 us | 12.84 us | 35.52 KB | 35.68 KB | 4.91 | yes |
| E5 | compile | DotnetCel | 9.73 us | 9.76 us | 24.38 KB | 24.55 KB | 3.73 | yes |
| E5 | compile | Cel.NET | 20.70 us | 20.72 us | 30.64 KB | 30.81 KB | 7.94 | yes |
| E5 | compile | TelusCel | 5.58 us | 5.60 us | 8.66 KB | 8.82 KB | 2.14 | yes |
| E5 | compile | GreatPartsCel | 3.19 us | 3.22 us | 6.43 KB | 6.59 KB | 1.00 |  |
| E5 | compile | GPCel-Input | 2.61 us | 2.63 us | 5.50 KB | 5.67 KB | 1.00 |  |
| E5 | evaluate | Celly | 545.7 ns | - | 752 B | - | 1.75 | yes |
| E5 | evaluate | DotnetCel | 565.4 ns | - | 448 B | - | 1.81 | yes |
| E5 | evaluate | Cel.NET | 78.13 us | - | 251.94 KB | - | 250.26 | yes |
| E5 | evaluate | TelusCel | 9.88 us | - | 14.41 KB | - | 31.65 | yes |
| E5 | evaluate | GreatPartsCel | 407.9 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Input | 312.2 ns | - | 288 B | - | 1.00 |  |
| E5 | evaluate | GPCel-Simple | 2.15 us | - | 7.27 KB | - | 3.94 | yes |
| E5 | full cycle | Celly | 13.96 us | 13.99 us | 36.14 KB | 36.31 KB | 4.43 | yes |
| E5 | full cycle | DotnetCel | 14.39 us | 14.42 us | 28.04 KB | 28.20 KB | 4.56 | yes |
| E5 | full cycle | Cel.NET | 115.00 us | 115.03 us | 282.58 KB | 282.75 KB | 36.46 | yes |
| E5 | full cycle | TelusCel | 16.87 us | 16.90 us | 23.06 KB | 23.23 KB | 5.35 | yes |
| E5 | full cycle | GreatPartsCel | 3.81 us | 3.84 us | 6.74 KB | 6.90 KB | 1.00 |  |
| E5 | full cycle | GPCel-Input | 3.15 us | 3.18 us | 5.81 KB | 5.97 KB | 1.00 |  |
| E5 | full cycle | GPCel-Simple | 5.29 us | 5.32 us | 12.88 KB | 13.04 KB | 1.00 |  |
| E6 | text creation | - | - | 27.7 ns | - | 184 B | - | |
| E6 | compile | Celly | 16.88 us | 16.91 us | 43.06 KB | 43.24 KB | 4.46 | yes |
| E6 | compile | Cel.NET | 37.91 us | 37.94 us | 50.61 KB | 50.79 KB | 10.02 | yes |
| E6 | compile | TelusCel | 10.32 us | 10.34 us | 13.98 KB | 14.16 KB | 2.73 | yes |
| E6 | compile | GreatPartsCel | 5.14 us | 5.17 us | 9.18 KB | 9.36 KB | 1.00 |  |
| E6 | compile | GPCel-Input | 3.78 us | 3.81 us | 6.46 KB | 6.64 KB | 1.00 |  |
| E6 | compile | DotnetCel | - | - | - | - | excluded | |
| E6 | evaluate | Celly | 525.4 ns | - | 912 B | - | 1.79 | yes |
| E6 | evaluate | Cel.NET | 535.4 ns | - | 1.55 KB | - | 1.82 | yes |
| E6 | evaluate | TelusCel | 12.65 us | - | 18.44 KB | - | 43.07 | yes |
| E6 | evaluate | GreatPartsCel | 374.1 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Input | 293.7 ns | - | 288 B | - | 1.00 |  |
| E6 | evaluate | GPCel-Simple | 2.13 us | - | 7.27 KB | - | 4.06 | yes |
| E6 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E6 | full cycle | Celly | 17.78 us | 17.81 us | 43.93 KB | 44.11 KB | 4.16 | yes |
| E6 | full cycle | Cel.NET | 39.04 us | 39.07 us | 52.16 KB | 52.34 KB | 9.14 | yes |
| E6 | full cycle | TelusCel | 24.43 us | 24.45 us | 32.42 KB | 32.60 KB | 5.72 | yes |
| E6 | full cycle | GreatPartsCel | 5.80 us | 5.82 us | 9.49 KB | 9.67 KB | 1.00 |  |
| E6 | full cycle | GPCel-Input | 4.27 us | 4.30 us | 6.77 KB | 6.95 KB | 1.00 |  |
| E6 | full cycle | GPCel-Simple | 6.79 us | 6.81 us | 14.02 KB | 14.20 KB | 1.00 |  |
| E6 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E7 | text creation | - | - | 24.8 ns | - | 128 B | - | |
| E7 | compile | Celly | 16.60 us | 16.63 us | 45.37 KB | 45.49 KB | 4.14 | yes |
| E7 | compile | DotnetCel | 11.12 us | 11.14 us | 26.02 KB | 26.14 KB | 2.77 | yes |
| E7 | compile | Cel.NET | 36.29 us | 36.31 us | 52.72 KB | 52.85 KB | 9.05 | yes |
| E7 | compile | GreatPartsCel | 4.42 us | 4.44 us | 7.50 KB | 7.63 KB | 1.00 |  |
| E7 | compile | GPCel-Input | 4.01 us | 4.04 us | 7.07 KB | 7.20 KB | 1.00 |  |
| E7 | compile | TelusCel | - | - | - | - | excluded | |
| E7 | evaluate | Celly | 717.6 ns | - | 1.18 KB | - | 2.61 | yes |
| E7 | evaluate | DotnetCel | 451.8 ns | - | 592 B | - | 1.64 | yes |
| E7 | evaluate | Cel.NET | 740.1 ns | - | 1.59 KB | - | 2.69 | yes |
| E7 | evaluate | GreatPartsCel | 378.2 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Input | 275.4 ns | - | 288 B | - | 1.00 |  |
| E7 | evaluate | GPCel-Simple | 2.07 us | - | 7.27 KB | - | 4.58 | yes |
| E7 | evaluate | TelusCel | - | - | - | - | excluded | |
| E7 | full cycle | Celly | 17.96 us | 17.99 us | 46.54 KB | 46.67 KB | 4.15 | yes |
| E7 | full cycle | DotnetCel | 14.99 us | 15.01 us | 29.17 KB | 29.29 KB | 3.46 | yes |
| E7 | full cycle | Cel.NET | 37.97 us | 37.99 us | 54.33 KB | 54.46 KB | 8.77 | yes |
| E7 | full cycle | GreatPartsCel | 4.93 us | 4.95 us | 7.82 KB | 7.94 KB | 1.00 |  |
| E7 | full cycle | GPCel-Input | 4.33 us | 4.36 us | 7.39 KB | 7.51 KB | 1.00 |  |
| E7 | full cycle | GPCel-Simple | 6.85 us | 6.87 us | 14.48 KB | 14.60 KB | 1.00 |  |
| E7 | full cycle | TelusCel | - | - | - | - | excluded | |
| E8 | text creation | - | - | 28.1 ns | - | 192 B | - | |
| E8 | compile | Celly | 21.69 us | 21.72 us | 52.61 KB | 52.80 KB | 3.68 | yes |
| E8 | compile | Cel.NET | 73.88 us | 73.91 us | 99.35 KB | 99.54 KB | 12.53 | yes |
| E8 | compile | TelusCel | 14.51 us | 14.54 us | 19.41 KB | 19.59 KB | 2.46 | yes |
| E8 | compile | GreatPartsCel | 6.88 us | 6.91 us | 12.51 KB | 12.70 KB | 1.00 |  |
| E8 | compile | GPCel-Input | 5.90 us | 5.92 us | 10.88 KB | 11.06 KB | 1.00 |  |
| E8 | compile | DotnetCel | - | - | - | - | excluded | |
| E8 | evaluate | Celly | 1.06 us | - | 1.66 KB | - | 3.05 | yes |
| E8 | evaluate | Cel.NET | 622.1 ns | - | 1.66 KB | - | 1.79 | yes |
| E8 | evaluate | TelusCel | 11.62 us | - | 16.25 KB | - | 33.43 | yes |
| E8 | evaluate | GreatPartsCel | 469.6 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Input | 347.6 ns | - | 336 B | - | 1.00 |  |
| E8 | evaluate | GPCel-Simple | 2.27 us | - | 7.27 KB | - | 3.65 | yes |
| E8 | evaluate | DotnetCel | - | - | - | - | excluded | |
| E8 | full cycle | Celly | 24.17 us | 24.19 us | 54.15 KB | 54.33 KB | 3.77 | yes |
| E8 | full cycle | Cel.NET | 76.88 us | 76.91 us | 100.98 KB | 101.17 KB | 11.98 | yes |
| E8 | full cycle | TelusCel | 28.07 us | 28.10 us | 35.66 KB | 35.84 KB | 4.37 | yes |
| E8 | full cycle | GreatPartsCel | 7.66 us | 7.69 us | 12.86 KB | 13.05 KB | 1.00 |  |
| E8 | full cycle | GPCel-Input | 6.42 us | 6.45 us | 11.23 KB | 11.42 KB | 1.00 |  |
| E8 | full cycle | GPCel-Simple | 9.12 us | 9.15 us | 18.41 KB | 18.60 KB | 1.00 |  |
| E8 | full cycle | DotnetCel | - | - | - | - | excluded | |
| E9 | text creation | - | - | 24.4 ns | - | 136 B | - | |
| E9 | compile | Celly | 19.19 us | 19.21 us | 52.65 KB | 52.78 KB | 4.40 | yes |
| E9 | compile | DotnetCel | 11.47 us | 11.50 us | 27.05 KB | 27.18 KB | 2.63 | yes |
| E9 | compile | Cel.NET | 48.60 us | 48.62 us | 76.35 KB | 76.48 KB | 11.15 | yes |
| E9 | compile | TelusCel | 7.23 us | 7.26 us | 10.09 KB | 10.23 KB | 1.66 | yes |
| E9 | compile | GreatPartsCel | 5.35 us | 5.37 us | 9.98 KB | 10.11 KB | 1.00 |  |
| E9 | compile | GPCel-Input | 4.36 us | 4.38 us | 8.43 KB | 8.56 KB | 1.00 |  |
| E9 | evaluate | Celly | 484.0 ns | - | 1.11 KB | - | 1.40 | yes |
| E9 | evaluate | DotnetCel | 504.1 ns | - | 808 B | - | 1.46 | yes |
| E9 | evaluate | Cel.NET | 570.5 ns | - | 1.73 KB | - | 1.65 | yes |
| E9 | evaluate | TelusCel | 7.52 us | - | 10.87 KB | - | 21.73 | yes |
| E9 | evaluate | GreatPartsCel | 442.7 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Input | 345.8 ns | - | 472 B | - | 1.00 |  |
| E9 | evaluate | GPCel-Simple | 2.24 us | - | 7.45 KB | - | 4.63 | yes |
| E9 | full cycle | Celly | 20.05 us | 20.08 us | 53.74 KB | 53.87 KB | 4.14 | yes |
| E9 | full cycle | DotnetCel | 15.03 us | 15.05 us | 30.41 KB | 30.54 KB | 3.10 | yes |
| E9 | full cycle | Cel.NET | 49.96 us | 49.98 us | 78.09 KB | 78.22 KB | 10.32 | yes |
| E9 | full cycle | TelusCel | 15.52 us | 15.55 us | 20.96 KB | 21.09 KB | 3.21 | yes |
| E9 | full cycle | GreatPartsCel | 5.93 us | 5.95 us | 10.47 KB | 10.60 KB | 1.00 |  |
| E9 | full cycle | GPCel-Input | 4.84 us | 4.87 us | 8.91 KB | 9.04 KB | 1.00 |  |
| E9 | full cycle | GPCel-Simple | 7.40 us | 7.42 us | 16.14 KB | 16.27 KB | 1.00 |  |
