# Types described at run time

An application can give expressions an object type that has no .NET type of its own: the fields of the type, with their CEL types, are described when the environment is built, and each value of the type is an instance of a carrier class. This suits data whose schema lives in metadata, such as the fields of an entity that an administrator defines. A type is described by `CelObjectTypeBuilder<TCarrier>` over a carrier class with a delegate for each field, or by `CelObjectTypeBuilder.ForDictionary` over a dictionary carrier, which is also the only form that an [environment description in JSON](environment-json.md#greatpartscel) can express.

Not in this version of the page: the descriptions of .NET types that the source generator writes, and the types of protobuf messages. They come with their parts, and the rules below already hold for them where they say so.

```csharp
using Great.Parts.Cel;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

CelObjectType dealType = new CelObjectTypeBuilder<Deal>("acme.crm.Deal")
    .AddField("title", CelType.String, deal => CelValue.FromString(deal.Title))
    .AddField("amount", CelType.Int, deal => CelValue.FromInt64(deal.Amount ?? 0), deal => deal.Amount is not null)
    .Build();

CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
    .AddTypes(CelTypeProvider.Create("crm", dealType))
    .AddVariable("deal", dealType.Type)
    .Build()
    .GetEnvironmentOrThrow();

CelProgram<bool> large = environment.Compile<bool>("has(deal.amount) && deal.amount > 100").GetProgramOrThrow();
Console.WriteLine(large.Evaluate(new CelBindings().Add("deal", new Deal("first", 150))).GetValueOrThrow()); // Output: True
Console.WriteLine(large.Evaluate(new CelBindings().Add("deal", new Deal("second", null))).GetValueOrThrow()); // Output: False

CelProgram<long> amount = environment.Compile<long>("deal.amount").GetProgramOrThrow();
Console.WriteLine(amount.Evaluate(new CelBindings().Add("deal", new Deal("second", null))).Errors[0].Code); // Output: FieldNotSet

record Deal(string Title, long? Amount);
```

The variable is declared with the CEL type of the description, `dealType.Type`, not with `AddVariable<Deal>`: the carrier class is the .NET type of no description, because one class serves many CEL types.

## Describing a type

| Rule |
| :- |
| `CelObjectTypeBuilder<TCarrier>(name)` collects the fields. `AddField(name, type, read, has)` gives the field the next number; `read` reads the value of the field from a carrier, and `has` tells whether the field is present. The reading is lazy: the data is not copied, and `read` is called each time an expression selects the field. |
| `CelObjectTypeBuilder.ForDictionary(name)` is the same over the carrier `IReadOnlyDictionary<string, object?>`: the field `f` is the entry with the key `f`, `AddField(name, type)` takes no delegates. The value of the entry is converted as the value of a member of the .NET type `object` by the input rows of the [type mapping](type-mapping.md), and checked against the declared type of the field. A boxed `CelValue`, a `CelList` and a `CelMap` in the dictionary are taken as they are, which is how a dictionary holds a list or a map. The values are boxed in the dictionary and checked at each read; the generic builder over a typed carrier avoids both. |
| The name of the type is a full CEL name: identifiers separated by dots, such as `acme.crm.Deal`. A field has a name and a CEL type; its number is the order of its declaration. |
| The name of a field is an identifier. A reserved word of CEL is allowed (`in`, `as`, `true`, `null`) and is selected in backticks: ``deal.`in` ``. A name with `-`, `.`, `/` or a space is refused although backticks would allow it. A repeated name, a name that is not an identifier and a type parameter as the type of a field are an `ArgumentException` at once, at the call of `AddField`. |
| `Build()` returns an immutable description over a copy of the fields, with `ClrType` `null`; `AddField` calls after it change nothing in it. |
| The presence of a field, which `has()` tests, is the `has` delegate; without one, the field is present when `read` returns a value that is not `null`. Over a dictionary the field is present when the dictionary has its key. |
| The description does not enter a table of .NET types. A value enters through a variable or a field that is declared with the CEL type of the description, as in the example. A carrier that is not an instance of `TCarrier` is the evaluation error `BindingTypeMismatch` (3011) when the expression reads the variable. |
| A field that has no value is read as the error `FieldNotSet` (3018) with the names of the type and the field: a silent zero would hide a gap in the data. An expression tests `has()` first. |
| A value of another type than the declared one is the error `FieldTypeMismatch` (3019) with the names of the type and the field. |
| An exception of `read` or `has` is the error `ApplicationException` (3014) with the original exception. |
| An object literal of such a type, `acme.crm.Deal{…}`, is not supported: it is the type checking error `TypeNotConstructible` (2008). |

The builders are not thread-safe. The descriptions they build are immutable and can be shared between threads and environments, and the delegates of the application must allow calls from many threads at once.

## `has()` and `null`

The presence of a field is decided before its value is read. A present field whose value is `null` has the outcome that its declared type gives:

| Declared type of the field | A present field with the value `null` | `has(e.f)` |
| :- | :- | :- |
| `wrapper(P)`, an object type, `dyn`, `null_type` | the value `null` | the presence of the field: `true` when it is present, whatever it holds |
| any other type: a simple type, `list`, `map`, `type` | the error `FieldTypeMismatch` (3019) | the presence of the field |

With the default presence a field whose `read` returns `null` is not present, so it is `FieldNotSet` when it is read and `has()` is `false`. A `null` field value reaches an expression only through an explicit `has` that says the field is present, or through a dictionary that has the key with a `null` value.

## How a field is read

A field is read in three steps, in this order:

1. **Presence.** With a `has` delegate, a field for which it returns `false` is `FieldNotSet`. Without one, the value of `read` is taken once, and `null` is not present, so it is `FieldNotSet` too. Over a dictionary the field is present when the key is.
2. **Value.** The value of `read`; over a dictionary the value of the entry. If the declared type of the field is an object type whose description is itself over a dictionary and the entry holds an `IReadOnlyDictionary<string, object?>`, the entry is the carrier of that type. Otherwise the entry converts as a value of the .NET type `object`: a boxed `CelValue`, a `CelList` and a `CelMap` are taken as they are, a `JsonElement` by its [rows](type-mapping.md#jsonelement), and any other instance as an object. A .NET collection that no type source describes does not convert: in a field of the type `dyn`, a list, a map, or an object type whose carrier is not a dictionary, and a dictionary of another type than `IReadOnlyDictionary<string, object?>` in any field, it is `FieldTypeMismatch` with the actual .NET type.
3. **The declared type, at the top level only.** The value must fit the declared type of the field: the kind for a simple type, `list(…)`, `map(…)` and `type(…)`; `null` or the simple value for `wrapper(P)`; `null` or an object of that type for an object type; anything for `dyn`. Otherwise it is `FieldTypeMismatch` with the actual type, the declared type, the field and the type of the object. The check does not descend into a list or a map: reading a field costs 1 unit whatever its value holds, and the elements of a collection of the application are checked where an operation reads them. An error value that `read` returns is the value of the field.

`has(e.f)` is the first step alone. A field that the type does not have is the type checking error `NoSuchField` (2004); on an operand of the type `dyn` it is `EvalNoSuchField` at evaluation, and a selection on a value that is no map and no object is `FieldSelectionNotSupported` (3025). Reading a field is an event of cancellation, because it calls code of the application.

## Type sources of an environment

`CelEnvironmentBuilder.AddTypes(provider)` adds a source of types; `CelTypeProvider.Create(sourceName, objectTypes)` makes a source of types described at run time, with no enumeration types. A source is added as a set: the same provider twice adds nothing. All sources are checked together when the environment is built, with the other build errors:

| Code | When |
| :- | :- |
| `DuplicateType` (4001) | two descriptions that are not the same instance have one CEL name, in one source or in two; a name of an object or enumeration type is equal to a type identifier of the standard library version (`int`, `list`, `google.protobuf.Timestamp`, …); two descriptions that are not the same instance have one .NET type, in one source or in two. The same description instance in two providers is one declaration. The message names both sources: `type '{name}' is declared by both {source1} and {source2}` |
| `TypeNotDeclared` (4008) | an object type is named in the type of a field and no source describes it |

A provider is the source `type source '<SourceName>'`. Of the two sources of one error the standard library comes first, otherwise the smaller text, so the message does not depend on the order of the calls. An object type is declared when a source has its name, and a type of a variable or of an overload that names it needs no more. The names and constants of enumeration types are identifiers of expressions: the constant `Active` of `acme.crm.Status` is the identifier `acme.crm.Status.Active` of the type `int`.

## Equality of objects

Two object values are compared by `==`, `!=`, `in` and the equality of the lists and maps that hold them:

1. An object and a value of another kind are not equal. Of two objects each needs its description; an instance that no source describes is the error `TypeNotAccessible` (3012) at the call.
2. Two objects of different descriptions are not equal, and no field is read.
3. Of one description, the fields are compared in the order of their numbers. A field present in one object only makes them unequal; a field absent from both is skipped; a field present in both is compared by the equality of CEL, and an error value that a read gives is the result. There is no shortcut for the same reference: a field that is a NaN makes an object unequal to itself, as the language definition says.

The cost is on the page [Cost](cost.md#data-of-the-application).
