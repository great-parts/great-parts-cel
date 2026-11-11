using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>
/// The files of the database in the download directory (docs/design/unicode-tables.md, "The data"): a file that is there is
/// checked against its SHA-256 and used without the network; a file that is not there is downloaded once, checked and kept.
/// </summary>
internal static class UcdFiles
{
    /// <summary>The text of a file, downloaded when it is not in the directory.</summary>
    /// <param name="file">The file as <c>inputs.txt</c> names it.</param>
    /// <param name="directory">The download directory.</param>
    /// <param name="download">Gets the bytes at an address.</param>
    /// <returns>The text of the file, UTF-8.</returns>
    /// <exception cref="UcdFormatException">The SHA-256 of the file is not the one in <c>inputs.txt</c>.</exception>
    /// <exception cref="IOException">The file cannot be downloaded or stored.</exception>
    internal static string Read(UcdFile file, string directory, Func<Uri, byte[]> download)
    {
        string path = Path.Combine(directory, file.Name);
        if (!File.Exists(path))
        {
            byte[] downloaded = download(file.Address);
            Check(file, downloaded);
            Directory.CreateDirectory(directory);
            string temporary = path + ".part";
            File.WriteAllBytes(temporary, downloaded);
            File.Move(temporary, path, overwrite: true);
            return Decode(downloaded);
        }

        byte[] bytes = File.ReadAllBytes(path);
        Check(file, bytes);
        return Decode(bytes);
    }

    /// <summary>The download of the container: an HTTPS request with no state.</summary>
    /// <param name="address">The address.</param>
    internal static byte[] Download(Uri address)
    {
        using HttpClient client = new();
        try
        {
            return client.GetByteArrayAsync(address).GetAwaiter().GetResult();
        }
        catch (HttpRequestException exception)
        {
            throw new IOException($"{address}: {exception.Message}", exception);
        }
    }

    private static void Check(UcdFile file, byte[] bytes)
    {
        string actual = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(actual, file.Sha256, StringComparison.Ordinal))
        {
            throw new UcdFormatException(file.Name, 0, string.Create(CultureInfo.InvariantCulture, $"the SHA-256 is {actual}, inputs.txt has {file.Sha256}"));
        }
    }

    private static string Decode(byte[] bytes) => new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
}
