using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Library.Extensions.Lists;

// The environment of the tests of the sets lists and encoders: the standard library with both sets and two variables of the type dyn,
// whose values reach the overloads without the check of static types and which bindings fill with what an expression cannot write —
// nested lists as deep as memory allows, lone surrogates, lists of the application whose elements fail, JSON documents.
internal static class ListsEnvironment
{
    internal static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Encoders(1))
        .AddVariable("x", CelType.Dyn)
        .AddVariable("y", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    // Evaluates an expression on the bindings, with a cost limit or without one.
    internal static CelEvalResult<CelValue> Evaluate(string expression, CelBindings? bindings = null, ulong? costLimit = null) =>
        Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings ?? new CelBindings(), new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);

    // A list of values as a value.
    internal static CelValue ListOf(params CelValue[] items) => CelValue.FromList(CelList.Create(items));
}
