using System;
using System.Collections.Generic;
using System.Globalization;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>A file of the Unicode Character Database that the generator reads.</summary>
/// <param name="Name">The name of the file, which is also its name in the download directory.</param>
/// <param name="Sha256">The SHA-256 of the file as 64 lower case hexadecimal digits.</param>
/// <param name="Address">The address the file is downloaded from.</param>
internal sealed record UcdFile(string Name, string Sha256, Uri Address);

/// <summary>
/// The content of <c>eng/unicode/inputs.txt</c> (docs/design/unicode-tables.md, "The data"): the version of the database and,
/// for each of the three files the generator reads, its name, its SHA-256 and its address.
/// </summary>
internal sealed class UcdInputs
{
    /// <summary>The name of the file with the general categories.</summary>
    internal const string UnicodeDataName = "UnicodeData.txt";

    /// <summary>The name of the file with the scripts.</summary>
    internal const string ScriptsName = "Scripts.txt";

    /// <summary>The name of the file with the case folding.</summary>
    internal const string CaseFoldingName = "CaseFolding.txt";

    private const string InputsName = "inputs.txt";

    private UcdInputs(string version, IReadOnlyList<UcdFile> files)
    {
        Version = version;
        Files = files;
    }

    /// <summary>The version of the database, such as <c>17.0.0</c>.</summary>
    internal string Version { get; }

    /// <summary>The three files: <c>UnicodeData.txt</c>, <c>Scripts.txt</c> and <c>CaseFolding.txt</c>, in that order.</summary>
    internal IReadOnlyList<UcdFile> Files { get; }

    /// <summary>
    /// Reads the text of <c>inputs.txt</c>. A line is empty, a comment that starts with <c>#</c>, <c>version &lt;version&gt;</c>,
    /// or <c>file &lt;name&gt; &lt;sha256&gt; &lt;address&gt;</c>, the fields separated by blanks. The version and each of the
    /// three files come once.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <exception cref="UcdFormatException">The text has another line, a file twice, or lacks the version or a file.</exception>
    internal static UcdInputs Parse(string text)
    {
        string? version = null;
        Dictionary<string, UcdFile> files = new(StringComparer.Ordinal);
        int number = 0;
        foreach (string raw in text.Split('\n'))
        {
            number++;
            string line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            string[] fields = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            switch (fields[0])
            {
                case "version" when fields.Length == 2 && version is null:
                    version = fields[1];
                    break;
                case "file" when fields.Length == 4:
                    UcdFile file = ReadFile(fields, number);
                    if (!files.TryAdd(file.Name, file))
                    {
                        throw new UcdFormatException(InputsName, number, $"the file {file.Name} is listed twice");
                    }

                    break;
                default:
                    throw new UcdFormatException(InputsName, number, "expected 'version <version>' once or 'file <name> <sha256> <address>'");
            }
        }

        if (version is null)
        {
            throw new UcdFormatException(InputsName, 0, "there is no 'version' line");
        }

        List<UcdFile> ordered = [];
        foreach (string name in new[] { UnicodeDataName, ScriptsName, CaseFoldingName })
        {
            ordered.Add(files.TryGetValue(name, out UcdFile? file)
                ? file
                : throw new UcdFormatException(InputsName, 0, $"there is no 'file' line for {name}"));
        }

        if (files.Count != ordered.Count)
        {
            throw new UcdFormatException(InputsName, 0, "a file other than " + string.Join(", ", ordered.ConvertAll(file => file.Name)) + " is listed");
        }

        return new UcdInputs(version, ordered);
    }

    private static UcdFile ReadFile(string[] fields, int line)
    {
        string sha256 = fields[2];
        if (sha256.Length != 64 || !IsLowerHexadecimal(sha256))
        {
            throw new UcdFormatException(InputsName, line, $"the SHA-256 of {fields[1]} is not 64 lower case hexadecimal digits");
        }

        if (!Uri.TryCreate(fields[3], UriKind.Absolute, out Uri? address) || address.Scheme != Uri.UriSchemeHttps)
        {
            throw new UcdFormatException(InputsName, line, $"the address of {fields[1]} is not an absolute https address");
        }

        return new UcdFile(fields[1], sha256, address);
    }

    private static bool IsLowerHexadecimal(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsAsciiDigit(c) && (c < 'a' || c > 'f'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The line of the header of a generated file that names a file and its SHA-256.</summary>
    /// <param name="file">The file.</param>
    internal static string HeaderLine(UcdFile file) =>
        string.Create(CultureInfo.InvariantCulture, $"//   {file.Name,-16} {file.Sha256}");
}
