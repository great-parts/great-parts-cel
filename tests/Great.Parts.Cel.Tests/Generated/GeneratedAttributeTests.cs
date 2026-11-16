using System;
using System.Reflection;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Generated;

// The bodies of the attributes that the source generator reads: what the constructors refuse and the defaults of the options.
public sealed class GeneratedAttributeTests
{
    private static readonly string[] IgnoredMembers = ["Secret", "Internal"];

    [Fact]
    [Trait("Requirement", "F-2")]
    public void CelObjectAttribute_NullType_IsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("type", () => new CelObjectAttribute(null!));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void CelObjectAttribute_KeepsTheTypeAndHasNoNameAndNoIgnoredMembersByDefault()
    {
        var attribute = new CelObjectAttribute(typeof(GeneratedAttributeTests));

        Assert.Same(typeof(GeneratedAttributeTests), attribute.Type);
        Assert.Null(attribute.Name);
        Assert.Null(attribute.IgnoreMembers);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void CelNameAttribute_NullName_IsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>("name", () => new CelNameAttribute(null!));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void CelNameAttribute_KeepsTheNameAsItIs() => Assert.Equal("in", new CelNameAttribute("in").Name);

    [Fact]
    [Trait("Requirement", "F-2")]
    public void CelTypeContextOptionsAttribute_Defaults_AreCamelCaseAndTheJsonAttributesRead()
    {
        var options = new CelTypeContextOptionsAttribute();

        Assert.Equal(CelNamingPolicy.CamelCase, options.NamingPolicy);
        Assert.False(options.IgnoreJsonAttributes);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void CelObjectAttribute_KeepsTheNameAndTheIgnoredMembersThatAreSet()
    {
        var attribute = new CelObjectAttribute(typeof(GeneratedAttributeTests)) { Name = "acme.Test", IgnoreMembers = [.. IgnoredMembers] };

        Assert.Equal("acme.Test", attribute.Name);
        Assert.Equal(IgnoredMembers, attribute.IgnoreMembers);
    }

    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData(CelNamingPolicy.CamelCase, 0)]
    [InlineData(CelNamingPolicy.AsIs, 1)]
    [InlineData(CelNamingPolicy.SnakeCaseLower, 2)]
    public static void CelNamingPolicy_NumericValues_AreFixed(CelNamingPolicy policy, int number)
    {
        Assert.Equal(number, (int)policy);
    }

    [Theory]
    [Trait("Requirement", "F-2")]
    [InlineData(CelNamingPolicy.AsIs, true)]
    [InlineData(CelNamingPolicy.SnakeCaseLower, false)]
    public static void CelTypeContextOptionsAttribute_KeepsThePolicyAndTheJsonSwitchThatAreSet(CelNamingPolicy policy, bool ignoreJsonAttributes)
    {
        var options = new CelTypeContextOptionsAttribute { NamingPolicy = policy, IgnoreJsonAttributes = ignoreJsonAttributes };

        Assert.Equal(policy, options.NamingPolicy);
        Assert.Equal(ignoreJsonAttributes, options.IgnoreJsonAttributes);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void CelIgnoreAttribute_MarksAPropertyOrAFieldOnceAndHasNoOptions()
    {
        AttributeUsageAttribute? usage = typeof(CelIgnoreAttribute).GetCustomAttribute<AttributeUsageAttribute>(inherit: false);

        Assert.NotNull(usage);
        Assert.Equal(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.Empty(typeof(CelIgnoreAttribute).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }
}
