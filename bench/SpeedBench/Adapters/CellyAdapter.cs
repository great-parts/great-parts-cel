using Celly;
using Celly.Checking;
using Celly.Providers;
using Celly.Types;
using Celly.Values;

namespace SpeedBench.Adapters;

/// <summary>
/// Celly 1.2.0. Data: NativeTypeProvider over the .NET classes, order declared as a struct type, a dictionary
/// activation (docs/guide/native-types.md, docs/guide/api.md). Compilation is Parse, Check, Program:
/// the documentation recommends checking user-supplied expressions; Compile alone only parses and plans.
/// Celly has no compilation cache.
/// </summary>
public sealed class CellyAdapter : ILibraryAdapter
{
    private static readonly NativeTypeProvider Provider = NativeTypeProvider.FromTypes(typeof(Order));

    private static readonly CelEnv Env = CelEnv.Create(new CelEnvSettings
    {
        TypeProvider = Provider,
        Adapter = Provider,
        Declarations =
        [
            new VariableDecl("order", CelType.Struct(typeof(Order).FullName!)),
            new VariableDecl("now", CelType.Timestamp),
        ],
    });

    public string Name => "Celly";

    public object Compile(string text)
    {
        var parsed = Env.Parse(text);
        var ast = parsed.Ast ?? throw new InvalidOperationException("parse: " + string.Join("; ", parsed.Issues));
        var check = Env.Check(ast);
        if (check.HasErrors)
        {
            throw new InvalidOperationException("check: " + string.Join("; ", check.Issues));
        }

        return Env.Program(ast);
    }

    public object? Evaluate(object compiled, Order order, DateTimeOffset now) =>
        ((CelProgram)compiled).Eval(new Dictionary<string, object?> { ["order"] = order, ["now"] = now });

    public object? FullCycle(string text, Order order, DateTimeOffset now) => Evaluate(Compile(text), order, now);

    public string Canonical(object? result) =>
        result is ErrorValue e ? "ERROR(" + e.Message + ")" : Canon.Format(((CelValue)result!).ToNative());
}
