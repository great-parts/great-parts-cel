using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One target framework in one configuration, with the preprocessor symbols that the compiler defines for it.</summary>
/// <param name="Target">The target framework moniker, such as <c>net11.0</c>; empty when no project file names a target.</param>
/// <param name="Configuration"><c>Debug</c> or <c>Release</c>.</param>
/// <param name="Symbols">The symbols: those of the SDK for the target, <c>DEBUG</c> or <c>RELEASE</c>, <c>TRACE</c>, then the names of <c>DefineConstants</c>.</param>
internal sealed record SymbolSet(string Target, string Configuration, IReadOnlyList<string> Symbols)
{
    /// <summary>The name of the set, such as <c>net11.0/Release</c>.</summary>
    internal string Name => Target + "/" + Configuration;
}

/// <summary>Finds the symbol sets of the repository: every target framework of the project files in every configuration.</summary>
internal static class SymbolSets
{
    private static readonly string[] Configurations = ["Debug", "Release"];

    /// <summary>
    /// Reads <c>TargetFramework</c>, <c>TargetFrameworks</c> and <c>DefineConstants</c> from the <c>*.csproj</c> and
    /// <c>*.props</c> files of the file set, wherever they are set and under any condition, and builds the sets. The names of
    /// <c>DefineConstants</c> are added to every set. A project file that is not well-formed XML is skipped: the build reports it.
    /// </summary>
    /// <param name="files">The file set.</param>
    /// <returns>
    /// A set for each target in each configuration, ordered by target (ordinal), then Debug, Release. When no project file
    /// names a target, one set per configuration with an empty target.
    /// </returns>
    internal static IReadOnlyList<SymbolSet> Discover(IReadOnlyList<RepositoryFile> files)
    {
        SortedSet<string> targets = new(StringComparer.Ordinal);
        List<string> defines = [];
        foreach (RepositoryFile file in files.Where(IsProjectFile))
        {
            if (TryLoad(file) is { } document)
            {
                Collect(document, targets, defines);
            }
        }

        return [.. (targets.Count == 0 ? [string.Empty] : targets.ToList()).SelectMany(target => Configurations.Select(configuration => Build(target, configuration, defines)))];
    }

    private static bool IsProjectFile(RepositoryFile file) =>
        file.Path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || file.Path.EndsWith(".props", StringComparison.OrdinalIgnoreCase);

    private static XDocument? TryLoad(RepositoryFile file)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        try
        {
            using FileStream stream = File.OpenRead(file.FullPath);
            using XmlReader reader = XmlReader.Create(stream, settings);
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static void Collect(XDocument document, SortedSet<string> targets, List<string> defines)
    {
        foreach (XElement element in document.Descendants())
        {
            switch (element.Name.LocalName)
            {
                case "TargetFramework" or "TargetFrameworks":
                    targets.UnionWith(Names(element.Value).Where(name => !name.Contains('$', StringComparison.Ordinal)));
                    break;
                case "DefineConstants":
                    defines.AddRange(Names(element.Value).Where(IsSymbolName));
                    break;
            }
        }
    }

    private static string[] Names(string value) =>
        value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // A MSBuild reference such as $(DefineConstants) is not a name; so is anything else that is not an identifier.
    private static bool IsSymbolName(string name) =>
        (char.IsLetter(name[0]) || name[0] == '_') && name.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static SymbolSet Build(string target, string configuration, List<string> defines)
    {
        List<string> symbols = [.. SdkSymbols.For(target), configuration == "Debug" ? "DEBUG" : "RELEASE", "TRACE", .. defines];
        return new SymbolSet(target, configuration, [.. symbols.Distinct(StringComparer.Ordinal)]);
    }
}
