using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The overloads of the extension sets among themselves and with the standard library (the page of the extension
// sets, "Versions and conflicts"). The sets of the catalog do not overlap: reverse of strings, of lists and of an
// application function of another receiver are three functions of different receivers, the overloads of string of
// network and of the standard library differ in the type of the argument, and all eight sets build together. An overload
// of a set of the application that overlaps one of a set of the catalog, or of another set of the application, is
// OverlappingOverloads, naming both overloads and their sources; one that overlaps the standard library
// is StandardLibraryConflict, as for a function the application declares itself. The sources are those of
// ExtensionBuildErrorTests, and the one that sorts first by its text comes first, so the message does not depend on the order of
// the calls.
public sealed class ExtensionOverlapTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    private const string Values = """
        'abc'.reverse() => 'cba'
        [1, 2, 3].reverse() => [3, 2, 1]
        string(ip('1.2.3.4')) => '1.2.3.4'
        string(cidr('10.1.2.3/8')) => '10.1.2.3/8'
        string(12) => '12'
        string(1.5) => '1.5'
        string(b'ab') => 'ab'
        """;

    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void AllSetsOfTheCatalog_BuildTogetherWithoutOverlap()
    {
        CelBuildResult result = Builder()
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(CelExtensions.Math(3))
            .AddExtension(CelExtensions.Lists(4))
            .AddExtension(CelExtensions.Sets(0))
            .AddExtension(CelExtensions.Encoders(1))
            .AddExtension(CelExtensions.Bindings(1))
            .AddExtension(CelExtensions.TwoVarComprehensions(0))
            .AddExtension(CelExtensions.Network(1))
            .Build();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    // reverse of strings (string) and of lists (list(T)) are both calls on a receiver, with receivers that do not
    // overlap, and string of network takes net.IP or net.CIDR, which are no type of the standard overloads of string.
    // Each call finds its own overload, whatever the order of the sets.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void SameNameInTwoSets_FindsItsOverloadByTheTypes()
    {
        CelExtension[][] orders =
        [
            [CelExtensions.Strings(5), CelExtensions.Lists(4), CelExtensions.Network(1)],
            [CelExtensions.Network(1), CelExtensions.Lists(4), CelExtensions.Strings(5)],
            [CelExtensions.Lists(4), CelExtensions.Network(1), CelExtensions.Strings(5)],
        ];
        foreach (CelExtension[] sets in orders)
        {
            CelEnvironmentBuilder builder = Builder();
            foreach (CelExtension set in sets)
            {
                builder.AddExtension(set);
            }

            Assert.Empty(ExtensionRows.CheckValues(builder.Build().GetEnvironmentOrThrow(), Values));
        }
    }

    // A reverse of an application on a receiver of a third kind overlaps nothing.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void OverloadOfAnotherReceiver_DoesNotOverlap()
    {
        CelExtension own = Own("acme", "reverse", CelOverloadDecl.Receiver("bytes_reverse", CelType.Bytes, [], CelType.Bytes));

        Assert.True(Builder().AddExtension(CelExtensions.Strings(5)).AddExtension(CelExtensions.Lists(4)).AddExtension(own).Build().IsSuccess);
    }

    // The overload of a set of the application that overlaps the overload of a set of the catalog after the type
    // parameters are erased: string and string, list(int) and list(T).
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-8")]
    public void OverloadOverlappingASetOfTheCatalog_IsOverlappingOverloads()
    {
        CelExtension text = Own("text", "reverse", CelOverloadDecl.Receiver("text_reverse", CelType.String, [], CelType.String));
        CelExtension ints = Own("ints", "reverse", CelOverloadDecl.Receiver("ints_reverse", CelType.ListOf(CelType.Int), [], CelType.ListOf(CelType.Int)));

        foreach (bool ownFirst in new[] { true, false })
        {
            AssertError(
                CelErrorCode.OverlappingOverloads,
                "overload 'text_reverse' of application overlaps overload 'string_reverse' of extension strings v5 of function 'reverse'",
                SingleError(Add(Builder(), ownFirst, text, CelExtensions.Strings(5))));
            AssertError(
                CelErrorCode.OverlappingOverloads,
                "overload 'ints_reverse' of application overlaps overload 'list_reverse' of extension lists v4 of function 'reverse'",
                SingleError(Add(Builder(), ownFirst, ints, CelExtensions.Lists(4))));
        }
    }

    // Two sets of the application, or a set and a function that the application declares in the builder, are both
    // "application": the smaller identifier comes first.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void OverloadsOfTwoSetsOfTheApplication_AreOverlappingOverloads()
    {
        CelExtension alpha = Own("alpha", "f", CelOverloadDecl.Global("alpha_f", [CelType.ListOf(CelType.TypeParameter("A"))], CelType.Int));
        CelExtension beta = Own("beta", "f", CelOverloadDecl.Global("beta_f", [CelType.ListOf(CelType.Int)], CelType.Int));
        const string Message = "overload 'alpha_f' of application overlaps overload 'beta_f' of application of function 'f'";

        AssertError(CelErrorCode.OverlappingOverloads, Message, SingleError(Builder().AddExtension(alpha).AddExtension(beta)));
        AssertError(CelErrorCode.OverlappingOverloads, Message, SingleError(Builder().AddExtension(beta).AddExtension(alpha)));

        CelEnvironmentBuilder withFunction = Builder()
            .AddExtension(CelExtensions.Strings(5))
            .AddFunction(new CelFunctionDecl("reverse", CelOverloadDecl.Receiver("app_reverse", CelType.String, [], CelType.String)))
            .AddFunctionImplementation("app_reverse", Echo);
        AssertError(
            CelErrorCode.OverlappingOverloads,
            "overload 'app_reverse' of application overlaps overload 'string_reverse' of extension strings v5 of function 'reverse'",
            SingleError(withFunction));
    }

    // An overload of a set of the application that overlaps one of the standard library is StandardLibraryConflict, the
    // same as an overload that the application declares with the builder.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void OverloadOverlappingTheStandardLibrary_IsStandardLibraryConflict()
    {
        CelExtension own = Own("sizes", "size", CelOverloadDecl.Global("size_text", [CelType.String], CelType.Int));

        foreach (bool ownFirst in new[] { true, false })
        {
            AssertError(
                CelErrorCode.StandardLibraryConflict,
                "overload 'size_text' of application conflicts with standard library v1: it overlaps overload 'size_string'",
                SingleError(Add(Builder(), ownFirst, own, CelExtensions.Strings(5))));
        }
    }

    // The errors of overlaps are reported with the others, in the order of the code and the text, the same in either
    // order of the calls.
    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-8")]
    public void OverlapsAndConflicts_AreTheSameInEitherOrder()
    {
        CelExtension text = Own("text", "reverse", CelOverloadDecl.Receiver("text_reverse", CelType.String, [], CelType.String));
        CelExtension sizes = Own("sizes", "size", CelOverloadDecl.Global("size_text", [CelType.String], CelType.Int));
        CelBuildResult forward = Builder().AddExtension(text).AddExtension(sizes).AddExtension(CelExtensions.Strings(5)).AddExtension(CelExtensions.Math(2)).Build();
        CelBuildResult reverse = Builder().AddExtension(CelExtensions.Math(2)).AddExtension(CelExtensions.Strings(5)).AddExtension(sizes).AddExtension(text).Build();

        Assert.Equal(
            new[]
            {
                (CelErrorCode.OverlappingOverloads, "overload 'text_reverse' of application overlaps overload 'string_reverse' of extension strings v5 of function 'reverse'"),
                (CelErrorCode.UnknownExtensionVersion, "extension 'math' has no version 2; available versions: 3"),
                (CelErrorCode.StandardLibraryConflict, "overload 'size_text' of application conflicts with standard library v1: it overlaps overload 'size_string'"),
            }.OrderBy(item => item.Item1).ThenBy(item => item.Item2, StringComparer.Ordinal),
            forward.Errors.Select(item => (item.Code, item.Message)));
        Assert.Equal(forward.Errors.Select(item => (item.Code, item.Message)), reverse.Errors.Select(item => (item.Code, item.Message)));
    }

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    // The two sets added in one order or the other.
    private static CelEnvironmentBuilder Add(CelEnvironmentBuilder builder, bool ownFirst, CelExtension own, CelExtension other) =>
        ownFirst ? builder.AddExtension(own).AddExtension(other) : builder.AddExtension(other).AddExtension(own);

    // A set of the application, version 1, with the one overload of the function.
    private static CelExtension Own(string name, string function, CelOverloadDecl overload) =>
        CelExtension.Create(
            name,
            1,
            [new CelFunctionDecl(function, overload)],
            new Dictionary<string, CelFunctionImplementation> { [overload.OverloadId] = Echo });

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
