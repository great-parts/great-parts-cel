using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// The library side of a run (docs/design/eng-difftest.md, "A run", steps 2 and 4), through the public API only: the
/// environment of the run manifest, <c>Compile&lt;CelValue&gt;</c> and <c>Evaluate</c>. An exception from the library counts
/// as an error of the step it escaped from, and the message names its type.
/// </summary>
internal sealed class LibrarySide : ICaseSide
{
    private readonly CelEnvironment environment;

    private LibrarySide(CelEnvironment environment) => this.environment = environment;

    /// <summary>
    /// Step 2 of a run: <c>new CelEnvironmentBuilder(CelStandardLibrary.V1)</c>, <c>CelEnvironmentJson.Apply</c> with the
    /// manifest and <c>Build</c>. An exception of the library escapes to the caller, which ends the run.
    /// </summary>
    /// <param name="manifest">The run manifest, UTF-8 JSON.</param>
    /// <param name="side">The library side, or <see langword="null"/> when the manifest gives no environment.</param>
    /// <param name="problem">Why the library gives no environment, one line; empty when it gives one.</param>
    /// <returns><see langword="true"/> when the library built the environment.</returns>
    internal static bool TryBuild(ReadOnlySpan<byte> manifest, out LibrarySide? side, out string problem)
    {
        side = null;
        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        ImmutableArray<CelError> applied = CelEnvironmentJson.Apply(builder, manifest);
        if (!applied.IsDefaultOrEmpty)
        {
            problem = "the library does not apply the run manifest: " + Messages(applied);
            return false;
        }

        CelBuildResult built = builder.Build();
        if (!built.IsSuccess || built.Environment is null)
        {
            problem = "the library does not build the environment of the run manifest: " + Messages(built.Errors);
            return false;
        }

        side = new LibrarySide(built.Environment);
        problem = string.Empty;
        return true;
    }

    /// <summary>A line that names an exception: its type and the first line of its message.</summary>
    /// <param name="exception">The exception.</param>
    internal static string Describe(Exception exception)
    {
        string message = exception.Message;
        int end = message.AsSpan().IndexOfAny('\r', '\n');
        return exception.GetType().Name + ": " + (end < 0 ? message : message[..end]);
    }

    /// <inheritdoc/>
    public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings)
    {
        CelCompileResult<CelValue> compiled;
        try
        {
            compiled = environment.Compile<CelValue>(expression);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Compile parses and checks; the library does not say which of the two threw.
            return SideOutcome.CompileError(CompileStage.Check, "the library threw " + Describe(exception));
        }

        if (!compiled.IsSuccess || compiled.Program is null)
        {
            CompileStage stage = compiled.Errors.Any(error => error.Stage == CelErrorStage.Parse) ? CompileStage.Parse : CompileStage.Check;
            return SideOutcome.CompileError(stage, Messages(compiled.Errors));
        }

        try
        {
            CelBindings values = new();
            foreach ((string name, Value value) in bindings)
            {
                values.Add(name, ValueConversion.ToCelValue(value));
            }

            CelEvalResult<CelValue> result = compiled.Program.Evaluate(values, null, CancellationToken.None);
            return result.IsSuccess
                ? SideOutcome.Of(ValueConversion.ToExprValue(result.Value))
                : SideOutcome.EvaluationError(Messages(result.Errors));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return SideOutcome.EvaluationError("the library threw " + Describe(exception));
        }
    }

    /// <summary>The profile has no time zone data file until time, so the library side has no offsets yet.</summary>
    /// <param name="zone">The name of the zone.</param>
    /// <param name="instant">The instant.</param>
    /// <returns>Nothing: it always throws.</returns>
    /// <exception cref="NotSupportedException">Always.</exception>
    public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) =>
        throw new NotSupportedException("the profile compares no time zone offsets until time");

    private static string Messages(ImmutableArray<CelError> errors) =>
        errors.IsDefaultOrEmpty ? "no error message" : string.Join("; ", errors.Select(error => error.Message));
}
