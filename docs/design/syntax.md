# Syntax: lexer, parser and syntax tree

`Great.Parts.Cel.Syntax` turns the text of an expression into a syntax tree with positions, or into parse errors. The grammar and the lexis are those of the language definition of the pinned cel-spec commit; where the language definition is silent, cel-go, the CEL implementation in Go, is the reference.

The walking skeleton parses the expressions of the cel-spec test files `basic` and `plumbing`: literals of every kind, identifiers, calls of global functions such as `f(17)`, list and map literals, unary `-`, binary `+`, `/` and `||`, and parentheses. Any other construct of the grammar is the error `SyntaxError` until the grammar is complete.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel/Syntax/Lexer.cs`, `Token.cs`, `TokenKind.cs` | tokens |
| `src/Great.Parts.Cel/Syntax/Literals.cs` | values of literal tokens |
| `src/Great.Parts.Cel/Syntax/Parser.cs` | recursive descent |
| `src/Great.Parts.Cel/Syntax/NodeFactory.cs` | creation of nodes, ids, positions |
| `src/Great.Parts.Cel/Syntax/ParsedExpression.cs`, `ExprNode.cs` and one file per node class | the tree |
| `src/Great.Parts.Cel/Syntax/ParseResult.cs` | the result of the internal entry point `CelEnvironment.Parse` (see [facade](facade.md)) |
| `src/Great.Parts.Cel/Diagnostics/LineTable.cs` | line table, shared with the checker, the evaluator and the JSON reader |

## Lexer

The lexer reads the text once, from left to right, and yields tokens: a kind, an offset and a length in UTF-16 code units. It covers the whole lexis, so that only the parser decides which constructs are accepted.

- Whitespace (space, `\t`, `\n`, `\f`, `\r`) and comments from `//` to the end of the line separate tokens.
- Identifiers are `[_a-zA-Z][_a-zA-Z0-9]*`. `true`, `false`, `null` and `in` are keywords. Reserved words such as `if` are identifiers to the lexer; the complete grammar rejects them.
- Numbers: decimal digits, or `0x` and hexadecimal digits, for integers; the suffix `u` or `U` for unsigned integers; a fraction or an exponent for floating literals (`1.5`, `.5`, `1e3`, `2.3e+1`). The sign is not part of the token.
- Strings: an optional `r` or `R` for a raw string, and `b` or `B` in front for bytes; the quotes `"`, `'`, `"""` and `'''`. A single-quoted form must not contain `\r` or `\n`; a triple-quoted form may span lines. Except in raw strings, the lexer steps over escape sequences while it looks for the closing quote.
- A character outside the lexis, or a string without its closing quote, is `SyntaxError` at its position.

## Literal values

Literal tokens become constants of type `CelValue`. The conversion never uses the culture of the process.

| Literal | Value | `InvalidLiteral` when |
| :- | :- | :- |
| integer | `long`, decimal or hexadecimal, with its sign | the value is outside the range of `long` |
| unsigned integer | `ulong` | the value is outside the range of `ulong` |
| floating | `double`, correctly rounded | the value overflows to infinity, as in cel-go |
| string | escape sequences are interpreted, except in a raw string | an unknown escape; an escape that gives a surrogate or a code point above U+10FFFF |
| bytes | the UTF-8 encoding of the text; `\x` and octal escapes are single bytes | as for strings; `\u` and `\U`, as in cel-go |
| `true`, `false`, `null` | `bool`, `null` | — |

