using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Great.Parts.Cel.Tests.Environment;

// The parsing and formatting of DateOnly and TimeOnly by .NET follow the culture of the process, so the build bans them in every
// package (eng/BannedSymbols.txt). The list is written from the documentation of net10.0; this test builds the documentation
// identifier of every public method of the types that parses or formats text, on the target the tests run on, and asks that the
// file has it, so an overload that a later target adds is found. The file is read from the checkout; a run without it, or without
// reflection over methods (Native AOT), is skipped.
public sealed class BannedDateTimeApiTests
{
    private static readonly string[] Names =
    [
        "Parse", "ParseExact", "TryParse", "TryParseExact", "ToString", "TryFormat",
        "ToLongDateString", "ToShortDateString", "ToLongTimeString", "ToShortTimeString",
    ];

    [Fact]
    public static void EveryParsingAndFormattingMethodOfDateOnly_IsBanned()
    {
        List<string> banned = ReadBannedIdentifiers();

        Assert.Empty(Missing(typeof(DateOnly), banned));
    }

    [Fact]
    public static void EveryParsingAndFormattingMethodOfTimeOnly_IsBanned()
    {
        List<string> banned = ReadBannedIdentifiers();

        Assert.Empty(Missing(typeof(TimeOnly), banned));
    }

    private static List<string> Missing([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type, List<string> banned)
    {
        List<string> missing = [];
        foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (!Names.Contains(method.Name, StringComparer.Ordinal))
            {
                continue;
            }

            string identifier = Identifier(type, method);
            if (!banned.Contains(identifier, StringComparer.Ordinal))
            {
                missing.Add(identifier);
            }
        }

        return missing;
    }

    // The documentation identifier of a method: M:System.DateOnly.TryParse(System.String,System.DateOnly@).
    private static string Identifier(Type type, MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        string list = parameters.Length == 0 ? string.Empty : "(" + string.Join(",", parameters.Select(parameter => TypeName(parameter.ParameterType))) + ")";
        return "M:" + type.FullName + "." + method.Name + list;
    }

    private static string TypeName(Type type)
    {
        if (type.IsByRef)
        {
            return TypeName(type.GetElementType()!) + "@";
        }

        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericType)
        {
            string name = type.GetGenericTypeDefinition().FullName!;
            return name[..name.IndexOf('`', StringComparison.Ordinal)] + "{" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + "}";
        }

        return type.FullName!.Replace('+', '.');
    }

    private static List<string> ReadBannedIdentifiers()
    {
        Assert.SkipUnless(RuntimeFeature.IsDynamicCodeSupported, "the methods are listed by reflection, which Native AOT does not have");
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "eng", "BannedSymbols.txt")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.SkipWhen(directory is null, "eng/BannedSymbols.txt is not in a parent directory of the tests");
        List<string> identifiers = [];
        foreach (string line in File.ReadLines(Path.Combine(directory!, "eng", "BannedSymbols.txt")))
        {
            int separator = line.IndexOf(';', StringComparison.Ordinal);
            if (line.StartsWith("M:", StringComparison.Ordinal) && separator > 0)
            {
                identifiers.Add(line[..separator]);
            }
        }

        return identifiers;
    }
}
