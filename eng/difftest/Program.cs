using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Corpus;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Difftest;

/// <summary>The options of a run (docs/design/eng-difftest.md, "Invocation").</summary>
/// <param name="Seed">The seed of the run.</param>
/// <param name="Budget">The time of the whole run, minimization included.</param>
/// <param name="Case">The only case to run, or <see langword="null"/> for cases 0, 1, 2, … until the budget is spent.</param>
/// <param name="NumbersOnly">Whether to print only the journal line and write no file.</param>
internal sealed record RunOptions(ulong Seed, TimeSpan Budget, ulong? Case, bool NumbersOnly);

/// <summary>What a run works with besides its options: the streams, the two sides and the commit.</summary>
/// <param name="Output">Standard output: the <c>log</c> lines of the oracle, the findings and the journal line.</param>
/// <param name="Error">Standard error: the messages of the run itself.</param>
/// <param name="Library">Step 2: builds the library side from the run manifest and the path of the time zone data file, or names why it cannot.</param>
/// <param name="OracleHost">The host of the oracle.</param>
/// <param name="OraclePort">The port of the oracle.</param>
/// <param name="ConnectLimit">How long step 3 tries to connect to the oracle.</param>
/// <param name="AnswerLimit">How long step 4 waits for the answer of the oracle to a request.</param>
/// <param name="Commit">The start of <c>HEAD</c> of the repository root, for the journal line.</param>
internal sealed record RunSetup(
    TextWriter Output,
    TextWriter Error,
    Func<byte[], string, (ICaseSide? Side, string Problem)> Library,
    string OracleHost,
    int OraclePort,
    TimeSpan ConnectLimit,
    TimeSpan AnswerLimit,
    Func<string, string> Commit)
{
    /// <summary>The setup of the container <c>difftest</c>: the library, the oracle of the Compose project, <c>git</c>.</summary>
    /// <param name="output">Standard output.</param>
    /// <param name="error">Standard error.</param>
    internal static RunSetup Container(TextWriter output, TextWriter error) => new(
        output,
        error,
        (manifest, zoneInfoFile) => LibrarySide.TryBuild(manifest, zoneInfoFile, out LibrarySide? side, out string problem) ? (side, problem) : (null, problem),
        OracleConnection.DefaultHost,
        OracleConnection.DefaultPort,
        OracleConnection.DefaultConnectLimit,
        OracleConnection.DefaultAnswerLimit,
        GitCommit);

    // The first 8 hexadecimal digits of HEAD, as git rev-parse gives them.
    private static string GitCommit(string root)
    {
        ProcessStartInfo start = new("git")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = root,
        };
        start.ArgumentList.Add("rev-parse");
        start.ArgumentList.Add("HEAD");
        try
        {
            using Process process = Process.Start(start) ?? throw new IOException("git rev-parse HEAD did not start");
            string output = process.StandardOutput.ReadToEnd().Trim();
            string error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && output.Length >= 8
                ? output[..8]
                : throw new IOException($"git rev-parse HEAD gives no commit: {error}");
        }
        catch (Win32Exception exception)
        {
            throw new IOException($"git rev-parse HEAD did not start: {exception.Message}", exception);
        }
    }
}

/// <summary>
/// The differential run against cel-go (docs/design/eng-difftest.md, "A run"). Exit code: 0 without new signatures, 1 with a
/// finding with <c>error</c>, 2 for an invalid invocation, a refused manifest, a failed oracle, its steps included, or a
/// minimized case that agrees when it is run again. With <c>--numbers-only</c> a message of exit code 2 quotes no case and no
/// line or answer of the oracle.
/// </summary>
internal static class Program
{
    private const string SeedOption = "--seed";
    private const string BudgetOption = "--budget";
    private const string CaseOption = "--case";
    private const string NumbersOnlyOption = "--numbers-only";

    // The number of instants that ENG822 draws from the seed; five fixed instants join them.
    private const int ZoneInstants = 256;

    /// <summary>The budget without <c>--budget</c>.</summary>
    internal static readonly TimeSpan DefaultBudget = TimeSpan.FromMinutes(5);

    internal static int Main(string[] args) =>
        Run(args, Directory.GetCurrentDirectory(), DateTimeOffset.UtcNow, RunSetup.Container(Console.Out, Console.Error));

