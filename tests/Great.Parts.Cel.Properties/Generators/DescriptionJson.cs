using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CsCheck;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Properties.Generators;

/// <summary>
/// Prints a <see cref="DescriptionModel"/> as the JSON of an environment description (docs/design/tests.md, "Environment from
/// JSON"): the keys of an object in random order, random white space, <c>type</c> or <c>type_name</c> with <c>params</c>, and
/// escapes in strings.
/// </summary>
internal static class DescriptionJson
{
    private static readonly string[] Spaces = ["", "", " ", "\n  ", "\t", "\r\n"];

    /// <summary>Prints the description of a model.</summary>
    /// <param name="model">The model.</param>
    /// <param name="random">The source of randomness for the order of keys, the white space and the escapes.</param>
    /// <returns>The JSON text.</returns>
    internal static string Print(DescriptionModel model, PCG random)
    {
        var writer = new Writer(random);
        var pairs = new List<(string Key, string Value)>();
        if (model.Name is not null)
        {
            pairs.Add(("name", writer.Str(model.Name)));
        }

        if (random.Next(4) == 0)
        {
            pairs.Add(("description", writer.Str("a description")));
        }

        if (model.Container.Length > 0 || random.Next(4) == 0)
        {
            pairs.Add(("container", writer.Str(model.Container)));
        }

        if (model.Variables.Count > 0 || random.Next(4) == 0)
        {
            pairs.Add(("variables", writer.Items(model.Variables.ConvertAll(writer.Variable))));
        }

        if (model.Functions.Count > 0 || random.Next(4) == 0)
        {
            pairs.Add(("functions", writer.Items(model.Functions.ConvertAll(writer.Function))));
        }

        if (model.Types.Count > 0 || random.Next(4) == 0)
        {
            pairs.Add(("greatPartsCel", writer.Braces([("objectTypes", writer.Items(model.Types.ConvertAll(writer.ObjectType)))])));
        }

        return writer.Braces(pairs);
    }

    private sealed class Writer(PCG random)
    {
        // A string with some of its characters, and every control character, written as \uXXXX.
        internal string Str(string value)
        {
            var text = new StringBuilder("\"");
            foreach (char character in value)
            {
                if (character < ' ' || random.Next(8) == 0)
                {
                    text.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    text.Append(character);
                }
            }

            return text.Append('"').ToString();
        }

        internal string Items(List<string> items)
        {
            var text = new StringBuilder("[").Append(Space());
            for (int index = 0; index < items.Count; index++)
            {
                text.Append(index == 0 ? "" : "," + Space()).Append(items[index]);
            }

            return text.Append(Space()).Append(']').ToString();
        }

        // The pairs in random order.
        internal string Braces(List<(string Key, string Value)> pairs)
        {
            for (int index = pairs.Count - 1; index > 0; index--)
            {
                int other = (int)random.Next((uint)index + 1);
                (pairs[index], pairs[other]) = (pairs[other], pairs[index]);
            }

            var text = new StringBuilder("{").Append(Space());
            for (int index = 0; index < pairs.Count; index++)
            {
                text.Append(index == 0 ? "" : "," + Space());
                text.Append(Str(pairs[index].Key)).Append(Space()).Append(':').Append(Space()).Append(pairs[index].Value);
            }

            return text.Append(Space()).Append('}').ToString();
        }

        internal string Variable(VariableModel variable)
        {
            List<(string Key, string Value)> pairs = [("name", Str(variable.Name))];
            AddType(pairs, variable.Type);
            if (random.Next(4) == 0)
            {
                pairs.Add(("description", Str("a variable")));
            }

            return Braces(pairs);
        }

        internal string Function(FunctionModel function)
        {
            List<(string Key, string Value)> pairs = [("name", Str(function.Name)), ("overloads", Items(new List<OverloadModel>(function.Overloads).ConvertAll(Overload)))];
            if (random.Next(4) == 0)
            {
                pairs.Add(("description", Str("a function")));
            }

            return Braces(pairs);
        }

