using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions;

// What no function of the library takes an optional for: a key of a map, the operand of an ordering, string(), json.encode and a clause
// of format. The value reaches each of them as the value of a variable of type dyn, so type checking lets the call through and the
// error is that of evaluation.
public sealed class OptionalRefusalTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddExtension(CelExtensions.Encoders(1))
        .AddExtension(CelExtensions.Strings(5))
        .AddVariable("d", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("{d: 1}", CelErrorCode.UnsupportedMapKeyType)]
    [InlineData("d < d", CelErrorCode.EvalNoMatchingOverload)]
    [InlineData("d >= d", CelErrorCode.EvalNoMatchingOverload)]
    [InlineData("string(d)", CelErrorCode.EvalNoMatchingOverload)]
    [InlineData("json.encode(d)", CelErrorCode.InvalidArgument)]
    [InlineData("'%s'.format([d])", CelErrorCode.InvalidArgument)]
    public static void OptionalValue_WhereNoOverloadTakesIt_IsAnErrorOfEvaluation(string expression, CelErrorCode code)
    {
        foreach (CelValue value in new[] { CelValue.FromOptional(default), CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(1))) })
        {
            CelBindings bindings = new CelBindings().Add("d", value);

            CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow()
                .Evaluate(bindings, cancellationToken: CancellationToken.None);

            CelError error = Assert.Single(result.Errors);
            Assert.Equal((code, CelErrorStage.Evaluate), (error.Code, error.Stage));
        }
    }

    [Fact]
    public static void OptionalAsAMapKey_NamesTheKindInTheMessage()
    {
        CelBindings bindings = new CelBindings().Add("d", CelValue.FromOptional(default));

        CelError error = Assert.Single(Environment.Compile<CelValue>("{d: 1}").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).Errors);

        Assert.Contains("optional_type", error.Message, StringComparison.Ordinal);
    }
}
