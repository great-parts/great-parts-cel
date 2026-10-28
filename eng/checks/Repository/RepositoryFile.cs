using System.IO;
using System.Text;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>A file of the file set of the repository.</summary>
/// <param name="Path">The path relative to the root, with <c>/</c>.</param>
/// <param name="FullPath">The full path on disk.</param>
internal sealed record RepositoryFile(string Path, string FullPath)
{
    /// <summary>Reads the file as UTF-8. A UTF-8 byte order mark is dropped; any other content is decoded as UTF-8 too.</summary>
    internal string ReadText()
    {
        using StreamReader reader = new(FullPath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), detectEncodingFromByteOrderMarks: false);
        return reader.ReadToEnd();
    }
}
