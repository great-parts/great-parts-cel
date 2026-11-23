using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Syntax;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// What the examples of CompileParsed leave out: a tree with a very large number of nodes that the environment does not accept
// reports a bounded number of errors, however many nodes it has.
[Trait("Requirement", "F-20")]
public sealed class CompileParsedLimitsTests
{
    private const int Elements = 10_000;
    private const int ErrorLimit = 100;

    [Fact]
    public static void ListOfTenThousandOptionalElements_ReportsOnlyTheFirstHundredErrors()
    {
        var factory = new CelExprFactory();
        CelExprNode[] elements = new CelExprNode[Elements];
        int[] optional = new int[Elements];
        for (int i = 0; i < Elements; i++)
        {
            elements[i] = factory.Constant(CelValue.FromInt64(i));
            optional[i] = i;
        }

        CelParsedExpr tree = factory.Build(factory.List(elements, optional));

        CelCompileResult<CelValue> result = TreeEnvironments.EnvNoOptional.CompileParsed<CelValue>(tree);

        Assert.Equal(ErrorLimit, result.Errors.Length);
        Assert.All(result.Errors, error => Assert.Equal((CelErrorCode.SyntaxNotEnabled, CelErrorStage.Parse), (error.Code, error.Stage)));
        Assert.DoesNotContain(TreeEnvironments.Env.CompileParsed<CelValue>(tree).Errors, error => error.Code == CelErrorCode.SyntaxNotEnabled);
    }
}
