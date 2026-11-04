# Type mapping

This page says how a .NET value becomes a CEL value and how a CEL value becomes a .NET value. The input rows apply wherever a value enters an expression: the bindings of `CelBindings`, the declaration `AddVariable<T>`, and the arguments and the results of the functions of the application. The result rows apply to the result of `Compile<T>`, `Evaluate<T>` and the one-step call, and to the parameters of the typed delegates of the functions of the application.

Not in this version of the page: the rows of time (`CelTimestamp`, `DateTimeOffset`, `DateTime`, `CelDuration`, `TimeSpan`), of `decimal` and of `optional`, and the rows of the objects of the application, of `JsonElement` and of the messages of protobuf. They come with their parts. A .NET type outside the rows is the build error `TypeNotDeclared` (4008) for a variable or a function, and an `ArgumentException` for the type `T` of a result.

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
| `Nullable<X>`, `X` a value type of the rows above | the wrapper of the CEL type of `X`, such as `wrapper(int)` | `null` is the value `null` |
| `CelValue` | `dyn` | as it is |
| `CelList`, `CelMap` | `list(dyn)`, `map(dyn, dyn)` | as they are; a `null` reference is the value `null` |
| `object` | `dyn` | by the row of its runtime type; a boxed `CelValue`, `CelList` or `CelMap` as it is; `null` is the value `null` |

A `null` reference of a reference type of these rows is the value `null`, whatever the static type. The value `null` does not fit `string`, `bytes`, `list` or `map`, so a `null` `CelList` that is bound to a variable of the type `list(dyn)` is `BindingTypeMismatch` with the type `null_type` when the variable is read.

A collection of .NET as a value of a binding or a result of a function — an array, an `IReadOnlyList<T>`, an `IReadOnlyDictionary<K, V>`, a set — comes with a later part, and `AddVariable<T>` with such a `T` is `TypeNotDeclared`. In this version a list or a map enters the expression as a `CelList` or a `CelMap`.

## Functions of the application

| Rule |
| :- |
| `AddFunction(name, delegate)` takes the CEL type of the result from the signature of the delegate by the input rows above, and those of the parameters by the table of the expected types below. The full form, a `CelFunctionDecl`, is for `dyn`, generic types and overloads on a receiver. A type outside the rows is `TypeNotDeclared`. |
| The identifier of the overload that the delegate form declares is the name of the function and the CEL types of the parameters in order, joined with `_`, written without spaces: `isInn_string`, `sum_list<int>`. A function without parameters has the name as its identifier. |
| **The arguments are checked first.** Before the cost function and the implementation, every argument is checked at any depth against the declared type of its parameter, whatever its static type; the type parameters are as `dyn`. An argument that does not fit is `EvalNoMatchingOverload` (3000). So the code of the application gets only values of the declared types. |
| **A value is passed to a parameter by the result rows below.** A lazy view or a copy is chosen by the pair of the declared type of the parameter and the .NET type. A refusal is `ArgumentConversion` (3024) with the names of the function, the overload and the parameter. It is a refusal by the value only: an `int` outside `Int32`, a `uint` outside `UInt32`, or such an element of an `IReadOnlyList<int>`. |
| **The CEL type of a parameter of a typed delegate** is taken from its .NET type by the table of the expected types below, as for `Compile<T>`, and must equal the declared type of the parameter after the type parameters are erased; `dyn` and a type parameter take `CelValue` and `object`. Otherwise the build error `ImplementationSignatureMismatch` (4011) is reported. The same holds for the delegate form of `AddFunction`, so `AddFunction<short, bool>("small", …)` is that error. |
| **Parameters that build:** `IReadOnlyList<long>` (a lazy view) and `IReadOnlyList<int>` (a copy) for `list(int)`; `CelList` for `list(dyn)`; `IReadOnlyList<CelList>` for `list(list(dyn))`; `IReadOnlyDictionary<string, long>` for `map(string, int)`; `long?` for `wrapper(int)`; `string` for `wrapper(string)`; `byte[]` and `ReadOnlyMemory<byte>?` for `wrapper(bytes)`. |
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
| `null_type` | reference types, `Nullable<>`, `object`, `CelValue` | `null`; for `CelValue` the value `null`. `CelEvalResult<T>.Value` is `null` also after `IsSuccess` is checked when the result is `null` for a reference `T`; `GetValueOrThrow()` returns `null` too | `T` a value type without `?` |
| `list(E)` | `IReadOnlyList<X>` with `E` to `X` by this table, one step deep; `CelList` | a lazy view when no element can fail its conversion and the list is a collection of the library, after a check of the value; otherwise an immutable copy made at the end of the evaluation | an element that does not convert to `X`, at the root; a value that fails the check before the view |
| `map(K, V)` | `IReadOnlyDictionary<XK, XV>` with `K` to `XK` and `V` to `XV` by this table, one step deep; `CelMap` | the same | a key or a value that does not convert, or fails the check |
| `type` | `CelType` | the parameters of types are erased at run time: `type([1]) == list` | a value of another type |
| a wrapper such as `wrapper(int)`: a plain value or `null` | `long?`, `int?`, `ulong?`, `uint?`, `double?`, `bool?`, `ReadOnlyMemory<byte>?`, and `string`, `byte[]` | not `null`: by the row of the CEL type of the value with `T = X`, so an `int` with `T = long?` is the same as with `long`; `null` is `null` | by the row of the CEL type of the value |
| any | `CelValue` | as it is | never |
| any | `object` | the natural mapping: `long`, `ulong`, `double`, `bool`, `string`, `byte[]`, `null`, `IReadOnlyList<object?>`, `IReadOnlyDictionary<object, object?>`, `CelType` | never |

