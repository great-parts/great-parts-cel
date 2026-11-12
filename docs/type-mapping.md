# Type mapping

This page says how a .NET value becomes a CEL value and how a CEL value becomes a .NET value. The input rows apply wherever a value enters an expression: the bindings of `CelBindings`, the declaration `AddVariable<T>`, and the arguments and the results of the functions of the application. The result rows apply to the result of `Compile<T>`, `Evaluate<T>` and the one-step call, and to the parameters of the typed delegates of the functions of the application.

Not in this version of the page: the rows of `decimal` and of `optional`, and the descriptions of .NET types that the source generator writes. The messages of protobuf have their rows in the section [Protobuf messages](#protobuf-messages). They come with their parts. The objects of the application, the collections of the application and `JsonElement` have their rows below, and the types described at run time are on the page [Types described at run time](runtime-types.md). A .NET type outside the rows is the build error `TypeNotDeclared` (4008) for a variable or a function, and an `ArgumentException` for the type `T` of a result.

An error of a conversion is an error of the result, not an exception, except the conditions that the documentation of a method names. A refusal of a value to enter the expression is an error value where the value is read: the binding when the expression reads the variable, the result of a function at the call.

## Input: a .NET type to a CEL type

`AddVariable<T>(name)` takes the CEL type from `T` by these rows; `AddVariable(name, type)` gives it explicitly. A value of another type than the declared one is the evaluation error `BindingTypeMismatch` (3011) with the name of the variable. The type is checked at any depth: the elements of a list and the keys and the values of a map are checked against the parameters of the declared type, except `dyn` and the type parameters. A variable without a value is the error `Unbound` (3009) when the expression reads it. An exception of application code during a read is the error `ApplicationException` (3014) with the original exception.

| .NET type | CEL type | How it is read |
| :- | :- | :- |
| `long`, `int`, `short`, `sbyte`, `nint` | `int` | exactly |
| `ulong`, `uint`, `ushort`, `byte`, `nuint` | `uint` | exactly |
| `double`, `float`, `Half` | `double` | exactly, a `float` and a `Half` are widened |
| `bool` | `bool` | as it is |
| `string` | `string` | the same instance, without a copy; a `null` reference is the value `null` |
| `Guid` | `string` | the format `D`, in lower case |
| an enumeration, also `[Flags]` | `int` | the underlying value. The value of an enumeration on `ulong` above the range of `int` is the evaluation error `ConversionRange` (3007) |
| `byte[]`, `ReadOnlyMemory<byte>` | `bytes` | without a copy; a `null` array is the value `null`. The application does not change the buffer while an evaluation reads it |
| `CelTimestamp` | `timestamp` | as it is, to the nanosecond |
| `DateTimeOffset` | `timestamp` | the same moment in UTC; the offset is not kept |
| `DateTime` of the kind `Utc` | `timestamp` | the same moment; another kind is refused, see [Time](#time) |
| `CelDuration` | `duration` | as it is |
| `TimeSpan` | `duration` | exactly, a tick is 100 ns; a value outside the range of `duration` is `ConversionRange` |
| `Nullable<X>`, `X` a value type of the rows above | the wrapper of the CEL type of `X`, such as `wrapper(int)`; for time the type of `X` itself, since CEL has no wrapper of time | `null` is the value `null`; for time it fits no type, see [Time](#time) |
| `CelValue` | `dyn` | as it is |
| `CelList`, `CelMap` | `list(dyn)`, `map(dyn, dyn)` | as they are; a `null` reference is the value `null` |
| `JsonElement` | `dyn` | by its kind, lazily; see [`JsonElement`](#jsonelement) |
| `object` | `dyn` | by the row of its runtime type; a boxed `CelValue`, `CelList` or `CelMap` as it is; a `JsonElement` by its section; any other instance as a bare object value, which the expression reads by the rules of [Objects and collections](#objects-and-collections); `null` is the value `null` |

A `null` reference of a reference type of these rows is the value `null`, whatever the static type. The value `null` does not fit `string`, `bytes`, `list` or `map`, so a `null` `CelList` that is bound to a variable of the type `list(dyn)` is `BindingTypeMismatch` with the type `null_type` when the variable is read.

An array, a list, a set or a dictionary of .NET as a value of a binding or a result of a function, a class, a structure or an interface of the application, and a `JsonElement` enter the expression by the rows of the sections [Objects and collections](#objects-and-collections) and [`JsonElement`](#jsonelement). An application that needs a collection outside those rows passes it as a `CelList` or a `CelMap`, made by `CelList.CreateView` or `CelMap.CreateView` with a converter of its elements.

## Objects and collections

These rows apply wherever the rows above do: a binding, `AddVariable<T>`, and the arguments and the results of the functions of the application.

| .NET type | CEL type | How it is read |
| :- | :- | :- |
| a class, `record class`, structure, `readonly struct`, `record struct` or interface that is not in `System` or its namespaces, and not in this library | an object type | the instance itself, as a value of the kind object, without a copy; a structure is boxed once when the value enters. `AddVariable<T>` and the delegate forms of `AddFunction` take the object type that a type source describes for exactly that .NET type, resolved when the environment is built; a type that no source describes is `TypeNotDeclared` (4008) |
| a carrier class of a type described at run time | the object type that the application declares for the variable | `AddVariable("deal", deal.Type)` declares the variable and `CelBindings.Add("deal", carrier)` binds the carrier; the carrier is the .NET type of no description, so `AddVariable<TCarrier>` is `TypeNotDeclared`. See [Types described at run time](runtime-types.md) |
| `X[]`, `IReadOnlyList<X>`, `IList<X>`, `List<X>`, `ImmutableArray<X>`, `ImmutableList<X>`, `ReadOnlyCollection<X>` | `list(E)`, `X` to `E` by the rows above | a lazy view: an element is converted at each read, access by index takes constant time, nothing is copied. A `null` reference is the value `null` |
| `IReadOnlySet<X>`, `ISet<X>` | `list(E)` | a copy that is made when the value enters the expression and sorted in the order of CEL: the order of a set depends on its hashes, and the result must not. An element that CEL cannot order — a NaN, a `null`, kinds that cannot be compared — and two elements that are equal in CEL are an `ArgumentException` of `CelBindings.Add`, and the error `InvalidArgument` (3008) when the set is the result of a function |
| any other `IReadOnlyCollection<X>`, such as `Queue<X>` | `list(E)` | a copy in the order of enumeration, made when the value enters the expression |
| `IReadOnlyDictionary<K, X>`, `IDictionary<K, X>`, `Dictionary<K, X>` | `map(K', E)`, `X` to `E` by the rows above | a lazy view that finds a key with one lookup in the dictionary and converts a value at each read. The order of enumeration is that of the dictionary for `Dictionary`, `OrderedDictionary`, `SortedDictionary`, `SortedList` and `ImmutableSortedDictionary`, and ascending by key for every other dictionary |

The keys `K` of a dictionary and their CEL types: `string` is `string`; `Guid` is `string`, written in the format `D` in lower case; `long` and `int` are `int`; `ulong` and `uint` are `uint`; `bool` is `bool`.

A key is found back from the CEL key. A CEL key of another kind than the key type gives, or one outside its range, finds nothing and is not an error; numeric keys meet on one number line, so a dictionary over `int` finds the key `1` by `1`, `1u` and `1.0`. A `Guid` key is found only by its canonical string: 36 characters, the format `D`, lower case, exactly the string that the enumeration gives. A string in upper case, with braces, or in the formats `N`, `B` or `P` finds nothing, so a map of `Guid` keys behaves as a map of its canonical strings.

**Which types the rows take.** The generic inputs — `AddVariable<T>`, `CelBindings.Add<T>`, the delegate forms of `AddFunction` and the result of a typed delegate — take a closed list of collections one step deep, because a closed generic type that is known only at run time cannot be built under Native AOT:

- `X` of an array, a list, a set and a collection, and the value of a dictionary, is one of `bool`, `long`, `int`, `ulong`, `uint`, `double`, `string`, `Guid`, `byte[]`, `ReadOnlyMemory<byte>`, the `Nullable<>` of those, `CelValue`, `object`, `CelList`, `CelMap` and `JsonElement`;
- a dictionary is an `IReadOnlyDictionary<K, X>`, an `IDictionary<K, X>` or a `Dictionary<K, X>`; another dictionary class, such as `ImmutableDictionary<K, X>`, `SortedDictionary<K, X>` or `ReadOnlyDictionary<K, X>`, is outside the rows and is passed with the type of an interface or as `object`;
- the key `K` of a dictionary is one of `string`, `Guid`, `long`, `int`, `ulong`, `uint` and `bool`;
- the element of an array, an `IReadOnlyList<X>`, an `IList<X>` and a collection without an index is also a class, a record or an interface of the application that is not in `System`: the list is read as a list of `object` and has the type `list(E)` of the object type that a type source describes for that class. An `IList<X>` of such an `X` also has to be an `IReadOnlyList<X>`. A set and a dictionary of such an `X` are outside the rows;
- anything else, such as `IReadOnlyList<IReadOnlyList<long>>`, `IReadOnlyDictionary<string, Order>`, `IReadOnlyList<MyStruct>` or a collection of an enumeration, is outside the rows: `TypeNotDeclared` for `AddVariable<T>` and the delegate forms of `AddFunction`, `ImplementationSignatureMismatch` (4011) for the result of `AddFunctionImplementation<…, TResult>`, and an `ArgumentException` for `CelBindings.Add<T>`. The application passes such data as a `CelList` or a `CelMap` that `CelList.CreateView` or `CelMap.CreateView` makes with a converter. The same list gives the results of `Compile<T>` below, where a class of the application is not an `X` either.

**Error elements.** An element of a collection of the application, and a value of its dictionary, can be an error value, as a converter or an exception of the collection gives: an exception of a view of the application is the error `ApplicationException` (3014) where the element is read. Such an element is not a mismatch of type. An operation that reads it — an index, a key lookup, a comprehension, `in`, equality, the check against a declared type, the conversion of a result — gets the error as the value it reads, with the place of the reading node; when several meet in one check, the first in the order of the walk is returned, a list by index and a map in the order of its iteration. `size` reads no element.

A source that is changed during an evaluation gives an undefined result: a view reads its source at each access.

**A .NET collection in a place of `object`.** A .NET collection — an instance of a type that implements `IEnumerable`, other than `string`, `byte[]`, `CelList` and `CelMap` — is read as a collection only through a row above, with its static type. A binding of the static type `object`, a value in a field or the result of a function that is such a collection is an object. Where the expression reads it — the identifier, the field selection, the call of the function — a type source that describes its exact .NET type or a base class of it makes it an object of that description, even if it is enumerable; otherwise the read is the evaluation error `TypeNotAccessible` (3012) with the .NET type. In a field of a type described over a dictionary it is `FieldTypeMismatch` (3019) with the .NET type. An element of a collection of the application takes none of these rules: it is an object that is described where its description is needed. The way around is a boxed value, `CelValue.FromList(CelList.CreateView(…))`.

## The type of an object

| Where the value enters the expression | The CEL type of the object |
| :- | :- |
| a variable, a field or an element with a static type | the type of the place: an instance of `Dog` in a member of the type `Animal` is an `Animal`, for reading fields and for `type()` |
| a place of the type `dyn`: `object`, a `dyn` variable | by the exact .NET type of the instance, then by its base classes; interfaces are not considered |
| a value of the type `dyn` whose .NET type no source describes | the error `TypeNotAccessible` (3012) with the name of the .NET type, at the node that needs the description: a field selection, `has()`, `type()`, `==`, `!=` or `in` on an object. Reading a value and passing it on need no description, so an instance inside a list or in a `dyn` binding fails only where it is used |
| an object that the application puts into a `dyn` place around the declarations | the application wraps it itself: `CelValue.FromObject(type, instance)`, which throws `ArgumentException` for an instance that does not belong to the type |

A bare instance in a place declared with an object type is the value of that type when it belongs to it: an instance of the .NET type of the description, or of its carrier class. This also looks through the lazy view of a dictionary: an `IReadOnlyDictionary<string, object?>` bound to a variable of a type described over a dictionary is the carrier of that type. When the description of the instance is not the declared type, which is always so for a type described at run time, the evaluation pairs the instance with the declared type; this is one allocation of at most 64 bytes, inside the unit of the read. An application avoids it by binding `CelValue.FromObject(type, carrier)`. A value that the application made with `CelList.CreateView`, `CelMap.CreateView` or `CelValue.FromMap` is a value of the application whose converter decides its values, and the rule does not look through it.

`type(v)` of an object is its object type, and type values compare by kind and name: `type(deal) == acme.crm.Deal`. Two objects are equal when they have the same description and the equal fields, and objects of different descriptions are not equal; see [Types described at run time](runtime-types.md#equality-of-objects). Checking a value against a type never gives `TypeNotAccessible`: an instance that no source describes and that does not belong to the declared type is a mismatch of that place — `BindingTypeMismatch` for a variable, `FieldTypeMismatch` for a field, `EvalNoMatchingOverload` for an argument, `ImplementationResultMismatch` for a result.

## `JsonElement`

The mapping follows the language definition, "JSON Data Conversion". The static type of `AddVariable<JsonElement>("payload")` is `dyn`. An application can declare a more exact type, such as `map(string, dyn)`; a `JsonElement` of another kind is then the error `BindingTypeMismatch`.

| `JsonValueKind` | CEL type | How it is read | Errors |
| :- | :- | :- | :- |
| `Null` | `null_type` | — | — |
| `True`, `False` | `bool` | — | — |
| `Number` | `double` | by `TryGetDouble`. An integer of JSON is a `double` too, as in the language definition and in cel-go; `int(payload.qty)` converts it explicitly | a number that is not a finite `double` is the error `ConversionRange` (3007) |
| `String` | `string` | a new string at each read, which the [cost](cost.md#data-of-the-application) counts | — |
| `Array` | `list(dyn)` | a lazy view of the element: count, index and enumeration in the order of the document | — |
| `Object` | `map(string, dyn)` | a lazy view of the element; a key is found by an ordinal comparison that distinguishes case, and the enumeration is in the order of the properties of the document | a repeated property name is the error `DuplicateMapKey` (3010) in every operation that touches that name |
| `Undefined` (`default(JsonElement)`) | — | — | a member, an element or the result of a function is the error `InvalidArgument` (3008) with the name; a binding is an `ArgumentException` of `CelBindings.Add` |

**Repeated names.** A key lookup — an index, a selection, `has()` — compares the key with every property name of the object, escape sequences decoded, and counts the matches: none is no key, one is the value, two or more are `DuplicateMapKey` with that key. `size`, enumeration, `in` and equality check the names of all properties for a repeat before they give their result, and the first name in the order of the document that repeats an earlier one is the key of the error. A name that is written with an escape sequence and the same name written without it are a repeat. A key that is not a `string` finds nothing. A JSON object equals a map by the rule of maps, with this lookup.

`has(payload.f)` tells whether the object has the key `f`. A document that the application disposed gives `ObjectDisposedException` from the `JsonElement`, which is the error `ApplicationException` (3014); the documentation of the application's code must keep the document until the evaluation ends.

`JsonElement` is never a result: the reverse mapping is ambiguous for `int`, `uint` and `bytes` and needs a new document, so a JSON value is given by the rows of `list`, `map` and the simple types in the results below. No type provider is needed for it: the values are `map`, `list` and simple values.

### Time

**Time.** A `DateTimeOffset` converts exactly, by its moment in UTC; its offset is not kept, and no zone or clock of the machine is read. A `DateTime` is a moment only when its kind is `Utc`; a `DateTime` of the kind `Local` or `Unspecified` would depend on the zone of the process, so it is refused: `CelBindings.Add` throws `ArgumentException` that names the kind, also when the `DateTime` is boxed as `object`, because the value becomes a CEL value at once. Read lazily — a field of an object, an element of a collection of the application, the result of a typed delegate — such a `DateTime` is the evaluation error `InvalidArgument` (3008) `invalid argument to '<name>': a DateTime of the kind <kind> is not an instant in UTC` at the node that reads it. A `TimeSpan` outside the range of `duration` ([Limits](limits.md#values)) is the evaluation error `ConversionRange` (3007), `<ticks> ticks is out of the range of duration`, where the variable is read.

CEL has no wrapper of time, so `DateTimeOffset?`, `DateTime?`, `TimeSpan?`, `CelTimestamp?` and `CelDuration?` have the CEL type of the value: `AddVariable<DateTimeOffset?>("t")` declares `timestamp`. A `null` bound to such a variable is the value `null`, which fits neither `timestamp` nor `duration`: `BindingTypeMismatch` (3011) at the first read. A typed delegate that returns such a `null` is `ImplementationResultMismatch`.

A time value is a scalar. In a collection it goes through `CelList`, `CelMap`, `CelValue` or `object` (boxed `DateTimeOffset`, `DateTime`, `TimeSpan`, `CelTimestamp` or `CelDuration`); a typed collection of time such as `IReadOnlyList<DateTimeOffset>` is not one of the rows: `AddVariable<T>` is `TypeNotDeclared` and `CelBindings.Add<T>` an `ArgumentException`. A field of the type `timestamp` or `duration` of a type described at run time reads `CelValue.FromTimestamp(…)` or `CelValue.FromDuration(…)`, which allocates nothing.

## Protobuf messages

The package `Great.Parts.Cel.Protobuf` describes the messages of `.proto` files, and a message enters an expression as the instance of its generated class, as an object of the rows above, without a copy. The fields of a message are read by these rows; the page [Protobuf messages](protobuf.md) has the details, the presence rules, the object literals, `Any` and the equality of messages.

| Protobuf field | CEL type | Value |
| :- | :- | :- |
| `int32`, `sint32`, `sfixed32`, `int64`, `sint64`, `sfixed64` | `int` | the number |
| `uint32`, `fixed32`, `uint64`, `fixed64` | `uint` | the number |
| `float`, `double` | `double` | a `float` is widened |
| `bool`, `string`, `bytes` | `bool`, `string`, `bytes` | as they are; `bytes` is the memory of the message, without a copy |
| an enumeration, `google.protobuf.NullValue` included | `int` | the number, also one that the enumeration does not declare; `NULL_VALUE` is 0 |
| a message `M` | the object type `M` | the instance; a field that is not set is an empty message, and `has()` tells the two apart |
| `repeated T` | `list(T)` | a view over the field, without a copy |
| `map<K, V>` | `map(K, V)` | a view over the field, in the order of insertion |
| `google.protobuf.Any` | `any` | the unpacked content; a field that is not set is `null`; an `Any` that cannot be unpacked is `InvalidAny` (3020) |
| `google.protobuf.Value` | `dyn` | by its kind: `null`, `double`, `string`, `bool`, `map(string, dyn)` or `list(dyn)`; a `Value` without a kind and one that is not set are `null` |
| `google.protobuf.Struct`, `google.protobuf.ListValue` | `map(string, dyn)`, `list(dyn)` | views, read only |
| `google.protobuf.Timestamp`, `google.protobuf.Duration` | `timestamp`, `duration` | the time value; a field that is not set is `1970-01-01T00:00:00Z` and `0s`; a value outside the range of CEL is `ConversionRange` (3007) |
| a wrapper such as `google.protobuf.Int64Value` | `wrapper(int)`, `wrapper(uint)`, `wrapper(double)`, `wrapper(bool)`, `wrapper(string)` or `wrapper(bytes)` | the value, or `null` for a field that is not set; a wrapper that is set to the default reads the default |

The values of the known types are never objects: a .NET value of the type `Timestamp`, `Int32Value`, `Struct` or `Any` is not described, so `AddVariable<Timestamp>` is `TypeNotDeclared` (4008), and such a value in a `dyn` place is `TypeNotAccessible` (3012). An application holds an `Any` as data in a field of a message. The result of an expression that is a message is converted to the generated class by the row of an object type.

## Types that are not read

A type in the namespace `System` or in a namespace below it that has no row on this page is not read. It is `TypeNotDeclared` for `AddVariable<T>` and the delegate forms of `AddFunction`, and an `ArgumentException` for `CelBindings.Add<T>`: its members would be read as data by a random set of properties, or reading them would run code or input and output. They include:

| Type | Why | What to do |
| :- | :- | :- |
| a class, a structure or an interface of `System` without a row: `Uri`, `Version`, `IPAddress`, `Regex`, `Exception`, `CultureInfo`, `CancellationToken`, `KeyValuePair<K, V>`, `ValueTuple<…>`, `Tuple<…>` | read as an object, it would be a random set of properties | a string, or a type of the application with the fields that are needed |
| `Task`, `Stream`, delegates, `Type`, pointers | not data: reading would run code of the application or input and output | — |
| `IEnumerable<T>` that is not an `IReadOnlyCollection<T>`, and the collections without a type of elements (`ArrayList`, `Hashtable`) | the size and the index are unknown, and the enumeration may never end or may have side effects | an array or a list |
| `char` | a unit of UTF-16, not a code point; a lone surrogate is no CEL string | `string` |
| `Int128`, `UInt128`, `BigInteger` | CEL has no integer wider than 64 bits, so an error of range would show only on data | `long` or `string` |
| `DateOnly`, `TimeOnly` | not supported in 1.0; they can be added in a minor version | `DateTimeOffset`, or a string |
| `JsonDocument`, `JsonNode` | a `JsonDocument` is disposed by the application and a `JsonNode` can be changed | the `RootElement` as a `JsonElement` |

A `string`, a `Guid`, an enumeration, the collections and the types of the rows above are read through their own rows, and the classes of the application that are not in `System` are objects.

## Functions of the application

| Rule |
| :- |
| `AddFunction(name, delegate)` takes the CEL type of the result from the signature of the delegate by the input rows above, and those of the parameters by the table of the expected types below. The full form, a `CelFunctionDecl`, is for `dyn`, generic types and overloads on a receiver. A type outside the rows is `TypeNotDeclared`. |
| The identifier of the overload that the delegate form declares is the name of the function and the CEL types of the parameters in order, joined with `_`, written without spaces: `isInn_string`, `sum_list<int>`. A function without parameters has the name as its identifier. |
| **The arguments are checked first.** Before the cost function and the implementation, every argument is checked at any depth against the declared type of its parameter, whatever its static type; the type parameters are as `dyn`. An argument that does not fit is `EvalNoMatchingOverload` (3000). So the code of the application gets only values of the declared types. |
| **A value is passed to a parameter by the result rows below.** A lazy view or a copy is chosen by the pair of the declared type of the parameter and the .NET type. A refusal is `ArgumentConversion` (3024) with the names of the function, the overload and the parameter. It is a refusal by the value only: an `int` outside `Int32`, a `uint` outside `UInt32`, or such an element of an `IReadOnlyList<int>`. |
| **The CEL type of a parameter of a typed delegate** is taken from its .NET type by the table of the expected types below, as for `Compile<T>`, and must equal the declared type of the parameter after the type parameters are erased; `dyn` and a type parameter take `CelValue` and `object`. Otherwise the build error `ImplementationSignatureMismatch` (4011) is reported. The same holds for the delegate form of `AddFunction`, so `AddFunction<short, bool>("small", …)` is that error. |
| **Parameters that build:** `IReadOnlyList<long>` (a lazy view) and `IReadOnlyList<int>` (a copy) for `list(int)`; `CelList` for `list(dyn)`; `IReadOnlyList<CelList>` for `list(list(dyn))`; `IReadOnlyDictionary<string, long>` for `map(string, int)`; `long?` for `wrapper(int)`; `string` for `wrapper(string)`; `byte[]` and `ReadOnlyMemory<byte>?` for `wrapper(bytes)`. |
| **A class of the application** as a parameter or as the result of `AddFunctionImplementation<T1, …, TResult>` is the object type that a type source describes for exactly that .NET type, compared with the declared overload when the environment is built; without a description it is `TypeNotDeclared`, not `ImplementationSignatureMismatch`. |
| **A list of a class.** A result `IReadOnlyList<Order>`, `Order[]` or `IList<Order>`, `Order` a class of the application, has the type `list(Order)` with the object type that a type source describes for `Order`, and also fits a declared `list(dyn)`; without a description of `Order` it is `TypeNotDeclared`. As a parameter it is `ImplementationSignatureMismatch` (4011): a list of a class has no expected type, because a view of it would be a closed generic type known only at run time. |
| **Parameters that do not build:** `Guid`, `short`, `sbyte`, `ushort`, `byte`, `nint`, `nuint`, `float`, `Half`, an enumeration and their `Nullable<>`; `IReadOnlyList<short>`; an array; a typed collection deeper than one step, such as `IReadOnlyList<IReadOnlyList<long>>`; `long` for `dyn`; `CelList` and `IReadOnlyList<object>` for `list(int)`, whose expected type is `list(dyn)`; `CelValue`, `object`, `ReadOnlyMemory<byte>` and `long` for a wrapper. |
| **A wrapper takes a reference type.** A declared `wrapper(string)` or `wrapper(bytes)`, for a parameter and for a result, also takes `string` and `byte[]`: the CEL type of those by the table below is the type inside the wrapper. The value `null` passes as the `null` reference. |
| **The result is checked at any depth** against the declared type of the result, after the implementation; the type parameters are as `dyn`, and the type of the node of the call is not checked. A value that does not fit is the evaluation error `ImplementationResultMismatch` (3028) with the names of the function and the overload. A result of the type `dyn` is not checked. A result whose CEL type by the input rows already gives the .NET type of the result is not checked either, such as `Func<long>` for `int`. A `null` reference of `string`, `byte[]`, `CelList` or `CelMap` is checked at the top level, because the input rows read it as the value `null`, which does not fit those types; the error then names the type `null_type`. A result `string` or `byte[]` of a declared `wrapper(string)` or `wrapper(bytes)` is not checked: its `null` fits the wrapper. An error value that the implementation returns spreads as an error value; it is not this error. |
| **Costs.** A copy that is passed to a parameter and the checks of the arguments and of the result are charged on the page [Cost](cost.md#functions-of-the-application). |

## Results

The value of the root is converted to the type `T` by the rows below. A value whose CEL type has no `T` in the column of the allowed types, or a value that the rule of its row does not convert, is the evaluation error `ResultConversion` (3015) at the root of the expression. A `dyn` result takes the type of the value: a string with `T = bool` is that error. There is no conversion between numeric types: a `uint` with `T = long` is `ResultConversion`. `T = Nullable<X>` is allowed for every value type `X` of the rows: a value that is not `null` converts by the row of its type with `T = X`, and `null` is `null`.

| CEL type of the value | Allowed `T` | Rule | Refused |
| :- | :- | :- | :- |
| `bool` | `bool` | as it is | a value of another type |
| `int` | `long`, `int` | `long` as it is; `int` with a check of the range | outside `Int32` with `T = int` |
| `uint` | `ulong`, `uint` | `ulong` as it is; `uint` with a check of the range | outside `UInt32` with `T = uint` |
| `double` | `double` | as it is | a value of another type |
| `string` | `string` | as it is | a value of another type |
| `bytes` | `ReadOnlyMemory<byte>`, `byte[]` | `ReadOnlyMemory<byte>` without a copy; `byte[]` as a copy | a value of another type |
| `timestamp` | `CelTimestamp`, `DateTimeOffset` | `CelTimestamp` exactly; `DateTimeOffset` with offset 0 | `T = DateTimeOffset` when the nanoseconds are not a multiple of 100: `ResultConversion` at the root, never rounded |
| `duration` | `CelDuration`, `TimeSpan` | `CelDuration` exactly; `TimeSpan` exactly | `T = TimeSpan` when the nanoseconds are not a multiple of 100 |
| `null_type` | reference types, `Nullable<>`, `object`, `CelValue` | `null`; for `CelValue` the value `null`. `CelEvalResult<T>.Value` is `null` also after `IsSuccess` is checked when the result is `null` for a reference `T`; `GetValueOrThrow()` returns `null` too | `T` a value type without `?` |
| `list(E)` | `IReadOnlyList<X>` with `E` to `X` by this table, one step deep; `CelList` | a lazy view when no element can fail its conversion and the list is a collection of the library, after a check of the value; otherwise an immutable copy made at the end of the evaluation | an element that does not convert to `X`, at the root; a value that fails the check before the view |
| `map(K, V)` | `IReadOnlyDictionary<XK, XV>` with `K` to `XK` and `V` to `XV` by this table, one step deep; `CelMap` | the same | a key or a value that does not convert, or fails the check |
| `type` | `CelType` | the parameters of types are erased at run time: `type([1]) == list` | a value of another type |
| a wrapper such as `wrapper(int)`: a plain value or `null` | `long?`, `int?`, `ulong?`, `uint?`, `double?`, `bool?`, `ReadOnlyMemory<byte>?`, and `string`, `byte[]` | not `null`: by the row of the CEL type of the value with `T = X`, so an `int` with `T = long?` is the same as with `long`; `null` is `null` | by the row of the CEL type of the value |
| any | `CelValue` | as it is | never |
| an object type, also one described at run time | the .NET class, record, structure or interface of the application that the instance is, a base class or an interface of it, and the carrier class `TCarrier` of a type described at run time; `object`; `CelValue` | the same instance, by a test of its type, without reading the description | an instance that is not a `T`, with the name of its description or of its .NET type; the error is never `TypeNotAccessible` |
| any | `object` | the natural mapping: `long`, `ulong`, `double`, `bool`, `string`, `byte[]`, `null`, `CelTimestamp`, `CelDuration`, `IReadOnlyList<object?>`, `IReadOnlyDictionary<object, object?>`, the instance of an object, `CelType` | never |

`JsonElement` and unknown values are never a result, and an error is not a value of the result: the errors of an evaluation come as the list of errors of the result.

For `string?` and `byte[]?` the type at run time is `string` and `byte[]`: the annotation does not enter the type argument. A `null` is therefore allowed for a reference `T` by the row `null_type`.

### Typed nesting is one step

`X`, `XK` and `XV` are types of the rows other than `IReadOnlyList<…>` and `IReadOnlyDictionary<…>`. Deeper levels go through `CelList`, `CelMap`, `CelValue` and `object`, at any depth: `IReadOnlyList<CelList>` for `list(list(int))`. The types of the rows are `bool`, `long`, `int`, `ulong`, `uint`, `double`, `string`, `ReadOnlyMemory<byte>`, `byte[]`, the `Nullable<>` of those, `CelValue`, `object`, `CelList`, `CelMap` and `CelType`; `XK` is `long`, `int`, `ulong`, `uint`, `bool`, `string`, `object` or `CelValue`. A class of the application is no `X` either: `IReadOnlyList<Order>` is a `T` outside the rows for `Compile<T>`, and `IReadOnlyList<object>` gives the instances. A deeper typed `T`, such as `IReadOnlyList<IReadOnlyList<long>>`, is a type outside the rows: an `ArgumentException` for a result, and `ImplementationSignatureMismatch` for a parameter of a delegate. The rule is the same under JIT and under Native AOT, because a closed generic type that is known only at run time cannot be built without dynamic code, and a result must not depend on the mode.

### The check before a lazy view

A static type is no guarantee of the value: `[1] + dyn(['a'])` has the type `list(int)` and the value `[1, 'a']`. A lazy view would throw when its element is read after the result is returned, so a collection is converted by these rules:

- a view is built only when no element, key or value can fail its conversion by the pair of the CEL type of the root and the type `X`: `int` to `long`, `uint` to `ulong`, `string` to `string`, `dyn` to `object` and `dyn` to `CelValue` cannot fail; `int` to `int`, `uint` to `uint` and `dyn` to another `X` can. The pair is taken from the static type of the root, not from the expected type: a value of the type `list(dyn)` with `T = IReadOnlyList<long>` is a copy, and a string element is `ResultConversion`;
- only a collection of the library is viewed lazily: a literal, a result of an evaluation, `CelList.Create`, `CelMap.Create`. The copy of any other collection is made, because reading it runs code of the application;
- before a view, the value is checked at any depth against the expected type that the table below gives for `T`, not against the static type of the root, in one pass, without a charge. A value that does not fit is `ResultConversion`. The check names what the view reads: a position that the view gives as `object` or `CelValue` is `dyn`, and one that it gives as `CelList` or `CelMap` is `list(dyn)` or `map(dyn, dyn)`;
- `null` also fits a position that the view gives as `string`, `byte[]`, `CelList`, `CelMap` or `CelType`, because the row `null_type` reads it there as a `null` reference, as a copy does. A position of `ReadOnlyMemory<byte>`, `long`, `int` or another value type does not take `null`; `long?` and the other `Nullable<>` take it as a wrapper does;
- the type in the text of the error is the expected type without that allowance: `list(int)`, never a type with `null` in it. It is the type of the first element, key or value that does not fit, in the order of enumeration: `list(string)` for `x + y` with `T = IReadOnlyList<long>`.

So the check refuses where a copy would refuse and takes what a copy would convert, and the choice between a view and a copy never changes the result. With `T = IReadOnlyList<string>`, both `['a'] + dyn([null])` (a view) and `dyn(['a', null])` (a copy) give `['a', null]`; with `T = IReadOnlyList<ReadOnlyMemory<byte>>`, both `[b'a'] + dyn([null])` and `dyn([b'a', null])` are `ResultConversion`. At the root nothing is checked: `(['a'] + dyn([null]))[1]` with `T = string` is `null`. `T = object`, `CelValue`, `CelList` and `CelMap` are not checked: they rely on no static type.

## The expected type

The expected type of `Compile<T>` is taken from `T`. An expression whose type is not assignable to it by the rules of type checking is the compilation error `ExpectedTypeMismatch` (2003) with the position. `CelCompileOptions.ExpectedType` sets the type explicitly when `T` says too little, such as `list(int)` with `T = CelValue`; the explicit type must be compatible with `T`, that is, `T` is allowed for that CEL type by the table of the results. A pair that is not compatible, and a `T` outside the rows such as `short`, `Guid` or `IReadOnlyList<Order>`, is an `ArgumentException`, thrown before the text is read. `CelProgram<T>.ResultType` is the CEL type of the result.

| `T` | Expected CEL type |
| :- | :- |
| `bool` | `bool` |
| `long`, `int` | `int` |
| `ulong`, `uint` | `uint` |
| `double` | `double` |
| `string` | `string` |
| `ReadOnlyMemory<byte>`, `byte[]` | `bytes` |
| `CelTimestamp`, `DateTimeOffset` | `timestamp` |
| `CelDuration`, `TimeSpan` | `duration` |
| `IReadOnlyList<X>` | `list(E')`, `E'` the expected type of `X` |
| `IReadOnlyDictionary<XK, XV>` | `map(K', V')` from `XK` and `XV` |
| `CelType` | `type` |
| `Nullable<X>` | the wrapper of the CEL type of `X`: `int?` and `long?` give `wrapper(int)`; `uint?` and `ulong?` give `wrapper(uint)`; `double?`, `bool?` and `ReadOnlyMemory<byte>?` give `wrapper(double)`, `wrapper(bool)` and `wrapper(bytes)`; `DateTimeOffset?`, `CelTimestamp?`, `TimeSpan?` and `CelDuration?` give `timestamp` and `duration`, which have no wrapper |
| a class, record, structure or interface of the application | its object type when a type source describes exactly this .NET type; otherwise `dyn`, and the instance is tested at the evaluation. An explicit `ExpectedType` must then be `dyn` or an object type |
| `CelValue`, `object` | `dyn`: a result of any type |
| `CelList` | `list(dyn)` |
| `CelMap` | `map(dyn, dyn)` |
