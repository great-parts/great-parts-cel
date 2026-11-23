using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Syntax;
using Great.Parts.Cel.Tests.Examples.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The checked expression that validation gives: the type of every node and the reference of every name and call, by the id of the node
// of the parsed tree, which keeps the form of parsing; the nodes inside a resolved name have neither.
[Trait("Requirement", "F-20")]
public sealed class CheckedExprTests
{
    private static readonly CelEnvironment Env = TreeEnvironments.Env;
    private static readonly CelEnvironment EnvMath = TreeEnvironments.EnvMath;

    private static readonly CelEnvironment EnvContainer = CheckedEnvironments.Container;
    private static readonly CelEnvironment EnvProto = CheckedEnvironments.Proto;

    [Fact]
    public static void Comparison_HasTheTypeAndTheReferenceOfEveryNode()
    {
        CelCheckedExpr checkedExpr = Checked<bool>(Env, "x > 1");

        CelCallNode root = Assert.IsType<CelCallNode>(checkedExpr.ParsedExpr.Root);
        CelExprNode x = root.Arguments[0];
        CelExprNode one = root.Arguments[1];
        Assert.Equal(CelType.Bool, checkedExpr.ResultType);
        Assert.Equal(CelType.Bool, checkedExpr.TypeMap[root.Id]);
        Assert.Equal(CelType.Int, checkedExpr.TypeMap[x.Id]);
        Assert.Equal(CelType.Int, checkedExpr.TypeMap[one.Id]);
        Assert.All(root.DescendantsAndSelf(), node => Assert.True(checkedExpr.TypeMap.ContainsKey(node.Id)));
        CelReference variable = checkedExpr.ReferenceMap[x.Id];
        Assert.Equal("x", variable.Name);
        Assert.Empty(variable.OverloadIds);
        Assert.Null(variable.Value);
        CelReference call = checkedExpr.ReferenceMap[root.Id];
        Assert.Equal("_>_", call.Name);
        Assert.Equal("greater_int64", Assert.Single(call.OverloadIds));
    }

    [Fact]
    public static void DynOperand_KeepsTheOverloadsThatItCouldTake()
    {
        CelCheckedExpr checkedExpr = Checked<bool>(Env, "d > 1");

        CelCallNode root = Assert.IsType<CelCallNode>(checkedExpr.ParsedExpr.Root);
        var ids = checkedExpr.ReferenceMap[root.Id].OverloadIds.ToList();
        Assert.True(ids.Count > 1);
        Assert.Contains("greater_int64", ids);
        Assert.Contains("greater_double_int64", ids);
        Assert.Equal(CelType.Dyn, checkedExpr.TypeMap[root.Arguments[0].Id]);
    }

    [Fact]
    public static void NameInTheContainer_HasTheFullNameInItsReference()
    {
        CelCheckedExpr checkedExpr = Checked<bool>(EnvContainer, "limit > 0");

        CelCallNode root = Assert.IsType<CelCallNode>(checkedExpr.ParsedExpr.Root);
        Assert.Equal("acme.limit", checkedExpr.ReferenceMap[root.Arguments[0].Id].Name);
    }

    [Fact]
    public static void EnumerationConstant_HasTheReferenceAtTheOuterSelection()
    {
        CelCheckedExpr checkedExpr = Checked<bool>(EnvProto, "TestAllTypes.NestedEnum.BAR == 1");

        CelCallNode root = Assert.IsType<CelCallNode>(checkedExpr.ParsedExpr.Root);
        CelSelectNode constant = Assert.IsType<CelSelectNode>(root.Arguments[0]);
        CelSelectNode type = Assert.IsType<CelSelectNode>(constant.Operand);
        CelIdentNode package = Assert.IsType<CelIdentNode>(type.Operand);
        CelReference reference = checkedExpr.ReferenceMap[constant.Id];
        Assert.Equal(ProtobufExample.TypeName3 + ".NestedEnum.BAR", reference.Name);
        Assert.True(reference.Value!.Value.TryGetInt64(out long value));
        Assert.Equal(1L, value);
        Assert.Equal(CelType.Int, checkedExpr.TypeMap[constant.Id]);
        Assert.Equal(CelType.Int, checkedExpr.TypeMap[root.Arguments[1].Id]);
        Assert.Equal(CelType.Bool, checkedExpr.TypeMap[root.Id]);
        int[] inner = [type.Id, package.Id];
        Assert.All(inner, id =>
        {
            Assert.False(checkedExpr.TypeMap.ContainsKey(id));
            Assert.False(checkedExpr.ReferenceMap.ContainsKey(id));
        });
    }

    [Fact]
    public static void QualifiedFunction_KeepsTheTreeAndHasTheFullNameInItsReference()
    {
        CelCheckedExpr checkedExpr = Checked<long>(EnvMath, "math.abs(x)");

        CelCallNode root = Assert.IsType<CelCallNode>(checkedExpr.ParsedExpr.Root);
        CelIdentNode math = Assert.IsType<CelIdentNode>(root.Target);
        Assert.Equal(("abs", "math"), (root.Function, math.Name));
        CelReference call = checkedExpr.ReferenceMap[root.Id];
        Assert.Equal("math.abs", call.Name);
        Assert.Equal("math_abs_int", Assert.Single(call.OverloadIds));
        Assert.Equal(CelType.Int, checkedExpr.TypeMap[root.Id]);
        Assert.Equal(CelType.Int, checkedExpr.TypeMap[root.Arguments[0].Id]);
        Assert.False(checkedExpr.TypeMap.ContainsKey(math.Id));
        Assert.False(checkedExpr.ReferenceMap.ContainsKey(math.Id));
    }

