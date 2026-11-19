using System;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Microsoft.Extensions.Options;

namespace Great.Parts.Cel.DependencyInjection;

// The check that ValidateCel adds: the expression over the fields of TOptions, in the environment derived from the registered one with
// TOptions as its input. The derived environment and the program are made at the first check and kept; a build or a compilation error
// is kept instead and fails every check.
internal sealed class CelOptionsValidation<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly string _optionsName;
    private readonly string _failureMessage;
    private readonly Lazy<(CelProgram<bool>? Program, CelError? Error)> _rule;

    internal CelOptionsValidation(string optionsName, CelEnvironment environment, string expression, string failureMessage)
    {
        _optionsName = optionsName;
        _failureMessage = failureMessage;
        _rule = new Lazy<(CelProgram<bool>?, CelError?)>(() => Compile(environment, expression), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        // As the checks of OptionsBuilder: only the options of the name of the builder.
        if (!string.Equals(name, _optionsName, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Skip;
        }

        ArgumentNullException.ThrowIfNull(options);

        (CelProgram<bool>? program, CelError? error) = _rule.Value;
        if (program is not null)
        {
            CelEvalResult<bool> result = program.EvaluateInput(options, cancellationToken: CancellationToken.None);
            if (result.IsSuccess)
            {
                return result.Value ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(_failureMessage);
            }

            error = result.Errors[0];
        }

        return ValidateOptionsResult.Fail(string.Create(CultureInfo.InvariantCulture, $"{_failureMessage} ({error!.Code}: {error.Message})"));
    }

    private static (CelProgram<bool>? Program, CelError? Error) Compile(CelEnvironment environment, string expression)
    {
        CelBuildResult build = environment.ToBuilder().UseInput<TOptions>().Build();
        if (!build.IsSuccess)
        {
            return (null, build.Errors[0]);
        }

        CelCompileResult<bool> compiled = build.GetEnvironmentOrThrow().Compile<bool>(expression);
        return compiled.IsSuccess ? (compiled.GetProgramOrThrow(), null) : (null, compiled.Errors[0]);
    }
}
