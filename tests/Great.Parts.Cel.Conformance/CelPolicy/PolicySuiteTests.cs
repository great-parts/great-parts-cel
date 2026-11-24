using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;

namespace Great.Parts.Cel.Conformance.CelPolicy;

// The CEL Policy suite on the library (docs/design/conformance.md, "CEL Policy suite").
public sealed class PolicySuiteTests
{
    // The number of the tests of tests.yaml of the 41 categories of the bundles at the pinned commit; a lost case shows up here.
    private const int CaseCount = 91;

    // The directories of the copy with a tests.yaml at the pinned commit, in ordinal order; a category that a new commit adds or removes
    // shows up here, and the list is written to be changed together with the commit.
    private static readonly string[] PinnedCategories =
    [
        "agent_tool_execution_governance",
        "aggregate",
        "aggregate_explicit_list_output",
        "aggregate_explicit_optional_none",
        "aggregate_nested_all_rules_fall_through_to_empty",
        "aggregate_nested_explicit_list_double_wrapping",
        "aggregate_nested_multi_branch_match",
        "aggregate_nested_single_conditional_match",
        "aggregate_shadowed_variables",
        "compile_errors/aggregate_false_condition",
        "compile_errors/aggregate_heterogeneous_outputs",
        "compile_errors/aggregate_nested_mixed_semantics",
        "compile_errors/aggregate_subrule_heterogeneous_outputs",
        "compile_errors/aggregate_unreachable_in_nested_first_match",
        "compile_errors/compose_conflicting_output",
        "compile_errors/compose_conflicting_subrule",
        "compile_errors/duplicate_variable",
        "compile_errors/import",
        "compile_errors/incompatible_outputs",
        "compile_errors/syntax",
        "compile_errors/undeclared_reference",
        "compile_errors/unreachable",
        "compile_errors/unreachable_under_unconditional_aggregate_subrule",
        "context_pb",
        "first_match_nested_aggregate",
        "k8s",
        "limits",
        "nested_rule",
        "nested_rule2",
        "nested_rule3",
        "nested_rule4",
        "nested_rule5",
        "nested_rule6",
        "nested_rule7",
        "nested_rules_variable_shadowing",
        "pb",
        "required_labels",
        "restricted_destinations",
        "unconditional_rules",
        "unnest",
        "variable_type_propagation",
    ];

    [Fact]
    public void AllCases_PassOrAreSkipped() =>
        SuiteAssert.PassOrAreSkipped(PolicySuite.Run(RunCell.Current), PolicySuite.Source);

    [Fact]
    public void Source_IsTheShortShaOfThePinnedCommit()
    {
        Assert.Equal("8e4cf0378e3f0d3406b537afbcc6f59562652ad6", PolicySuite.Commit);
        Assert.Equal("cel-policy 8e4cf03", PolicySuite.Source);
    }

    [Fact]
    public void Categories_AreTheDirectoriesWithTests()
    {
        Assert.Equal(41, PolicySuite.Categories.Count);
        Assert.Equal(PinnedCategories, PolicySuite.Categories);
    }

    [Fact]
    public void Categories_HaveTheCasesOfThePinnedCommit()
    {
        Assert.Equal(CaseCount, PolicySuite.ReadCategories().Sum(category => category.Tests.Count));
        Assert.Equal(CaseCount, PolicySuite.Run(RunCell.Current).Total);
    }
}
