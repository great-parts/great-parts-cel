using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The declarations of time. A time zone database is added with UseTimeZones and becomes part of the
// environment: a different database added to an environment that already has one is the build error
// TimeZoneDatabaseConflict (never a silent replacement), in either order, also through ToBuilder; the same
// database object added again changes nothing; Empty is a database like any other. The build errors have the
// stage Build and no place. The name google.protobuf.Timestamp is a type identifier of the standard
// library, as google.protobuf.Duration is, so a variable of that name is StandardLibraryConflict. The typed
// implementations of application functions are checked against the declared overload when the environment
// is built: the .NET type of a parameter must map to the declared CEL type and be one that an argument
// can fill — DateTimeOffset and CelTimestamp for timestamp, TimeSpan and CelDuration for duration; a
// DateTime, which an input row reads as a timestamp but no result row gives, is ImplementationSignatureMismatch.
public sealed class TimeDeclarationTests
{
    private readonly CelStandardLibrary library = CelStandardLibrary.V1;

    [Fact]
    [Trait("Requirement", "+R-4")]
    [Trait("Requirement", "+R-8")]
    public void ADifferentDatabase_IsTimeZoneDatabaseConflict()
    {
        CelTimeZoneDatabase first = TimeZoneData.Database();
        CelTimeZoneDatabase second = CelTimeZoneDatabase.Create([TimeZoneData.Pair("Test/Fixed", TimeZoneData.Fixed)], "2026b");

        AssertConflict(SingleError(Builder().UseTimeZones(first).UseTimeZones(second)));
        AssertConflict(SingleError(Builder().UseTimeZones(second).UseTimeZones(first)));
        AssertConflict(SingleError(Builder().UseTimeZones(CelTimeZoneDatabase.Empty).UseTimeZones(first)));
        AssertConflict(SingleError(Builder().UseTimeZones(first).UseTimeZones(CelTimeZoneDatabase.Empty)));
    }

    [Fact]
    [Trait("Requirement", "+R-4")]
    [Trait("Requirement", "+R-7")]
    public void ADifferentDatabaseThroughToBuilder_IsTimeZoneDatabaseConflict()
    {
        CelTimeZoneDatabase first = TimeZoneData.Database();
        CelTimeZoneDatabase second = CelTimeZoneDatabase.Create([TimeZoneData.Pair("Test/Fixed", TimeZoneData.Fixed)]);
        CelEnvironment environment = Builder().UseTimeZones(first).Build().GetEnvironmentOrThrow();

        AssertConflict(SingleError(environment.ToBuilder().UseTimeZones(second)));
        Assert.True(environment.ToBuilder().UseTimeZones(first).Build().IsSuccess);
        Assert.True(environment.ToBuilder().Build().IsSuccess);
    }

    [Fact]
    [Trait("Requirement", "+R-4")]
    public void TheSameDatabaseAgain_ChangesNothing()
    {
        CelTimeZoneDatabase database = TimeZoneData.Database();

        Assert.True(Builder().UseTimeZones(database).UseTimeZones(database).Build().IsSuccess);
        Assert.True(Builder().UseTimeZones(CelTimeZoneDatabase.Empty).UseTimeZones(CelTimeZoneDatabase.Empty).Build().IsSuccess);
    }

