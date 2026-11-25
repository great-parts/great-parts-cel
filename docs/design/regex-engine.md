# Regex engine: program, matching and the cache

The second half of `Great.Parts.Cel.Library.Regex` ([regex-syntax](regex-syntax.md) is the first): the program that a parsed pattern is built into, the engine that runs it, and the cache of programs that an environment keeps. The engine answers one question — does some part of the string match — in time linear in the string, with no backtracking and no recursion. How `matches` charges its steps, and where it finds the program of a constant pattern, is on [library](library.md) and [evaluation](evaluation.md).

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Regex/Instruction.cs` | the instruction and its kinds |
| `src/Great.Parts.Cel/Library/Regex/CompiledPattern.cs` | the program: instructions, ranges, size; immutable |
| `src/Great.Parts.Cel/Library/Regex/ProgramBuilder.cs` | the build of a program from a parsed pattern |
| `src/Great.Parts.Cel/Library/Regex/Matcher.cs` | the engine: `IsMatch(program, text, ref steps)` |
| `src/Great.Parts.Cel/Library/Regex/SparseSet.cs` | the two sets of states, over rented arrays |
| `src/Great.Parts.Cel/Library/Regex/Dfa.cs` | the deterministic automaton of a literal pattern: tables and `IsMatch` |
| `src/Great.Parts.Cel/Library/Regex/DfaBuilder.cs` | the subset construction with its limits; `DfaRefusal` |
| `src/Great.Parts.Cel/Library/Regex/CodePointClasses.cs` | the classes of code points of an automaton |
| `src/Great.Parts.Cel/Library/Regex/LiteralPrefix.cs` | the literal prefix that every match begins with |
| `src/Great.Parts.Cel/Library/Regex/PooledList.cs` | a growing list over rented arrays, for the build |
| `src/Great.Parts.Cel/Library/Regex/PatternCache.cs` | the cache of an environment and its entries |

## The program

A program is a `CompiledPattern`: an array of instructions, an array of ranges, the size m and the index of the first instruction, and two facts that the build reads for the engine — the literal prefix and whether the program is anchored at the beginning of the text (see "Skipping in the engine" below). It never changes after the build, so any number of threads run it at once. An instruction is a struct of four `int`s, 16 bytes: the kind, the next instruction, and two arguments.

| Kind | Meaning | Arguments |
| :- | :- | :- |
| `Char` | one code point | the code point |
| `CharFold` | a code point and the rest of its orbit | the smallest code point of the orbit |
| `Class` | a code point in a list of ranges | the first index in the array of ranges, the number of ranges |
| `StaticClass` | a code point in a table | the table, and whether the table is negated |
| `Any`, `AnyNotNewline` | any code point; any but U+000A | — |
| `Alt` | either of two successors | the second successor; the first is the next instruction |
| `Empty` | a condition of zero width | the condition: beginning or end of text or of a line, a word boundary, or none |
| `Match` | the end of the program | — |

There is no no-operation and no jump: a path goes from an instruction to its successor directly, as the build makes it with patch lists. A class that holds nothing is a `Class` of no ranges. The ranges of the program are pairs `(lo, hi)` of `int`s, 8 bytes, sorted and merged within a class; the copies that a counted repeat makes of a `Class` share the one list. A `StaticClass` names a table of `Library.Unicode` ([unicode-tables](unicode-tables.md)), or one of the five small tables of this namespace — `[0-9]`, `[\t\n\f\r ]`, `[0-9A-Za-z_]` and `Any` and `Ascii` — kept as constants.

The size is the one the parse counted: the number of instructions plus the number of ranges, as [regex-syntax](regex-syntax.md), "The size of the program", defines it. A program takes at most 16 bytes of array per unit of m — 16 for an instruction, 8 for a range — which is what the charge of the build, a quarter of a unit for each unit of m, pays for at 64 bytes a unit ([cost](../cost.md), "matches").

## Building the program

`ProgramBuilder.Build(tree)` follows `compile.go` of Go on the simplified tree: a Thompson construction, one fragment per node, each with a list of the outgoing edges that the next fragment fills in. Counted repeats are expanded as `simplify.go` does ([regex-syntax](regex-syntax.md)): `x{n}` as n copies, `x{n,}` as n − 1 copies and `x+`, `x{n,m}` as n copies and the nested `(x(x)?)?` of m − n levels. The walk is a loop over an explicit stack of the nodes and the copies still to emit; it has no recursion, so the height of the tree and the count of the repeats cost no stack. The array of instructions is allocated once with the number of instructions the parse counted, the `Match` included, and the array of ranges with the number of ranges it counted; the build never grows an array. The parse counts the ranges of a class for its node in the text, so a class that stands only under `x{0}` is counted and not built: then the build copies the ranges it used into an array of the exact length, and `Ranges` of the program is shorter than the count of the parse. Only the ranges differ; `Size` stays the number the parse counted — the bound that the limit and the charge use — and the number of instructions is always the one counted. A program that comes out with another number of instructions than the parse counted is a defect of the library: a debug assertion, and a test that builds every pattern of the tests and compares the two numbers.

The build runs after the parse has been accepted and both the build and the match have been charged ([library](library.md), "Steps"). It allocates the two arrays and nothing else that lives after it returns; the work arrays — the stack of the walk, the lists of edges — are rented from the same pool, as the arrays of the parse.

## The engine

`Matcher.IsMatch(CompiledPattern program, ReadOnlySpan<char> text, ref long steps)` answers `true` as soon as the `Match` instruction enters a set, and `false` when the text is read to its end. It searches without an anchor: at each position the first instruction is added again, so a match may begin anywhere, which is how `matches` means "some part of the string". The answer does not depend on the order of the alternatives or on greediness, so a state is only in the set or not, and there are no priorities and no captured groups.

**The sets.** Two sparse sets of states over the instruction indexes — the current and the next — and an explicit stack for the closure over the instructions that consume nothing. A sparse set is a pair of arrays, `dense` of the members in the order they came and `sparse` that maps an index to its place in `dense`, with a count; a member is true when `(uint)sparse[i] < (uint)count && dense[sparse[i]] == i`. A rented array has old contents, so `sparse[i]` may be any `int`, a negative one or one at or past `count`; the unsigned comparison refuses both in one test, and `dense` is read only at an index inside `count`. Clearing sets the count to 0, so no array is ever cleared and a rented array with old contents is fine. Each set holds at most the number of instructions I, and the stack at most I + 1 entries, since each pop of an `Alt` adds one net entry and a visited instruction is skipped.

**One position.** The engine reads the text as UTF-16 and takes one code point at a time: a high surrogate followed by a low one is one code point; a lone surrogate is U+FFFD and is one position by itself, as Go reads a byte that is not valid UTF-8 as U+FFFD ([regex-syntax](regex-syntax.md)). The code point after the current one is read before the successors are closed, since the conditions of zero width at the next position need both neighbours. For a position the conditions that hold are a bit mask, computed once from the code point before and the one after, as `EmptyOpContext` of Go does: beginning of text when there is none before; beginning of a line then or after U+000A; end of text when there is none after; end of a line then or before U+000A; a word boundary when exactly one of the two is a word character, `[0-9A-Za-z_]` and nothing else, and no boundary when both or neither are.

```text
read c0; flags = context(none, c0); clear(current)
for each position:
    add(current, first, flags)                 // a match may begin here; found → return true
    if there is no code point here: return false
    read c1 after c; flagsNext = context(c, c1); clear(next)
    for each member of current, in order:      // each consuming instruction tests c
        if it accepts c: add(next, its successor, flagsNext)    // found → return true
    swap(current, next); c = c1; flags = flagsNext

