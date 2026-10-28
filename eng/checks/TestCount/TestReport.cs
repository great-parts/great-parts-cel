using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// An xUnit.net v2+ XML report of one test module and run, read for <c>test-count</c>
/// (<c>docs/design/eng-checks-artifacts.md</c>, "test-count"). Only the tests that ran are kept: a test with the result <c>NotRun</c> is
/// not counted. A test is the pair of its <c>type</c> and <c>name</c> attributes; the first element of a pair gives its place.
/// </summary>
internal sealed class TestReport
{
    private const string NotRunResult = "NotRun";

    private TestReport(string path, IReadOnlyList<string> assemblies, IReadOnlyDictionary<(string Type, string Name), ReportedTest> tests)
    {
        Path = path;
        Assemblies = assemblies;
        Tests = tests;
    }

    /// <summary>The full path of the report file.</summary>
    internal string Path { get; }

    /// <summary>The <c>name</c> attributes of the <c>&lt;assembly&gt;</c> elements: the files that the run loaded or started.</summary>
    internal IReadOnlyList<string> Assemblies { get; }

    /// <summary>The tests that ran by the pair of <c>type</c> and <c>name</c>.</summary>
    internal IReadOnlyDictionary<(string Type, string Name), ReportedTest> Tests { get; }

    /// <summary>Reads a report.</summary>
    /// <param name="path">The full path of the report.</param>
    /// <exception cref="CheckInputException">The file cannot be read or is not an xUnit.net report.</exception>
    internal static TestReport Read(string path)
    {
        XDocument document;
        try
        {
            using FileStream stream = File.OpenRead(path);
            document = XDocument.Load(stream, LoadOptions.SetLineInfo);
        }
        catch (Exception exception) when (exception is XmlException or IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new CheckInputException($"the test report {path} cannot be read: {FirstLine(exception.Message)}", exception);
        }

        XElement? root = document.Root;
        if (root is null || (root.Name.LocalName != "assemblies" && root.Name.LocalName != "assembly"))
        {
            throw new CheckInputException($"the file {path} is not an xUnit.net XML report: its root element is not 'assemblies' or 'assembly'");
        }

        List<string> assemblies = [];
        foreach (XElement assembly in root.DescendantsAndSelf("assembly"))
        {
            if (assembly.Attribute("name")?.Value is { Length: > 0 } name)
            {
                assemblies.Add(name);
            }
        }

        Dictionary<(string Type, string Name), ReportedTest> tests = [];
        foreach (XElement element in root.Descendants("test"))
        {
            if (element.Attribute("result")?.Value != NotRunResult)
            {
                ReportedTest test = ReadTest(path, element);
                tests.TryAdd((test.Type, test.Name), test);
            }
        }

        return new TestReport(path, assemblies, tests);
    }

    private static ReportedTest ReadTest(string path, XElement element)
    {
        int elementLine = element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 1;
        if (element.Attribute("type")?.Value is not { Length: > 0 } type || element.Attribute("name")?.Value is not { Length: > 0 } name)
        {
            throw new CheckInputException($"the test report {path} has a <test> element without a 'type' or a 'name' attribute at line {elementLine}");
        }

        int line = int.TryParse(element.Attribute("source-line")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;
        return new ReportedTest(
            type,
            name,
            element.Attribute("method")?.Value is { Length: > 0 } method ? method : MethodOf(name),
            element.Attribute("source-file")?.Value,
            line,
            elementLine);
    }

    // The full name without the arguments of a theory case, after its last dot.
    private static string MethodOf(string name)
    {
        int arguments = name.IndexOf('(', StringComparison.Ordinal);
        string withoutArguments = arguments < 0 ? name : name[..arguments];
        return withoutArguments[(withoutArguments.LastIndexOf('.') + 1)..];
    }

    private static string FirstLine(string text)
    {
        int end = text.IndexOf('\n', StringComparison.Ordinal);
        return (end < 0 ? text : text[..end]).TrimEnd();
    }
}
