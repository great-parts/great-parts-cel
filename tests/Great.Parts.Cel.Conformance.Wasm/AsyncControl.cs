using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Great.Parts.Cel.Conformance;

/// <summary>
/// The control example of asynchronous evaluation on browser-wasm: the runtime has one thread there, and the driver needs none. The
/// expression calls a function of the application that waits twice, the second call with the result of the first, so the evaluation
/// takes three passes and two waits; the continuation after each wait runs on the one thread. The example also checks that a synchronous
/// evaluation of the same expression is the error <c>AsyncEvaluationRequired</c> and that partial evaluation leaves the unknown part.
/// </summary>
internal static class AsyncControl
{
    // The problems of the example; none when it ran as it should.
    internal static async Task<string[]> ProblemsAsync()
    {
        var problems = new List<string>();
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<long>("x")
            .AddVariable<long>("y")
            .AddAsyncFunction("inc", async (long value, CancellationToken token) =>
            {
                await Task.Yield();
                return value + 1;
            })
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<long> program = environment.Compile<long>("inc(inc(x))").GetProgramOrThrow();

        CelEvalResult<long> result = await program.EvaluateAsync(new CelBindings().Add("x", 1L), cancellationToken: CancellationToken.None).ConfigureAwait(false);
        if (!result.IsSuccess || result.Value != 3)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"inc(inc(x)) with x = 1 is not 3: {Describe(result)}"));
        }

        if (!IsAsyncEvaluationRequired(program))
        {
            problems.Add("the synchronous evaluation of a call that waits is not AsyncEvaluationRequired");
        }

        CelProgram<long> partial = environment.Compile<long>("inc(x) + y", new CelCompileOptions { PartialEvaluation = true }).GetProgramOrThrow();
        CelPartialEvalResult<long> unknown = await partial.EvaluatePartialAsync(
            new CelBindings().Add("x", 1L),
            [new CelAttributePattern("y")],
            cancellationToken: CancellationToken.None).ConfigureAwait(false);
        if (!unknown.IsUnknown || CelSyntax.Format(unknown.GetResidual()) != "2 + y")
        {
            problems.Add("the partial evaluation of inc(x) + y with y unknown does not leave 2 + y");
        }

        return [.. problems];
    }

    // Whether a synchronous evaluation of the program is the error AsyncEvaluationRequired.
    private static bool IsAsyncEvaluationRequired(CelProgram<long> program)
    {
        CelEvalResult<long> synchronous = program.Evaluate(new CelBindings().Add("x", 1L), cancellationToken: CancellationToken.None);
        return !synchronous.IsSuccess && synchronous.Errors[0].Code == Diagnostics.CelErrorCode.AsyncEvaluationRequired;
    }

    private static string Describe(CelEvalResult<long> result) =>
        result.IsSuccess ? result.Value.ToString(CultureInfo.InvariantCulture) : result.Errors[0].Code.ToString();
}
