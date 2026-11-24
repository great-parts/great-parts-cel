using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// The description of an environment that the translation reads - the variables, the object types, the enumeration types, the
/// enumeration fields of the options and the fields that a value without a field is searched in - read once when the compiler is
/// created, immutable and shared by all calls.
/// </summary>
internal sealed class FieldCatalog
{
    private const string Options = "options";

    private readonly Dictionary<string, CelType> _variables;
    private readonly Dictionary<string, CelObjectType> _objectTypes;
    private readonly Dictionary<string, CelEnumType> _enumTypes;

    // The value of each constant of each enumeration type of the environment, by its full name.
    private readonly Dictionary<string, long> _constants;

    // The functions of the environment, by name.
    private readonly Dictionary<string, CelFunctionDecl> _functions;

    // The enumeration type of each field of the options, by the name of the variable or "<object type>.<field>".
    private readonly Dictionary<string, string> _enumFields;

    internal FieldCatalog(CelEnvironment environment, CelFilterOptions options)
    {
        _variables = new Dictionary<string, CelType>(environment.Variables.Length, StringComparer.Ordinal);
        foreach (CelVariableDecl variable in environment.Variables)
        {
            _variables[variable.Name] = variable.Type;
        }

        _objectTypes = new Dictionary<string, CelObjectType>(environment.ObjectTypes.Length, StringComparer.Ordinal);
        foreach (CelObjectType type in environment.ObjectTypes)
        {
            _objectTypes[type.Name] = type;
        }

        _enumTypes = new Dictionary<string, CelEnumType>(environment.EnumTypes.Length, StringComparer.Ordinal);
        _constants = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (CelEnumType type in environment.EnumTypes)
        {
            _enumTypes[type.Name] = type;
            foreach (KeyValuePair<string, long> constant in type.Constants)
            {
                _constants[type.Name + "." + constant.Key] = constant.Value;
            }
        }

        _functions = new Dictionary<string, CelFunctionDecl>(environment.Functions.Length, StringComparer.Ordinal);
        foreach (CelFunctionDecl function in environment.Functions)
        {
            _functions[function.Name] = function;
        }

        _enumFields = new Dictionary<string, string>(options.EnumTypes, StringComparer.Ordinal);
        CheckEnumFields();
        SearchPaths = ReadSearchPaths(options);
    }

    /// <summary>
    /// The fields in which a value without a field is searched, resolved to paths of the environment, in the order of the options.
    /// </summary>
    internal ImmutableArray<ResolvedPath> SearchPaths { get; }

    /// <summary>
    /// Resolves the parts of a dotted name: the longest start whose dotted name is a variable, then the fields of the types.
    /// </summary>
    /// <param name="names">The names of the parts, at least one.</param>
    /// <returns>The path; a name that the environment does not declare is a path without a variable.</returns>
    internal ResolvedPath Resolve(IReadOnlyList<string> names)
    {
        for (int length = names.Count; length >= 1; length--)
        {
            if (_variables.TryGetValue(Joined(names, length), out CelType? variable))
            {
                return ResolveFields(names, length, variable);
            }
        }

        return new ResolvedPath(names, 0, []);
    }

    /// <summary>
    /// Finds the enumeration type of a field.
    /// </summary>
    /// <param name="path">The path of the field.</param>
    /// <returns>The full name of the enumeration type of the options for the field; <see langword="null"/> when the field is not mapped.</returns>
    internal string? EnumTypeOf(ResolvedPath path)
    {
        if (path.VariableLength == 0)
        {
            return null;
        }

        if (path.Names.Count == path.VariableLength)
        {
            return _enumFields.GetValueOrDefault(Joined(path.Names, path.Names.Count));
        }

        // A field of an object type: the key is the full name of the type that holds the field, a dot and the name of the field.
        return path.TypeAfter(path.Names.Count - 2) is { Kind: CelTypeKind.Object } holder
            ? _enumFields.GetValueOrDefault(holder.Name + "." + path.Names[^1])
            : null;
    }

    /// <summary>
    /// Tells whether a name is a variable of the environment.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the environment declares a variable with the name.</returns>
    internal bool IsVariable(string name) => _variables.ContainsKey(name);

    /// <summary>
    /// Finds an enumeration type of the environment.
    /// </summary>
    /// <param name="name">The full name of the type.</param>
    /// <returns>The type; <see langword="null"/> when the environment has none with the name.</returns>
    internal CelEnumType? EnumTypeNamed(string name) => _enumTypes.GetValueOrDefault(name);