    /// <summary>Runs one invocation in the container <c>difftest</c>, with the findings on standard output.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="root">The repository root.</param>
    /// <param name="now">The current time; its date in UTC is the seed without <c>--seed</c>.</param>
    /// <param name="error">Standard error: the messages of the run itself.</param>
    /// <returns>The exit code.</returns>
    internal static int Run(IReadOnlyList<string> args, string root, DateTimeOffset now, TextWriter error) =>
        Run(args, root, now, RunSetup.Container(Console.Out, error));

    /// <summary>Runs one invocation.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="root">The repository root.</param>
    /// <param name="now">The current time; its date in UTC is the seed without <c>--seed</c>.</param>
    /// <param name="setup">The streams, the sides and the commit.</param>
    /// <returns>The exit code.</returns>
    internal static int Run(IReadOnlyList<string> args, string root, DateTimeOffset now, RunSetup setup)
    {
        if (!TryParse(args, now, out RunOptions? options, out string problem))
        {
            return Failed(setup.Error, problem);
        }

        // Step 1: the manifest.
        if (!RunManifest.TryRead(root, out problem))
        {
            return Failed(setup.Error, problem);
        }

        try
        {
            return Run(options!, root, setup);
        }
        catch (IOException exception)
        {
            // A failed oracle, a closed connection, or a file of the repository that the run cannot read.
            return Failed(setup.Error, exception.Message);
        }
        catch (SideFailureException exception)
        {
            // A line or an answer of the oracle that does not read, or a minimized case that does not diverge again; the public
            // log of the nightly run shows no case and no line of the oracle.
            return Failed(setup.Error, options!.NumbersOnly ? exception.Message : exception.QuotedMessage);
        }
    }

    // Steps 2 to 5 of a run.
    private static int Run(RunOptions options, string root, RunSetup setup)
    {
        string commit = setup.Commit(root);

        // Step 2: the library side; an exception of the library is an error of this step.
        ICaseSide? library;
        string problem;
        try
        {
            (library, problem) = setup.Library(
                File.ReadAllBytes(Path.Combine(root, ProfileTables.ManifestPath)),
                Path.Combine(root, ProfileTables.ZoneInfoPath));
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or IOException))
        {
            return Failed(setup.Error, "step 2, the environment of the library: the library threw " + LibrarySide.Describe(exception));
        }

        if (library is null)
        {
            return Failed(setup.Error, "step 2, the environment of the library: " + problem);
        }

        // Step 3: the oracle; the budget counts from the handshake.
        using OracleConnection oracle = OracleConnection.Open(
            setup.OracleHost,
            setup.OraclePort,
            setup.ConnectLimit,
            setup.AnswerLimit,
            ProfileTables.ManifestPath,
            line => Print(setup.Output, options, line));
        Stopwatch spent = Stopwatch.StartNew();
        if (OracleGoVersion.Check(root, oracle.Handshake.Go) is { } otherGo)
        {
            Print(setup.Output, options, otherGo.ToString());
            setup.Output.WriteLine(JournalLine(options, commit, oracle.Handshake, cases: 0, [], knownFound: 0));
            return 1;
        }

        // Between the handshake and the cases, inside the budget: the offsets of the zones of the data file (ENG822). A run with
        // --case reproduces one case and does not run it.
        List<Finding> zoneFindings = [];
        if (options.Case is null)
        {
            zoneFindings.AddRange(ZoneOffsets.Compare(root, ProfileTables.ZoneInfoPath, ZoneOffsets.Instants(options.Seed, ZoneInstants), library, oracle));
        }

        // Step 4: cases 0, 1, 2, … until the budget is spent, or the one case of --case.
        DivergenceSearch search = new(library, oracle, oracle, KnownSignatures.Read(root));
        ulong cases = 0;
        for (ulong number = options.Case ?? 0; options.Case is not null ? cases == 0 : spent.Elapsed < options.Budget; number++)
        {
            search.Check(options.Seed, number, CaseGenerator.Case(options.Seed, number));
            cases++;
        }

        // Step 5: pending.textproto, the findings and the journal line. A run with --case reproduces a case and leaves the
        // working tree as it is: it prints the text pending.textproto would have instead of writing the file.
        PendingText pending = PendingFile.Build(search.NewSignatures);
        IReadOnlyList<Finding> findings = [.. zoneFindings, .. PendingFile.Findings(search.NewSignatures, pending)];
        if (!options.NumbersOnly && options.Case is null)
        {
            PendingFile.Write(root, pending);
        }
        else if (search.NewSignatures.Count > 0)
        {
            Print(setup.Output, options, pending.Text.TrimEnd('\n'));
        }

