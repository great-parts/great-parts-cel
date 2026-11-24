# The public syntax tree, the expression factory and compilation of a tree

The tree of a parsed expression is public: `CelParsedExpr` and the node classes of `Great.Parts.Cel.Syntax`, built by parsing, by `CelExprFactory` or by loading protobuf ([ast-protobuf](ast-protobuf.md)). `CelEnvironment.CompileParsed<T>` and `ValidateParsed` type-check and plan a tree from any of them, and `CelSyntax.Format` prints it. Members and documentation are in the XML documentation of the public API.

## One representation

The internal tree is the public one: there is no wrapper and no copy between them. The classes of [syntax](syntax.md) are public and sealed, with internal constructors, so only the parser, the factory and the loader create nodes:

| Class | Notes |
| :- | :- |
| `CelExprNode` (abstract), `CelExprKind` | `Kind` is set by each class; `Children` is computed on the first read and kept; `DescendantsAndSelf()` walks in pre-order with an explicit stack. The numbers of `CelExprKind` are fixed |
| `CelConstantNode`, `CelIdentNode`, `CelSelectNode`, `CelCallNode`, `CelListNode`, `CelComprehensionNode` | the members of the internal classes before the change |
| `CelMapNode` with `CelMapEntryNode`; `CelStructNode` with `CelStructFieldNode` | the two forms of `CreateStruct` of `syntax.proto`: a map has keys, a message has a type name and field names; the checker and the planner switch on the two classes |
| `CelMacroCall` | a macro call as written. `Function` is the name the text writes; for a macro of a namespace it is the qualified name (`cel.bind`, `math.greatest`) and `Target` is `null`: the identifier of the namespace is not a node of the tree. A call that an expansion records for its own part, such as the `map` inside `sortBy`, stays internal (`IsWritten` is false) and `GetMacroCall` does not return it |
| `CelParsedExpr` | the root, `SourceName`, `GetSpan`, `GetMacroCall`, `FindNode`, `FindParent`, `ToProtobuf`, `FromProtobuf`, `FromCheckedExprProtobuf` |
| `CelParseResult` | `ParsedExpr` and `GetParsedExprOrThrow()`; reading the expression of an unsuccessful result throws `CelException` |