    [Fact]
    public static void FunctionInTheContainer_HasTheFullNameInItsReference()
    {
        CelCheckedExpr checkedExpr = Checked<long>(EnvContainer, "f(x)");

        CelCallNode root = Assert.IsType<CelCallNode>(checkedExpr.ParsedExpr.Root);
        Assert.Equal("f", root.Function);
        Assert.Equal("acme.f", checkedExpr.ReferenceMap[root.Id].Name);
    }

    [Fact]
    public static void VariableOfAComprehension_IsALocalAndAFieldSelectionHasNoReference()
    {
        CelCheckedExpr loop = Checked<bool>(Env, "items.all(v, v > 0)");
        CelIdentNode variable = loop.ParsedExpr.Root.DescendantsAndSelf().OfType<CelIdentNode>().First(node => node.Name == "v");
        Assert.Equal("v", loop.ReferenceMap[variable.Id].Name);

        CelCheckedExpr selection = Checked<CelValue>(Env, "m.f");
        Assert.DoesNotContain(selection.ParsedExpr.Root.Id, selection.ReferenceMap.Keys);
    }

    [Fact]
    public static void LeadingDot_StaysInTheReferenceOnlyWhenAComprehensionVariableShadowsTheName()
    {
        CelCheckedExpr shadowed = Checked<bool>(Env, "[5].exists(x, .x > 0)");
        CelIdentNode dotted = shadowed.ParsedExpr.Root.DescendantsAndSelf().OfType<CelIdentNode>().Single(node => node.Name == ".x");
        Assert.Equal(".x", shadowed.ReferenceMap[dotted.Id].Name);

        CelCheckedExpr other = Checked<bool>(Env, "[5].exists(y, .x > 0)");
        CelIdentNode global = other.ParsedExpr.Root.DescendantsAndSelf().OfType<CelIdentNode>().Single(node => node.Name == ".x");
        Assert.Equal("x", other.ReferenceMap[global.Id].Name);

        CelCheckedExpr outside = Checked<bool>(Env, ".x > 0");
        CelIdentNode first = Assert.IsType<CelIdentNode>(Assert.IsType<CelCallNode>(outside.ParsedExpr.Root).Arguments[0]);
        Assert.Equal("x", outside.ReferenceMap[first.Id].Name);
    }

    [Fact]
    public static void MessageLiteral_HasTheFullNameOfItsTypeAndItsFieldsHaveNoReference()
    {
        CelCheckedExpr checkedExpr = Checked<CelValue>(EnvProto, "TestAllTypes{single_int64: 1}");

        CelStructNode root = Assert.IsType<CelStructNode>(checkedExpr.ParsedExpr.Root);
        Assert.Equal(ProtobufExample.TypeName3, checkedExpr.ReferenceMap[root.Id].Name);
        Assert.False(checkedExpr.ReferenceMap.ContainsKey(root.Fields[0].Id));
    }

    [Fact]
    public static void OptionalSelection_HasTheOverloadOfTheSet()
    {
        CelCheckedExpr checkedExpr = Checked<CelValue>(Env, "m.?f");

        CelReference reference = checkedExpr.ReferenceMap[checkedExpr.ParsedExpr.Root.Id];
        Assert.Equal("select_optional_field", Assert.Single(reference.OverloadIds));
    }

    [Fact]
    public static void ExpressionWithErrors_HasNoCheckedExpression()
    {
        CelValidationResult result = Env.Validate<bool>("x > ");

        Assert.False(result.IsValid);
        Assert.Null(result.CheckedExpr);
        Assert.Null(Env.Validate<bool>("name > 1").CheckedExpr);
    }

    [Fact]
    public static void ValidateParsed_KeepsTheTreeThatWasGiven()
    {
        CelParsedExpr tree = Env.Parse("x > 1").GetParsedExprOrThrow();

        CelCheckedExpr checkedExpr = Env.ValidateParsed<bool>(tree).CheckedExpr!;

        Assert.Same(tree, checkedExpr.ParsedExpr);
        Assert.Same(tree.Root, checkedExpr.ParsedExpr.Root);
    }

    [Fact]
    public static void Maps_AreMadeOnceAndShared()
    {
        CelCheckedExpr checkedExpr = Checked<bool>(Env, "x > 1");

        Assert.Same(checkedExpr.TypeMap, checkedExpr.TypeMap);
        Assert.Same(checkedExpr.ReferenceMap, checkedExpr.ReferenceMap);
        IReadOnlyDictionary<int, CelReference> references = checkedExpr.ReferenceMap;
        Assert.Equal(2, references.Count);
    }

    private static CelCheckedExpr Checked<T>(CelEnvironment environment, string text)
    {
        CelValidationResult result = environment.Validate<T>(text);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.CheckedExpr!;
    }
}
