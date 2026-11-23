using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Conformance.CelSpec;

// The plan of a program that has nothing to do with partial or asynchronous evaluation is the plan the library had before they existed:
// no tree kept for the residual expression, no keys of attribute chains, no call that waits. The cases of cel-spec are the sample of the
// language, compiled in the environment of the suite, which has no asynchronous function; the program is not compiled for partial
// evaluation, and its plan is inspected through the internal members of the plan, without reflection.
public sealed class SyncPlanTests
{
    [Fact]
    public void CelSpecCases_CompiledWithoutTheFlag_HaveNoPartialOrAsynchronousPart()
    {
        var withPart = new List<string>();
        int compiledCases = 0;
        foreach (ConformanceCase testCase in CelSpecSuite.ReadCases().Where(testCase => testCase.Check == CheckMode.Check && !testCase.Environment.DisableMacros))
        {
            CelBuildResult built = LibraryEvaluator.Setup(testCase.Environment).Build();
            if (!built.IsSuccess)
            {
                continue;
            }

            CelCompileResult<CelValue> compiled = built.Environment.Compile<CelValue>(testCase.Expression);
            if (!compiled.IsSuccess)
            {
                continue;
            }

            compiledCases++;
            Plan plan = compiled.Program.Plan;
            if (plan.Tree is not null || plan.KeyCount != 0 || plan.HasAsyncCalls)
            {
                withPart.Add(testCase.Name);
            }
        }

        Assert.True(compiledCases > 2_000, $"{compiledCases} cases were compiled");
        Assert.Empty(withPart);
    }

    // The control of the test: the flag is what gives the plan a tree.
    [Fact]
    public void ProgramCompiledWithTheFlag_HasATree()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        CelCompileResult<CelValue> compiled = environment.Compile<CelValue>("1 + 1", new CelCompileOptions { PartialEvaluation = true });
        CelCompileResult<CelValue> plain = environment.Compile<CelValue>("1 + 1");

        Assert.NotNull(compiled.GetProgramOrThrow().Plan.Tree);
        Assert.Null(plain.GetProgramOrThrow().Plan.Tree);
    }
}
