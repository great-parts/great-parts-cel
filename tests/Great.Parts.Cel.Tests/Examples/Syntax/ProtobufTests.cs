using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Google.Protobuf;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Values;
using Xunit;
using GCheckedExpr = global::Cel.Expr.CheckedExpr;
using GConstant = global::Cel.Expr.Constant;
using GExpr = global::Cel.Expr.Expr;
using GParsedExpr = global::Cel.Expr.ParsedExpr;
using GType = global::Cel.Expr.Type;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The protobuf form of parsed and checked expressions, against the messages ParsedExpr and CheckedExpr that Google.Protobuf compiles
// from the pinned cel-spec: what the library writes, Google.Protobuf reads, and the same bytes written again by Google.Protobuf are
// the bytes of the library; what the library reads, from its own bytes and from the messages of Google.Protobuf.
[Trait("Requirement", "F-20")]
[Trait("Requirement", "R-10")]
public sealed class ProtobufTests
{
    private static readonly CelEnvironment Env = TreeEnvironments.Env;
    private static readonly CelEnvironment EnvMath = TreeEnvironments.EnvMath;
    private static readonly CelEnvironment EnvContainer = CheckedEnvironments.Container;
    private static readonly CelEnvironment EnvProto = CheckedEnvironments.Proto;
    private static readonly CelEnvironment EnvDecimal = DecimalExample.Create();

    private static readonly string[] Texts =
    [
        "a + b",
        "-5",
        "2.5",
        "1u",
        "\"s\"",
        "b\"\\x00\"",
        "true",
        "null",
        ".x",
        "m.`a-b`",
        "has(m.f)",
        "items.all(v, v > 0)",
        "items.exists_one(v, v > 0)",
        "items.map(v, v > 0, v * 2)",
        "cel.bind(t, 1, t + 1)",
        "[1, ?d]",
        "{\"a\": 1, ?\"b\": d}",
        "m.?f",
        "s.startsWith(\"a\")",
        "a + b * c",
        "x > 1 ? a : b",
        "[1, 2][x]",
        "!(a == b) && c > 1 || items.exists(v, v == x)",
        "items.all(v, items.exists(w, v == w))",
        "\"é😀\" + s",
    ];

    [Fact]
    public static void CheckedExpression_IsReadByGoogleProtobufAndLoadsBackWithThePositions()
    {
        const string text = "items.all(v, v > x) && name.startsWith(\"é😀\")";
        CelValidationResult validated = Env.Validate<bool>(text);
        byte[] bytes = validated.CheckedExpr!.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);
        Assert.Equal(GExpr.ExprKindOneofCase.CallExpr, message.Expr.ExprKindCase);
        Assert.Equal("_&&_", message.Expr.CallExpr.Function);
        Assert.Contains(message.SourceInfo.MacroCalls, call => call.Value.CallExpr.Function == "all");
        Assert.Equal<int>([text.Length], message.SourceInfo.LineOffsets);
        Assert.Equal(bytes, message.ToByteArray());

