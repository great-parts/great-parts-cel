using System;
using System.Text.RegularExpressions;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The name of a release tag, <c>v</c> and a semantic version, and the version that both packages of the release have.</summary>
internal sealed class ReleaseTag
{
    private const string Number = "(?:0|[1-9][0-9]*)";
    private const string Identifier = "(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)";

    private static readonly Regex Form = new(
        "^v" + Number + @"\." + Number + @"\." + Number + "(?:-" + Identifier + @"(?:\." + Identifier + ")*)?(?:" + @"\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant);

    private ReleaseTag(string name) => Name = name;

    /// <summary>The name of the tag with <c>v</c>, such as <c>v1.0.0</c>.</summary>
    internal string Name { get; }

    /// <summary>The version of the tag, the name without <c>v</c>.</summary>
    internal string Version => Name[1..];

    /// <summary>Reads the name of a tag.</summary>
    /// <param name="name">The name given on the command line.</param>
    /// <exception cref="CheckInputException">The name is not <c>v</c> and a semantic version.</exception>
    internal static ReleaseTag Parse(string name) =>
        name.Length > 0 && name.IndexOf('\n', StringComparison.Ordinal) < 0 && Form.IsMatch(name)
            ? new ReleaseTag(name)
            : throw new CheckInputException($"the tag '{name}' is not v and a semantic version, such as v1.0.0");
}
