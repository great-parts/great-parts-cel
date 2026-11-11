# Syntax: lexer, parser and syntax tree

`Great.Parts.Cel.Syntax` turns the text of an expression into a syntax tree with positions, or into parse errors. The grammar and the lexis are those of the language definition of the pinned cel-spec commit, plus escaped field names and optional selection, index and elements of cel-go, the CEL implementation in Go. Where the language definition is silent, cel-go v0.32.0 is the reference: `parser/parser.go`, `parser/macro.go`, `parser/gen/CEL.g4`.

| File in `src/Great.Parts.Cel/` | Content |
| :- | :- |
| `Syntax/Lexer.cs`, `Token.cs`, `TokenKind.cs`, `Literals.cs` | tokens and the values of literals |
| `Syntax/Parser.cs` | recursive descent, limits, recovery |
| `Syntax/SyntaxOptions.cs`, `Macros.cs` | the syntax options; the macro table and the standard macros |
| `Syntax/NodeFactory.cs`, `ParsedExpression.cs`, `ExprNode.cs`, `MacroCall.cs`, one file per node class | the tree |
| `Syntax/Printer.cs`, `SyntaxTreeComparer.cs` | printing and tree equality |
| `Syntax/ParseResult.cs` | the result of the internal entry point `CelEnvironment.Parse` ([facade](facade.md)) |
| `Diagnostics/LineTable.cs` | line table, shared with the checker, the evaluator and the JSON reader |

## Syntax options

The parser receives an immutable `SyntaxOptions`: `OptionalSyntax`, whether `.?`, `[?` and `?` before an element are accepted, and `Macros`, a `MacroTable` of macros by name, argument count and call style. `SyntaxOptions.Standard` has `OptionalSyntax` off and `MacroTable.Standard`, and every environment uses it until extension sets bring syntax: the set `optional` turns `OptionalSyntax` on, sets with macros add them, and the flag of the `decimal` literal joins with the `decimal` set. With `MacroTable.Empty` every call is a call.

## Lexer

The lexer reads the text once, from left to right, and yields tokens: a kind, an offset and a length in UTF-16 code units. It covers the whole lexis; only the parser decides which constructs are accepted.

- Whitespace (space, `\t`, `\n`, `\f`, `\r`) and comments from `//` to the end of the line separate tokens.
- Identifiers are `[_a-zA-Z][_a-zA-Z0-9]*`; `true`, `false`, `null` and `in` are keywords. Reserved words such as `if` are identifiers to the lexer.
- An escaped identifier is a backtick, one or more letters, digits, `_`, `.`, `-`, `/` or spaces, and a backtick (`ESC_IDENTIFIER` of cel-go); its value is the text between the backticks. An empty or unclosed one, or one with another character, is `SyntaxError` at the opening backtick.
- Numbers: decimal digits, or `0x` and hexadecimal digits; the suffix `u` or `U` for unsigned integers; a fraction or an exponent for floating literals (`1.5`, `.5`, `1e3`). The sign is not part of the token.
- A `decimal` literal is a floating or decimal integer literal with the suffix `m` or `M` (`12.34m`, `100m`, `.5m`) not followed by an identifier character; otherwise the number ends before the suffix, so `0x10m` and `12.5mx` are a number and an identifier. Without the flag of the options it is `SyntaxNotEnabled`.
- Strings: an optional `r` or `R` for a raw string, and `b` or `B` in front for bytes; the quotes `"`, `'`, `"""` and `'''`. A single-quoted form must not contain `\r` or `\n`. Except in raw strings, the lexer steps over escape sequences while it looks for the closing quote.
- `?` is one token wherever it appears; whitespace may separate it from a `.` or `[` before it, as in cel-go, where they are separate tokens too.
- A character outside the lexis, or a string without its closing quote, is `SyntaxError` at its position.

## Literal values

Literal tokens become constants of type `CelValue`. The conversion never uses the culture of the process.

