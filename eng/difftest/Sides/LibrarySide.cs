using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Configuration;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// The library side of a run (docs/design/eng-difftest.md, "A run", steps 2 and 4), through the public API only: the
/// environment of the run manifest with the time zone database of the data file, <c>Compile&lt;CelValue&gt;</c> and
/// <c>Evaluate</c>. An exception from the library counts as an error of the step it escaped from, and the message names its type.
/// </summary>
internal sealed class LibrarySide : ICaseSide
{
    // The offset of a zone at an instant, through the public API: the six fields of the local time, which the calendar of the
    // library gives for the instant s in the zone z (docs/design/eng-difftest.md, "Findings and output").
    private const string OffsetExpression =
        "[timestamp(s).getFullYear(z), timestamp(s).getMonth(z), timestamp(s).getDate(z), timestamp(s).getHours(z), timestamp(s).getMinutes(z), timestamp(s).getSeconds(z)]";

    private readonly CelEnvironment environment;
    private readonly CelProgram<CelValue> offsets;

    private LibrarySide(CelEnvironment environment, CelProgram<CelValue> offsets)
    {
        this.environment = environment;
        this.offsets = offsets;
    }

    /// <summary>
    /// Step 2 of a run: <c>new CelEnvironmentBuilder(CelStandardLibrary.V1)</c>, <c>CelEnvironmentJson.Apply</c> with the
    /// manifest, <c>AddTypes</c> with a provider of the known types of protobuf, <c>UseTimeZones</c> with the database made from
    /// the time zone data file — every zone of the archive, the version of <see cref="ProfileTables.ZoneInfoVersion"/> — and <c>Build</c>. An exception of the library escapes to the caller, which
    /// ends the run.
    /// </summary>
    /// <param name="manifest">The run manifest, UTF-8 JSON.</param>
    /// <param name="zoneInfoFile">The path of the time zone data file: a zip archive with one entry per zone.</param>
    /// <param name="side">The library side, or <see langword="null"/> when the manifest gives no environment.</param>
    /// <param name="problem">Why the library gives no environment, one line; empty when it gives one.</param>
    /// <returns><see langword="true"/> when the library built the environment.</returns>
    internal static bool TryBuild(ReadOnlySpan<byte> manifest, string zoneInfoFile, out LibrarySide? side, out string problem)
    {
        side = null;
        if (!TryCreateDatabase(zoneInfoFile, out CelTimeZoneDatabase? database, out problem))
        {
            return false;
        }

        CelEnvironmentBuilder builder = new(CelStandardLibrary.V1);
        ImmutableArray<CelError> applied = CelEnvironmentJson.Apply(builder, manifest);
        if (!applied.IsDefaultOrEmpty)
        {
            problem = "the library does not apply the run manifest: " + Messages(applied);
            return false;
        }

        // The known types of protobuf, always: a manifest that names the type any resolves on the side of the oracle by default,
        // and on this side only with a provider (docs/design/eng-difftest.md, "Provider of protobuf on the library side").
        builder.AddTypes(new CelProtobufTypesBuilder("difftest").Build());

        CelBuildResult built = builder.UseTimeZones(database!).Build();
        if (!built.IsSuccess || built.Environment is null)
        {
            problem = "the library does not build the environment of the run manifest: " + Messages(built.Errors);
            return false;
        }

        // The environment of the offsets has the same database object, so both environments read the same zones.
        CelBuildResult offsetBuild = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("s", CelType.Int)
            .AddVariable("z", CelType.String)
            .UseTimeZones(database!)
            .Build();
        if (!offsetBuild.IsSuccess || offsetBuild.Environment is null)
        {
            problem = "the library does not build the environment of the zone offsets: " + Messages(offsetBuild.Errors);
            return false;
        }

        CelCompileResult<CelValue> compiled = offsetBuild.Environment.Compile<CelValue>(OffsetExpression);
        if (!compiled.IsSuccess || compiled.Program is null)
        {
            problem = "the library does not compile the expression of the zone offsets: " + Messages(compiled.Errors);
            return false;
        }

        side = new LibrarySide(built.Environment, compiled.Program);
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

    /// <summary>
    /// The offset of a zone at an instant through the public API of the library: one expression, compiled once, gives the year,
    /// the month, the day, the hour, the minute and the second of the instant in the zone. The local time is built from them
    /// with <see cref="DateTime"/>, a calendar that is not the library's, and the offset is its seconds minus the instant, so a
    /// fault of the calendar of the library shows as a divergence of the offsets too.
    /// </summary>
    /// <param name="zone">The name of the zone in the time zone data file.</param>
    /// <param name="instant">The instant, in whole seconds.</param>
    /// <returns>The offset from UTC.</returns>
    /// <exception cref="SideFailureException">The library gives no fields for the zone and the instant.</exception>
    public TimeSpan ZoneOffset(string zone, DateTimeOffset instant)
    {
        long seconds = instant.ToUnixTimeSeconds();
        CelBindings values = new();
        values.Add("s", CelValue.FromInt64(seconds));
        values.Add("z", CelValue.FromString(zone));
        CelEvalResult<CelValue> result;
        try
        {
            result = offsets.Evaluate(values, null, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw Failure(zone, instant, "the library threw " + Describe(exception));
        }

        if (!result.IsSuccess)
        {
            throw Failure(zone, instant, Messages(result.Errors));
        }

        long[] fields = new long[6];
        if (!result.Value.TryGetList(out CelList? list) || list.Count != fields.Length)
        {
            throw Failure(zone, instant, "the expression of the offsets gave no list of six fields");
        }

        for (int i = 0; i < fields.Length; i++)
        {
            if (!list[i].TryGetInt64(out fields[i]))
            {
                throw Failure(zone, instant, "a field of the local time is not an int");
            }
        }

        // The month of the library counts from 0, its day of the month from 1.
        DateTime local = new((int)fields[0], (int)fields[1] + 1, (int)fields[2], (int)fields[3], (int)fields[4], (int)fields[5], DateTimeKind.Unspecified);
        return local - DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
    }

    private static bool TryCreateDatabase(string zoneInfoFile, out CelTimeZoneDatabase? database, out string problem)
    {
        database = null;
        IReadOnlyList<KeyValuePair<string, byte[]>> entries = ZoneData.Entries(zoneInfoFile);
        try
        {
            database = CelTimeZoneDatabase.Create(
                entries.Select(entry => new KeyValuePair<string, ReadOnlyMemory<byte>>(entry.Key, entry.Value)),
                ProfileTables.ZoneInfoVersion);
        }
        catch (ArgumentException exception)
        {
            // The message of the library names the zone that it refuses.
            problem = "the library refuses the time zone data file: " + Describe(exception);
            return false;
        }

        problem = string.Empty;
        return true;
    }

    private static SideFailureException Failure(string zone, DateTimeOffset instant, string reason) =>
        new(string.Create(
            CultureInfo.InvariantCulture,
            $"the library gives no local time for zone '{zone}' at {instant.UtcDateTime:yyyy'-'MM'-'dd'T'HH':'mm':'ss'Z'}: {reason}"));

    private static string Messages(ImmutableArray<CelError> errors) =>
        errors.IsDefaultOrEmpty ? "no error message" : string.Join("; ", errors.Select(error => error.Message));
}
