using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// Field selection e.f and the presence test has(e.f) on values of this part: on a map, f is a string key;
// any other value, which only an operand of the static type dyn can give, does not support selection. The
// place of a selection is its ".", the place of has() the "(" after its name. m is map(string, int) and
// holds {'a': 1}; dm, d, ds and dl are dyn and hold {'k': 1}, 1, 's' and [1].
public sealed class SelectionTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("dm", CelType.Dyn)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("ds", CelType.Dyn)
        .AddVariable("dl", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("m", Map("a", 1))
        .Add("dm", CelValue.FromMap(Map("k", 1)))
        .Add("d", 1L)
        .Add("ds", "s")
        .Add("dl", CelValue.FromList(CelList.Create(CelValue.FromInt64(1))));

    [Theory]
    [Trait("Requirement", "F-1")]
    [InlineData("m.a == 1", true)]
    [InlineData("dm.k == 1", true)]
    [InlineData("has(m.a)", true)]
    [InlineData("has(m.b)", false)]
    [InlineData("has(dm.missing)", false)]
    public void SelectionOnAMap_ReadsTheKey(string expression, bool expected)
    {
        Assert.Equal(expected, environment.Compile<bool>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // cel-go v0.32.0 gives an error too, "no such key: missing".
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("m.missing", 1)]
    [InlineData("dm.missing", 2)]
    public void SelectionOfAMissingKey_IsEvalNoSuchFieldAtTheSelection(string expression, int offset)
    {
        CelError error = SingleError(expression);

        Assert.Equal(CelErrorCode.EvalNoSuchField, error.Code);
        Assert.Equal("no such field 'missing' in map", error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    // has() on such a value is an error as well, not false (the language definition, "Field Selection");
    // cel-go v0.32.0 with the option EnableErrorOnBadPresenceTest, which its conformance runner sets, gives
    // an error for each of these expressions.
    [Theory]
    [Trait("Requirement", "F-1")]
    [Trait("Requirement", "F-4")]
    [InlineData("d.f", "int", 1)]
    [InlineData("has(d.f)", "int", 3)]
    [InlineData("ds.f", "string", 2)]
    [InlineData("dl.f", "list", 2)]
    [InlineData("has(dl.f)", "list", 3)]
    public void SelectionOnAValueThatIsNotAMap_IsFieldSelectionNotSupported(string expression, string type, int offset)
    {
        CelError error = SingleError(expression);

        Assert.Equal(CelErrorCode.FieldSelectionNotSupported, error.Code);
        Assert.Equal("value of type '" + type + "' does not support field selection of 'f'", error.Message);
        Assert.Equal((offset, 1, offset + 1), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    private CelError SingleError(string expression)
    {
        CelEvalResult<CelValue> result = environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        return error;
    }

    private static CelMap Map(string key, long value) =>
        CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString(key), CelValue.FromInt64(value))]);
}
