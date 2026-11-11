using System;
using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The order of the builder calls does not change the environment: the same sets added in another order give the same
// results of the expressions, the same errors with the same texts and places, and the same macros. The examples add
// all eight sets of the catalog in several orders (the order of the list, the reverse, two rotations and an
// interleaving), and then the sets with variables and functions of the application in two orders. Each order is checked
// against the values that the table states, not only against another order, so an environment that is wrong in the
// same way in every order does not pass. The errors are compared as the whole list: code, text, stage and place.
public sealed class ExtensionOrderTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    private const string Rows = """
        'abc'.reverse() => 'cba'
        [1, 2, 3].reverse() => [3, 2, 1]
        'a,b'.split(',') => ['a', 'b']
        'abc'.charAt(1) => 'b'
        math.greatest(1, 3, 2) => 3
        math.sqrt(16) => 4.0
        [3, 1, 2].sort() => [1, 2, 3]
        [3, 1, 2].sortBy(e, -e) => [3, 2, 1]
        lists.range(3) => [0, 1, 2]
        sets.contains([1, 2, 3], [1, 2]) => true
        base64.encode(b'abc') => 'YWJj'
        json.encode([1, 'a']) => '[1,"a"]'
        cel.bind(x, 2, x * x) => 4
        [1, 2, 3].all(i, v, v > i) => true
        [1, 2, 3].all(v, v > 0) => true
        {'a': 1}.transformMap(k, v, v + 1) => {'a': 2}
        isIP('1.2.3.4') => true
        string(ip('1.2.3.4')) => '1.2.3.4'
        cidr('10.0.0.0/8').containsIP('10.1.2.3') => true
        string(12) => '12'
        """;

    // The orders in which the eight sets, numbered as in the list below, are added.
    private static readonly int[][] Orders =
    [
        [0, 1, 2, 3, 4, 5, 6, 7],
        [7, 6, 5, 4, 3, 2, 1, 0],
        [3, 4, 5, 6, 7, 0, 1, 2],
        [5, 6, 7, 0, 1, 2, 3, 4],
        [0, 4, 1, 5, 2, 6, 3, 7],
    ];

    private static CelExtension[] Catalog() =>
    [
        CelExtensions.Strings(5),
        CelExtensions.Math(3),
        CelExtensions.Lists(4),
        CelExtensions.Sets(0),
        CelExtensions.Encoders(1),
        CelExtensions.Bindings(1),
        CelExtensions.TwoVarComprehensions(0),
        CelExtensions.Network(1),
    ];

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void AllSetsInAnyOrder_GiveTheSameResults()
    {
        foreach (int[] order in Orders)
        {
            CelExtension[] sets = Catalog();
            CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
            foreach (int index in order)
            {
                builder.AddExtension(sets[index]);
            }

            Assert.Empty(ExtensionRows.CheckValues(builder.Build().GetEnvironmentOrThrow(), Rows));
        }
    }

    // Every pair of sets of the catalog, in both orders: it builds, and the pair does not change the other sets' functions.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void EveryPairOfSets_BuildsInBothOrders()
    {
        CelExtension[] sets = Catalog();
        for (int first = 0; first < sets.Length; first++)
        {
            for (int second = 0; second < sets.Length; second++)
            {
                if (first == second)
                {
                    continue;
                }

                CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
                    .AddExtension(sets[first])
                    .AddExtension(sets[second])
                    .Build();

                Assert.True(result.IsSuccess, first.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", " + second.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    // The calls of the builder of every kind in another order: the sets, a variable, a function of the application, and a set
    // of the application that needs a set of the catalog.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void SetsVariablesAndFunctions_InAnyOrder_GiveTheSameResults()
    {
        const string Mixed = """
            acmeProbe('x') => 'x'
            triple(n) => 15
            'abc'.reverse() == 'cba' && [1, 2].reverse() == [2, 1] => true
            math.greatest(n, 3) => 5
            """;
        CelExtension own = CelExtension.Create(
            "acme",
            1,
            [new CelFunctionDecl("acmeProbe", CelOverloadDecl.Global("acme_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation> { ["acme_probe_string"] = Echo },
            "strings");
        var function = new CelFunctionDecl("triple", CelOverloadDecl.Global("triple_int", [CelType.Int], CelType.Int));
        Func<CelEnvironmentBuilder, CelEnvironmentBuilder>[] steps =
        [
            builder => builder.AddExtension(own),
            builder => builder.AddVariable<long>("n"),
            builder => builder.AddExtension(CelExtensions.Lists(4)),
            builder => builder.AddFunction(function),
            builder => builder.AddFunctionImplementation("triple_int", arguments => CelValueOf(arguments[0], 3)),
            builder => builder.AddExtension(CelExtensions.Math(3)),
            builder => builder.AddExtension(CelExtensions.Strings(5)),
        ];
        var bindings = new CelBindings().Add("n", 5L);

        foreach (IEnumerable<Func<CelEnvironmentBuilder, CelEnvironmentBuilder>> order in new[] { steps, steps.Reverse(), steps.Skip(3).Concat(steps.Take(3)) })
        {
            CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
            foreach (Func<CelEnvironmentBuilder, CelEnvironmentBuilder> step in order)
            {
                builder = step(builder);
            }

            Assert.Empty(ExtensionRows.CheckValues(builder.Build().GetEnvironmentOrThrow(), Mixed, bindings));
        }
    }

    // The errors of conflicting sets are the same list, whole, in every order: the code, the text, the stage and the
    // absent place. Seven calls of the builder in several orders; the sources are named by their text, not by the
    // order of the calls.
    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-4")]
    public void ConflictingSets_GiveTheSameErrorsInAnyOrder()
    {
        CelExtension own = CelExtension.Create(
            "strings",
            5,
            [new CelFunctionDecl("stringsProbe", CelOverloadDecl.Global("strings_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation> { ["strings_probe_string"] = Echo });
        CelExtension acme1 = Own("acme", 1);
        CelExtension acme2 = Own("acme", 2);
        CelExtension report = Own("report", 1, "network");
        Func<CelExtension>[] additions =
        [
            () => own,
            () => CelExtensions.Strings(5),
            () => acme1,
            () => acme2,
            () => report,
            () => CelExtensions.Math(2),
            () => CelExtensions.Lists(4),
        ];
        int[][] orders =
        [
            [0, 1, 2, 3, 4, 5, 6],
            [6, 5, 4, 3, 2, 1, 0],
            [3, 1, 4, 0, 6, 2, 5],
            [1, 0, 3, 2, 5, 4, 6],
        ];

        string[] expected =
        [
            "ExtensionVersionConflict Build 4003 extension 'acme' is added in versions 1 and 2",
            "UnknownExtensionVersion Build 4004 extension 'math' has no version 2; available versions: 3",
            "MissingExtensionDependency Build 4005 extension report v1 requires extension 'network', which is not added to the environment",
            "DuplicateExtension Build 4018 extension 'strings' version 5 is added as two different sets: application and extension strings v5",
        ];

        foreach (int[] order in orders)
        {
            CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
            foreach (int index in order)
            {
                builder.AddExtension(additions[index]());
            }

            CelBuildResult result = builder.Build();

            Assert.False(result.IsSuccess);
            Assert.Equal(
                expected,
                result.Errors.Select(error => error.Code + " " + error.Stage + " " + ((int)error.Code).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + error.Message));
            Assert.All(result.Errors, error => Assert.Equal(default(CelSourceLocation), error.Location));
        }
    }

    private static CelValue CelValueOf(CelValue argument, long factor) =>
        argument.TryGetInt64(out long number) ? CelValue.FromInt64(number * factor) : argument;

    private static CelExtension Own(string name, uint version, params string[] dependencies) =>
        CelExtension.Create(
            name,
            version,
            [new CelFunctionDecl(name + "Probe", CelOverloadDecl.Global(name + "_probe_string", [CelType.String], CelType.String))],
            new Dictionary<string, CelFunctionImplementation> { [name + "_probe_string"] = Echo },
            dependencies);
}
