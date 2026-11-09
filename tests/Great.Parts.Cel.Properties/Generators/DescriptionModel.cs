using System.Collections.Generic;
using System.Globalization;
using CsCheck;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Properties.Generators;

/// <summary>A variable of a model: a name and a type.</summary>
/// <param name="Name">The name, which may not be an identifier.</param>
/// <param name="Type">The declared type.</param>
internal sealed record VariableModel(string Name, CelType Type);

/// <summary>A field of an object type of a model.</summary>
/// <param name="Name">The name; the names of one type are distinct.</param>
/// <param name="Type">The type of the field.</param>
internal sealed record FieldModel(string Name, CelType Type);

/// <summary>An object type of a model, which becomes a dictionary type.</summary>
/// <param name="Name">The qualified name.</param>
/// <param name="Fields">The fields in the order of their numbers.</param>
internal sealed record ObjectTypeModel(string Name, FieldModel[] Fields);

/// <summary>An overload of a function of a model.</summary>
/// <param name="Id">The overload identifier.</param>
/// <param name="Target">The type of the receiver, or <see langword="null"/> for a global overload.</param>
/// <param name="Args">The types of the arguments, without the receiver.</param>
/// <param name="Return">The type of the result.</param>
internal sealed record OverloadModel(string Id, CelType? Target, CelType[] Args, CelType Return);

/// <summary>A function of a model with its overloads.</summary>
/// <param name="Name">The name, which may be qualified.</param>
/// <param name="Overloads">The overloads in order.</param>
internal sealed record FunctionModel(string Name, OverloadModel[] Overloads);

/// <summary>
/// A random description of an environment (docs/design/tests.md, "Environment from JSON"): the name, the container, the
/// variables, the functions and the object types of <c>greatPartsCel</c>, the calls that both builders get in code first, the
/// description printed as JSON, and the seed of a set of bindings. Names come from small pools, so repeats give build errors;
/// three descriptions in five are drawn without repeats and so build, which gives the comparison of the two environments its
/// cases.
/// </summary>
internal sealed class DescriptionModel
{
    private static readonly string[] VariableNames = ["a", "b", "c", "m.x", "m.y", "p.q.r", "1bad"];
    private static readonly string[] FunctionNames = ["f", "g", "h", "m.k"];
    private static readonly string[] OverloadIds = ["f_1", "f_2", "g_1", "g_2", "h_1", "k_1"];
    private static readonly string[] DeclaredTypes = ["m.A", "m.B", "n.C"];
    private static readonly string[] Referenced = ["m.A", "m.B", "n.C", "m.Z"];
    private static readonly string[] Containers = ["m", "p", "p.q", "m.n", "p.q.r"];
    private static readonly string[] FieldNames = ["a", "b", "c", "d"];
    private static readonly string[] Names = ["env", "e1", "e 2"];
    private static readonly CelType[] Simple = [CelType.Int, CelType.UInt, CelType.Double, CelType.Bool, CelType.String, CelType.Bytes];
    private static readonly CelType[] Keys = [CelType.Int, CelType.UInt, CelType.Bool, CelType.String];
    private static readonly CelType[] TypeParameters = [CelType.TypeParameter("A"), CelType.TypeParameter("B")];

    // The type of the variable in code: nested deeper than any type of a model, so that it equals none. The same declaration
    // of a variable from code and from a description is a conflict only when the description is JSON, which the calls cannot repeat.
    private static readonly CelType CodeVariableType = CelType.ListOf(CelType.ListOf(CelType.ListOf(CelType.Int)));

