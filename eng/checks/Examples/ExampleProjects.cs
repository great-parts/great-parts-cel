using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The files of the build of the examples (<c>docs/design/eng-docs.md</c>, "examples"): a project for every example, with
/// <c>Program.cs</c>, and one solution that lists them all. The projects are written outside the repository, so the strict settings
/// of the repository do not apply and an example is checked like the application of a user.
/// </summary>
internal static class ExampleProjects
{
    /// <summary>The project of the core package, relative to the repository root.</summary>
    internal const string LibraryProject = "src/Great.Parts.Cel/Great.Parts.Cel.csproj";

    /// <summary>The project of the Protobuf package, relative to the repository root.</summary>
    internal const string ProtobufProject = "src/Great.Parts.Cel.Protobuf/Great.Parts.Cel.Protobuf.csproj";

    /// <summary>The project of the source generator, which the package adds to the compiler of an application as an analyzer.</summary>
    internal const string GeneratorProject = "src/Great.Parts.Cel.Generator/Great.Parts.Cel.Generator.csproj";

    /// <summary>The file name of the generated solution.</summary>
    internal const string SolutionFile = "Examples.slnx";

    /// <summary>The name of the project of the example with the zero-based index: <c>Example0001</c> for the first.</summary>
    /// <param name="index">The index of the example in the list of the built examples.</param>
    internal static string Name(int index) => string.Create(CultureInfo.InvariantCulture, $"Example{index + 1:0000}");

    /// <summary>The path of the assembly that the build of the example writes, relative to the directory of the solution.</summary>
    /// <param name="index">The index of the example in the list of the built examples.</param>
    /// <param name="target">The target framework of the example.</param>
    internal static string Assembly(int index, string target) => $"{Name(index)}/bin/Release/{target}/{Name(index)}.dll";

    /// <summary>
    /// The project file of an example: a console application for its target with nullable reference types, implicit usings and warnings
    /// as errors, referencing both package projects, and the generator as an analyzer, as the package does. No application host is
    /// made: the program runs with <c>dotnet</c>. The audit of packages is off, so a new advisory does not break a build of examples.
    /// </summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <param name="example">The example.</param>
    internal static string ProjectText(string root, Example example)
    {
        XElement references = new("ItemGroup");
        foreach (string project in new[] { LibraryProject, ProtobufProject })
        {
            references.Add(new XElement("ProjectReference", new XAttribute("Include", Path.Combine(root, project))));
        }

        string generator = Path.Combine(root, GeneratorProject);
        if (File.Exists(generator))
        {
            references.Add(new XElement(
                "ProjectReference",
                new XAttribute("Include", generator),
                new XAttribute("OutputItemType", "Analyzer"),
                new XAttribute("ReferenceOutputAssembly", "false")));
        }

        XElement properties = new(
            "PropertyGroup",
            new XElement("OutputType", "Exe"),
            new XElement("TargetFramework", example.Target),
            new XElement("Nullable", "enable"),
            new XElement("ImplicitUsings", "enable"),
            new XElement("TreatWarningsAsErrors", "true"),
            new XElement("UseAppHost", "false"),
            new XElement("NuGetAudit", "false"));
        return new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"), properties, references).ToString() + "\n";
    }

    /// <summary>
    /// <c>Program.cs</c> of an example: its code behind a <c>#line</c> directive, so that the compiler points at the line of the file that
    /// holds the example.
    /// </summary>
    /// <param name="example">The example.</param>
    internal static string ProgramText(Example example) =>
        $"#line {example.FirstLine.ToString(CultureInfo.InvariantCulture)} \"{example.Path}\"\n{string.Join('\n', example.Lines)}\n";

    /// <summary>The solution that lists the projects of the examples.</summary>
    /// <param name="count">The number of examples.</param>
    internal static string SolutionText(int count)
    {
        IEnumerable<XElement> projects = Enumerable.Range(0, count)
            .Select(index => new XElement("Project", new XAttribute("Path", $"{Name(index)}/{Name(index)}.csproj")));
        return new XElement("Solution", projects).ToString() + "\n";
    }
}
