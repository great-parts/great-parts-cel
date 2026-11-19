using System;
using Microsoft.CodeAnalysis;
using Xunit;
using static Great.Parts.Cel.Generator.Tests.CallSites.CallSiteRun;

namespace Great.Parts.Cel.Generator.Tests.CallSites;

// Each row of the list of the property types of the simple entry: the type of the property in the created file, which the created
// code must compile with, and each kind of type outside the list with its reason. The examples take the list as one anonymous
// object; these tests take it row by row.
[Trait("Requirement", "U-7")]
public sealed class PropertyRuleTests
{
    private static readonly string[] Reflection = ["typeof", "GetType", "System.Reflection", "Activator", "MakeGenericType", "RequiresUnreferencedCode", "RequiresDynamicCode", "dynamic"];

    [Theory]
    [InlineData("true", "bool")]
    [InlineData("(sbyte)1", "sbyte")]
    [InlineData("(byte)1", "byte")]
    [InlineData("(short)1", "short")]
    [InlineData("(ushort)1", "ushort")]
    [InlineData("1", "int")]
    [InlineData("1u", "uint")]
    [InlineData("1L", "long")]
    [InlineData("1UL", "ulong")]
    [InlineData("1.5", "double")]
    [InlineData("\"s\"", "string")]
    [InlineData("System.Guid.Empty", "global::System.Guid")]
    [InlineData("new byte[0]", "byte[]")]
    [InlineData("new System.ReadOnlyMemory<byte>()", "global::System.ReadOnlyMemory<byte>")]
    [InlineData("System.DateTimeOffset.UnixEpoch", "global::System.DateTimeOffset")]
    [InlineData("System.DateTime.UtcNow", "global::System.DateTime")]
    [InlineData("System.TimeSpan.Zero", "global::System.TimeSpan")]
    [InlineData("default(System.Text.Json.JsonElement)", "global::System.Text.Json.JsonElement")]
    [InlineData("default(Great.Parts.Cel.Values.CelValue)", "global::Great.Parts.Cel.Values.CelValue")]
    [InlineData("(Great.Parts.Cel.Values.CelList)null!", "global::Great.Parts.Cel.Values.CelList")]
    [InlineData("(Great.Parts.Cel.Values.CelMap)null!", "global::Great.Parts.Cel.Values.CelMap")]
    [InlineData("(int?)1", "int?")]
    [InlineData("(System.Guid?)null", "global::System.Guid?")]
    [InlineData("(System.TimeSpan?)null", "global::System.TimeSpan?")]
    [InlineData("new long[0]", "long[]")]
    [InlineData("new string[0]", "string[]")]
    [InlineData("new List<string>()", "global::System.Collections.Generic.List<string>")]
    [InlineData("(IReadOnlyList<double>)null!", "global::System.Collections.Generic.IReadOnlyList<double>")]
    [InlineData("System.Collections.Immutable.ImmutableArray<int>.Empty", "global::System.Collections.Immutable.ImmutableArray<int>")]
    [InlineData("new List<object>()", "global::System.Collections.Generic.List<object>")]
    [InlineData("new List<long?>()", "global::System.Collections.Generic.List<long?>")]
    [InlineData("new List<byte[]>()", "global::System.Collections.Generic.List<byte[]>")]
    [InlineData("new List<System.Guid>()", "global::System.Collections.Generic.List<global::System.Guid>")]
    [InlineData("new Dictionary<string, bool>()", "global::System.Collections.Generic.Dictionary<string, bool>")]
    [InlineData("(IReadOnlyDictionary<string, object>)null!", "global::System.Collections.Generic.IReadOnlyDictionary<string, object>")]
    [InlineData("new Dictionary<string, System.Text.Json.JsonElement>()", "global::System.Collections.Generic.Dictionary<string, global::System.Text.Json.JsonElement>")]
    public static void RowOfTheList_IsTheTypeOfTheProperty(string value, string csharpType)
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { p = " + value + " });\n"));

        Assert.Empty(outcome.Reported);
        string text = Assert.Single(outcome.Texts);
        Assert.Contains("new { p = default(" + csharpType + ") }", text, StringComparison.Ordinal);
        Assert.Contains("builder.AddVariable<" + csharpType + ">(\"p\")", text, StringComparison.Ordinal);
        Assert.Contains("bindings.Add(\"p\", data.p)", text, StringComparison.Ordinal);
        Assert.Empty(outcome.CompilerErrors);
    }

    [Theory]
    [InlineData("1.5f", "float", "a type outside the list")]
    [InlineData("'c'", "char", "a type outside the list")]
    [InlineData("1m", "decimal", "a type outside the list")]
    [InlineData("System.DayOfWeek.Monday", "System.DayOfWeek", "a type outside the list")]
    [InlineData("new System.Uri(\"http://a\")", "System.Uri", "a type outside the list")]
    [InlineData("new Order()", "Acme.Order", "an object of the application")]
    [InlineData("(object)1", "object", "object")]
    [InlineData("new List<short>()", "System.Collections.Generic.List<short>", "a collection outside the list")]
    [InlineData("new List<System.DateTimeOffset>()", "System.Collections.Generic.List<System.DateTimeOffset>", "a collection outside the list")]
    [InlineData("new List<Order>()", "System.Collections.Generic.List<Acme.Order>", "a collection outside the list")]
    [InlineData("new Dictionary<int, long>()", "System.Collections.Generic.Dictionary<int, long>", "a collection outside the list")]
    [InlineData("new HashSet<long>()", "System.Collections.Generic.HashSet<long>", "a collection outside the list")]
    [InlineData("(IList<long>)null!", "System.Collections.Generic.IList<long>", "a collection outside the list")]
    [InlineData("new long[0, 0]", "long[,]", "a collection outside the list")]
    [InlineData("new { b = 1 }", "<anonymous type: int b>", "a nested anonymous object")]
    public static void TypeOutsideTheList_IsGPCEL011WithItsReason(string value, string shown, string reason)
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { p = " + value + " });\n"));

        Diagnostic error = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL011", error.Id);
        Assert.Equal("Property 'p' of the anonymous data has type '" + shown + "', which the CEL simple entry cannot read: " + reason + "; build an environment with a type source", error.Message());
        Assert.Empty(outcome.HintNames);
    }

    [Fact]
    public void Keywords_AreWrittenWithAtAndTheCelNameIsThePropertyName()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { @class = 1L, @int = \"s\" });\n"));

        Assert.Empty(outcome.Reported);
        string text = Assert.Single(outcome.Texts);
        Assert.Contains("new { @class = default(long), @int = default(string) }", text, StringComparison.Ordinal);
        Assert.Contains(".AddVariable<long>(\"class\").AddVariable<string>(\"int\")", text, StringComparison.Ordinal);
        Assert.Contains(".Add(\"class\", data.@class).Add(\"int\", data.@int)", text, StringComparison.Ordinal);
        Assert.Empty(outcome.CompilerErrors);
    }

    // A word that CEL reserves is a name of C#: the property is accepted by the compiler and refused by the generator, as the
    // identifier rule of the library would refuse it at run time.
    [Theory]
    [InlineData("for")]
    [InlineData("in")]
    [InlineData("null")]
    public static void NameThatIsNotACelIdentifier_IsGPCEL011AtTheProperty(string name)
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { @" + name + " = 1L });\n"));

        Diagnostic error = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL011", error.Id);
        Assert.Contains("Property '" + name + "' of the anonymous data has type 'long'", error.Message(), StringComparison.Ordinal);
        Assert.Contains("not a CEL identifier", error.Message(), StringComparison.Ordinal);
        Assert.Contains(name, Place(error), StringComparison.Ordinal);
        Assert.Empty(outcome.HintNames);
    }

    [Fact]
    public void NameWithLettersOutsideAscii_IsGPCEL011()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { café = 1L });\n"));

        Diagnostic error = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL011", error.Id);
        Assert.Contains("not a CEL identifier", error.Message(), StringComparison.Ordinal);
    }

    // The anonymous type of a generic method has the type parameter in its type: the created file cannot write it.
    [Fact]
    public void PropertyOfATypeParameter_IsGPCEL011()
    {
        Outcome outcome = Run(Head + "class C\n{\n    long M<T>(T value) => CelExpression.Evaluate<long>(\"1\", new { value });\n}\n");

        Diagnostic error = Assert.Single(outcome.Reported);
        Assert.Equal("GPCEL011", error.Id);
        Assert.Contains("a type parameter of the enclosing method", error.Message(), StringComparison.Ordinal);
    }

    // The properties that cannot be read are all reported, each at its own place, and the shape gives no file.
    [Fact]
    public void SeveralPropertiesOutsideTheList_AreSeveralErrors()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { a = 1L, order = new Order(), f = 1.5f });\n"));

        Assert.Equal(2, outcome.Reported.Length);
        Assert.All(outcome.Reported, static diagnostic => Assert.Equal("GPCEL011", diagnostic.Id));
        Assert.Empty(outcome.HintNames);
    }

    // One anonymous type that two calls pass is one error: the type has one place for each property.
    [Fact]
    public void SameBadAnonymousTypeAtTwoCalls_IsOneError()
    {
        Outcome outcome = Run(Body(
            "var d = new { order = new Order() };\n"
            + "CelExpression.Evaluate<long>(\"1\", d);\n"
            + "CelExpression.Evaluate<long>(\"2\", d);\n"));

        Assert.Single(outcome.Reported);
    }

    [Fact]
    public void AnonymousObjectWithoutProperties_IsOneFileThatCompiles()
    {
        Outcome outcome = Run(Body("CelExpression.Evaluate<long>(\"1\", new { });\n"));

        Assert.Empty(outcome.Reported);
        Assert.Single(outcome.HintNames);
        Assert.Empty(outcome.CompilerErrors);
    }

    [Fact]
    public void CreatedCode_HasNoReflectionAndNoAnnotationOfTrimming()
    {
        Outcome outcome = Run(Body(
            "CelExpression.Evaluate<long>(\"1\", new { b = true, s = \"x\", ids = new List<long>(), map = new Dictionary<string, double>(), t = System.TimeSpan.Zero });\n"));

        string text = Assert.Single(outcome.Texts);
        foreach (string word in Reflection)
        {
            Assert.DoesNotContain(word, text, StringComparison.Ordinal);
        }
    }
}
