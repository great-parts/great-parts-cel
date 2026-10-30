using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Great.Parts.Cel.Eng.Checks;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Checks;

// `eng/checks mutation` (docs/design/eng-mutate.md, "mutation"). The reports have the form of the real report of Stryker.NET 5.0.0:
// the keys of the files are absolute paths of the container ("/repo/..."), "projectRoot" is the directory of the project, "thresholds"
// has "high" and "low" without "break". A test builds one from the mutants it names, or uses an excerpt of a real report. The command
// runs through Program, so the options and the exit codes are the ones of the command line. The changed files are those of a
// temporary git repository, where the committed files are the base and the files written afterwards are the change.
public sealed class MutationTests
{
    private const string ProjectDirectory = "src/Great.Parts.Cel";
    private const string Project = ProjectDirectory + "/Great.Parts.Cel.csproj";
    private const string Arithmetic = ProjectDirectory + "/Library/IntArithmetic.cs";
    private const string Other = ProjectDirectory + "/Library/Other.cs";
    private const string SinceReason = "Removed by since filter";
    private const string MutateReason = "Removed by mutate filter";
    private const string ReportFile = "artifacts/mutate/reports/mutation-report.json";
    private const string ConfigFile = "stryker-config.json";

    // The configuration of the main run; "break" is at line 11, column 7.
    private const string MainConfig = """
        {
          "stryker-config": {
            "project": "src/Great.Parts.Cel/Great.Parts.Cel.csproj",
            "since": {
              "enabled": true,
              "target": "main"
            },
            "thresholds": {
              "high": 80,
              "low": 80,
              "break": 80
            }
          }
        }
        """;

    // The configuration of the control set: no since, "break" at line 10, column 7.
    private const string ControlConfig = """
        {
          "stryker-config": {
            "project": "eng/checks/Great.Parts.Cel.Eng.Checks.csproj",
            "since": {
              "enabled": false
            },
            "thresholds": {
              "high": 100,
              "low": 100,
              "break": 100
            }
          }
        }
        """;

    // The same without since: the changed files are not read from git.
    private static readonly string NoSince = MainConfig.Replace("\"enabled\": true", "\"enabled\": false", StringComparison.Ordinal);

    // A real report of the control set, cut to one file and without the lists of tests: five killed mutants, one ignored by a filter,
    // one that does not compile. The text is as the tool wrote it: characters such as < are escaped as in JSON, and the source file
    // has its own line endings.
    private const string RealReport = """"
        {"files":{"/repo/eng/checks/Commands/RatchetCommand.cs":{"language":"cs","source":"using System.Collections.Generic;\r\nusing Great.Parts.Cel.Eng.Findings;\r\n\r\nnamespace Great.Parts.Cel.Eng.Checks;\r\n\r\n/// <summary>\r\n/// Compares the numbers of the repository with the baseline <c>eng/ratchet.json</c>, so a number grows only by a change of the\r\n/// baseline in the same change set and a baseline that is too high is lowered\r\n/// (<c>docs/design/eng-checks-source.md</c>, \"ratchet\"). It uses the scans of <c>suppressions</c> and <c>duplicates</c> of the same\r\n/// run, and makes them when they are not there. The file is read by <see cref=\"RatchetFile\"/>, the numbers are compared by\r\n/// <see cref=\"RatchetComparison\"/>.\r\n/// </summary>\r\ninternal sealed class RatchetCommand : ICheckCommand\r\n{\r\n    /// <inheritdoc/>\r\n    public string Name => \"ratchet\";\r\n\r\n    /// <inheritdoc/>\r\n    public IEnumerable<Finding> Run(CheckContext context)\r\n    {\r\n        RatchetFile baseline = RatchetFile.Of(context);\r\n        return baseline.Problems.Count > 0 ? baseline.Problems : RatchetComparison.Compare(context, baseline);\r\n    }\r\n}\r\n","mutants":[{"id":"42","mutatorName":"String mutation","replacement":"\"\"","location":{"start":{"line":16,"column":27},"end":{"line":16,"column":36}},"status":"Killed","static":false},{"id":"43","mutatorName":"Block removal mutation","replacement":"{}","location":{"start":{"line":20,"column":5},"end":{"line":23,"column":6}},"status":"Ignored","statusReason":"Removed by block already covered filter","static":false},{"id":"44","mutatorName":"Conditional (true) mutation","replacement":"(true?baseline.Problems :RatchetComparison.Compare(context, baseline))","location":{"start":{"line":22,"column":16},"end":{"line":22,"column":110}},"status":"Killed","static":false},{"id":"45","mutatorName":"Conditional (false) mutation","replacement":"(false?baseline.Problems :RatchetComparison.Compare(context, baseline))","location":{"start":{"line":22,"column":16},"end":{"line":22,"column":110}},"status":"Killed","static":false},{"id":"46","mutatorName":"Equality mutation","replacement":"baseline.Problems.Count < 0","location":{"start":{"line":22,"column":16},"end":{"line":22,"column":43}},"status":"Killed","static":false},{"id":"47","mutatorName":"Equality mutation","replacement":"baseline.Problems.Count >= 0","location":{"start":{"line":22,"column":16},"end":{"line":22,"column":43}},"status":"Killed","static":false},{"id":"48","mutatorName":"Linq method mutation (Count() to Sum())","replacement":"baseline.Problems.Sum","location":{"start":{"line":22,"column":16},"end":{"line":22,"column":39}},"status":"CompileError","statusReason":"Mutant caused compile errors","static":false}]}},"projectRoot":"/repo/eng/checks","schemaVersion":"2","thresholds":{"high":100,"low":100}}
        """";

