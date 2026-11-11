using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>
/// The generator of the Unicode tables (docs/design/unicode-tables.md). Without arguments it reads the three files of the database —
/// from <c>artifacts/unicode/</c>, downloaded once and checked against <c>eng/unicode/inputs.txt</c> — and writes the three files of
/// <c>src/Great.Parts.Cel/Library/Unicode/</c>. With <c>--check</c> it writes nothing and fails when a file of the directory differs
/// from the output. Exit code: 0 on success, 1 when <c>--check</c> finds a file that differs, 2 for an invalid invocation or an input
/// that is missing, cannot be downloaded or does not match <c>inputs.txt</c>.
/// </summary>
internal static class Program
{
    /// <summary>The path of <c>inputs.txt</c> from the repository root.</summary>
    internal const string InputsPath = "eng/unicode/inputs.txt";

    /// <summary>The download directory from the repository root; it is outside git.</summary>
    internal const string DownloadDirectory = "artifacts/unicode";

    /// <summary>The output directory from the repository root.</summary>
    internal const string OutputDirectory = "src/Great.Parts.Cel/Library/Unicode";

    private const string CheckOption = "--check";

    internal static int Main(string[] args) =>
        Run(args, Directory.GetCurrentDirectory(), Console.Out, Console.Error, UcdFiles.Download);

    /// <summary>Runs one invocation.</summary>
    /// <param name="args">The command line.</param>
    /// <param name="root">The repository root.</param>
    /// <param name="output">Standard output: the files written or found equal.</param>
    /// <param name="error">Standard error: the messages of exit code 1 and 2.</param>
    /// <param name="download">Gets the bytes at an address, for a file that is not in the download directory.</param>
    /// <returns>The exit code.</returns>
    internal static int Run(IReadOnlyList<string> args, string root, TextWriter output, TextWriter error, Func<Uri, byte[]> download)
    {
        bool check = args.Count == 1 && args[0] == CheckOption;
        if (args.Count > 1 || (args.Count == 1 && !check))
        {
            error.WriteLine("usage: eng/unicode [--check]");
            return 2;
        }

        IReadOnlyList<GeneratedFile> files;
        try
        {
            files = Generate(root, download);
        }
        catch (Exception exception) when (exception is UcdFormatException or IOException)
        {
            error.WriteLine("eng/unicode: " + exception.Message);
            return 2;
        }

        string directory = Path.Combine(root, OutputDirectory);
        int exitCode = 0;
        foreach (GeneratedFile file in files)
        {
            string path = Path.Combine(directory, file.Name);
            string shown = OutputDirectory + "/" + file.Name;
            byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(file.Text);
            if (check)
            {
                if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
                {
                    error.WriteLine($"eng/unicode: {shown} differs from the output of the generator; run eng/unicode without arguments and commit the result");
                    exitCode = 1;
                }
                else
                {
                    output.WriteLine($"eng/unicode: {shown} is as generated");
                }

                continue;
            }

            Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, bytes);
            output.WriteLine($"eng/unicode: wrote {shown}");
        }

        return exitCode;
    }

    /// <summary>Reads the inputs of a repository and makes the text of the output files.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="download">Gets the bytes at an address, for a file that is not in the download directory.</param>
    /// <exception cref="UcdFormatException">An input does not have the form the generator reads, or its SHA-256 is not the one of <c>inputs.txt</c>.</exception>
    /// <exception cref="IOException"><c>inputs.txt</c> does not exist, or a file cannot be downloaded or stored.</exception>
    internal static IReadOnlyList<GeneratedFile> Generate(string root, Func<Uri, byte[]> download)
    {
        string inputsPath = Path.Combine(root, InputsPath);
        if (!File.Exists(inputsPath))
        {
            throw new IOException($"{InputsPath} does not exist");
        }

        UcdInputs inputs = UcdInputs.Parse(File.ReadAllText(inputsPath));
        string directory = Path.Combine(root, DownloadDirectory);
        string[] texts = new string[inputs.Files.Count];
        for (int index = 0; index < texts.Length; index++)
        {
            texts[index] = UcdFiles.Read(inputs.Files[index], directory, download);
        }

        return TablesWriter.Write(TableModel.Build(inputs, texts[0], texts[1], texts[2]));
    }
}
