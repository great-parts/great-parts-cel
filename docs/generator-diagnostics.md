# Source generator diagnostics

The source generator of `Great.Parts.Cel` has two parts. The first reads a type context — a `partial` class derived from `CelTypeContext` with `[CelObject]` attributes — and writes the descriptions of the .NET types it lists and of the types their members reach; `GPCEL001` to `GPCEL006`, `GPCEL008` and `GPCEL009` are its diagnostics. The second reads the calls of the simple entry `CelExpression.Evaluate` and registers the anonymous objects that they pass; `GPCEL010` and `GPCEL011` are its diagnostics. When the generator cannot describe something, it reports a compiler diagnostic. This page lists the ten diagnostics; `GPCEL007`, which excluded a `System.Decimal` member on .NET 10, is not reported any more and its number is not used again. The help link of each diagnostic points to its section here.

How to read the entries:

- **Severity.** An `Error` fails the build. A `Warning` leaves the member out of the CEL type and lets the build go on; with `TreatWarningsAsErrors` it fails the build too.
- **Message.** The text of the diagnostic. `{0}`, `{1}` … stand for the values that the generator fills in: the names of types and members as C# writes them, and the reasons below.
- **Where.** The place that the diagnostic points at: the argument of an attribute, a member, the declaration of the context, the data argument of a call or a property of an anonymous object. The place of an argument is the whole attribute argument as written (`typeof(T)` or `Name = "x"`); the place of a member is its name; the place of a context is the name of its class.

A diagnostic that leaves a type or a member out does not stop the generator from describing the rest of the context. The generator writes no code for a context that has `GPCEL006`, `GPCEL008` or `GPCEL009`; the build then also reports that `Default` does not exist wherever the application uses the context, and those errors follow from the error of the generator. The field names that the generator gives and the types it reads are on the page [Type mapping](type-mapping.md).

## GPCEL001

**Type cannot be used by CEL expressions.** Severity: Error.

Message: `Type '{0}' cannot be used in a CEL type context: {1}`. Where: the argument of `[CelObject]`.

A type listed in `[CelObject]` is of a kind that the generator cannot describe. `{1}` is the reason:

| The type | Reason |
| :- | :- |
| a `ref struct` | `ref structs cannot be stored in a CEL value` |
| an open generic type, a type parameter | `open generic types and type parameters cannot be described without reflection; list a closed type` |
| a pointer | `pointers are not data` |
| a delegate | `delegates are not data` |
| `System.Type` | `System.Type is not data` |
| `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` | `tasks are not data` |
| `Stream` and the types derived from it | `streams are not data` |
| `IEnumerable<T>`, `IAsyncEnumerable<T>` | `<type> has no size and no index; use an array, a list or a collection with a size` |
| a non-generic collection (`ArrayList`, `Hashtable`) | `non-generic collections have no element type; use a typed collection` |
| `char` | `char is a UTF-16 code unit, not a code point; use string` |
| `Int128`, `UInt128`, `BigInteger` | `CEL has no integers wider than 64 bits; use long or string` |
| `DateOnly`, `TimeOnly` | `<type> is not supported; use DateTimeOffset or string` |
| `JsonDocument` | `JsonDocument is released by the application; use JsonElement` |
| `JsonNode` and the other types of `System.Text.Json.Nodes` | `JsonNode is mutable; use JsonElement` |
| another class, structure or interface of `System` or of the assemblies `Great.Parts.Cel` and `Great.Parts.Cel.Protobuf` that the type mapping has no row for, such as `Uri`, `Version`, `IPAddress` | `<type> has no CEL type; use a string or your own type` |
| a type of `Google.Protobuf.WellKnownTypes` | `well-known protobuf types are read only as fields of messages` |
| the interface `IMessage` or `IMessage<T>` of protobuf | `list the message class, not the interface` |
| a message class of protobuf | `protobuf messages are described by Great.Parts.Cel.Protobuf, not by a type context` |
| a type marked `[Obsolete]`, `[Experimental]`, `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` | `<type> is marked [<attribute>]` |
| a collection | `<type> is not an object type; list the element type` |
| a type that has its own CEL type, such as `int` or `string` | `<type> has its own CEL type` |
| `Nullable<T>` | `list the underlying type` |

What to do: list a type of your own that holds the data, a closed generic type, or the element type of a collection. A type that the compiler cannot find is the compiler's error, not this one.

