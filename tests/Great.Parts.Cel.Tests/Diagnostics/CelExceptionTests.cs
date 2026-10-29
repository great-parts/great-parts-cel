using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Tests.Diagnostics;

public sealed class CelExceptionTests
{
    [Fact]
    public void Constructor_KeepsTheErrorsInTheirOrder()
    {
        ImmutableArray<CelError> errors =
        [
            new(CelErrorCode.SyntaxError, CelErrorStage.Parse, "syntax error: b", new CelSourceLocation(2, 1, 3), null, null),
            new(CelErrorCode.InvalidLiteral, CelErrorStage.Parse, "invalid int literal: a", new CelSourceLocation(0, 1, 1), null, null),
        ];

        CelException exception = new(errors);

        Assert.Equal(errors, exception.Errors);
    }

    [Fact]
    public void Message_ListsEachErrorOnItsLine()
    {
        ImmutableArray<CelError> errors =
        [
            new(CelErrorCode.InvalidLiteral, CelErrorStage.Parse, "invalid int literal: 99999999999999999999", new CelSourceLocation(12, 2, 4), null, null),
            new(CelErrorCode.InvalidConfiguration, CelErrorStage.Build, "invalid environment description at line 1, column 2: unknown key", default, new CelSourceLocation("environment JSON", 1, 1, 2), null),
        ];

        CelException exception = new(errors);

        Assert.Equal(
            "The result is not successful:\n"
            + "InvalidLiteral at line 2, column 4: invalid int literal: 99999999999999999999\n"
            + "InvalidConfiguration: invalid environment description at line 1, column 2: unknown key",
            exception.Message);
    }
}
