using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Great.Parts.Cel.DependencyInjection;

/// <summary>
/// Validates the options of an application with CEL expressions: <c>services.AddOptions&lt;ShopOptions&gt;().ValidateCel("maxDiscount &lt;= 50", "The discount is above 50%")</c>.
/// </summary>
/// <remarks>
/// <para>
/// The fields of the options type are the variables of the expression: the environment registered with
/// <see cref="CelServiceCollectionExtensions.AddCel"/>, or with <see cref="CelServiceCollectionExtensions.AddKeyedCel"/> under
/// the given name, is extended with the options type as its input type, as
/// <c>environment.ToBuilder().UseInput&lt;TOptions&gt;()</c> does, so a type source of that environment must describe the
/// options type. The derived environment and the compiled expression are created once per options type and expression.
/// </para>
/// <para>
/// The check is an <c>IValidateOptions&lt;TOptions&gt;</c>: it runs when the options are read, or at startup with
/// <c>ValidateOnStart()</c>. An expression that gives <see langword="false"/> fails the validation with the message; an
/// expression that does not compile or whose evaluation fails also fails it, with the message, the code of the error and
/// its text. The check works without a web host, so it serves services and browser applications as well.
/// </para>
/// </remarks>
public static class CelOptionsBuilderExtensions
{
    /// <summary>
    /// Adds a validation of the options by a CEL expression over their fields, which must give <see langword="true"/>.
    /// </summary>
    /// <typeparam name="TOptions">The options type, described by a type source of the CEL environment.</typeparam>
    /// <param name="builder">The options builder.</param>
    /// <param name="expression">The expression of the type <c>bool</c> over the fields of <typeparamref name="TOptions"/>.</param>
    /// <param name="failureMessage">The message of the validation failure.</param>
    /// <param name="environmentName">
    /// The name of the environment registered with <see cref="CelServiceCollectionExtensions.AddKeyedCel"/>, or
    /// <see langword="null"/> for the one registered with <see cref="CelServiceCollectionExtensions.AddCel"/>.
    /// </param>
    /// <returns><paramref name="builder"/>, for chained calls.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="expression"/> or <paramref name="failureMessage"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="environmentName"/> is empty.</exception>
    public static OptionsBuilder<TOptions> ValidateCel<TOptions>(
        this OptionsBuilder<TOptions> builder,
        string expression,
        string failureMessage,
        string? environmentName = null)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(failureMessage);
        if (environmentName is { Length: 0 })
        {
            throw new ArgumentException("The name of the environment is empty; pass null for the environment registered with AddCel.", nameof(environmentName));
        }

        string optionsName = builder.Name;
        builder.Services.AddSingleton<IValidateOptions<TOptions>>(provider => new CelOptionsValidation<TOptions>(
            optionsName,
            environmentName is null ? provider.GetRequiredService<CelEnvironment>() : provider.GetRequiredKeyedService<CelEnvironment>(environmentName),
            expression,
            failureMessage));
        return builder;
    }
}
