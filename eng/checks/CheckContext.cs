using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The repository root, the options of the run and the scans that several commands of one run share. A scan is computed
/// on first use and kept for the other commands.
/// </summary>
internal sealed class CheckContext
{
    private readonly Dictionary<string, object> _scans = new(StringComparer.Ordinal);

    /// <summary>Creates the context of one run.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="options">The options of the command line by name, with the leading <c>--</c>; the value of a flag is empty.</param>
    /// <param name="arguments">The arguments of the commands by command name; <see langword="null"/> for none.</param>
    internal CheckContext(string root, IReadOnlyDictionary<string, string> options, IReadOnlyDictionary<string, string>? arguments = null)
    {
        Root = root;
        Options = options;
        Arguments = arguments ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>The full path of the repository root.</summary>
    internal string Root { get; }

    /// <summary>The options of the command line by name, with the leading <c>--</c>; the value of a flag is empty.</summary>
    internal IReadOnlyDictionary<string, string> Options { get; }

    /// <summary>The argument of each command that takes one, by command name, see <see cref="ICheckCommand.ArgumentName"/>.</summary>
    internal IReadOnlyDictionary<string, string> Arguments { get; }

    /// <summary>The files of the file set of the repository, sorted by path; computed on first use.</summary>
    internal IReadOnlyList<RepositoryFile> Files => Scan("repository-files", () => RepositoryFiles.Enumerate(Root));

    /// <summary>The C# files of the file set parsed under every symbol set, generated files apart; computed on first use.</summary>
    internal CSharpSources Sources => Scan("csharp-sources", () => CSharpSources.Load(Files));

    /// <summary>Returns the scan with the name, computing it on the first call of the run.</summary>
    /// <typeparam name="T">The type of the result of the scan.</typeparam>
    /// <param name="name">The name of the scan, unique in the application.</param>
    /// <param name="compute">Computes the scan; called at most once per run for a name.</param>
    internal T Scan<T>(string name, Func<T> compute)
        where T : notnull
    {
        if (_scans.TryGetValue(name, out object? value))
        {
            return (T)value;
        }

        T result = compute();
        _scans[name] = result;
        return result;
    }
}
