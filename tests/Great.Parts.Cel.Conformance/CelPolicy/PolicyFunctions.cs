using System;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Conformance.CelPolicy;

/// <summary>
/// The functions a category declares in <c>config.yaml</c> and the test harness supplies, as the harness of cel-go does for its tests of
/// policies: the implementation of each overload by its identifier. <c>locationCode_string</c> is <c>locationCode</c> of the harness of
/// the policy package of cel-go; <c>hasCreditCard</c> and <c>hasEmailOrPhone</c> are those of its test of the composed expression.
/// </summary>
internal static class PolicyFunctions
{
    /// <summary>Registers the implementation of an overload.</summary>
    /// <param name="builder">The builder of the environment.</param>
    /// <param name="overloadId">The identifier of the overload, as <c>config.yaml</c> declares it.</param>
    /// <exception cref="NotImplementedException">The runner has no implementation of the overload.</exception>
    internal static void Register(CelEnvironmentBuilder builder, string overloadId)
    {
        ArgumentNullException.ThrowIfNull(builder);
        switch (overloadId)
        {
            case "locationCode_string":
                builder.AddFunctionImplementation<string, string>(overloadId, static address => LocationCode(address));
                break;
            case "hasCreditCard":
                builder.AddFunctionImplementation<CelValue, bool>(overloadId, static arguments => HasKey(arguments, "cc"));
                break;
            case "hasEmailOrPhone":
                builder.AddFunctionImplementation<CelValue, bool>(overloadId, static arguments => HasKey(arguments, "email") || HasKey(arguments, "phone"));
                break;
            default:
                throw new NotImplementedException($"the runner has no implementation of the overload '{overloadId}'");
        }
    }

    private static string LocationCode(string address) => address switch
    {
        "10.0.0.1" => "us",
        "10.0.0.2" => "de",
        _ => "ir",
    };

    private static bool HasKey(CelValue value, string key) =>
        value.TryGetMap(out CelMap? map) && map.TryGetValue(CelValue.FromString(key), out _);
}
