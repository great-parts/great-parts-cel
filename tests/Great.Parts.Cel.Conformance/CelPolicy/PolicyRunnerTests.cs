using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Policy.Yaml;
using Xunit;

namespace Great.Parts.Cel.Conformance.CelPolicy;

// The runner of the CEL Policy suite itself, on synthetic categories: the translation of a configuration, the judgement of a test and
// the match with the skip list (docs/design/conformance.md, "CEL Policy suite").
public sealed class PolicyRunnerTests
{
    private const string Policy = """
        name: synthetic
        rule:
          match:
            - condition: x > 1
              output: "'big'"
            - output: "'small'"
        """;

    private const string Config = """
        variables:
          - name: x
            type_name: int
        """;

    [Fact]
    public void Config_TranslatesNamesTypesAndVersionsToTheJsonForm()
    {
        YamlMapping config = YamlAccess.ReadMapping("config.yaml", """
            name: c
            container: a.b
            extensions:
              - name: strings
                version: latest
              - name: sets
            variables:
              - name: now
                type:
                  type_name: google.protobuf.Timestamp
              - name: m
                type_name: map
                params:
                  - type_name: string
                  - type_name: dyn
            functions:
              - name: f
                overloads:
                  - id: f_string
                    args:
                      - type_name: string
                    return:
                      type_name: bool
            stdlib:
              include_macros:
                - all
            """);

        Assert.Equal(
            """{"name":"c","container":"a.b","extensions":[{"name":"strings","version":"5"},{"name":"sets","version":"0"}],"variables":[{"name":"now","type":"timestamp"},{"name":"m","type":"map<string, dyn>"}],"functions":[{"name":"f","overloads":[{"id":"f_string","args":["string"],"return":"bool"}]}]}""",
            PolicyConfig.ToJsonText(config));
        Assert.Equal(["f_string"], PolicyConfig.OverloadIds(config));
    }

    [Fact]
    public void Judge_PassesAValueAnExpressionAndAnErrorSetAndFailsTheOthers()
    {
        PolicyCategory category = PolicySuite.ReadCategory("synthetic", Policy, Config, "synthetic/tests.yaml", """
            section:
              - name: values
                tests:
                  - name: big
                    input:
                      x:
                        value: 2
                    output:
                      value: big
                  - name: small_by_expression
                    input:
                      x:
                        expr: "1 - 1"
                    output:
                      expr: "'sm' + 'all'"
                  - name: wrong_value
                    input:
                      x:
                        value: 0
                    output:
                      value: big
                  - name: no_error
                    output:
                      error_set:
                        - nope
            """);

        Dictionary<string, bool> passed = Judge(category);

        Assert.Equal(
            new Dictionary<string, bool>
            {
                ["synthetic/values/big"] = true,
                ["synthetic/values/small_by_expression"] = true,
                ["synthetic/values/wrong_value"] = false,
                ["synthetic/values/no_error"] = false,
            },
            passed);
    }

    [Fact]
    public void Judge_ComparesAnErrorSetWithTheMessagesOfTheErrors()
    {
        PolicyCategory category = PolicySuite.ReadCategory("errors", "rule:\n  match:\n    - output: \"nope\"\n", null, "errors/tests.yaml", """
            sections:
              - name: compile
                tests:
                  - name: contained
                    output:
                      error_set:
                        - "undeclared reference to 'nope'"
                  - name: not_contained
                    output:
                      error_set:
                        - "undeclared reference to 'other'"
            """);

        Dictionary<string, bool> passed = Judge(category);

        Assert.Equal(
            new Dictionary<string, bool> { ["errors/compile/contained"] = true, ["errors/compile/not_contained"] = false },
            passed);
    }

    [Fact]
    public void Run_CountsThePassedCasesAndReportsAFailureOutsideTheSkipList()
    {
        PolicyCategory category = PolicySuite.ReadCategory("synthetic", Policy, Config, "synthetic/tests.yaml", """
            section:
              - name: values
                tests:
                  - name: big
                    input:
                      x:
                        value: 2
                    output:
                      value: big
                  - name: wrong_value
                    input:
                      x:
                        value: 0
                    output:
                      value: big
            """);
        SkipList skips = SkipList.ReadPolicy("skips.md", "| Case | Scope | Reason | Reference | Issue |\n|---|---|---|---|---|\n");

        SuiteResult result = PolicySuite.Run([category], skips, RunCell.Current);

        Assert.Equal((1, 2), (result.Passed, result.Total));
        Finding finding = Assert.Single(result.Findings);
        Assert.Equal(EngCodes.Eng801, finding.Code);
        Assert.Equal(("synthetic/tests.yaml", 10, 15),(finding.Path, finding.Line, finding.Column));
        Assert.Contains("synthetic/values/wrong_value", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadCategory_RejectsTwoTestsWithOneName()
    {
        const string tests = """
            section:
              - name: s
                tests:
                  - name: t
                    output:
                      value: a
                  - name: t
                    output:
                      value: a
            """;

        Assert.Throws<InvalidDataException>(() => PolicySuite.ReadCategory("c", Policy, null, "c/tests.yaml", tests));
    }

    private static Dictionary<string, bool> Judge(PolicyCategory category)
    {
        PreparedCategory prepared = PreparedCategory.Prepare(category);
        return category.Tests.ToDictionary(test => test.Name, test => PolicyJudge.Judge(prepared, test).Passed);
    }
}
