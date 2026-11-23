using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The environment and the helpers of the examples of partial evaluation: the variables x and y of int, b of bool, s of string, l of
// list(int), d of dyn, admins of list(string), and request, resource and m of map(string, dyn), with the sets optional and bindings.
internal static class PartialTestSupport
{
    internal static CelEnvironment Env { get; } = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable<long>("y")
        .AddVariable<bool>("b")
        .AddVariable<string>("s")
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .AddVariable("d", CelType.Dyn)
        .AddVariable("admins", CelType.ListOf(CelType.String))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddVariable("request", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddVariable("resource", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddExtension(CelExtensions.Optional(2))
        .AddExtension(CelExtensions.Bindings(1))
        .Build()
        .GetEnvironmentOrThrow();

    // The compile options of a program for partial evaluation.
    internal static CelCompileOptions Partial { get; } = new() { PartialEvaluation = true };

    // The qualifier that matches any field, index or key.
    internal static readonly object Any = new();

    // A pattern of a variable and qualifiers: a string, a long, a ulong or a bool; Any matches any.
    internal static CelAttributePattern P(string variable, params object[] qualifiers)
    {
        var pattern = new CelAttributePattern(variable);
        foreach (object qualifier in qualifiers)
        {
            pattern = qualifier switch
            {
                string name => pattern.Qualify(name),
                long number => pattern.Qualify(number),
                ulong unsigned => pattern.Qualify(unsigned),
                bool flag => pattern.Qualify(flag),
                _ => pattern.QualifyAny(),
            };
        }

        return pattern;
    }

    // The patterns of whole variables, from their names separated by commas; empty text for none.
    internal static List<CelAttributePattern> Patterns(string variables) =>
        variables.Length == 0 ? [] : [.. variables.Split(',').Select(name => new CelAttributePattern(name))];

    internal static CelValue Map(params (string Key, CelValue Value)[] entries) =>
        CelValue.FromMap(CelMap.Create(entries.Select(entry => KeyValuePair.Create(CelValue.FromString(entry.Key), entry.Value))));

    internal static CelValue Int(long value) => CelValue.FromInt64(value);

    internal static CelValue Str(string value) => CelValue.FromString(value);

    internal static CelValue IntList(params long[] items) => CelValue.FromList(CelList.Create([.. items.Select(CelValue.FromInt64)]));

    // The bindings of the examples: x = 1, y = 2, l = [1, 2].
    internal static CelBindings Bindings() => new CelBindings()
        .Add("x", 1L)
        .Add("y", 2L)
        .Add("l", IntList(1, 2));

    // The text of the unknown attributes of a result, in the order of their ids, separated by commas.
    internal static string Trails(CelPartialEvalResult<CelValue> result) =>
        string.Join(",", result.Unknowns.Select(unknown => unknown.Attribute!.ToString()));

    // The partial evaluation of an expression compiled for it.
    internal static CelPartialEvalResult<CelValue> Evaluate(
        string expression,
        IReadOnlyList<CelAttributePattern> unknowns,
        CelBindings? bindings = null,
        CelEvalOptions? options = null,
        CelEnvironment? environment = null) =>
        (environment ?? Env).Compile<CelValue>(expression, Partial).GetProgramOrThrow()
            .EvaluatePartial(bindings ?? Bindings(), unknowns, options, CancellationToken.None);
}
