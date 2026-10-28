using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One mutant of the report: the mutator, the replacement, the place in the source and the status.</summary>
/// <param name="Mutator">The name of the mutator, such as <c>Arithmetic</c>.</param>
/// <param name="Replacement">The code that replaces the original code.</param>
/// <param name="Start">The first character of the original code, from 1.</param>
/// <param name="End">The place after the last character of the original code.</param>
/// <param name="Status">The status, such as <c>Killed</c> or <c>Survived</c>.</param>
/// <param name="StatusReason">The reason of the status, such as <c>Removed by since filter</c>; <see langword="null"/> when the report gives none.</param>
internal sealed record ReportedMutant(string Mutator, string Replacement, Position Start, Position End, string Status, string? StatusReason);

/// <summary>One file of the report: its path as the report gives it, its source text and its mutants.</summary>
/// <param name="Path">The key of the file in the report, an absolute path of the machine that ran the tool.</param>
/// <param name="Source">The text of the source file as the tool read it.</param>
/// <param name="Mutants">The mutants of the file, in the order of the report.</param>
internal sealed record ReportedFile(string Path, string Source, IReadOnlyList<ReportedMutant> Mutants);

/// <summary>
/// The JSON report of Stryker.NET in the mutation-testing-elements schema: for every file its source and its mutants
/// (<c>docs/design/eng-mutate.md</c>, "mutation").
/// </summary>
internal sealed class MutationReport
{
    private MutationReport(string? projectRoot, IReadOnlyList<ReportedFile> files)
    {
        ProjectRoot = projectRoot;
        Files = files;
    }

    /// <summary>A report without any file, the report of a run that did not write one.</summary>
    internal static MutationReport Empty { get; } = new(null, []);

    /// <summary>The directory of the project under test as the report gives it, or <see langword="null"/> when it gives none.</summary>
    internal string? ProjectRoot { get; }

    /// <summary>The files of the report.</summary>
    internal IReadOnlyList<ReportedFile> Files { get; }

    /// <summary>Reads a report from a file.</summary>
    /// <param name="path">The full path of the report.</param>
    /// <exception cref="CheckInputException">The file cannot be read or is not a report.</exception>
    internal static MutationReport Read(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new CheckInputException($"the mutation report {path} cannot be read: {exception.Message}", exception);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            return Parse(document.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            throw new CheckInputException($"the file {path} is not a mutation report: {exception.Message.ReplaceLineEndings(" ")}", exception);
        }
    }

    private static MutationReport Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("files", out JsonElement files) || files.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("it has no \"files\" object");
        }

        List<ReportedFile> parsed = [];
        foreach (JsonProperty file in files.EnumerateObject())
        {
            string source = Text(file.Value, "source") ?? string.Empty;
            List<ReportedMutant> mutants = [];
            if (file.Value.TryGetProperty("mutants", out JsonElement list))
            {
                foreach (JsonElement mutant in list.EnumerateArray())
                {
                    mutants.Add(ParseMutant(mutant));
                }
            }

            parsed.Add(new ReportedFile(file.Name, source, mutants));
        }

        return new MutationReport(Text(root, "projectRoot"), parsed);
    }

    private static ReportedMutant ParseMutant(JsonElement mutant)
    {
        JsonElement location = mutant.GetProperty("location");
        return new ReportedMutant(
            Text(mutant, "mutatorName") ?? string.Empty,
            Text(mutant, "replacement") ?? string.Empty,
            ParsePosition(location.GetProperty("start")),
            ParsePosition(location.GetProperty("end")),
            mutant.GetProperty("status").GetString() ?? string.Empty,
            Text(mutant, "statusReason"));
    }

    private static Position ParsePosition(JsonElement position) =>
        new(position.GetProperty("line").GetInt32(), position.GetProperty("column").GetInt32());

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
