using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using Cel.Expr;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Great.Parts.Cel.Eng.Difftest.Numbers;
using Great.Parts.Cel.Eng.Findings;
using Duration = Google.Protobuf.WellKnownTypes.Duration;
using Timestamp = Google.Protobuf.WellKnownTypes.Timestamp;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>The handshake of the oracle: the line it writes before it answers requests.</summary>
/// <param name="Oracle">The module and the version of cel-go, such as <c>cel.dev/cel-go v0.32.0</c>.</param>
/// <param name="Go">The Go that built the oracle, such as <c>go1.27.3</c>.</param>
internal sealed record OracleHandshake(string Oracle, string Go)
{
    /// <summary>The version of cel-go without the module and the <c>v</c>, such as <c>0.32.0</c>, for the journal line.</summary>
    internal string Version
    {
        get
        {
            string version = Oracle[(Oracle.LastIndexOf(' ') + 1)..];
            return version.StartsWith('v') ? version[1..] : version;
        }
    }
}

/// <summary>
/// The session with the oracle (docs/design/eng-difftest.md, "A run", step 3; docs/design/eng-oracle.md, "Launcher",
/// "Protocol"): the connection to the launcher of the oracle container, the path of the run manifest, the <c>log</c> lines of
/// its steps, the handshake, and then one request and one answer per line. A <c>failed</c> line, a closed connection, no
/// connection within the limit and no answer within the limit after the handshake throw <see cref="IOException"/>; a line or
/// an answer that does not read, and a binding whose text does not read back to its value (<see cref="BindingJson"/>), throw
/// <see cref="SideFailureException"/>, whose message quotes the line or the text only in
/// <see cref="SideFailureException.QuotedMessage"/>. Both end the run with exit code 2.
/// </summary>
internal sealed class OracleConnection : ICaseSide, ICaseRecorder, IDisposable
{
    /// <summary>The host of the oracle in the network of the Compose project.</summary>
    internal const string DefaultHost = "oracle";

    /// <summary>The port on which the launcher of the oracle listens.</summary>
    internal const int DefaultPort = 7070;

    /// <summary>The command that shows why the oracle container did not listen.</summary>
    internal const string LogsCommand = "docker compose -f eng/compose.yaml logs oracle";

    /// <summary>How long the run tries to connect: the container of the oracle starts with the run.</summary>
    internal static readonly TimeSpan DefaultConnectLimit = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long the run waits for the answer to a request after the handshake: a case of the profile takes the oracle
    /// milliseconds. The steps before the handshake have no limit.
    /// </summary>
    internal static readonly TimeSpan DefaultAnswerLimit = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan RetryPause = TimeSpan.FromMilliseconds(250);

    // Reads the values of an answer: a value of time is an Any of a message of the two types, which the parser must know.
    private static readonly JsonParser ValueParser = new(JsonParser.Settings.Default.WithTypeRegistry(
        TypeRegistry.FromMessages(Timestamp.Descriptor, Duration.Descriptor)));

    private readonly TcpClient client;
    private readonly NetworkStream stream;
    private readonly StreamReader reader;
    private readonly Action<string> log;
    private readonly TimeSpan answerLimit;
    private long nextId;

    private OracleConnection(TcpClient client, TimeSpan answerLimit, Action<string> log)
    {
        this.client = client;
        this.answerLimit = answerLimit;
        this.log = log;
        stream = client.GetStream();
        reader = new StreamReader(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: false);
    }

    /// <summary>The handshake of the oracle.</summary>
    internal OracleHandshake Handshake { get; private set; } = new(string.Empty, string.Empty);

    /// <summary>
    /// Connects to the launcher of the oracle, sends the path of the run manifest and reads up to the handshake. Each
    /// <c>log</c> line goes to <paramref name="log"/> unchanged, here and in the rest of the session.
    /// </summary>
    /// <param name="host">The host of the launcher.</param>
    /// <param name="port">The port of the launcher.</param>
    /// <param name="limit">How long to try to connect.</param>
    /// <param name="answerLimit">How long to wait for the answer to a request after the handshake.</param>
    /// <param name="manifestPath">The path of the run manifest from the repository root.</param>
    /// <param name="log">Receives the text of each <c>log</c> line.</param>
    /// <returns>The open session after the handshake.</returns>
    /// <exception cref="IOException">No connection within the limit, a <c>failed</c> line or a closed connection.</exception>
    /// <exception cref="SideFailureException">A line that does not read.</exception>
    internal static OracleConnection Open(string host, int port, TimeSpan limit, TimeSpan answerLimit, string manifestPath, Action<string> log)
    {
        OracleConnection? connection = null;
        try
        {
            connection = new(Connect(host, port, limit), answerLimit, log);
            connection.Send(writer => writer.WriteString("env", manifestPath));
            using JsonDocument handshake = connection.Receive("the handshake", request: null);
            JsonElement root = handshake.RootElement;
            if (!root.TryGetProperty("oracle", out JsonElement oracle) || oracle.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("go", out JsonElement go) || go.ValueKind != JsonValueKind.String)
            {
                throw Quoting("the oracle sent no handshake", root.GetRawText());
            }

            connection.Handshake = new OracleHandshake(oracle.GetString()!, go.GetString()!);
            OracleConnection opened = connection;
            connection = null;
            return opened;
        }
        finally
        {
            connection?.Dispose();
        }
    }

