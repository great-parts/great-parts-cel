using System;
using System.Collections.Generic;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Declarations.ExtensionTestSets;

namespace Great.Parts.Cel.Tests.Declarations;

// The checks of Build over the extension sets of a builder: a version the catalog lacks (4004), one name in two versions
// (4003), two different sets with one name and version (4018), a dependency that no set meets (4005), and what the sets
// that remain join: overlaps, identifiers, names, implementations. Sets of the catalog are made by the tests.
public sealed class ExtensionChecksTests
{
    private static readonly CelFunctionImplementation Implementation = static arguments => CelValue.FromInt64(arguments.Length);

    private static readonly Dictionary<string, CelFunctionImplementation> NoImplementations = [];

    [Fact]
    public void UnknownVersion_IsOneErrorForTheSetWithTheVersionsOfTheCatalog()
    {
        CelEnvironmentBuilder builder = Builder().AddExtension(Missing("alpha", 7, 1, 2)).AddExtension(Of(Set("beta", 1), 1));

        Assert.Equal(["4004 extension 'alpha' has no version 7; available versions: 1, 2"], Errors(builder));
    }

    [Fact]
    public void UnknownVersionOfTheSameSetAddedTwice_IsOneError()
    {
        CelEnvironmentBuilder builder = Builder().AddExtension(Missing("alpha", 7, 1)).AddExtension(Missing("alpha", 7, 1));

        Assert.Equal(["4004 extension 'alpha' has no version 7; available versions: 1"], Errors(builder));
    }

    [Fact]
    public void UnknownVersions_AreOneErrorEachInTheOrderOfTheMessages()
    {
        CelEnvironmentBuilder builder = Builder().AddExtension(Missing("alpha", 9, 1)).AddExtension(Missing("alpha", 7, 1));

        Assert.Equal(
            ["4004 extension 'alpha' has no version 7; available versions: 1", "4004 extension 'alpha' has no version 9; available versions: 1"],
            Errors(builder));
    }

    [Fact]
    public void OneNameInTwoVersions_IsOneErrorWithTheSmallerVersionFirst()
    {
        CelEnvironmentBuilder builder = Builder().AddExtension(Of(Set("alpha", 2), 1, 2)).AddExtension(Of(Set("alpha", 1), 1, 2));

        Assert.Equal(["4003 extension 'alpha' is added in versions 1 and 2"], Errors(builder));
    }

    [Fact]
    public void OneNameInThreeVersions_IsAnErrorForEachPair()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddExtension(Of(Set("alpha", 3)))
            .AddExtension(Of(Set("alpha", 1)))
            .AddExtension(Of(Set("alpha", 2)));

