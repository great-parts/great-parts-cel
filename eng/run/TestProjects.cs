using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>A test project of the solution.</summary>
/// <param name="Name">The name of the project and of its assembly.</param>
/// <param name="Directory">The directory of the project relative to the repository root, with <c>/</c>.</param>
/// <param name="Targets">The target frameworks of the project.</param>
/// <param name="PublishAot">Whether the project is published under Native AOT.</param>
internal sealed record TestProject(string Name, string Directory, IReadOnlyList<string> Targets, bool PublishAot);

/// <summary>
/// Finds the test projects of the solution, so that a new test project is run without a change of <c>eng/run</c>. A test project is a
/// project of the solution with a package reference to <c>xunit.v3.*</c>; it is published under Native AOT when it sets
/// <c>PublishAot</c> to <c>true</c>.
/// </summary>
internal static class TestProjects
{
    private const string TestFramework = "xunit.v3";

    /// <summary>Reads the solution and its projects in the order of the solution.</summary>
    /// <param name="solution">The path of the solution file.</param>
    /// <exception cref="InvalidOperationException">The solution has no test project, or a test project has no target framework.</exception>
    internal static IReadOnlyList<TestProject> Find(string solution)
    {
        List<TestProject> projects = [];
        foreach (string path in XDocument.Load(solution).Descendants("Project").Select(project => (string?)project.Attribute("Path")).OfType<string>())
        {
            if (Read(path) is { } project)
            {
                projects.Add(project);
            }
        }

        return projects.Count > 0 ? projects : throw new InvalidOperationException($"{solution} has no test project: a project with a package reference to {TestFramework}.*");
    }

    private static TestProject? Read(string path)
    {
        XDocument document = XDocument.Load(path);
        bool isTest = document.Descendants("PackageReference")
            .Any(reference => ((string?)reference.Attribute("Include"))?.StartsWith(TestFramework, StringComparison.Ordinal) == true);
        if (!isTest)
        {
            return null;
        }

        string frameworks = Property(document, "TargetFrameworks") ?? Property(document, "TargetFramework")
            ?? throw new InvalidOperationException($"{path} has no target framework");
        string[] targets = frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string name = Property(document, "AssemblyName") ?? Path.GetFileNameWithoutExtension(path);
        string directory = (Path.GetDirectoryName(path) ?? string.Empty).Replace('\\', '/');
        return new TestProject(name, directory, targets, Property(document, "PublishAot") == "true");
    }

    private static string? Property(XDocument document, string name) => document.Descendants(name).FirstOrDefault()?.Value.Trim();
}
