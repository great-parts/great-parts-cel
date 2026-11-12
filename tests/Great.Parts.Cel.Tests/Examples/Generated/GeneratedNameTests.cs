using System;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Naming;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// The names of fields ("Names of fields" of the type mapping): the first rule that applies — [CelIgnore] or [CelName], then
// [JsonIgnore] with the condition Always or [JsonPropertyName] unless the context ignores the attributes of System.Text.Json, then
// the naming policy applied to the C# name. The sample class Names is described by four contexts that differ in the options, one CEL
// name each (samples.CamelNames and the like), and ForeignContext hides members of a type of another assembly by IgnoreMembers.
// The names that the policies give are those of JsonNamingPolicy.CamelCase and SnakeCaseLower. The fields of a description are in the
// order of their names, ordinal.
public sealed class GeneratedNameTests
{
    private static readonly string[] CamelFields =
    [
        "renamed", "fromCel", "wire_name", "conditional", "totalAmount", "urlPath", "httpServer", "order2Line", "id", "in", "as",
    ];

    private static readonly string[] AsIsFields =
    [
        "renamed", "fromCel", "wire_name", "Conditional", "TotalAmount", "URLPath", "HTTPServer", "Order2Line", "ID", "In", "As",
    ];

    private static readonly string[] SnakeFields =
    [
        "renamed", "fromCel", "wire_name", "conditional", "total_amount", "url_path", "http_server", "order2_line", "id", "in", "as",
    ];

    // IgnoreJsonAttributes: [JsonPropertyName] and [JsonIgnore] are not read, so Wire, Skipped and AlwaysSkipped are fields with their own
    // names under the policy; [CelName] and [CelIgnore] still apply.
    private static readonly string[] PlainFields =
    [
        "renamed", "fromCel", "wire", "skipped", "conditional", "alwaysSkipped", "totalAmount", "urlPath", "httpServer", "order2Line", "id", "in", "as",
    ];

    public static TheoryData<string> Contexts => new() { "camel", "asis", "snake", "plain" };

    [Theory]
    [Trait("Requirement", "F-2")]
    [MemberData(nameof(Contexts))]
    public void Context_GivesTheFieldsOfItsPolicyInTheOrderOfTheirNames(string context)
    {
        (CelTypeContext provider, string[] expected) = context switch
        {
            "camel" => ((CelTypeContext)CamelContext.Default, CamelFields),
            "asis" => (AsIsContext.Default, AsIsFields),
            "snake" => (SnakeContext.Default, SnakeFields),
            _ => (PlainContext.Default, PlainFields),
        };

        CelObjectType type = Assert.Single(provider.ObjectTypes);

        Assert.Equal(expected.Order(StringComparer.Ordinal), type.Fields.Select(field => field.Name));
        Assert.Equal(Enumerable.Range(0, expected.Length), type.Fields.Select(field => field.Number));
    }

    // The names are the ones an expression writes: a word reserved by CEL (in, as) is a field name that an expression writes in
    // backquotes; [CelName] wins over [JsonPropertyName] on one member; a member with [JsonIgnore] is not a field, and one whose condition
    // is not Always is.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void CamelCase_NamesAreReadByTheExpression()
    {
        CelEnvironment environment = Builder(CamelContext.Default).AddVariable<Names>("n").Build().GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings().Add("n", Sample());

        Assert.True(Evaluate<bool>(environment, "n.renamed == 1 && n.fromCel == 2 && n.wire_name == 3", bindings));
        Assert.True(Evaluate<bool>(environment, "n.totalAmount == 4 && n.urlPath == 5 && n.httpServer == 6 && n.order2Line == 7 && n.id == 8", bindings));
        Assert.True(Evaluate<bool>(environment, "n.`in` == 9 && n.`as` == 10 && !has(n.conditional)", bindings));
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "n.hidden").Code);
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "n.skipped").Code);
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "n.alwaysSkipped").Code);
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "n.original").Code);
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "n.both").Code);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void AsIsAndSnakeCase_NamesAreReadByTheExpression()
    {
        CelEnvironment asIs = Builder(AsIsContext.Default).AddVariable<Names>("n").Build().GetEnvironmentOrThrow();
        CelEnvironment snake = Builder(SnakeContext.Default).AddVariable<Names>("n").Build().GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings().Add("n", Sample());

        Assert.True(Evaluate<bool>(asIs, "n.TotalAmount == 4 && n.URLPath == 5 && n.HTTPServer == 6 && n.Order2Line == 7 && n.ID == 8 && n.In == 9 && n.As == 10", bindings));
        Assert.True(Evaluate<bool>(snake, "n.total_amount == 4 && n.url_path == 5 && n.http_server == 6 && n.order2_line == 7 && n.id == 8 && n.`in` == 9", bindings));
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(asIs, "n.totalAmount").Code);
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(snake, "n.totalAmount").Code);
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    public void IgnoreJsonAttributes_ReadsTheMembersTheirOwnNames()
    {
        CelEnvironment environment = Builder(PlainContext.Default).AddVariable<Names>("n").Build().GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings().Add("n", Sample());

        Assert.True(Evaluate<bool>(environment, "n.wire == 3 && n.skipped == 11 && n.alwaysSkipped == 12 && n.fromCel == 2", bindings));
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "n.wire_name").Code);
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "n.hidden").Code);
    }

    // IgnoreMembers of a root hides the members of that root type by their C# names, also for a type that cannot be marked: of
    // Google.Protobuf.ByteString the members Span (a ref struct, which is not a member anyway) and IsEmpty are hidden, Length and Memory are read.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void IgnoreMembersOfAForeignType_HidesThoseMembers()
    {
        CelEnvironment environment = Builder(ForeignContext.Default).AddVariable<Google.Protobuf.ByteString>("b").Build().GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings().Add("b", Google.Protobuf.ByteString.CopyFrom(1, 2));

        Assert.Equal(["length", "memory"], Assert.Single(ForeignContext.Default.ObjectTypes).Fields.Select(field => field.Name));
        Assert.True(Evaluate<bool>(environment, "b.length == 2 && b.memory == b'\\x01\\x02'", bindings));
        Assert.Equal(CelErrorCode.NoSuchField, CompileError(environment, "b.isEmpty").Code);
    }

    // Two contexts that describe one .NET type with other options — another policy — give two descriptions of one type that differ,
    // so the environment is DuplicateType, and the same in either order of the calls: the same type would answer to two names.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "+R-8")]
    public void ContextsWithOtherOptions_AreDuplicateTypeInEveryOrder()
    {
        CelBuildResult camelFirst = Builder(CamelContext.Default, SnakeContext.Default).Build();
        CelBuildResult snakeFirst = Builder(SnakeContext.Default, CamelContext.Default).Build();

        Assert.False(camelFirst.IsSuccess);
        Assert.Equal(Codes(camelFirst), Codes(snakeFirst));
        Assert.All(camelFirst.Errors, error => Assert.Equal(CelErrorCode.DuplicateType, error.Code));
        Assert.Contains(camelFirst.Errors, error => error.Message.Contains("CamelContext", StringComparison.Ordinal) && error.Message.Contains("SnakeContext", StringComparison.Ordinal));
    }

    private static Names Sample() => new()
    {
        Original = 1,
        Hidden = 99,
        Both = 2,
        Wire = 3,
        Skipped = 11,
        AlwaysSkipped = 12,
        TotalAmount = 4,
        URLPath = 5,
        HTTPServer = 6,
        Order2Line = 7,
        ID = 8,
        In = 9,
        As = 10,
    };
}
