using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One entry of an object of <c>eng/ratchet.json</c>: the key, the number and the place of the key in the file.</summary>
/// <param name="Key">The key: a rule, or the name of a form without a rule.</param>
/// <param name="Value">The number; not negative.</param>
/// <param name="Position">The line and the column of the opening quote of the key.</param>
internal readonly record struct RatchetEntry(string Key, int Value, Position Position);

/// <summary>
/// The baseline <c>eng/ratchet.json</c> (<c>docs/design/eng-checks-source.md</c>, "ratchet"). The reader works on the bytes with
/// <see cref="Utf8JsonReader"/> and keeps the place of every key. What is wrong with the file is <see cref="Problems"/>, one
/// <c>ENG600</c> at the place of each; the numbers that the reader could take from the file are kept even then.
/// </summary>
internal sealed class RatchetFile
{
    /// <summary>The path of the file, relative to the root.</summary>
    internal const string Path = "eng/ratchet.json";

    internal const string CodeMetricsKey = "codeMetrics";
    internal const string DuplicatesKey = "duplicates";
    internal const string SuppressionsKey = "suppressions";

    private const string ScanName = "ratchet-file";
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private readonly byte[] _json;
    private readonly SourceLines _lines;
    private readonly List<Finding> _problems = [];
    private readonly List<RatchetEntry> _codeMetrics = [];
    private readonly List<RatchetEntry> _suppressions = [];

    private RatchetFile(byte[] json)
    {
        _json = json;
        _lines = new SourceLines(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(json));
        Root = new Position(1, 1);
        CodeMetricsAt = SuppressionsAt = DuplicatesAt = Root;
    }

    /// <summary>One <c>ENG600</c> per place of the file that is not valid; empty when the file is valid.</summary>
    internal IReadOnlyList<Finding> Problems => _problems;

    /// <summary>The number of pairs that the file allows; 0 when the file does not give a valid one.</summary>
    internal int Duplicates { get; private set; }

    /// <summary>The thresholds of <c>codeMetrics</c>, in the order of the file.</summary>
    internal IReadOnlyList<RatchetEntry> CodeMetrics => _codeMetrics;

    /// <summary>The numbers of <c>suppressions</c>, in the order of the file.</summary>
    internal IReadOnlyList<RatchetEntry> Suppressions => _suppressions;

    /// <summary>The place of the root object; where a finding goes when the key that it names is not in the file.</summary>
    internal Position Root { get; private set; }

    /// <summary>The place of the key <c>codeMetrics</c>, or <see cref="Root"/> when the file has none.</summary>
    internal Position CodeMetricsAt { get; private set; }

    /// <summary>The place of the key <c>duplicates</c>, or <see cref="Root"/> when the file has none.</summary>
    internal Position DuplicatesAt { get; private set; }

    /// <summary>The place of the key <c>suppressions</c>, or <see cref="Root"/> when the file has none.</summary>
    internal Position SuppressionsAt { get; private set; }

    /// <summary>Returns the baseline of the run, reading the file on the first call.</summary>
    /// <param name="context">The context of the run.</param>
    internal static RatchetFile Of(CheckContext context) => context.Scan(ScanName, () => Read(context.Root));

    /// <summary>Reads the file of the repository. A missing file is an input that does not exist, not a problem of the file.</summary>
    /// <param name="root">The full path of the repository root.</param>
    /// <exception cref="CheckInputException">The file does not exist.</exception>
    internal static RatchetFile Read(string root)
    {
        string full = System.IO.Path.Combine(root, Path);
        if (!File.Exists(full))
        {
            throw new CheckInputException($"{Path} does not exist");
        }

        return Parse(File.ReadAllBytes(full));
    }

    /// <summary>Reads the content of the file.</summary>
    /// <param name="json">The bytes of the file.</param>
    internal static RatchetFile Parse(byte[] json)
    {
        byte[] content = json.AsSpan().StartsWith(Utf8Bom) ? json[Utf8Bom.Length..] : json;
        RatchetFile file = new(content);
        file.Load();
        return file;
    }

