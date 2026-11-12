using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The set optional in the builder (the page of the extension sets, "Versions and conflicts" and "optional"): the set has the name
// optional and the one version 2 in this release, which is the latest of cel-go v0.32.0, so a version below or above it
// is UnknownExtensionVersion at Build, "extension 'optional' has no version <n>; available versions: 2" (optFlatMap, first,
// last, optional.unwrap and unwrapOpt are of versions 1 and 2 in cel-go; they are all there in the version of this
// release). The set declares the abstract type optional_type, so a variable of that type needs the set whichever call comes
// first, and without it the declaration is TypeNotDeclared (4008) with the hint that names the set. The set conflicts with
// other sets of its name as the sets of the catalog do (ExtensionBuildErrorTests): another version is
// ExtensionVersionConflict, another set of the application with the same version DuplicateExtension. A build error has the stage Build
// and no place in an expression.
[Trait("Requirement", "F-3")]
[Trait("Requirement", "S-2")]
[Trait("Requirement", "F-8")]
public sealed class OptionalExtensionBuildTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    [Fact]
    public void Set_HasItsNameAndItsVersion()
    {
        CelExtension extension = CelExtensions.Optional(2);

        Assert.Equal(("optional", 2u), (extension.Name, extension.Version));
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(extension).Build().IsSuccess);
    }

    // The method does not check the version: a set that has no such version is asked for, and the error is that of Build, which
    // lists the versions that exist. Versions 0 and 1 exist in cel-go and are not in this release (the register of known
    // differences has no entry for them: any number is a version in cel-go, and an error here).
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(3u)]
    [InlineData(uint.MaxValue)]
    public static void VersionThatTheSetDoesNotHave_IsUnknownExtensionVersion(uint asked)
    {
        CelExtension extension = CelExtensions.Optional(asked);
        Assert.Equal(("optional", asked), (extension.Name, extension.Version));

        CelError error = SingleError(Builder().AddExtension(extension));

        AssertError(
            CelErrorCode.UnknownExtensionVersion,
            "extension 'optional' has no version " + asked.ToString(CultureInfo.InvariantCulture) + "; available versions: 2",
            error);
    }

    [Fact]
    public void SameSetAddedAgain_IsOneSet()
    {
        Assert.True(Builder().AddExtension(CelExtensions.Optional(2)).AddExtension(CelExtensions.Optional(2)).Build().IsSuccess);
    }

    // A variable of the type optional_type(int) is declared by the extension: it builds with the set, in either order of the
    // calls, and without it the error is TypeNotDeclared at the variable, which names the set that the application lacks.
    [Fact]
    public void VariableOfAnOptionalType_NeedsTheSet_InEitherOrderOfTheCalls()
    {
        CelType optional = CelType.OptionalOf(CelType.Int);

        Assert.True(Builder().AddExtension(CelExtensions.Optional(2)).AddVariable("o", optional).Build().IsSuccess);
        Assert.True(Builder().AddVariable("o", optional).AddExtension(CelExtensions.Optional(2)).Build().IsSuccess);

        CelError error = SingleError(Builder().AddVariable("o", optional));

        Assert.Equal((CelErrorCode.TypeNotDeclared, CelErrorStage.Build), (error.Code, error.Stage));
        Assert.StartsWith("variable 'o' has type 'optional_type", error.Message, StringComparison.Ordinal);
        Assert.Contains("which no type source or extension of the environment declares", error.Message, StringComparison.Ordinal);
        Assert.EndsWith("; add the extension set 'optional'", error.Message, StringComparison.Ordinal);
        Assert.Equal(default(CelSourceLocation), error.Location);
    }

    // The name optional_type is the type of the set too: a list or a map of optionals needs it, wherever the optional is.
    [Fact]
    public void OptionalTypeInsideAnotherType_NeedsTheSet()
    {
        CelType list = CelType.ListOf(CelType.OptionalOf(CelType.String));
        CelType map = CelType.MapOf(CelType.String, CelType.OptionalOf(CelType.Int));

        Assert.True(Builder().AddExtension(CelExtensions.Optional(2)).AddVariable("a", list).AddVariable("b", map).Build().IsSuccess);
        Assert.All(Builder().AddVariable("a", list).AddVariable("b", map).Build().Errors, error => Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code));
    }

    [Fact]
    public void SetInAnotherVersion_IsExtensionVersionConflict()
    {
        const string Message = "extension 'optional' is added in versions 1 and 2";

        AssertError(CelErrorCode.ExtensionVersionConflict, Message, SingleError(Builder().AddExtension(CelExtensions.Optional(2)).AddExtension(Own("optional", 1))));
        AssertError(CelErrorCode.ExtensionVersionConflict, Message, SingleError(Builder().AddExtension(Own("optional", 1)).AddExtension(CelExtensions.Optional(2))));
    }

    [Fact]
    public void DifferentSetOfTheSameNameAndVersion_IsDuplicateExtension()
    {
        const string Message = "extension 'optional' version 2 is added as two different sets: application and extension optional v2";

        AssertError(CelErrorCode.DuplicateExtension, Message, SingleError(Builder().AddExtension(CelExtensions.Optional(2)).AddExtension(Own("optional", 2))));
        AssertError(CelErrorCode.DuplicateExtension, Message, SingleError(Builder().AddExtension(Own("optional", 2)).AddExtension(CelExtensions.Optional(2))));
    }

    // The set brings no dependency: it builds alone, with no other set, and with every set of the catalog.
    [Fact]
    public void Set_BuildsAloneAndWithTheOtherSets()
    {
        Assert.True(Builder().AddExtension(CelExtensions.Optional(2)).Build().IsSuccess);
        Assert.True(Builder()
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(CelExtensions.Math(3))
            .AddExtension(CelExtensions.Lists(4))
            .AddExtension(CelExtensions.Sets(0))
            .AddExtension(CelExtensions.Encoders(1))
            .AddExtension(CelExtensions.Bindings(1))
            .AddExtension(CelExtensions.TwoVarComprehensions(0))
            .AddExtension(CelExtensions.Network(1))
            .AddExtension(CelExtensions.Optional(2))
            .Build()
            .IsSuccess);
    }

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    // A set of the application with one function, <name>Probe(string) -> string.
    private static CelExtension Own(string name, uint version) =>
        CelExtension.Create(
            name,
            version,
            [new CelFunctionDecl(name + "Probe", CelOverloadDecl.Global(name + "_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation> { [name + "_probe_string"] = Echo });

    private static CelError SingleError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    private static void AssertError(CelErrorCode code, string message, CelError error)
    {
        Assert.Equal((code, CelErrorStage.Build, message), (error.Code, error.Stage, error.Message));
        Assert.Equal(default(CelSourceLocation), error.Location);
        Assert.Null(error.SourceLocation);
    }
}
