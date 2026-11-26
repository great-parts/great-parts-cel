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

/// <summary>
/// The libraries of the comparison. An adapter is created on first use, not with the list: the environment of a library is built by
/// the type initializer of its adapter, and a library that cannot do it (under Native AOT, for example) must fail alone, in the
/// verification of that library, and not take the list of the other libraries with it.
/// </summary>
public static class Libraries
{
    private static readonly (string Name, Lazy<ILibraryAdapter> Adapter)[] Entries =
    [
        Entry("Celly", () => new CellyAdapter()),
        Entry("DotnetCel", () => new DotnetCelAdapter()),
        Entry("Cel.NET", () => new CelNetAdapter()),
        Entry("TelusCel", () => new TelusCelAdapter()),
        Entry("GreatPartsCel", () => new GreatPartsCelAdapter()),
        Entry("GPCel-Input", () => new GreatPartsCelInputAdapter()),
        Entry("GPCel-Simple", () => new GreatPartsCelSimpleAdapter()),
    ];

    /// <summary>The names of the libraries in the order of the tables.</summary>
    public static IReadOnlyList<string> Names => [.. Entries.Select(e => e.Name)];

    public static ILibraryAdapter Get(string name)
    {
        foreach (var (entryName, adapter) in Entries)
        {
            if (entryName == name)
            {
                var created = adapter.Value;
                return created.Name == name ? created : throw new InvalidOperationException("the adapter " + created.Name + " is listed as " + name);
            }
        }

        throw new ArgumentException("unknown library " + name);
    }

    private static (string, Lazy<ILibraryAdapter>) Entry(string name, Func<ILibraryAdapter> create) => (name, new Lazy<ILibraryAdapter>(create));
}