add(set, i, flags):  push i
    while the stack is not empty: pop j; if j is in the set, continue; put j in the set; steps = steps + 1
        Alt:   push both successors
        Empty: if its condition is in flags, push the successor
        Match: found
        a consuming kind: stays in the set
```

**Skipping in the engine.** The build reads two facts of the program, for a pattern from data too, within the charge of the build: the literal prefix, by the rules of the prefix of the automaton below (`LiteralPrefix.Of`: at least two code units, none U+FFFD, no `(?i)` at the start, cut at 256 units and not inside a pair of surrogates), and whether every path from the first instruction passes `\A` (`^` without `(?m)`). When the set carried to a position is empty, no thread is alive, and only a match that begins at that position or later can be found: the engine jumps to the next occurrence of the prefix with `SubstringSearch.IndexOf` of `Library.Text`, and answers `false` when there is none; the conditions of the new position are computed from the code point before it and the one at it, as at any position, so `\b`, `(?m)^` and the others hold there as they would after reading the skipped text. An anchored program answers `false` at the first position after 0 where the set is empty after the first instruction is added. Neither changes an answer — every match begins with the prefix, and an anchored one at position 0 — and the counter of steps can only drop. The set of code units that the automaton skips to (below) is not used here: it needs the classes of the automaton.

`Char` accepts its code point, `CharFold` a code point whose orbit holds the argument — walk `SimpleFold` from the code point and compare, at most three steps — `Class` a code point by a binary search over its ranges, `StaticClass` by the generated lookup with its negation, `Any` every code point, `AnyNotNewline` all but U+000A. Nothing allocates, nothing calls a delegate, no LINQ and no closure stand on the path, and the standard search of .NET text and the .NET `Regex` are not used.

**Why it is linear.** At one position every instruction enters a set at most once — the sparse set refuses a second entry — and each entry does a constant amount of work: a pop, at most two pushes, or one test of a code point and one binary search over the ranges of a class, of at most r ranges, r bounded by the table or the pattern and constant for a fixed pattern. There are at most n + 1 positions for a text of n UTF-16 code units. So the work is at most c · (n + 1) · m · log r, and the memory is O(m).

**The counter of steps.** A step is the entry of an instruction into a set, in `add`; the test of a consuming instruction is part of the step that put it there. The counter is the parameter `ref long steps`, as the one of the substring search ([library](library.md), "Substring search"): the library passes a local and ignores it, the property test reads it and asserts `steps ≤ (n + 1) · m` with n the UTF-16 length of the text and m the size of the program. The bound holds since an instruction enters a set at most once per position, positions number at most n + 1, and the number of instructions is at most m; the match ends the run early. One unit of the charge for the match is therefore at least ten steps of this counter.

**Memory.** The five arrays — the two sets of two arrays each and the stack — are one `int[5 · (I + 2)]` rented from `ArrayPool<int>.Shared` and returned in a `finally`; at most 100 000 instructions make that 2 MB at the most, below the largest array the pool keeps. A run allocates no managed memory in steady state: the rent finds the array of an earlier call on the thread. The first rent of each size on a process or a thread allocates, and that is not charged; a test that measures allocations runs the evaluation once before it measures. The program of a constant pattern is built once; the program of a pattern from data is built once and kept by the cache; the engine allocates nothing else.

At n = 0 a pattern of size m costs about m/10 units for the match, 6.4 bytes at B = 64 for each unit of m, while the sets and the stack take 20 bytes for each instruction. That is the reason for the pool and not for a change of the divisor of the charge: with rented arrays the bytes counted by the allocation measure at n = 0 are zero in steady state, and the cost table keeps the divisor 10. The cold case, the first rent of a size on a thread, allocates the array once, and the calibration records its number next to the steady one ([tests](tests.md), "Calibration of `matches`"); the bound B per unit is a bound of the steady state, as for every other row of the cost table, and a cold rent is paid once for a size class of the pool — and again only after the runtime has trimmed the pool — not by each evaluation, so it is not held against B. A steady-state number above B goes back as a question about the divisor of the charge; the code does not change the divisor.

## The automaton of a literal pattern

A pattern that is a string literal gets a deterministic automaton, `Dfa`, when `Matches.Preparer` builds its program at the check of the expression; the automaton is kept in `CompiledPattern.Automaton`, set once before the program is shared, and `matches` runs it whenever a program has one. A pattern from data has none: its build is charged ⌈m/4⌉, which the subset construction would pass ([cost](../cost.md), "matches"), so it is run by `Matcher`. A literal whose text the cache already holds as the program of a pattern from data — a program built without an attempt at the automaton — gets the automaton on a copy of that program over the same arrays of instructions and ranges; the copy is the prepared object of the call site, and the entry of the cache stays as it is, since other threads may run its program and `Automaton` never changes once the program is shared. So a literal has its automaton whatever patterns the environment saw before. The automaton answers the question of `Matcher` — does some part of the text match — with the same result for every pattern and every text; the property that the two agree is checked by `DfaEquivalenceTests` (the corpus of RE2) and by the properties of the engine. The cost of a call does not change.

**When there is none.** `DfaBuilder.TryBuild` gives `null`, and `DfaRefusal` the reason, when the program has a condition of zero width other than the beginning and the end of the text (`^` and `$` without `(?m)` are `\A` and `\z`; the conditions of a line and of a word look at the code point before or after the position, and `Matcher` runs them); when the classes of code points are more than 64; when the states are more than 256; or when the build takes more than 200 000 visits of an instruction — the visits of the evaluations that the classes take, of the closures and of the scans of the members of a state. The three numbers are the limits `DfaBuilder.MaxStates`, `MaxClasses` and `MaxWork` ([limits](../limits.md), "Cache of patterns of `matches`"); a build that passes one stops at once and drops what it made.

**The classes.** The bounds of the instructions cut the code points into intervals; each interval has the set of the instructions that accept it, and the intervals with the same set, next to each other or not, are one class. A class is the unit of a column of the table. A code point below 128 is looked up in a table of bytes, any other one by a binary search over the starts of the merged intervals. `\p{L}` with a space and a hyphen is two or three classes and not the hundreds of intervals of the table of letters; only the classes count against the limit of 64.

**The states.** A state is a sorted set of instructions that read a code point, with the conditions `\z` that the closure could not pass because the text may end there. The first instruction is added to every state, as `Matcher` adds it at each position, so the automaton searches without an anchor; a match is the first state that holds the `Match`, and the run ends there. Row 0 of the table is the state that no text can leave with a match, row 1 is the state that has matched. `\A` is only in the first state: a program that begins with it has no state without a live thread and reaches row 0 at the first position that cannot go on, which ends the run. The end of the text is not a code point, so a row has an answer for it of its own, which a `\z` that is reached through the conditions gives. The text is read as `Matcher` reads it: a pair of surrogates is one code point and a lone surrogate is U+FFFD.

**The tables.** The transitions are an `ushort[]`, a row for each state with one offset of the next row for each class, so a step is `table[offset + class]`; the answers for the end of the text are one more `ushort` for each row, kept in the same array after the transitions. The classes are one `int[]`: the table of the code points below 128, a byte each, and then the merged intervals, each as its start with the number of its class above it. So a build allocates four objects instead of seven: the automaton, its array, the classes and their array (a skip by units or a prefix adds its own). The limits of the build are unchanged. D, the bytes of the tables — the transitions, an answer of a byte for each row, the table of the code points below 128, and the starts of the intervals with their numbers, five bytes an interval — is what the weight of the entry of the cache counts, ⌈D/16⌉ units of 16 bytes — the unit of an instruction of the program — in addition to the size m ([limits](../limits.md)). The tables never change after the build, so any number of threads read them at once. The build takes its working arrays from the shared pool and allocates only what it returns. The limit of the benchmark rig for the build of the automaton of E5 is 2 µs on the standard GitHub Linux runner.

**The search for the next place of a match.** The state of no live thread is the state of the closure of the first instruction alone; in it only a place where a match can begin changes the state, so the automaton does not read the code points between those places. Two ways go to the next one, chosen when the automaton is built:

- The literal prefix — the `Char` instructions from the first instruction, through the conditions of zero width, up to a fork, a class or a folded character, so a pattern that begins with `(?i)` has none — when it is at least two UTF-16 code units, has no U+FFFD (which a lone surrogate of a text is read as, so a search for code units would pass it) and is cut at the 256 units that `SubstringSearch` keeps on the stack and not inside a pair of surrogates. `SubstringSearch.IndexOf` of `Library.Text` finds it. Every match begins with it, so no place before its next occurrence can begin one, and the partial threads that the skipped places could have had end in the same state. After the jump the automaton reads the whole prefix, or the thread dies in a state that is not the first one, so the searches cost at most the text in all.
- Without such a prefix, the code units of the classes that take the first state to another one, when they are all ASCII, in any number, or when there are at most `DfaBuilder.MaxSkipUnits` (3) of them, all in the Basic Multilingual Plane, none a surrogate or U+FFFD: a `SearchValues<char>` made at the build and used with `IndexOfAny`. A set of ASCII units is searched through a bitmap whatever its size, so `[0-9]{4}-[0-9]{2}` in a text of letters goes from digit to digit; a larger set with a unit outside ASCII has no skip.

Otherwise each code point is read. The choice is `Dfa.Skip` (`None`, `Prefix`, `Units`); the prefix is `Dfa.Prefix`.

## The cache of patterns

An environment keeps the programs of patterns it has seen. A `PatternCache` is a field of `CelEnvironment`, created empty by `CelEnvironmentBuilder.Build`: a new environment, and the environment that `ToBuilder().Build()` gives, each have an empty cache of their own, and no cache is static. The programs of one environment share it, any number of threads at once. The cache does not change a result or a cost: a pattern found in the cache is charged for its parse and its build as if it were not, and a pattern that was evicted is built again with the same outcome ([library](library.md), "Cost").

- **Key and value.** The key is the pattern text, compared ordinally with the default randomized hash of the string. The value is a `PatternEntry`: either the `CompiledPattern`, or the `RegexError` of the pattern; a pattern that fails to compile is cached too. An entry also holds the size m of its program, 0 for an error, so a hit knows what to charge without a parse. Constants and non-constants share the keys: the preparation of a constant at compilation and the evaluation of a pattern from data use the same entries.
- **Size of an entry.** Its weight is m + ⌈D/16⌉ for a program, D the bytes of the tables of its automaton (0 for a program without one), and 1 for an error, in the units of the size of the program; the unit of D is 16 bytes, as an instruction is. The literal prefix, at most 256 code units, and the set of code units to skip to are held beyond the weight: their size has a constant bound, under 1 KB an entry, which the limit of 128 entries keeps within the rule of 4 MiB below. A pattern longer than 4 096 UTF-16 code units, or whose weight is over the capacity of the cache, is parsed and built and used, but not kept: the key is held, and the key of a long pattern would break the bound of the memory that the weights give.
- **Two limits.** The number of entries, 128, and the sum of the weights, 100 000 — the two internal constants `PatternCache.DefaultMaxPatterns` and `DefaultMaxWeight`, no public parameter. The calibration measured an entry at about 194 bytes besides its key and its program (the object, the node of the dictionary, the slot of the ring, the headers), the key at 22 + 2 · L bytes for L UTF-16 code units, the program at 16 bytes for each unit of m ([tests](tests.md), "Calibration of `matches`"). The rule keeps a full cache within 4 MiB: the sum of the weights at most 3 MiB / 16 = 196 608, rounded down to one digit 100 000; the number of entries at most 1 MiB over the entry with the longest key that is kept, 194 + 22 + 2 · 4 096 = 8 408 bytes, which is 124, the same first digit as 128. The largest cache is then 128 · 8 408 + 16 · 100 000 bytes, 2.55 MiB. A program of the largest size, 100 000, fills the whole sum alone.
- **Eviction.** The entries stand in a queue in the order they came, each with a flag that a hit sets. An insertion that would pass either limit takes the oldest entry: when its flag is set the flag is cleared and the entry goes to the end of the queue — its second chance — and when it is clear the entry is removed; this repeats until the new entry fits. That is the order of CLOCK with the new entry inserted behind the hand, the hand standing always at the oldest entry. After one turn every flag is clear, so the loop ends; the work of one insertion is at most one turn over at most 128 entries, a constant; no thread and no timer exists, and nothing happens except in an insertion.
- **Threads.** A lookup takes no lock: a `ConcurrentDictionary<string, PatternEntry>` with `StringComparer.Ordinal`, whose `TryGetValue` is lock-free, and a hit sets the flag with a plain `Volatile.Write` — a flag lost to a race only makes an entry the next to be removed. An insertion takes the one lock of the cache. Two threads that miss the same pattern may both build it; the first insertion wins, the other thread uses the entry that is in the cache — or its own program, which is the same — and nothing is observed.
- **The entry outlives its place.** A plan of an expression with a constant pattern holds the program by a reference, so an eviction does not touch it.

The internal contract that the library uses, in the order of a call:

```text
internal static class PatternParser
    bool TryParse(ReadOnlySpan<char> pattern, out PatternTree? parsed, out RegexError? failure)
                                                  // a PatternTree with Size, or a RegexError; rented arrays inside the tree