## GPCEL002

**Member excluded from CEL type.** Severity: Warning.

Message: `Member '{0}' of type '{1}' is excluded from the CEL type: {2}`. Where: the member; for a member of a type from another assembly, which has no place in your source, the argument of the `[CelObject]` attribute that reached the type.

A property or a field of a described type is left out, because its type is one that GPCEL001 lists, or because it is marked `[Obsolete]`, `[Experimental]`, `[RequiresUnreferencedCode]` or `[RequiresDynamicCode]` (the mark may stand on the property or on its `get` accessor; the generated code would call it and the compiler of the application would warn). `{2}` is the reason: the one of the table above, or `the member is marked [<attribute>]`. For an element, a key or a value of a collection the reason names the part, for example `the element type System.IO.Stream cannot be read: streams are not data`; a dictionary key that is not `string`, an integer, `bool`, an enumeration or `Guid` is `dictionary keys must be string, an integer, bool, an enumeration or Guid`, and the elements of a set that CEL does not order are `set elements must be numbers, bool, string, bytes, timestamp, duration or decimal, which CEL orders`. An element, a key or a value, at any depth, that is a set, a collection without an index (`Queue<T>`, `Stack<T>`, `LinkedList<T>`) or a structure that is a collection other than `ImmutableArray<T>` has the reason `the element type {type} cannot be read: {type} is copied at each read of an element, which the cost does not charge; use a list, an array or a dictionary` (`the value type …` for a value): the copy would be made at each read of an element, where the limit of cost has no charge.

Selecting the member in an expression is the type checking error `FieldNotAccessible` with the reason. The warning is not given for a member that is hidden on purpose: marked `[CelIgnore]`, listed in `IgnoreMembers` of the `[CelObject]` attribute of its type, or marked `[JsonIgnore]` with the condition `Always` (unless the context sets `IgnoreJsonAttributes`). Selecting such a member is `NoSuchField`.

What to do: give the member a type that the type mapping reads, or hide it.

## GPCEL003

**Duplicate CEL field name.** Severity: Error.

Message: `Members '{0}' and '{1}' of type '{2}' both map to the CEL field name '{3}'`. Where: the second member.

Two members of one type get the same field name after `[CelName]`, `[JsonPropertyName]` and the naming policy. The members are taken in the order of their C# names, ordinal; the second one is reported and left out.

What to do: give one of them another name with `[CelName]`, or hide it.

## GPCEL004

**Duplicate CEL type name.** Severity: Error.

Message: `Types '{0}' and '{1}' in type context '{2}' both map to the CEL type name '{3}'`. Where: the argument of the `[CelObject]` attribute of the second type; for a type that a member reaches, the argument of the attribute of the root that reached it first.

Two types of one context get the same CEL name: they have the same default name, such as `Acme.Box<Acme.A_B>` and the class `Acme.Box_Acme_A_B`, or `Name` gives one name to both, or one type is listed twice with different names. The type with the greater display name (ordinal) is the second; it is left out. The order of the attributes in the text does not decide.

What to do: set `Name` on one of the types.

## GPCEL005

**Invalid CEL name.** Severity: Error.

Message: `'{0}' is not a valid CEL {1}`. Where: the argument of the attribute for a type, the member for a field.

The name is not one that an expression can write. `{1}` is `qualified type name` for a type: words of letters, digits and underscores separated by dots, none a keyword of CEL (`true`, `false`, `null`, `in`) and the first not a reserved word. It is `identifier` for a field: letters, digits and underscores, not starting with a digit. The name comes from `Name` of `[CelObject]`, from `[CelName]`, from `[JsonPropertyName]`, from the default name of a type or from the name of the member itself when it has letters outside ASCII and the naming policy does not change them. A member whose type has a default name that is not a CEL name is left out with this error too.

What to do: set a valid `Name` or `[CelName]`.

## GPCEL006

**Invalid CEL type context.** Severity: Error.

Message: `Type context '{0}' must be a non-generic partial class that derives from CelTypeContext and is not nested in a generic type`. Where: the name of the class.

A class with `[CelObject]` is not `partial`, is generic, is nested in a generic type, or does not derive from `CelTypeContext`. The generator reads nothing else of it and writes no code.

