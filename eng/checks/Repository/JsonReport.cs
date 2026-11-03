using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>Reads the JSON report of a tool from a file: the reports of lychee and of the mutation tool.</summary>
internal static class JsonReport
{
    /// <summary>Reads a report from a file and parses its root element.</summary>
    /// <typeparam name="T">The type of the parsed report.</typeparam>
    /// <param name="path">The full path of the report.</param>
    /// <param name="kind">The kind of the report in the messages, such as <c>lychee report</c>.</param>
    /// <param name="parse">
    /// Parses the root element. It throws <see cref="InvalidOperationException"/>, <see cref="FormatException"/> or
    /// <see cref="KeyNotFoundException"/> when the element is not a report of the kind.
    /// </param>
    /// <exception cref="CheckInputException">The file cannot be read or is not a report of the kind.</exception>
    internal static T Read<T>(string path, string kind, Func<JsonElement, T> parse)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CheckInputException($"the {kind} {path} cannot be read: {exception.Message}", exception);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return parse(document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new CheckInputException($"the file {path} is not a {kind}: {exception.Message.ReplaceLineEndings(" ")}", exception);
        }
    }
}
