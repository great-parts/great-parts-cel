using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Great.Parts.Cel.Eng.Tests.Support;
using Great.Parts.Cel.Eng.Unicode;
using GeneratorProgram = Great.Parts.Cel.Eng.Unicode.Program;

namespace Great.Parts.Cel.Eng.Tests.Unicode;

// Small excerpts of the three files of the Unicode Character Database in the form of the real files, and the repository around
// them that the generator reads. No test of the generator uses the network or the real files.
internal static class Excerpts
{
    // A range as two lines, a single surrogate range, the unassigned in between, and the code points of four orbits.
    internal const string UnicodeData = """
        0041;LATIN CAPITAL LETTER A;Lu;0;L;;;;;N;;;;0061;
        0042;LATIN CAPITAL LETTER B;Lu;0;L;;;;;N;;;;0062;
        004B;LATIN CAPITAL LETTER K;Lu;0;L;;;;;N;;;;006B;
        0061;LATIN SMALL LETTER A;Ll;0;L;;;;;N;;;0041;;0041
        006B;LATIN SMALL LETTER K;Ll;0;L;;;;;N;;;004B;;004B
        00B5;MICRO SIGN;Ll;0;L;<compat> 03BC;;;;N;;;039C;;039C
        00DF;LATIN SMALL LETTER SHARP S;Ll;0;L;;;;;N;;;;;
        0345;COMBINING GREEK YPOGEGRAMMENI;Mn;240;NSM;;;;;N;GREEK NON-SPACING IOTA BELOW;;0399;;0399
        0399;GREEK CAPITAL LETTER IOTA;Lu;0;L;;;;;N;;;;03B9;
        03B9;GREEK SMALL LETTER IOTA;Ll;0;L;;;;;N;;;0399;;0399
        1E9E;LATIN CAPITAL LETTER SHARP S;Lu;0;L;;;;;N;;;;00DF;
        1FBE;GREEK PROSGEGRAMMENI;Ll;0;L;<compat> 03B9;;;;N;;;0399;;0399
        212A;KELVIN SIGN;Lu;0;L;004B;;;;N;DEGREES KELVIN;;;006B;
        3400;<CJK Ideograph Extension A, First>;Lo;0;L;;;;;N;;;;;
        4DBF;<CJK Ideograph Extension A, Last>;Lo;0;L;;;;;N;;;;;
        D800;<Non Private Use High Surrogate, First>;Cs;0;L;;;;;N;;;;;
        DB7F;<Non Private Use High Surrogate, Last>;Cs;0;L;;;;;N;;;;;
        """;

    internal const string Scripts = """
        # Scripts-17.0.0.txt
        # Date: 2025-07-24, 13:28:55 GMT

        0041..005A    ; Latin # L&  [26] LATIN CAPITAL LETTER A..LATIN CAPITAL LETTER Z
        0061..007A    ; Latin # L&  [26] LATIN SMALL LETTER A..LATIN SMALL LETTER Z
        00B5          ; Common # L&       MICRO SIGN
        00DF          ; Latin # Ll       LATIN SMALL LETTER SHARP S
        0345          ; Inherited # Mn       COMBINING GREEK YPOGEGRAMMENI
        0399          ; Greek # Lu       GREEK CAPITAL LETTER IOTA
        03B9          ; Greek # Ll       GREEK SMALL LETTER IOTA
        10300..1031F  ; Old_Italic # Lo  [32] OLD ITALIC LETTER A..OLD ITALIC LETTER ESS
        1E9E          ; Latin # Lu       LATIN CAPITAL LETTER SHARP S
        1FBE          ; Greek # Ll       GREEK PROSGEGRAMMENI
        212A          ; Latin # Lu       KELVIN SIGN
        """;

    // The four orbits {A, a}, {K, k, KELVIN}, {iota with its three forms} and {sharp s}, the lines F and T that are not read, and
    // a line of status C for the micro sign, which is folded to a code point that the file does not list.
    internal const string CaseFolding = """
        # CaseFolding-17.0.0.txt
        0041; C; 0061; # LATIN CAPITAL LETTER A
        004B; C; 006B; # LATIN CAPITAL LETTER K
        00B5; C; 03BC; # MICRO SIGN
        0130; F; 0069 0307; # LATIN CAPITAL LETTER I WITH DOT ABOVE
        0130; T; 0069; # LATIN CAPITAL LETTER I WITH DOT ABOVE
        0345; C; 03B9; # COMBINING GREEK YPOGEGRAMMENI
        0399; C; 03B9; # GREEK CAPITAL LETTER IOTA
        1E9E; F; 0073 0073; # LATIN CAPITAL LETTER SHARP S
        1E9E; S; 00DF; # LATIN CAPITAL LETTER SHARP S
        1FBE; C; 03B9; # GREEK PROSGEGRAMMENI
        212A; C; 006B; # KELVIN SIGN
        """;

    internal static readonly string[] FileNames = [UcdInputs.UnicodeDataName, UcdInputs.ScriptsName, UcdInputs.CaseFoldingName];

    internal static string[] Texts { get; } = [UnicodeData, Scripts, CaseFolding];

    // The text of inputs.txt for the three excerpts: their real SHA-256 and the address of the real files.
    internal static string InputsText() =>
        "# test inputs\nversion 17.0.0\n" + string.Join(
            '\n',
            FileNames.Select((name, index) =>
                $"file {name} {Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Texts[index])))} https://www.unicode.org/Public/17.0.0/ucd/{name}")) + "\n";

    internal static UcdInputs Inputs() => UcdInputs.Parse(InputsText());

    internal static TableModel Model() => TableModel.Build(Inputs(), UnicodeData, Scripts, CaseFolding);

    // The repository of the generator: inputs.txt, and the downloaded files unless the test leaves them out.
    internal static TempDirectory Repository(bool withDownloads)
    {
        TempDirectory root = new();
        root.Write(GeneratorProgram.InputsPath, InputsText());
        if (withDownloads)
        {
            for (int index = 0; index < FileNames.Length; index++)
            {
                root.Write(GeneratorProgram.DownloadDirectory + "/" + FileNames[index], Texts[index]);
            }
        }

        return root;
    }

    // The ranges of a table as text: lo-hi pairs in hexadecimal, separated by blanks.
    internal static string Show(RangeSet set)
    {
        List<string> parts = [];
        ReadOnlySpan<int> pairs = set.Pairs;
        for (int index = 0; index < pairs.Length; index += 2)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{pairs[index]:X}-{pairs[index + 1]:X}"));
        }

        return string.Join(' ', parts);
    }

    // The ranges of the table with a key.
    internal static string Table(TableModel model, string name) => Show(model.Tables.Single(table => table.Name == name).Ranges);

    internal static string Slash(string path) => path.Replace('\\', '/');

    internal static string ReadOutput(string root, string name) => File.ReadAllText(Path.Combine(root, GeneratorProgram.OutputDirectory, name));
}