    /// <summary>
    /// Finds the value of a constant of an enumeration type of the environment by its full name.
    /// </summary>
    /// <param name="fullName">The full name of the constant, such as <c>acme.State.ACTIVE</c>.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when an enumeration type of the environment has the constant.</returns>
    internal bool TryGetConstant(string fullName, out long value) => _constants.TryGetValue(fullName, out value);

    /// <summary>
    /// Finds the type that a call of a function returns, when the number of arguments settles it.
    /// </summary>
    /// <param name="function">The name of the function.</param>
    /// <param name="arguments">The number of arguments of the call.</param>
    /// <returns>The result type of the overloads of the function that take this number of arguments, when all have the same one; <see langword="null"/> otherwise.</returns>
    internal CelType? ResultTypeOf(string function, int arguments)
    {
        if (!_functions.TryGetValue(function, out CelFunctionDecl? declaration))
        {
            return null;
        }

        CelType? result = null;
        foreach (CelOverloadDecl overload in declaration.Overloads)
        {
            if (overload.IsReceiverStyle || overload.ParameterTypes.Length != arguments)
            {
                continue;
            }

            if (result is not null && !result.Equals(overload.ResultType))
            {
                return null;
            }

            result = overload.ResultType;
        }

        return result;
    }

    private static string Joined(IReadOnlyList<string> names, int length)
    {
        if (length == 1)
        {
            return names[0];
        }

        StringBuilder joined = new(names[0]);
        for (int i = 1; i < length; i++)
        {
            joined.Append('.').Append(names[i]);
        }

        return joined.ToString();
    }

    private ResolvedPath ResolveFields(IReadOnlyList<string> names, int variableLength, CelType variable)
    {
        List<CelType?> types = [variable];
        CelType? current = variable;
        for (int i = variableLength; i < names.Count; i++)
        {
            current = current is null ? null : FieldType(current, names[i]);
            types.Add(current);
        }

        return new ResolvedPath(names, variableLength, [.. types]);
    }

    // The type of the field of a value of a type: a field of an object, the value of a map, a field of the elements of a list (the
    // path then goes through the list with a loop); unknown for other types.
    private CelType? FieldType(CelType holder, string name)
    {
        switch (holder.Kind)
        {
            case CelTypeKind.List:
                return FieldType(holder.Parameters[0], name);
            case CelTypeKind.Object:
                return _objectTypes.TryGetValue(holder.Name, out CelObjectType? type) && type.TryGetField(name, out CelObjectField? field)
                    ? field.Type
                    : null;
            case CelTypeKind.Map:
                return holder.Parameters[1];
            default:
                return null;
        }
    }

    // The options that name a field: the field is an int field of the environment, and the enumeration type is one of the environment.
    private void CheckEnumFields()
    {
        foreach ((string key, string enumType) in _enumFields)
        {
            CelType fieldType = TypeOfKey(key)
                ?? throw new ArgumentException("The key '" + key + "' of EnumTypes names neither a variable nor a field of an object type of the environment.", Options);
            if (fieldType.Kind != CelTypeKind.Int)
            {
                throw new ArgumentException("The field '" + key + "' of EnumTypes is not of the type int.", Options);
            }

            if (!_enumTypes.ContainsKey(enumType))
            {
                throw new ArgumentException("The enumeration type '" + enumType + "' of the field '" + key + "' is not a type of the environment.", Options);
            }
        }
    }

    // The type of a key of the options: a variable, or the full name of an object type, a dot and the name of its field.
    private CelType? TypeOfKey(string key)
    {
        if (_variables.TryGetValue(key, out CelType? variable))
        {
            return variable;
        }

        int dot = key.AsSpan().LastIndexOf('.');
        return dot > 0 && _objectTypes.TryGetValue(key[..dot], out CelObjectType? holder) && holder.TryGetField(key[(dot + 1)..], out CelObjectField? field)
            ? field.Type
            : null;
    }

    private ImmutableArray<ResolvedPath> ReadSearchPaths(CelFilterOptions options)
    {
        ImmutableArray<ResolvedPath>.Builder paths = ImmutableArray.CreateBuilder<ResolvedPath>(options.SearchFields.Length);
        foreach (string field in options.SearchFields)
        {
            ResolvedPath path = Resolve(field.Split('.'));
            if (path.VariableLength == 0 || !IsString(path.FinalType))
            {
                throw new ArgumentException(
                    "The search field '" + field + "' is not a path from a variable of the environment to a field of the type string or the wrapper of string.",
                    Options);
            }

            paths.Add(path);
        }

        return paths.MoveToImmutable();
    }

    private static bool IsString(CelType? type) =>
        type is { Kind: CelTypeKind.String } or { Kind: CelTypeKind.Wrapper, Parameters: [{ Kind: CelTypeKind.String }] };
}