        Assert.Equal(
            [
                "4003 extension 'alpha' is added in versions 1 and 2",
                "4003 extension 'alpha' is added in versions 1 and 3",
                "4003 extension 'alpha' is added in versions 2 and 3",
            ],
            Errors(builder));
    }

    // The sets that conflict are left out, so their overloads cause no second error.
    [Fact]
    public void SetsOfAVersionConflict_AreLeftOutOfTheOtherChecks()
    {
        CatalogExtension first = Set("alpha", 1, [Global(Source("alpha", 1), "f", "alpha1_f", [CelType.Int], CelType.Int)]);
        CatalogExtension second = Set("alpha", 2, [Global(Source("alpha", 2), "f", "alpha2_f", [CelType.Int], CelType.Int)]);

        Assert.Equal(["4003 extension 'alpha' is added in versions 1 and 2"], Errors(Builder().AddExtension(Of(first)).AddExtension(Of(second))));
    }

    [Fact]
    public void ApplicationSetWithTheNameOfACatalogSet_IsDuplicateExtensionNamingBothSources()
    {
        CelExtension application = CelExtension.Create("alpha", 1, [], NoImplementations);
        CelEnvironmentBuilder builder = Builder().AddExtension(Of(Set("alpha", 1), 1)).AddExtension(application);

        Assert.Equal(["4018 extension 'alpha' version 1 is added as two different sets: application and extension alpha v1"], Errors(builder));
    }

    [Fact]
    public void TwoApplicationSetsWithOneNameAndVersion_AreDuplicateExtension()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddExtension(CelExtension.Create("app", 3, [], NoImplementations))
            .AddExtension(CelExtension.Create("app", 3, [], NoImplementations));

        Assert.Equal(["4018 extension 'app' version 3 is added as two different sets: application and application"], Errors(builder));
    }

    [Fact]
    public void ApplicationSetOfAnotherVersion_IsVersionConflictNotDuplicate()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddExtension(CelExtension.Create("app", 3, [], NoImplementations))
            .AddExtension(CelExtension.Create("app", 4, [], NoImplementations));

        Assert.Equal(["4003 extension 'app' is added in versions 3 and 4"], Errors(builder));
    }

    [Fact]
    public void SameInstanceAddedTwice_IsOneSet()
    {
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("twice", CelOverloadDecl.Global("twice_int", [CelType.Int], CelType.Int))],
            new Dictionary<string, CelFunctionImplementation> { ["twice_int"] = Implementation });
        CelEnvironmentBuilder builder = Builder().AddExtension(application).AddExtension(application);

        Assert.Single(builder.Extensions);
        Assert.Single(BuildEnvironment(builder).Declarations.GetOverloads("twice", 1));
    }

    [Fact]
    public void SetOfTheCatalogAddedTwice_IsOneSetEvenFromTwoDefinitions()
    {
        CelEnvironmentBuilder builder = Builder().AddExtension(Of(Set("alpha", 1), 1)).AddExtension(Of(Set("alpha", 1), 1));

        Assert.Single(builder.Extensions);
        Assert.True(builder.Build().IsSuccess);
    }

    [Fact]
    public void MissingDependency_IsNamedFromTheSetThatAsks()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddExtension(CelExtension.Create("app", 2, [], NoImplementations, "alpha"))
            .AddExtension(Of(Set("beta", 1, dependencies: ["gamma"]), 1));

        Assert.Equal(
            [
                "4005 extension app v2 requires extension 'alpha', which is not added to the environment",
                "4005 extension beta v1 requires extension 'gamma', which is not added to the environment",
            ],
            Errors(builder));
    }

    [Fact]
    public void DependencyMetByAnyVersion_IsNoError()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddExtension(CelExtension.Create("app", 2, [], NoImplementations, "alpha"))
            .AddExtension(Of(Set("alpha", 9), 9));

        Assert.True(builder.Build().IsSuccess);
    }

    [Fact]
    public void DependencyOnASetThatWasLeftOut_IsMissing()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddExtension(Missing("alpha", 7, 1))
            .AddExtension(CelExtension.Create("app", 2, [], NoImplementations, "alpha"));

        Assert.Equal(
            [
                "4004 extension 'alpha' has no version 7; available versions: 1",
                "4005 extension app v2 requires extension 'alpha', which is not added to the environment",
            ],
            Errors(builder));
    }

    [Fact]
    public void TwoMissingDependencies_AreTwoErrors()
    {
        CelEnvironmentBuilder builder = Builder().AddExtension(CelExtension.Create("app", 2, [], NoImplementations, "alpha", "beta", "alpha"));

        Assert.Equal(
            [
                "4005 extension app v2 requires extension 'alpha', which is not added to the environment",
                "4005 extension app v2 requires extension 'beta', which is not added to the environment",
            ],
            Errors(builder));
    }

    // The order of the calls changes neither the list nor the text of an error: every permutation of five sets that raise all
    // four codes gives the same errors.
    [Fact]
    public void EveryOrderOfTheCalls_GivesTheSameErrors()
    {
        CelExtension[] sets =
        [
            Missing("alpha", 7, 1),
            Of(Set("beta", 1), 1, 2),
            Of(Set("beta", 2), 1, 2),
            CelExtension.Create("gamma", 1, [], NoImplementations, "delta"),
            Of(Set("gamma", 1), 1),
        ];
        string[]? expected = null;
        int permutations = 0;
        foreach (int[] order in Permutations(sets.Length))
        {
            CelEnvironmentBuilder builder = Builder();
            foreach (int index in order)
            {
                builder.AddExtension(sets[index]);
            }

            string[] errors = Errors(builder);
            expected ??= errors;
            Assert.Equal(expected, errors);
            permutations++;
        }

        Assert.Equal(120, permutations);
        Assert.NotNull(expected);
        Assert.Equal(
            [
                "4003 extension 'beta' is added in versions 1 and 2",
                "4004 extension 'alpha' has no version 7; available versions: 1",
                "4018 extension 'gamma' version 1 is added as two different sets: application and extension gamma v1",
            ],
            expected);
    }

    [Fact]
    public void OverlapOfASetAndAnApplicationSet_NamesBothSourcesWhateverTheOrder()
    {
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "f", "alpha_f", [CelType.Int], CelType.Int)]);
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("f", CelOverloadDecl.Global("app_f", [CelType.Int], CelType.Int))],
            new Dictionary<string, CelFunctionImplementation> { ["app_f"] = Implementation });
        const string Message = "4002 overload 'app_f' of application overlaps overload 'alpha_f' of extension alpha v1 of function 'f'";

        Assert.Equal([Message], Errors(Builder().AddExtension(Of(alpha)).AddExtension(application)));
        Assert.Equal([Message], Errors(Builder().AddExtension(application).AddExtension(Of(alpha))));
    }

    [Fact]
    public void OverlapOfTwoCatalogSets_NamesTheSetsInTheOrderOfTheirSources()
    {
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "f", "alpha_f", [CelType.ListOf(CelType.Int)], CelType.Int)]);
        CatalogExtension beta = Set("beta", 1, [Global(Source("beta", 1), "f", "beta_f", [CelType.ListOf(CelType.Dyn)], CelType.Int)]);

        Assert.Equal(
            ["4002 overload 'alpha_f' of extension alpha v1 overlaps overload 'beta_f' of extension beta v1 of function 'f'"],
            Errors(Builder().AddExtension(Of(beta)).AddExtension(Of(alpha))));
    }

    [Fact]
    public void OverloadsOfDifferentSetsThatDoNotOverlap_Build()
    {
        CatalogExtension alpha = Set(
            "alpha",
            1,
            [
                Global(Source("alpha", 1), "f", "alpha_f", [CelType.String], CelType.Int),
                Receiver(Source("alpha", 1), "g", "alpha_g", CelType.String, [], CelType.Int),
            ]);
        CatalogExtension beta = Set(
            "beta",
            1,
            [
                Global(Source("beta", 1), "f", "beta_f", [CelType.Bytes], CelType.Int),
                Receiver(Source("beta", 1), "g", "beta_g", CelType.Bytes, [], CelType.Int),
            ]);

        Assert.True(Builder().AddExtension(Of(alpha)).AddExtension(Of(beta)).Build().IsSuccess);
    }

    [Fact]
    public void OverloadOfASetThatOverlapsTheStandardLibrary_IsStandardLibraryConflict()
    {
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "size", "alpha_size", [CelType.String], CelType.Int)]);

        Assert.Equal(
            ["4006 overload 'alpha_size' of extension alpha v1 conflicts with standard library v1: it overlaps overload 'size_string'"],
            Errors(Builder().AddExtension(Of(alpha))));
    }

    [Fact]
    public void IdentifierOfASetUsedByTheApplication_IsDuplicateOverloadIdWhateverTheOrder()
    {
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "f", "shared_id", [CelType.Int], CelType.Int)]);
        CelFunctionDecl declaration = Function("g", CelOverloadDecl.Global("shared_id", [CelType.String], CelType.Int));
        const string Message = "4013 overload id 'shared_id' is used by both application and extension alpha v1";

        Assert.Equal([Message], Errors(Builder().AddExtension(Of(alpha)).AddFunction(declaration)));
        Assert.Equal([Message], Errors(Builder().AddFunction(declaration).AddExtension(Of(alpha))));
    }

    [Fact]
    public void IdentifierUsedByTwoSets_IsDuplicateOverloadIdNamingBothSets()
    {
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "f", "shared_id", [CelType.Int], CelType.Int)]);
        CatalogExtension beta = Set("beta", 1, [Global(Source("beta", 1), "g", "shared_id", [CelType.Int], CelType.Int)]);

        Assert.Equal(
            ["4013 overload id 'shared_id' is used by both extension alpha v1 and extension beta v1"],
            Errors(Builder().AddExtension(Of(beta)).AddExtension(Of(alpha))));
    }

    [Fact]
    public void ImplementationRegisteredForAnOverloadOfASet_IsDuplicateImplementation()
    {
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "f", "alpha_f", [CelType.Int], CelType.Int)]);

        Assert.Equal(
            ["4014 overload id 'alpha_f' has more than one implementation"],
            Errors(Builder().AddExtension(Of(alpha)).AddFunctionImplementation("alpha_f", Implementation)));
    }

    [Fact]
    public void FunctionNameOfAnApplicationSet_FollowsTheRulesOfTheApplicationAndTheCatalogIsExempt()
    {
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("a b", CelOverloadDecl.Global("bad_name", [CelType.Int], CelType.Int))],
            new Dictionary<string, CelFunctionImplementation> { ["bad_name"] = Implementation });
        CatalogExtension alpha = Set("alpha", 1, [Global(Source("alpha", 1), "math.@max", "alpha_max", [CelType.Int], CelType.Int)]);

        Assert.Equal(
            ["4015 function name 'a b' of application is not an identifier or a qualified name; operators and internal functions cannot be declared"],
            Errors(Builder().AddExtension(application)));
        Assert.True(Builder().AddExtension(Of(alpha)).Build().IsSuccess);
    }

    [Fact]
    public void OverloadOfAnApplicationSetWithoutAnImplementation_IsMissingImplementation()
    {
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int))],
            NoImplementations);

        Assert.Equal(["4007 overload 'f_int' of function 'f' has no implementation"], Errors(Builder().AddExtension(application)));
    }

    [Fact]
    public void ImplementationOfAnApplicationSetThatNoOverloadHas_IsUnusedImplementation()
    {
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int))],
            new Dictionary<string, CelFunctionImplementation> { ["f_int"] = Implementation, ["f_int2"] = Implementation });

        Assert.Equal(["4010 implementation 'f_int2' has no matching overload declaration"], Errors(Builder().AddExtension(application)));
    }

    // An implementation belongs to its set: the one that a builder registers does not serve an overload of a set.
    [Fact]
    public void ImplementationOfTheBuilder_DoesNotServeAnOverloadOfAnApplicationSet()
    {
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int))],
            NoImplementations);
        CelEnvironmentBuilder builder = Builder().AddExtension(application).AddFunctionImplementation("f_int", Implementation);

        Assert.Equal(
            ["4007 overload 'f_int' of function 'f' has no implementation", "4014 overload id 'f_int' has more than one implementation"],
            Errors(builder));
    }

    [Fact]
    public void FunctionOfAnApplicationSetWithTheFormOfAStandardMacro_IsStandardLibraryConflict()
    {
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("all", CelOverloadDecl.Receiver("all_list", CelType.ListOf(CelType.Int), [CelType.String, CelType.Bool], CelType.Bool))],
            new Dictionary<string, CelFunctionImplementation> { ["all_list"] = Implementation });

        Assert.Equal(
            ["4006 function 'all' of application conflicts with standard library v1: it has the form of the macro 'all'"],
            Errors(Builder().AddExtension(application)));
    }

    [Fact]
    public void FunctionOfTheApplicationWithTheFormOfAMacroOfASet_IsStandardLibraryConflictNamingTheSet()
    {
        Macro sortBy = new("sortBy", null, 2, isReceiver: true, static (_, _) => MacroCheck.Expand, static (nodes, _, _, offset) => nodes.Constant(CelValue.Null, offset));
        CatalogExtension alpha = Set("alpha", 1, macros: [sortBy, Successor(), Counting()]);
        CelFunctionDecl receiver = Function("sortBy", CelOverloadDecl.Receiver("sort_by", CelType.ListOf(CelType.Int), [CelType.String, CelType.Int], CelType.Int));
        CelFunctionDecl qualified = Function("test.succ", CelOverloadDecl.Global("succ_int", [CelType.Int], CelType.Int));
        CelFunctionDecl any = Function("test.count", CelOverloadDecl.Global("count_four", [CelType.Int, CelType.Int, CelType.Int, CelType.Int], CelType.Int));

        CelEnvironmentBuilder builder = Builder()
            .AddExtension(Of(alpha))
            .AddFunction(receiver)
            .AddFunction(qualified)
            .AddFunction(any)
            .AddFunctionImplementation("sort_by", Implementation)
            .AddFunctionImplementation("succ_int", Implementation)
            .AddFunctionImplementation("count_four", Implementation);

        Assert.Equal(
            [
                "4006 function 'sortBy' of application conflicts with extension alpha v1: it has the form of the macro 'sortBy'",
                "4006 function 'test.count' of application conflicts with extension alpha v1: it has the form of the macro 'count'",
                "4006 function 'test.succ' of application conflicts with extension alpha v1: it has the form of the macro 'succ'",
            ],
            Errors(builder));
    }

    [Fact]
    public void FunctionWithAnotherCallStyleOrCountThanAMacro_IsNoConflict()
    {
        Macro sortBy = new("sortBy", null, 2, isReceiver: true, static (_, _) => MacroCheck.Expand, static (nodes, _, _, offset) => nodes.Constant(CelValue.Null, offset));
        CatalogExtension alpha = Set("alpha", 1, macros: [sortBy]);
        CelFunctionDecl global = Function("sortBy", CelOverloadDecl.Global("sort_by_global", [CelType.Int, CelType.Int], CelType.Int));
        CelFunctionDecl otherCount = Function("sortBy", CelOverloadDecl.Receiver("sort_by_one", CelType.ListOf(CelType.Int), [CelType.String], CelType.Int));

        CelBuildResult result = Builder()
            .AddExtension(Of(alpha))
            .AddFunction(global)
            .AddFunction(otherCount)
            .AddFunctionImplementation("sort_by_global", Implementation)
            .AddFunctionImplementation("sort_by_one", Implementation)
            .Build();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TypeOfAnApplicationSetThatNothingDeclares_IsTypeNotDeclared()
    {
        CelExtension application = CelExtension.Create(
            "app",
            1,
            [Function("f", CelOverloadDecl.Global("f_unknown", [CelType.ObjectNamed("acme.Unknown")], CelType.Int))],
            new Dictionary<string, CelFunctionImplementation> { ["f_unknown"] = Implementation });

        Assert.Equal(
            ["4008 overload 'f_unknown' has type 'acme.Unknown', which no type source or extension of the environment declares"],
            Errors(Builder().AddExtension(application)));
    }

    [Fact]
    public void AbstractTypeOfASet_JoinsTheTypeIdentifiersAndConflictsWithATypeOfAProvider()
    {
        CatalogExtension alpha = Set("alpha", 1, abstractTypes: [new AbstractTypeDeclaration("alpha.Thing", CelType.Bytes)]);
        CelObjectType thing = CelObjectTypeBuilder.ForDictionary("alpha.Thing").Build();
        CelTypeProvider provider = CelTypeProvider.Create("types", [thing]);

        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(Of(alpha)));
        Assert.True(environment.Declarations.TryGetTypeIdentifier("alpha.Thing", out CelType? type));
        Assert.Equal(CelType.Bytes, type);
        Assert.Equal(
            ["4001 type 'alpha.Thing' is declared by both extension alpha v1 and type source 'types'"],
            Errors(Builder().AddExtension(Of(alpha)).AddTypes(provider)));
    }

    [Fact]
    public void AbstractTypeOfASetNamedAsAStandardIdentifier_IsDuplicateType()
    {
        CatalogExtension alpha = Set("alpha", 1, abstractTypes: [new AbstractTypeDeclaration("int", CelType.Bytes)]);

        Assert.Equal(["4001 type 'int' is declared by both standard library v1 and extension alpha v1"], Errors(Builder().AddExtension(Of(alpha))));
    }

    [Fact]
    public void AbstractTypeDeclaredByTwoSets_IsDuplicateTypeInTheOrderOfTheSources()
    {
        CatalogExtension alpha = Set("alpha", 1, abstractTypes: [new AbstractTypeDeclaration("shared.Thing", CelType.Bytes)]);
        CatalogExtension beta = Set("beta", 1, abstractTypes: [new AbstractTypeDeclaration("shared.Thing", CelType.Bytes)]);

        Assert.Equal(
            ["4001 type 'shared.Thing' is declared by both extension alpha v1 and extension beta v1"],
            Errors(Builder().AddExtension(Of(beta)).AddExtension(Of(alpha))));
    }

    [Fact]
    public void VariableOfASet_IsADeclarationOfTheEnvironmentAndConflictsWithTheApplication()
    {
        CatalogExtension alpha = Set("alpha", 1, variables: [new CelVariableDecl("alpha_slot", CelType.Dyn)]);

        Assert.True(BuildEnvironment(Builder().AddExtension(Of(alpha))).Declarations.TryGetVariable("alpha_slot", out CelVariableDecl? variable));
        Assert.Equal(CelType.Dyn, variable.Type);
        Assert.Equal(
            ["4000 variable 'alpha_slot' is declared by both application and extension alpha v1"],
            Errors(Builder().AddExtension(Of(alpha)).AddVariable("alpha_slot", CelType.Int)));
    }

    [Fact]
    public void ApplicationSetCopiesItsArguments()
    {
        CelFunctionDecl[] functions = [Function("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int))];
        Dictionary<string, CelFunctionImplementation> implementations = new() { ["f_int"] = Implementation };
        string[] dependencies = ["alpha"];
        CelExtension application = CelExtension.Create("app", 1, functions, implementations, dependencies);

        functions[0] = Function("g", CelOverloadDecl.Global("g_int", [CelType.Int], CelType.Int));
        implementations.Clear();
        dependencies[0] = "beta";

        Assert.Equal(
            ["4005 extension app v1 requires extension 'alpha', which is not added to the environment"],
            Errors(Builder().AddExtension(application)));
        Assert.Equal("app", application.Name);
        Assert.Equal(1u, application.Version);
        Assert.Equal(
            "f",
            BuildEnvironment(Builder().AddExtension(application).AddExtension(Of(Set("alpha", 1)))).Declarations.GetOverloads("f", 1)[0].Function);
    }

    [Fact]
    public void Create_RefusesNullsAndEmptyNames()
    {
        Assert.Throws<ArgumentNullException>(() => CelExtension.Create(null!, 1, [], NoImplementations));
        Assert.Throws<ArgumentException>(() => CelExtension.Create("", 1, [], NoImplementations));
        Assert.Throws<ArgumentNullException>(() => CelExtension.Create("app", 1, [], null!));
        Assert.Throws<ArgumentNullException>(() => CelExtension.Create("app", 1, [null!], NoImplementations));
        Assert.Throws<ArgumentNullException>(() => CelExtension.Create("app", 1, [], new Dictionary<string, CelFunctionImplementation> { ["a"] = null! }));
        Assert.Throws<ArgumentNullException>(() => CelExtension.Create("app", 1, [], NoImplementations, [null!]));
        Assert.Throws<ArgumentException>(() => CelExtension.Create("app", 1, [], NoImplementations, ""));
        Assert.Throws<ArgumentNullException>(() => Builder().AddExtension(null!));
    }

    [Fact]
    public void ToBuilder_KeepsTheSetsAndBuildsTheSameEnvironment()
    {
        CelExtension alpha = Of(Set("alpha", 1, [Global(Source("alpha", 1), "f", "alpha_f", [CelType.Int], CelType.Int)]), 1);
        CelEnvironment environment = BuildEnvironment(Builder().AddExtension(alpha));

        CelEnvironmentBuilder again = environment.ToBuilder();

        Assert.Equal(new[] { alpha }, again.Extensions);
        Assert.Single(BuildEnvironment(again).Declarations.GetOverloads("f", 1));
        Assert.Equal(["4018 extension 'alpha' version 1 is added as two different sets: application and extension alpha v1"],
            Errors(again.AddExtension(CelExtension.Create("alpha", 1, [], NoImplementations))));
    }

    [Fact]
    public void ExtensionsOfTheBuilder_AreOrderedByNameThenVersion()
    {
        CelExtension alpha1 = Of(Set("alpha", 1));
        CelExtension alpha2 = Of(Set("alpha", 2));
        CelExtension beta = Of(Set("beta", 1));

        CelEnvironmentBuilder builder = Builder().AddExtension(beta).AddExtension(alpha2).AddExtension(alpha1).AddExtension(alpha2);

        Assert.Equal(new[] { alpha1, alpha2, beta }, builder.Extensions);
    }

    // Every ordering of 0..count-1.
    private static IEnumerable<int[]> Permutations(int count)
    {
        int[] current = new int[count];
        bool[] used = new bool[count];
        return Permute(current, used, 0);
    }

    private static IEnumerable<int[]> Permute(int[] current, bool[] used, int position)
    {
        if (position == current.Length)
        {
            yield return [.. current];
            yield break;
        }

        for (int i = 0; i < current.Length; i++)
        {
            if (used[i])
            {
                continue;
            }

            used[i] = true;
            current[position] = i;
            foreach (int[] permutation in Permute(current, used, position + 1))
            {
                yield return permutation;
            }

            used[i] = false;
        }
    }
}
