using System;
using System.Linq;
using Great.Parts.Cel.Generator.Tests.CallSites;
using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Output;

// The members of CelObjectType that a created description overrides are protected internal. The override of an assembly that the
// library gives the access to its internal members (InternalsVisibleTo) must be protected internal, and the override of any other
// assembly must be protected: each form is a compiler error in the other assembly. The assembly named Great.Parts.Cel.Tests is
// one of the assemblies that the library names, Application is not.
public sealed class OverrideAccessTests
{
    private const string Job =
        "public sealed class Job { public long Id { get; set; } public System.Action Callback { get; set; } = static () => { }; public HashSet<long> Tags { get; set; } = []; }\n";

    private const string Value = "global::Great.Parts.Cel.Values.CelValue";

    // The description of a class in a property of an anonymous object, with an excluded member and a set (so that the reasons and the
    // charge of a read are written) and a list of the class (so that the owner of the error of a null is written).
    [Theory]
    [Trait("Requirement", "U-7")]
    [InlineData("Application", false)]
    [InlineData("Great.Parts.Cel.Tests", true)]
    public static void CallSiteDescriptions_OverrideWithTheAccessOfTheAssembly(string assemblyName, bool internalAccess)
    {
        CallSiteRun.Outcome outcome = CallSiteRun.Run(
            CallSiteRun.Head + Job + "class C\n{\n    void M()\n    {\n"
            + "CelExpression.Evaluate<bool>(\"job.id == 1\", new { job = new Job(), jobs = new List<Job>() });\n"
            + "    }\n}\n",
            assemblyName: assemblyName);

        Assert.DoesNotContain(outcome.Reported, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(outcome.CompilerErrors);
        AssertAccess(string.Concat(outcome.Texts), internalAccess, true);
    }

    // The same for the nested class of a type context.
    [Theory]
    [Trait("Requirement", "U-7")]
    [InlineData("Application", false)]
    [InlineData("Great.Parts.Cel.Tests", true)]
    public static void TypeContextDescriptions_OverrideWithTheAccessOfTheAssembly(string assemblyName, bool internalAccess)
    {
        CSharpCompilation compilation = GeneratorExample.Compile(
            GeneratorExample.Preamble + "[CelObject(typeof(Job))]\npublic partial class Ctx : CelTypeContext { }\n" + Job,
            assemblyName: assemblyName);
        GeneratorDriverRunResult result = GeneratorExample.Run(compilation);

        Assert.DoesNotContain(GeneratorExample.Reported(result), diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(compilation.AddSyntaxTrees(result.GeneratedTrees).GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        AssertAccess(GeneratorExample.CreatedText(result), internalAccess, false);
    }

    private static void AssertAccess(string created, bool internalAccess, bool owner)
    {
        string modifiers = internalAccess ? "protected internal override " : "protected override ";
        Assert.Contains(modifiers + Value + " GetField(in global::Acme.Job instance, int number)", created, StringComparison.Ordinal);
        Assert.Contains(modifiers + "bool HasField(object instance, int number)", created, StringComparison.Ordinal);
        Assert.Contains(modifiers + "string? GetExcludedFieldReason(string fieldName)", created, StringComparison.Ordinal);
        Assert.Contains(modifiers + "long FieldReadCost(in global::Acme.Job instance, int number)", created, StringComparison.Ordinal);
        if (owner)
        {
            Assert.Contains(modifiers + Value + " GetField(in object instance, int number) => ", created, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(internalAccess ? "protected override " : "protected internal override ", created, StringComparison.Ordinal);
    }
}