| Literal | Value | `InvalidLiteral` when |
| :- | :- | :- |
| integer | `long`, decimal or hexadecimal, with its sign | outside the range of `long` |
| unsigned integer | `ulong` | outside the range of `ulong` |
| floating | `double`, correctly rounded | it overflows to infinity, as in cel-go |
| string | escape sequences are interpreted, except in a raw string | an unknown escape; an escape that gives a surrogate or a code point above U+10FFFF; a lone surrogate in the text, raw strings included |
| bytes | the UTF-8 encoding of the text; `\x` and octal escapes are single bytes | as for strings; `\u` and `\U`, as in cel-go |
| `true`, `false`, `null` | `bool`, `null` | — |

Escape sequences: `\\`, `\?`, `\"`, `\'`, `` \` ``, `\a`, `\b`, `\f`, `\n`, `\r`, `\t`, `\v`; `\x` or `\X` with two hexadecimal digits; `\u` with four; `\U` with eight; three octal digits from `000` to `377`. In a string, the numeric escapes denote code points. A lone surrogate outside literals is a character outside the lexis; in a comment it is ignored. cel-go reads UTF-8 and has no such character.

## Grammar

Levels from the lowest; the operands of a level are expressions of the next one.

| Level | Construct | Function in the tree | Associativity | AST depth |
| :- | :- | :- | :- | :- |
| 1 | `c ? a : b`; `c` and `a` are of level 2 | `_?_:_` | right | counts |
| 2 | `\|\|` | `_\|\|_` | balanced tree | no |
| 3 | `&&` | `_&&_` | balanced tree | no |
| 4 | `<` `<=` `>=` `>` `==` `!=` `in` | `_<_` `_<=_` `_>=_` `_>_` `_==_` `_!=_` `@in` | left | counts |
| 5 | `+` `-` | `_+_` `_-_` | left | counts |
| 6 | `*` `/` `%` | `_*_` `_/_` `_%_` | left | counts |
| 7 | a run of `!` or of `-` | `!_`, `-_` | — | no |
| 8 | `.f`, `.?f`, `.f(args)`, `[i]`, `[?i]` after a member | select, `_?._`, receiver call, `_[_]`, `_[?_]` | left | counts |
| 9 | primary | — | — | no |

- **Primary.** A literal; an identifier, with an optional leading `.`; such an identifier followed by `(`, a call of a global function; `(` expression `)`, which adds no node; a list `[a, ?b]`; a map `{k: v, ?k2: v2}`; a message `a.b{f: v, ?g: w}`, also with a leading `.`. Lists, maps and messages allow a trailing comma, arguments do not. The grammar gives them `[items] [","]`, so a lone comma without items is valid too and the construct is empty — `[,]`, `{,}`, `a.b{,}` — while a comma before a first item, `[,1]`, is a `SyntaxError` at the item.
- **Names.** An identifier and a global function name are an `IDENT`: a reserved word there is `ReservedIdentifier`. A field after `.` or `.?` and a message field is a `SELECTOR` — reserved words allowed, `a.if` — or an escaped identifier. A receiver function name and a segment of a message name are a `SELECTOR`, not escaped: `a.package()` is a call. A leading dot is part of the name: `.x`, `.a.b`.
- **Messages.** An identifier followed by `.SELECTOR` pairs and `{` is a message name: the parser looks ahead over the pairs, the only place where it needs more than one token of lookahead.
- **Unary runs.** A run of `-` or of `!` applies to one member and is folded as in cel-go: an even run adds no node, an odd run is one call at the offset of its first operator, whitespace between them does not matter. So `--x` is `x`, `!!!x` is `!_(x)`, and with `x` the smallest `int`, `--x` is a value while `-(-x)` is `Overflow`. A run never mixes: `-!x` and `!-x` are `SyntaxError` at the second operator.
- **Signed literals.** Where an operand is expected, a single minus followed by an integer or floating literal belongs to the literal, also after a run of `!`: `-9223372036854775808` is the smallest `int`, `[-1]` a list of one constant, `!-1` is `!_(-1)`, `-1.f` a selection on `-1`, `-(-1)` is `-_(-1)`. A minus before any other operand, an unsigned literal included, is a call of `-_`; a minus in a run of minuses never belongs to a literal: `--1` is `1`, `---1` is `-_(1)`.
- **Optional syntax.** `.?f` is `_?._(operand, "f")`, `[?i]` is `_[?_](operand, i)`; `?` before a list element makes its index optional, before a map key or a message field the entry. Without `OptionalSyntax` each is `SyntaxNotEnabled` at the `?`.

## Syntax tree

The tree follows `syntax.proto` of cel-spec. Nodes are immutable classes with an id unique in the tree. The tree holds no types.

| `syntax.proto` | Class |
| :- | :- |
| `ParsedExpr` | `ParsedExpression`: the root node, positions by id, the line table, macro calls by id |
| `Expr`, field `id` | `ExprNode`, abstract, property `Id` |
| `Constant` | `ConstantNode`: a `CelValue` of the kind `Null`, `Bool`, `Int`, `UInt`, `Double`, `String` or `Bytes` |
| `Expr.Ident` | `IdentNode`: the name |
| `Expr.Select` | `SelectNode`: the operand, the field, `IsPresenceTest`, set only by `has` |
| `Expr.Call` | `CallNode`: the function name, the target or `null`, the arguments; operators included |
| `Expr.CreateList` | `ListNode`: the elements, `OptionalIndices` |
| `Expr.CreateStruct` | `StructNode`: the message name, empty for a map; entries `StructEntry` with an id, a field name or a key, a value and `IsOptional` |
| `Expr.Comprehension` | `ComprehensionNode`: `IterVar`, `IterRange`, `AccuVar`, `AccuInit`, `LoopCondition`, `LoopStep`, `Result` |
| `SourceInfo` | positions and macro calls in `ParsedExpression`; `MacroCall`: function, target, arguments as written; `LineTable` |

The class names differ from the `syntax.proto` messages so that the conformance runner can use both without ambiguity.

Nodes are created only through `NodeFactory`, one method per node class and one for entries, also by other sources of trees inside the assembly. It assigns ids from 1 in the order of creation, without gaps, and records the offset of each; the parser creates a node after its children. Its own tests check the ids and the offset of every node class and of entries. Nodes of arguments that a macro drops keep their ids, so the ids of a tree may have gaps, as in cel-go.

## Positions

A position is an offset in UTF-16 code units from the start of the text — the index in the .NET string, the unit of `CelSourceLocation`; `syntax.proto` counts code points. The token is the one that cel-go takes for the id of the node.

| Node | Its offset is the offset of |
| :- | :- |
| literal | its first character, the minus included |
| identifier | the identifier token, without a leading dot |
| global call, receiver call | the `(` |
| select, `_?._` | the `.`; the field constant of `_?._` — the field token |
| `_[_]`, `_[?_]` | the `[` |
| binary operator; `!_`, `-_`; `_?_:_` | the operator; the first operator of the run; the `?` |
| list, map, message; map entry, message field | `[` or `{`; the `:` |
| every node created by a macro | the `(` of the macro call |

`LineTable` holds the offset right after each line break (`\n`, `\r\n`, `\r`). The line of an offset is 1 plus the number of line starts not greater than it; the column is the offset minus the start of its line, plus 1. An error at the end of the text has the offset equal to its length.

## Macros

A call whose name, argument count and style — global or receiver — match a macro of the options is replaced by its expansion when the call has been parsed, with its target and arguments already expanded; expansion is not recursive. `ParsedExpression` records the call as written under the id of the expansion. The shape is that of `parser/macro.go` with the hidden accumulator `@result`, a name no text can write (cel-go `EnableHiddenAccumulatorName`). The standard macros, `StandardMacros` of cel-go, with `r` the target and `v` the identifier of the first argument:

| Macro | `AccuInit` | `LoopCondition` | `LoopStep` | `Result` |
| :- | :- | :- | :- | :- |
| `r.all(v, p)` | `true` | `@not_strictly_false(@result)` | `@result && p` | `@result` |
| `r.exists(v, p)` | `false` | `@not_strictly_false(!@result)` | `@result \|\| p` | `@result` |
| `r.exists_one(v, p)` | `0` | `true` | `p ? @result + 1 : @result` | `@result == 1` |
| `r.map(v, f)` | `[]` | `true` | `@result + [f]` | `@result` |
| `r.map(v, p, f)` | `[]` | `true` | `p ? @result + [f] : @result` | `@result` |
| `r.filter(v, p)` | `[]` | `true` | `p ? @result + [v] : @result` | `@result` |

Each is a `ComprehensionNode` with `IterVar` `v`, `IterRange` `r` and `AccuVar` `@result`; each `@result` in a part is an `IdentNode`. The factory creates the parts in the order of the columns, then the comprehension. `filter` puts the identifier node of its first argument into the step, the other macros drop it. `has(e.f)` is `SelectNode(e, f)` with `IsPresenceTest`; the select node of the argument is dropped.

`InvalidMacroCall` (1006), and the call is not expanded, when: the first argument of a comprehension macro is not a simple identifier (`argument must be a simple name`); it is `__result__`, the accumulator of cel-go without the hidden one (`iteration variable overwrites accumulator variable`); the argument of `has` is not a selection, `.?f` included (`argument must be a field selection`).

## Macros of extension sets

The macros of `math`, `lists`, `bindings` and of two iteration variables, their keys, expansions, the texts of their errors, and the second iteration variable of `ComprehensionNode` are on [extension-macros](extension-macros.md). The options of an environment hold them in the `MacroTable` that `Build` makes from the standard macros and those of the sets the environment has.

## Limits and the stack

The values are those of the limits page ([limits](../limits.md)); none can be configured.

- **Length.** Before lexing: more than 65 536 code units is over the limit, since a code unit is at least one UTF-8 byte; otherwise one pass counts the bytes, 3 for a lone surrogate, the length of the U+FFFD that replaces it. Over 65 536 bytes is `ExpressionTooLong` at the first character past the limit; only the first 65 537 code units are read, also for the line and the column, and nothing is parsed.
- **Nesting.** As in cel-go, which counts open calls per grammar rule: at a point of the text it is `1 + B + max(F, R, A)`, with `B` enclosing brackets of any kind, `F` enclosing third operands of `?:`, `R` enclosing right operands of level 4, `A` of levels 5 and 6. Nothing else adds, the second operand of `?:` included; flat chains do not grow it; in `a + b * c` the `c` has `A` = 2. Over 250 is `NestingTooDeep`, kind `nesting`, at the `(`, `[` or `{` that opens a bracket construct; at the operator whose right operand it is; for a third operand of `?:`, at the `?` of that `?:`, not at the `:` before the operand. So 249 nested `(` or `c ? (` pass, 250 fail; 124 `x + (` pass, 125 fail.
- **AST depth.** The depth of a subtree is the largest number of nodes marked "counts" in the grammar table on a path down from its root, the root included, computed bottom-up as nodes are created; a macro counts as the call it is written as. Over 250 is `NestingTooDeep`, kind `syntax tree`, at the first counting node created with depth 251, added only if parsing reaches the end of the text: cel-go counts it after parsing. So 250 chained additions, selections or indexes pass, 251 fail.
- **Stack.** Each recursion of the parser goes through a nested sub-expression; before it, `RuntimeHelpers.TryEnsureSufficientExecutionStack()` is called, and `false` is `InsufficientStack` at the current token. Within the limits it occurs only on a thread with a small stack.

`NestingTooDeep` of kind `nesting` and `InsufficientStack` stop parsing.

## Errors and recovery

All have the stage `Parse`.

| Code | Message | Position |
| :- | :- | :- |
| `SyntaxError` (1000) | `syntax error: expected {what}, found {token}`; from the lexer, `syntax error: {detail}` | the token where the grammar stopped |
| `InvalidLiteral` (1001) | `invalid {kind} literal: {detail}` — `int`, `uint`, `double`, `string`, `bytes` | the literal |
| `ExpressionTooLong` (1002) | `expression is longer than 65536 UTF-8 bytes` | the character past the limit |
| `NestingTooDeep` (1003) | `{depthKind} depth exceeds the limit of 250` | see "Limits" |
| `TooManyErrors` (1004) | `more than 100 syntax errors; parsing stopped` | the place of the error it replaces |
| `ReservedIdentifier` (1005) | `reserved identifier '{name}'` | the identifier |
| `InvalidMacroCall` (1006) | `invalid call of macro '{macro}': {detail}` | the `(` of the call; for a macro of an extension set, the node at which cel-go reports ([extension-macros](extension-macros.md#errors-of-the-expansion)) |
| `SyntaxNotEnabled` (1007) | `'.?'`, `'[?'` or `'?' is not enabled: add the extension set optional`; `decimal literal is not enabled: enable decimal literals in the environment builder`, in `net10.0` `…: decimal requires .NET 11` | the `?`; the literal |
| `InsufficientStack` (9000) | `insufficient execution stack to continue parsing` | the current token |

- Only `SyntaxError` breaks the grammar. After it the parser skips tokens, stepping over brackets it meets while skipping, up to the first of these: a `)`, `]` or `}` that closes an open construct — a closer that closes none is skipped; a `,`, when the innermost open construct separates items, which a list, a map, a message and an argument list do; the end of the text. A `,` that stops the skip continues that construct; its closer ends it; a closer of an outer construct ends the inner ones without being read; with nothing open, the rest of the text is skipped. Until the next token is read, the construct that stopped the skip reports no further error at its token.
- An index and parentheses separate no items, so there a `,` is skipped like any other token: `(a, b)` and `a[1, 2]` are one error at the comma, and the skip goes on to the closer. In a construct with items an empty item is a `SyntaxError` of its own: `f(,)` has two errors, at the `,` and at the `)`; the lone comma of an empty list, map or message (see "Primary") is no empty item. The recovery is this library's own and does not reproduce the error recovery of cel-go, which ANTLR generates.
- After the other codes parsing goes on as if the construct were valid.
- An error at the offset of an earlier one is not reported.
- Errors count in the order found; the 101st becomes `TooManyErrors` at its place, and parsing stops.
- The result lists the errors by offset, those at one offset in the order found; the error that stopped parsing comes last. A text with errors gives no tree.

`InternalError` (9001) comes from the compilation boundary around the parser ([facade](facade.md)).

## Printing and tree equality

Both serve the property "parse, print, parse gives the same tree" ([tests](tests.md#properties-of-the-parser-and-the-checker)). They are internal classes of `Syntax`, as `parser/unparser.go` belongs to the cel-go parser, and use the grammar table of the parser. Both recurse without the stack check: they serve tests on trees within the limits.

`Printer.Print(ParsedExpression)` gives a text that parses, with the same options, into an equal tree:

- Binary operators and `?:` with single spaces; `[a, ?b]`, `{k: v}`, `a.b{f: v}`, `.?f`, `[?i]`; a field that is not a `SELECTOR` in backticks.
- Parentheses only where the tree needs them: an operand of a lower level than its place requires; the right operand of a left-associative level at the same level; an operand of `_&&_` or `_||_` that is the same operator — balanced chains are not rebuilt; the operand of `!_` or `-_` that is a numeric constant, a call of `!_` or `-_`, or of a lower level; a numeric constant or a call of an operator below level 8 as the target of a selection, an index or a receiver call.
- A comprehension or a presence test is printed as its recorded macro call; without the record `Print` throws `InvalidOperationException`.
- `int` in decimal; `uint` with `u`; `double` in the shortest round-trip invariant form (for a power of two that .NET writes with digits that read back to the double below, the digits of `DoubleTextRules`), with `.0` appended when it has neither a point nor an exponent; a string in double quotes with `\\`, `\"`, `\n`, `\r`, `\t` and `\u00HH` for the other control characters, as the key text of [evaluation](evaluation.md) errors; bytes as `b"…"`, printable ASCII other than `"` and `\` as it is, other bytes as `\xHH`.

`SyntaxTreeComparer.Difference(ExprNode, ExprNode)` returns `null` for equal trees, otherwise the path to the first difference with both values. Equal trees have the same node classes, children in the same order, names, functions, fields, flags (presence test, optional indices and entries) and comprehension variables, and constants of the same kind and value, doubles by their bits. Ids, positions and macro calls are not compared.
