# Unicode tables

`Great.Parts.Cel.Library.Unicode` holds the data about code points that the library needs: the general categories and the scripts for the classes `\p{…}` of a pattern, and the simple case folding for `(?i)` ([regex-syntax](regex-syntax.md)). The data are the Unicode Character Database 17.0.0, the version of `unicode.Version` in Go 1.27.3 that the oracle pins, so the classes and the folding of the library are those of the reference and do not depend on the version of .NET it runs on. The tables are generated code, committed to the repository; a generator under `eng/` makes them.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Unicode/UnicodeTables.Ranges.g.cs` | the ranges of the categories, the scripts and the fold tables |
| `src/Great.Parts.Cel/Library/Unicode/UnicodeTables.Folding.g.cs` | the orbits of the simple case folding |
| `src/Great.Parts.Cel/Library/Unicode/UnicodeTables.Names.g.cs` | the names of the tables |
| `src/Great.Parts.Cel/Library/Unicode/UnicodeTables.cs` | the reading: the lookup, the folding step, the names; written by hand |
| `eng/unicode/` | the generator: a console project `Great.Parts.Cel.Eng.Unicode` |
| `eng/unicode/inputs.txt` | the files of the Unicode Character Database: name, address, SHA-256 |
| `eng/unicode/reference/` | the program of Go that writes the reference, and the reference of Go 1.27.3 |

The namespace is internal and has no dependency on another namespace of the library (`eng/layers.txt`).

## The data

Three files of the Unicode Character Database 17.0.0, from `https://www.unicode.org/Public/17.0.0/ucd/`:

| File | SHA-256 | What it gives |
| :- | :- | :- |
| `UnicodeData.txt` | `2e1efc1dcb59c575eedf5ccae60f95229f706ee6d031835247d843c11d96470c` | the general category of each assigned code point; a range is written as two lines, `<…, First>` and `<…, Last>` |
| `Scripts.txt` | `9f5e50d3abaee7d6ce09480f325c706f485ae3240912527e651954d2d6b035bf` | the script of each code point that has one, as ranges |
| `CaseFolding.txt` | `ff8d8fefbf123574205085d6714c36149eb946d717a0c585c27f0f4ef58c4183` | the simple case folding: the lines of status `C` and `S`; the lines `F` and `T` are not read |

The files are not in the repository (UnicodeData.txt alone is 2.2 MB). `eng/unicode/inputs.txt` pins them — the address and the SHA-256 above — and the generator downloads each file once into `artifacts/unicode/`, outside git, checks its SHA-256 and stops with exit code 2, naming the file, when one differs. With the files in `artifacts/unicode/` it needs no network. No other file of the database is read: the names of the categories are the standard two-letter codes of the general categories and of their unions, the aliases of the names are a table of the generator, copied from `unicode.CategoryAliases` of Go 1.27.3, and the script names are those of `Scripts.txt`.

A change of the version moves three things in one change: the address and the three checksums in `inputs.txt`, the tables, and the reference of Go; the change that moves the Go of the oracle (the `go` line of `eng/oracle/go.mod` and the image tag in `eng/compose.yaml`) does it, since the tables must equal what that Go says.

## What is generated

- **Categories.** The 38 names of `unicode.Categories` of Go: the 30 two-letter general categories (`Lu`, `Ll`, `Lt`, `Lm`, `Lo`, `Mn`, `Mc`, `Me`, `Nd`, `Nl`, `No`, `Pc`, `Pd`, `Ps`, `Pe`, `Pi`, `Pf`, `Po`, `Sm`, `Sc`, `Sk`, `So`, `Zs`, `Zl`, `Zp`, `Cc`, `Cf`, `Cs`, `Co`, `Cn`); the unions `L`, `M`, `N`, `P`, `S`, `Z`, `C`; and `LC`, the cased letters `Lu`, `Ll` and `Lt`. `Cn`, the unassigned, is every code point from 0 to U+10FFFF that `UnicodeData.txt` does not list, noncharacters included; `C` is `Cc`, `Cf`, `Cs`, `Co` and `Cn`.
- **Scripts.** One table for each script name of `Scripts.txt`, by that name, with an underscore where the file has one (`Old_Italic`).
- **Fold tables.** For each category and each script whose table `T` is not closed under case folding: the table of the code points that are not in `T` and whose orbit has a member in `T`. This is `unicode.FoldCategory` and `unicode.FoldScript` of Go, which a class under `(?i)` adds to its table. A table that is closed has none. `LC` has none either, though by this definition it would have one (the orbit of U+0345, a `Mn`, has U+0399 and U+03B9 in `LC`): `unicode.FoldCategory` of Go has no key `LC`, so `(?i)\p{LC}` of Go matches `LC` alone, and the generator follows Go.
- **Orbits.** The classes of code points that fold together — the code points with the same simple case folding by the lines `C` and `S` of `CaseFolding.txt`, a code point with no line being its own class. A table of pairs: for each code point that has a class of more than one member, the next member in ascending order, the smallest after the last, as `unicode.SimpleFold` of Go gives it. The orbit of `k` is `K`, `k`, U+212A; of U+0345 is U+0345, U+0399, U+03B9, U+1FBE; of `ß` is `ß` and U+1E9E; U+0130 and U+0131 have none, since their lines are `T` and `F`.
- **Names.** A `switch` on a `ReadOnlySpan<char>` of the canonical names — the name of Go after `canonicalName`, written once, in its canonical form — that gives the table, its fold table or none, and whether the class is the complement of the table (`Assigned`). It contains the names of the categories, their aliases, the scripts, and `Lc`; not `Any` and `Ascii`, which the parser of the pattern knows by itself ([regex-syntax](regex-syntax.md)). A `switch` on constants is a series of comparisons that the compiler writes, so the lookup of a name allocates nothing and needs no dictionary.

