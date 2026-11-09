using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// Calls of the overloads of time on values, found in the table of the library by their identifiers, at the place of a
// call in a one-line expression. An error is read as its code, the offset of its place and its message.
internal static class TimeCalls
{
    internal const int Offset = 4;

    private static readonly LineTable Lines = new("0123456789");

    internal static CelValue Call(string overloadId, CelTimeZoneDatabase? database, params CelValue[] arguments)
    {
        OverloadBinding overload = Assert.Single(StandardLibraryV1.Overloads, candidate => candidate.Declaration.OverloadId == overloadId);
        return overload.Implementation!(arguments, new CallSite(overload.Function, Offset, Lines, timeZones: database));
    }

    internal static CelValue Call(string overloadId, params CelValue[] arguments) => Call(overloadId, null, arguments);

    internal static (CelErrorCode Code, int Offset, string Message) Error(CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error), value.Kind.ToString());
        return (error.Code, error.Location.Offset, error.Message);
    }

    internal static void AssertNoMatchingOverload(string overloadId, params CelValue[] arguments) =>
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Error(Call(overloadId, arguments)).Code);

    internal static CelValue Ts(long seconds, int nanoseconds = 0) => CelValue.FromTimestamp(new CelTimestamp(seconds, nanoseconds));

    internal static CelValue Du(long nanoseconds) => CelValue.FromDuration(new CelDuration(nanoseconds));

    internal static CelValue Int(long value) => CelValue.FromInt64(value);

    internal static CelValue Str(string value) => CelValue.FromString(value);

    internal static (long Seconds, int Nanoseconds) TimestampOf(CelValue value)
    {
        Assert.True(value.TryGetTimestamp(out CelTimestamp timestamp), value.Kind.ToString());
        return (timestamp.Seconds, timestamp.Nanoseconds);
    }

    internal static long DurationOf(CelValue value)
    {
        Assert.True(value.TryGetDuration(out CelDuration duration), value.Kind.ToString());
        return duration.TotalNanoseconds;
    }

    internal static string StringOf(CelValue value)
    {
        Assert.True(value.TryGetString(out string? text), value.Kind.ToString());
        return text;
    }

    internal static long IntOf(CelValue value)
    {
        Assert.True(value.TryGetInt64(out long number), value.Kind.ToString());
        return number;
    }

    // The identifiers of the overloads of one function, in the order of the table.
    internal static string[] Ids(string function) =>
        [.. StandardLibraryV1.Overloads.Where(overload => overload.Function == function).Select(overload => overload.Declaration.OverloadId)];
}
