using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Types;

public sealed class CelTypeKindTests
{
    [Theory]
    [InlineData(CelTypeKind.Dyn, 0)]
    [InlineData(CelTypeKind.Bool, 2)]
    [InlineData(CelTypeKind.String, 6)]
    [InlineData(CelTypeKind.Map, 12)]
    [InlineData(CelTypeKind.Optional, 17)]
    public static void Kind_HasItsPublishedNumber(CelTypeKind kind, int number)
    {
        Assert.Equal(number, (int)kind);
    }
}