## The reading

`UnicodeTables` is a static class of internal members, written by hand next to the generated parts:

```text
static ReadOnlySpan<int> Ranges(int table)                 // the pairs lo, hi of a table, ascending, merged
static bool Contains(int table, int codePoint)             // a binary search over Ranges(table)
static int Next(int codePoint)                             // the next member of the orbit, or the code point itself
static int OrbitCount                                      // the number of code points that have an orbit of more than one member
static int OrbitKey(int index)                             // the code point at an index of that list, ascending
static int FirstOrbitIndex(int codePoint)                  // the index of the first code point of the list that is not below the given one; OrbitCount when there is none
static bool TryFindTable(ReadOnlySpan<char> canonicalName, out TableName name)
                                                           // the table, the fold table (-1 for none), the complement flag
```

The three members `OrbitCount`, `OrbitKey` and `FirstOrbitIndex` serve the folding of a range of a class: the fold visits only the code points that have an orbit, from `FirstOrbitIndex(lo)` on while `OrbitKey(index) ≤ hi`, instead of every code point of the range.

A table is an `int` number. The ranges of all tables are one array of `int` pairs, with the first index and the count of each table kept beside it, so a table is a slice and its lookup is a binary search; the orbits are two arrays of `int`, the code points and their successors, searched the same way. Each array is written as a `ReadOnlySpan<int>` of constants, which the compiler places in the data of the assembly: loading the type allocates nothing, and under Native AOT the data are in the image. The reading never takes a `char` property, `CharUnicodeInfo`, `Rune` or an `IsLetter` of .NET, and no frozen or concurrent collection.

## The form of the output

The generated files are `*.g.cs` in `src/Great.Parts.Cel/Library/Unicode/`, the directory that the source checks allow generated files in ([eng-checks-source](eng-checks-source.md#generated-code)). Each begins with the comment `// <auto-generated` … `// </auto-generated>`, which names the version of the database and the three checksums, and nothing that changes from one run to the next — no date, no machine, no version of the generator. Because the marker makes the file generated, the compiler and the analyzers skip it, and the checks treat it as outside their file set: it takes no `[GeneratedCode]`, no `#pragma`, no entry of `.editorconfig` and no place in the ratchet, and none of those is to be added. A generated file has the nullable context off; the code in it needs none.

The output is deterministic. The tables of the categories and the scripts go first, together in the order of their names compared ordinally, numbered from 0; the fold tables follow them, in the same order of the names of their tables, so a fold table never comes between two main tables; ranges ascend; numbers are written in hexadecimal with the invariant culture and the same width; lines end in LF with a fixed indentation and a fixed number of values on a line; the file ends in one line feed. Running the generator twice, in any directory and with any culture of the process, gives the same bytes; the committed files are the output of a run, never edited by hand, and the command has a mode that regenerates and fails when a file differs from the committed one.

## How the tables are checked

Two sets of tests, none of which needs the network:

- **The generator**, in `eng/tests/Unicode/`, over small committed excerpts of the three files: the two-line form of a range, the lines of the statuses `C`, `S`, `F` and `T`, the unions, `Cn` as the complement, the aliases, the orbit table, the fold table of a category with one orbit that leaves it, the form of the output, and that two runs give the same bytes.
- **The tables against Go**, in `tests/Great.Parts.Cel.Tests/Regex/`: the program in `eng/unicode/reference/` is run in a one-off container of the image of the oracle — `golang:1.27.3`, the tag of the `go` line of `eng/oracle/go.mod` — by the `docker run` command written in the header of `reference.go`, run by hand (it is needed only when the version moves, so `eng/run` has no command for it), and writes `eng/unicode/reference/go-1.27.3.txt` with `unicode.Version`, the ranges of every table of `unicode.Categories` and `unicode.Scripts`, the aliases, `unicode.FoldCategory` and `unicode.FoldScript`, and `unicode.SimpleFold` of every code point that it changes. `UnicodeTablesTests` reads the file, as a file next to the test assembly, and compares: the version equal to the one in `inputs.txt`; every table of the file equal, range by range, to the generated one, and no table that is not in the file; the lookup `Contains` at the two ends of every range and one code point beyond each; the aliases; `Next` of every code point from 0 to U+10FFFF equal to `unicode.SimpleFold`. The reference is regenerated in the same change as the tables, by the same command.

Both the tables and the reference are of the same Go as the oracle, so the differential run ([eng-difftest](eng-difftest.md)) checks them again, in use, with `\p{…}` and `(?i)` in generated patterns.

## NOTICE

The tables are data derived from the Unicode Character Database, under the Unicode License V3. The `NOTICE` of the repository has one entry for them, written with the generator: the line `This product includes data derived from the Unicode Character Database, version 17.0.0. Copyright © 1991-2026 Unicode, Inc. Licensed under the Unicode License V3.` and, below it, the text of the license as published at `https://www.unicode.org/license.txt`, copied whole, with its copyright line. The license asks that copies of the data keep the notice of copyright and the permission notice; the generated files carry the first, and the `NOTICE` carries both. The package contains `NOTICE`.

Related pages: [regex-syntax](regex-syntax.md), [regex-engine](regex-engine.md), [core](core.md), [eng-checks-source](eng-checks-source.md), [eng-difftest](eng-difftest.md).
