using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Great.Parts.Cel.Policy.Yaml;

namespace Great.Parts.Cel.Conformance.CelPolicy;

/// <summary>
/// Translates a <c>config.yaml</c> of a category, the environment description of cel-go, into the JSON form of the library
/// (<c>CelEnvironmentJson</c>): the keys <c>name</c>, <c>container</c>, <c>extensions</c>, <c>variables</c> and <c>functions</c>.
/// Keys the JSON form has no place for are not carried: <c>stdlib</c> (the library keeps the whole standard library) and
/// <c>context_variable</c> (a protobuf message whose fields are variables).
/// </summary>
internal static class PolicyConfig
{
    // The version of each set of the catalog, the only one the library has. A configuration of cel-go names a version of its own
    // ("latest", a number such as strings 2, or none, which is 0); the library has no older version of a set, and the version it has
    // adds to the older ones, so every configuration gets that version.
    private static readonly Dictionary<string, string> LatestVersions = new(StringComparer.Ordinal)
    {
        ["strings"] = "5",
        ["math"] = "3",
        ["lists"] = "4",
        ["sets"] = "0",
        ["encoders"] = "1",
        ["bindings"] = "1",
        ["two-var-comprehensions"] = "0",
        ["network"] = "1",
        ["protos"] = "0",
        ["optional"] = "2",
    };

    /// <summary>The overload identifiers that the configuration declares, in the order of the file.</summary>
    /// <param name="config">The root of <c>config.yaml</c>.</param>
    internal static IReadOnlyList<string> OverloadIds(YamlMapping config)
    {
        List<string> ids = [];
        foreach (YamlNode function in YamlAccess.Items(YamlAccess.Find(config, "functions")))
        {
            foreach (YamlNode overload in YamlAccess.Items(function is YamlMapping mapping ? YamlAccess.Find(mapping, "overloads") : null))
            {
                if (overload is YamlMapping overloadMapping && YamlAccess.Text(YamlAccess.Find(overloadMapping, "id")) is { } id)
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>Writes the JSON form of a configuration.</summary>
    /// <param name="config">The root of <c>config.yaml</c>.</param>
    internal static byte[] ToJson(YamlMapping config)
    {
        ArrayBufferWriter<byte> buffer = new();
        // The relaxed escaping writes < and > as they are, which the type texts of the JSON form have.
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            WriteText(writer, config, "name", "name");
            WriteText(writer, config, "container", "container");
            WriteExtensions(writer, config);
            WriteVariables(writer, config);
            WriteFunctions(writer, config);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>The text of the JSON form, for the tests of the runner.</summary>
    /// <param name="config">The root of <c>config.yaml</c>.</param>
    internal static string ToJsonText(YamlMapping config) => Encoding.UTF8.GetString(ToJson(config));

    private static void WriteText(Utf8JsonWriter writer, YamlMapping config, string key, string jsonKey)
    {
        if (YamlAccess.Text(YamlAccess.Find(config, key)) is { } text)
        {
            writer.WriteString(jsonKey, text);
        }
    }

    private static void WriteExtensions(Utf8JsonWriter writer, YamlMapping config)
    {
        IReadOnlyList<YamlNode> extensions = YamlAccess.Items(YamlAccess.Find(config, "extensions"));
        if (extensions.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("extensions");
        foreach (YamlNode node in extensions)
        {
            YamlMapping extension = node as YamlMapping ?? throw new InvalidDataException("an extension of config.yaml is no mapping");
            string name = YamlAccess.Text(YamlAccess.Find(extension, "name")) ?? throw new InvalidDataException("an extension of config.yaml has no name");
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WriteString("version", LatestVersions.TryGetValue(name, out string? version) ? version : throw new InvalidDataException($"the catalog has no set '{name}'"));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteVariables(Utf8JsonWriter writer, YamlMapping config)
    {
        IReadOnlyList<YamlNode> variables = YamlAccess.Items(YamlAccess.Find(config, "variables"));
        if (variables.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("variables");
        foreach (YamlNode node in variables)
        {
            YamlMapping variable = node as YamlMapping ?? throw new InvalidDataException("a variable of config.yaml is no mapping");
            writer.WriteStartObject();
            WriteText(writer, variable, "name", "name");
            writer.WriteString("type", TypeText(variable));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteFunctions(Utf8JsonWriter writer, YamlMapping config)
    {
        IReadOnlyList<YamlNode> functions = YamlAccess.Items(YamlAccess.Find(config, "functions"));
        if (functions.Count == 0)
        {
            return;
        }

        writer.WriteStartArray("functions");
        foreach (YamlNode node in functions)
        {
            YamlMapping function = node as YamlMapping ?? throw new InvalidDataException("a function of config.yaml is no mapping");
            writer.WriteStartObject();
            WriteText(writer, function, "name", "name");
            writer.WriteStartArray("overloads");
            foreach (YamlNode overloadNode in YamlAccess.Items(YamlAccess.Find(function, "overloads")))
            {
                YamlMapping overload = overloadNode as YamlMapping ?? throw new InvalidDataException("an overload of config.yaml is no mapping");
                writer.WriteStartObject();
                WriteText(writer, overload, "id", "id");
                if (YamlAccess.Find(overload, "target") is YamlMapping target)
                {
                    writer.WriteString("target", TypeText(target));
                }

                writer.WriteStartArray("args");
                foreach (YamlNode argument in YamlAccess.Items(YamlAccess.Find(overload, "args")))
                {
                    writer.WriteStringValue(TypeText(argument as YamlMapping ?? throw new InvalidDataException("an argument of config.yaml is no mapping")));
                }

                writer.WriteEndArray();
                writer.WriteString(
                    "return",
                    TypeText(YamlAccess.Find(overload, "return") as YamlMapping ?? throw new InvalidDataException("an overload of config.yaml has no return")));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    // A type record of cel-go: type_name and params, directly or under the key type, as the string of the JSON form: list<int>,
    // map<string, dyn>. The types of time are written timestamp and duration in the JSON form.
    private static string TypeText(YamlMapping record)
    {
        YamlMapping type = YamlAccess.Find(record, "type") as YamlMapping ?? record;
        string name = YamlAccess.Text(YamlAccess.Find(type, "type_name")) ?? throw new InvalidDataException("a type record of config.yaml has no type_name");
        string text = name switch
        {
            "google.protobuf.Timestamp" => "timestamp",
            "google.protobuf.Duration" => "duration",
            _ => name,
        };
        IReadOnlyList<YamlNode> parameters = YamlAccess.Items(YamlAccess.Find(type, "params"));
        if (parameters.Count == 0)
        {
            return text;
        }

        StringBuilder builder = new(text);
        builder.Append('<');
        for (int index = 0; index < parameters.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(TypeText(parameters[index] as YamlMapping ?? throw new InvalidDataException("a parameter of config.yaml is no mapping")));
        }

        return builder.Append('>').ToString();
    }
}
