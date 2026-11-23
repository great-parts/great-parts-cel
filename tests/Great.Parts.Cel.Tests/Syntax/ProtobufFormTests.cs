using System.Linq;
using System.Threading;
using Google.Protobuf;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using GCheckedExpr = global::Cel.Expr.CheckedExpr;
using GConstant = global::Cel.Expr.Constant;
using GExpr = global::Cel.Expr.Expr;
using GParsedExpr = global::Cel.Expr.ParsedExpr;
using GSourceInfo = global::Cel.Expr.SourceInfo;
using GType = global::Cel.Expr.Type;

namespace Great.Parts.Cel.Tests.Syntax;

// What the examples of the protobuf form leave out: the macros of a namespace as cel-go records them, the repeated fields of a message
// read packed and unpacked, a negative int64 in a constant, the fields of the source info that the library does not use, and the
// names of a checked expression that type checking rewrites, the names that the container resolves again when the checked expression
// is loaded, and the kinds of Type that are written and read.
[Trait("Requirement", "F-20")]
[Trait("Requirement", "R-10")]
public sealed class ProtobufFormTests
{
    private static readonly CelEnvironment EnvMath = TreeEnvironments.EnvMath;

    // x of int, pkg.v of map(string, int) and the global function f of int.
    private static readonly CelEnvironment EnvNames = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .AddVariable("pkg.v", CelType.MapOf(CelType.String, CelType.Int))
        .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int)))
        .AddFunctionImplementation("f_int", _ => CelValue.FromInt64(1))
        .Build()
        .GetEnvironmentOrThrow();

    // The container acme with the variables acme.limit and acme.acme.limit.
    private static readonly CelEnvironment EnvAcme = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseContainer("acme")
        .AddVariable<long>("acme.limit")
        .AddVariable<long>("acme.acme.limit")
        .Build()
        .GetEnvironmentOrThrow();

    // The container com.example with the variables y and com.example.y.
    private static readonly CelEnvironment EnvExample = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .UseContainer("com.example")
        .AddVariable<long>("y")
        .AddVariable<long>("com.example.y")
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("cel.bind(t, 1, t + 1)", "cel", "bind")]
    [InlineData("math.greatest(1, 2)", "math", "greatest")]
    [InlineData("math.least(1, 2, 3)", "math", "least")]
    public static void MacroOfANamespace_LoadsTheSameFromTheFormOfCelGoAndFromTheFormOfTheLibrary(string text, string @namespace, string name)
    {
        CelParsedExpr parsed = EnvMath.Parse(text).GetParsedExprOrThrow();
        GParsedExpr message = GParsedExpr.Parser.ParseFrom(parsed.ToProtobuf());
        byte[] written = message.ToByteArray();
        GExpr recorded = Assert.Single(message.SourceInfo.MacroCalls).Value;
        Assert.Equal(@namespace + "." + name, recorded.CallExpr.Function);
        Assert.Null(recorded.CallExpr.Target);

        // cel-go records the call with the identifier of the namespace as the target.
        recorded.CallExpr.Function = name;
        recorded.CallExpr.Target = new GExpr { Id = parsed.IdCount + 10L, IdentExpr = new GExpr.Types.Ident { Name = @namespace } };
        byte[] celGo = message.ToByteArray();

        CelParsedExpr fromLibrary = CelParsedExpr.FromProtobuf(written, text).GetParsedExprOrThrow();
        CelParsedExpr fromCelGo = CelParsedExpr.FromProtobuf(celGo, text).GetParsedExprOrThrow();

        CelMacroCall expected = fromLibrary.GetMacroCall(fromLibrary.Root.Id)!;
        CelMacroCall actual = fromCelGo.GetMacroCall(fromCelGo.Root.Id)!;
        Assert.Equal((@namespace + "." + name, true), (expected.Function, expected.Target is null));
        Assert.Equal((expected.Function, expected.Target is null), (actual.Function, actual.Target is null));
        Assert.Equal(expected.Arguments.Length, actual.Arguments.Length);
        for (int i = 0; i < expected.Arguments.Length; i++)
        {
            Assert.Null(SyntaxTreeComparer.Difference(expected.Arguments[i], actual.Arguments[i]));
        }

        Assert.Equal(CelSyntax.Format(fromLibrary), CelSyntax.Format(fromCelGo));
        Assert.Equal(text, CelSyntax.Format(fromCelGo));
    }

    [Fact]
    public static void RepeatedFieldsOfTheMessage_AreReadPackedAndUnpacked()
    {
        byte[] packed = ListMessage(indices: [0x12, 0x02, 0x01, 0x00], lineOffsets: [0x1A, 0x02, 0x04, 0x07]);
        byte[] unpacked = ListMessage(indices: [0x10, 0x01, 0x10, 0x00], lineOffsets: [0x18, 0x04, 0x18, 0x07]);

        (string Indices, int Line, int Offset) fromPacked = ListFacts(packed);
        (string Indices, int Line, int Offset) fromUnpacked = ListFacts(unpacked);

        Assert.Equal(("0,1", 2, 5), fromPacked);
        Assert.Equal(fromPacked, fromUnpacked);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(-5L)]
    [InlineData(long.MinValue)]
    public static void NegativeInt64_IsReadFromTheTenBytesOfAVarintAndWrittenBack(long value)
    {
        byte[] bytes = new GParsedExpr { Expr = new GExpr { Id = 1, ConstExpr = new GConstant { Int64Value = value } } }.ToByteArray();

        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(bytes).GetParsedExprOrThrow();

        CelConstantNode constant = Assert.IsType<CelConstantNode>(loaded.Root);
        Assert.True(constant.Value.TryGetInt64(out long read));
        Assert.Equal(value, read);
        Assert.Equal(value, GParsedExpr.Parser.ParseFrom(loaded.ToProtobuf()).Expr.ConstExpr.Int64Value);
        var factory = new CelExprFactory();
        byte[] written = factory.Build(factory.Constant(CelValue.FromInt64(value))).ToProtobuf();
        Assert.Equal(value, GParsedExpr.Parser.ParseFrom(written).Expr.ConstExpr.Int64Value);
    }

    [Fact]
    public static void SyntaxVersionAndExtensionsOfTheSourceInfo_AreReadAndNotWritten()
    {
        var message = new GParsedExpr
        {
            Expr = new GExpr { Id = 1, IdentExpr = new GExpr.Types.Ident { Name = "x" } },
            SourceInfo = new GSourceInfo
            {
                SyntaxVersion = "cel1",
                Location = "rules.json",
                Extensions = { new GSourceInfo.Types.Extension { Id = "comments" } },
            },
        };

        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(message.ToByteArray()).GetParsedExprOrThrow();

        Assert.Equal("x", Assert.IsType<CelIdentNode>(loaded.Root).Name);
        Assert.Equal("rules.json", loaded.SourceName);
        GSourceInfo again = GParsedExpr.Parser.ParseFrom(loaded.ToProtobuf()).SourceInfo;
        Assert.Empty(again.Extensions);
        Assert.Equal(string.Empty, again.SyntaxVersion);
    }

    [Fact]
    public static void TypeIdentifierOfAChecking_IsAnIdentifierWithItsNameInTheReferenceAndTheTypeOfTypes()
    {
        const string text = "type(x) == int";
        byte[] bytes = EnvNames.Validate<bool>(text).CheckedExpr!.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        GExpr identifier = message.Expr.CallExpr.Args[1];
        Assert.Equal("int", identifier.IdentExpr.Name);
        Assert.Equal("int", message.ReferenceMap[identifier.Id].Name);
        Assert.Equal(GType.TypeKindOneofCase.Type_, message.TypeMap[identifier.Id].TypeKindCase);
        Assert.Equal(bytes, message.ToByteArray());
        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();
        Assert.True(Run<bool>(EnvNames, loaded, new CelBindings().Add("x", 1L)));
    }

    [Fact]
    public static void FieldSelectedAfterAResolvedName_IsASelectionOfTheIdentifierOfTheFullName()
    {
        const string text = "pkg.v.key == 1";
        byte[] bytes = EnvNames.Validate<bool>(text).CheckedExpr!.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        GExpr selection = message.Expr.CallExpr.Args[0];
        Assert.Equal("key", selection.SelectExpr.Field);
        Assert.Equal("pkg.v", selection.SelectExpr.Operand.IdentExpr.Name);
        Assert.Equal("pkg.v", message.ReferenceMap[selection.SelectExpr.Operand.Id].Name);
        Assert.Equal(bytes, message.ToByteArray());
        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();
        Assert.Equal(text, CelSyntax.Format(loaded));
        Assert.True(EnvNames.CompileParsed<bool>(loaded).IsSuccess);
    }

    [Fact]
    public static void GlobalCallWithALeadingDot_IsAGlobalCallWithItsOverloadInTheReference()
    {
        const string text = ".f(x) == 1";
        byte[] bytes = EnvNames.Validate<bool>(text).CheckedExpr!.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        GExpr call = message.Expr.CallExpr.Args[0];
        Assert.Equal("f", call.CallExpr.Function);
        Assert.Null(call.CallExpr.Target);
        Assert.Equal("f_int", Assert.Single(message.ReferenceMap[call.Id].OverloadId));
        Assert.Equal(bytes, message.ToByteArray());
        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();
        Assert.True(Run<bool>(EnvNames, loaded, new CelBindings().Add("x", 2L)));
    }

    [Fact]
    public static void NameThatTheContainerResolvesToAnotherDeclaration_IsResolvedAgainByTheLoad()
    {
        const string text = "limit";
        CelBindings bindings = new CelBindings().Add("acme.limit", 1L).Add("acme.acme.limit", 2L);
        byte[] bytes = EnvAcme.Validate<long>(text).CheckedExpr!.ToProtobuf();

        long fromText = EnvAcme.Compile<long>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();

        Assert.Equal(1L, fromText);
        Assert.Equal("acme.limit", Assert.IsType<CelIdentNode>(loaded.Root).Name);
        Assert.Equal(2L, Run<long>(EnvAcme, loaded, bindings));
    }

    [Fact]
    public static void LeadingDotWithoutAShadow_IsWrittenWithoutTheDotAndResolvedAgainByTheLoad()
    {
        const string text = ".y";
        CelBindings bindings = new CelBindings().Add("y", 1L).Add("com.example.y", 2L);
        byte[] bytes = EnvExample.Validate<long>(text).CheckedExpr!.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);
        long fromText = EnvExample.Compile<long>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();

        Assert.Equal("y", message.Expr.IdentExpr.Name);
        Assert.Equal("y", message.ReferenceMap[message.Expr.Id].Name);
        Assert.Equal(1L, fromText);
        Assert.Equal(2L, Run<long>(EnvExample, loaded, bindings));
    }

    [Fact]
    public static void TypeValuesOfAChecking_AreWrittenAsTheKindTypeWithTheirParameter()
    {
        GType ofOne = RootType(EnvNames, "type(1)");
        GType ofTypeOfOne = RootType(EnvNames, "type(type(1))");
        GType ofIdentifier = RootType(EnvNames, "int");

        Assert.Equal(GType.TypeKindOneofCase.Type_, ofOne.TypeKindCase);
        Assert.Equal(GType.Types.PrimitiveType.Int64, ofOne.Type_.Primitive);
        Assert.Equal(GType.TypeKindOneofCase.Type_, ofTypeOfOne.Type_.TypeKindCase);
        Assert.Equal(GType.Types.PrimitiveType.Int64, ofTypeOfOne.Type_.Type_.Primitive);
        Assert.Equal(GType.TypeKindOneofCase.Type_, ofIdentifier.TypeKindCase);
        Assert.Equal(GType.Types.PrimitiveType.Int64, ofIdentifier.Type_.Primitive);
    }

    [Fact]
    public static void TypeWriter_WritesATypeParameterATypeWithoutAParameterAndDyn()
    {
        GType parameter = WrittenType(CelType.TypeParameter("T"));
        GType bare = WrittenType(new CelType(CelTypeKind.Type, "type", []));
        GType dyn = WrittenType(CelType.Dyn);

        Assert.Equal((GType.TypeKindOneofCase.TypeParam, "T"), (parameter.TypeKindCase, parameter.TypeParam));
        Assert.Equal(GType.TypeKindOneofCase.Type_, bare.TypeKindCase);
        Assert.Equal(GType.TypeKindOneofCase.None, bare.Type_.TypeKindCase);
        Assert.Equal(GType.TypeKindOneofCase.Dyn, dyn.TypeKindCase);
    }

    [Fact]
    public static void CheckedExprFromCelGo_WithEveryKindOfTypeInItsTypeMap_LoadsWithoutAnError()
    {
        var message = new GCheckedExpr { Expr = Ident(1, "x") };
        message.TypeMap[1] = new GType
        {
            Function = new GType.Types.FunctionType
            {
                ResultType = new GType { Primitive = GType.Types.PrimitiveType.Int64 },
                ArgTypes = { new GType { Primitive = GType.Types.PrimitiveType.String }, new GType { TypeParam = "T" } },
            },
        };
        message.TypeMap[2] = new GType { Error = new Google.Protobuf.WellKnownTypes.Empty() };
        message.TypeMap[3] = new GType { TypeParam = "T" };
        message.TypeMap[4] = new GType { Type_ = new GType { Type_ = new GType { Primitive = GType.Types.PrimitiveType.Bool } } };
        message.TypeMap[5] = new GType { Type_ = new GType() };

        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(message.ToByteArray()).GetParsedExprOrThrow();

        Assert.Equal("x", Assert.IsType<CelIdentNode>(loaded.Root).Name);
    }

    [Fact]
    public static void CheckedExprFromCelGo_WithAFunctionTypeCutShort_IsInvalidProtobuf()
    {
        // type_map { 1: Type { function { result_type: a message of 5 bytes of which 1 is there } } }.
        byte[] entry = [0x1A, 0x09, 0x08, 0x01, 0x12, 0x05, 0x42, 0x03, 0x0A, 0x05, 0x18];
        byte[] bytes = [.. new GCheckedExpr { Expr = Ident(1, "x") }.ToByteArray(), .. entry];

        CelParseResult result = CelParsedExpr.FromCheckedExprProtobuf(bytes);

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.InvalidProtobuf, CelErrorStage.Parse), (error.Code, error.Stage));
    }

    // ParsedExpr { expr: list [x, y] with the optional indices and the line offsets as given }, written by hand, with the position 5
    // for the list.
    private static byte[] ListMessage(byte[] indices, byte[] lineOffsets)
    {
        byte[] elements = new GExpr.Types.CreateList { Elements = { Ident(2, "x"), Ident(3, "y") } }.ToByteArray();
        byte[] list = [.. elements, .. indices];
        byte[] expr = [0x10, 0x01, 0x3A, (byte)list.Length, .. list];
        byte[] positions = [0x22, 0x04, 0x08, 0x01, 0x10, 0x05];
        byte[] sourceInfo = [.. positions, .. lineOffsets];
        return [0x12, (byte)expr.Length, .. expr, 0x1A, (byte)sourceInfo.Length, .. sourceInfo];
    }

    private static GType RootType(CelEnvironment environment, string text)
    {
        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(environment.Validate<CelValue>(text).CheckedExpr!.ToProtobuf());
        return message.TypeMap[message.Expr.Id];
    }

    // The Type message that the writer of the library gives for the type, read by Google.Protobuf: the writer puts it in the
    // field 1 of a message, with a tag of one byte and a length of one byte.
    private static GType WrittenType(CelType type)
    {
        byte[] bytes = ProtoWriter.Encode(output => TypeWriter.Write(output, 1, type));
        Assert.Equal(0x0A, bytes[0]);
        Assert.Equal(bytes.Length - 2, bytes[1]);
        return GType.Parser.ParseFrom(bytes[2..]);
    }

    private static GExpr Ident(long id, string name) => new() { Id = id, IdentExpr = new GExpr.Types.Ident { Name = name } };

    private static (string Indices, int Line, int Offset) ListFacts(byte[] bytes)
    {
        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(bytes).GetParsedExprOrThrow();
        CelListNode list = Assert.IsType<CelListNode>(loaded.Root);
        CelSourceSpan span = loaded.GetSpan(list.Id);
        return (string.Join(',', list.OptionalIndices.Select(index => index.ToString(System.Globalization.CultureInfo.InvariantCulture))), span.Start.Line, span.Start.Offset);
    }

    private static T Run<T>(CelEnvironment environment, CelParsedExpr tree, CelBindings bindings) =>
        environment.CompileParsed<T>(tree).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