Escape sequences: `\\`, `\?`, `\"`, `\'`, `` \` ``, `\a`, `\b`, `\f`, `\n`, `\r`, `\t`, `\v`; `\x` or `\X` with two hexadecimal digits; `\u` with four; `\U` with eight; three octal digits from `000` to `377`. In a string, the numeric escapes denote code points.

Where an operand is expected, a minus followed by an integer or floating literal belongs to the literal, as in the literal grammar of the language definition and in cel-go. So `-9223372036854775808` is the smallest `int`, and `[-1]` is a list of one constant. A minus before any other operand, an unsigned literal included, is a call of `-_`.

## Parser

The parser is a recursive descent over the tokens with one token of lookahead.

- Binary operators are parsed by one loop over a table of precedence levels, from `||` (lowest) through `+` to `/` (highest) in the walking skeleton; operators of one level associate to the left. The complete grammar adds its levels to the same table.
- A chain of `||` becomes a balanced tree of `_||_` calls, the shape the complete parser gives to `&&` and `||` chains.
- A unary `-` applies to one primary; a run of unary operators comes with the complete grammar.
- A primary is a literal; an identifier; an identifier followed by `(`, which is a call of a global function; an expression in parentheses, which adds no node; a list `[a, b]`; or a map `{k: v}`. Lists and maps allow a trailing comma.
- Parsing stops at the first `SyntaxError`; recovery comes with the complete grammar. An invalid literal is reported as `InvalidLiteral` and parsing goes on.

## Syntax tree

The tree follows `syntax.proto` of cel-spec. Nodes are immutable classes. Each node has an id that is unique in its tree, assigned from 1 in the order in which nodes are created. The tree holds no types: types are the result of checking.

| `syntax.proto` | Class | In the walking skeleton |
| :- | :- | :- |
| `ParsedExpr` | `ParsedExpression`: the root node, positions by id, the line table | yes |
| `Expr`, field `id` | `ExprNode`, abstract, property `Id` | yes |
| `Constant` | `ConstantNode`: a `CelValue` of the kind `Null`, `Bool`, `Int`, `UInt`, `Double`, `String` or `Bytes` | yes |
| `Expr.Ident` | `IdentNode`: the name | yes |
| `Expr.Call` | `CallNode`: the function name, the target or `null`, the arguments | global calls and `_+_`, `_/_`, `-_`, `_||_` |
| `Expr.CreateList` | `ListNode`: the elements | yes |
| `Expr.CreateStruct` | `StructNode`: the message name, empty for a map; entries `StructEntry` with an id, a key and a value | maps |
| `Expr.Select` | `SelectNode` | with the complete grammar |
| `Expr.Comprehension` | `ComprehensionNode` | with the macros |
| `SourceInfo.positions` | positions by id in `ParsedExpression` | yes |
| `SourceInfo.line_offsets` | `LineTable` | yes |

The class names differ from the `syntax.proto` messages so that the conformance runner can use both without ambiguity.

Nodes are created only through `NodeFactory`, which assigns ids and records positions; other sources of trees inside the assembly use it too. Its own tests come with the complete grammar.

## Positions

A position is an offset in UTF-16 code units from the start of the text — the index in the .NET string, the unit of `CelSourceLocation`. `syntax.proto` counts code points instead; this is the only difference in the meaning of positions.

| Node | Its offset is the offset of |
| :- | :- |
| literal | its first character, the minus included |
| identifier | its first character |
| call of an operator | the operator |
| call of a global function | the `(` after the name, as in cel-go |
| list, map | `[` or `{` |
| map entry | the `:` |

`LineTable` holds the offset right after each line break, where lines 2, 3 and so on begin. Line breaks are `\n`, `\r\n` and `\r`, as in the language definition. The line of an offset is 1 plus the number of line starts not greater than it (binary search); the column is the offset minus the start of its line, plus 1. An error at the end of the text has the offset equal to the length of the text.

## Errors

| Code | Message | Position |
| :- | :- | :- |
| `SyntaxError` (1000) | `syntax error: {detail}` — what was expected and what was found | the token at which parsing could not continue |
| `InvalidLiteral` (1001) | `invalid {kind} literal: {detail}` — the kind is `int`, `uint`, `double`, `string` or `bytes` | the literal |

Both have the stage `Parse`; the parser reports them in the order in which it finds them.

## Limits, stack and the compilation boundary

None of them is in the walking skeleton; they come with the complete grammar: the length limit, the limits of the two nesting depths, the limit on the number of errors with recovery at `,` `)` `]` `}`, the stack check before each recursive step (`InsufficientStack`) and `InternalError` for an unexpected exception while parsing (see [facade](facade.md)). Until then a deeply nested text can exhaust the stack of the thread, so the parser must not receive untrusted text.