    [Fact]
    [Trait("Requirement", "+R-4")]
    public void ANullDatabase_IsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Builder().UseTimeZones(null!));
    }

    [Fact]
    [Trait("Requirement", "+R-8")]
    [Trait("Requirement", "F-3")]
    public void VariableNamedAsATypeOfTime_IsStandardLibraryConflict()
    {
        const string name = "google.protobuf.Timestamp";
        CelError error = SingleError(Builder().AddVariable(name, CelType.Dyn));

        Assert.Equal((CelErrorCode.StandardLibraryConflict, CelErrorStage.Build), (error.Code, error.Stage));
        Assert.Equal("variable '" + name + "' of application conflicts with standard library v1: it is a type identifier", error.Message);
        Assert.Equal(default, error.Location);
    }

    [Theory]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    [InlineData("timestamp", "TimeSpan")]
    [InlineData("timestamp", "CelDuration")]
    [InlineData("timestamp", "DateTime")]
    [InlineData("duration", "DateTimeOffset")]
    [InlineData("duration", "CelTimestamp")]
    [InlineData("duration", "DateTime")]
    public void TypedImplementationOfAnotherTimeType_IsImplementationSignatureMismatch(string declared, string implemented)
    {
        CelEnvironmentBuilder builder = Builder().AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_time", [Declared(declared)], CelType.Int)));
        builder = implemented switch
        {
            "TimeSpan" => builder.AddFunctionImplementation<TimeSpan, long>("f_time", span => span.Ticks),
            "CelDuration" => builder.AddFunctionImplementation<CelDuration, long>("f_time", span => span.TotalNanoseconds),
            "DateTimeOffset" => builder.AddFunctionImplementation<DateTimeOffset, long>("f_time", moment => moment.Ticks),
            "CelTimestamp" => builder.AddFunctionImplementation<CelTimestamp, long>("f_time", moment => moment.Seconds),
            _ => builder.AddFunctionImplementation<DateTime, long>("f_time", moment => moment.Ticks),
        };

        CelError error = SingleError(builder);

        Assert.Equal((CelErrorCode.ImplementationSignatureMismatch, CelErrorStage.Build), (error.Code, error.Stage));
        Assert.StartsWith("implementation 'f_time' does not match the signature", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Requirement", "F-3")]
    [InlineData("timestamp", "DateTimeOffset")]
    [InlineData("timestamp", "CelTimestamp")]
    [InlineData("duration", "TimeSpan")]
    [InlineData("duration", "CelDuration")]
    public void TypedImplementationOfTheDeclaredTimeType_Builds(string declared, string implemented)
    {
        CelEnvironmentBuilder builder = Builder().AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_time", [Declared(declared)], CelType.Int)));
        builder = implemented switch
        {
            "TimeSpan" => builder.AddFunctionImplementation<TimeSpan, long>("f_time", span => span.Ticks),
            "CelDuration" => builder.AddFunctionImplementation<CelDuration, long>("f_time", span => span.TotalNanoseconds),
            "DateTimeOffset" => builder.AddFunctionImplementation<DateTimeOffset, long>("f_time", moment => moment.Ticks),
            _ => builder.AddFunctionImplementation<CelTimestamp, long>("f_time", moment => moment.Seconds),
        };

        Assert.True(builder.Build().IsSuccess);
    }

    // The types that a delegate of AddFunction takes and gives are those of the rows: the overload that it
    // derives has the CEL types timestamp and duration.
    [Fact]
    [Trait("Requirement", "F-3")]
    public void AddFunctionOfTimeTypes_DerivesOverloadsOfTimeTypes()
    {
        CelEnvironment environment = Builder()
            .AddFunction<DateTimeOffset, TimeSpan>("shift", moment => TimeSpan.FromTicks(moment.Ticks % 10))
            .AddFunction<CelDuration, CelTimestamp>("back", _ => new CelTimestamp(0, 0))
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(CelType.Duration, environment.Compile<CelValue>("shift(timestamp(0))").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.Timestamp, environment.Compile<CelValue>("back(duration('1s'))").GetProgramOrThrow().ResultType);
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(environment.Compile<CelValue>("shift(duration('1s'))").Errors).Code);
        Assert.Equal(CelErrorCode.NoMatchingOverload, Assert.Single(environment.Compile<CelValue>("back(timestamp(0))").Errors).Code);
    }

    private CelEnvironmentBuilder Builder() => new(library);

    private static CelType Declared(string name) => name == "timestamp" ? CelType.Timestamp : CelType.Duration;

    private static CelError SingleError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();

        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    private static void AssertConflict(CelError error)
    {
        Assert.Equal(
            (CelErrorCode.TimeZoneDatabaseConflict, CelErrorStage.Build, "a different time zone database is already added to the environment"),
            (error.Code, error.Stage, error.Message));
        Assert.Equal(default, error.Location);
        Assert.Null(error.SourceLocation);
    }
}
