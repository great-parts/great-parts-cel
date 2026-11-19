using System;
using Xunit;
using static Great.Parts.Cel.Generator.Tests.CallSites.CallSiteRun;

namespace Great.Parts.Cel.Generator.Tests.CallSites;

// The forms of the call that the generator reads, and the one it does not: the examples fix the full name, using static and an alias;
// these tests take the named arguments, the number of the files across source files, and the call through a method group.
[Trait("Requirement", "U-7")]
public sealed class CallFormTests
{
    [Fact]
    public void NamedDataArgument_IsFoundInAnyPosition()
    {
        Outcome outcome = Run(Body(
            "CelExpression.Evaluate<long>(expression: \"x\", data: new { x = 1L });\n"
            + "CelExpression.Evaluate<long>(data: new { y = 1L }, expression: \"y\");\n"
            + "CelExpression.Evaluate<long>(\"z\", costLimit: 10, data: new { z = 1L });\n"));

        Assert.Empty(outcome.Reported);
        Assert.Equal(3, outcome.HintNames.Length);
        Assert.Empty(outcome.CompilerErrors);
    }

    [Fact]
    public void NamedDataArgumentOfAHiddenType_IsGPCEL010AtTheArgument()
    {
        Outcome outcome = Run(Body("object o = new { x = 1L };\nCelExpression.Evaluate<long>(data: o, expression: \"x\");\n"));

        Assert.Equal("GPCEL010", Assert.Single(outcome.Reported).Id);
        Assert.Equal("o", Place(outcome.Reported[0]));
    }

    // The files are numbered in the order of the first call by the path of the file and the position: the path of More0.cs is before
    // the one of Source.cs, so the shape that is first called there is the first file even though the call is in the second source.
    [Fact]
    public void FilesAreNumberedByTheFirstCallInTheOrderOfPathAndPosition()
    {
        const string Other = "using Great.Parts.Cel;\nnamespace Acme;\nclass D\n{\n    void M() { CelExpression.Evaluate<long>(\"a\", new { early = 1L }); }\n}\n";

        Outcome outcome = Run(
            Body("CelExpression.Evaluate<long>(\"1\", new { late = 1L });\nCelExpression.Evaluate<long>(\"2\", new { early = 1L });\n"),
            [Other]);

        Assert.Equal(["CelAnonymousData_1.g.cs", "CelAnonymousData_2.g.cs"], outcome.HintNames);
        Assert.Contains("early", outcome.Texts[0], StringComparison.Ordinal);
        Assert.Contains("late", outcome.Texts[1], StringComparison.Ordinal);
    }

    // A call through a delegate is not a call of the method, so the generator does not see it: no warning and no file; at run time it
    // works by the registry.
    [Fact]
    public void CallThroughAMethodGroup_IsNotSeen()
    {
        Outcome outcome = Run(Body(
            "System.Func<string, object, ulong, System.Threading.CancellationToken, long> f = CelExpression.Evaluate<long>;\n"
            + "f(\"x\", new { x = 1L }, 10UL, default);\n"));

        Assert.Empty(outcome.Reported);
        Assert.Empty(outcome.HintNames);
    }

    // The overload with a dictionary is not the overload with the object: a dictionary of any type is of no interest to the generator.
    [Fact]
    public void CallOfTheOverloadWithADictionary_IsNotSeen()
    {
        Outcome outcome = Run(Body(
            "CelExpression.Evaluate<long>(\"1\", new Dictionary<string, object?>());\n"
            + "CelExpression.Evaluate<long>(\"1\");\n"));

        Assert.Empty(outcome.Reported);
        Assert.Empty(outcome.HintNames);
    }

    [Fact]
    public void DictionaryThatTheEntryReadsAtRunTime_ThroughItsInterfaces_IsNothing()
    {
        Outcome outcome = Run(
            Head + "sealed class Map : Dictionary<string, double> { }\nclass C\n{\n    void M() { CelExpression.Evaluate<long>(\"1\", new Map()); }\n}\n");

        Assert.Empty(outcome.Reported);
        Assert.Empty(outcome.HintNames);
    }

    [Fact]
    public void ClassThatImplementsADictionaryOutsideTheList_IsGPCEL010WithTheAdviceOfDictionaries()
    {
        Outcome outcome = Run(
            Head + "sealed class Map : Dictionary<int, double> { }\nclass C\n{\n    void M() { CelExpression.Evaluate<long>(\"1\", new Map()); }\n}\n");

        Assert.Equal("GPCEL010", Assert.Single(outcome.Reported).Id);
        Assert.EndsWith("pass a dictionary with string keys and values of the list, or an anonymous object", outcome.Reported[0].Message(), StringComparison.Ordinal);
    }

    [Fact]
    public void AdviceDependsOnTheStaticType()
    {
        Outcome wrapper = Run(Head + "class C\n{\n    long M<T>(T d) where T : notnull => CelExpression.Evaluate<long>(\"1\", d);\n}\n");
        Outcome named = Run(Body("CelExpression.Evaluate<long>(\"1\", new Order());\n"));

        Assert.EndsWith(": pass the anonymous object at the call or pass a dictionary", Assert.Single(wrapper.Reported).Message(), StringComparison.Ordinal);
        Assert.EndsWith(": build an environment with a type source and UseInput<T>", Assert.Single(named.Reported).Message(), StringComparison.Ordinal);
    }
}
