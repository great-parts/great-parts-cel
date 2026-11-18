extern alias TelusCel;

using SpeedBench.Proto;
using CelEnvironment = TelusCel::Cel.CelEnvironment;
using CelProgramDelegate = TelusCel::Cel.CelProgramDelegate;

namespace SpeedBench.Adapters;

/// <summary>
/// TELUS Cel 0.3.3 (package Cel; extern alias TelusCel, because Cel.NET also uses the namespace Cel). Data:
/// "your own classes ... must be defined as a protocol buffer message and the descriptor registered in the CEL
/// startup" (README), so the whole order graph is translated into Proto/order.proto messages on every
/// evaluation; now is a DateTimeOffset, the library's own timestamp value. The environment with the file
/// descriptor is built once per process. Compilation is CelEnvironment.Compile (parse, then a tree of
/// delegates; the library has no type check). TELUS Cel has no compilation cache: the README advises the
/// application to cache the delegate.
/// </summary>
public sealed class TelusCelAdapter : ILibraryAdapter
{
    private static readonly CelEnvironment Env = new([OrderReflection.Descriptor], "speedbench");

    // No space: the name is part of the BenchmarkDotNet parameter "library:case".
    public string Name => "TelusCel";

    public object Compile(string text) => Env.Compile(text);

    public object? Evaluate(object compiled, Order order, DateTimeOffset now) =>
        ((CelProgramDelegate)compiled).Invoke(new Dictionary<string, object?>
        {
            ["order"] = ProtoData.From(order),
            ["now"] = now,
        });

    public object? FullCycle(string text, Order order, DateTimeOffset now) => Evaluate(Compile(text), order, now);

    public string Canonical(object? result) => Canon.Format(result);
}
