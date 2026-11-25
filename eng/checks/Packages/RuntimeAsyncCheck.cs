using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// <c>ENG211</c> (<c>docs/design/eng-checks-artifacts.md</c>, "packages"): the runtime-async flag of the <c>net11.0</c> assemblies of a
/// package that runs in WebAssembly applications follows the kind of the build. The assemblies of <c>lib/net11.0/</c> are for the other
/// applications and are built with runtime async; the assemblies of <c>runtimes/browser-wasm/lib/net11.0/</c> and
/// <c>runtimes/wasi-wasm/lib/net11.0/</c> are for WebAssembly and are built without it. A package that has no assembly of the second
/// kind (and a <c>browser</c> line in its list) must have no method with the flag at all.
/// </summary>
internal sealed class RuntimeAsyncCheck
{
    private const string LibraryFolder = "lib/net11.0/";

    private static readonly string[] WebAssemblyFolders = ["runtimes/browser-wasm/lib/net11.0/", "runtimes/wasi-wasm/lib/net11.0/"];

    private readonly PackageList _list;
    private readonly PackageContent _package;
    private readonly ZipArchive _archive;
    private readonly List<Finding> _findings = [];

    private RuntimeAsyncCheck(PackageList list, PackageContent package, ZipArchive archive)
    {
        _list = list;
        _package = package;
        _archive = archive;
    }

    /// <summary>
    /// Checks the package. A package with no assembly for WebAssembly and no <c>browser</c> line is not checked.
    /// </summary>
    /// <param name="list">The list of the package.</param>
    /// <param name="package">The package.</param>
    /// <param name="fullPath">The full path of the <c>.nupkg</c>, which the check opens again to read the assemblies.</param>
    /// <exception cref="CheckInputException">The package or an assembly in it cannot be read.</exception>
    internal static IEnumerable<Finding> Check(PackageList list, PackageContent package, string fullPath)
    {
        if (list.BrowserLine == 0 && !package.Entries.Any(IsWebAssemblyAssembly))
        {
            return [];
        }

        try
        {
            using ZipArchive archive = ZipFile.OpenRead(fullPath);
            RuntimeAsyncCheck check = new(list, package, archive);
            check.Run();
            return check._findings;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            throw new CheckInputException($"the package {package.Path} cannot be read: {exception.Message}", exception);
        }
    }

    private static bool IsWebAssemblyAssembly(string entry) => WebAssemblyFolders.Any(folder => IsAssembly(entry, folder));

    private static bool IsAssembly(string entry, string folder) =>
        entry.StartsWith(folder, StringComparison.Ordinal)
        && entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
        && entry.IndexOf('/', folder.Length) < 0;

    private static string FileName(string entry) => entry[(entry.LastIndexOf('/') + 1)..];

    private void Run()
    {
        string[] webAssemblies = [.. _package.Entries.Where(IsWebAssemblyAssembly)];
        string[] libraries = [.. _package.Entries.Where(entry => IsAssembly(entry, LibraryFolder))];
        foreach (string library in libraries)
        {
            string[] counterparts = [.. webAssemblies.Where(web => FileName(web) == FileName(library))];
            CheckLibrary(library, Read(library), counterparts.Length > 0);
        }

        foreach (string web in webAssemblies)
        {
            CheckWebAssembly(web, libraries.FirstOrDefault(library => FileName(library) == FileName(web)));
        }
    }

    // An assembly of lib/net11.0/ with a counterpart for WebAssembly is built with runtime async: every async method has the flag. One
    // without a counterpart runs in WebAssembly applications too, so no method of it may have the flag.
    private void CheckLibrary(string library, AssemblyFacts facts, bool hasCounterpart)
    {
        int line = LineOf(library);
        if (hasCounterpart)
        {
            foreach (string method in facts.StateMachineMethods)
            {
                Add(line, $"the async method '{method}' of '{library}' has no runtime-async flag; the assemblies of lib/net11.0/ are built with runtime async");
            }

            return;
        }

        foreach (string method in facts.RuntimeAsyncMethods)
        {
            Add(line, $"the method '{method}' of '{library}' has the runtime-async flag, but the package has no assembly of that name for WebAssembly applications under runtimes/");
        }
    }

    // An assembly for WebAssembly has no method with the flag and the public surface of the assembly of lib/net11.0/ with the same name.
    private void CheckWebAssembly(string web, string? library)
    {
        int line = LineOf(web);
        AssemblyFacts facts = Read(web);
        foreach (string method in facts.RuntimeAsyncMethods)
        {
            Add(line, $"the method '{method}' of '{web}' has the runtime-async flag; an assembly for WebAssembly applications is built without runtime async");
        }

        if (library is null)
        {
            Add(line, $"the package has no assembly '{FileName(web)}' in lib/net11.0/ to compare '{web}' with");
            return;
        }

        AssemblyFacts reference = Read(library);
        foreach (string item in facts.Surface.Except(reference.Surface, StringComparer.Ordinal))
        {
            Add(line, $"the public surface of '{web}' differs from '{library}': '{item}' is only in the first");
        }

        foreach (string item in reference.Surface.Except(facts.Surface, StringComparer.Ordinal))
        {
            Add(line, $"the public surface of '{web}' differs from '{library}': '{item}' is only in the second");
        }
    }

    // The line of the file in the list; for a file that the list does not name, the browser line, and without one the first line.
    private int LineOf(string entry) =>
        _list.Files.TryGetValue(entry, out int line) ? line : Math.Max(_list.BrowserLine, 1);

    private void Add(int line, string message) =>
        _findings.Add(new Finding(_list.Path, line, 1, FindingSeverity.Error, EngCodes.Eng211, message));

    private AssemblyFacts Read(string entry)
    {
        ZipArchiveEntry zipEntry = _archive.GetEntry(entry) ?? throw new CheckInputException($"the package {_package.Path} has no entry {entry}");
        using MemoryStream bytes = new();
        using (Stream stream = zipEntry.Open())
        {
            stream.CopyTo(bytes);
        }

        try
        {
            return AssemblyFacts.Read([.. bytes.ToArray()]);
        }
        catch (Exception exception) when (exception is BadImageFormatException or InvalidOperationException)
        {
            throw new CheckInputException($"the assembly {entry} of the package {_package.Path} cannot be read: {exception.Message}", exception);
        }
    }
}