    [Fact]
    public void MutationCommand_IsInTheRegistryAndTakesNoArgument()
    {
        ICheckCommand command = Assert.Single(CommandRegistry.All, candidate => candidate.Name == "mutation");

        Assert.Null(command.ArgumentName);
    }

    [Fact]
    public void RealReportOfTheControlSet_HasNoFindings()
    {
        using TempDirectory root = new();

        Result result = Check(root, RealReport, ControlConfig, "eng/stryker-control.json");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void RealReportOfTheControlSet_ASurvivedMutant_IsEng501AndTheScoreIsEng503AtTheBreakOfTheControlConfiguration()
    {
        using TempDirectory root = new();
        string report = RealReport.Replace("\"status\":\"Killed\"", "\"status\":\"Survived\"", StringComparison.Ordinal);

        Result result = Check(root, report, ControlConfig, "eng/stryker-control.json");

        AssertErrors(
            result,
            Error(EngCodes.Eng501, "eng/checks/Commands/RatchetCommand.cs", 16, 27, "survived mutant (String mutation): '\"ratchet\"' -> '\"\"'"),
            Error(EngCodes.Eng501, "eng/checks/Commands/RatchetCommand.cs", 22, 16, "survived mutant (Conditional (true) mutation): 'baseline.Problems.Count > 0 ? baseline.Problems : RatchetComparison.Compare(context, baseline)' -> '(true?baseline.Problems :RatchetComparison.Compare(context, baseline))'"),
            Error(EngCodes.Eng501, "eng/checks/Commands/RatchetCommand.cs", 22, 16, "survived mutant (Conditional (false) mutation): 'baseline.Problems.Count > 0 ? baseline.Problems : RatchetComparison.Compare(context, baseline)' -> '(false?baseline.Problems :RatchetComparison.Compare(context, baseline))'"),
            Error(EngCodes.Eng501, "eng/checks/Commands/RatchetCommand.cs", 22, 16, "survived mutant (Equality mutation): 'baseline.Problems.Count > 0' -> 'baseline.Problems.Count < 0'"),
            Error(EngCodes.Eng501, "eng/checks/Commands/RatchetCommand.cs", 22, 16, "survived mutant (Equality mutation): 'baseline.Problems.Count > 0' -> 'baseline.Problems.Count >= 0'"),
            Error(EngCodes.Eng503, "eng/stryker-control.json", 10, 7, "mutation score 0 % is below the threshold 100 % (0 of 5 mutants detected)"));
    }

    [Fact]
    public void Eng501AndEng502_ScoreBelowBreak_AreErrorsAtTheMutantAndEng503IsAtTheBreakLine()
    {
        using TempDirectory root = new();
        string report = Report((Arithmetic, Source(), [.. Many("Killed", 6), Survived, NoCoverage]));

        Result result = Check(root, report, NoSince, ConfigFile);

        AssertErrors(
            result,
            Error(EngCodes.Eng501, Arithmetic, 57, 24, "survived mutant (Arithmetic): 'a + b' -> 'a - b'"),
            Error(EngCodes.Eng502, Arithmetic, 58, 16, "mutant without coverage (Arithmetic): 'a * b' -> 'a / b'"),
            Error(EngCodes.Eng503, ConfigFile, 11, 7, "mutation score 75 % is below the threshold 80 % (6 of 8 mutants detected)"));
    }

    [Fact]
    public void Eng501AndEng502_ScoreAtBreak_AreWarningsAndThereIsNoEng503()
    {
        using TempDirectory root = new();
        string report = Report((Arithmetic, Source(), [.. Many("Killed", 8), Survived, NoCoverage]));

        Result result = Check(root, report, NoSince, ConfigFile);

        Assert.Equal(string.Empty, result.Error);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            [
                $"{Arithmetic}(57,24): warning ENG501: survived mutant (Arithmetic): 'a + b' -> 'a - b'",
                $"{Arithmetic}(58,16): warning ENG502: mutant without coverage (Arithmetic): 'a * b' -> 'a / b'",
            ],
            result.Lines);
    }