    private void Load()
    {
        Utf8JsonReader reader = new(_json);
        try
        {
            if (reader.Read())
            {
                Root = At(reader.TokenStartIndex);
                LoadRoot(ref reader);
            }

            // Anything after the root value is an error of syntax; the reader reports it.
            _ = reader.Read();
        }
        catch (JsonException exception)
        {
            Duplicates = 0;
            Problem(AtLine(exception.LineNumber ?? 0, exception.BytePositionInLine ?? 0), "the file is not valid JSON");
        }
    }

    private void LoadRoot(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            Problem(Root, "the file must hold one JSON object");
            reader.Skip();
            return;
        }

        KeyOrder order = new();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string key = reader.GetString()!;
            Position at = At(reader.TokenStartIndex);
            bool fresh = order.Accept(key, at, this);
            reader.Read();
            switch (key)
            {
                case CodeMetricsKey:
                    CodeMetricsAt = fresh ? at : CodeMetricsAt;
                    LoadMap(ref reader, key, at, fresh ? _codeMetrics : [], zeroAllowed: true);
                    break;
                case SuppressionsKey:
                    SuppressionsAt = fresh ? at : SuppressionsAt;
                    LoadMap(ref reader, key, at, fresh ? _suppressions : [], zeroAllowed: false);
                    break;
                case DuplicatesKey:
                    DuplicatesAt = fresh ? at : DuplicatesAt;
                    int? number = LoadNumber(ref reader, key, at);
                    Duplicates = fresh && number is { } value ? value : Duplicates;
                    break;
                default:
                    Problem(at, $"unknown key '{key}'; the keys are {CodeMetricsKey}, {DuplicatesKey} and {SuppressionsKey}");
                    reader.Skip();
                    break;
            }
        }
    }

    private void LoadMap(ref Utf8JsonReader reader, string name, Position nameAt, List<RatchetEntry> into, bool zeroAllowed)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            Problem(nameAt, $"the value of '{name}' must be an object of numbers");
            reader.Skip();
            return;
        }

        KeyOrder order = new();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            string key = reader.GetString()!;
            Position at = At(reader.TokenStartIndex);
            bool fresh = order.Accept(key, at, this);
            reader.Read();
            int? number = LoadNumber(ref reader, key, at);
            if (number == 0 && !zeroAllowed)
            {
                Problem(at, $"'{key}' is 0 in {name}; a rule without a place is not written");
            }
            else if (number is { } value && fresh)
            {
                into.Add(new RatchetEntry(key, value, at));
            }
        }
    }

    // The current token must be a number that is an integer and not negative; any other value is a problem at the key.
    private int? LoadNumber(ref Utf8JsonReader reader, string key, Position keyAt)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out int value) && value >= 0)
        {
            return value;
        }

        Problem(keyAt, $"the value of '{key}' must be an integer that is not negative");
        reader.Skip();
        return null;
    }

    private Position At(long byteOffset) => _lines.Locate(Encoding.UTF8.GetCharCount(_json, 0, (int)byteOffset));

    // The reader counts a line by line feeds and a column in bytes, both from 0.
    private Position AtLine(long line, long byteInLine)
    {
        int offset = 0;
        for (long seen = 0; seen < line && offset < _json.Length; offset++)
        {
            seen += _json[offset] == (byte)'\n' ? 1 : 0;
        }

        return At(Math.Min(offset + byteInLine, _json.Length));
    }

    private void Problem(Position at, string message) =>
        _problems.Add(new Finding(Path, at.Line, at.Column, FindingSeverity.Error, EngCodes.Eng600, message));

    // The keys of one object must be written once each and in ordinal order.
    private sealed class KeyOrder
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private string? _previous;

        // Returns false for a repeated key; its number is then not used.
        internal bool Accept(string key, Position at, RatchetFile file)
        {
            if (!_seen.Add(key))
            {
                file.Problem(at, $"repeated key '{key}'");
                return false;
            }

            if (_previous is not null && string.CompareOrdinal(key, _previous) < 0)
            {
                file.Problem(at, $"key '{key}' is out of order: it must come before '{_previous}'");
            }

            _previous = key;
            return true;
        }
    }
}