`JsonElement` and unknown values are never a result, and an error is not a value of the result: the errors of an evaluation come as the list of errors of the result.

For `string?` and `byte[]?` the type at run time is `string` and `byte[]`: the annotation does not enter the type argument. A `null` is therefore allowed for a reference `T` by the row `null_type`.

### Typed nesting is one step

`X`, `XK` and `XV` are types of the rows other than `IReadOnlyList<…>` and `IReadOnlyDictionary<…>`. Deeper levels go through `CelList`, `CelMap`, `CelValue` and `object`, at any depth: `IReadOnlyList<CelList>` for `list(list(int))`. The types of the rows are `bool`, `long`, `int`, `ulong`, `uint`, `double`, `string`, `ReadOnlyMemory<byte>`, `byte[]`, the `Nullable<>` of those, `CelValue`, `object`, `CelList`, `CelMap` and `CelType`; `XK` is `long`, `int`, `ulong`, `uint`, `bool`, `string`, `object` or `CelValue`. A deeper typed `T`, such as `IReadOnlyList<IReadOnlyList<long>>`, is a type outside the rows: an `ArgumentException` for a result, and `ImplementationSignatureMismatch` for a parameter of a delegate. The rule is the same under JIT and under Native AOT, because a closed generic type that is known only at run time cannot be built without dynamic code, and a result must not depend on the mode.

### The check before a lazy view

A static type is no guarantee of the value: `[1] + dyn(['a'])` has the type `list(int)` and the value `[1, 'a']`. A lazy view would throw when its element is read after the result is returned, so a collection is converted by these rules:

- a view is built only when no element, key or value can fail its conversion by the pair of the CEL type of the root and the type `X`: `int` to `long`, `uint` to `ulong`, `string` to `string`, `dyn` to `object` and `dyn` to `CelValue` cannot fail; `int` to `int`, `uint` to `uint` and `dyn` to another `X` can. The pair is taken from the static type of the root, not from the expected type: a value of the type `list(dyn)` with `T = IReadOnlyList<long>` is a copy, and a string element is `ResultConversion`;
- only a collection of the library is viewed lazily: a literal, a result of an evaluation, `CelList.Create`, `CelMap.Create`. The copy of any other collection is made, because reading it runs code of the application;
- before a view, the value is checked at any depth against the expected type that the table below gives for `T`, not against the static type of the root, in one pass, without a charge. A value that does not fit is `ResultConversion`. The check names what the view reads: a position that the view gives as `object` or `CelValue` is `dyn`, and one that it gives as `CelList` or `CelMap` is `list(dyn)` or `map(dyn, dyn)`;
- `null` also fits a position that the view gives as `string`, `byte[]`, `CelList`, `CelMap` or `CelType`, because the row `null_type` reads it there as a `null` reference, as a copy does. A position of `ReadOnlyMemory<byte>`, `long`, `int` or another value type does not take `null`; `long?` and the other `Nullable<>` take it as a wrapper does;
- the type in the text of the error is the expected type without that allowance: `list(int)`, never a type with `null` in it. It is the type of the first element, key or value that does not fit, in the order of enumeration: `list(string)` for `x + y` with `T = IReadOnlyList<long>`.

So the check refuses where a copy would refuse and takes what a copy would convert, and the choice between a view and a copy never changes the result. With `T = IReadOnlyList<string>`, both `['a'] + dyn([null])` (a view) and `dyn(['a', null])` (a copy) give `['a', null]`; with `T = IReadOnlyList<ReadOnlyMemory<byte>>`, both `[b'a'] + dyn([null])` and `dyn([b'a', null])` are `ResultConversion`. At the root nothing is checked: `(['a'] + dyn([null]))[1]` with `T = string` is `null`. `T = object`, `CelValue`, `CelList` and `CelMap` are not checked: they rely on no static type.

## The expected type

The expected type of `Compile<T>` is taken from `T`. An expression whose type is not assignable to it by the rules of type checking is the compilation error `ExpectedTypeMismatch` (2003) with the position. `CelCompileOptions.ExpectedType` sets the type explicitly when `T` says too little, such as `list(int)` with `T = CelValue`; the explicit type must be compatible with `T`, that is, `T` is allowed for that CEL type by the table of the results. A pair that is not compatible, and a `T` outside the rows such as `short`, `Guid` or a class of the application, is an `ArgumentException`, thrown before the text is read. `CelProgram<T>.ResultType` is the CEL type of the result.

| `T` | Expected CEL type |
| :- | :- |
| `bool` | `bool` |
| `long`, `int` | `int` |
| `ulong`, `uint` | `uint` |
| `double` | `double` |
| `string` | `string` |
| `ReadOnlyMemory<byte>`, `byte[]` | `bytes` |
| `IReadOnlyList<X>` | `list(E')`, `E'` the expected type of `X` |
| `IReadOnlyDictionary<XK, XV>` | `map(K', V')` from `XK` and `XV` |
| `CelType` | `type` |
| `Nullable<X>` | the wrapper of the CEL type of `X`: `int?` and `long?` give `wrapper(int)`; `uint?` and `ulong?` give `wrapper(uint)`; `double?`, `bool?` and `ReadOnlyMemory<byte>?` give `wrapper(double)`, `wrapper(bool)` and `wrapper(bytes)` |
| `CelValue`, `object` | `dyn`: a result of any type |
| `CelList` | `list(dyn)` |
| `CelMap` | `map(dyn, dyn)` |
