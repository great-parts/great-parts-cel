# Object literals of .NET types and of types described at run time

From version 2.0 an expression can create an instance of an application type with the syntax of a message literal, `acme.Money{amount: 10.5m}`, when the application allows it for that type: `AllowObjectLiterals = true` on `[CelObject]`, or `AllowObjectLiterals(…)` on a builder. Protobuf messages always allowed it. The rules a user sees are on the page `docs/type-mapping.md` ("Object literals"); this page is how the generator, type checking and evaluation implement them.

## The contract of a description

`CelObjectType` has four members for literals; type checking and evaluation use nothing else:

| Member | Default | Generated code of a type with literals |
| :- | :- | :- |
| `IsConstructible` | `false` | `true` |
| `CanSetField(int)` | `true` | `false` for a field without a public `set` or `init` and not a parameter of the chosen constructor, and for a member whose .NET type the code cannot create from a CEL value |
| `IsRequiredField(int)` | `false` | `true` for a `required` member and a constructor parameter without a default value |
| `CreateInstance(numbers, values)` | `InvalidOperationException` | the instance, below |

`MessageType` of the protobuf package keeps its overrides; it does not override the two new members. A type of the generator without `AllowObjectLiterals` overrides none of the four.

## The generator

`[CelObject(typeof(T), AllowObjectLiterals = true)]` is read with the other named arguments of the attribute into `RootInfo` and then into the model of the type (a `bool` and the place of the argument, equatable, no symbols; two attributes for one type are joined by "or"). A type reached only through members never gets literals. `Reading/LiteralReader.cs` picks the constructor:

1. a public parameterless constructor, or the implicit one of a structure;
2. otherwise the only public constructor whose every parameter matches a field by name, ignoring case and with the type of the field (a positional record);
3. otherwise `GPCEL012` on the argument of the attribute, with the reason: an interface, an abstract class, no public constructor, several constructors with parameters, a parameter without a field, a parameter of another type than the field, or a `required` member that a literal cannot set. The type stays described, without literals.

A member is settable when it has a public `set` or `init` accessor or is a public field that is not `readonly`, and its .NET type has a creation row (`Reading/CreateShapes.cs`): the scalars of the type mapping, `string`, `byte[]`, the time types, `decimal`, `DateOnly`, `TimeOnly`, `char`, the integers wider than 64 bits, `Guid`, `CelValue`, `CelList`, `CelMap`, an object type of the closure (an instance whose .NET type casts to the member type), and `T[]`, `List<T>`, `IReadOnlyList<T>`, `IList<T>`, `ImmutableArray<T>`, `Dictionary<K, V>`, `IReadOnlyDictionary<K, V>`, `IDictionary<K, V>` of those, one level deep. Enumerations, `object`, `JsonElement`, `Decimal128`, sets and collections of collections have no creation row: such a member is a field that the literal does not set.

`Emit/LiteralWriter.cs` writes `CreateInstance` as one method with a `switch` over the field numbers of the literal that converts each value into a local of the member type and records that the field is set, by ordinary C# over the public members of `CelValue`, without reflection. A value that does not fit — an `int` outside `Int32`, a string that is not one UTF-16 unit for `char`, an element of a list — returns `FieldError(number, ConversionRange or InvalidArgument, …)` at once; a value of a kind that does not suit the member (possible without type checking or through `dyn`) is `InvalidArgument` with the kind. Then it calls the constructor with the set values or the default values of its parameters, and sets the members that the literal set. A member that the literal did not set is not touched, so it keeps the value of the constructor and the initializers.

**`init` members** cannot be assigned outside an object initializer in C#. The generated code calls an `[UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_X")]` extern method for each `init` member that is not `required`, after the constructor: supported by Native AOT, no reflection, and a member that the literal did not set keeps the value of its initializer. A `required` member, of any kind, is set in an object initializer, because C# does not compile the construction otherwise. A publish with Native AOT on `linux-x64` of the test project gives 0 warnings IL2xxx and IL3xxx.

The generated members are part of the snapshots of the generator tests.

## Type checking

The literal path of `Checker` already rejects a type with `IsConstructible = false` (`TypeNotConstructible`, 2008). For a constructible type it now also calls `CanSetField` for each field the literal names — `false` is `FieldNotAccessible` (2007) with the reason `cannot be set by an object literal`, located at the field — and `IsRequiredField` for each field of the type that the literal does not name — `true` is `RequiredFieldNotSet` (2011), located at the literal, with the field as the subject. The types of the values are checked as for messages.

## Types described at run time

- `CelObjectTypeBuilder.ForDictionary(name).AllowObjectLiterals()` makes the dictionary type constructible: `CreateInstance` builds a `ReadOnlyDictionary<string, object?>` over a new `Dictionary` from the field names to the boxed `CelValue` of each value (a read-only view, not a frozen dictionary: a frozen dictionary would build its hash table at each literal). The dictionary row of the type reads a `CelValue` as it is, so the round trip is exact, and a field the literal does not set has no key (`has()` is `false`).
- `CelObjectTypeBuilder<TCarrier>.AllowObjectLiterals(create)` keeps the delegate in the carrier type: `CreateInstance` passes a new dictionary of the set fields and wraps the carrier; a `null` carrier is `UnexpectedNull`.

## Evaluation and cost

`MessagePlanNode` already calls `CreateInstance` for messages. The call is code of the application now, so it goes through the application boundary (`IApplicationBoundary`) as the getters do: an exception becomes `ApplicationException` (3014) with the original exception, and cancellation is checked before it. The cost of a literal is 1 + n + E: n fields, E the elements and pairs of the collections among the set values that the code copies into members, counted from the values at the top level (a builder copies nothing, so E is 0 there), charged after the type is resolved (`docs/cost.md`).
