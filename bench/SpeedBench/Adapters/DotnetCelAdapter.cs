using DotnetCel;
using DotnetCel.Types;

namespace SpeedBench.Adapters;

/// <summary>
/// DotnetCel 1.0.7. Data: the default POCO adapter (reflection, lazy field reads) with order declared as an
/// object type and the camelCase naming convention, a dictionary activation (guides/working-with-pocos.md,
/// guides/performance-and-trimming.md, "prefer dictionaries over anonymous objects"). Compilation is
/// CelExpression.Compile (parse and check), which returns a CompiledProgram. DotnetCel has no compilation
/// cache: its documentation advises the application to keep one.
/// </summary>
public sealed class DotnetCelAdapter : ILibraryAdapter
{
    private static readonly CelEnv Env = CelEnv.NewBuilder()
        .UsePocoNaming(PocoNamingConvention.CamelCase)
        .Variable("order", CelTypes.Object(nameof(Order)))
        .Variable("now", CelTypes.Timestamp)
        .Build();

    public string Name => "DotnetCel";

    public object Compile(string text) => CelExpression.Compile(text, Env);

    public object? Evaluate(object compiled, Order order, DateTimeOffset now) =>
        ((CompiledProgram)compiled).Eval(
            (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["order"] = order, ["now"] = now });

    public object? FullCycle(string text, Order order, DateTimeOffset now) => Evaluate(Compile(text), order, now);

    // Eval returns a CLR value and throws CelEvaluationException for a CEL error; Verification records the exception.
    public string Canonical(object? result) => Canon.Format(result);
}
