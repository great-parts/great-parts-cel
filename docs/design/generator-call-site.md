# Generator of call sites: anonymous data of `CelExpression.Evaluate`

The second pipeline of the source generator of the package `Great.Parts.Cel`. For each anonymous type passed as `data` to `CelExpression.Evaluate<T>(string, object, …)` it writes one file that registers the type with `CelAnonymousData.Register` from a module initializer, so that the simple entry ([simple-entry](simple-entry.md)) reads the properties without reflection and works under Native AOT. For the classes, structures and interfaces of the application in the properties it also writes their descriptions (see "Descriptions of classes"). The rules of the property types are `docs/type-mapping.md` ("Data of the simple entry"); the diagnostics are `GPCEL010` and `GPCEL011` of `docs/generator-diagnostics.md`, `GPCEL008` for the language version, and the diagnostics of a type context for the descriptions.

| File | Content |
| :- | :- |
| `src/Great.Parts.Cel.Generator/CelAnonymousDataGenerator.cs` | the `IIncrementalGenerator`: predicate, transform, outputs |
| `src/Great.Parts.Cel.Generator/CallSites/CallSiteModel.cs` | the value records of the pipeline |
| `src/Great.Parts.Cel.Generator/CallSites/CallSiteReader.cs` | the predicate and the transform: the call found by the symbol of the method, the argument `data` by position or name, each property against the rules |
| `src/Great.Parts.Cel.Generator/CallSites/PropertyRules.cs` | the types of the list: which property types the simple entry reads without a description, how each is bound, and the reason for the others |
| `src/Great.Parts.Cel.Generator/CallSites/ClassProperties.cs` | the classes of the application that the properties of one anonymous object reach, read by the code of the type context (`Reading/`) |
| `src/Great.Parts.Cel.Generator/CallSites/ClassDescriptions.cs` | the closures of all calls put together for the assembly: one description for each type, the limit of the closure, the names of the created classes |
| `src/Great.Parts.Cel.Generator/CallSites/RegistrationEmitter.cs` | the text of a registration file |
| `src/Great.Parts.Cel.Generator/CallSites/DescriptionEmitter.cs` | the text of the file of descriptions, written with the code of the type context (`Emit/`) |
| `src/Great.Parts.Cel.Generator/Emit/CallSiteNames.cs` | the names of the file of descriptions and of the converters that the registrations use |
| `src/Great.Parts.Cel.Generator/Diagnostics/Descriptors.cs` | the descriptors `GPCEL010` and `GPCEL011`, beside the existing ones |

The generator is `[Generator(LanguageNames.CSharp)] public sealed class CelAnonymousDataGenerator` — public, as `CelTypeContextGenerator`: the generator tests create it directly and the generator assembly gives no `InternalsVisibleTo`; the analyzer assembly is not referenced by the code of the application, so the class is not a public API of a package, beside `CelTypeContextGenerator` in the same assembly and package (`analyzers/dotnet/cs`). The two pipelines share the model and the output of the descriptions of types (`Reading/`, `Emit/`), the helpers of names, `LocationInfo` and the facts of the compilation (`Model/CompilationFacts.cs`: the language version).

## The pipeline

1. **Predicate** (syntax only): an `InvocationExpressionSyntax` whose expression is a name `Evaluate` — a simple name, a generic name or a member access ending in one — with at least two arguments.
2. **Transform** (semantic model): the symbol of the invocation is the method `Great.Parts.Cel.CelExpression.Evaluate<T>(string, object, ulong, CancellationToken)` (compared with the symbol of the compilation, not by text), so a full name, `using static` and a `using` alias are found and a method `Evaluate` of another type is not. The argument bound to the parameter `data` (by position or by name) gives the static type by `GetTypeInfo(…).Type`.
3. The static type gives one of three results:
   - an anonymous type: its properties in declaration order, each read against the list of the values, then against the classes of the application ("Descriptions of classes"), or a `GPCEL011` for each property that is outside both, at the property — the result is a registration model and the closure of its classes, or the diagnostic models;
   - a dictionary of the list (`IReadOnlyDictionary<string, X>`, `Dictionary<string, X>`, `X` of the list, found through the interfaces of the type): nothing;
   - anything else — a type parameter, `object`, a named class or structure, a dictionary outside the list: a `GPCEL010` model.
