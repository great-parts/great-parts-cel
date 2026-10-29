using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

public sealed class CelVariableDeclTests
{
    [Fact]
    public void Constructor_KeepsTheNameAndTheType()
    {
        CelVariableDecl declaration = new("request.auth", CelType.Dyn);

        Assert.Equal("request.auth", declaration.Name);
        Assert.Same(CelType.Dyn, declaration.Type);
    }

    [Fact]
    public void Constructor_NullName_Throws()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new CelVariableDecl(null!, CelType.Int));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new CelVariableDecl(string.Empty, CelType.Int));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void Constructor_NullType_Throws()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new CelVariableDecl("x", null!));

        Assert.Equal("type", exception.ParamName);
    }
}
