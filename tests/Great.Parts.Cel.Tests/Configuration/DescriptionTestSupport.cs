using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Configuration;

// Helpers of the tests of the environment description in JSON. A marked document has a "^" at the place of each expected
// error; the marks are removed before the text is read.
internal static class DescriptionTestSupport
{
    internal const string Label = "environment JSON";

    internal static CelEnvironmentBuilder NewBuilder() => new(CelStandardLibrary.V1);

    internal static ImmutableArray<CelError> Apply(CelEnvironmentBuilder builder, string json) =>
        CelEnvironmentJson.Apply(builder, Encoding.UTF8.GetBytes(json));

    // The offset of the first occurrence of a text in the JSON, which must be there.
    internal static int IndexOf(string json, string needle)
    {
        int index = json.IndexOf(needle, StringComparison.Ordinal);
        Assert.True(index >= 0, $"'{needle}' is not in the text");
        return index;
    }

    // The text without the marks, and the offsets of the marks.
    internal static (string Json, int[] Offsets) Mark(string marked)
    {
        StringBuilder json = new();
        List<int> offsets = [];
        foreach (char character in marked)
        {
            if (character == '^')
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

    // The description has exactly the marked errors, in the order of the text, each InvalidConfiguration at its mark; each
    // detail is contained in the message of the error at the same index.
    internal static void AssertErrors(string marked, string label = Label, params string[] details)
    {
        (string json, int[] offsets) = Mark(marked);
        CelEnvironmentBuilder builder = NewBuilder().AddVariable("before", CelType.Bool).UseContainer("keep");

        ImmutableArray<CelError> errors = Apply(builder, json);

        Assert.Equal(offsets, errors.Select(error => error.SourceLocation?.Offset ?? -1));
        foreach (CelError error in errors)
        {
            AssertInvalid(error, label, json);
        }

        Assert.Equal(details.Length, details.Count(detail => errors.Any(error => error.Message.Contains(detail, StringComparison.Ordinal))));
        Assert.Equal(new[] { ("before", CelType.Bool) }, Variables(builder));
        Assert.Equal("keep", builder.Container);
        Assert.Empty(builder.FunctionDeclarations);
        Assert.Empty(builder.TypeProviders);
    }

    // One marked error.
    internal static void AssertError(string marked, string detail, string label = Label) =>
        AssertErrors(marked, label, detail);

    private static void AssertInvalid(CelError error, string label, string json)
    {
        Assert.Equal(CelErrorCode.InvalidConfiguration, error.Code);
        Assert.Equal(CelErrorStage.Build, error.Stage);
        Assert.Equal(default, error.Location);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal(label, place.SourceName);
        Assert.Equal(1, place.Line);
        Assert.Equal(place.Offset + 1, place.Column);
        Assert.StartsWith(
            string.Create(CultureInfo.InvariantCulture, $"invalid environment description at line {place.Line}, column {place.Column}: "),
            error.Message,
            StringComparison.Ordinal);
        Assert.InRange(place.Offset, 0, json.Length);
    }

    internal static (string Name, CelType Type)[] Variables(CelEnvironmentBuilder builder) =>
        [.. builder.VariableDeclarations
            .Select(declaration => (declaration.Name, declaration.Type))
            .OrderBy(declaration => declaration.Name, StringComparer.Ordinal)];

    // The only build error of a builder.
    internal static CelError BuildError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    // The error has the code and the message, no place in an expression, and the place in the JSON text of the declaration
    // that it names, with the label of the description as the source name; a null offset means no place.
    internal static void AssertBuildError(CelError error, CelErrorCode code, string message, string label, int? offset)
    {
        Assert.Equal((code, CelErrorStage.Build, message), (error.Code, error.Stage, error.Message));
        Assert.Equal(default, error.Location);
        if (offset is null)
        {
            Assert.Null(error.SourceLocation);
        }
        else
        {
            Assert.Equal(new CelSourceLocation(label, offset.Value, 1, offset.Value + 1), error.SourceLocation);
        }
    }
}
