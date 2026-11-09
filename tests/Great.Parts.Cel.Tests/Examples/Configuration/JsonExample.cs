using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// Helpers of the examples of the environment description in JSON.
//
// A marked document is a single-line JSON text in which every '|' marks the place of one expected error and
// is removed from the text. The places are UTF-16 offsets into the text, so on a single line the column is the
// offset plus one. The expected errors are in the order of the marks, which is the order of the text.
internal static class JsonExample
{
    internal const string Label = "environment JSON";

    internal static ImmutableArray<CelError> Apply(CelEnvironmentBuilder builder, string json) =>
        CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes(json));

    internal static CelEnvironmentBuilder NewBuilder() => new(CelStandardLibrary.V1);

    // A builder that holds a variable, a container, no functions and no type sources: the state that an
    // erroneous description must leave as it is.
    internal static CelEnvironmentBuilder BuilderWithState() =>
        NewBuilder().AddVariable("before", CelType.Bool).UseContainer("keep");

    internal static (string Json, int[] Offsets) Mark(string marked)
    {
        var json = new StringBuilder();
        var offsets = new List<int>();
        foreach (char character in marked)
        {
            if (character == '|')
            {
                offsets.Add(json.Length);
            }
            else
            {
                json.Append(character);
            }
        }

        return (json.ToString(), [.. offsets]);
    }

    // The description has exactly the marked errors: each is InvalidConfiguration at its mark, with the label of
    // the description; the builder is left as it was. With ordered false the errors are compared as a set of
    // places, for documents whose errors the text does not order (a missing key at the "{" of an object and an
    // error at a value inside it).
    internal static CelError[] AssertErrors(string marked, string label = Label, bool ordered = true)
    {
        (string json, int[] offsets) = Mark(marked);
        Assert.DoesNotContain('\n', json);
        Assert.DoesNotContain('\r', json);
        CelEnvironmentBuilder builder = BuilderWithState();

        CelError[] errors = [.. Apply(builder, json)];

        if (!ordered)
        {
            errors = [.. errors.OrderBy(error => error.SourceLocation?.Offset)];
            offsets = [.. offsets.Order()];
        }

        Assert.Equal(offsets.Length, errors.Length);
        for (int i = 0; i < errors.Length; i++)
        {
            AssertInvalid(errors[i], label, offsets[i], 1, offsets[i] + 1);
        }

        AssertUnchanged(builder);
        return errors;
    }

    internal static CelError AssertError(string marked, string label = Label, string? mentions = null)
    {
        CelError error = Assert.Single(AssertErrors(marked, label));
        if (mentions is not null)
        {
            Assert.Contains(mentions, error.Message, StringComparison.Ordinal);
        }

        return error;
    }

    internal static void AssertInvalid(CelError error, string label, int offset, int line, int column)
    {
        Assert.Equal(CelErrorCode.InvalidConfiguration, error.Code);
        Assert.Equal(CelErrorStage.Build, error.Stage);
        Assert.Equal(default(CelSourceLocation), error.Location);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal((label, offset, line, column), (place.SourceName, place.Offset, place.Line, place.Column));
        string prefix = string.Create(
            CultureInfo.InvariantCulture,
            $"invalid environment description at line {line}, column {column}: ");
        Assert.StartsWith(prefix, error.Message, StringComparison.Ordinal);
    }

    internal static void AssertUnchanged(CelEnvironmentBuilder builder)
    {
        Assert.Equal(new[] { ("before", CelType.Bool) }, Variables(builder));
        Assert.Equal("keep", builder.Container);
        Assert.Empty(builder.FunctionDeclarations);
        Assert.Empty(builder.TypeProviders);
    }

    // The declarations are sets: the order of the calls does not matter.
    internal static (string Name, CelType Type)[] Variables(CelEnvironmentBuilder source) =>
        [.. source.VariableDeclarations
            .Select(declaration => (declaration.Name, declaration.Type))
            .OrderBy(declaration => declaration.Name, StringComparer.Ordinal)];

    // A function by its name and its overloads in order: the identifier, the call style, the parameter types
    // (the receiver first when there is one) and the result type.
    internal static string[] Functions(CelEnvironmentBuilder source) =>
        [.. source.FunctionDeclarations
            .Select(function => function.Name + ": " + string.Join(
                "; ",
                function.Overloads.Select(overload =>
                    $"{overload.OverloadId} | {overload.IsReceiverStyle} | {string.Join(", ", overload.ParameterTypes)} | {overload.ResultType}")))
            .Order(StringComparer.Ordinal)];

    internal static CelError BuildError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    internal static void AssertBuildError(CelError error, CelErrorCode code, string message)
    {
        Assert.Equal((code, CelErrorStage.Build, message), (error.Code, error.Stage, error.Message));
        Assert.Equal(default(CelSourceLocation), error.Location);
    }

    // The place of a build error of a declaration in the JSON: the source name is the label, the offset the
    // position of the "{" or the value that declared it. The JSON is on one line, so the column is the offset plus one.
    internal static void AssertBuildErrorAt(CelError error, CelErrorCode code, string message, string label, int offset)
    {
        AssertBuildError(error, code, message);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal((label, offset, 1, offset + 1), (place.SourceName, place.Offset, place.Line, place.Column));
    }
}
