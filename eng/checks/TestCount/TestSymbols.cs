using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>The first sequence point of a method of the JIT build.</summary>
/// <param name="File">The document name of the PDB: the path of the source file as the build saw it.</param>
/// <param name="Line">The line, starting with 1.</param>
internal readonly record struct TestSymbol(string File, int Line);

/// <summary>
/// The methods of the JIT build with the place of their first sequence point, read with <c>System.Reflection.Metadata</c> from the
/// assemblies of the JIT report and the PDBs next to them. The files are read on the first search, and only then: a run whose tests
/// all have a source in their report reads neither.
/// </summary>
internal sealed class TestSymbols
{
    private readonly IReadOnlyList<string> _assemblies;
    private Dictionary<(string Type, string Method), TestSymbol>? _methods;

    /// <summary>Creates the lookup.</summary>
    /// <param name="assemblies">The <c>name</c> attributes of the JIT report: the built <c>.dll</c> files.</param>
    internal TestSymbols(IReadOnlyList<string> assemblies) => _assemblies = assemblies;

    /// <summary>Looks for a method by the full name of its class and its name.</summary>
    /// <param name="type">The full name of the class; <c>+</c> separates a nested class.</param>
    /// <param name="method">The method name.</param>
    /// <param name="symbol">The place of the method.</param>
    /// <returns><see langword="true"/> when a method with this name has a sequence point.</returns>
    /// <exception cref="CheckInputException">The report names no assembly, or an assembly or its PDB cannot be read.</exception>
    internal bool TryFind(string type, string method, out TestSymbol symbol)
    {
        _methods ??= Load();
        return _methods.TryGetValue((type, method), out symbol);
    }

    private Dictionary<(string Type, string Method), TestSymbol> Load()
    {
        if (_assemblies.Count == 0)
        {
            throw new CheckInputException("the JIT report has no <assembly> element with a name: the PDB of the JIT build cannot be found");
        }

        Dictionary<(string Type, string Method), TestSymbol> methods = [];
        foreach (string assembly in _assemblies)
        {
            try
            {
                Index(assembly, methods);
            }
            catch (Exception exception) when (exception is BadImageFormatException or InvalidOperationException)
            {
                throw new CheckInputException($"the assembly {assembly} or its PDB is not valid: {exception.Message}", exception);
            }
        }

        return methods;
    }

    private static void Index(string assemblyPath, Dictionary<(string Type, string Method), TestSymbol> methods)
    {
        ImmutableArray<byte> assembly = Read(assemblyPath, "the assembly of the JIT run");
        ImmutableArray<byte> pdb = Read(Path.ChangeExtension(assemblyPath, ".pdb"), "the PDB of the JIT build");

        using PEReader peReader = new(assembly);
        using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbImage(pdb);
        MetadataReader metadata = peReader.GetMetadataReader();
        MetadataReader symbols = provider.GetMetadataReader();
        Dictionary<int, MethodDebugInformationHandle> bodies = StateMachineBodies(symbols);
        foreach (MethodDefinitionHandle handle in metadata.MethodDefinitions)
        {
            MethodDebugInformationHandle debug = handle.ToDebugInformationHandle();
            // The body of an async method is the MoveNext of its state machine; the PDB links it to the method.
            if (symbols.GetMethodDebugInformation(debug).SequencePointsBlob.IsNil
                && bodies.TryGetValue(MetadataTokens.GetRowNumber(handle), out MethodDebugInformationHandle body))
            {
                debug = body;
            }

            if (FirstPoint(symbols, debug) is { } symbol)
            {
                MethodDefinition definition = metadata.GetMethodDefinition(handle);
                methods.TryAdd((TypeName(metadata, definition.GetDeclaringType()), metadata.GetString(definition.Name)), symbol);
            }
        }
    }

    private static ImmutableArray<byte> Read(string path, string what)
    {
        try
        {
            return [.. File.ReadAllBytes(path)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new CheckInputException($"{what} cannot be read: {path}", exception);
        }
    }

    // The row of the method that the compiler turned into a state machine -> the debug information of the MoveNext with its body.
    private static Dictionary<int, MethodDebugInformationHandle> StateMachineBodies(MetadataReader symbols)
    {
        Dictionary<int, MethodDebugInformationHandle> bodies = [];
        foreach (MethodDebugInformationHandle handle in symbols.MethodDebugInformation)
        {
            MethodDefinitionHandle kickoff = symbols.GetMethodDebugInformation(handle).GetStateMachineKickoffMethod();
            if (!kickoff.IsNil)
            {
                bodies.TryAdd(MetadataTokens.GetRowNumber(kickoff), handle);
            }
        }

        return bodies;
    }

    private static TestSymbol? FirstPoint(MetadataReader symbols, MethodDebugInformationHandle handle)
    {
        MethodDebugInformation information = symbols.GetMethodDebugInformation(handle);
        if (information.SequencePointsBlob.IsNil)
        {
            return null;
        }

        foreach (SequencePoint point in information.GetSequencePoints())
        {
            if (!point.IsHidden)
            {
                return new TestSymbol(symbols.GetString(symbols.GetDocument(point.Document).Name), point.StartLine);
            }
        }

        return null;
    }

    private static string TypeName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        TypeDefinition type = metadata.GetTypeDefinition(handle);
        string name = metadata.GetString(type.Name);
        TypeDefinitionHandle declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
        {
            return TypeName(metadata, declaring) + "+" + name;
        }

        string space = metadata.GetString(type.Namespace);
        return space.Length == 0 ? name : space + "." + name;
    }
}