        CelParsedExpr original = validated.CheckedExpr.ParsedExpr;
        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();
        Assert.Null(SyntaxTreeComparer.Difference(loaded.Root, original.Root));
        Assert.Equal(CelSyntax.Format(original), CelSyntax.Format(loaded));
        Assert.True(Env.CompileParsed<bool>(loaded).IsSuccess);
        AssertSameStarts(original, loaded);
    }

    [Fact]
    public static void ParsedExpression_IsReadByGoogleProtobufAndIsItsOwnBytes()
    {
        foreach (string text in Texts)
        {
            byte[] bytes = Env.Parse(text).GetParsedExprOrThrow().ToProtobuf();

            GParsedExpr message = GParsedExpr.Parser.ParseFrom(bytes);

            Assert.True(message.Expr.ExprKindCase != GExpr.ExprKindOneofCase.None, text);
            Assert.Equal(bytes, message.ToByteArray());
        }
    }

    [Fact]
    public static void WritingTwice_GivesTheSameBytes()
    {
        CelParsedExpr tree = Env.Parse("items.all(v, v > x) && has(m.f)").GetParsedExprOrThrow();
        CelCheckedExpr checkedExpr = Env.Validate<bool>("items.all(v, v > x) && has(m.f)").CheckedExpr!;

        Assert.Equal(tree.ToProtobuf(), tree.ToProtobuf());
        Assert.Equal(checkedExpr.ToProtobuf(), checkedExpr.ToProtobuf());
    }

    [Fact]
    public static void EveryTextOfTheTable_LoadsBackWithItsTreeStartsAndMacroCalls()
    {
        foreach (string text in Texts)
        {
            CelParsedExpr original = Env.Parse(text).GetParsedExprOrThrow();

            CelParsedExpr loaded = CelParsedExpr.FromProtobuf(original.ToProtobuf(), text).GetParsedExprOrThrow();

            Assert.Null(SyntaxTreeComparer.Difference(loaded.Root, original.Root));
            AssertSameStarts(original, loaded);
            Assert.All(loaded.Root.DescendantsAndSelf(), node => Assert.Equal(0, loaded.GetSpan(node.Id).Length));
            AssertSameMacroCalls(original, loaded);
        }
    }

    [Fact]
    public static void TextWithoutCharactersOutsideTheBasicPlane_LoadsBackWithoutTheText()
    {
        CelParsedExpr original = Env.Parse("items.all(v, v > x)\n && name == s").GetParsedExprOrThrow();

        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(original.ToProtobuf()).GetParsedExprOrThrow();

        Assert.Null(SyntaxTreeComparer.Difference(loaded.Root, original.Root));
        AssertSameStarts(original, loaded);
        Assert.Equal(2, loaded.GetSpan(loaded.Root.Id).Start.Line);
    }

    [Fact]
    public static void Positions_AreCodePointsInTheMessageAndUtf16UnitsInTheTree()
    {
        const string text = "\"😀\" == name";
        CelParsedExpr tree = Env.Parse(text).GetParsedExprOrThrow();
        byte[] bytes = tree.ToProtobuf();

        GParsedExpr message = GParsedExpr.Parser.ParseFrom(bytes);

        Assert.Equal(5, tree.GetSpan(tree.Root.Id).Start.Offset);
        Assert.Equal(4, message.SourceInfo.Positions[tree.Root.Id]);
        Assert.Equal<int>([text.Length], message.SourceInfo.LineOffsets);
        Assert.Equal(5, CelParsedExpr.FromProtobuf(bytes, text).GetParsedExprOrThrow().GetSpan(tree.Root.Id).Start.Offset);
        Assert.Equal(4, CelParsedExpr.FromProtobuf(bytes).GetParsedExprOrThrow().GetSpan(tree.Root.Id).Start.Offset);
    }

    [Fact]
    public static void LinesAndLocation_AreWrittenAsCelGoWritesThem()
    {
        var options = new CelCompileOptions { SourceLocation = new CelSourceLocation("rules.json", 10, 2, 5) };
        CelParsedExpr tree = Env.Parse("a +\n b", options).GetParsedExprOrThrow();

        GParsedExpr message = GParsedExpr.Parser.ParseFrom(tree.ToProtobuf());

        Assert.Equal("rules.json", message.SourceInfo.Location);
        Assert.Equal<int>([4, 7], message.SourceInfo.LineOffsets);
        Assert.Equal("rules.json", CelParsedExpr.FromProtobuf(tree.ToProtobuf(), "a +\n b").GetParsedExprOrThrow().SourceName);
    }

    [Fact]
    public static void PresenceTest_LoadsBackWithTheArgumentThatTheTreeDoesNotHave()
    {
        CelParsedExpr original = Env.Parse("has(m.f)").GetParsedExprOrThrow();

        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(original.ToProtobuf(), "has(m.f)").GetParsedExprOrThrow();

        CelSelectNode root = Assert.IsType<CelSelectNode>(loaded.Root);
        Assert.True(root.IsPresenceTest);
        CelMacroCall call = loaded.GetMacroCall(root.Id)!;
        Assert.Equal("has", call.Function);
        CelSelectNode argument = Assert.IsType<CelSelectNode>(Assert.Single(call.Arguments));
        Assert.Null(loaded.FindNode(argument.Id));
        Assert.Same(root.Operand, argument.Operand);
    }

    [Fact]
    public static void ReplacementInALoadedTree_ReachesTheMacroCall()
    {
        const string text = "x + items.all(v, v > x)";
        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(Env.Parse(text).GetParsedExprOrThrow().ToProtobuf(), text).GetParsedExprOrThrow();
        var factory = new CelExprFactory(text);

        CelExprNode copy = factory.Copy(loaded, loaded.Root, node => node is CelIdentNode { Name: "x" } ? factory.Constant(CelValue.FromInt64(5)) : null);

        Assert.Equal("5 + items.all(v, v > 5)", CelSyntax.Format(factory.Build(copy)));
    }

    [Fact]
    public static void MessageOfGoogleProtobuf_LoadsWithNewIds()
    {
        var message = new GParsedExpr { Expr = new GExpr { Id = 7, IdentExpr = new GExpr.Types.Ident { Name = "x" } } };

        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(message.ToByteArray()).GetParsedExprOrThrow();

        CelIdentNode ident = Assert.IsType<CelIdentNode>(loaded.Root);
        Assert.Equal("x", ident.Name);
        Assert.Equal(1, ident.Id);
    }

    [Fact]
    public static void DecimalConstant_IsWrittenAsACallAndLoadsAsACall()
    {
        const string text = "1.50m";
        CelParsedExpr parsed = EnvDecimal.Parse(text).GetParsedExprOrThrow();
        byte[] bytes = parsed.ToProtobuf();

        GParsedExpr message = GParsedExpr.Parser.ParseFrom(bytes);

        GExpr call = message.Expr;
        Assert.Equal("decimal", call.CallExpr.Function);
        GExpr argument = Assert.Single(call.CallExpr.Args);
        Assert.Equal("1.50", argument.ConstExpr.StringValue);
        Assert.Equal((long)parsed.Root.Id, call.Id);
        Assert.Equal((long)parsed.IdCount + 1, argument.Id);
        Assert.Equal(0, message.SourceInfo.Positions[call.Id]);
        Assert.Equal(0, message.SourceInfo.Positions[argument.Id]);
        Assert.Equal(bytes, message.ToByteArray());

        // Loaded in an environment with the set and without the literal option, which only the call does not need.
        CelEnvironment withoutLiteral = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(CelExtensions.Decimal(1)).Build().GetEnvironmentOrThrow();
        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(bytes, text).GetParsedExprOrThrow();
        CelCallNode root = Assert.IsType<CelCallNode>(loaded.Root);
        Assert.Equal("decimal", root.Function);
        CelConstantNode text1 = Assert.IsType<CelConstantNode>(Assert.Single(root.Arguments));
        Assert.True(text1.Value.TryGetString(out string? digits));
        Assert.Equal("1.50", digits);
        Assert.Contains("decimal", withoutLiteral.ValidateParsed(loaded).ReferencedFunctions);

        var factory = new CelExprFactory();
        CelParsedExpr shown = factory.Build(factory.Call("string", factory.Copy(loaded, loaded.Root)));
        Assert.Equal("1.50", Run<string>(withoutLiteral, shown, new CelBindings()));
    }

    [Fact]
    public static void DecimalConstantsOfAChecking_HaveTheTypesOfTheCallAndOfItsArgument()
    {
        CelCheckedExpr checkedExpr = EnvDecimal.Validate<CelValue>("1.50m + 1.00m").CheckedExpr!;
        byte[] bytes = checkedExpr.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        int largest = checkedExpr.ParsedExpr.IdCount;
        Google.Protobuf.Collections.RepeatedField<GExpr> calls = message.Expr.CallExpr.Args;
        Assert.Equal(2, calls.Count);
        for (int i = 0; i < calls.Count; i++)
        {
            GExpr call = calls[i];
            GExpr argument = Assert.Single(call.CallExpr.Args);
            Assert.Equal("decimal", call.CallExpr.Function);
            Assert.Equal((long)(largest + 1 + i), argument.Id);
            Assert.Equal(GType.TypeKindOneofCase.AbstractType, message.TypeMap[call.Id].TypeKindCase);
            Assert.Equal("decimal", message.TypeMap[call.Id].AbstractType.Name);
            Assert.Equal(GType.Types.PrimitiveType.String, message.TypeMap[argument.Id].Primitive);
            Assert.Equal("string_to_decimal", Assert.Single(message.ReferenceMap[call.Id].OverloadId));
        }

        Assert.Equal(bytes, message.ToByteArray());
    }

    [Fact]
    public static void QualifiedFunctionOfAChecking_IsAGlobalCallOfTheFullName()
    {
        const string text = "math.abs(x)";
        CelCheckedExpr checkedExpr = EnvMath.Validate<long>(text).CheckedExpr!;
        CelCallNode original = Assert.IsType<CelCallNode>(checkedExpr.ParsedExpr.Root);
        int mathId = Assert.IsType<CelIdentNode>(original.Target).Id;
        byte[] bytes = checkedExpr.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        Assert.Equal((long)original.Id, message.Expr.Id);
        Assert.Equal("math.abs", message.Expr.CallExpr.Function);
        Assert.Null(message.Expr.CallExpr.Target);
        Assert.Equal("x", Assert.Single(message.Expr.CallExpr.Args).IdentExpr.Name);
        Assert.False(message.SourceInfo.Positions.ContainsKey(mathId));
        Assert.False(message.TypeMap.ContainsKey(mathId));
        Assert.Equal(string.Empty, message.ReferenceMap[original.Id].Name);
        Assert.Equal("math_abs_int", Assert.Single(message.ReferenceMap[original.Id].OverloadId));
        Assert.Equal(bytes, message.ToByteArray());

        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();
        CelCallNode root = Assert.IsType<CelCallNode>(loaded.Root);
        Assert.Equal("math.abs", root.Function);
        Assert.Null(root.Target);
        Assert.Equal(3L, Run<long>(EnvMath, loaded, new CelBindings().Add("x", -3L)));
        Assert.Equal(text, CelSyntax.Format(loaded));
    }

    [Fact]
    public static void EnumerationConstantOfAChecking_IsAnIdentifierOfTheFullName()
    {
        CelCheckedExpr checkedExpr = EnvProto.Validate<bool>("TestAllTypes.NestedEnum.BAR == 1").CheckedExpr!;
        CelCallNode original = Assert.IsType<CelCallNode>(checkedExpr.ParsedExpr.Root);
        CelSelectNode constant = Assert.IsType<CelSelectNode>(original.Arguments[0]);
        byte[] bytes = checkedExpr.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        GExpr first = message.Expr.CallExpr.Args[0];
        Assert.Equal("cel.expr.conformance.proto3.TestAllTypes.NestedEnum.BAR", first.IdentExpr.Name);
        Assert.Equal((long)constant.Id, first.Id);
        Assert.Equal("cel.expr.conformance.proto3.TestAllTypes.NestedEnum.BAR", message.ReferenceMap[constant.Id].Name);
        Assert.Equal(1L, message.ReferenceMap[constant.Id].Value.Int64Value);
        Assert.Equal(1, message.Expr.CallExpr.Args.Count(arg => arg.ExprKindCase == GExpr.ExprKindOneofCase.IdentExpr));

        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes).GetParsedExprOrThrow();
        Assert.True(Run<bool>(EnvProto, loaded, new CelBindings()));
    }

    [Fact]
    public static void NamesInTheContainerOfAChecking_AreWrittenWithTheirFullNamesAndCompileAsTheText()
    {
        const string text = "limit > 0 && f(x) == 1";
        CelCheckedExpr checkedExpr = EnvContainer.Validate<bool>(text).CheckedExpr!;
        byte[] bytes = checkedExpr.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        GExpr comparison = message.Expr.CallExpr.Args[0];
        Assert.Equal("acme.limit", comparison.CallExpr.Args[0].IdentExpr.Name);
        GExpr call = message.Expr.CallExpr.Args[1].CallExpr.Args[0];
        Assert.Equal("acme.f", call.CallExpr.Function);

        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();
        var bindings = new CelBindings().Add("acme.limit", 5L).Add("x", 2L);
        Assert.Equal(Run<bool>(EnvContainer, EnvContainer.Parse(text).GetParsedExprOrThrow(), bindings), Run<bool>(EnvContainer, loaded, bindings));
    }

    [Fact]
    public static void MessageLiteralOfAChecking_HasTheFullNameOfItsType()
    {
        const string text = "TestAllTypes{single_int64: 1}.single_int64 == 1";
        CelCheckedExpr checkedExpr = EnvProto.Validate<bool>(text).CheckedExpr!;
        byte[] bytes = checkedExpr.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        GExpr literal = message.Expr.CallExpr.Args[0].SelectExpr.Operand;
        Assert.Equal("cel.expr.conformance.proto3.TestAllTypes", literal.StructExpr.MessageName);
        Assert.Equal("single_int64", Assert.Single(literal.StructExpr.Entries).FieldKey);
        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();
        Assert.True(Run<bool>(EnvProto, loaded, new CelBindings()));
    }

    [Fact]
    public static void QualifiedFunctionInAMacro_IsAGlobalCallInTheTreeAndTheFormOfParsingInTheMacroCall()
    {
        const string text = "[1, -2].all(v, math.abs(v) > 0)";
        CelCheckedExpr checkedExpr = EnvMath.Validate<bool>(text).CheckedExpr!;
        int mathId = checkedExpr.ParsedExpr.Root.DescendantsAndSelf().OfType<CelIdentNode>().Single(node => node.Name == "math").Id;
        byte[] bytes = checkedExpr.ToProtobuf();

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(bytes);

        GExpr predicate = message.SourceInfo.MacroCalls.Single().Value.CallExpr.Args[1];
        GExpr abs = predicate.CallExpr.Args[0];
        Assert.Equal("abs", abs.CallExpr.Function);
        Assert.Equal("math", abs.CallExpr.Target.IdentExpr.Name);
        Assert.True(message.SourceInfo.Positions.ContainsKey(mathId));
        Assert.False(message.TypeMap.ContainsKey(mathId));

        CelParsedExpr loaded = CelParsedExpr.FromCheckedExprProtobuf(bytes, text).GetParsedExprOrThrow();
        CelMacroCall call = loaded.GetMacroCall(loaded.Root.Id)!;
        Assert.Same(loaded.FindNode(call.Arguments[1].Id), call.Arguments[1]);
        Assert.True(Run<bool>(EnvMath, loaded, new CelBindings()));
        Assert.Equal(text, CelSyntax.Format(loaded));
    }

    [Fact]
    public static void LeadingDotOfAChecking_IsKeptInTheIdentifierAndInTheReferenceWhenAVariableShadowsTheName()
    {
        CelCheckedExpr shadowed = Env.Validate<bool>("[5].exists(x, .x > 0)").CheckedExpr!;
        CelIdentNode dotted = shadowed.ParsedExpr.Root.DescendantsAndSelf().OfType<CelIdentNode>().Single(node => node.Name == ".x");
        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(shadowed.ToProtobuf());
        Assert.Equal(".x", message.ReferenceMap[dotted.Id].Name);
        Assert.Equal(".x", Descendants(message.Expr).Single(node => node.Id == dotted.Id).IdentExpr.Name);
        Assert.Equal(".x", shadowed.ReferenceMap[dotted.Id].Name);

        CelCheckedExpr outside = Env.Validate<bool>(".x > 0").CheckedExpr!;
        CelIdentNode global = Assert.IsType<CelIdentNode>(Assert.IsType<CelCallNode>(outside.ParsedExpr.Root).Arguments[0]);
        GCheckedExpr plain = GCheckedExpr.Parser.ParseFrom(outside.ToProtobuf());
        Assert.Equal("x", plain.ReferenceMap[global.Id].Name);
        Assert.Equal("x", plain.Expr.CallExpr.Args[0].IdentExpr.Name);
    }

    [Fact]
    public static void NameInTheContainerInAMacro_IsAFullNameInTheTreeAndTheWrittenNameInTheMacroCall()
    {
        CelCheckedExpr checkedExpr = EnvContainer.Validate<bool>("[1].all(v, v > limit)").CheckedExpr!;

        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(checkedExpr.ToProtobuf());

        Assert.Contains(Descendants(message.Expr), node => node.ExprKindCase == GExpr.ExprKindOneofCase.IdentExpr && node.IdentExpr.Name == "acme.limit");
        GExpr predicate = message.SourceInfo.MacroCalls.Single().Value.CallExpr.Args[1];
        Assert.Equal("limit", predicate.CallExpr.Args[1].IdentExpr.Name);
    }

    [Fact]
    public static void TypesOfAChecking_AreWrittenAsTheKindsOfTheMessage()
    {
        AssertRootType(Env, "[1, 2]", type => Assert.Equal(GType.Types.PrimitiveType.Int64, type.ListType.ElemType.Primitive));
        AssertRootType(Env, "{\"a\": 1}", type => Assert.Equal(GType.Types.PrimitiveType.String, type.MapType.KeyType.Primitive));
        AssertRootType(Env, "m.?f", type => Assert.Equal("optional_type", type.AbstractType.Name));
        AssertRootType(Env, "timestamp(\"2020-01-01T00:00:00Z\")", type => Assert.Equal(GType.Types.WellKnownType.Timestamp, type.WellKnown));
        AssertRootType(Env, "duration(\"1s\")", type => Assert.Equal(GType.Types.WellKnownType.Duration, type.WellKnown));
        AssertRootType(Env, "null", type => Assert.Equal(GType.TypeKindOneofCase.Null, type.TypeKindCase));
        AssertRootType(Env, "b\"x\"", type => Assert.Equal(GType.Types.PrimitiveType.Bytes, type.Primitive));
        AssertRootType(Env, "d", type => Assert.Equal(GType.TypeKindOneofCase.Dyn, type.TypeKindCase));
        AssertRootType(EnvProto, "TestAllTypes{}", type => Assert.Equal("cel.expr.conformance.proto3.TestAllTypes", type.MessageType));
        AssertRootType(EnvProto, "TestAllTypes{}.single_int64_wrapper", type => Assert.Equal(GType.Types.PrimitiveType.Int64, type.Wrapper));
    }

    [Fact]
    public static void DeprecatedDurationConstant_LoadsAsAConstantOfTheKindDuration()
    {
        // A ParsedExpr with the constant duration_value (field 8), which is the field of an older writer:
        // expr(2) { id(2) = 1, const_expr(3) { duration_value(8) { seconds(1) = 5 } } }.
        byte[] bytes = [0x12, 0x08, 0x10, 0x01, 0x1A, 0x04, 0x42, 0x02, 0x08, 0x05];

        CelParsedExpr loaded = CelParsedExpr.FromProtobuf(bytes).GetParsedExprOrThrow();

        CelConstantNode node = Assert.IsType<CelConstantNode>(loaded.Root);
        Assert.Equal(CelValueKind.Duration, node.Value.Kind);
        Assert.Equal("duration(\"5s\")", CelSyntax.Format(loaded));
        Assert.True(Env.CompileParsed<CelValue>(loaded).IsSuccess);
        Assert.Equal(GConstant.ConstantKindOneofCase.DurationValue, GParsedExpr.Parser.ParseFrom(loaded.ToProtobuf()).Expr.ConstExpr.ConstantKindCase);
    }

    [Fact]
    public static void EmptyBytes_AreNoExpression()
    {
        AssertFailure(CelParsedExpr.FromProtobuf(ReadOnlySpan<byte>.Empty), CelErrorCode.InvalidSyntaxTree);
        AssertFailure(CelParsedExpr.FromCheckedExprProtobuf(ReadOnlySpan<byte>.Empty), CelErrorCode.InvalidSyntaxTree);
    }

    [Fact]
    public static void BytesThatAreNotAMessage_AreInvalidProtobuf()
    {
        byte[] cut = [0xFF];
        byte[] beyond = [0x12, 0x7F, 0x01];
        byte[] notUtf8 = [0x12, 0x07, 0x22, 0x05, 0x0A, 0x03, 0xFF, 0xFE, 0xFD];

        AssertFailure(CelParsedExpr.FromProtobuf(cut), CelErrorCode.InvalidProtobuf);
        AssertFailure(CelParsedExpr.FromProtobuf(beyond), CelErrorCode.InvalidProtobuf);
        AssertFailure(CelParsedExpr.FromProtobuf(notUtf8), CelErrorCode.InvalidProtobuf);
    }

    [Fact]
    public static void ExpressionWithoutAKind_IsInvalidSyntaxTreeWithItsId()
    {
        byte[] bytes = new GParsedExpr { Expr = new GExpr { Id = 41 } }.ToByteArray();

        CelParseResult result = CelParsedExpr.FromProtobuf(bytes);

        AssertFailure(result, CelErrorCode.InvalidSyntaxTree);
        Assert.Contains("41", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void TreeThatBreaksTheRules_IsInvalidSyntaxTree()
    {
        GExpr Ident(long id) => new() { Id = id, IdentExpr = new GExpr.Types.Ident { Name = "x" } };

        var twice = new GExpr { Id = 1, CallExpr = new GExpr.Types.Call { Function = "_+_", Args = { Ident(2), Ident(2) } } };
        var optional = new GExpr { Id = 1, ListExpr = new GExpr.Types.CreateList { Elements = { Ident(2), Ident(3) }, OptionalIndices = { 5 } } };
        var fieldInMap = new GExpr
        {
            Id = 1,
            StructExpr = new GExpr.Types.CreateStruct
            {
                Entries = { new GExpr.Types.CreateStruct.Types.Entry { Id = 2, FieldKey = "a", Value = Ident(3) } },
            },
        };
        var noName = new GExpr { Id = 1, IdentExpr = new GExpr.Types.Ident() };

        foreach (GExpr expr in new[] { twice, optional, fieldInMap, noName })
        {
            AssertFailure(CelParsedExpr.FromProtobuf(new GParsedExpr { Expr = expr }.ToByteArray()), CelErrorCode.InvalidSyntaxTree);
        }
    }

    [Fact]
    public static void PositionOutsideTheText_IsInvalidSyntaxTree()
    {
        var message = new GParsedExpr
        {
            Expr = new GExpr { Id = 1, IdentExpr = new GExpr.Types.Ident { Name = "x" } },
            SourceInfo = new global::Cel.Expr.SourceInfo { Positions = { [1] = 100 } },
        };

        AssertFailure(CelParsedExpr.FromProtobuf(message.ToByteArray(), "0123456789"), CelErrorCode.InvalidSyntaxTree);
        Assert.True(CelParsedExpr.FromProtobuf(message.ToByteArray()).IsSuccess);
    }

    [Fact]
    public static void MacroCallOfTheMessage_MustNameANodeOfTheTree()
    {
        var call = new GExpr { CallExpr = new GExpr.Types.Call { Function = "has" } };
        var message = new GParsedExpr
        {
            Expr = new GExpr { Id = 1, IdentExpr = new GExpr.Types.Ident { Name = "x" } },
            SourceInfo = new global::Cel.Expr.SourceInfo { MacroCalls = { [99] = call } },
        };

        AssertFailure(CelParsedExpr.FromProtobuf(message.ToByteArray()), CelErrorCode.InvalidSyntaxTree);
    }

    [Fact]
    public static void DeepNesting_IsNestingTooDeepWithoutAnException()
    {
        AssertFailure(CelParsedExpr.FromProtobuf(NestedNots(100_000)), CelErrorCode.NestingTooDeep);

        // A tree of the depth that parsing accepts is written and read back.
        var factory = new CelExprFactory();
        CelExprNode node = factory.Ident("x");
        for (int i = 0; i < 200; i++)
        {
            node = factory.Call("!_", node);
        }

        CelParsedExpr deep = factory.Build(node);
        Assert.True(CelParsedExpr.FromProtobuf(deep.ToProtobuf()).IsSuccess);
    }

    [Fact]
    public static void UnknownField_IsSkipped()
    {
        // Expr { id: 1, ident_expr { name: "x" }, field 99: 5 }
        byte[] expr = [0x10, 0x01, 0x22, 0x03, 0x0A, 0x01, (byte)'x', 0x98, 0x06, 0x05];
        byte[] bytes = [0x12, (byte)expr.Length, .. expr];

        CelParseResult result = CelParsedExpr.FromProtobuf(bytes);

        Assert.True(result.IsSuccess);
        Assert.Equal("x", Assert.IsType<CelIdentNode>(result.GetParsedExprOrThrow().Root).Name);
    }

    [Fact]
    public static void BrokenTypeMapOfAChecking_IsInvalidProtobuf()
    {
        byte[] valid = Env.Validate<bool>("x > 1").CheckedExpr!.ToProtobuf();
        byte[] broken = [.. valid, 0x1A, 0x7F];

        AssertFailure(CelParsedExpr.FromCheckedExprProtobuf(broken), CelErrorCode.InvalidProtobuf);
        Assert.True(CelParsedExpr.FromCheckedExprProtobuf(valid).IsSuccess);
    }

    [Fact]
    public static void ParsedExpressionRead_AsACheckedOne_IsAnErrorNotAnException()
    {
        byte[] bytes = Env.Parse("x > 1").GetParsedExprOrThrow().ToProtobuf();

        CelParseResult result = CelParsedExpr.FromCheckedExprProtobuf(bytes);

        Assert.False(result.IsSuccess);
        Assert.True(result.Errors[0].Code is CelErrorCode.InvalidSyntaxTree or CelErrorCode.InvalidProtobuf);
    }

    private static T Run<T>(CelEnvironment environment, CelParsedExpr tree, CelBindings bindings) =>
        environment.CompileParsed<T>(tree).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();

    private static void AssertFailure(CelParseResult result, CelErrorCode code)
    {
        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal((code, CelErrorStage.Parse), (error.Code, error.Stage));
    }

    // The start of each node, node by node in the order of the walk, as the original has it.
    private static void AssertSameStarts(CelParsedExpr original, CelParsedExpr loaded)
    {
        CelExprNode[] was = [.. original.Root.DescendantsAndSelf()];
        CelExprNode[] now = [.. loaded.Root.DescendantsAndSelf()];
        Assert.Equal(was.Length, now.Length);
        for (int i = 0; i < was.Length; i++)
        {
            Assert.Equal(original.GetSpan(was[i].Id).Start.Offset, loaded.GetSpan(now[i].Id).Start.Offset);
        }
    }

    // The macro calls of the loaded tree: the same calls, and the same split of their parts into nodes of the tree and nodes of their own.
    private static void AssertSameMacroCalls(CelParsedExpr original, CelParsedExpr loaded)
    {
        CelExprNode[] was = [.. original.Root.DescendantsAndSelf()];
        CelExprNode[] now = [.. loaded.Root.DescendantsAndSelf()];
        for (int i = 0; i < was.Length; i++)
        {
            CelMacroCall? before = original.GetMacroCall(was[i].Id);
            CelMacroCall? after = loaded.GetMacroCall(now[i].Id);
            Assert.Equal(before is null, after is null);
            if (before is null || after is null)
            {
                continue;
            }

            Assert.Equal(before.Function, after.Function);
            Assert.Equal(before.Target is null, after.Target is null);
            Assert.Equal(before.Arguments.Length, after.Arguments.Length);
            for (int j = 0; j < before.Arguments.Length; j++)
            {
                Assert.Equal(
                    ReferenceEquals(original.FindNode(before.Arguments[j].Id), before.Arguments[j]),
                    ReferenceEquals(loaded.FindNode(after.Arguments[j].Id), after.Arguments[j]));
            }
        }
    }

    // The type that the checked message gives the root.
    private static void AssertRootType(CelEnvironment environment, string text, Action<GType> check)
    {
        CelValidationResult validated = environment.Validate<CelValue>(text);
        Assert.True(validated.IsValid, text);
        GCheckedExpr message = GCheckedExpr.Parser.ParseFrom(validated.CheckedExpr!.ToProtobuf());

        check(message.TypeMap[message.Expr.Id]);
    }

    // The expression and the expressions below it, in the order of the walk.
    private static IEnumerable<GExpr> Descendants(GExpr root)
    {
        var pending = new Stack<GExpr>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            GExpr node = pending.Pop();
            yield return node;
            foreach (GExpr child in ChildrenOf(node).Reverse())
            {
                pending.Push(child);
            }
        }
    }

    private static IEnumerable<GExpr> ChildrenOf(GExpr node)
    {
        switch (node.ExprKindCase)
        {
            case GExpr.ExprKindOneofCase.SelectExpr:
                return [node.SelectExpr.Operand];
            case GExpr.ExprKindOneofCase.CallExpr:
                return node.CallExpr.Target is null ? node.CallExpr.Args : node.CallExpr.Args.Prepend(node.CallExpr.Target);
            case GExpr.ExprKindOneofCase.ListExpr:
                return node.ListExpr.Elements;
            case GExpr.ExprKindOneofCase.ComprehensionExpr:
                GExpr.Types.Comprehension c = node.ComprehensionExpr;
                return [c.IterRange, c.AccuInit, c.LoopCondition, c.LoopStep, c.Result];
            default:
                return [];
        }
    }

    // ParsedExpr { expr: <depth> nested calls of !_ around an identifier }, written from the bytes, without recursion: the sizes of
    // the messages come from the inside out and the bytes from the outside in.
    private static byte[] NestedNots(int depth)
    {
        long[] sizes = new long[depth + 1];
        byte[] leaf = [0x22, 0x03, 0x0A, 0x01, (byte)'x'];
        sizes[0] = leaf.Length;
        for (int level = 1; level <= depth; level++)
        {
            long call = 4 + 1 + VarintSize(sizes[level - 1]) + sizes[level - 1];
            sizes[level] = 1 + VarintSize(call) + call;
        }

        var bytes = new List<byte>((int)(sizes[depth] + 8)) { 0x12 };
        AddVarint(bytes, sizes[depth]);
        for (int level = depth; level >= 1; level--)
        {
            long call = 4 + 1 + VarintSize(sizes[level - 1]) + sizes[level - 1];
            bytes.Add(0x32);
            AddVarint(bytes, call);
            bytes.AddRange([0x12, 0x02, (byte)'!', (byte)'_', 0x1A]);
            AddVarint(bytes, sizes[level - 1]);
        }

        bytes.AddRange(leaf);
        return [.. bytes];
    }

    private static int VarintSize(long value)
    {
        int size = 1;
        for (ulong rest = (ulong)value; rest >= 0x80; rest >>= 7)
        {
            size++;
        }

        return size;
    }

    private static void AddVarint(List<byte> bytes, long value)
    {
        ulong rest = (ulong)value;
        while (rest >= 0x80)
        {
            bytes.Add((byte)(rest | 0x80));
            rest >>= 7;
        }

        bytes.Add((byte)rest);
    }
}
