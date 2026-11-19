using System;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace Great.Parts.Cel.DependencyInjection;

// The check of CelOptions of every name: a capacity of the compilation cache is 1 or more. The options system runs it when it creates
// the options, so a wrong value is an OptionsValidationException of IOptions<CelOptions>.Value, before the cache is created.
internal sealed class CelOptionsCheck : IValidateOptions<CelOptions>
{
    public ValidateOptionsResult Validate(string? name, CelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.CacheMaxEntryCount < 1)
        {
            return ValidateOptionsResult.Fail(string.Create(
                CultureInfo.InvariantCulture,
                $"CelOptions.CacheMaxEntryCount is {options.CacheMaxEntryCount}; a capacity of the compilation cache is 1 or more."));
        }

        if (options.CacheMaxTotalTextLength < 1)
        {
            return ValidateOptionsResult.Fail(string.Create(
                CultureInfo.InvariantCulture,
                $"CelOptions.CacheMaxTotalTextLength is {options.CacheMaxTotalTextLength}; a capacity of the compilation cache is 1 or more."));
        }

        return ValidateOptionsResult.Success;
    }
}
