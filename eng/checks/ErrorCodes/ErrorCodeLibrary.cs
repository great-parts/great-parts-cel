using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the members of <c>Great.Parts.Cel.Diagnostics.CelErrorCode</c> from the built <c>Great.Parts.Cel.dll</c> with
/// <c>System.Reflection.Metadata</c> and their summaries from the XML documentation file next to it
/// (<c>docs/design/eng-docs.md</c>, "error-codes"). Nothing of the library is loaded or run.
/// </summary>
internal static class ErrorCodeLibrary
{
    private const string EnumNamespace = "Great.Parts.Cel.Diagnostics";
    private const string EnumName = "CelErrorCode";
    private const string MemberPrefix = "F:" + EnumNamespace + "." + EnumName + ".";

    /// <summary>Reads the members, sorted by number.</summary>
    /// <param name="assemblyPath">The full path of the built <c>Great.Parts.Cel.dll</c>.</param>
    /// <param name="documentationPath">The full path of its XML documentation file.</param>
    /// <exception cref="CheckInputException">A file does not exist or cannot be read, or the library has no such enumeration.</exception>
    internal static List<ErrorCodeMember> Read(string assemblyPath, string documentationPath)
    {
        // The library first: without a build neither file exists, and the message names the library and the build.
        List<(string Name, int Number)> constants = ReadConstants(assemblyPath);
        Dictionary<string, string> summaries = ReadSummaries(documentationPath);
        List<ErrorCodeMember> members = [];
        foreach ((string name, int number) in constants.OrderBy(constant => constant.Number).ThenBy(constant => constant.Name, StringComparer.Ordinal))
        {
            string stage = ErrorCodeStages.Of(number)
                ?? throw new CheckInputException($"the number {number} of {EnumName}.{name} is in none of the ranges of the stages");
            members.Add(new ErrorCodeMember(name, number, stage, summaries.GetValueOrDefault(name, string.Empty)));
        }

        return members;
    }

    private static List<(string Name, int Number)> ReadConstants(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
        {
            throw new CheckInputException($"the library {assemblyPath} does not exist; build src/Great.Parts.Cel with -c Release first");
        }

        try
        {
            using FileStream stream = File.OpenRead(assemblyPath);
            using PEReader peReader = new(stream);
            MetadataReader reader = peReader.GetMetadataReader();
            foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
            {
                TypeDefinition type = reader.GetTypeDefinition(handle);
                if (reader.GetString(type.Namespace) == EnumNamespace && reader.GetString(type.Name) == EnumName)
                {
                    return [.. ConstantsOf(reader, type)];
                }
            }
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or InvalidOperationException)
        {
            throw new CheckInputException($"the library {assemblyPath} cannot be read: {exception.Message}", exception);
        }

        throw new CheckInputException($"the library {assemblyPath} has no type {EnumNamespace}.{EnumName}");
    }

    // The fields that are constants; the special field value__ of the enumeration is not one.
    private static IEnumerable<(string Name, int Number)> ConstantsOf(MetadataReader reader, TypeDefinition type)
    {
        foreach (FieldDefinitionHandle handle in type.GetFields())
        {
            FieldDefinition field = reader.GetFieldDefinition(handle);
            if ((field.Attributes & FieldAttributes.Literal) == 0)
            {
                continue;
            }

            Constant constant = reader.GetConstant(field.GetDefaultValue());
            object? value = reader.GetBlobReader(constant.Value).ReadConstant(constant.TypeCode);
            yield return (reader.GetString(field.Name), Convert.ToInt32(value, CultureInfo.InvariantCulture));
        }
    }

    // The summary of every member of the enumeration by member name.
    private static Dictionary<string, string> ReadSummaries(string documentationPath)
    {
        if (!File.Exists(documentationPath))
        {
            throw new CheckInputException($"the XML documentation {documentationPath} does not exist; build src/Great.Parts.Cel with -c Release first");
        }

        XDocument document;
        try
        {
            document = XDocument.Load(documentationPath);
        }
        catch (Exception exception) when (exception is XmlException or IOException)
        {
            throw new CheckInputException($"the XML documentation {documentationPath} cannot be read: {exception.Message}", exception);
        }

        Dictionary<string, string> summaries = new(StringComparer.Ordinal);
        foreach (XElement member in document.Descendants("member"))
        {
            string id = member.Attribute("name")?.Value ?? string.Empty;
            if (id.StartsWith(MemberPrefix, StringComparison.Ordinal) && member.Element("summary") is { } summary)
            {
                summaries[id[MemberPrefix.Length..]] = SummaryText.Of(summary);
            }
        }

        return summaries;
    }
}
