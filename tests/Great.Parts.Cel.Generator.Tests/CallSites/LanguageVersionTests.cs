using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static Great.Parts.Cel.Generator.Tests.CallSites.CallSiteRun;

namespace Great.Parts.Cel.Generator.Tests.CallSites;

// GPCEL008 of the generator of call sites: the created files use file-local classes, so a project below C# 12 gets no file and one
// error for the compilation, at its first call of CelExpression.Evaluate with an anonymous object; and the advice of GPCEL011 for a
// name that is not a CEL identifier, which a type source does not correct.
[Trait("Requirement", "U-7")]
public sealed class LanguageVersionTests
{
    private const string TwoCalls = "CelExpression.Evaluate<long>(\"1\", new { a = 1L });\nCelExpression.Evaluate<long>(\"2\", new { b = 2L });\n";

    [Theory]
    [InlineData(LanguageVersion.CSharp11, "11")]
    [InlineData(LanguageVersion.CSharp10, "10")]
    public static void LanguageBelow12_IsOneGPCEL008AtTheFirstCall_AndNoFile(LanguageVersion version, string shown)
    {
        Outcome outcome = Run(Body(TwoCalls), version: version);

        Diagnostic error = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL008", error.Id);
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Equal("The code generated for type context 'CelExpression.Evaluate' requires C# 12 or later; the project uses C# " + shown, error.Message());
        Assert.Equal("CelExpression.Evaluate<long>(\"1\", new { a = 1L })", Place(error));
        Assert.Empty(outcome.HintNames);
    }

    [Theory]
    [InlineData(LanguageVersion.CSharp12)]
    [InlineData(LanguageVersion.Latest)]
    public static void Language12OrLater_HasNoGPCEL008_AndOneFileForEachShape(LanguageVersion version)
    {
        Outcome outcome = Run(Body(TwoCalls), version: version);

        Assert.Empty(outcome.Reported);
        Assert.Equal(2, outcome.HintNames.Length);
    }

    // Only a call with an anonymous object needs a created file: the warning of a data type that is not anonymous is the same.
    [Fact]
    public void LanguageBelow12_KeepsTheWarningOfADataTypeThatIsNotAnonymous()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new Order());\n"), version: LanguageVersion.CSharp11);

        Diagnostic warning = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL010", warning.Id);
    }

    // The error of a property and the version are both reported: the first does not hide the second.
    [Fact]
    public void LanguageBelow12_ReportsThePropertyErrorToo()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { a = 1.5f });\n"), version: LanguageVersion.CSharp11);

        string[] ids = [.. outcome.Reported.Select(diagnostic => diagnostic.Id).Order(StringComparer.Ordinal)];
        Assert.Equal(["GPCEL008", "GPCEL011"], ids);
        Assert.Empty(outcome.HintNames);
    }

    [Fact]
    public void NameThatIsNotACelIdentifier_AdvisesToRenameTheProperty()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { @for = 1L });\n"));

        Diagnostic error = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL011", error.Id);
        Assert.Equal("Property 'for' of the anonymous data has type 'long', which the CEL simple entry cannot read: not a CEL identifier; rename the property", error.Message());
    }
}
