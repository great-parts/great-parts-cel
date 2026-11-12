using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Protobuf;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Properties.Examples.Declarations;

// The order of the calls of the builder when the environment has providers of protobuf (the property of the order of the
// calls, written for protobuf): the calls AddTypes with a provider, AddVariable with the class of a message, UseContainer
// and AddExtension with the set protos give the same environment in every order, with or without repeated calls — the same
// results of a fixed set of texts: compile results, values and errors; and two providers that share a file give the same build errors,
// with the same texts and in the same order, whichever is added first. The test makes every order of the calls of one
// environment (120 orders of five calls), which is a case of the property that the generator of calls of the
// property test draws at random.
[Trait("Requirement", "+R-8")]
[Trait("Requirement", "F-2")]
public sealed class ProtobufBuilderOrderTests
{
    private static readonly string[] Texts =
    [
        "x.single_int32 == 5",
        "has(x.single_int32)",
        "x.single_any.bb == 5",
        "TestAllTypes{single_int32: 1} == TestAllTypes{single_int32: 1}",
        "TestAllTypes{single_int32: 3000000000} == TestAllTypes{}",
        "proto.hasExt(p2x, cel.expr.conformance.proto2.int32_ext)",
        "proto.getExt(p2x, cel.expr.conformance.proto2.int32_ext) == 7",
        "proto.getExt(x, cel.expr.conformance.proto2.int32_ext) == 7",
        "x.missing == 1",
        "type(x) == TestAllTypes",
    ];

    private static readonly CelProtobufTypes Provider = new CelProtobufTypesBuilder("messages")
        .AddFile(Proto3.TestAllTypesReflection.Descriptor)
        .AddFile(Proto2.TestAllTypesExtensionsReflection.Descriptor)
        .AddProtoExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext)
        .Build();

    private static readonly Action<CelEnvironmentBuilder>[] Calls =
    [
        builder => builder.AddTypes(Provider),
        builder => builder.AddVariable<Proto3.TestAllTypes>("x"),
        builder => builder.AddVariable<Proto2.TestAllTypes>("p2x"),
        builder => builder.UseContainer("cel.expr.conformance.proto3"),
        builder => builder.AddExtension(CelExtensions.Protos(0)),
    ];

    private static readonly CelBindings Bindings = new CelBindings()
        .Add("x", new Proto3.TestAllTypes { SingleInt32 = 5, SingleAny = Any.Pack(new Proto3.TestAllTypes.Types.NestedMessage { Bb = 5 }) })
        .Add("p2x", WithExtension());

    [Fact]
    public void EveryOrderOfTheCalls_GivesTheSameEnvironment()
    {
        int[] model = [0, 1, 2, 3, 4];
        string[] expected = Snapshot(model);
        Assert.Equal("built", expected[0]);
        Assert.Equal("x.single_int32 == 5 => True", expected[1]);

        var different = new List<string>();
        foreach (int[] order in Permutations(model))
        {
            string[] actual = Snapshot(order);
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            {
                different.Add(string.Join(' ', order) + ": " + string.Join(" | ", actual.Except(expected)));
            }
        }

        Assert.Empty(different);
    }

    // A call made twice, and a provider that is added again, change nothing.
    [Fact]
    public void RepeatedCalls_GiveTheSameEnvironment()
    {
        string[] expected = Snapshot([0, 1, 2, 3, 4]);

        Assert.Equal(expected, Snapshot([0, 0, 1, 2, 3, 4]));
        Assert.Equal(expected, Snapshot([4, 3, 2, 1, 0, 0, 4, 1]));
        Assert.Equal(expected, Snapshot([1, 0, 2, 4, 3, 0, 0]));
    }

    // Two providers that share a file are DuplicateType in every order of the calls, with the same texts in the same order.
    [Fact]
    public void TwoProvidersThatShareAFile_GiveTheSameBuildErrorsInEveryOrder()
    {
        CelProtobufTypes first = new CelProtobufTypesBuilder("first").AddFile(Proto3.TestAllTypesReflection.Descriptor).Build();
        CelProtobufTypes second = new CelProtobufTypesBuilder("second").AddFile(Proto3.TestAllTypesReflection.Descriptor).Build();
        Action<CelEnvironmentBuilder>[] two =
        [
            builder => builder.AddTypes(first),
            builder => builder.AddTypes(second),
            builder => builder.AddVariable<Proto3.TestAllTypes>("x"),
        ];

        string[]? expected = null;
        foreach (int[] order in Permutations([0, 1, 2]))
        {
            var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
            foreach (int index in order)
            {
                two[index](builder);
            }

            CelBuildResult result = builder.Build();
            Assert.False(result.IsSuccess);
            string[] actual = [.. result.Errors.Select(error => (int)error.Code + " " + error.Message)];

            expected ??= actual;
            Assert.Equal(expected, actual);
        }

        Assert.Contains("4001 type 'cel.expr.conformance.proto3.TestAllTypes' is declared by both type source 'first' and type source 'second'", expected!);
    }

    // What the environment of the calls gives: the result of Build, then each text with its compile errors or its value or error.
    private static string[] Snapshot(int[] order)
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);
        foreach (int index in order)
        {
            Calls[index](builder);
        }

        CelBuildResult result = builder.Build();
        if (!result.IsSuccess)
        {
            return [.. result.Errors.Select(error => (int)error.Code + " " + error.Message)];
        }

        CelEnvironment environment = result.GetEnvironmentOrThrow();
        var lines = new List<string> { "built" };
        foreach (string text in Texts)
        {
            CelCompileResult<bool> compiled = environment.Compile<bool>(text);
            if (!compiled.IsSuccess)
            {
                lines.Add(text + " => compile " + string.Join(", ", compiled.Errors.Select(Shown)));
                continue;
            }

            CelEvalResult<bool> evaluated = compiled.GetProgramOrThrow().Evaluate(Bindings, cancellationToken: CancellationToken.None);
            lines.Add(text + " => " + (evaluated.IsSuccess ? evaluated.GetValueOrThrow().ToString() : "eval " + string.Join(", ", evaluated.Errors.Select(Shown))));
        }

        return [.. lines];
    }

    private static string Shown(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)error.Code}@{error.Location.Offset} {error.Message}");

    private static Proto2.TestAllTypes WithExtension()
    {
        var message = new Proto2.TestAllTypes();
        message.SetExtension(Proto2.TestAllTypesExtensionsExtensions.Int32Ext, 7);
        return message;
    }

    private static IEnumerable<int[]> Permutations(int[] items)
    {
        if (items.Length <= 1)
        {
            yield return items;
            yield break;
        }

        for (int index = 0; index < items.Length; index++)
        {
            int[] rest = [.. items.Where((_, position) => position != index)];
            foreach (int[] tail in Permutations(rest))
            {
                yield return [items[index], .. tail];
            }
        }
    }
}
