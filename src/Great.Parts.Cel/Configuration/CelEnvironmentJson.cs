using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Configuration;

/// <summary>
/// Reads an environment description in JSON into a <see cref="CelEnvironmentBuilder"/>: variables with
/// their types, object types over dictionaries, extension sets with their versions, and declarations of
/// functions whose implementations the application registers in code.
/// </summary>
/// <remarks>
/// <para>
/// The format is a subset of the JSON form of the environment configuration of cel-go, the CEL
/// implementation in Go: the keys <c>name</c>, <c>description</c>, <c>container</c>, <c>extensions</c>,
/// <c>variables</c> and <c>functions</c>, and the object <c>greatPartsCel</c> for what cel-go has no keys
/// for: object types whose values are dictionaries, described as by
/// <see cref="Values.CelObjectTypeBuilder.ForDictionary"/>, which tells how field values are read; the
/// description has no enumeration types. Types are written as in cel-go, such as <c>int</c>,
/// <c>map&lt;string, dyn&gt;</c>, <c>list&lt;acme.sales.Order&gt;</c> and <c>~T</c> for a type parameter. The
/// keys, their values and the type syntax are in the environment description table of the documentation.
/// </para>
/// <para>
/// The description does not carry what only code can supply: the standard library version, which is the
/// argument of the builder constructor; the implementations of functions, registered with
/// <see cref="CelEnvironmentBuilder.AddFunctionImplementation(string, Declarations.CelFunctionImplementation)"/>
/// or its typed overloads before or after this call; type contexts and protobuf types, added with
/// <see cref="CelEnvironmentBuilder.AddTypes"/>; the time zone database; and on .NET 11 the <c>decimal</c>
/// extension set and its literal option.
/// </para>
/// <para>
/// Reading is strict. An unknown key, a repeated key, a value of the wrong kind, a missing required key,
/// an extension set the catalog does not have (such as <c>block</c> or <c>regex</c>), an extension version
/// that is empty or <c>latest</c>, a key of the cel-go format that this library does not support (such as
/// <c>imports</c>, <c>stdlib</c>, <c>validators</c>, <c>features</c> or <c>limits</c>), and a value the
/// builder would reject as an invalid argument are errors <see cref="CelErrorCode.InvalidConfiguration"/>
/// with the position in the JSON text in <see cref="CelError.SourceLocation"/>. The JSON follows RFC 8259:
/// no comments, no trailing commas; a UTF-8 byte order mark at the start is skipped.
/// </para>
/// <para>
/// The method maps the description one to one onto calls of the builder and makes no decisions of its
/// own. An environment described in JSON is therefore the same as one built by the same calls in code,
/// and conflicts and other errors of the declarations are the same build errors, which
/// <see cref="CelEnvironmentBuilder.Build"/> reports with the position of the declaration in the JSON text.
/// Errors name the description by a source label: <c>environment JSON '&lt;name&gt;'</c> with the value of
/// its <c>name</c> key, or <c>environment JSON</c> without that key. The same label is the
/// <see cref="CelSourceLocation.SourceName"/> of every position in the JSON text, both of the errors
/// returned here and of the build errors. The key <c>container</c> calls
/// <see cref="CelEnvironmentBuilder.UseContainer"/>: a different non-empty container declared in code is the
/// build error <see cref="CelErrorCode.ContainerConflict"/>, and the same container is one declaration.
/// </para>
/// </remarks>
public static class CelEnvironmentJson
{
    // The UTF-8 encoding of U+FEFF.
    private static ReadOnlySpan<byte> ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <summary>
    /// Reads an environment description in JSON and applies it to the builder, or reports why it cannot be
    /// read.
    /// </summary>
    /// <remarks>
    /// When the description has an error, the builder is not changed at all: the description is applied
    /// completely or not at all. The builder is used from one thread, as always.
    /// </remarks>
    /// <param name="builder">The builder to which the declarations are added.</param>
    /// <param name="utf8Json">The description, JSON text in UTF-8.</param>
    /// <returns>
    /// The errors <see cref="CelErrorCode.InvalidConfiguration"/> of the stage <see cref="CelErrorStage.Build"/>
    /// found while reading; empty when the description was applied.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static ImmutableArray<CelError> Apply(CelEnvironmentBuilder builder, ReadOnlySpan<byte> utf8Json)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ReadOnlySpan<byte> json = utf8Json.StartsWith(ByteOrderMark) ? utf8Json[ByteOrderMark.Length..] : utf8Json;
        EnvironmentDescription description = EnvironmentReader.Read(json);
        if (!description.Errors.IsEmpty)
        {
            return Errors(json, description);
        }

        foreach (CelVariableDecl variable in description.Variables)
        {
            builder.AddVariable(variable.Name, variable.Type);
        }

        return [];
    }

    private static ImmutableArray<CelError> Errors(ReadOnlySpan<byte> json, EnvironmentDescription description)
    {
        string sourceName = description.Name is null ? "environment JSON" : $"environment JSON '{description.Name}'";
        int[] byteIndexes = [.. description.Errors.Select(error => error.ByteIndex)];
        ImmutableArray<CelSourceLocation> locations = JsonPositions.Locate(json, byteIndexes, sourceName);
        return [.. description.Errors.Zip(locations, Error)];
    }

    private static CelError Error(JsonError error, CelSourceLocation location) => new(
        CelErrorCode.InvalidConfiguration,
        CelErrorStage.Build,
        string.Create(
            CultureInfo.InvariantCulture,
            $"invalid environment description at line {location.Line}, column {location.Column}: {error.Detail}"),
        default,
        location,
        null);
}