    /// <summary>A random description.</summary>
    internal static readonly Gen<DescriptionModel> Generator = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Create(new PCG(stream, seed)));

    private DescriptionModel()
    {
    }

    /// <summary>The <c>name</c> key, or <see langword="null"/> for none.</summary>
    internal string? Name { get; private set; }

    /// <summary>The <c>container</c> key; empty for none.</summary>
    internal string Container { get; private set; } = "";

    /// <summary>The variables in order.</summary>
    internal List<VariableModel> Variables { get; } = [];

    /// <summary>The functions in order.</summary>
    internal List<FunctionModel> Functions { get; } = [];

    /// <summary>The object types of <c>greatPartsCel</c> in order.</summary>
    internal List<ObjectTypeModel> Types { get; } = [];

    /// <summary>A container that both builders get in code, or <see langword="null"/>.</summary>
    internal string? CodeContainer { get; private set; }

    /// <summary>A variable that both builders get in code, or <see langword="null"/>.</summary>
    internal VariableModel? CodeVariable { get; private set; }

    /// <summary>The name of a type of a provider that both builders get in code, or <see langword="null"/>.</summary>
    internal string? CodeType { get; private set; }

    /// <summary>The description as JSON.</summary>
    internal string Json { get; private set; } = "";

    /// <summary>The seed of the bindings of the case.</summary>
    internal ulong BindingSeed { get; private set; }

    /// <summary>The container that the environment has when both sources give no conflict.</summary>
    internal string EffectiveContainer => Container.Length > 0 ? Container : CodeContainer ?? "";

    /// <summary>The description of the model as JSON, for a failure message.</summary>
    /// <returns>The JSON text.</returns>
    public override string ToString() => Json;

    private static DescriptionModel Create(PCG random)
    {
        bool clean = random.Next(5) < 3;
        var model = new DescriptionModel
        {
            Name = random.Next(3) == 0 ? null : Pick(random, Names),
            Container = random.Next(3) == 0 ? Pick(random, Containers) : "",
            BindingSeed = ((ulong)random.Next() << 32) | random.Next(),
        };
        model.CodeContainer = random.Next(4) == 0 ? (clean && model.Container.Length > 0 ? model.Container : Pick(random, Containers)) : null;
        model.CodeType = random.Next(4) == 0 ? Pick(random, DeclaredTypes) : null;
        model.AddTypes(random, clean);
        string[] references = clean ? model.DeclaredNames() : Referenced;
        model.AddVariables(random, clean, references);
        model.AddFunctions(random, clean, references);
        model.Json = DescriptionJson.Print(model, random);
        return model;
    }

    // The types of the description, with the fields that name the declared types. Without repeats, no type of the description has
    // the name of the type in code, and the names are distinct.
    private void AddTypes(PCG random, bool clean)
    {
        string[] pool = Shuffled(random, DeclaredTypes);
        var names = new List<string>();
        int count = (int)random.Next(4);
        for (int index = 0; index < count; index++)
        {
            if (!clean)
            {
                names.Add(Pick(random, DeclaredTypes));
            }
            else if (index < pool.Length && pool[index] != CodeType)
            {
                names.Add(pool[index]);
            }
        }

        string[] references = clean ? [.. names, .. CodeType is null ? [] : new[] { CodeType }] : Referenced;
        foreach (string name in names)
        {
            Types.Add(new ObjectTypeModel(name, Fields(random, references)));
        }
    }

    private string[] DeclaredNames()
    {
        var names = new List<string>();
        foreach (ObjectTypeModel type in Types)
        {
            names.Add(type.Name);
        }

        if (CodeType is not null)
        {
            names.Add(CodeType);
        }

        return [.. names];
    }

    private void AddVariables(PCG random, bool clean, string[] references)
    {
        string[] names = clean ? Shuffled(random, VariableNames[..^1]) : [];
        uint count = random.Next(7);
        for (int index = 0; index < count; index++)
        {
            if (clean && index >= names.Length)
            {
                break;
            }

            string name = clean ? names[index] : Pick(random, VariableNames);
            Variables.Add(new VariableModel(name, RandomType(random, 0, parameters: false, references)));
        }

        // The variable in code has a name that no variable of the description has when the names do not repeat.
        string codeName = clean ? FreeName(names) : Pick(random, VariableNames);
        if (codeName.Length > 0 && random.Next(4) == 0)
        {
            CodeVariable = new VariableModel(codeName, CodeVariableType);
        }
    }

    private string FreeName(string[] names)
    {
        foreach (string name in names)
        {
            if (!Variables.Exists(variable => variable.Name == name))
            {
                return name;
            }
        }

        return "";
    }

    private void AddFunctions(PCG random, bool clean, string[] references)
    {
        string[] names = clean ? Shuffled(random, FunctionNames) : [];
        uint count = random.Next(5);
        for (int index = 0; index < count && (!clean || index < names.Length); index++)
        {
            Functions.Add(clean ? CleanFunction(random, names[index], index, references) : RandomFunction(random, references));
        }
    }

    private static string Pick(PCG random, string[] pool) => pool[random.Next((uint)pool.Length)];

    private static string[] Shuffled(PCG random, string[] pool)
    {
        string[] names = [.. pool];
        for (int index = names.Length - 1; index > 0; index--)
        {
            int other = (int)random.Next((uint)index + 1);
            (names[index], names[other]) = (names[other], names[index]);
        }

        return names;
    }

    // Overloads of one call style and distinct numbers of arguments, so that none overlaps another, with identifiers of their own;
    // a type parameter of the result is the first argument.
    private static FunctionModel CleanFunction(PCG random, string name, int function, string[] references)
    {
        bool receiver = random.Next(3) == 0;
        var overloads = new OverloadModel[1 + random.Next(3)];
        for (int index = 0; index < overloads.Length; index++)
        {
            CelType? target = receiver ? RandomType(random, 0, parameters: true, references) : null;
            var args = new CelType[index];
            for (int arg = 0; arg < args.Length; arg++)
            {
                args[arg] = RandomType(random, 0, parameters: true, references);
            }

            CelType result = args.Length > 0 && args[0].Kind == CelTypeKind.TypeParameter && random.Next(2) == 0
                ? args[0]
                : RandomType(random, 0, parameters: false, references);
            overloads[index] = new OverloadModel(string.Create(CultureInfo.InvariantCulture, $"o{function}_{index}"), target, args, result);
        }

        return new FunctionModel(name, overloads);
    }

    private static FunctionModel RandomFunction(PCG random, string[] references)
    {
        var overloads = new OverloadModel[1 + random.Next(3)];
        for (int index = 0; index < overloads.Length; index++)
        {
            CelType? target = random.Next(3) == 0 ? RandomType(random, 0, parameters: true, references) : null;
            var args = new CelType[random.Next(3)];
            for (int arg = 0; arg < args.Length; arg++)
            {
                args[arg] = RandomType(random, 0, parameters: true, references);
            }

            overloads[index] = new OverloadModel(Pick(random, OverloadIds), target, args, RandomType(random, 0, parameters: true, references));
        }

        return new FunctionModel(Pick(random, FunctionNames), overloads);
    }

    // Field names of one type are distinct: the first k of the pool in a random order.
    private static FieldModel[] Fields(PCG random, string[] references)
    {
        string[] names = Shuffled(random, FieldNames);
        var fields = new FieldModel[random.Next(5)];
        for (int index = 0; index < fields.Length; index++)
        {
            fields[index] = new FieldModel(names[index], RandomType(random, 0, parameters: false, references));
        }

        return fields;
    }

    // A type of the grammar that the reader maps: simple types, dyn, null_type, wrappers, type, list and map up to depth 2, the
    // object types of the references, and type parameters where an overload takes them.
    private static CelType RandomType(PCG random, int depth, bool parameters, string[] references)
    {
        uint choice = random.Next(depth < 2 ? 12u : 9u);
        return choice switch
        {
            9 => CelType.ListOf(RandomType(random, depth + 1, parameters, references)),
            10 => CelType.MapOf(Keys[random.Next((uint)Keys.Length)], RandomType(random, depth + 1, parameters, references)),
            11 => CelType.TypeOf(RandomType(random, depth + 1, parameters, references)),
            _ => Leaf(random, choice, parameters, references),
        };
    }

    private static CelType Leaf(PCG random, uint choice, bool parameters, string[] references) => choice switch
    {
        1 => CelType.Dyn,
        2 => CelType.Null,
        3 => CelType.WrapperOf(Simple[random.Next((uint)Simple.Length)]),
        4 => CelType.TypeOf(CelType.Dyn),
        5 or 6 when references.Length > 0 => CelType.ObjectNamed(Pick(random, references)),
        7 when parameters => TypeParameters[random.Next((uint)TypeParameters.Length)],
        _ => Simple[random.Next((uint)Simple.Length)],
    };
}