    [Fact]
    public void Score_CountsKilledAndTimeoutAsDetectedAndLeavesOutTheOtherStatuses()
    {
        using TempDirectory root = new();
        MutantSpec[] uncounted = [.. Many("CompileError", 9, "Mutant caused compile errors"), .. Many("RuntimeError", 3), .. Many("Ignored", 9, "Removed by block already covered filter"), .. Many("Pending", 2)];
        string report = Report((Arithmetic, Source(), [.. Many("Killed", 3), .. Many("Timeout", 1), Survived, .. uncounted]));

        Result result = Check(root, report, NoSince, ConfigFile);

        // Four of five are detected, 80 %: Timeout is detected, the others are not counted.
        Assert.Equal(0, result.ExitCode);
        Assert.Equal([$"{Arithmetic}(57,24): warning ENG501: survived mutant (Arithmetic): 'a + b' -> 'a - b'"], result.Lines);
    }

    [Fact]
    public void Score_IsRoundedDownInTheMessage()
    {
        using TempDirectory root = new();
        string report = Report((Arithmetic, Source(), [.. Many("Killed", 2), Survived]));

        Result result = Check(root, report, NoSince, ConfigFile);

        // Two of three is 66.66...: the message shows 66.66, not 66.67.
        Assert.Contains("mutation score 66.66 % is below the threshold 80 % (2 of 3 mutants detected)", result.Lines.Single(line => line.Contains("ENG503", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void Break_IsTheOneOfTheConfigurationFile()
    {
        using TempDirectory root = new();
        string report = Report((Arithmetic, Source(), [.. Many("Killed", 17), Survived, Survived, Survived]));

        Result lower = Check(root, report, NoSince.Replace("\"break\": 80", "\"break\": 85", StringComparison.Ordinal), ConfigFile);

        // 17 of 20 is 85 %: at break, so warnings; with break 86 it is below.
        Assert.Equal(0, lower.ExitCode);
        Result higher = Check(root, report, NoSince.Replace("\"break\": 80", "\"break\": 86", StringComparison.Ordinal), ConfigFile);
        Assert.Equal(1, higher.ExitCode);
        Assert.Contains("ENG503: mutation score 85 % is below the threshold 86 % (17 of 20 mutants detected)", higher.Lines.Single(line => line.Contains("ENG503", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationWithoutBreak_HasNoThresholdSoEveryMutantIsAWarning()
    {
        using TempDirectory root = new();
        string report = Report((Arithmetic, Source(), [Survived, NoCoverage]));
        string config = NoSince.Replace("\"break\": 80", "\"other\": 80", StringComparison.Ordinal);

        Result result = Check(root, report, config, ConfigFile);

        Assert.Equal(0, result.ExitCode);
        Assert.All(result.Lines, line => Assert.Contains("warning ENG50", line, StringComparison.Ordinal));
        Assert.Equal(2, result.Lines.Length);
    }

    [Fact]
    public void ConfigurationWithoutTheSection_HasItsSettingsAtTheTopOfTheFileAndCommentsAreAllowed()
    {
        using TempDirectory root = new();
        string report = Report((Arithmetic, Source(), [Survived]));
        string config = "{\n  // the thresholds\n  \"project\": \"src/Great.Parts.Cel/Great.Parts.Cel.csproj\",\n  \"since\": { \"enabled\": false, },\n  \"test-projects\": [\"a\", { \"break\": 1 }],\n  \"thresholds\": {\n    \"break\": 50\n  }\n}\n";

        Result result = Check(root, report, config, ConfigFile);

        AssertErrors(
            result,
            Error(EngCodes.Eng501, Arithmetic, 57, 24, "survived mutant (Arithmetic): 'a + b' -> 'a - b'"),
            Error(EngCodes.Eng503, ConfigFile, 7, 5, "mutation score 0 % is below the threshold 50 % (0 of 1 mutants detected)"));
    }

    [Fact]
    public void ReportWithoutCountedMutants_HasNoScoreAndNoEng503()
    {
        using TempDirectory root = new();
        string report = Report((Arithmetic, Source(), [.. Many("Ignored", 2, SinceReason), .. Many("CompileError", 1, "Mutant caused compile errors")]));

        Result result = Check(root, report, MainConfig.Replace("true", "false", StringComparison.Ordinal), ConfigFile);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void MutantOfSeveralLines_HasItsOriginalAndReplacementOnOneLine()
    {
        using TempDirectory root = new();
        MutantSpec mutant = new("Survived", Mutator: "Block removal", Replacement: "{\r\n}", Line: 3, Column: 4, EndLine: 5, EndColumn: 5);
        string report = Report((Arithmetic, Source(), [mutant]));

        Result result = Check(root, report, MainConfig.Replace("true", "false", StringComparison.Ordinal), ConfigFile);

        AssertErrors(
            result,
            Error(EngCodes.Eng501, Arithmetic, 3, 4, "survived mutant (Block removal): 'line 3 // line 4 // l' -> '{ }'"),
            Error(EngCodes.Eng503, ConfigFile, 11, 7, "mutation score 0 % is below the threshold 80 % (0 of 1 mutants detected)"));
    }

    [Fact]
    public void PlaceOutsideTheSource_IsCutToTheSource()
    {
        using TempDirectory root = new();
        MutantSpec mutant = new("Survived", Line: 70, Column: 99, EndLine: 80, EndColumn: 99);
        string report = Report((Arithmetic, Source(), [mutant, Killed]));

        Result result = Check(root, report, MainConfig.Replace("\"break\": 80", "\"break\": 10", StringComparison.Ordinal).Replace("true", "false", StringComparison.Ordinal), ConfigFile);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal([$"{Arithmetic}(70,99): warning ENG501: survived mutant (Arithmetic): '' -> 'a - b'"], result.Lines);
    }

    [Fact]
    public void KeysOfTheReport_AreMadeRelativeToTheRepositoryRoot()
    {
        using TempDirectory root = new();
        string hostRoot = root.Path.Replace('\\', '/');
        string noSince = MainConfig.Replace("true", "false", StringComparison.Ordinal);
        string container = Report((Arithmetic, Source(), [Survived]));
        string host = container.Replace("/repo/", hostRoot + "/", StringComparison.Ordinal);
        string projectRoot = "\"projectRoot\":\"";
        string hostWithoutProjectRoot = host.Replace(projectRoot + hostRoot + "/" + ProjectDirectory + "\",", string.Empty, StringComparison.Ordinal);
        string strange = container.Replace(projectRoot + "/repo/" + ProjectDirectory, projectRoot + "/other", StringComparison.Ordinal).Replace("/repo/", "/elsewhere/", StringComparison.Ordinal);
        string configWithoutProject = noSince.Replace("\"project\": \"" + Project + "\",", string.Empty, StringComparison.Ordinal);

        // The root of the container is found from the project directory of the report and of the configuration.
        Assert.StartsWith(Arithmetic + "(57,24)", Check(root, container, noSince, ConfigFile).Lines[0], StringComparison.Ordinal);
        Assert.StartsWith(Arithmetic + "(57,24)", Check(root, host, noSince, ConfigFile).Lines[0], StringComparison.Ordinal);
        // Without it the root of the run is the root of the paths.
        Assert.StartsWith(Arithmetic + "(57,24)", Check(root, hostWithoutProjectRoot, noSince, ConfigFile).Lines[0], StringComparison.Ordinal);
        Assert.StartsWith(Arithmetic + "(57,24)", Check(root, host, configWithoutProject, ConfigFile).Lines[0], StringComparison.Ordinal);
        // A path under none of the roots stays as the report gives it.
        Assert.StartsWith("/elsewhere/" + Arithmetic + "(57,24)", Check(root, strange, noSince, ConfigFile).Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void FindingsOfSeveralFiles_AreSortedByPathAndPlace()
    {
        using TempDirectory root = new();
        string report = Report((Other, Source(), [Survived]), (Arithmetic, Source(), [NoCoverage, Survived]));

        Result result = Check(root, report, MainConfig.Replace("true", "false", StringComparison.Ordinal).Replace("\"break\": 80", "\"break\": 0", StringComparison.Ordinal), ConfigFile);

        Assert.Equal(
            [$"{Arithmetic}(57,24)", $"{Arithmetic}(58,16)", $"{Other}(57,24)"],
            result.Lines.Select(line => Finding.TryParse(line, out Finding finding) ? $"{finding.Path}({finding.Line},{finding.Column})" : line));
    }

    // ---- ENG504: the changed files ----

    [Fact]
    public void Eng504_ChangedFileWhoseMutantsAreRemovedBySince_IsAtTheFirstPlaceOfTheFile()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic, Other);
        Change(root, Arithmetic);
        string report = Report((Arithmetic, Source(), [.. Many("Ignored", 3, SinceReason)]), (Other, Source(), [Killed]));

        Result result = Check(root, report, MainConfig, ConfigFile);

        AssertErrors(result, Error(EngCodes.Eng504, Arithmetic, 1, 1, "changed file was not mutated: none of its mutants was counted (Removed by since filter)"));
    }

    [Fact]
    public void Eng504_ChangedFileWhoseMutantsAreRemovedByMutate_IsAFindingToo()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic);
        Change(root, Arithmetic);
        string report = Report((Arithmetic, Source(), [.. Many("Ignored", 1, MutateReason), .. Many("Ignored", 1, SinceReason), .. Many("CompileError", 1, "Mutant caused compile errors")]));

        Result result = Check(root, report, MainConfig, ConfigFile);

        AssertErrors(result, Error(EngCodes.Eng504, Arithmetic, 1, 1, "changed file was not mutated: none of its mutants was counted (Removed by mutate filter, Removed by since filter)"));
    }

    [Fact]
    public void Eng504_ChangedFileThatTheReportDoesNotHave_IsAFinding()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic, Other);
        Change(root, Other);
        string report = Report((Arithmetic, Source(), [Killed]));

        Result result = Check(root, report, MainConfig, ConfigFile);

        AssertErrors(result, Error(EngCodes.Eng504, Other, 1, 1, "changed file was not mutated: it is not in the mutation report"));
    }

    [Fact]
    public void Eng504_ChangedFileWithACountedMutant_IsNoFindingEvenWhenOthersAreRemovedAndItsPathMatchesGit()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic);
        Change(root, Arithmetic);
        string report = Report((Arithmetic, Source(), [Killed, .. Many("Ignored", 4, SinceReason)]));

        Result result = Check(root, report, MainConfig, ConfigFile);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Theory]
    [InlineData("NoMutants")]
    [InlineData("CompileErrors")]
    [InlineData("EquivalentComment")]
    public static void Eng504_ChangedFileWithoutCountedMutantsForAnotherReason_IsNoFinding(string kind)
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic);
        Change(root, Arithmetic);
        MutantSpec[] mutants = kind switch
        {
            "NoMutants" => [],
            "CompileErrors" => Many("CompileError", 2, "Mutant caused compile errors"),
            _ => Many("Ignored", 2, "Removed by comment"),
        };

        Result result = Check(root, Report((Arithmetic, Source(), mutants)), MainConfig, ConfigFile);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Eng504_OnlyTheChangedCSharpFilesOfTheProjectCount()
    {
        using TempDirectory root = new();
        string[] unrelated = [ProjectDirectory + "/notes.md", "tests/Great.Parts.Cel.Tests/FooTests.cs", "src/Great.Parts.Cel.Other/Library/Other.cs", ProjectDirectory + "/Library/Gone.cs"];
        Repository(root, [Arithmetic, .. unrelated]);
        Change(root, [Arithmetic, .. unrelated.Take(3)]);
        File.Delete(Path.Combine(root.Path, unrelated[3]));

        Result result = Check(root, Report((Arithmetic, Source(), [Killed])), MainConfig, ConfigFile);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Eng504_ProjectInTheRootOfTheRepository_CountsEveryChangedCSharpFile()
    {
        using TempDirectory root = new();
        Repository(root, "Library/Arithmetic.cs", "src/Elsewhere.cs");
        Change(root, "Library/Arithmetic.cs", "src/Elsewhere.cs");
        string config = MainConfig.Replace(Project, "Great.Parts.Cel.csproj", StringComparison.Ordinal);

        Result result = Check(root, Report(("Library/Arithmetic.cs", Source(), [Killed])).Replace("/repo/src/Great.Parts.Cel\"", "/repo\"", StringComparison.Ordinal), config, ConfigFile);

        AssertErrors(result, Error(EngCodes.Eng504, "src/Elsewhere.cs", 1, 1, "changed file was not mutated: it is not in the mutation report"));
    }

    // Stryker.NET enables since for an object "since" without "enabled", so the check reads the changed files too.
    [Fact]
    public void Eng504_SinceObjectWithoutEnabled_EnablesSinceAsInTheTool()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic, Other);
        Change(root, Other);
        string config = MainConfig.Replace("\"enabled\": true,", string.Empty, StringComparison.Ordinal);

        Result result = Check(root, Report((Arithmetic, Source(), [Killed])), config, ConfigFile);

        Assert.DoesNotContain("enabled", config, StringComparison.Ordinal);
        AssertErrors(result, Error(EngCodes.Eng504, Other, 1, 1, "changed file was not mutated: it is not in the mutation report"));
    }

    [Fact]
    public void Eng504_WithoutSince_ThereAreNoChangedFilesAndGitIsNotUsed()
    {
        using TempDirectory root = new();
        string config = MainConfig.Replace("\"enabled\": true", "\"enabled\": false", StringComparison.Ordinal);

        // The directory is not a repository: with since it would be an input that cannot be read.
        Result result = Check(root, Report((Other, Source(), [Killed])), config, ConfigFile);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void ChangeWithoutAChangedFileOfTheProject_HasNoScoreAndNoFinding()
    {
        using TempDirectory root = new();
        Repository(root, "docs/guide.md");
        Change(root, "docs/guide.md");
        string report = Report((Arithmetic, Source(), [.. Many("Ignored", 5, SinceReason), .. Many("CompileError", 2, "Mutant caused compile errors")]));

        Result result = Check(root, report, MainConfig, ConfigFile);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void MissingReport_WithSince_IsAReportWithoutMutantsAndEveryChangedFileIsEng504()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic, Other);
        Change(root, Arithmetic, Other);

        Result result = CheckWithoutReport(root, MainConfig);

        AssertErrors(
            result,
            Error(EngCodes.Eng504, Arithmetic, 1, 1, "changed file was not mutated: it is not in the mutation report"),
            Error(EngCodes.Eng504, Other, 1, 1, "changed file was not mutated: it is not in the mutation report"));
    }

    [Fact]
    public void MissingReport_WithSinceAndNothingChanged_HasNoFinding()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic);

