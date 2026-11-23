# The checked expression and the protobuf form of parsed and checked expressions

`CelCheckedExpr` and `CelReference` (`Great.Parts.Cel.Checking`), `CelValidationResult.CheckedExpr`, and the reading and writing of the messages `cel.expr.ParsedExpr` and `cel.expr.CheckedExpr` of cel-spec without a protobuf package. Members and documentation are in the XML documentation of the public API.

## Checked expression

`CelCheckedExpr` holds the internal `CheckedExpression` of a successful check and the `CelParsedExpr` it checked. Validation creates it; a program does not keep it. The maps are built on the first read, once, and published with `Volatile.Write`:

- `TypeMap`: the internal type map as it is, `dyn` included. It has every node of the parsed tree except those that the checker does not visit, which are the nodes cel-go removes by rewriting: below the node of a chain that names a declaration (`Checker.VisitChain` records the resolved node and the selections after it) and the target of a call of a qualified function name (`VisitCall` checks only the arguments). Nothing is added for them.
- `ReferenceMap`: from the internal references by id —

| Internal `Reference` | `CelReference` |
| :- | :- |
| `Variable` | `Name` — the full name of the declaration, with a leading dot when the flag below is set |
| `TypeValue` | `Name` — the CEL name of the type, with a leading dot when the flag below is set |
| `Local` | `Name` — the name of the comprehension variable |
| `Constant` (enumeration) | `Name` — the full name of the constant, with a leading dot when the flag below is set; `Value` — `int` |
| `Candidates` | `Name` — the resolved name of the function (the full name for a call of a qualified name or a name resolved in the container; cel-go leaves it empty), `OverloadIds` — the ids of the candidates in the order of the declaration index |
| `ToMessage` | `Name` — the full CEL name of the type |
| `ToField` (selection, entry of a message literal) | no reference |

The optional selection `_?._` gets `Name = "_?._"` and `OverloadIds = ["select_optional_field"]`.

`Reference.ToConstant` holds the full name of the constant besides its value (the candidate that `TryResolveName` matched). A reference of a name resolved from a written leading dot records whether a comprehension variable of the first part of the name is in scope at the node (`TryGetLocal` of the first part in `VisitIdentifier` and `VisitChain`); this is `requiresDisambiguation` of cel-go, which keeps the dot in the written name and in the name of the reference. The dot belongs in both: the planner of cel-go plans an identifier that has a reference by the name of the reference (`planner.go`, `planIdent` → `planCheckedIdent`: `AbsoluteAttribute(id, identRef.Name)`), and only `AbsoluteAttribute` reads the dot as "the global variable, not the variable of the comprehension" (`attributes.go`, `disambiguateNames`); without it `[5].exists(x, .x > 0)` with a global `x = 0` is planned as the local `x`. So `ReferenceMap` builds `CelReference.Name` with the dot for a reference with the flag, and `ToProtobuf` writes the name of the reference and the name of the identifier from the same string.

## The form of `expr` in `CheckedExpr`

`CelCheckedExpr.ParsedExpr` is the parsed tree, not rewritten. `CheckedExprCodec` writes `expr` in the form that `checker.Check` of cel-go gives (`checkIdent`, `checkSelect`, `checkCall`, `checkCreateStruct` call `SetKindCase`), in one pass over the parsed tree with the internal references:

| Node of the parsed tree | Written as |
| :- | :- |
| `CelIdentNode` or `CelSelectNode` with a reference `Variable`, `TypeValue` or `Constant` | `Expr{id, ident_expr: {name}}` — the full name (the `Name` of the reference); a leading dot only when the name was written with a dot and the flag above is set — in `ident_expr.name` and in `reference_map[id].name` alike; the operand subtree is not written |
| `CelIdentNode` with `Local` | as is |
| `CelCallNode` with a target whose candidates are global overloads (a qualified function name, such as `math.abs(x)`) | `Expr{id, call_expr: {function: <full name>, args}}`, without the target; the target subtree is not written |
| global `CelCallNode` | `call_expr.function` — the full name the container resolved, without a leading dot |
| member `CelCallNode` of a member overload (`s.startsWith("a")`) | as is |
| `CelStructNode` | `message_name` — the full name of the type |
| any other node | as `ParsedExprCodec` writes it |

`positions` has the ids of the written `expr` and of the macro calls (cel-go `ClearUnusedIDs`). `macro_calls` are written as for a parsed expression, from the parsed tree, without rewriting. cel-go does it differently: `buildMacroCallArg` copies only the `Call` and `List` nodes among the arguments of a macro call, and every other node (identifier, selection, literal, map, message, comprehension) is the same object as in the tree, so that `SetKindCase` of the checker rewrites it in `macro_calls` too — `items.all(v, v > limit)` in the container `acme` has `acme.limit` in the `macro_calls` of cel-go and `limit` in ours, and its `positions` lack the ids of the removed nodes of a chain, which ours has. It is a difference in the form of the data, not in the plan or the result: the planner of cel-go does not read `macro_calls`. `type_map` and `reference_map` have the ids of `TypeMap` and `ReferenceMap`, all of which are nodes of the written `expr`; the `name` of the reference of a call is empty and `overload_id` lists the candidates, as cel-go `NewFunctionReference` writes them.