4. **Model**: records with value equality, no `ISymbol`, `SyntaxNode` or `Location`: `AnonymousShape(EquatableArray<PropertyModel>, EquatableArray<string>, EquatableArray<string>)` with `PropertyModel` with the name and the C# type of the property, how it is bound (generic `Add<T>`, the factory of `CelValue`, or the shape of a collection of classes), and the CEL names of the object types and enumerations that the properties reach; `ClassClosure`, the models of those types as the type context writes them (`ObjectTypeModel`, `EnumModel`) with the names of the created classes left empty; a diagnostic model with the id, the arguments and a `LocationInfo` kept apart (the same record as in the type context pipeline), so an edit above a call that moves it does not change the shape or the models. The steps `CallSite`, `Shapes`, `CallSiteDiagnostics`, `ClassDescriptions`, `ClassDescriptionsModel` and `ClassDescriptionDiagnostics` are named for tracking.
5. **Output**: the shapes are collected, made distinct by value (names, types, order: the key of C# for one anonymous type), and numbered in the order of their first call site by file path and position; each distinct shape gives `CelAnonymousData_{n}.g.cs`. The closures of all calls are put together in the same order into the model of the file of descriptions, which gives `CelCallSiteTypes.g.cs`. Diagnostics are reported from their own models.
6. **Language version**: the created files use file-local classes and primary constructors, which need C# 11 and C# 12. Below C# 12 — the threshold of the type context pipeline — the pipeline writes no file and reports `GPCEL008` once for the compilation, at the first call it found (by file path and position), with `{0}` `CelExpression.Evaluate`.

## Descriptions of classes

A property whose type is a class, a structure or an interface of the application (also an array, `List<X>`, `IReadOnlyList<X>`, `ImmutableArray<X>`, `Dictionary<string, X>`, `IReadOnlyDictionary<string, X>` of one) is read by the rules of a type context with the default options: the naming policy `CamelCase`, the attributes of `System.Text.Json` honoured, the public instance members. The pipeline builds the model of the closure of the types the property reaches with the code of the type context pipeline (`TypeClassifier`, `MemberReader`, `ModelBuilder`), and the output writes:

- one file `CelCallSiteTypes.g.cs` for the assembly, an internal class with one description (`CelObjectType<TInstance>`) for each type reached by any call site, at most 250 types (`GPCEL009` with `{0}` `CelExpression.Evaluate`);
- for each shape with such properties, a type source with the descriptions that its properties reach, and in the declare delegate `builder.AddTypes(source)` before the variables.

The diagnostics of the context (`GPCEL002`, `GPCEL003`, `GPCEL004`, `GPCEL005`, `GPCEL009`) are reported at the property of the anonymous object through which the type is reached when the member has no location in the compilation. A type that a context cannot describe is `GPCEL011` with the reason "a type that a type context cannot describe" and the reason of `GPCEL001`.

A type that two properties of one object, or two calls, reach is one description. Two types of one CEL name are `GPCEL004` at the second; more than 250 types in the assembly are `GPCEL009` at the call that passes the limit, and then no description and no registration that needs one is written. The 250 types are counted for the whole assembly, not for one call, since the file of descriptions is one.

The class `CelCallSiteTypes` is internal and in the global namespace; each registration is a file-local class that names the descriptions it needs, and the descriptions are the same objects for every form, so a description that two forms share is created once and an environment of one form holds the same instance as the environment of another.

## The created file

```text
// <auto-generated/>
#nullable disable
file static class CelAnonymousData_1
{
    [global::System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Register() =>
        global::Great.Parts.Cel.CelAnonymousData.Register(
            new { x = default(long), name = default(string) },
            static builder => builder.AddVariable<long>("x").AddVariable<string>("name"),
            static (data, bindings) => bindings
                .Add("x", global::Great.Parts.Cel.Values.CelValue.FromInt64(data.x))
                .Add("name", data.name is null ? global::Great.Parts.Cel.Values.CelValue.Null : global::Great.Parts.Cel.Values.CelValue.FromString(data.name)));
}
```

(Here the chain of `Add` is broken over lines; the created text has the whole chain on one line.) The example expression uses `default(T)` with the C# type as the compiler displays it with `global::`, so the created anonymous type is the type of the call site (same names, types, order). `#nullable disable` lets `default(string)` stand for `string?` and `string` alike. A property name that is a C# keyword is written with `@` (`data.@class`), and the CEL name is the property name without `@`; a name that is not a CEL identifier is `GPCEL011` with the reason "not a CEL identifier": the generator has its own copy of the rule of `CelSyntax.IsIdentifier`, since the analyzer cannot call the library. The variable is declared with `AddVariable<TProperty>`, which gives the CEL type through the input table of the core, so the generator writes no CEL type itself for a property of the list.

**Direct translation.** A property whose type a row of the type mapping turns into a value without a failure — `bool`, the integers up to 64 bits, `double`, `string`, `Guid`, `CelList`, `CelMap` and the `Nullable<>` of the numbers and of `bool` — is bound with the factory of `CelValue` that the row calls, and with the same handling of `null` as the row: a `null` string, list or map and a `Nullable<>` without a value are `CelValue.Null`. `CelValue` itself is bound as it is. Every other row is bound by `Add<T>`. The test of the generator compares both paths at the values of the limits of each row.

**Classes.** A single class, structure or interface, and a `Nullable<>` of a structure, is declared and bound by `AddVariable<T>` and `Add<T>` as before; the object is kept by reference and read lazily. The form has a source of types:

```text
#nullable enable annotations
file static class CelAnonymousData_1
{
    private sealed class TypeSource()
        : global::Great.Parts.Cel.Values.CelTypeProvider(
            "CelAnonymousData_1",
            [global::CelCallSiteTypes.Type_Acme_Line.Description],
            []);

    private static readonly TypeSource Types = new();
    ...
            static builder => builder.AddTypes(Types).AddVariable<global::Acme.Line>("line").AddVariable("lines", global::Great.Parts.Cel.Types.CelType.ListOf(global::Great.Parts.Cel.Types.CelType.ObjectNamed("Acme.Line"))),
            static (data, bindings) => bindings.Add("line", data.line).Add("lines", data.lines is null ? global::CelCallSiteTypes.Unexpected() : global::Great.Parts.Cel.Values.CelValue.FromList(global::Great.Parts.Cel.Values.CelList.CreateView<global::Acme.Line>(data.lines, global::CelCallSiteTypes.ConvertNotNull_Acme_Line))));
}
```

A collection of classes or structures (a list, an array, `ImmutableArray<X>`, a dictionary with `string` keys) is not bound by `Add<T>`: its closed list of collections cannot name a dictionary of classes or a list of structures, and a closed generic type cannot be built at run time under Native AOT. The created code declares the variable with the `CelType` of the shape and binds it with a lazy view closed over the type of the element, with the code of the type context for a member that is a collection (`Emit/ValueReader.cs`, which has a mode for a call site). The view of an `ImmutableArray<X>` goes through `ImmutableCollectionsMarshal.AsArray`, so nothing is copied; a `default` array and a `null` collection are `UnexpectedNull`.

The converter of an element is one static method of the file of descriptions for each type, not a lambda at each call: `Convert_Acme_Line` for a place that its annotation allows to be `null` (or that has no annotation, in a project without the nullable context), where `null` is the null value, and `ConvertNotNull_Acme_Line` for a place that does not, where `null` is an error value `UnexpectedNull`. A structure has the first only. Both pair the description of the **declared** type with the instance (`CelValue.FromObject(description, instance)`), so an instance of a derived class is read by the members of the declared type and a member that only the derived class has is `NoSuchField` at checking; a nested collection is a lambda over the method of its element. The error value comes from a description of no type that the file holds (`NullOwner`): only a description can make the error value of a null member, and none of the application is the owner of a property of an anonymous object.

The file of descriptions:

```text
internal static class CelCallSiteTypes
{
    internal sealed class Type_Acme_Line : global::Great.Parts.Cel.Values.CelObjectType<global::Acme.Line> { ... }   // as in a type context
    internal static CelValue ConvertNotNull_Acme_Line(Line e) => e is null ? Unexpected() : CelValue.FromObject(Type_Acme_Line.Description, e);
    internal static CelValue Unexpected() => NullOwner.Instance.Error();
    private sealed class NullOwner : CelObjectType<object> { ... }
}
```

`#nullable enable annotations` stands in the registration of a form with a collection of classes, since the type of an element may be annotated `?`; the warnings of the nullable analysis stay off in created code. A form without such a collection keeps `#nullable disable`.

## Diagnostics

| Code | When | Place | Text arguments |
| :- | :- | :- | :- |
| `GPCEL010` (Warning) | the static type of `data` is not anonymous and not a listed dictionary | the argument `data` | `{0}` the type as C# displays it; `{1}` the advice: for a named class or structure "pass it as a property of an anonymous object, such as new { order }, or build an environment with a type source and UseInput<T>", for a type parameter or `object` "pass the anonymous object at the call or pass a dictionary", for a dictionary outside the list "pass a dictionary with string keys and values of the list, or an anonymous object" |
| `GPCEL011` (Error) | a property of an anonymous type has a type outside the list, or a name that is not a CEL identifier | the property in the anonymous object creation expression | `{0}` the property name; `{1}` the type as C# displays it; `{2}` the reason (`a type that a type context cannot describe` with the reason of `GPCEL001`, `a nested anonymous object`, `object`, `not accessible from the created file`, `a type outside the list`, `a type parameter of the enclosing method`, `a collection outside the list`, `not a CEL identifier`) and the advice "; build an environment with a type source", or "; rename the property" for a name that is not a CEL identifier |
| `GPCEL008` (Error) | the language version of the project is below C# 12 | the first call found | `{0}` `CelExpression.Evaluate`; `{1}` the version |
| `GPCEL002`, `GPCEL003`, `GPCEL004`, `GPCEL005`, `GPCEL009` | as for a type context, for the types that the properties reach | the member; the property that reached the type when the member has no place in the compilation | `{0}` of `GPCEL004` and `GPCEL009` is `CelExpression.Evaluate` |

A shape with a `GPCEL011` property gives no file, and neither does its closure. A call through a method group (`Func<string, object, long> f = CelExpression.Evaluate<long>;`) is not an invocation of the method and is not seen; at run time it works when the same anonymous type is registered by another call of the assembly, and otherwise it is `TypeNotDeclared`.

## Tests

The examples (`tests/Great.Parts.Cel.Generator.Tests/Examples/CallSites/CallSiteTests.cs` and `ClassCallSiteTests.cs`, `tests/Great.Parts.Cel.Tests/Examples/Generated/AnonymousDataTests.cs` and `ClassPropertyTests.cs`) fix one file per shape, the created text, the diagnostics with place and message, and the call at run time under the JIT and Native AOT: the generator is an analyzer of `tests/Great.Parts.Cel.Tests`. The developer tests take the rest: the cached steps (`trackIncrementalGeneratorSteps`, an edit above the call keeps the steps of the shapes and of the descriptions `Unchanged`), every row of `PropertyRules` and its binding, the escaping of keywords, `using static` and an alias, named arguments, the numbering across files, the language version, the absence of reflection in the created text, the snapshot of the registration and of the file of descriptions, the file of descriptions for the assembly and the source of types for each form (a type of another assembly, a type that a member reaches, a list and a dictionary of classes, nested collections, a nullable structure, an enumeration, an interface and an abstract class), the diagnostics of the context at the call, the limit of 250 types for one call and for two, and the values of the simple entry for each kind of property at run time, with the values of the direct translation compared with `Add<T>`. `tests/Great.Parts.Cel.Wasm.App` calls the simple entry with a class and a list of classes and references the generator as an analyzer (`OutputItemType="Analyzer"`, `ReferenceOutputAssembly="false"`, as `tests/Great.Parts.Cel.Tests` does): without the reference there is no registration, and the publication would not analyze the created code. The application is not run; the check is the publication with 0 warnings IL2xxx and IL3xxx.

Related pages: [generator](generator.md), [generator-output](generator-output.md), [simple-entry](simple-entry.md).