        Result result = CheckWithoutReport(root, MainConfig);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void MissingReport_WithoutSince_IsExitCodeTwoAndNamesTheFile()
    {
        using TempDirectory root = new();

        Result result = CheckWithoutReport(root, ControlConfig);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains("mutation-report.json", result.Error, StringComparison.Ordinal);
        Assert.Contains("cannot be read", result.Error, StringComparison.Ordinal);
    }

    // ---- inputs that are not there ----

    [Theory]
    [InlineData("--config", "--report")]
    [InlineData("--report", "--config")]
    public static void MissingOption_IsExitCodeTwoAndNamesTheOption(string given, string missing)
    {
        using TempDirectory root = new();

        Result result = Run(root, "mutation", given, "given.json");

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains(missing, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void NoOption_IsExitCodeTwo()
    {
        using TempDirectory root = new();

        Result result = Run(root, ["mutation"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--report", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationThatDoesNotExist_IsExitCodeTwoAndNamesTheFile()
    {
        using TempDirectory root = new();
        root.Write(ReportFile, Report((Arithmetic, Source(), [Killed])));

        Result result = Run(root, ["mutation", "--report", ReportFile, "--config", "missing-config.json"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains("missing-config.json", result.Error, StringComparison.Ordinal);
        Assert.Contains("cannot be read", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{ \"stryker-config\": { \"since\": { \"enabled\": \"yes\" } } }")]
    [InlineData("{ \"stryker-config\": { \"since\": { \"enabled\": true }, \"thresholds\": { \"break\": \"80\" } } }")]
    [InlineData("{ \"stryker-config\": { \"project\": 5 } }")]
    public static void ConfigurationThatIsNotAValidConfiguration_IsExitCodeTwoAndNamesTheFile(string content)
    {
        using TempDirectory root = new();
        root.Write(ReportFile, Report((Arithmetic, Source(), [Killed])));

        Result result = Check(root, null, content, ConfigFile);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains(ConfigFile, result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{ \"files\": [] }")]
    [InlineData("{ \"files\": { \"/repo/a.cs\": { \"mutants\": [ { \"status\": \"Killed\" } ] } } }")]
    [InlineData("{ \"files\": { \"/repo/a.cs\": { \"mutants\": [ { \"status\": 5, \"location\": { \"start\": { \"line\": 1, \"column\": 1 }, \"end\": { \"line\": 1, \"column\": 1 } } } ] } } }")]
    [InlineData("{ \"files\": { \"/repo/a.cs\": { \"mutants\": [ { \"status\": \"Killed\", \"location\": { \"start\": { \"line\": \"1\", \"column\": 1 }, \"end\": { \"line\": 1, \"column\": 1 } } } ] } } }")]
    public static void ReportThatIsNotAMutationReport_IsExitCodeTwoAndNamesTheFile(string content)
    {
        using TempDirectory root = new();

        Result result = Check(root, content, ControlConfig, ConfigFile);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains("mutation-report.json", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Since_ConfigurationWithoutAProject_IsExitCodeTwo()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic);

        Result result = Check(root, Report((Arithmetic, Source(), [Killed])), MainConfig.Replace("\"project\": \"src/Great.Parts.Cel/Great.Parts.Cel.csproj\",", string.Empty, StringComparison.Ordinal), ConfigFile);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("names no project", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Since_DirectoryThatIsNotARepository_IsExitCodeTwo()
    {
        using TempDirectory root = new();

        Result result = Check(root, Report((Arithmetic, Source(), [Killed])), MainConfig, ConfigFile);

        Assert.Equal(2, result.ExitCode);
        Assert.Empty(result.Lines);
        Assert.Contains("cannot be listed by git diff", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Since_RepositoryWithoutTheBranchMain_IsExitCodeTwo()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic);
        Assert.True(Git.Run(root.Path, "branch", "-m", "trunk").Succeeded);

        Result result = Check(root, Report((Arithmetic, Source(), [Killed])), MainConfig, ConfigFile);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("cannot be listed by git diff", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativePathsOfTheReportAndTheConfiguration_AreTakenFromTheWorkingDirectory()
    {
        using TempDirectory root = new();
        root.Write("work/report.json", Report((Arithmetic, Source(), [Survived])));
        root.Write("work/config.json", MainConfig.Replace("true", "false", StringComparison.Ordinal));
        using StringWriter output = new() { NewLine = "\n" };
        using StringWriter error = new() { NewLine = "\n" };

        int exitCode = Program.Run(
            ["mutation", "--report", "report.json", "--config", "config.json", "--root", root.Path],
            [new MutationCommand()],
            Path.Combine(root.Path, "work"),
            output,
            error);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("ENG501", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("work/config.json(11,7): error ENG503", output.ToString(), StringComparison.Ordinal);
    }

    // ---- --since: the commit from which the changes are counted ----

    // `--since:<commit>` enables since in Stryker.NET whatever the configuration says, so the changed files are checked too.
    [Fact]
    public void SinceOption_ConfigurationWithoutSince_ChecksTheChangedFilesAsTheToolMutatesThem()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic, Other);
        string baseCommit = Git.Run(root.Path, "rev-parse", "HEAD").Output.Trim();
        Change(root, Other);
        root.Write(ReportFile, Report((Arithmetic, Source(), [Killed])));
        root.Write(ConfigFile, NoSince);

        Result result = Run(root, "mutation", "--report", ReportFile, "--config", ConfigFile, "--since", baseCommit);

        AssertErrors(result, Error(EngCodes.Eng504, Other, 1, 1, "changed file was not mutated: it is not in the mutation report"));
    }

    // Without the report, --since counts as since: every changed file is a finding, not a missing input.
    [Fact]
    public void SinceOption_ConfigurationWithoutSinceAndNoReport_EveryChangedFileIsAFinding()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic);
        string baseCommit = Git.Run(root.Path, "rev-parse", "HEAD").Output.Trim();
        Change(root, Arithmetic);
        root.Write(ConfigFile, NoSince);

        Result result = Run(root, "mutation", "--report", ReportFile, "--config", ConfigFile, "--since", baseCommit);

        AssertErrors(result, Error(EngCodes.Eng504, Arithmetic, 1, 1, "changed file was not mutated: it is not in the mutation report"));
    }

    [Fact]
    public void SinceOption_CommitInsteadOfMain_ListsTheFilesChangedAgainstIt()
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic, Other);
        string baseCommit = Git.Run(root.Path, "rev-parse", "HEAD").Output.Trim();
        Change(root, Other);
        Assert.True(Git.Run(root.Path, "add", "--all").Succeeded);
        Assert.True(Git.Run(root.Path, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "change").Succeeded);
        string report = Report((Arithmetic, Source(), [Killed]));

        Result againstMain = Check(root, report, MainConfig, ConfigFile);
        Result againstBase = Run(root, "mutation", "--report", ReportFile, "--config", ConfigFile, "--since", baseCommit);

        Assert.Equal((0, string.Empty), (againstMain.ExitCode, againstMain.Error));
        Assert.Empty(againstMain.Lines);
        AssertErrors(againstBase, Error(EngCodes.Eng504, Other, 1, 1, "changed file was not mutated: it is not in the mutation report"));
    }

    [Theory]
    [InlineData("no-such-commit", "cannot be listed by git diff")]
    [InlineData("--output=x", "is not a commit")]
    public static void SinceOption_ValueThatIsNotACommit_IsExitCodeTwo(string value, string message)
    {
        using TempDirectory root = new();
        Repository(root, Arithmetic);
        Check(root, Report((Arithmetic, Source(), [Killed])), MainConfig, ConfigFile);

        Result result = Run(root, "mutation", "--report", ReportFile, "--config", ConfigFile, "--since", value);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(message, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void SinceOption_WithoutAValue_IsExitCodeTwo()
    {
        using TempDirectory root = new();

        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = Program.Run(["mutation", "--root", root.Path, "--since"], [new MutationCommand()], root.Path, output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("the option --since needs a commit", error.ToString(), StringComparison.Ordinal);
    }

    // ---- helpers ----

    private static MutantSpec Killed => new("Killed");

    private static MutantSpec Survived => new("Survived", Line: 57, Column: 24, EndColumn: 29);

    private static MutantSpec NoCoverage => new("NoCoverage", Replacement: "a / b", Line: 58, Column: 16, EndColumn: 21);

    private static MutantSpec[] Many(string status, int count, string? reason = null, string replacement = "a - b", int line = 1, int column = 1, int endColumn = 2) =>
        [.. Enumerable.Range(0, count).Select(_ => new MutantSpec(status, reason, Replacement: replacement, Line: line, Column: column, EndColumn: endColumn))];

    // A source of 60 lines: line 57 has "a + b" at column 24, line 58 has "a * b" at column 16, the others are comments "// line N".
    private static string Source() => string.Join("\r\n", Enumerable.Range(1, 60).Select(index => index switch
    {
        57 => "        return checked(a + b);",
        58 => "        return a * b;",
        _ => "// line " + index,
    }));

    // A report in the form of the real one: the keys are paths of the container and "thresholds" has no "break".
    private static string Report(params (string Path, string Source, MutantSpec[] Mutants)[] files)
    {
        JsonObject nodes = new();
        foreach ((string path, string source, MutantSpec[] mutants) in files)
        {
            nodes["/repo/" + path] = new JsonObject
            {
                ["language"] = "cs",
                ["source"] = source,
                ["mutants"] = new JsonArray([.. mutants.Select(Mutant)]),
            };
        }

        return new JsonObject
        {
            ["files"] = nodes,
            ["projectRoot"] = "/repo/" + ProjectDirectory,
            ["schemaVersion"] = "2",
            ["thresholds"] = new JsonObject { ["high"] = 80, ["low"] = 80 },
        }.ToJsonString();
    }

    private static JsonNode Mutant(MutantSpec spec, int index)
    {
        JsonObject mutant = new()
        {
            ["id"] = index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["mutatorName"] = spec.Mutator,
            ["replacement"] = spec.Replacement,
            ["location"] = new JsonObject
            {
                ["start"] = new JsonObject { ["line"] = spec.Line, ["column"] = spec.Column },
                ["end"] = new JsonObject { ["line"] = spec.EndLine == 0 ? spec.Line : spec.EndLine, ["column"] = spec.EndColumn },
            },
            ["status"] = spec.Status,
        };
        if (spec.Reason is not null)
        {
            mutant["statusReason"] = spec.Reason;
        }

        mutant["static"] = false;
        return mutant;
    }

    // A repository on `main` with the files committed as the base.
    private static void Repository(TempDirectory root, params string[] files)
    {
        foreach (string file in files)
        {
            root.Write(file, "// base\n");
        }

        Assert.True(Git.Run(root.Path, "init", "-q", "-b", "main").Succeeded);
        Assert.True(Git.Run(root.Path, "add", "--all").Succeeded);
        GitResult commit = Git.Run(root.Path, "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "commit", "-q", "-m", "base");
        Assert.True(commit.Succeeded, commit.Error);
    }

    private static void Change(TempDirectory root, params string[] files)
    {
        foreach (string file in files)
        {
            root.Write(file, "// changed\n");
        }
    }

    private static Result Check(TempDirectory root, string? report, string config, string configPath)
    {
        if (report is not null)
        {
            root.Write(ReportFile, report);
        }

        root.Write(configPath, config);
        return Run(root, ["mutation", "--report", ReportFile, "--config", configPath]);
    }

    private static Result CheckWithoutReport(TempDirectory root, string config)
    {
        root.Write(ConfigFile, config);
        return Run(root, ["mutation", "--report", ReportFile, "--config", ConfigFile]);
    }

    private static Result Run(TempDirectory root, params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int exitCode = Program.Run([.. args, "--root", root.Path], [new MutationCommand()], root.Path, output, error);
        return new Result(exitCode, output.ToString().ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries), error.ToString().ReplaceLineEndings("\n"));
    }

    // The run failed with these errors and nothing else on standard error.
    private static void AssertErrors(Result result, params Finding[] expected)
    {
        Assert.Equal((1, string.Empty), (result.ExitCode, result.Error));
        FindingAssert.Exactly([.. result.Lines.Select(line => Finding.TryParse(line, out Finding finding) ? finding : throw new InvalidOperationException(line))], expected);
    }

    private static Finding Error(string code, string path, int line, int column, string message) => FindingAssert.Error(code, path, line, column, message);

    private sealed record MutantSpec(
        string Status,
        string? Reason = null,
        string Mutator = "Arithmetic",
        string Replacement = "a - b",
        int Line = 1,
        int Column = 1,
        int EndLine = 0,
        int EndColumn = 2);

    private sealed record Result(int ExitCode, string[] Lines, string Error);
}
