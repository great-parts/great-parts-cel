using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Cel.Expr;
using Any = Google.Protobuf.WellKnownTypes.Any;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;
using Great.Parts.Cel.Eng.Difftest;
using Great.Parts.Cel.Eng.Difftest.Corpus;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Eng.Findings;
using Great.Parts.Cel.Eng.Tests.Support;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The session with the oracle and steps 2 to 5 of a run of eng/difftest (docs/design/eng-difftest.md, "A run", "Findings and
// output", "Tests"), with a fake oracle on a loopback socket that plays the launcher: it reads the first line and sends given
// lines, then answers requests or closes the connection. The library side is a fake that gives one value for every case,
// unless a test names another.
public sealed class OracleConnectionTests
{
    private const string Handshake = """{"oracle":"cel.dev/cel-go v0.32.0","go":"go1.27.3"}""";
    private const string Commit = "9f8e7d6c";
    private const string Loopback = "127.0.0.1";

    // An expression in a line or an answer of the oracle that a run with --numbers-only must not quote.
    private const string Secret = "i1 + 9223372036854775807";

    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    // The answer limit of the tests in which the fake oracle does not answer.
    private static readonly TimeSpan ShortLimit = TimeSpan.FromMilliseconds(500);

    // ---- The session ----

    [Fact]
    public void LogLines_PassUnchangedAndTheFirstLineNamesTheManifest()
    {
        string[] logs = ["== gofmt -l .", "main.go:3:1: say \"hi\" \\ to\ttab", "Ünïcødé ✓", string.Empty];
        using FakeOracle fake = new([.. logs.Select(Log), Handshake], answer: null);
        List<string> received = [];

        using (OracleConnection connection = OracleConnection.Open(Loopback, fake.Port, Limit, Limit, ProfileTables.ManifestPath, received.Add))
        {
            Assert.Equal(new OracleHandshake("cel.dev/cel-go v0.32.0", "go1.27.3"), connection.Handshake);
            Assert.Equal("0.32.0", connection.Handshake.Version);
        }

        fake.Join();
        Assert.Equal(logs, received);
        Assert.Equal("""{"env":"eng/difftest/Profile/environment.json"}""", fake.FirstLine);
    }

    [Fact]
    public void Answers_GiveTheOutcomesOfTheProtocol()
    {
        using FakeOracle fake = new([Handshake], request => request.GetProperty("expr").GetString() switch
        {
            "null" => """{"id":ID,"outcome":"value","value":{"nullValue":null}}""",
            "1 / 0" => """{"id":ID,"outcome":"eval-error","message":"division by zero"}""",
            "1 +" => """{"id":ID,"outcome":"compile-error","stage":"parse","message":"Syntax error"}""",
            "x1 - b1" => """{"id":ID,"outcome":"compile-error","stage":"check","message":"found no matching overload"}""",
            _ => """{"id":ID,"textproto":"name: \"7-1\"\nexpr: \"i1 + 1\""}""",
        });
        using OracleConnection connection = OracleConnection.Open(Loopback, fake.Port, Limit, Limit, ProfileTables.ManifestPath, _ => { });

        SideOutcome value = connection.Evaluate("null", new Dictionary<string, Value>());
        SideOutcome evaluationError = connection.Evaluate("1 / 0", new Dictionary<string, Value>());
        SideOutcome parseError = connection.Evaluate("1 +", new Dictionary<string, Value>());
        SideOutcome checkError = connection.Evaluate("x1 - b1", new Dictionary<string, Value>());
        string text = connection.Case("7-1", "add_int64(int, int) we-value", "i1 + 1", new Dictionary<string, Value> { ["i1"] = Int(41) });

        Assert.Equal(new Value { NullValue = Google.Protobuf.WellKnownTypes.NullValue.NullValue }, value.Result);
        Assert.Equal((OutcomeClass.EvaluationError, "division by zero"), (evaluationError.Class, evaluationError.Message));
        Assert.Equal((OutcomeClass.CompileError, CompileStage.Parse), (parseError.Class, parseError.Stage));
        Assert.Equal((OutcomeClass.CompileError, CompileStage.Check), (checkError.Class, checkError.Stage));
        Assert.Equal("name: \"7-1\"\nexpr: \"i1 + 1\"", text);
        connection.Dispose();
        fake.Join();
        using JsonDocument request = JsonDocument.Parse(fake.Requests[^1]);
        JsonElement fields = request.RootElement;
        Assert.Equal(
            (4L, "case", "i1 + 1", "41", "7-1", "add_int64(int, int) we-value"),
            (fields.GetProperty("id").GetInt64(),
                fields.GetProperty("op").GetString(),
                fields.GetProperty("expr").GetString(),
                fields.GetProperty("bindings").GetProperty("i1").GetProperty("int64Value").GetString(),
                fields.GetProperty("name").GetString(),
                fields.GetProperty("description").GetString()));
    }

    // A value of time is an Any of google.protobuf.Timestamp or google.protobuf.Duration in the protojson of the oracle.
    [Fact]
    public void ValueOfTime_IsReadFromAnAnyOfItsMessage()
    {
        using FakeOracle fake = new([Handshake], request => request.GetProperty("expr").GetString() switch
        {
            "timestamp('2023-01-01T00:00:00.000000005Z')" => """{"id":ID,"outcome":"value","value":{"objectValue":{"@type":"type.googleapis.com/google.protobuf.Timestamp","value":"2023-01-01T00:00:00.000000005Z"}}}""",
            _ => """{"id":ID,"outcome":"value","value":{"objectValue":{"@type":"type.googleapis.com/google.protobuf.Duration","value":"-1.500s"}}}""",
        });
        using OracleConnection connection = OracleConnection.Open(Loopback, fake.Port, Limit, Limit, ProfileTables.ManifestPath, _ => { });

        SideOutcome timestamp = connection.Evaluate("timestamp('2023-01-01T00:00:00.000000005Z')", new Dictionary<string, Value>());
        SideOutcome duration = connection.Evaluate("duration('-1.5s')", new Dictionary<string, Value>());

        Assert.Equal(new Value { ObjectValue = Any.Pack(new Timestamp { Seconds = 1672531200, Nanos = 5 }) }, timestamp.Result);
        Assert.Equal(new Value { ObjectValue = Any.Pack(new Duration { Seconds = -1, Nanos = -500_000_000 }) }, duration.Result);
    }

    [Fact]
    public void ZoneOffset_SendsTheRequestOffsetAndReadsTheSeconds()
    {
        using FakeOracle fake = new([Handshake], request => request.GetProperty("op").GetString() == "offset"
            ? """{"id":ID,"outcome":"offset","seconds":-17762}"""
            : null);
        using OracleConnection connection = OracleConnection.Open(Loopback, fake.Port, Limit, Limit, ProfileTables.ManifestPath, _ => { });

        TimeSpan offset = connection.ZoneOffset("America/New_York", new DateTimeOffset(1800, 1, 1, 0, 0, 0, TimeSpan.Zero));
        connection.Dispose();
        fake.Join();

        Assert.Equal(TimeSpan.FromSeconds(-17762), offset);
        using JsonDocument request = JsonDocument.Parse(fake.Requests.Single());
        JsonElement fields = request.RootElement;
        Assert.Equal(["id", "op", "zone", "instant"], fields.EnumerateObject().Select(property => property.Name));
        Assert.Equal(("offset", "America/New_York", -5364662400L), (fields.GetProperty("op").GetString(), fields.GetProperty("zone").GetString(), fields.GetProperty("instant").GetInt64()));
    }

    [Fact]
    public void ZoneOffset_OfAZoneThatTheOracleDoesNotLoad_IsAFailureOfTheSide()
    {
        using FakeOracle fake = new([Handshake], _ => """{"id":ID,"outcome":"zone-error","message":"unknown time zone Mars/Base"}""");
        using OracleConnection connection = OracleConnection.Open(Loopback, fake.Port, Limit, Limit, ProfileTables.ManifestPath, _ => { });

        SideFailureException failure = Assert.Throws<SideFailureException>(() => connection.ZoneOffset("Mars/Base", DateTimeOffset.UnixEpoch));

        Assert.Equal("the oracle does not load the time zone 'Mars/Base': unknown time zone Mars/Base", failure.Message);
    }

    // The formatter of Google.Protobuf writes 2^-25 with "r", which on .NET 10 gives the text of the neighbouring double.
    [Fact]
    public void DoubleBindings_ReachTheOracleAsTheSameBits()
    {
        using FakeOracle fake = new([Handshake], answer: _ => """{"id":ID,"outcome":"value","value":{"int64Value":"41"}}""");
        Dictionary<string, Value> bindings = new()
        {
            ["d1"] = Double(BitConverter.Int64BitsToDouble(0x3E60000000000000)),
            ["d2"] = Double(-0.0),
            ["x1"] = Double(double.NaN),
            ["x2"] = Double(double.PositiveInfinity),
            ["d3"] = Double(double.NegativeInfinity),
        };
        using (OracleConnection connection = OracleConnection.Open(Loopback, fake.Port, Limit, Limit, ProfileTables.ManifestPath, _ => { }))
        {
            _ = connection.Evaluate("d1", bindings);
        }

        fake.Join();
        using JsonDocument request = JsonDocument.Parse(fake.Requests[0]);
        JsonElement sent = request.RootElement.GetProperty("bindings");
        Assert.Equal("""{"doubleValue":2.9802322387695312E-08}""", sent.GetProperty("d1").GetRawText());
        Assert.Equal("""{"doubleValue":-0}""", sent.GetProperty("d2").GetRawText());
        Assert.Equal("""{"doubleValue":"NaN"}""", sent.GetProperty("x1").GetRawText());
        Assert.Equal("""{"doubleValue":"Infinity"}""", sent.GetProperty("x2").GetRawText());
        Assert.Equal("""{"doubleValue":"-Infinity"}""", sent.GetProperty("d3").GetRawText());
        Assert.All(bindings, binding => Assert.Equal(
            BitConverter.DoubleToInt64Bits(binding.Value.DoubleValue),
            BitConverter.DoubleToInt64Bits(Google.Protobuf.JsonParser.Default.Parse<Value>(sent.GetProperty(binding.Key).GetRawText()).DoubleValue)));
    }

    // A binding whose text reads back to another value fails the side, which is exit code 2 of a run; only the quoted message
    // has the text.
    [Fact]
    public void BindingThatDoesNotReadBack_FailsTheSideQuotingTheTextOnlyInTheQuotedMessage()
    {
        Value bound = Double(BitConverter.Int64BitsToDouble(0x3E60000000000000));
        string lossy = """{"doubleValue":2.980232238769531E-08}""";
        Value list = new() { ListValue = new ListValue { Values = { Int(1), Double(1.5) } } };

        SideFailureException exception = Assert.Throws<SideFailureException>(() => OracleConnection.CheckReadsBack("d1", bound, lossy));

        Assert.Equal("the binding of d1 does not read back to the bound value", exception.Message);
        Assert.Equal("the binding of d1 does not read back to the bound value: " + lossy, exception.QuotedMessage);
        OracleConnection.CheckReadsBack("d1", bound, """{"doubleValue":2.9802322387695312E-08}""");
        Assert.Equal(list, Google.Protobuf.JsonParser.Default.Parse<Value>(OracleConnection.BindingJson("l1", list)));
    }

    // A double inside a bound list or map is written by its shortest text too, so 2^-25, which the formatter of Google.Protobuf
    // writes as the neighbouring double, reads back bit for bit and does not fail the run.
    [Fact]
    public void DoubleInsideABoundListOrMap_ReadsBackBitForBit()
    {
        Value lossy = Double(BitConverter.Int64BitsToDouble(0x3E60000000000000));
        Value list = new() { ListValue = new ListValue { Values = { Int(1), lossy, Double(double.NaN), Double(-0.0) } } };
        Value map = new()
        {
            MapValue = new MapValue
            {
                Entries =
                {
                    new MapValue.Types.Entry { Key = new Value { StringValue = "a" }, Value = lossy },
                    new MapValue.Types.Entry { Key = Int(2), Value = list },
                },
            },
        };

        foreach (Value bound in new[] { list, map })
        {
            Assert.Equal(bound, Google.Protobuf.JsonParser.Default.Parse<Value>(OracleConnection.BindingJson("x1", bound)));
        }
    }

    [Fact]
    public void ClosedConnectionInTheSession_ThrowsIOException()
    {
        using FakeOracle fake = new([Handshake], answer: null);
        using OracleConnection connection = OracleConnection.Open(Loopback, fake.Port, Limit, Limit, ProfileTables.ManifestPath, _ => { });
        fake.Join();

        IOException exception = Assert.Throws<IOException>(() => connection.Evaluate("1", new Dictionary<string, Value>()));

        Assert.StartsWith("the oracle closed the connection", exception.Message, StringComparison.Ordinal);
    }

    // ---- Exit codes of a run ----

    [Fact]
    public void FailedLine_IsExitCode2AfterTheLogLines()
    {
        using FakeOracle fake = new([Log("== go vet ./..."), Log("serve.go:12:2: unreachable code"), Failed("go vet ./...")], answer: null);
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, fake.Port, Limit);

        Assert.Equal(2, exitCode);
        Assert.Equal("== go vet ./...\nserve.go:12:2: unreachable code\n", output);
        Assert.Equal("eng/difftest: the oracle failed at the step 'go vet ./...'; the log lines above say why\n", error);
    }

    [Fact]
    public void ClosedConnectionBeforeTheHandshake_IsExitCode2()
    {
        using FakeOracle fake = new([Log("oracle: the run manifest has 'validators'")], answer: null);
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, fake.Port, Limit);

        Assert.Equal(2, exitCode);
        Assert.Equal("oracle: the run manifest has 'validators'\n", output);
        Assert.Equal("eng/difftest: the oracle closed the connection before the handshake; the log lines above say why\n", error);
    }

    [Fact]
    public void NoListener_IsExitCode2AndTheMessageNamesTheLogsCommand()
    {
        using Socket bound = BoundPort();
        int port = Port(bound);
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, port, TimeSpan.FromSeconds(1));

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.StartsWith($"eng/difftest: no connection to the oracle at {Loopback}:{port} within 1 s (", error, StringComparison.Ordinal);
        Assert.EndsWith(
            "); the log of its container: docker compose -f eng/compose.yaml logs oracle, with the same -p <name> when the run had one\n",
            error,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherGoInTheHandshake_GivesEng821AtTheGoLineAndStopsBeforeTheFirstCase()
    {
        using FakeOracle fake = new([Handshake.Replace("go1.27.3", "go1.27.4", StringComparison.Ordinal)], answer: _ => """{"id":ID,"outcome":"value","value":{"int64Value":"41"}}""");
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, fake.Port, Limit);
        fake.Join();

        Assert.Equal(1, exitCode);
        Assert.Empty(error);
        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(Finding.TryParse(lines[0], out Finding finding), lines[0]);
        FindingAssert.Exactly(
            [finding],
            FindingAssert.Error(
                EngCodes.Eng821,
                OracleGoVersion.ModulePath,
                3,
                1,
                "the Go of the oracle differs from the go line: the handshake names go1.27.4, eng/oracle/go.mod has go 1.27.3; the image tag of the service oracle and the go line change together"));
        Assert.Equal($"difftest seed=7 commit={Commit} oracle=0.32.0 cases=0 signatures new=0 (S1=0, S2=0) known=0 new-cases=", lines[1]);
        Assert.Equal(2, lines.Length);
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public void AgreeingCase_IsExitCode0WithTheJournalLineAndLeavesThePendingFileUnwritten()
    {
        using FakeOracle fake = new([Handshake], answer: _ => """{"id":ID,"outcome":"value","value":{"int64Value":"41"}}""");
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, fake.Port, Limit, "--case", "3");
        fake.Join();

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        Assert.Equal($"difftest seed=7 commit={Commit} oracle=0.32.0 cases=1 signatures new=0 (S1=0, S2=0) known=0 new-cases=\n", output);
        Assert.False(File.Exists(Path.Combine(root.Path, PendingFile.Path)));
        Assert.Single(fake.Requests);
        Assert.Contains($"\"expr\":{JsonSerializer.Serialize(CaseGenerator.Case(7, 3).Text)}", fake.Requests[0], StringComparison.Ordinal);
    }

    [Fact]
    public void SilentOracle_IsExitCode2AfterTheAnswerLimit()
    {
        using FakeOracle fake = new([Handshake], answer: _ => null);
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, fake.Port, ShortLimit, "--case", "3");
        fake.Join();

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Equal("eng/difftest: the oracle did not answer request 0 within 0.5 seconds\n", error);
        Assert.Single(fake.Requests);
    }

    // The answer limit holds for the whole answer: log lines without a pause do not extend it. --numbers-only keeps the log
    // lines out of the output.
    [Fact]
    public void OracleThatOnlyLogs_IsExitCode2AfterTheAnswerLimit()
    {
        using FakeOracle fake = new([Handshake], answer: _ => null, logsAfterRequest: Log("oracle: still busy"));
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, fake.Port, ShortLimit, "--case", "3", "--numbers-only");
        fake.Join();

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Equal("eng/difftest: the oracle did not answer request 0 within 0.5 seconds\n", error);
        Assert.Single(fake.Requests);
    }

    [Fact]
    public void NumbersOnly_PrintsTheJournalLineAloneAfterEng821()
    {
        using FakeOracle fake = new(
            [Log("== go build"), Handshake.Replace("go1.27.3", "go1.27.4", StringComparison.Ordinal)],
            answer: _ => """{"id":ID,"outcome":"value","value":{"int64Value":"41"}}""");
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, fake.Port, Limit, "--numbers-only");
        fake.Join();

        Assert.Equal(1, exitCode);
        Assert.Empty(error);
        Assert.Equal($"difftest seed=7 commit={Commit} oracle=0.32.0 cases=0 signatures new=0 (S1=0, S2=0) known=0 new-cases=\n", output);
    }

    [Fact]
    public void NumbersOnly_PrintsTheJournalLineAloneAndWritesNoFile()
    {
        using FakeOracle fake = new([Log("== go build"), Handshake], answer: _ => """{"id":ID,"outcome":"value","value":{"int64Value":"41"}}""");
        using TempDirectory root = Repository();

        (int exitCode, string output, string error) = Run(root, fake.Port, Limit, "--case", "3", "--numbers-only");
        fake.Join();

        Assert.Equal(0, exitCode);
        Assert.Empty(error);
        Assert.Equal($"difftest seed=7 commit={Commit} oracle=0.32.0 cases=1 signatures new=0 (S1=0, S2=0) known=0 new-cases=\n", output);
        Assert.False(File.Exists(Path.Combine(root.Path, PendingFile.Path)));
    }

    // Each line or answer of the oracle that does not read, with the message of exit code 2 without the quote; a run without
    // --numbers-only adds ": " and the line.
    [Theory]
    [InlineData("handshake", "the oracle sent no handshake")]
    [InlineData("not JSON", "the oracle sent a line that is not JSON")]
    [InlineData("not an object", "the oracle sent a line that is not a JSON object")]
    [InlineData("another id", "the oracle answered request 0 with a line without its id")]
    [InlineData("no outcome", "the answer of the oracle to request 0 has no outcome of the protocol")]
    [InlineData("no message", "the answer of the oracle to request 0 has no 'message'")]
    [InlineData("unknown stage", "the answer of the oracle to request 0 has the stage 'link', which the protocol does not have")]
    public static void LineThatDoesNotRead_IsExitCode2QuotedOnlyWithoutNumbersOnly(string kind, string message)
    {
        string line = kind switch
        {
            "handshake" => $$"""{"oracle":"{{Secret}}"}""",
            "not JSON" => Secret,
            "not an object" => $$"""["{{Secret}}"]""",
            "another id" => $$"""{"id":7,"outcome":"eval-error","message":"{{Secret}}"}""",
            "no outcome" => $$"""{"id":0,"outcome":"{{Secret}}"}""",
            "unknown stage" => $$"""{"id":0,"outcome":"compile-error","stage":"link","message":"{{Secret}}"}""",
            _ => $$"""{"id":0,"outcome":"eval-error","note":"{{Secret}}"}""",
        };
        string[] lines = kind == "handshake" ? [line] : [Handshake];
        Func<JsonElement, string?> answer = _ => line;

        using (FakeOracle fake = new(lines, answer))
        using (TempDirectory root = Repository())
        {
            (int exitCode, string output, string error) = Run(root, fake.Port, Limit, "--case", "3", "--numbers-only");
            fake.Join();

            Assert.Equal(2, exitCode);
            Assert.Empty(output);
            Assert.Equal($"eng/difftest: {message}\n", error);
        }

        using (FakeOracle fake = new(lines, answer))
        using (TempDirectory root = Repository())
        {
            (int exitCode, _, string error) = Run(root, fake.Port, Limit, "--case", "3");
            fake.Join();

            Assert.Equal(2, exitCode);
            Assert.Equal($"eng/difftest: {message}: {line}\n", error);
        }
    }

    [Fact]
    public void MinimizedCaseThatAgreesAgain_IsExitCode2NamingTheSeedAndTheCaseAndQuotingItOnlyWithoutNumbersOnly()
    {
        string text = CaseGenerator.Case(7, 3).Text;

        (int exitCode, string output, string error) = RunWithFlakyLibrary("--numbers-only");

        Assert.Equal(2, exitCode);
        Assert.Empty(output);
        Assert.Equal("eng/difftest: case 3 of seed 7: the minimized case does not diverge again: a side is not deterministic\n", error);

        (exitCode, _, error) = RunWithFlakyLibrary();

        Assert.Equal(2, exitCode);
        Assert.Equal($"eng/difftest: case 3 of seed 7: the minimized case '{text}' does not diverge again: a side is not deterministic\n", error);
    }

    // A run of the same seed and --case asks the oracle the same requests and gives the same minimized case and signature.
    [Fact]
    public void SameSeedAndCase_GiveTheSameMinimizedCaseAndSignature()
    {
        string number = SignatureTests.CallReadingI1(7).ToString(CultureInfo.InvariantCulture);

        (int exitCode, string output, string pending, List<string> requests) = RunWithLibraryThatDivergesOnI1(number);
        (int exitCodeAgain, string outputAgain, string pendingAgain, List<string> requestsAgain) = RunWithLibraryThatDivergesOnI1(number);

        Assert.Equal(1, exitCode);
        Assert.Contains("new divergence signature 'ident(int) value-mismatch' (S1)", output, StringComparison.Ordinal);
        Assert.Contains($"name: \"7-{number}\"\nexpr: \"i1\"", pending, StringComparison.Ordinal);
        Assert.Equal((exitCode, output, pending), (exitCodeAgain, outputAgain, pendingAgain));
        Assert.Equal(requests, requestsAgain);
    }

    [Fact]
    public void ExceptionOfTheLibraryInStep2_IsExitCode2NamingItsTypeBeforeTheOracle()
    {
        using TempDirectory root = Repository();
        using StringWriter output = new();
        using StringWriter error = new();
        using Socket bound = BoundPort();
        RunSetup setup = new(output, error, (_, _) => throw new NotImplementedException("The method or operation is not implemented."), Loopback, Port(bound), Limit, Limit, _ => Commit);

        int exitCode = Program.Run(["--seed", "7"], root.Path, DateTimeOffset.UnixEpoch, setup);

        Assert.Equal(2, exitCode);
        Assert.Equal(
            "eng/difftest: step 2, the environment of the library: the library threw NotImplementedException: The method or operation is not implemented.\n",
            error.ToString().ReplaceLineEndings("\n"));
    }

    // ---- Helpers ----

    private static string Log(string text) => JsonSerializer.Serialize(new Dictionary<string, string> { ["log"] = text });

    private static string Failed(string step) => JsonSerializer.Serialize(new Dictionary<string, string> { ["failed"] = step });

    private static Value Int(long value) => new() { Int64Value = value };

    private static Value Double(double value) => new() { DoubleValue = value };

    // A repository with what a run reads: the run manifest, go.mod of the oracle and the tables of the known signatures.
    private static TempDirectory Repository()
    {
        string repository = RepositoryRoot.Find();
        TempDirectory root = new();
        foreach (string path in new[] { ProfileTables.ManifestPath, KnownSignatures.RegistryPath, KnownSignatures.SkipListPath })
        {
            root.Write(path, File.ReadAllText(Path.Combine(repository, path)));
        }

        root.Write(OracleGoVersion.ModulePath, "module example.com/oracle\n\ngo 1.27.3\n\nrequire cel.dev/cel-go v0.32.0\n");
        return root;
    }

    // A run with seed 7 whose library side gives int 41 for every case, as the fake oracle does; the limit is the limit to
    // connect and to answer.
    private static (int ExitCode, string Output, string Error) Run(TempDirectory root, int port, TimeSpan limit, params string[] args) =>
        Run(root, port, limit, new AgreeingLibrary(), args);

    private static (int ExitCode, string Output, string Error) Run(TempDirectory root, int port, TimeSpan limit, ICaseSide library, params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        RunSetup setup = new(output, error, (_, _) => (library, string.Empty), Loopback, port, limit, limit, _ => Commit);

        int exitCode = Program.Run(["--seed", "7", .. args], root.Path, DateTimeOffset.UnixEpoch, setup);

        return (exitCode, output.ToString().ReplaceLineEndings("\n"), error.ToString().ReplaceLineEndings("\n"));
    }

    // A case of seed 7 with a library side that diverges from the fake oracle on every case that reads the bound variable i1;
    // the fake oracle writes the case of a new signature as its name and expression. A run with --case leaves pending.textproto
    // unwritten and prints its text, so the pending text is taken from the output.
    private static (int ExitCode, string Output, string Pending, List<string> Requests) RunWithLibraryThatDivergesOnI1(string number)
    {
        using FakeOracle fake = new([Handshake], answer: request => request.GetProperty("op").GetString() == "case"
            ? $$"""{"id":ID,"textproto":{{JsonSerializer.Serialize($"name: \"{request.GetProperty("name").GetString()}\"\nexpr: \"{request.GetProperty("expr").GetString()}\"")}}}"""
            : """{"id":ID,"outcome":"value","value":{"int64Value":"41"}}""");
        using TempDirectory root = Repository();
        (int exitCode, string output, string error) = Run(root, fake.Port, Limit, new LibraryThatDivergesOnI1(), ["--case", number]);
        fake.Join();
        Assert.Empty(error);
        Assert.False(File.Exists(Path.Combine(root.Path, PendingFile.Path)));
        return (exitCode, output, output, fake.Requests);
    }

    // Case 3 of seed 7 with a library side that diverges from the fake oracle on its first evaluation only, so the minimized
    // case agrees when it is run again.
    private static (int ExitCode, string Output, string Error) RunWithFlakyLibrary(params string[] args)
    {
        using FakeOracle fake = new([Handshake], answer: _ => """{"id":ID,"outcome":"value","value":{"int64Value":"41"}}""");
        using TempDirectory root = Repository();
        (int ExitCode, string Output, string Error) result = Run(root, fake.Port, Limit, new FlakyLibrary(), ["--case", "3", .. args]);
        fake.Join();
        return result;
    }

    // A socket bound to a loopback port without listening: a connection to the port is refused, and no other test can take the
    // port while the socket is open.
    private static Socket BoundPort()
    {
        Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static int Port(Socket bound) => ((IPEndPoint)bound.LocalEndPoint!).Port;

    private sealed class AgreeingLibrary : ICaseSide
    {
        public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings) => SideOutcome.Of(Int(41));

        public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) => throw new NotSupportedException();
    }

    // Gives int 42 for a case that reads the bound variable i1 and int 41, as the fake oracle does, for every other case.
    private sealed class LibraryThatDivergesOnI1 : ICaseSide
    {
        public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings) =>
            SideOutcome.Of(Int(SignatureTests.ReadsI1(expression, bindings) ? 42 : 41));

        public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) => throw new NotSupportedException();
    }

    // Gives int 42 on its first evaluation and int 41, as the fake oracle does, on every later one.
    private sealed class FlakyLibrary : ICaseSide
    {
        private bool evaluated;

        public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings)
        {
            long value = evaluated ? 41 : 42;
            evaluated = true;
            return SideOutcome.Of(Int(value));
        }

        public TimeSpan ZoneOffset(string zone, DateTimeOffset instant) => throw new NotSupportedException();
    }

    // The launcher of the oracle on a loopback socket: it accepts one connection, reads the first line, sends the given lines,
    // then answers each request with the answer for it, where "ID" stands for the id of the request, or closes the connection
    // when there is no answer function. An answer of null leaves the request unanswered. With a line for after a request, it
    // sends that line again and again after the first request, without a pause, until the run closes the connection.
    private sealed class FakeOracle : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly Thread thread;
        private readonly IReadOnlyList<string> lines;
        private readonly Func<JsonElement, string?>? answer;
        private readonly string? logsAfterRequest;
        private readonly List<string> requests = [];
        private string? firstLine;

        internal FakeOracle(IReadOnlyList<string> lines, Func<JsonElement, string?>? answer, string? logsAfterRequest = null)
        {
            this.lines = lines;
            this.answer = answer;
            this.logsAfterRequest = logsAfterRequest;
            listener.Start();
            thread = new Thread(Serve) { IsBackground = true };
            thread.Start();
        }

        internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        // Read after Join.
        internal string? FirstLine => firstLine;

        // Read after Join.
        internal List<string> Requests => requests;

        // Waits until the fake oracle has closed its connection.
        internal void Join() => Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "the fake oracle did not end");

        public void Dispose()
        {
            listener.Dispose();
            _ = thread.Join(TimeSpan.FromSeconds(30));
        }

        private void Serve()
        {
            try
            {
                using TcpClient client = listener.AcceptTcpClient();
                listener.Stop();
                using NetworkStream stream = client.GetStream();
                UTF8Encoding utf8 = new(encoderShouldEmitUTF8Identifier: false);
                using StreamReader reader = new(stream, utf8);
                using StreamWriter writer = new(stream, utf8) { NewLine = "\n", AutoFlush = true };
                firstLine = reader.ReadLine();
                foreach (string line in lines)
                {
                    writer.WriteLine(line);
                }

                while (answer is not null && reader.ReadLine() is { } request)
                {
                    requests.Add(request);
                    using JsonDocument document = JsonDocument.Parse(request);
                    string id = document.RootElement.GetProperty("id").GetRawText();
                    if (answer(document.RootElement) is { } line)
                    {
                        writer.WriteLine(line.Replace("ID", id, StringComparison.Ordinal));
                    }

                    while (logsAfterRequest is not null)
                    {
                        writer.WriteLine(logsAfterRequest);
                    }
                }
            }
            catch (Exception exception) when (exception is SocketException or IOException or ObjectDisposedException)
            {
                // The test ended the listener without a connection, or the run closed the connection.
            }
        }
    }
}