    /// <inheritdoc/>
    public SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings)
    {
        long id = Request("eval", expression, bindings, null, null);
        using JsonDocument answer = Answer(id);
        JsonElement root = answer.RootElement;
        string outcome = Text(root, "outcome", id);
        return outcome switch
        {
            "value" when root.TryGetProperty("value", out JsonElement value) => SideOutcome.Of(ValueParser.Parse<Value>(value.GetRawText())),
            "eval-error" => SideOutcome.EvaluationError(Text(root, "message", id)),
            "compile-error" => SideOutcome.CompileError(Stage(root, id), Text(root, "message", id)),
            _ => throw Quoting(string.Create(CultureInfo.InvariantCulture, $"the answer of the oracle to request {id} has no outcome of the protocol"), root.GetRawText()),
        };
    }

    /// <inheritdoc/>
    public string Case(string name, string description, string expression, IReadOnlyDictionary<string, Value> bindings)
    {
        long id = Request("case", expression, bindings, name, description);
        using JsonDocument answer = Answer(id);
        return Text(answer.RootElement, "textproto", id);
    }

    /// <summary>
    /// The request <c>offset</c> of the oracle (docs/design/eng-oracle.md, "Protocol"): the offset that <c>time.LoadLocation</c>
    /// gives for the zone at the instant, in seconds.
    /// </summary>
    /// <param name="zone">The name of the zone in the time zone data file.</param>
    /// <param name="instant">The instant, in whole seconds.</param>
    /// <returns>The offset from UTC.</returns>
    /// <exception cref="SideFailureException">The oracle does not load the zone, or sends an answer that does not read.</exception>
    public TimeSpan ZoneOffset(string zone, DateTimeOffset instant)
    {
        long id = nextId++;
        Send(request =>
        {
            request.WriteNumber("id", id);
            request.WriteString("op", "offset");
            request.WriteString("zone", zone);
            request.WriteNumber("instant", instant.ToUnixTimeSeconds());
        });
        using JsonDocument answer = Answer(id);
        JsonElement root = answer.RootElement;
        string outcome = Text(root, "outcome", id);
        if (outcome == "offset" && root.TryGetProperty("seconds", out JsonElement seconds) && seconds.TryGetInt64(out long offset))
        {
            return TimeSpan.FromSeconds(offset);
        }

        throw outcome == "zone-error"
            ? new SideFailureException(string.Create(CultureInfo.InvariantCulture, $"the oracle does not load the time zone '{zone}': {Text(root, "message", id)}"))
            : Quoting(string.Create(CultureInfo.InvariantCulture, $"the answer of the oracle to request {id} has no offset"), root.GetRawText());
    }

    /// <summary>Closes the connection: standard input of the oracle ends, and the oracle and its container end.</summary>
    public void Dispose()
    {
        reader.Dispose();
        stream.Dispose();
        client.Dispose();
    }

    // Tries to connect until the limit: the launcher listens only after the container started and compiled it.
    private static TcpClient Connect(string host, int port, TimeSpan limit)
    {
        Stopwatch waited = Stopwatch.StartNew();
        while (true)
        {
            TcpClient client = new();
            try
            {
                client.Connect(host, port);
                return client;
            }
            catch (SocketException exception)
            {
                client.Dispose();
                if (waited.Elapsed + RetryPause >= limit)
                {
                    throw new IOException(string.Create(
                        CultureInfo.InvariantCulture,
                        $"no connection to the oracle at {host}:{port} within {limit.TotalSeconds:0} s ({exception.Message}); the log of its container: {LogsCommand}, with the same -p <name> when the run had one"));
                }

                Thread.Sleep(RetryPause);
            }
        }
    }

    /// <summary>
    /// The protojson of a binding for a request. A <c>double</c>, also in a list or a map, is written by
    /// <see cref="DoubleRoundTrip"/>, and NaN and the infinities as the strings of protojson: the formatter of Google.Protobuf
    /// writes a <c>double</c> with <c>"r"</c>, which on .NET 10 gives for some values, such as 2^-25, the text of the
    /// neighbouring <c>double</c>. Lists and maps are written element by element; every other kind goes through that formatter. The text is then read back and compared with the value, a <c>double</c> bit for bit, so a value nested in
    /// a list or a map that the formatter writes as another value fails the run instead of reaching the oracle.
    /// </summary>
    /// <param name="variable">The name of the variable.</param>
    /// <param name="value">The value bound to it.</param>
    /// <exception cref="SideFailureException">The text does not read back to the value.</exception>
    internal static string BindingJson(string variable, Value value)
    {
        string json = ValueJson(value);
        CheckReadsBack(variable, value, json);
        return json;
    }

    /// <summary>Checks that the protojson of a binding reads back to the bound value, a <c>double</c> bit for bit.</summary>
    /// <param name="variable">The name of the variable.</param>
    /// <param name="value">The value bound to it.</param>
    /// <param name="json">The protojson written for it.</param>
    /// <exception cref="SideFailureException">The text reads back to another value; only the quoted message has the text.</exception>
    internal static void CheckReadsBack(string variable, Value value, string json)
    {
        // The generated equality of a message compares double fields by their bits: NaN equals NaN, -0.0 differs from 0.0.
        if (!JsonParser.Default.Parse<Value>(json).Equals(value))
        {
            throw Quoting($"the binding of {variable} does not read back to the bound value", json);
        }
    }

    // The protojson of a value: a double by its shortest text, lists and maps element by element, other kinds by the formatter.
    private static string ValueJson(Value value) => value.KindCase switch
    {
        Value.KindOneofCase.DoubleValue => "{\"doubleValue\":" + DoubleJson(value.DoubleValue) + "}",
        Value.KindOneofCase.ListValue => "{\"listValue\":{\"values\":[" + string.Join(',', value.ListValue.Values.Select(ValueJson)) + "]}}",
        Value.KindOneofCase.MapValue => "{\"mapValue\":{\"entries\":["
            + string.Join(',', value.MapValue.Entries.Select(entry => "{\"key\":" + ValueJson(entry.Key) + ",\"value\":" + ValueJson(entry.Value) + "}"))
            + "]}}",
        _ => JsonFormatter.Default.Format(value),
    };

    // A double in protojson: NaN and the infinities as strings, a finite value, -0.0 included, as its shortest number.
    private static string DoubleJson(double value) => value switch
    {
        double.NaN => "\"NaN\"",
        double.PositiveInfinity => "\"Infinity\"",
        double.NegativeInfinity => "\"-Infinity\"",
        _ => DoubleRoundTrip.Format(value),
    };

    private static CompileStage Stage(JsonElement answer, long id)
    {
        string stage = Text(answer, "stage", id);
        return stage switch
        {
            "parse" => CompileStage.Parse,
            "check" => CompileStage.Check,
            "program" => CompileStage.Program,
            _ => throw Quoting(
                string.Create(CultureInfo.InvariantCulture, $"the answer of the oracle to request {id} has the stage '{stage}', which the protocol does not have"),
                answer.GetRawText()),
        };
    }

    private static string Text(JsonElement answer, string name, long id) =>
        answer.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : throw Quoting(string.Create(CultureInfo.InvariantCulture, $"the answer of the oracle to request {id} has no '{name}'"), answer.GetRawText());

    // The message of a line or an answer that does not read, without the quote and with it.
    private static SideFailureException Quoting(string message, string quote) => new(message, message + ": " + quote);

    private long Request(string op, string expression, IReadOnlyDictionary<string, Value> bindings, string? name, string? description)
    {
        long id = nextId++;
        Send(request =>
        {
            request.WriteNumber("id", id);
            request.WriteString("op", op);
            request.WriteString("expr", expression);
            request.WriteStartObject("bindings");
            foreach ((string variable, Value value) in bindings)
            {
                request.WritePropertyName(variable);
                request.WriteRawValue(BindingJson(variable, value));
            }

            request.WriteEndObject();
            if (name is not null)
            {
                request.WriteString("name", name);
                request.WriteString("description", description);
            }
        });
        return id;
    }

    private JsonDocument Answer(long id)
    {
        JsonDocument answer = Receive(string.Create(CultureInfo.InvariantCulture, $"the answer to request {id}"), id);
        if (!answer.RootElement.TryGetProperty("id", out JsonElement answered)
            || !answered.TryGetInt64(out long answeredId)
            || answeredId != id)
        {
            string text = answer.RootElement.GetRawText();
            answer.Dispose();
            throw Quoting(string.Create(CultureInfo.InvariantCulture, $"the oracle answered request {id} with a line without its id"), text);
        }

        return answer;
    }

    // Writes one JSON object as one line.
    private void Send(Action<Utf8JsonWriter> write)
    {
        using MemoryStream line = new();
        using (Utf8JsonWriter json = new(line))
        {
            json.WriteStartObject();
            write(json);
            json.WriteEndObject();
        }

        line.WriteByte((byte)'\n');
        try
        {
            stream.Write(line.GetBuffer().AsSpan(0, (int)line.Length));
        }
        catch (IOException exception)
        {
            throw new IOException("the oracle closed the connection: " + exception.Message, exception);
        }
    }

    // Reads lines up to one that is neither log nor failed: log lines go on unchanged, failed and the end of the connection throw.
    // The answer to a request, its log lines included, comes within the answer limit; the handshake has no limit.
    private JsonDocument Receive(string expected, long? request)
    {
        long started = Stopwatch.GetTimestamp();
        while (true)
        {
            stream.ReadTimeout = request is null
                ? Timeout.Infinite
                : Math.Max(1, (int)Math.Ceiling((answerLimit - Stopwatch.GetElapsedTime(started)).TotalMilliseconds));
            string? line;
            try
            {
                line = reader.ReadLine();
            }
            catch (IOException exception) when (request is { } id && exception.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut })
            {
                throw NoAnswer(id, exception);
            }
            catch (IOException exception)
            {
                throw new IOException($"the oracle closed the connection before {expected}: {exception.Message}", exception);
            }

            if (line is null)
            {
                throw new IOException($"the oracle closed the connection before {expected}; the log lines above say why");
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                throw Quoting("the oracle sent a line that is not JSON", line);
            }

            JsonElement root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("log", out JsonElement text) && text.ValueKind == JsonValueKind.String)
            {
                log(text.GetString()!);
                document.Dispose();

                // The reader gives buffered lines without a read of the socket, so the read timeout alone does not end the wait.
                if (request is { } waiting && Stopwatch.GetElapsedTime(started) >= answerLimit)
                {
                    throw NoAnswer(waiting, cause: null);
                }

                continue;
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("failed", out JsonElement step) && step.ValueKind == JsonValueKind.String)
            {
                string failed = step.GetString()!;
                document.Dispose();
                throw new IOException($"the oracle failed at the step '{failed}'; the log lines above say why");
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw Quoting("the oracle sent a line that is not a JSON object", line);
            }

            return document;
        }
    }

    private IOException NoAnswer(long request, IOException? cause) => new(
        string.Create(CultureInfo.InvariantCulture, $"the oracle did not answer request {request} within {answerLimit.TotalSeconds:0.###} seconds"),
        cause);
}