internal static class ProgramBuilder
    CompiledPattern Build(PatternTree tree)       // allocates the program; the caller disposes the tree
internal sealed class PatternCache
    bool TryGet(string pattern, out PatternEntry entry)
    void Add(string pattern, PatternEntry entry)
internal static class Matcher
    bool IsMatch(CompiledPattern program, ReadOnlySpan<char> text, ref long steps)
```

There is no wrapper over the two calls: the library calls the parser and the builder itself. The three steps of the charge — the parse, the build, the match — are made by the caller: the parse before `TryParse`, the build and the match after it and before `Build`, so that each is charged before the work it pays for and a limit leaves nothing built or kept ([library](library.md), "Steps"). A `PatternTree` holds rented arrays, so it is disposed in a `finally` whatever happens between `TryParse` and `Build`, a charge that stops the evaluation included.

## What the tests read

`Matcher.IsMatch` is called directly by the tests of the engine and by the property of linearity with a `ref long` of their own ([tests](tests.md)); `PatternCache` has `Count` and `Weight` internal members for the tests of the limits and of the eviction. Nothing else of this page is visible outside the namespace.

Related pages: [regex-syntax](regex-syntax.md), [unicode-tables](unicode-tables.md), [library](library.md), [evaluation](evaluation.md), [tests](tests.md), [core](core.md).
