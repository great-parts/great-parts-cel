using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The settings of a Stryker.NET configuration file that <c>mutation</c> needs: the project under test, whether <c>since</c> is enabled
/// and the <c>break</c> threshold with its place in the file (<c>docs/design/eng-mutate.md</c>, "mutation"). The settings stand
/// under the key <c>stryker-config</c>, or at the top of the file.
/// </summary>
internal sealed class MutationConfig
{
    private const string Section = "stryker-config";

    private MutationConfig(string? project, bool sinceEnabled, double? breakThreshold, Position breakPlace)
    {
        Project = project;
        SinceEnabled = sinceEnabled;
        Break = breakThreshold;
        BreakPlace = breakPlace;
    }

    /// <summary>The project file under test, relative to the repository root, or <see langword="null"/> when the file names none.</summary>
    internal string? Project { get; }

    /// <summary>Whether <c>since</c> is enabled: only the files changed against <c>main</c> are mutated.</summary>
    internal bool SinceEnabled { get; }

    /// <summary>The threshold <c>break</c> of the thresholds, or <see langword="null"/> when the file sets none.</summary>
    internal double? Break { get; }

    /// <summary>The place of the key <c>break</c> in the file; meaningful only when <see cref="Break"/> is set.</summary>
    internal Position BreakPlace { get; }

    /// <summary>Reads the settings from a file.</summary>
    /// <param name="path">The full path of the configuration file.</param>
    /// <exception cref="CheckInputException">The file cannot be read or is not JSON, or a setting has a value of another kind.</exception>
    internal static MutationConfig Read(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CheckInputException($"the configuration file {path} cannot be read: {exception.Message}", exception);
        }

        try
        {
            return Parse(text);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new CheckInputException($"the configuration file {path} is not valid: {exception.Message.ReplaceLineEndings(" ")}", exception);
        }
    }

    // One pass over the tokens: a value is found by the names of the properties that lead to it.
    private static MutationConfig Parse(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        Utf8JsonReader reader = new(bytes, new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        SourceLines lines = new(text);
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
        {
            throw new InvalidOperationException("it is not a JSON object");
        }

        // The names of the properties that hold the containers around the token; null for the root and for an element of an array.
        List<string?> containers = [null];
        string? name = null;
        int nameStart = 0;
        string? project = null;
        bool sinceEnabled = false;
        double? breakThreshold = null;
        Position breakPlace = new(1, 1);
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
                    name = reader.GetString();
                    nameStart = Encoding.UTF8.GetCharCount(bytes, 0, (int)reader.TokenStartIndex);
                    break;
                case JsonTokenType.StartObject or JsonTokenType.StartArray:
                    containers.Add(name);
                    name = null;
                    break;
                case JsonTokenType.EndObject or JsonTokenType.EndArray:
                    containers.RemoveAt(containers.Count - 1);
                    break;
                default:
                    switch (Setting(containers, name))
                    {
                        case "project":
                            project = reader.TokenType == JsonTokenType.String ? reader.GetString() : throw new InvalidOperationException("\"project\" is not a string");
                            break;
                        case "since/enabled":
                            sinceEnabled = reader.TokenType is JsonTokenType.True or JsonTokenType.False ? reader.GetBoolean() : throw new InvalidOperationException("\"enabled\" of \"since\" is not true or false");
                            break;
                        case "thresholds/break":
                            breakThreshold = reader.TokenType == JsonTokenType.Number ? reader.GetDouble() : throw new InvalidOperationException("\"break\" of \"thresholds\" is not a number");
                            breakPlace = lines.Locate(nameStart);
                            break;
                    }

                    name = null;
                    break;
            }
        }

        return new MutationConfig(project, sinceEnabled, breakThreshold, breakPlace);
    }

    // The names of the properties that lead to a value, without the section name, joined with a slash; empty for a value that is
    // not in a property, such as an element of an array.
    private static string Setting(List<string?> containers, string? name)
    {
        if (name is null)
        {
            return string.Empty;
        }

        List<string> names = [.. containers.OfType<string>(), name];
        return string.Join('/', names.Skip(names[0] == Section ? 1 : 0));
    }
}
