# Regex engine: program, matching and the cache

The second half of `Great.Parts.Cel.Library.Regex` ([regex-syntax](regex-syntax.md) is the first): the program that a parsed pattern is built into, the engine that runs it, and the cache of programs that an environment keeps. The engine answers one question — does some part of the string match — in time linear in the string, with no backtracking and no recursion. How `matches` charges its steps, and where it finds the program of a constant pattern, is on [library](library.md) and [evaluation](evaluation.md).

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Library/Regex/Instruction.cs` | the instruction and its kinds |
| `src/Great.Parts.Cel/Library/Regex/CompiledPattern.cs` | the program: instructions, ranges, size; immutable |
| `src/Great.Parts.Cel/Library/Regex/ProgramBuilder.cs` | the build of a program from a parsed pattern |
| `src/Great.Parts.Cel/Library/Regex/Matcher.cs` | the engine: `IsMatch(program, text, ref steps)` |
| `src/Great.Parts.Cel/Library/Regex/SparseSet.cs` | the two sets of states, over rented arrays |
| `src/Great.Parts.Cel/Library/Regex/PatternCache.cs` | the cache of an environment and its entries |

## The program

A program is a `CompiledPattern`: an array of instructions, an array of ranges, the size m and the index of the first instruction. It never changes after the build, so any number of threads run it at once. An instruction is a struct of four `int`s, 16 bytes: the kind, the next instruction, and two arguments.

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

`Char` accepts its code point, `CharFold` a code point whose orbit holds the argument — walk `SimpleFold` from the code point and compare, at most three steps — `Class` a code point by a binary search over its ranges, `StaticClass` by the generated lookup with its negation, `Any` every code point, `AnyNotNewline` all but U+000A. Nothing allocates, nothing calls a delegate, no LINQ and no closure stand on the path, and the standard search of .NET text and the .NET `Regex` are not used.

**Why it is linear.** At one position every instruction enters a set at most once — the sparse set refuses a second entry — and each entry does a constant amount of work: a pop, at most two pushes, or one test of a code point and one binary search over the ranges of a class, of at most r ranges, r bounded by the table or the pattern and constant for a fixed pattern. There are at most n + 1 positions for a text of n UTF-16 code units. So the work is at most c · (n + 1) · m · log r, and the memory is O(m).

**The counter of steps.** A step is the entry of an instruction into a set, in `add`; the test of a consuming instruction is part of the step that put it there. The counter is the parameter `ref long steps`, as the one of the substring search ([library](library.md), "Substring search"): the library passes a local and ignores it, the property test reads it and asserts `steps ≤ (n + 1) · m` with n the UTF-16 length of the text and m the size of the program. The bound holds since an instruction enters a set at most once per position, positions number at most n + 1, and the number of instructions is at most m; the match ends the run early. One unit of the charge for the match is therefore at least ten steps of this counter.

**Memory.** The five arrays — the two sets of two arrays each and the stack — are one `int[5 · (I + 2)]` rented from `ArrayPool<int>.Shared` and returned in a `finally`; at most 100 000 instructions make that 2 MB at the most, below the largest array the pool keeps. A run allocates no managed memory in steady state: the rent finds the array of an earlier call on the thread. The first rent of each size on a process or a thread allocates, and that is not charged; a test that measures allocations runs the evaluation once before it measures. The program of a constant pattern is built once; the program of a pattern from data is built once and kept by the cache; the engine allocates nothing else.

At n = 0 a pattern of size m costs about m/10 units for the match, 6.4 bytes at B = 64 for each unit of m, while the sets and the stack take 20 bytes for each instruction. That is the reason for the pool and not for a change of the divisor of the charge: with rented arrays the bytes counted by the allocation measure at n = 0 are zero in steady state, and the cost table keeps the divisor 10. The cold case, the first rent of a size on a thread, allocates the array once, and the calibration records its number next to the steady one ([tests](tests.md), "Calibration of `matches`"); the bound B per unit is a bound of the steady state, as for every other row of the cost table, and a cold rent is paid once for a size class of the pool — and again only after the runtime has trimmed the pool — not by each evaluation, so it is not held against B. A steady-state number above B goes back as a question about the divisor of the charge; the code does not change the divisor.

## The cache of patterns

An environment keeps the programs of patterns it has seen. A `PatternCache` is a field of `CelEnvironment`, created empty by `CelEnvironmentBuilder.Build`: a new environment, and the environment that `ToBuilder().Build()` gives, each have an empty cache of their own, and no cache is static. The programs of one environment share it, any number of threads at once. The cache does not change a result or a cost: a pattern found in the cache is charged for its parse and its build as if it were not, and a pattern that was evicted is built again with the same outcome ([library](library.md), "Cost").

- **Key and value.** The key is the pattern text, compared ordinally with the default randomized hash of the string. The value is a `PatternEntry`: either the `CompiledPattern`, or the `RegexError` of the pattern; a pattern that fails to compile is cached too. An entry also holds the size m of its program, 0 for an error, so a hit knows what to charge without a parse. Constants and non-constants share the keys: the preparation of a constant at compilation and the evaluation of a pattern from data use the same entries.
- **Size of an entry.** Its weight is m for a program and 1 for an error, in the units of the size of the program. A pattern longer than 4 096 UTF-16 code units, or whose weight is over the capacity of the cache, is parsed and built and used, but not kept: the key is held, and the key of a long pattern would break the bound of the memory that the weights give.
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