What to do: declare the class `partial`, non-generic, derived from `CelTypeContext`. A containing type has to be `partial` too; the compiler reports it when it is not.

## GPCEL008

**C# language version not supported.** Severity: Error.

Message: `The code generated for type context '{0}' requires C# 12 or later; the project uses C# {1}`. Where: the name of the class.

The language version of the project is below C# 12. `default` and `latest` are the versions the compiler resolves them to. The generator writes no code for the context.

The generator of the calls of `CelExpression.Evaluate` reports it too: its files declare file-local classes, so under C# 11 and earlier it writes none, and `{0}` is `CelExpression.Evaluate`. It is reported once for the compilation, at the first call that passes an anonymous object. Without the files the call fails at run time for an anonymous type that is not registered.

What to do: set `LangVersion` to 12 or later.

## GPCEL009

**CEL type context too large.** Severity: Error.

Message: `The type closure of type context '{0}' contains more than {1} types`. Where: the name of the class.

The types that the roots list and the types their members reach are more than the limit, 250 object types for a context (enumerations do not count). A context that pulled in a graph of entities by accident is the usual cause. The generator writes no code for the context.

What to do: split the context into several, or hide the members that lead into the rest of the graph.

## GPCEL010

**CEL simple entry cannot see the data type.** Severity: Warning.

Message: `The static type '{0}' of the data argument of CelExpression.Evaluate is not an anonymous type or a supported dictionary; the call fails at run time unless the value is one of them: {1}`. Where: the `data` argument of the call.

The generator reads the static type of the expression that a call of `CelExpression.Evaluate<T>(string, object, …)` passes as `data`, not the type of the parameter, which is always `object`. It writes the registration of an anonymous type and says nothing about a dictionary that the simple entry reads (`IReadOnlyDictionary<string, X>`, `Dictionary<string, X>` or a type that implements one, with `X` among the types of the elements on the page [Type mapping](type-mapping.md#data-of-the-simple-entry)). For any other static type it warns: a type parameter, `object`, a class or a structure of the application, a dictionary outside that list.

At run time such a call is a `CelException` with the error `TypeNotDeclared` and the full name of the .NET type, unless the value is a supported dictionary or an anonymous object of a registered type. Anonymous types belong to one assembly, and the registry is keyed by the type, not by the call: a call through a generic method or an `object` works when an anonymous object of the same property names, types and order is passed directly to `CelExpression.Evaluate` elsewhere in the same assembly, so the warning can be a false alarm there. The call of such a wrapper is the case where an application may suppress the warning, with `#pragma warning disable GPCEL010` at the call.

`{1}` is the advice: `pass the anonymous object at the call or pass a dictionary` for a type parameter and `object`; `pass a dictionary with string keys and values of the list, or an anonymous object` for a dictionary outside the list; `build an environment with a type source and UseInput<T>` for a class or a structure.

What to do: pass the anonymous object in the call itself, pass a dictionary, or build an environment with `UseInput<T>`.

## GPCEL011

**Property type not supported by the CEL simple entry.** Severity: Error.

Message: `Property '{0}' of the anonymous data has type '{1}', which the CEL simple entry cannot read: {2}`. Where: the property in the creation expression of the anonymous object.

A call of `CelExpression.Evaluate` passes an anonymous object with a property that the simple entry cannot declare. `{2}` is the reason, followed by `; build an environment with a type source`, or by `; rename the property` for a name that is not a CEL identifier, which a type source does not correct:

| The property | Reason |
| :- | :- |
| a class or a structure of the application | `an object of the application` |
| an anonymous object | `a nested anonymous object` |
| `object` | `object` |
| a type that is private, protected or file-local, alone or inside a collection | `not accessible from the created file` |
| a collection outside the list: `IList<X>`, a set, a dictionary with a key that is not `string`, an element outside the list | `a collection outside the list` |
| `float`, `char`, `decimal`, an enumeration or another type outside the list | `a type outside the list` |
| a type of a generic method | `a type parameter of the enclosing method` |
| a name that is a reserved word of CEL (`for`, `in`, `null`) or has letters outside ASCII | `not a CEL identifier` |

The generator writes no registration for that anonymous type. Without the error the call would fail at run time, for a name that is not an identifier with `InvalidVariableName`.

What to do: pass the data as a dictionary, rename the property, or build an environment with a type source and `UseInput<T>`, which describes objects of the application.
