using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The MSBuild properties that turn a check off or lower its level when they have a value other than the safe one
/// (<c>docs/design/eng-checks-source.md</c>, "suppressions"). A value is trimmed and compared ignoring case; any other value,
/// a <c>$(…)</c> reference included, is a form.
/// </summary>
internal static class MSBuildProperties
{
    private const string AnalysisMode = "AnalysisMode";
    private const string AnalysisLevel = "AnalysisLevel";
    private const string True = "true";
    private const string False = "false";

    private static readonly string[] NotTrue =
    [
        "RunAnalyzers",
        "RunAnalyzersDuringBuild",
        "RunAnalyzersDuringLiveAnalysis",
        "EnableNETAnalyzers",
        "EnforceCodeStyleInBuild",
        "GenerateDocumentationFile",
        "IsAotCompatible",
        "EnableTrimAnalyzer",
        "EnableAotAnalyzer",
        "EnableSingleFileAnalyzer",
        "CodeAnalysisTreatWarningsAsErrors",
        "ILLinkTreatWarningsAsErrors",
        "IlcTreatWarningsAsErrors",
    ];

    private static readonly string[] NotFalse = ["SuppressTrimAnalysisWarnings", "SuppressAotAnalysisWarnings"];
    private static readonly string[] AnyValue = ["WarningLevel", "CodeAnalysisRuleSet"];

    private static readonly Dictionary<string, Property> Table = Build();

    /// <summary>
    /// Finds the property of the table by the name of the element: the table is case-insensitive, like MSBuild, and a name
    /// with a category such as <c>AnalysisModeSecurity</c> belongs to <c>AnalysisMode</c>.
    /// </summary>
    /// <param name="name">The name of the element.</param>
    /// <returns>The property, or <see langword="null"/> when the element is not a form of the table.</returns>
    internal static Property? Find(string name)
    {
        if (Table.TryGetValue(name, out Property? property))
        {
            return property;
        }

        if (name.StartsWith(AnalysisMode, StringComparison.OrdinalIgnoreCase))
        {
            return Table[AnalysisMode];
        }

        return name.StartsWith(AnalysisLevel, StringComparison.OrdinalIgnoreCase) ? Table[AnalysisLevel] : null;
    }

    /// <summary>A property of the table: the key of the ratchet and the test of a value.</summary>
    /// <param name="Key">The name of the form, which is the key of the ratchet.</param>
    /// <param name="IsForm">Tells whether the value of the element, trimmed, is a form.</param>
    internal sealed record Property(string Key, Func<string, bool> IsForm);

    private static Dictionary<string, Property> Build()
    {
        Dictionary<string, Property> table = new(StringComparer.OrdinalIgnoreCase)
        {
            ["TreatWarningsAsErrors"] = new("TreatWarningsAsErrors", value => Same(value, False)),
            ["Nullable"] = new("Nullable", value => !Same(value, "enable")),
            [AnalysisMode] = new(AnalysisMode, value => !Same(value, "All")),
            [AnalysisLevel] = new(AnalysisLevel, IsLoweredLevel),
        };
        foreach (string name in NotTrue)
        {
            table[name] = new Property(name, value => !Same(value, True));
        }

        foreach (string name in NotFalse)
        {
            table[name] = new Property(name, value => !Same(value, False));
        }

        foreach (string name in AnyValue)
        {
            table[name] = new Property(name, _ => true);
        }

        return table;
    }

    // <version>-<mode>: the version is 'latest' or 'preview' and the mode, when there is one, is 'all'.
    private static bool IsLoweredLevel(string value)
    {
        string[] parts = value.Split('-', 2);
        bool versionIsSafe = parts[0].Trim().Equals("latest", StringComparison.OrdinalIgnoreCase)
            || parts[0].Trim().Equals("preview", StringComparison.OrdinalIgnoreCase);
        bool modeIsSafe = parts.Length == 1 || Same(parts[1], "all");
        return !(versionIsSafe && modeIsSafe);
    }

    private static bool Same(string value, string expected) => value.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase);
}
