using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Policy;
using Great.Parts.Cel.Policy.Yaml;

namespace Great.Parts.Cel.Conformance.CelPolicy;

/// <summary>
/// The CEL Policy suite: the categories of <c>conformance/testdata</c> of the pinned commit of <c>cel-expr/cel-policy</c>, copied to
/// <c>tests/cel-policy</c>, and the skip list <c>CelPolicy/skips.md</c>. A case is a test of <c>tests.yaml</c> of a category. The YAML of
/// the data is read by the YAML reader of the policy package, the same for the policy, the configuration and the tests.
/// </summary>
internal static class PolicySuite
{
    private const string DataDirectory = "tests/cel-policy/";
    private const string VersionPath = DataDirectory + "VERSION";
    private const string SkipListPath = "tests/Great.Parts.Cel.Conformance/CelPolicy/skips.md";

    // The categories are the directories of the copy that hold a tests.yaml, in ordinal order, so that a category that the pinned
    // commit adds is run, and one that it removes is not: the other directories are the configurations of a test of the policy of cel-go.
    private static readonly string[] CategoryNames = [.. EmbeddedData.DirectoriesWithFile(DataDirectory, "tests.yaml")];

    private static readonly CaseEnvironment NoEnvironment = new(string.Empty, [], false);
    private static readonly Expectation NoExpectation = new(ExpectationKind.Value, "none", null, null);

    /// <summary>The full SHA of the pinned commit, the first line of <c>tests/cel-policy/VERSION</c>.</summary>
    internal static string Commit { get; } = EmbeddedData.ReadText(VersionPath).Split('\n')[0].Trim();

    /// <summary>The source of the suite in its summary line: <c>cel-policy</c> and the short SHA of the commit.</summary>
    internal static string Source { get; } = "cel-policy " + Commit[..7];

    /// <summary>The names of the categories of the suite.</summary>
    internal static IReadOnlyList<string> Categories => CategoryNames;

    /// <summary>Reads the categories of the suite from the embedded data.</summary>
    internal static IReadOnlyList<PolicyCategory> ReadCategories() => [.. CategoryNames.Select(ReadCategory)];

    /// <summary>Reads a category of the copy.</summary>
    /// <param name="name">The name of the directory relative to <c>tests/cel-policy</c>.</param>
    internal static PolicyCategory ReadCategory(string name)
    {
        string directory = DataDirectory + name + "/";
        string testsPath = directory + "tests.yaml";
        string configPath = directory + "config.yaml";
        return ReadCategory(
            name,
            EmbeddedData.ReadText(directory + "policy.yaml"),
            EmbeddedData.Exists(configPath) ? EmbeddedData.ReadText(configPath) : null,
            testsPath,
            EmbeddedData.ReadText(testsPath));
    }

    /// <summary>Reads a category from the texts of its files.</summary>
    /// <param name="name">The name of the category.</param>
    /// <param name="policyText">The text of <c>policy.yaml</c>.</param>
    /// <param name="configText">The text of <c>config.yaml</c>, or <see langword="null"/>.</param>
    /// <param name="testsPath">The path of <c>tests.yaml</c> for the positions of the cases.</param>
    /// <param name="testsText">The text of <c>tests.yaml</c>.</param>
    /// <exception cref="InvalidDataException">A file is no YAML of the form the runner reads, or two tests of the category have one name.</exception>
    internal static PolicyCategory ReadCategory(string name, string policyText, string? configText, string testsPath, string testsText)
    {
        YamlMapping? config = configText is null ? null : YamlAccess.ReadMapping(name + "/config.yaml", configText);
        YamlMapping suite = YamlAccess.ReadMapping(testsPath, testsText);
        LineTable lines = new(testsText);
        List<PolicyTest> tests = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        YamlNode? sections = YamlAccess.Find(suite, "section") ?? YamlAccess.Find(suite, "sections");
        foreach (YamlNode sectionNode in YamlAccess.Items(sections))
        {
            YamlMapping section = sectionNode as YamlMapping ?? throw new InvalidDataException($"a section of '{testsPath}' is no mapping");
            string sectionName = YamlAccess.Text(YamlAccess.Find(section, "name")) ?? string.Empty;
            foreach (YamlNode testNode in YamlAccess.Items(YamlAccess.Find(section, "tests")))
            {
                PolicyTest test = ReadTest(name, sectionName, testsPath, lines, testNode as YamlMapping ?? throw new InvalidDataException($"a test of '{testsPath}' is no mapping"));
                if (!names.Add(test.Name))
                {
                    throw new InvalidDataException($"the test '{test.Name}' of '{testsPath}' is not unique");
                }

                tests.Add(test);
            }
        }

        return new PolicyCategory(name, policyText, config, tests);
    }