/// <summary>
/// The version of Go of the oracle (docs/design/eng-oracle.md, "Version of Go"): the <c>go</c> line of <c>eng/oracle/go.mod</c>
/// against the Go that the handshake names; another Go is <c>ENG821</c>, and the run stops before the first case.
/// </summary>
internal static class OracleGoVersion
{
    /// <summary>The module file of the oracle, relative to the repository root.</summary>
    internal const string ModulePath = "eng/oracle/go.mod";

    private const string GoDirective = "go ";

    /// <summary>Compares the Go of the handshake with the <c>go</c> line of the module file.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="handshakeGo">The Go that the handshake names, such as <c>go1.27.3</c>.</param>
    /// <returns>The <c>ENG821</c> finding at the <c>go</c> line, column 1, or <see langword="null"/> when the versions are equal.</returns>
    /// <exception cref="IOException">The module file has no <c>go</c> line.</exception>
    internal static Finding? Check(string root, string handshakeGo)
    {
        string[] lines = File.ReadAllText(Path.Combine(root, ModulePath)).Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd('\r');
            if (!line.StartsWith(GoDirective, StringComparison.Ordinal))
            {
                continue;
            }

            string pinned = line[GoDirective.Length..].Trim();
            return handshakeGo == "go" + pinned
                ? null
                : new Finding(
                    ModulePath,
                    index + 1,
                    1,
                    FindingSeverity.Error,
                    EngCodes.Eng821,
                    $"the Go of the oracle differs from the go line: the handshake names {handshakeGo}, {ModulePath} has go {pinned}; the image tag of the service oracle and the go line change together");
        }

        throw new IOException($"{ModulePath} has no go line");
    }
}
