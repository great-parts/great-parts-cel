using System;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Generated;

// The bodies of the attributes that the source generator reads: what the constructors refuse and the defaults of the options.
public sealed class GeneratedAttributeTests
{
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
}