The lexer, the parser, `MacroTable`, `Printer`, `SyntaxTreeComparer`, `LineTable` and `NodeFactory` stay internal, and so do `Checking/CheckedExpression` and `Reference`: `CelCheckedExpr` wraps a `CheckedExpression` and builds its public maps on the first read ([ast-protobuf](ast-protobuf.md#checked-expression)).

Nothing on the path of compilation creates what only readers need. The planner reads the keys and values of a literal through internal members, not through `Children`, and `Children` and the index of `FindNode` and `FindParent` are not created by `Compile<T>`; a test checks it. The index is built on the first call and published with `Volatile.Write`, as the ends of nodes in `LineTable` are.

## Ids and spans

Ids are positive and unique in the tree; their values are not a contract. `GetSpan(id)` gives the start (with line and column) and the length of a node or an entry, as an error at that node reports its fragment. It throws `ArgumentOutOfRangeException` for an id outside the range of ids that the creator of the tree gave; an id inside the range that is not in the tree — an argument that a macro dropped, a node of a factory that did not reach `Build` — gives the position recorded for it, or `default(CelSourceSpan)`.

## Parsing

`CelEnvironment.Parse(text, options)` is the parse step of `Compile<T>` made public: the internal `Parse(string, SyntaxOptions?)` stays as the step of the compilation boundary, and the public member wraps its result with the source name and the mark of the syntax the tree was parsed with. `options.SourceLocation` gives the source name and the document positions of parse errors, as in compilation; the expected type is not read.

A parser of another language returns the same result type: `CelParseResult.FromParsedExpr(tree)` is a success with the same tree, and `FromErrors(errors)` is a failure with the errors in order; `GetParsedExprOrThrow()` then throws a `CelException` that carries all of them. Both wrap the internal constructors. `FromErrors` copies the sequence once into an `ImmutableArray` and rejects an empty sequence (`ArgumentException`) and a `null` element or sequence (`ArgumentNullException`). `CelCompileResult<T>.FromErrors` does the same for a compile result, with no program.

## Positions

The parser records the offset and the end of each node and entry. A factory with a source text records the range of `Position` for each node and entry created while it is set: offset `range.Start`, end `range.End`, resolved against the length of the text. A node without a position has the offset −1 in the table; `GetSpan` gives `default(CelSourceSpan)` for it and an error at it has no place. The line table of a factory is built from its source text and is empty without one. To write positions in code points ([ast-protobuf](ast-protobuf.md)), the line table also keeps the offsets of the surrogate pairs of the text, found in the same pass that finds the line breaks.

## The factory

`CelExprFactory` wraps the internal `NodeFactory` of the parser: each method checks its arguments (`ArgumentNullException`, `ArgumentException`), sets the offset of the node from `Position` and calls the method of `NodeFactory`. To know its own nodes the factory keeps the list of the nodes and entries it created, the one with the id i + 1 at the index i: a node belongs to it when its id is in the range of the list and the object at that index is the same one. The parser's `NodeFactory` has no such list.

- `Constant` accepts the kinds of a literal: `Null`, `Bool`, `Int`, `UInt`, `Double`, `String`, `Bytes`, `Decimal`.
- `Ident`, `Call`, `MemberCall`, `Select`, `PresenceTest`, `Struct`, `StructField`: non-empty strings; no grammar check — names are resolved by type checking, and internal `@` names are allowed.
- `List`: each optional index once, in range; stored ascending.
- `ExpandMacro(name, target, arguments)` looks the macro up by qualified name, argument count and style in the table of all macros of the library (`MacroTable.All`: the standard ones and those of every extension set) and runs its expander with the factory, as the parser does after parsing a call; the factory records the macro call of the expansion. An expander that reports `InvalidMacroCall` makes the factory throw `ArgumentException` with that message. The nodes of the expansion get the current `Position`.
- `Copy(source, node, replace, mapOffset)` walks the subtree with an explicit stack. For each node it first asks `replace`; otherwise it creates the children, then the copy, and copies the macro call recorded for the node with its target and arguments mapped to the copies; a target or argument that is not a node of the subtree (the `m.f` of `has(m.f)`) is copied on its own, with `replace` asked for its nodes too. The offset and the end of the copy are those of the source node, passed through `mapOffset` when given, and only if the factory has a source text. A result of `mapOffset` outside the text of the factory is `ArgumentException`; a node without a position in the source stays without one.
- `CreateError(code, message, subject, expectedType, actualType)` makes an error at `Position` of the source text of the factory, for a parser of another language ([filtering](filtering.md)). The offset, line, column and length come from the same line table as the node positions, so `ToDisplayString(text)` and the places of type-checking errors agree. The stage comes from the range of the code (below 2000 `Parse`, 2000s `Check`, 3000s `Evaluate`, then `Build`); a code of 9000–9999, a value that is not a member of `CelErrorCode`, and an empty message are `ArgumentException`. Without a `Position`, or with a factory without a text, the error has no place.
- `Build(root)` walks the subtree with an explicit stack, checks that every node and entry belongs to the factory and occurs once (a bit per id), and creates the `CelParsedExpr` with the position table, the macro calls of the reachable nodes and the line table.

## Compilation of a tree

`CompileParsed<T>(tree, options)` and `ValidateParsed` run the compilation of `Compile<T>` from the step after parsing. Before the check, one pass over the tree (explicit stack, `TreeInput`) checks the input:

| Check | Error, stage `Parse` |
| :- | :- |
| depth greater than 250 (the limit of parsing) | `NestingTooDeep` at the first node beyond it |
| a constant of the kind `Decimal` and the environment without the decimal literal option | `SyntaxNotEnabled` at the node, the text of the literal error |
| a call `_?._` or `_[?_]`, an optional element, entry or field, and the environment without the `optional` set | `SyntaxNotEnabled` at the node |

A tree from the parser of the same environment passes these checks by construction, so the pass is skipped for it. Then type checking, the lists of referenced names and planning run as for a text. The errors take their places from the position table of the tree, and `CelCompileOptions.SourceLocation` adds the document position as for a text.

No path evaluates a tree without type checking: the internal entry point of the conformance runner stays internal.

## Printing

`CelSyntax.Format(tree)` calls the internal `Printer`. Each recursive step checks `RuntimeHelpers.TryEnsureSufficientExecutionStack()` and the depth (250), and a tree without a text form — a comprehension or a presence test without a recorded call, deeper than the limit, or without stack — is `ArgumentException`. Any expansion with a recorded macro call is printed as that call, `math.greatest(1, 2)` and not the expansion `math.@max([1, 2])`, so that the text parses back into an equal tree; a tree built by the factory without `ExpandMacro` is printed as it is. Names are printed as they are, `@` names included.

## Tests

The tree, the factory and the printing are covered by the examples in `tests/Great.Parts.Cel.Tests/Examples/Syntax/` and `Examples/Facade/CompileParsedTests.cs`, and by these properties:

- print and parse again gives an equal tree for every expression of the cel-spec suite;
- for every case of the cel-spec suite that is type checked, the compilation of the text, of the tree from `Parse`, of the tree loaded from protobuf and of the tree loaded from a checked expression give the same errors (stage, code and place) and the same result; the cost of a loaded `sortBy` may be higher, see [ast-protobuf](ast-protobuf.md#reading);
- nothing on the path of `Compile<T>` creates `Children` or the index of nodes.