Loaded back (`FromCheckedExprProtobuf`), the rewritten tree compiles as the text: `VisitIdentifier` resolves a name with dots through the candidates of the container, as `VisitChain` resolves the written chain, and `VisitGlobalCall` resolves a function name with dots the same way. The one case where it differs is a full name that the container resolves to another declaration, `acme.acme.limit` beside `acme.limit` in the container `acme`: the tree is checked again in the environment of the call, as it would be in cel-go. It has a second form: a name written without the leading dot that the text had — `.y` in the container `com.example` with `y` and `com.example.y` declared: the checking writes `y`, as cel-go does, and the check again resolves it to `com.example.y`.

## The codec

Internal, without reflection and without packages; the field numbers are those of cel-expr v0.25.1, and the same bytes are the messages of `google.api.expr.v1alpha1`. It lives in two subdirectories with the namespace of their layer, `Great.Parts.Cel.Syntax` and `Great.Parts.Cel.Checking`: a namespace per subdirectory would make a cycle with the layer (`ToProtobuf` calls the codec, the codec creates nodes), and the layer table gets no row for it.

| File | Content |
| :- | :- |
| `Syntax/Protobuf/ProtoWriter.cs`, `ProtoReader.cs` | varint, fixed64 for `double`, length-delimited fields; the reader stops at the first fault |
| `Syntax/Protobuf/ExprWriter.cs`, `ExprForm.cs`, `ParsedExprCodec.cs` | writing `ParsedExpr` and the `expr` and `SourceInfo` of both messages, in the parsed form or in the checked form |
| `Syntax/Protobuf/ExprReader.cs`, `SourceInfoReader.cs`, `ExprData.cs` | reading `Expr` and `SourceInfo` into data |
| `Syntax/Protobuf/TreeLoader.cs`, `ProtoErrors.cs` | the rules of the message and the tree, the nodes through `NodeFactory`, the macro calls |
| `Syntax/Protobuf/CheckedMessageValidator.cs` | the wire check of `reference_map` and `type_map` of a loaded `CheckedExpr` |
| `Checking/Protobuf/CheckedExprCodec.cs`, `TypeWriter.cs` | writing `CheckedExpr`: the maps, `Type`, the checked form of `expr` |

### Writing

The writer runs the same code three times over one array: two runs only measure and record the length of each nested message, the third fills the array, so `ToProtobuf` allocates the result array and one array of sizes. Field order is the order of field numbers and proto3 defaults outside a oneof are left out, as protobuf writers do, so the bytes equal what `Google.Protobuf` writes for the same message; maps are written in ascending order of keys. A tree deeper than the stack allows is `InvalidOperationException` of `ToProtobuf`; there is no limit of 250 on writing, and a message deeper than 250 reads back as `NestingTooDeep`.

| Element | Written as |
| :- | :- |
| node | `Expr{id, <kind>}`; ids as in the tree; in `CheckedExpr` the rewritten form above |
| `CelConstantNode` | `Constant` of its kind; `Decimal` — `Expr{id, call_expr: {function: "decimal", args: [Expr{arg id, const_expr: {string_value}}]}}` with the text of `string()`: the call has the id of the constant; the argument has the id `max id of the tree + k`, k = 1, 2, … in the pre-order of the `decimal` constants (so that the copy in `macro_calls` gets the same id); both positions are the start of the constant. In `CheckedExpr`: `type_map[call] = abstract decimal`, `type_map[arg] = string`, `reference_map[call] = {overload_id: ["string_to_decimal"]}` |
| `CelMapNode` / `CelStructNode` | `CreateStruct` with `map_key` / `field_key`, `optional_entry` |
| positions | `positions[id]` = start offset in code points, for every node and entry with a position |
| `line_offsets` | the start of each line after the first, in code points, then the length of the text in code points plus 1, as cel-go `runes.NewBufferAndLineOffsets`; the line breaks are those of the language definition (`\n`, `\r\n`, `\r`), as in `LineTable`, while cel-go counts `\n` only, so the offsets differ for a text with a lone `\r` |
| `location` | `SourceName` |
| macro calls | `macro_calls[id of the expansion]` = the call: a global call `Expr{id: 0, call_expr}`, a member call with its target; an argument or target that is itself an expansion — `Expr{id}` without a kind; other arguments — copies with the ids of the tree. A macro of a namespace is written as a global call of the qualified name, `cel.bind`; cel-go writes a member call with the target `cel` |
| `type_map` | `Type` by kind: primitives, wrappers, well-known types, `list_type`, `map_type`, `message_type`, `type_param`, `type`, `null`, `dyn`; `optional_type(T)`, `decimal` and the abstract types of the extension sets — `abstract_type{name, parameter_types}` |
| `reference_map` | `Reference{name, overload_id, value}`; `value` — `Constant{int64_value}` |

