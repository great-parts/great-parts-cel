using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.AspNetCore;

// The validation rule of a request: the expression over the fields of TRequest, compiled once in the environment derived from the
// registered one with TRequest as its input. A build or a compilation error is kept instead of the program and fails every request.
internal sealed class RequestRule<TRequest>
{
    private readonly CelProgram<bool>? _program;
    private readonly CelError? _error;

    internal RequestRule(CelEnvironment environment, string expression)
    {
        CelBuildResult build = environment.ToBuilder().UseInput<TRequest>().Build();
        if (!build.IsSuccess)
        {
            _error = build.Errors[0];
            return;
        }

        CelCompileResult<bool> compiled = build.GetEnvironmentOrThrow().Compile<bool>(expression);
        if (compiled.IsSuccess)
        {
            _program = compiled.GetProgramOrThrow();
        }
        else
        {
            _error = compiled.Errors[0];
        }
    }

    // True when the expression gives true; otherwise false, with the error when there is one and null when the expression gave false.
    internal bool Passes(TRequest request, out CelError? error)
    {
        if (_program is null)
        {
            error = _error;
            return false;
        }

        CelEvalResult<bool> result = _program.EvaluateInput(request, cancellationToken: CancellationToken.None);
        if (!result.IsSuccess)
        {
            error = result.Errors[0];
            return false;
        }

        error = null;
        return result.Value;
    }

    // The first argument of the type TRequest among the arguments of a handler or an action.
    internal static bool TryFind(IEnumerable<object?> arguments, [MaybeNullWhen(false)] out TRequest request)
    {
        foreach (object? argument in arguments)
        {
            if (argument is TRequest typed)
            {
                request = typed;
                return true;
            }
        }

        request = default;
        return false;
    }

    // The errors of the validation problem: the message of the rule, as an error of the whole request.
    internal static Dictionary<string, string[]> Errors(string message) => new(StringComparer.Ordinal) { [string.Empty] = [message] };

    // The detail of the validation problem: why the rule did not run to a result; null when the expression gave false.
    internal static string? Detail(bool found, CelError? error) =>
        !found
            ? string.Create(CultureInfo.InvariantCulture, $"The request has no argument of the type {typeof(TRequest).Name}.")
            : error is null
                ? null
                : string.Create(CultureInfo.InvariantCulture, $"The validation rule failed with the error {error.Code}.");
}
