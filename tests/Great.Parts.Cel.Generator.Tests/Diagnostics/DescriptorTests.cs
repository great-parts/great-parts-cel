using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Diagnostics;

// The nine descriptors, the page docs/generator-diagnostics.md and AnalyzerReleases.Unshipped.md say the same: the identifier, the
// severity, the title, the category and the help link of each code. The descriptors are internal, so the test reads the fields of the
// class by reflection; the examples of the diagnostics fix when a code is reported and with what text, and leave these facts to this test.
public sealed class DescriptorTests
{
    private static readonly Type DescriptorsType = Assert.Single(
        typeof(CelTypeContextGenerator).Assembly.GetTypes(),
        type => type.Name == "Descriptors" && type.Namespace == "Great.Parts.Cel.Generator");

    private static readonly DiagnosticDescriptor[] Descriptors =
    [
        .. DescriptorsType
            .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(field => field.FieldType == typeof(DiagnosticDescriptor))
            .Select(field => (DiagnosticDescriptor)field.GetValue(null)!)
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal),
    ];

    private static readonly string[] Codes = [.. Enumerable.Range(1, 9).Select(number => "GPCEL00" + number)];

    public static TheoryData<string> CodeNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (string code in Codes)
            {
                data.Add(code);
            }

            return data;
        }
    }

    [Fact]
    [Trait("Requirement", "S-2")]
    public void Descriptors_AreTheNineCodesAndNoOther() => Assert.Equal(Codes, Descriptors.Select(descriptor => descriptor.Id));

    [Theory]
    [Trait("Requirement", "S-2")]
    [MemberData(nameof(CodeNames))]
    public static void Descriptor_HasTheCategoryAndTheHelpLinkOfItsCode(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        DiagnosticDescriptor descriptor = Descriptors.Single(item => item.Id == code);
        string page = (string)DescriptorsType.GetField("HelpPage", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetRawConstantValue()!;

        Assert.Equal("Great.Parts.Cel.Generator", descriptor.Category);
        Assert.Equal(page + "#gpcel" + code["GPCEL".Length..], descriptor.HelpLinkUri);
        Assert.True(descriptor.IsEnabledByDefault);
    }

    // The anchor of GitHub is the heading in lower case: the page has a section whose heading is the code, and the section names the
    // severity and the title of the descriptor and gives the message of it.
    [Theory]
    [Trait("Requirement", "S-2")]
    [MemberData(nameof(CodeNames))]
    public static void PageOfTheDiagnostics_HasTheSectionOfTheCode(string code)
    {
        DiagnosticDescriptor descriptor = Descriptors.Single(item => item.Id == code);
        string page = File.ReadAllText(RepositoryFile("docs/generator-diagnostics.md")).Replace("\r\n", "\n", StringComparison.Ordinal);

        int start = page.IndexOf("\n## " + code + "\n", StringComparison.Ordinal);
        Assert.True(start >= 0, "no heading '## " + code + "' on the page");
        int next = page.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = next < 0 ? page[start..] : page[start..next];
        string severity = descriptor.DefaultSeverity == DiagnosticSeverity.Error ? "Error" : "Warning";

        Assert.Contains("**" + descriptor.Title + ".** Severity: " + severity + ".", section, StringComparison.Ordinal);
        Assert.Contains("Message: `" + descriptor.MessageFormat + "`", section, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "S-2")]
    public void PageOfTheDiagnostics_HasNoHeadingOfAnotherCode()
    {
        string page = File.ReadAllText(RepositoryFile("docs/generator-diagnostics.md"));

        string[] headings = [.. page.Split('\n').Select(line => line.TrimEnd('\r')).Where(line => line.StartsWith("## GPCEL", StringComparison.Ordinal))];

        Assert.Equal(Codes.Select(code => "## " + code), headings);
    }

    // The release notes list each code with its category, severity and title (rule RS2008), once.
    [Fact]
    [Trait("Requirement", "S-2")]
    public void UnshippedReleases_ListEachCodeWithItsCategorySeverityAndTitle()
    {
        string[] lines = [.. File.ReadAllLines(RepositoryFile("src/Great.Parts.Cel.Generator/AnalyzerReleases.Unshipped.md"))];

        foreach (DiagnosticDescriptor descriptor in Descriptors)
        {
            string severity = descriptor.DefaultSeverity == DiagnosticSeverity.Error ? "Error" : "Warning";
            string expected = descriptor.Id + " | " + descriptor.Category + " | " + severity + " | " + descriptor.Title;

            Assert.Equal(1, lines.Count(line => line == expected));
        }

        Assert.Equal(Codes.Length, lines.Count(line => line.StartsWith("GPCEL", StringComparison.Ordinal)));
    }

    // The directory above the output of the build that holds the file: the working copy of the repository.
    private static string RepositoryFile(string relative)
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, relative);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException("The file is not in a directory above the test output: " + relative);
    }
}
