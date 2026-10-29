using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

public sealed class CelBuildResultTests
{
    [Fact]
    public void SuccessfulResult_HoldsTheEnvironmentAndNoErrors()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Same(result.Environment, result.GetEnvironmentOrThrow());
    }

    [Fact]
    public void UnsuccessfulResult_HoldsTheErrors()
    {
        ImmutableArray<CelError> errors = [Error("first"), Error("second")];

        CelBuildResult result = new(errors);

        Assert.False(result.IsSuccess);
        Assert.Equal(errors, result.Errors);
    }

    [Fact]
    public void UnsuccessfulResult_Environment_Throws()
    {
        CelBuildResult result = new([Error("first")]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => result.Environment);

        Assert.Equal("The environment was not built: the result has build errors.", exception.Message);
    }

    [Fact]
    public void UnsuccessfulResult_GetEnvironmentOrThrow_ThrowsWithTheErrors()
    {
        ImmutableArray<CelError> errors = [Error("first"), Error("second")];
        CelBuildResult result = new(errors);

        CelException exception = Assert.Throws<CelException>(result.GetEnvironmentOrThrow);

        Assert.Equal(errors, exception.Errors);
    }

    private static CelError Error(string message) =>
        new(CelErrorCode.InvalidConfiguration, CelErrorStage.Build, message, default, null, null);
}
