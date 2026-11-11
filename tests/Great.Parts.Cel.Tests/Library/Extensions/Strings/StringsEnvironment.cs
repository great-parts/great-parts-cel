using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Library.Extensions.Strings;

// The environment of the tests of the sets strings and sets: the standard library with both sets and a few variables, among them
// strings that an expression cannot write — lone surrogates, which bindings can hold — and variables of the type dyn, whose
// values reach the overloads without the check of static types.
internal static class StringsEnvironment
{
    internal static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Sets(0))
        .AddVariable("text", CelType.String)
        .AddVariable("pattern", CelType.String)
        .AddVariable("other", CelType.String)
        .AddVariable("index", CelType.Int)
        .AddVariable("a", CelType.Dyn)
        .AddVariable("b", CelType.Dyn)
        .AddVariable("x", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // Evaluates an expression on the bindings, with a cost limit or without one.
    internal static CelEvalResult<CelValue> Evaluate(string expression, CelBindings? bindings = null, ulong? costLimit = null) =>
        Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings ?? new CelBindings(), new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);

    // The string that an expression of the bindings gives, which has to be a string.
    internal static string EvaluateString(string expression, CelBindings bindings)
    {
        CelValue value = Evaluate(expression, bindings).GetValueOrThrow();
        return value.TryGetString(out string? text) ? text : throw new System.InvalidOperationException("The value is not a string: " + value.Kind);
    }

    // A list of values as a value.
    internal static CelValue ListOf(params CelValue[] items) => CelValue.FromList(CelList.Create(items));
}
