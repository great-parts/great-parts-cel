using System;
using System.Collections.Generic;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.MessageSamples;

namespace Great.Parts.Cel.Tests.Evaluation;

// The macros of the set protos on a test description whose field has the name of an extension, pkg.count_ext, without the package
// of protobuf: the tree that the parser makes, the cost of the expansion, and the value and the presence of the field.
public sealed class ProtosMacroEvaluationTests
{
    private static readonly MessageType Description = NewMessageType();
    private static readonly CelEnvironment WithSet = CreateEnvironment(Description);

    [Theory]
    [InlineData("proto.getExt(msg, pkg.count_ext)", "pkg.count_ext", false)]
    [InlineData("proto.hasExt(msg, pkg.count_ext)", "pkg.count_ext", true)]
    [InlineData("proto.getExt(msg, a.b.c.d)", "a.b.c.d", false)]
    [InlineData("proto.hasExt(msg, .a.b)", ".a.b", true)]
    public static void Macro_IsTheSelectionOfTheFieldNamedByTheChain_AtTheParenthesis(string expression, string field, bool isPresenceTest)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ParseResult parsed = WithSet.Parse(expression);
        Assert.True(parsed.IsSuccess, expression);

        var root = Assert.IsType<SelectNode>(parsed.Expression.Root);

        Assert.Equal((field, isPresenceTest), (root.Field, root.IsPresenceTest));
        Assert.Equal("msg", Assert.IsType<IdentNode>(root.Operand).Name);
        Assert.Equal(expression.IndexOf('(', StringComparison.Ordinal), parsed.Expression.GetOffset(root.Id));
    }

    [Fact]
    public static void Macros_ReadAndTestTheField()
    {
        Dictionary<string, CelValue> set = Bind(count: 5);
        Dictionary<string, CelValue> unset = Bind(count: null);

        Assert.Equal(CelValue.FromInt64(5), Run(WithSet, "proto.getExt(msg, pkg.count_ext)", set).Value);
        Assert.Equal(CelValue.FromBoolean(true), Run(WithSet, "proto.hasExt(msg, pkg.count_ext)", set).Value);
        Assert.Equal(CelValue.FromInt64(0), Run(WithSet, "proto.getExt(msg, pkg.count_ext)", unset).Value);
        Assert.Equal(CelValue.FromBoolean(false), Run(WithSet, "proto.hasExt(msg, pkg.count_ext)", unset).Value);
    }

    [Fact]
    public static void Macros_HaveTheTypeOfTheField()
    {
        Assert.Equal(CelType.Int, WithSet.Compile<object>("proto.getExt(msg, pkg.count_ext)").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.Bool, WithSet.Compile<object>("proto.hasExt(msg, pkg.count_ext)").GetProgramOrThrow().ResultType);
    }

    // The macro costs what its expansion costs: the selection of the field, or its presence test, beyond the operand.
    [Fact]
    public static void Macro_CostsWhatTheSelectionOfTheFieldCosts()
    {
        Dictionary<string, CelValue> bindings = Bind(count: 5);

        Assert.Equal(Run(WithSet, "msg.`pkg.count_ext`", bindings).Cost, Run(WithSet, "proto.getExt(msg, pkg.count_ext)", bindings).Cost);
        Assert.Equal(Run(WithSet, "has(msg.id)", bindings).Cost, Run(WithSet, "proto.hasExt(msg, pkg.count_ext)", bindings).Cost);
    }

    // A field whose name has a dot is reachable by its macro and by the name between backquotes, and not by a chain of names.
    [Fact]
    public static void FieldWithADottedName_IsReachedByTheEscapedNameAsByTheMacro()
    {
        Dictionary<string, CelValue> bindings = Bind(count: 5);

        Assert.Equal(CelValue.FromInt64(5), Run(WithSet, "msg.`pkg.count_ext`", bindings).Value);
        Assert.False(WithSet.Compile<object>("msg.pkg.count_ext").IsSuccess);
    }

    private static Dictionary<string, CelValue> Bind(long? count)
    {
        var message = new Message();
        if (count is { } number)
        {
            message.Values[Extension] = CelValue.FromInt64(number);
        }

        CelValue value = CelValue.FromObject(Description, message);
        return new Dictionary<string, CelValue> { ["msg"] = value, ["d"] = value };
    }
}
