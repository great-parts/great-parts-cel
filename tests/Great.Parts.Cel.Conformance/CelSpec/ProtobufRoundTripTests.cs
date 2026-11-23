using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Google.Protobuf;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;
using ProtoParsedExpr = global::Cel.Expr.ParsedExpr;

namespace Great.Parts.Cel.Conformance.CelSpec;

// The protobuf form on the cel-spec suite (docs/design/tests.md, "Round trip of protobuf"): for every expression of the suite,
// parsed as CelSpecParseTests parses it, FromProtobuf(ToProtobuf(tree), text) gives a tree equal to the first one, with the start of
// every node, the macro calls and their parts; and Google.Protobuf reads the bytes into a message that it writes as the same bytes.
// The expressions that do not parse are the cases of BlockTestMacroCases, and no other: they are listed in the failure message of the
// test, not skipped.
public sealed class ProtobufRoundTripTests
{
    private const int FailuresShown = 40;

    [Fact]
    [Trait("Requirement", "F-20")]
    public void EveryExpressionOfTheSuite_LoadsBackFromItsBytesAndGoogleProtobufWritesTheSameBytes()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();
        List<string> failures = [];
        List<string> unparsed = [];
        int compared = 0;
        foreach (ConformanceCase testCase in CelSpecSuite.ReadCases())
        {
            string? failure;
            try
            {
                CelParseResult parsed = environment.Parse(testCase.Expression, CelSpecParseTests.SyntaxOf(testCase));
                if (!parsed.IsSuccess)
                {
                    unparsed.Add(testCase.Name);
                    continue;
                }

                compared++;
                failure = Difference(parsed.GetParsedExprOrThrow(), testCase.Expression);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failure = $"{exception.GetType().Name}: {exception.Message}";
            }

            if (failure is not null)
            {
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"{testCase.Path}({testCase.Line},{testCase.Column}): {testCase.Name}: {failure}"));
            }
        }

        string[] notExpected = [.. unparsed.Except(CelSpecParseTests.BlockTestMacroCases)];
        string[] notFound = [.. CelSpecParseTests.BlockTestMacroCases.Except(unparsed)];
        Assert.True(
            failures.Count == 0 && notExpected.Length == 0 && notFound.Length == 0,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{failures.Count} of {compared} expressions fail:\n{string.Join('\n', failures.Take(FailuresShown))}\nnot parsed, not expected: [{string.Join(", ", notExpected)}]\nparsed, expected not to: [{string.Join(", ", notFound)}]"));
        Assert.True(compared > 2500, string.Create(CultureInfo.InvariantCulture, $"only {compared} expressions were compared"));
    }

    private static string? Difference(CelParsedExpr original, string text)
    {
        byte[] bytes = original.ToProtobuf();
        CelParseResult result = CelParsedExpr.FromProtobuf(bytes, text);
        if (!result.IsSuccess)
        {
            return "FromProtobuf: " + result.Errors[0].Code + ": " + result.Errors[0].Message;
        }

        CelParsedExpr loaded = result.GetParsedExprOrThrow();
        if (SyntaxTreeComparer.Difference(original.Root, loaded.Root) is { } difference)
        {
            return "the loaded tree differs: " + difference;
        }

        CelExprNode[] was = [.. original.Root.DescendantsAndSelf()];
        CelExprNode[] now = [.. loaded.Root.DescendantsAndSelf()];
        return Starts(original, was, loaded, now)
            ?? MacroCalls(original, was, loaded, now)
            ?? GoogleProtobuf(bytes);
    }

    // The start of each node, node by node in the order of the walk, as the original has it; the length of a loaded node is 0.
    private static string? Starts(CelParsedExpr original, CelExprNode[] was, CelParsedExpr loaded, CelExprNode[] now)
    {
        if (was.Length != now.Length)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{was.Length} nodes became {now.Length}");
        }

        for (int i = 0; i < was.Length; i++)
        {
            int before = original.GetSpan(was[i].Id).Start.Offset;
            CelSourceSpan after = loaded.GetSpan(now[i].Id);
            if (before != after.Start.Offset || after.Length != 0)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"node {i} starts at {before}, loaded at {after.Start.Offset} with the length {after.Length}");
            }
        }

        return null;
    }

    // The macro calls of the loaded tree: the same function, target and arguments, and the parts are nodes of the loaded tree, as in
    // the original, except the arguments that the tree does not have, such as the m.f of has(m.f).
    private static string? MacroCalls(CelParsedExpr original, CelExprNode[] was, CelParsedExpr loaded, CelExprNode[] now)
    {
        for (int i = 0; i < was.Length; i++)
        {
            CelMacroCall? before = original.GetMacroCall(was[i].Id);
            CelMacroCall? after = loaded.GetMacroCall(now[i].Id);
            if (before is null != after is null)
            {
                return string.Create(CultureInfo.InvariantCulture, $"node {i}: the macro call is lost or new");
            }

            if (before is null || after is null)
            {
                continue;
            }

            if (before.Function != after.Function || before.Target is null != after.Target is null || before.Arguments.Length != after.Arguments.Length)
            {
                return string.Create(CultureInfo.InvariantCulture, $"node {i}: the macro call {before.Function} became {after.Function}");
            }

            if (before.Target is not null && Part(original, before.Target, loaded, after.Target!, mayBeOutside: false) is { } target)
            {
                return string.Create(CultureInfo.InvariantCulture, $"node {i}: the target of {before.Function}: {target}");
            }

            for (int j = 0; j < before.Arguments.Length; j++)
            {
                if (Part(original, before.Arguments[j], loaded, after.Arguments[j], mayBeOutside: true) is { } argument)
                {
                    return string.Create(CultureInfo.InvariantCulture, $"node {i}: the argument {j} of {before.Function}: {argument}");
                }
            }
        }

        return null;
    }

    private static string? Part(CelParsedExpr original, CelExprNode before, CelParsedExpr loaded, CelExprNode after, bool mayBeOutside)
    {
        if (SyntaxTreeComparer.Difference(before, after) is { } difference)
        {
            return difference;
        }

        bool wasInTree = ReferenceEquals(original.FindNode(before.Id), before);
        bool isInTree = ReferenceEquals(loaded.FindNode(after.Id), after);
        if (wasInTree != isInTree)
        {
            return wasInTree ? "a node of the tree became a node of its own" : "a node of its own became a node of the tree";
        }

        return isInTree || mayBeOutside ? null : "is not a node of the tree";
    }

    // Google.Protobuf reads the bytes and writes the message as the same bytes.
    private static string? GoogleProtobuf(byte[] bytes) =>
        ProtoParsedExpr.Parser.ParseFrom(bytes).ToByteArray().AsSpan().SequenceEqual(bytes)
            ? null
            : "Google.Protobuf writes other bytes than the library";
}
