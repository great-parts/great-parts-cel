using FluentValidation;
using FluentValidation.Results;
using Great.Parts.Cel;

/// <summary>
/// The method <c>Cel</c> for the rules of FluentValidation: <c>RuleFor(order => order.Total).Cel(environment, "value &gt; 0.0", "...")</c>.
/// Copy this file into your project. The libraries of Great.Parts.Cel do not depend on FluentValidation.
/// </summary>
public static class CelRuleBuilderExtensions
{
    /// <summary>
    /// Checks a property with a CEL expression of the type <c>bool</c>. The expression reads the value of the property as the variable
    /// <c>value</c>; the environment may declare other names, but only <c>value</c> is bound.
    /// </summary>
    /// <remarks>
    /// <c>true</c> passes. <c>false</c> is a <see cref="ValidationFailure"/> with <paramref name="message"/>. An error of the evaluation, such
    /// as a division by zero, is a failure with the same message, and its <see cref="ValidationFailure.ErrorCode"/> is the name of the
    /// <c>CelErrorCode</c>. A rule that does not compile is a <see cref="Great.Parts.Cel.Diagnostics.CelException"/> here, when the validator
    /// is created.
    /// </remarks>
    /// <param name="ruleBuilder">The rule builder of FluentValidation.</param>
    /// <param name="environment">The CEL environment of the application.</param>
    /// <param name="expression">The text of the rule.</param>
    /// <param name="message">The message of the failure.</param>
    public static IRuleBuilderOptions<T, TProperty> Cel<T, TProperty>(
        this IRuleBuilder<T, TProperty> ruleBuilder,
        CelEnvironment environment,
        string expression,
        string message)
    {
        ArgumentNullException.ThrowIfNull(ruleBuilder);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(message);

        // The rule is compiled once, here. The environment of the application gets one more variable, the value of the property.
        CelProgram<bool> program = environment.ToBuilder()
            .AddVariable<TProperty>("value")
            .Build()
            .GetEnvironmentOrThrow()
            .Compile<bool>(expression)
            .GetProgramOrThrow();

        return ruleBuilder.Must((_, value, context) =>
        {
            CelEvalResult<bool> result = program.Evaluate(new CelBindings().Add("value", value));
            if (result.IsSuccess && result.Value)
            {
                return true;
            }

            // The failure is added here, with the code of the error, so the predicate reports success: if it returned false,
            // FluentValidation would add a second failure with a message of its own.
            context.AddFailure(new ValidationFailure(context.PropertyPath, message, value)
            {
                ErrorCode = result.IsSuccess ? null : result.Errors[0].Code.ToString(),
            });
            return true;
        });
    }
}
