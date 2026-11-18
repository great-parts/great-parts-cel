namespace SpeedBench.Adapters;

/// <summary>
/// One CEL library with the three operations of the comparison. The environment (declarations, providers) is built once
/// per process and is not part of any operation.
/// </summary>
public interface ILibraryAdapter
{
    /// <summary>Library name in tables and in benchmark parameters.</summary>
    string Name { get; }

    /// <summary>Compilation: all calls of the library from the text to an object ready for evaluation.</summary>
    object Compile(string text);

    /// <summary>
    /// Evaluation of a compiled object on the application data. Includes everything the application does per
    /// evaluation: building the library's activation and, if the library does not read .NET objects,
    /// building its own structure from the whole order graph.
    /// </summary>
    object? Evaluate(object compiled, Order order, DateTimeOffset now);

    /// <summary>Full cycle: all calls of the library from the text to the value.</summary>
    object? FullCycle(string text, Order order, DateTimeOffset now);

    /// <summary>Canonical text of a result returned by <see cref="Evaluate"/> or <see cref="FullCycle"/>.</summary>
    string Canonical(object? result);
}

public static class Libraries
{
    public static readonly IReadOnlyList<ILibraryAdapter> All =
    [
        new CellyAdapter(),
        new DotnetCelAdapter(),
        new CelNetAdapter(),
        new TelusCelAdapter(),
        new GreatPartsCelAdapter(),
    ];

    public static ILibraryAdapter Get(string name) =>
        All.FirstOrDefault(l => l.Name == name) ?? throw new ArgumentException("unknown library " + name);
}
