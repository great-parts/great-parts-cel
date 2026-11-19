namespace SpeedBench.Adapters;

/// <summary>
/// One CEL library with the three operations of the comparison. The environment (declarations, providers) is built once
/// per process and is not part of any operation.
/// </summary>
public interface ILibraryAdapter
{
    /// <summary>Library name in tables and in benchmark parameters.</summary>
    string Name { get; }

    /// <summary>
    /// Whether the library has a compilation of its own that the rig measures. A library without it (the simple entry of
    /// Great.Parts.Cel) has the operations evaluation and full cycle only.
    /// </summary>
    bool HasCompile => true;

    /// <summary>
    /// The text of a scenario as this library reads it. The same text unless the way of passing the data changes the names
    /// (the fields of the order as variables of the top level). The text of the scenario stays the one that the rows show.
    /// </summary>
    string Rewrite(string expression) => expression;

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
        new GreatPartsCelInputAdapter(),
        new GreatPartsCelSimpleAdapter(),
    ];

    public static ILibraryAdapter Get(string name) =>
        All.FirstOrDefault(l => l.Name == name) ?? throw new ArgumentException("unknown library " + name);
}
