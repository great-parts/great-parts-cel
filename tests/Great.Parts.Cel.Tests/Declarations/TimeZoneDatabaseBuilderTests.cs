using System;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// UseTimeZones: the first database stays, the same object again is nothing, a different object is the build error 4012 in
// either order and through ToBuilder, and the environment keeps its database.
public sealed class TimeZoneDatabaseBuilderTests
{
    private const string ConflictMessage = "a different time zone database is already added to the environment";

    private static readonly CelTimeZoneDatabase First = Database("Test/First", 3_600);
    private static readonly CelTimeZoneDatabase Second = Database("Test/Second", 7_200);

    [Fact]
    public static void NullDatabase_IsArgumentNullException()
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);

        Assert.Throws<ArgumentNullException>(() => builder.UseTimeZones(null!));
    }

    [Fact]
    public static void UseTimeZones_ReturnsTheBuilder()
    {
        var builder = new CelEnvironmentBuilder(CelStandardLibrary.V1);

        Assert.Same(builder, builder.UseTimeZones(First));
    }

    [Fact]
    public static void TheSameDatabaseObjectTwice_IsNoConflict()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(First).UseTimeZones(First).Build();

        Assert.True(result.IsSuccess);
        Assert.Equal("2", Hours(result.GetEnvironmentOrThrow(), "Test/First"));
    }

    // Two databases made from equal bytes are two objects: adding both is the conflict.
    [Fact]
    public static void TwoDatabasesFromEqualBytes_AreTwoObjects()
    {
        CelTimeZoneDatabase same = Database("Test/First", 3_600);

        AssertConflict(new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(First).UseTimeZones(same).Build());
    }

    [Fact]
    public static void DifferentDatabases_AreOneBuildError_InEitherOrder()
    {
        CelBuildResult forward = new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(First).UseTimeZones(Second).Build();
        CelBuildResult backward = new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(Second).UseTimeZones(First).Build();

        AssertConflict(forward);
        AssertConflict(backward);
        Assert.Equal(forward.Errors[0].Message, backward.Errors[0].Message);
    }

    // However many different objects are added, the build reports one error.
    [Fact]
    public static void ManyDifferentDatabases_AreStillOneBuildError()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .UseTimeZones(First)
            .UseTimeZones(Second)
            .UseTimeZones(Database("Test/Third", 0))
            .UseTimeZones(CelTimeZoneDatabase.Empty)
            .Build();

        AssertConflict(result);
    }

    [Fact]
    public static void EmptyDatabase_IsADatabaseLikeAnyOther()
    {
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(CelTimeZoneDatabase.Empty).UseTimeZones(CelTimeZoneDatabase.Empty).Build().IsSuccess);
        AssertConflict(new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(CelTimeZoneDatabase.Empty).UseTimeZones(First).Build());
        AssertConflict(new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(First).UseTimeZones(CelTimeZoneDatabase.Empty).Build());
    }

    // The environment keeps the database, which ToBuilder returns again; a different one added through ToBuilder is the conflict.
    [Fact]
    public static void ToBuilder_KeepsTheDatabase()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(First).Build().GetEnvironmentOrThrow();

        CelEnvironment copy = environment.ToBuilder().Build().GetEnvironmentOrThrow();
        CelEnvironment same = environment.ToBuilder().UseTimeZones(First).Build().GetEnvironmentOrThrow();

        Assert.Equal("2", Hours(copy, "Test/First"));
        Assert.Equal("2", Hours(same, "Test/First"));
        AssertConflict(environment.ToBuilder().UseTimeZones(Second).Build());
        Assert.Equal("2", Hours(environment, "Test/First"));
    }

    // An environment with no database takes one through ToBuilder, and the first environment stays without.
    [Fact]
    public static void EnvironmentWithoutADatabase_TakesOneThroughToBuilder()
    {
        CelEnvironment without = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        CelEnvironment with = without.ToBuilder().UseTimeZones(First).Build().GetEnvironmentOrThrow();

        Assert.Equal("2", Hours(with, "Test/First"));
        CelCompileResult<CelValue> compiled = without.Compile<CelValue>("timestamp(3600).getHours('Test/First')");
        CelError error = Assert.Single(compiled.GetProgramOrThrow().Evaluate(new CelBindings(), null, TestContext.Current.CancellationToken).Errors);
        Assert.Equal(CelErrorCode.TimeZoneNotFound, error.Code);
    }

    // The conflict is one build error among the others, ordered by code.
    [Fact]
    public static void Conflict_IsReportedWithTheOtherBuildErrors()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .UseTimeZones(First)
            .UseTimeZones(Second)
            .AddVariable("google.protobuf.Timestamp", CelType.Int)
            .Build();

        Assert.Equal([CelErrorCode.StandardLibraryConflict, CelErrorCode.TimeZoneDatabaseConflict], [.. result.Errors.Select(error => error.Code)]);
    }

    // The builder is not changed by a build: the same builder gives the same result again.
    [Fact]
    public static void Build_DoesNotChangeTheBuilder()
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1).UseTimeZones(First).UseTimeZones(Second);

        AssertConflict(builder.Build());
        AssertConflict(builder.Build());
    }

    private static void AssertConflict(CelBuildResult result)
    {
        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors, candidate => candidate.Code == CelErrorCode.TimeZoneDatabaseConflict);
        Assert.Equal((CelErrorStage.Build, ConflictMessage), (error.Stage, error.Message));
    }

    // The hour of 1970-01-01T01:00:00Z in the zone: 2 in a zone one hour east of UTC.
    private static string Hours(CelEnvironment environment, string zone)
    {
        CelValue value = environment.Evaluate<CelValue>("timestamp(3600).getHours('" + zone + "')", new CelBindings()).GetValueOrThrow();
        Assert.True(value.TryGetInt64(out long hours), zone);
        return hours.ToString(CultureInfo.InvariantCulture);
    }

    private static CelTimeZoneDatabase Database(string id, int offset) =>
        CelTimeZoneDatabase.Create([TzifFile.Pair(id, TzifFile.Fixed(offset, string.Empty))], "2026a");
}
