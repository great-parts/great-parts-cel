using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Conformance;

/// <summary>
/// The database of the operating system on browser-wasm in the invariant time zone mode (<c>System.TimeZoneInfo.Invariant</c>, what <c>InvariantTimezone</c> sets) (<c>docs/design/time-zones-system.md</c>,
/// "Tests"). The runtime then has no named zone, so <see cref="CelTimeZoneDatabase.FromSystem"/> has to behave as a machine without zones: no
/// zone is found, a name is <see cref="CelErrorCode.TimeZoneNotFound"/> and never the offset of another zone, and <c>UTC</c> and a fixed
/// offset still give their hours. The check first asks the platform itself, so a build in which the mode has no effect is
/// reported as that, and not as a fault of the library.
/// </summary>
internal static class SystemZones
{
    private const string Zone = "Europe/Moscow";
    private const string Moment = "timestamp('2026-07-01T12:00:00Z')";

    /// <summary>Runs the check.</summary>
    /// <returns>The problems found, one line each; none when the database of the system behaves as expected.</returns>
    internal static string[] Problems()
    {
        List<string> problems = [];
        if (PlatformFindsTheZone())
        {
            problems.Add($"TimeZoneInfo finds {Zone}: the invariant time zone mode is not in effect in this build");
            return [.. problems];
        }

        CelEnvironment environment = new CelEnvironmentBuilder()
            .UseTimeZones(CelTimeZoneDatabase.FromSystem())
            .Build()
            .GetEnvironmentOrThrow();

        if (CelTimeZoneDatabase.FromSystem().Contains(Zone))
        {
            problems.Add($"FromSystem().Contains(\"{Zone}\") is true");
        }

        CelEvalResult<long> result = Evaluate(environment, $"{Moment}.getHours('{Zone}')");
        if (result.IsSuccess)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"getHours('{Zone}') is {result.Value}, not an error"));
        }
        else if (result.Errors.Length != 1 || result.Errors[0].Code != CelErrorCode.TimeZoneNotFound || result.Errors[0].Subject != Zone)
        {
            problems.Add($"getHours('{Zone}') is not the single error TimeZoneNotFound with the name: {result.Errors[0].Code} {result.Errors[0].Subject}");
        }

        ExpectHours(environment, "UTC", 12, problems);
        ExpectHours(environment, "+03:00", 15, problems);
        return [.. problems];
    }

    private static bool PlatformFindsTheZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(Zone) is not null;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
    }

    private static void ExpectHours(CelEnvironment environment, string zone, long expected, List<string> problems)
    {
        CelEvalResult<long> result = Evaluate(environment, $"{Moment}.getHours('{zone}')");
        if (!result.IsSuccess || result.Value != expected)
        {
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"getHours('{zone}') is not {expected}"));
        }
    }

    private static CelEvalResult<long> Evaluate(CelEnvironment environment, string text) =>
        environment.Compile<long>(text).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);
}