    /// <summary>Runs every case of the suite and matches the results with the skip list.</summary>
    /// <param name="cell">The cell of the run.</param>
    internal static SuiteResult Run(RunCell cell) =>
        Run(ReadCategories(), SkipList.ReadPolicy(SkipListPath, EmbeddedData.ReadText(SkipListPath)), cell);

    /// <summary>Runs the cases of categories and matches the results with a skip list.</summary>
    /// <param name="categories">The categories.</param>
    /// <param name="skipList">The skip list.</param>
    /// <param name="cell">The cell of the run.</param>
    internal static SuiteResult Run(IReadOnlyList<PolicyCategory> categories, SkipList skipList, RunCell cell)
    {
        Dictionary<string, (Lazy<PreparedCategory> Category, PolicyTest Test)> byName = new(StringComparer.Ordinal);
        List<ConformanceCase> cases = [];
        foreach (PolicyCategory category in categories)
        {
            Lazy<PreparedCategory> prepared = new(() => PreparedCategory.Prepare(category));
            foreach (PolicyTest test in category.Tests)
            {
                byName.Add(test.Name, (prepared, test));
                cases.Add(new ConformanceCase(test.Name, test.Path, test.Line, test.Column, string.Empty, NoEnvironment, new Dictionary<string, ExprValue>(), CheckMode.Check, NoExpectation));
            }
        }

        return SuiteRun.Run(cases, skipList, testCase => PolicyJudge.Judge(byName[testCase.Name].Category.Value, byName[testCase.Name].Test), cell);
    }

    private static PolicyTest ReadTest(string category, string section, string path, LineTable lines, YamlMapping test)
    {
        YamlNode? nameNode = YamlAccess.Find(test, "name");
        string name = YamlAccess.Text(nameNode) ?? throw new InvalidDataException($"a test of '{path}' has no name");
        (int line, int column) = lines.Locate(nameNode!.Start);
        List<KeyValuePair<string, PolicyInput>> inputs = [];
        if (YamlAccess.Find(test, "input") is YamlMapping inputMapping)
        {
            foreach (YamlPair pair in inputMapping.Pairs)
            {
                YamlMapping input = pair.Value as YamlMapping ?? throw new InvalidDataException($"an input of the test '{name}' of '{path}' is no mapping");
                inputs.Add(KeyValuePair.Create(
                    YamlAccess.Text(pair.Key) ?? throw new InvalidDataException($"an input of the test '{name}' of '{path}' has no name"),
                    new PolicyInput(YamlAccess.Text(YamlAccess.Find(input, "expr")), YamlAccess.Find(input, "value"))));
            }
        }

        return new PolicyTest(
            $"{category}/{section}/{name}",
            path,
            line,
            column,
            inputs,
            YamlAccess.Text(YamlAccess.Find(test, "context_expr")),
            ReadOutput(test, path, name));
    }

    private static PolicyOutput ReadOutput(YamlMapping test, string path, string name)
    {
        YamlMapping output = YamlAccess.Find(test, "output") as YamlMapping ?? throw new InvalidDataException($"the test '{name}' of '{path}' has no output");
        if (YamlAccess.Find(output, "value") is { } value)
        {
            return new PolicyOutput(PolicyOutputKind.Value, string.Empty, value, []);
        }

        if (YamlAccess.Text(YamlAccess.Find(output, "expr")) is { } expression)
        {
            return new PolicyOutput(PolicyOutputKind.Expression, expression, null, []);
        }

        if (YamlAccess.Find(output, "error_set") is { } errors)
        {
            return new PolicyOutput(PolicyOutputKind.ErrorSet, string.Empty, null, [.. YamlAccess.Items(errors).Select(error => YamlAccess.Text(error) ?? string.Empty)]);
        }

        return new PolicyOutput(PolicyOutputKind.Other, output.Pairs.Count == 0 ? string.Empty : YamlAccess.Text(output.Pairs[0].Key) ?? string.Empty, null, []);
    }
}
