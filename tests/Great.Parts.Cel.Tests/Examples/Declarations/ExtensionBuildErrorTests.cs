using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The errors of the extension sets at Build (the page of the extension sets, "Versions and conflicts"): a version that the
// set does not have, a set in two versions, two different sets with one name and one version, a set whose
// dependency is not in the environment. A build error has the stage Build and no place in an expression. The source
// of a set of the catalog is named "extension strings v5"; a set that the application builds with CelExtension.Create is
// "application"; the one that sorts first by its text, ordinal, comes first in a pair, so the text does not
// depend on the order of the calls. The set that a test builds has one function, <name>Probe(string), whose
// overload and delegate are the same in all of its versions, so two versions of it declare the same overload, which is
// one declaration and no conflict of overloads.
public sealed class ExtensionBuildErrorTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    // The sets of the catalog by their name in an environment description, with the one version each has.
    private static readonly (string Name, uint Version)[] Sets =
    [
        ("strings", 5),
        ("math", 3),
        ("lists", 4),
        ("sets", 0),
        ("encoders", 1),
        ("bindings", 1),
        ("two-var-comprehensions", 0),
        ("network", 1),
    ];

    public static TheoryData<string, uint> Catalog
    {
        get
        {
            var data = new TheoryData<string, uint>();
            foreach ((string name, uint version) in Sets)
            {
                data.Add(name, version);
            }

            return data;
        }
    }

    // For each set, the versions that it does not have: the one below and the one above (not for the version 0, which
    // has none below), 0 where that is not the version, and the largest. A version that cel-go would accept
    // is among them, since it takes any number (the register of known differences has no entry for it).
    public static TheoryData<string, uint, uint> UnknownVersions
    {
        get
        {
            var data = new TheoryData<string, uint, uint>();
            foreach ((string name, uint version) in Sets)
            {
                var bad = new SortedSet<uint> { version + 1, 0, uint.MaxValue };
                if (version > 0)
                {
                    bad.Add(version - 1);
                }

                bad.Remove(version);
                foreach (uint other in bad)
                {
                    data.Add(name, version, other);
                }
            }

            return data;
        }
    }

    [Theory]
    [Trait("Requirement", "F-3")]
    [MemberData(nameof(Catalog))]
    public static void SetOfTheCatalog_HasItsNameAndItsVersion(string name, uint version)
    {
        CelExtension extension = FromCatalog(name, version);

        Assert.Equal((name, version), (extension.Name, extension.Version));
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(extension).Build().IsSuccess);
    }

    // The method of the catalog does not check the version: a set that has no such version is asked for, and the
    // error is that of Build, which lists the versions that exist.
    [Theory]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "S-2")]
    [Trait("Requirement", "F-8")]
    [MemberData(nameof(UnknownVersions))]
    public static void VersionThatTheSetDoesNotHave_IsUnknownExtensionVersion(string name, uint known, uint asked)
    {
        CelExtension extension = FromCatalog(name, asked);
        Assert.Equal((name, asked), (extension.Name, extension.Version));

        CelError error = SingleError(Builder().AddExtension(extension));

        AssertError(
            CelErrorCode.UnknownExtensionVersion,
            "extension '" + name + "' has no version " + asked.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "; available versions: " + known.ToString(System.Globalization.CultureInfo.InvariantCulture),
            error);
    }

    // The set that has no such version is left out of the other checks: it is one error, not also a conflict of versions
    // with the version that exists, and two unknown versions are two errors, ordered by their text.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void SetWithAnUnknownVersion_IsLeftOutOfTheOtherChecks()
    {
        CelError error = SingleError(Builder().AddExtension(CelExtensions.Strings(5)).AddExtension(CelExtensions.Strings(4)));
        AssertError(CelErrorCode.UnknownExtensionVersion, "extension 'strings' has no version 4; available versions: 5", error);

        CelBuildResult both = Builder().AddExtension(CelExtensions.Strings(6)).AddExtension(CelExtensions.Strings(4)).Build();

        Assert.Equal(
            new[]
            {
                (CelErrorCode.UnknownExtensionVersion, "extension 'strings' has no version 4; available versions: 5"),
                (CelErrorCode.UnknownExtensionVersion, "extension 'strings' has no version 6; available versions: 5"),
            },
            both.Errors.Select(item => (item.Code, item.Message)));
    }

    // One set in two versions: two sets that the application builds, or a set of the catalog and a set that the
    // application builds under the same name. The versions are named in ascending order, in whatever order they
    // were added.
    [Theory]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-8")]
    [InlineData("acme", 1u, 2u, "extension 'acme' is added in versions 1 and 2")]
    [InlineData("acme", 7u, 3u, "extension 'acme' is added in versions 3 and 7")]
    [InlineData("acme", 0u, 4294967295u, "extension 'acme' is added in versions 0 and 4294967295")]
    public static void SetInTwoVersions_IsExtensionVersionConflict(string name, uint first, uint second, string message)
    {
        AssertError(
            CelErrorCode.ExtensionVersionConflict,
            message,
            SingleError(Builder().AddExtension(Own(name, first)).AddExtension(Own(name, second))));
        AssertError(
            CelErrorCode.ExtensionVersionConflict,
            message,
            SingleError(Builder().AddExtension(Own(name, second)).AddExtension(Own(name, first))));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void SetOfTheCatalogAndASetOfTheApplicationInAnotherVersion_IsExtensionVersionConflict()
    {
        const string Message = "extension 'strings' is added in versions 4 and 5";

        AssertError(
            CelErrorCode.ExtensionVersionConflict,
            Message,
            SingleError(Builder().AddExtension(CelExtensions.Strings(5)).AddExtension(Own("strings", 4))));
        AssertError(
            CelErrorCode.ExtensionVersionConflict,
            Message,
            SingleError(Builder().AddExtension(Own("strings", 4)).AddExtension(CelExtensions.Strings(5))));
    }

    // The same version again is one set: the same instance, or the same set of the catalog, however many times it is
    // added.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void SameSetAddedAgain_IsOneSet()
    {
        CelExtension own = Own("acme", 1);

        Assert.True(Builder().AddExtension(own).AddExtension(own).Build().IsSuccess);
        Assert.True(Builder().AddExtension(CelExtensions.Strings(5)).AddExtension(CelExtensions.Strings(5)).AddExtension(CelExtensions.Strings(5)).Build().IsSuccess);
        Assert.True(Builder()
            .AddExtension(CelExtensions.Network(1))
            .AddExtension(CelExtensions.Math(3))
            .AddExtension(CelExtensions.Network(1))
            .AddExtension(CelExtensions.Math(3))
            .Build()
            .IsSuccess);
    }

    // Two different sets with one name and one version: a set of the catalog and a set that the application built under its
    // name, or two sets of the application. The sources are named, the smaller text first.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-8")]
    public void TwoDifferentSetsOfOneNameAndVersion_AreDuplicateExtension()
    {
        const string WithTheCatalog = "extension 'strings' version 5 is added as two different sets: application and extension strings v5";
        const string TwoOwn = "extension 'acme' version 1 is added as two different sets: application and application";

        AssertError(
            CelErrorCode.DuplicateExtension,
            WithTheCatalog,
            SingleError(Builder().AddExtension(CelExtensions.Strings(5)).AddExtension(Own("strings", 5))));
        AssertError(
            CelErrorCode.DuplicateExtension,
            WithTheCatalog,
            SingleError(Builder().AddExtension(Own("strings", 5)).AddExtension(CelExtensions.Strings(5))));
        AssertError(
            CelErrorCode.DuplicateExtension,
            TwoOwn,
            SingleError(Builder().AddExtension(Own("acme", 1)).AddExtension(Own("acme", 1))));
    }

    // The name of a set of the catalog is allowed to a set of the application, as long as no set of the catalog of
    // that name and version is added too; a set of the catalog of another version is the conflict of versions.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void SetOfTheApplicationUnderTheNameOfACatalogSet_BuildsAlone()
    {
        Assert.True(Builder().AddExtension(Own("strings", 5)).Build().IsSuccess);
        Assert.True(Builder().AddExtension(Own("math", 3)).AddExtension(CelExtensions.Strings(5)).Build().IsSuccess);
    }

    // A set whose dependency is not in the environment: the text names both, the set that asks as "extension <name> v<version>".
    // Dependencies are never added by themselves, and a name needs a set of that name, of the catalog or of the
    // application, in any version.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-8")]
    public void SetWithoutItsDependency_IsMissingExtensionDependency()
    {
        AssertError(
            CelErrorCode.MissingExtensionDependency,
            "extension report v1 requires extension 'strings', which is not added to the environment",
            SingleError(Builder().AddExtension(Own("report", 1, "strings"))));
        AssertError(
            CelErrorCode.MissingExtensionDependency,
            "extension report v3 requires extension 'nosuchset', which is not added to the environment",
            SingleError(Builder().AddExtension(Own("report", 3, "nosuchset"))));
        AssertError(
            CelErrorCode.MissingExtensionDependency,
            "extension report v1 requires extension 'base', which is not added to the environment",
            SingleError(Builder().AddExtension(Own("report", 1, "base")).AddExtension(Own("other", 1))));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void SeveralMissingDependencies_AreSeveralErrorsOrderedByTheirText()
    {
        CelBuildResult result = Builder().AddExtension(Own("report", 1, "strings", "math")).Build();

        Assert.Equal(
            new[]
            {
                (CelErrorCode.MissingExtensionDependency, "extension report v1 requires extension 'math', which is not added to the environment"),
                (CelErrorCode.MissingExtensionDependency, "extension report v1 requires extension 'strings', which is not added to the environment"),
            },
            result.Errors.Select(item => (item.Code, item.Message)));
        Assert.All(result.Errors, item => Assert.Equal((CelErrorStage.Build, default(CelSourceLocation)), (item.Stage, item.Location)));
    }

    // The dependency is met by a set of that name in either order of the calls: of the catalog, or of the application.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void SetWithItsDependency_BuildsInEitherOrder()
    {
        Assert.True(Builder().AddExtension(Own("report", 1, "strings")).AddExtension(CelExtensions.Strings(5)).Build().IsSuccess);
        Assert.True(Builder().AddExtension(CelExtensions.Strings(5)).AddExtension(Own("report", 1, "strings")).Build().IsSuccess);
        Assert.True(Builder().AddExtension(Own("report", 1, "base")).AddExtension(Own("base", 2)).Build().IsSuccess);
        Assert.True(Builder().AddExtension(Own("report", 1, "base", "strings")).AddExtension(CelExtensions.Strings(5)).AddExtension(Own("base", 1)).Build().IsSuccess);
    }

    // A dependency that is a set with an unknown version is no set that was added: the set is left out, and the one
    // that asks for it reports what it lacks. The errors are ordered by code, then by text, so 4004 comes first.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void DependencyThatHasAnUnknownVersion_IsNotMet()
    {
        CelBuildResult result = Builder().AddExtension(Own("report", 1, "strings")).AddExtension(CelExtensions.Strings(4)).Build();

        Assert.Equal(
            new[]
            {
                (CelErrorCode.UnknownExtensionVersion, "extension 'strings' has no version 4; available versions: 5"),
                (CelErrorCode.MissingExtensionDependency, "extension report v1 requires extension 'strings', which is not added to the environment"),
            },
            result.Errors.Select(item => (item.Code, item.Message)));
    }

    // The functions of a set of the application are those of an application declaration: a call finds the overload
    // and runs its implementation.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void FunctionOfASetOfTheApplication_Runs()
    {
        CelEnvironment environment = Builder().AddExtension(Own("acme", 1)).Build().GetEnvironmentOrThrow();

        CelValue value = environment.Compile<CelValue>("acmeProbe('x')").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

        Assert.True(value.TryGetString(out string? text) && text == "x");
    }

    // An overload without an implementation in the set is no error of Create but of Build, as for a function declared by
    // the builder; the set that Create returns is the same set after that.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void SetOfTheApplicationWithoutAnImplementation_IsMissingImplementationAtBuild()
    {
        CelExtension extension = CelExtension.Create(
            "acme",
            1,
            [new CelFunctionDecl("acmeProbe", CelOverloadDecl.Global("acme_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation>());

        CelError error = SingleError(Builder().AddExtension(extension));

        Assert.Equal((CelErrorCode.MissingImplementation, CelErrorStage.Build), (error.Code, error.Stage));
        Assert.Equal("overload 'acme_probe_string' of function 'acmeProbe' has no implementation", error.Message);
    }

    // Every error of several sets is reported, by code and then by text, and the reverse order of the calls gives
    // the same list.
    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-8")]
    public void ErrorsOfSeveralSets_AreTheSameInEitherOrder()
    {
        CelBuildResult forward = Builder()
            .AddExtension(Own("acme", 1))
            .AddExtension(Own("acme", 2))
            .AddExtension(CelExtensions.Math(2))
            .AddExtension(Own("report", 1, "network"))
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(Own("strings", 5))
            .Build();
        CelBuildResult reverse = Builder()
            .AddExtension(Own("strings", 5))
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(Own("report", 1, "network"))
            .AddExtension(CelExtensions.Math(2))
            .AddExtension(Own("acme", 2))
            .AddExtension(Own("acme", 1))
            .Build();

        Assert.Equal(
            new[]
            {
                (CelErrorCode.ExtensionVersionConflict, "extension 'acme' is added in versions 1 and 2"),
                (CelErrorCode.UnknownExtensionVersion, "extension 'math' has no version 2; available versions: 3"),
                (CelErrorCode.MissingExtensionDependency, "extension report v1 requires extension 'network', which is not added to the environment"),
                (CelErrorCode.DuplicateExtension, "extension 'strings' version 5 is added as two different sets: application and extension strings v5"),
            }.OrderBy(item => item.Item1).ThenBy(item => item.Item2, StringComparer.Ordinal),
            forward.Errors.Select(item => (item.Code, item.Message)));
        Assert.Equal(forward.Errors.Select(item => (item.Code, item.Message)), reverse.Errors.Select(item => (item.Code, item.Message)));
    }

    private static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);

    private static CelExtension FromCatalog(string name, uint version) => name switch
    {
        "strings" => CelExtensions.Strings(version),
        "math" => CelExtensions.Math(version),
        "lists" => CelExtensions.Lists(version),
        "sets" => CelExtensions.Sets(version),
        "encoders" => CelExtensions.Encoders(version),
        "bindings" => CelExtensions.Bindings(version),
        "two-var-comprehensions" => CelExtensions.TwoVarComprehensions(version),
        "network" => CelExtensions.Network(version),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    // A set of the application with one function, <name>Probe(string) -> string.
    private static CelExtension Own(string name, uint version, params string[] dependencies) =>
        CelExtension.Create(
            name,
            version,
            [new CelFunctionDecl(name + "Probe", CelOverloadDecl.Global(name + "_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation> { [name + "_probe_string"] = Echo },
            dependencies);

    private static CelError SingleError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        Assert.Throws<InvalidOperationException>(() => result.Environment);
        return Assert.Single(result.Errors);
    }

    private static void AssertError(CelErrorCode code, string message, CelError error)
    {
        Assert.Equal((code, CelErrorStage.Build, message), (error.Code, error.Stage, error.Message));
        Assert.Equal(default(CelSourceLocation), error.Location);
        Assert.Null(error.SourceLocation);
    }
}