        foreach (Finding finding in findings)
        {
            Print(setup.Output, options, finding.ToString());
        }

        setup.Output.WriteLine(JournalLine(options, commit, oracle.Handshake, cases, search.NewSignatures, search.KnownSignaturesFound.Count));
        return findings.Any(finding => finding.Severity == FindingSeverity.Error) ? 1 : 0;
    }

    /// <summary>
    /// The journal line, the last line of a run: only numbers, so the public log of the nightly workflow shows no case.
    /// <c>difftest seed=20261104 commit=9f8e7d6c oracle=0.32.0 cases=412345 signatures new=1 (S1=1, S2=0) known=0 new-cases=1207</c>.
    /// </summary>
    /// <param name="options">The options of the run.</param>
    /// <param name="commit">The start of <c>HEAD</c>.</param>
    /// <param name="handshake">The handshake of the oracle.</param>
    /// <param name="cases">The number of cases the run checked.</param>
    /// <param name="newSignatures">The first case of each new signature.</param>
    /// <param name="knownFound">The number of known signatures the run found.</param>
    internal static string JournalLine(
        RunOptions options,
        string commit,
        OracleHandshake handshake,
        ulong cases,
        IReadOnlyList<NewSignature> newSignatures,
        int knownFound)
    {
        int s1 = newSignatures.Count(found => found.Divergence.Kind.Severity() == DivergenceSeverity.S1);
        string numbers = string.Join(',', newSignatures.Select(found => found.Number.ToString(CultureInfo.InvariantCulture)));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"difftest seed={options.Seed} commit={commit} oracle={handshake.Version} cases={cases} signatures new={newSignatures.Count} (S1={s1}, S2={newSignatures.Count - s1}) known={knownFound} new-cases={numbers}");
    }

    // A line other than the journal line: --numbers-only prints only the journal line.
    private static void Print(TextWriter output, RunOptions options, string line)
    {
        if (!options.NumbersOnly)
        {
            output.WriteLine(line);
        }
    }

    /// <summary>Parses the command line.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="now">The current time; its date in UTC is the seed without <c>--seed</c>.</param>
    /// <param name="options">The options, or <see langword="null"/> when the command line is invalid.</param>
    /// <param name="problem">What is wrong with the command line, one line; empty when it is valid.</param>
    /// <returns><see langword="true"/> when the command line is valid.</returns>
    internal static bool TryParse(IReadOnlyList<string> args, DateTimeOffset now, out RunOptions? options, out string problem)
    {
        options = null;
        ulong seed = ulong.Parse(now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        TimeSpan budget = DefaultBudget;
        ulong? single = null;
        bool numbersOnly = false;
        for (int i = 0; i < args.Count; i++)
        {
            string option = args[i];
            if (option == NumbersOnlyOption)
            {
                numbersOnly = true;
                continue;
            }

            if (option is not (SeedOption or BudgetOption or CaseOption))
            {
                problem = $"unknown option '{option}'; the options are {SeedOption} <n>, {BudgetOption} <time>, {CaseOption} <n> and {NumbersOnlyOption}";
                return false;
            }

            string? value = i + 1 < args.Count ? args[++i] : null;
            if (!TryValue(option, value, ref seed, ref budget, ref single))
            {
                problem = option == BudgetOption
                    ? $"{BudgetOption} needs a time such as 30s, 5m or 2h, not '{value}'"
                    : $"{option} needs a number, not '{value}'";
                return false;
            }
        }

        options = new RunOptions(seed, budget, single, numbersOnly);
        problem = string.Empty;
        return true;
    }

    private static bool TryValue(string option, string? value, ref ulong seed, ref TimeSpan budget, ref ulong? single)
    {
        if (value is null)
        {
            return false;
        }

        if (option == BudgetOption)
        {
            return BudgetText.TryParse(value, out budget);
        }

        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong number))
        {
            return false;
        }

        if (option == SeedOption)
        {
            seed = number;
        }
        else
        {
            single = number;
        }

        return true;
    }

    private static int Failed(TextWriter error, string problem)
    {
        error.WriteLine($"eng/difftest: {problem}");
        return 2;
    }
}
