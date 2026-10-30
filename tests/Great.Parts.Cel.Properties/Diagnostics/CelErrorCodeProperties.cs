using System;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Properties.Diagnostics;

public sealed class CelErrorCodeProperties
{
    [Fact]
    public void EveryCode_IsInTheRangeOfAStage()
    {
        Assert.All(
            Enum.GetValues<CelErrorCode>(),
            code =>
            {
                int number = (int)code;
                Assert.True(number is >= 1000 and < 5000 or >= 9000 and < 10000, $"{code} = {number}");
            });
    }
}