        internal string ObjectType(ObjectTypeModel type)
        {
            List<(string Key, string Value)> pairs = [("name", Str(type.Name))];
            if (type.Fields.Length > 0 || random.Next(2) == 0)
            {
                var fields = new List<string>();
                foreach (FieldModel field in type.Fields)
                {
                    fields.Add(Braces([("name", Str(field.Name)), ("type", Str(TypeRecord(field.Type)))]));
                }

                pairs.Add(("fields", Items(fields)));
            }

            return Braces(pairs);
        }

        private static (string Name, CelType[] Parameters) Named(CelType type) => type.Kind switch
        {
            CelTypeKind.List => ("list", [type.Parameters[0]]),
            CelTypeKind.Map => ("map", [type.Parameters[0], type.Parameters[1]]),
            CelTypeKind.Type when type.Parameters[0].Kind != CelTypeKind.Dyn => ("type", [type.Parameters[0]]),
            CelTypeKind.Type => ("type", []),
            _ => (TypeRecord(type, withSpaces: false), []),
        };

        private static string TypeRecord(CelType type, bool withSpaces)
        {
            string gap = withSpaces ? " " : "";
            return type.Kind switch
            {
                CelTypeKind.Int => "int",
                CelTypeKind.UInt => "uint",
                CelTypeKind.Double => "double",
                CelTypeKind.Bool => "bool",
                CelTypeKind.String => "string",
                CelTypeKind.Bytes => "bytes",
                CelTypeKind.Dyn => "dyn",
                CelTypeKind.Null => "null_type",
                CelTypeKind.Wrapper => TypeRecord(type.Parameters[0], withSpaces: false) + "_wrapper",
                CelTypeKind.List => $"list<{gap}{TypeRecord(type.Parameters[0], withSpaces)}{gap}>",
                CelTypeKind.Map => $"map<{TypeRecord(type.Parameters[0], withSpaces)},{gap}{TypeRecord(type.Parameters[1], withSpaces)}>",
                CelTypeKind.Type when type.Parameters[0].Kind == CelTypeKind.Dyn => "type",
                CelTypeKind.Type => $"type<{TypeRecord(type.Parameters[0], withSpaces)}>",
                CelTypeKind.TypeParameter => "~" + type.Name.TrimStart('~'),
                _ => type.Name,
            };
        }

        private string Space() => Spaces[random.Next((uint)Spaces.Length)];

        private string Overload(OverloadModel overload)
        {
            List<(string Key, string Value)> pairs = [("id", Str(overload.Id)), ("return", Str(TypeRecord(overload.Return)))];
            if (overload.Target is { } target)
            {
                pairs.Add(("target", Str(TypeRecord(target))));
            }

            if (overload.Args.Length > 0 || random.Next(2) == 0)
            {
                pairs.Add(("args", Items(new List<CelType>(overload.Args).ConvertAll(argument => Str(TypeRecord(argument))))));
            }

            if (random.Next(4) == 0)
            {
                pairs.Add(("examples", Items(["\"an example\""])));
            }

            return Braces(pairs);
        }

        // The type of a variable as `type`, or as `type_name` with `params`.
        private void AddType(List<(string Key, string Value)> pairs, CelType type)
        {
            if (random.Next(2) == 0)
            {
                pairs.Add(("type", Str(TypeRecord(type))));
                return;
            }

            (string name, CelType[] parameters) = Named(type);
            pairs.Add(("type_name", Str(name)));
            if (parameters.Length > 0 || random.Next(4) == 0)
            {
                pairs.Add(("params", Items(new List<CelType>(parameters).ConvertAll(parameter => Str(TypeRecord(parameter))))));
            }
        }

        // A record, padded with white space now and then.
        private string TypeRecord(CelType type)
        {
            string record = TypeRecord(type, withSpaces: true);
            return random.Next(2) == 0 ? Space() + record + Space() : record;
        }
    }
}
