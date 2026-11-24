# Versioning and compatibility

The six packages `Great.Parts.Cel`, `Great.Parts.Cel.Protobuf`, `Great.Parts.Cel.DependencyInjection`, `Great.Parts.Cel.AspNetCore`, `Great.Parts.Cel.Policy` and `Great.Parts.Cel.Filtering` follow [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html) and always have the same version. A breaking change comes only in a major version. This page says what is breaking, what a minor version may change, and what an application can rely on. Which versions get fixes is in [SECURITY.md](../SECURITY.md): versions 1.x get none, and from release 2.0 only the latest release does.

## What is breaking

A change is breaking, and comes only in a major version, when it is one of these:

- a change of the public API that breaks code written against it: a type or a member is removed or renamed, or its signature changes;
- an overload that makes a call written against an earlier version ambiguous (compiler error CS0121), for example a method with a second delegate type of the same shape, so that a lambda or an `async` lambda fits both, or an overload that takes an array next to one that takes a span; a form like that comes as a method with a new name, not as an overload, in a minor version;
- a different result, or a different error, for an expression that was valid in an earlier version;
- the end of support of a .NET version: the packages target `net10.0` and `net11.0`.

The public API of each package is recorded in the `PublicAPI` directory of its project in the repository, and the build fails when the code and the record differ, so a change of the API is always visible in a change of the repository.

## What a minor version may change

A minor or a patch version may:

- add types and members, functions of the extension sets as new versions of the sets, and error codes;
- make the text of the message of an error more precise: the contract of an error is the number, the name, the meaning and the stage of its code, not its text, see [Error codes](error-codes.md#rules-of-the-codes); a change of the text is described in the notes of the release;
- change the code that the source generator writes, as long as the values, the errors and the allocations of a read stay the same, see [What is contract](design/generator-output.md#what-is-contract);
- make an evaluation faster, or use less memory, without changing a result, an error or a cost.

One exception makes a result different in a minor version: a fix that makes the library agree with the CEL specification (cel-spec). Such a fix is described under `Behavior changes` in [CHANGELOG.md](../CHANGELOG.md), in the entry of its version.

## Removed and obsolete members

A member of the public API can be removed or replaced in a major version without an earlier `[Obsolete]` mark: Semantic Versioning allows it, and the change is described in [CHANGELOG.md](../CHANGELOG.md) in the entry of that version. A minor version can mark a member `[Obsolete]`, with the name of its replacement in the mark, and the mark breaks nothing.

## Classes that an application derives from

An application derives from `CelObjectType`, `CelObjectType<TInstance>`, `CelTypeProvider` and `CelTypeContext` to describe types of its own, and the code that the source generator writes derives from them too. A member that is added to any of these classes in a minor version is `virtual` and has an implementation, so a class compiled against an earlier minor version keeps working with a later one; a new `abstract` member, or a new constraint, is a breaking change. `CelList` and `CelMap` cannot be derived from outside the assembly: values of these types come from the factories of the library.

## What the created code calls

The code that the source generator of a version 2.x writes works with every later 2.x, so what it calls and overrides does not change or go away in a minor version. This is the protected members of the classes above that the code overrides or uses (`GetField(in TInstance, int)`, `HasField`, `FieldReadCost`, `GetExcludedFieldReason`, `IsConstructible`, `CreateInstance`, `CanSetField`, `IsRequiredField`, and the `From…` helpers and `FieldError` of `CelObjectType`); the constructors of `CelObjectField`, `CelEnumType` and `CelTypeContext`; the factories `CelValue.From…`, `CelList.CreateView`, `CreateCopy` and `CreateSorted`, and `CelMap.CreateView`; `CelAnonymousData.Register`, `CelEnvironmentBuilder.AddVariable<T>` and `CelBindings.Add<T>`; and the members that name a type or a value kind: `CelType.ObjectNamed`, `ListOf`, `MapOf`, `WrapperOf` and the scalar `CelType` members, `CelTimestamp.FromDateTimeOffset`, `CelDuration`, `CelValueKind.Null` and `Decimal`, and the error codes `CelErrorCode.InvalidArgument`, `UnexpectedNull` and `ConversionRange`. A new form of one of these comes as a new overload or a new member beside the old one, which stays. See [What is contract](design/generator-output.md#what-is-contract) for the form of the text.

## What does not change

- **Versions of the sets.** The declarations of a version of the standard library and of an extension set never change. A function that is added later comes as a new version of its set, so updating a package does not change the functions of an environment that names the versions it was built with. See [Standard library](standard-library.md#version-1) and [Extension sets](extensions.md#the-catalog).
- **The cost of an operation.** The units of the table on the page [Cost](cost.md) are the same in every version that has the operation; a row changes only in a major version.
- **The error codes.** A code keeps its number and its meaning, and a number is never used again.

Related pages: [Error codes](error-codes.md), [Cost](cost.md), [Extension sets](extensions.md), [CHANGELOG.md](../CHANGELOG.md).
