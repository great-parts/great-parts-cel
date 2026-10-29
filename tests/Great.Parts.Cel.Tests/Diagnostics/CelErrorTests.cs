using System;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

public sealed class CelErrorTests
{
    [Fact]
    public void Constructor_KeepsEveryArgument()
    {
        CelSourceLocation location = new(4, 1, 5);
        CelSourceLocation sourceLocation = new("rules/order.cel", 104, 7, 9);
        InvalidOperationException cause = new("failed");

        CelError error = new(
            CelErrorCode.ApplicationException,
            CelErrorStage.Evaluate,
            "function 'f' threw an exception",
            location,
            sourceLocation,
            cause);

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        Assert.Equal("function 'f' threw an exception", error.Message);
        Assert.Equal(location, error.Location);
        Assert.Equal(sourceLocation, error.SourceLocation);
        Assert.Same(cause, error.Exception);
    }

    [Fact]
    public void Constructor_WithoutPositionsAndCause_KeepsThemEmpty()
    {
        CelError error = new(CelErrorCode.InvalidConfiguration, CelErrorStage.Build, "invalid", default, null, null);

        Assert.Equal(0, error.Location.Line);
        Assert.Null(error.SourceLocation);
        Assert.Null(error.Exception);
    }
}
