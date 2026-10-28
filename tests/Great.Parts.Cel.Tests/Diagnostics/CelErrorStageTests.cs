using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

public sealed class CelErrorStageTests
{
    [Fact]
    public void Stages_FollowThePipelineOrder()
    {
        Assert.Equal(
            [CelErrorStage.Build, CelErrorStage.Parse, CelErrorStage.Check, CelErrorStage.Evaluate],
            [(CelErrorStage)0, (CelErrorStage)1, (CelErrorStage)2, (CelErrorStage)3]);
    }
}