Offsets in UTF-16 become code points with the offsets of the surrogate pairs of the line table: the code-point offset is the UTF-16 offset minus the number of pairs before it (binary search). The inner `map` that the expansion of `sortBy` records for its own part is not written.

### Reading

One reader over the span, with a depth counter for nested messages (`Expr` inside `Expr`): depth over 250 stops with `NestingTooDeep`; the reader does not recurse on the .NET stack beyond that counter. Wire errors — a truncated varint, a length beyond the end, a wire type that does not match a known field, a string that is not UTF-8 — stop with `InvalidProtobuf` and the byte offset in the message. Unknown fields are skipped by wire type. The loader builds the nodes through `NodeFactory`, so the ids are renumbered from 1; a dictionary maps the ids of the message to the new nodes for the positions and the macro calls. Rules checked after reading are `InvalidSyntaxTree` with the id of the message: a node without a kind, an empty name, function, field or type name, an id used twice, an optional index outside the list or repeated, a map entry with `field_key` or a message field with `map_key`, a comprehension without `iter_var` or `accu_var`, a macro call recorded under an id that is not a node of the tree, an argument or target of a macro call without a kind whose id is not an expansion with a macro call of its own, a position outside `sourceText`.

Macro calls: the target and the arguments of a recorded call are copies in the message, and one rule builds them, applied to each copy node from the top down: a copy node whose id is an id of a node of the tree is that node (the same object, so `Copy` with `replace` changes the call too and the memory is not doubled); any other copy node is a new node built from the copy, with a new id, and its children by the same rule. So `has(m.f)` has the argument `m.f` that the tree does not contain — a new selection over the node `m` of the tree; the argument `math.abs(v) > 0` of an `all` in a rewritten `CheckedExpr` has the id of the tree node `_>_`, so it is that node, with the global call `math.abs`, and prints the same; its copy of the target `math`, which the tree no longer has, is not built, because the node `_>_` that holds it is taken whole. The new nodes are not found by `FindNode` and `FindParent`, as `m.f` of a parsed `has(m.f)` is not. An `Expr` without a kind is the expansion it names: the node of the tree with that id, which must have a macro call of its own. A recorded call with a target that is the identifier of a macro namespace and a function that completes a qualified macro name (`cel` and `bind`, the form of cel-go) is loaded as the call of the qualified name with no target, as the parser records it. A call `decimal` with one string constant stays a call: loading does not restore a `decimal` constant, because the constant needs the literal option of the environment and the call does not. A loaded tree has no record for the inner `map` of `sortBy` (cel-go writes none), so the loop that collects the keys is charged as an ordinary comprehension: the cost of a loaded `sortBy` is not lower than that of the text, and the value and the errors are the same.

Positions in code points become UTF-16 offsets through `sourceText` when given (a pass over the text counts code points); without it they are taken as UTF-16 offsets. The end of a loaded node is its start. `syntax_version`, `expr_version` and `extensions` are read and not used. `FromCheckedExprProtobuf` reads `expr` and `source_info` and reads `reference_map` and `type_map` through the same reader without building anything, so that their wire errors are `InvalidProtobuf`.

The deprecated `Constant.duration_value` and `timestamp_value` become constants of the kinds `Duration` and `Timestamp`; the checker types them as `duration` and `timestamp`, and `Format` prints them as `duration("…")` and `timestamp("…")`.

## Exchange with cel-go

The oracle `eng/oracle` has a request `checked` ([eng-oracle](eng-oracle.md#checked-expressions)): for an expression and a manifest it returns the bytes of `cel.AstToCheckedExpr` of its check, and for the bytes of a `CheckedExpr` it plans `cel.CheckedExprToAst` in the environment of the manifest and evaluates on the bindings. `eng/difftest` adds two comparisons per case on which the sides agree and that compiles on both — the result of cel-go from our bytes against the result of cel-go from the text, and our result from the bytes of cel-go against our result from the text — with signatures of their own ([eng-difftest](eng-difftest.md#exchange-of-checked-expressions)). The corpus has the extension sets `math`, `strings`, `encoders`, `sets`, `lists` and containers, whose qualified function names and resolved names are the rewritten form; cel-go plans a call with a reference by the function name and the target of `expr` (`interpreter/planner.go`, `resolveFunction`, `planCall`), so a form without rewriting would fail there.
