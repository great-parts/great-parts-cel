using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

// One call of the simple entry (docs/design/simple-entry.md, "One call"): the shape of the data, the hidden environment of the shape
// from level 1 or built and added, the program from level 2 or compiled and added, the bindings, the evaluation with the limit of the
// call; every error is thrown as CelException.
internal static class SimpleEntryCall
{
    private const string AnonymousTypePrefix = "<>f__AnonymousType";

    // The bindings of the thread: one instance that a call takes (the field is set to null), fills and evaluates with, and puts back
    // cleared in a finally block, so that the references to the data of the application are not kept after the call. A call that finds the
    // field empty makes an instance of its own: a call from inside an evaluation on the same thread (a getter of a member of an object
    // that calls the simple entry), or a call after an exception that left the instance taken. Such an instance goes into the field
    // when its call ends, and the call that took the first instance puts that one back over it. The simple entry has no asynchronous
    // call, so the instance does not outlive a call.
    [ThreadStatic]
    private static CelBindings? t_bindings;

    // The overload with a dictionary of names and values.
    internal static T Evaluate<T>(
        SimpleEntryCache cache,
        string expression,
        IReadOnlyDictionary<string, object?>? variables,
        ulong costLimit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        cancellationToken.ThrowIfCancellationRequested();
        return EvaluateObjects<T>(cache, expression, variables, nameof(variables), costLimit, cancellationToken);
    }

    // The overload with data of the type object: a dictionary of the closed list of docs/type-mapping.md, an anonymous object of a registered
    // type, or an error that names the type.
    internal static T Evaluate<T>(SimpleEntryCache cache, string expression, object data, ulong costLimit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        if (data is IReadOnlyDictionary<string, object?> dictionary)
        {
            return EvaluateObjects<T>(cache, expression, dictionary, nameof(data), costLimit, cancellationToken);
        }

        if (AnonymousRegistry.TryGet(data.GetType(), out AnonymousRegistry.Entry? entry))
        {
            return EvaluateAnonymous<T>(cache, expression, data, entry, costLimit, cancellationToken);
        }

        if (ConversionRules.TryFromRuntimeCollection(data, out CelValue converted) && converted.TryGetMap(out CelMap? map)
            && map.RecordedType is { Kind: CelTypeKind.Map } mapType && mapType.Parameters[0].Kind == CelTypeKind.String)
        {
            return EvaluateTyped<T>(cache, expression, map, mapType.Parameters[1], nameof(data), costLimit, cancellationToken);
        }

        throw CelException.FromErrors([NotDeclared("data", "data", data.GetType(), hint: DataHint(data.GetType()))]);
    }

    // A dictionary IReadOnlyDictionary<string, object?>: the type of each variable is that of its value.
    private static T EvaluateObjects<T>(
        SimpleEntryCache cache,
        string expression,
        IReadOnlyDictionary<string, object?>? variables,
        string parameter,
        ulong costLimit,
        CancellationToken cancellationToken)
    {
        int capacity = variables?.Count ?? 0;
        ShapeSlot[] slots = ArrayPool<ShapeSlot>.Shared.Rent(capacity);
        try
        {
            int count = 0;
            List<CelError>? errors = null;
            if (variables is not null)
            {
                foreach (KeyValuePair<string, object?> pair in variables)
                {
                    CheckName(pair.Key, parameter);
                    SimpleValues.Outcome outcome = SimpleValues.TryConvert(pair.Value, out CelValue converted, out CelType type);
                    if (outcome == SimpleValues.Outcome.Outside)
                    {
                        (errors ??= []).Add(NotDeclared("variable", pair.Key, pair.Value!.GetType(), hint: ValueHint(pair.Value.GetType())));
                        continue;
                    }

                    if (outcome == SimpleValues.Outcome.Refused)
                    {
                        throw new ArgumentException(ConversionRules.BindingsRefusalMessage(converted), parameter);
                    }

                    slots[count++] = new ShapeSlot { Name = pair.Key, Type = type, Value = converted };
                }
            }

            ThrowIf(errors);
            return EvaluateSlots<T>(cache, expression, slots, count, costLimit, cancellationToken);
        }
        finally
        {
            ArrayPool<ShapeSlot>.Shared.Return(slots, clearArray: true);
        }
    }

    // A dictionary of one value type, read through the lazy view of the input table: the type of the variables is that of the values.
    private static T EvaluateTyped<T>(
        SimpleEntryCache cache,
        string expression,
        CelMap map,
        CelType valueType,
        string parameter,
        ulong costLimit,
        CancellationToken cancellationToken)
    {
        ShapeSlot[] slots = ArrayPool<ShapeSlot>.Shared.Rent(map.Count);
        try
        {
            int count = 0;
            foreach (KeyValuePair<CelValue, CelValue> pair in map)
            {
                _ = pair.Key.TryGetString(out string? name);
                CheckName(name!, parameter);
                if (ConversionRules.IsRefusedByBindings(pair.Value))
                {
                    throw new ArgumentException(ConversionRules.BindingsRefusalMessage(pair.Value), parameter);
                }

                slots[count++] = new ShapeSlot { Name = name!, Type = valueType, Value = pair.Value };
            }

            return EvaluateSlots<T>(cache, expression, slots, count, costLimit, cancellationToken);
        }
        finally
        {
            ArrayPool<ShapeSlot>.Shared.Return(slots, clearArray: true);
        }
    }

    // The slots are the variables of the data: sorted by name, they are the shape of level 1.
    private static T EvaluateSlots<T>(
        SimpleEntryCache cache,
        string expression,
        ShapeSlot[] slots,
        int count,
        ulong costLimit,
        CancellationToken cancellationToken)
    {
        slots.AsSpan(0, count).Sort(static (left, right) => string.CompareOrdinal(left.Name, right.Name));
        var probe = new ShapeProbe(slots, count);
        if (!cache.TryGetEnvironment(probe, out SimpleEntryCache.EnvironmentEntry? environment))
        {
            environment = cache.AddEnvironment(new ShapeKey(probe), Build(slots, count));
        }

        CelProgram<T> program = cache.GetOrCompile<T>(environment, expression).GetProgramOrThrow();
        if (count == 0)
        {
            return Run(program, CelBindings.Empty, costLimit, cancellationToken);
        }

        CelBindings bindings = TakeBindings();
        try
        {
            for (int i = 0; i < count; i++)
            {
                bindings.Set(slots[i].Name, slots[i].Value);
            }

            return Run(program, bindings, costLimit, cancellationToken);
        }
        finally
        {
            ReleaseBindings(bindings);
        }
    }

    // An anonymous object of a registered type: the environment of the type by the reference of its entry of the registry, the program by
    // the table of the recent programs of the environment, the bindings by the delegate of the registration. The bindings take only the
    // names that the plan of the program can read (Plan.ReadNames) and only while the delegate runs: the value of every property is still
    // converted and checked, so that an error of a value that the program does not read is still thrown, but the name that no node reads
    // is not inserted into the dictionary.
    private static T EvaluateAnonymous<T>(
        SimpleEntryCache cache,
        string expression,
        object data,
        AnonymousRegistry.Entry entry,
        ulong costLimit,
        CancellationToken cancellationToken)
    {
        SimpleEntryCache.EnvironmentEntry environment = cache.GetEnvironment(entry);
        CelProgram<T> program = cache.GetOrCompile<T>(environment, expression).GetProgramOrThrow();
        CelBindings bindings = TakeBindings();
        try
        {
            bindings.SetNameFilter(program.Plan.ReadNames);
            entry.Bind(data, bindings);
            bindings.SetNameFilter(null);
            return Run(program, bindings, costLimit, cancellationToken);
        }
        finally
        {
            ReleaseBindings(bindings);
        }
    }

    private static T Run<T>(CelProgram<T> program, CelBindings bindings, ulong costLimit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return program.EvaluateWithLimit(bindings, costLimit, cancellationToken).GetValueOrThrow();
    }

    // Takes the bindings of the thread out of the field, or makes an instance when the field is empty.
    private static CelBindings TakeBindings()
    {
        CelBindings? bindings = t_bindings;
        if (bindings is null)
        {
            return new CelBindings();
        }

        t_bindings = null;
        return bindings;
    }

    // The filter of the names goes with the values, whether the call ended or threw: the instance in the field has none.
    private static void ReleaseBindings(CelBindings bindings)
    {
        bindings.SetNameFilter(null);
        bindings.Clear();
        t_bindings = bindings;
    }

    // The bindings that the current thread keeps between calls, or null while a call has taken them or when no call has been made: the
    // tests read it to see that it is empty after a call and not shared by a call inside a call.
    internal static CelBindings? ThreadBindings => t_bindings;

    // The hidden environment of a dictionary: the standard library version 1 and one variable per slot. A key that is not a name is
    // InvalidVariableName of the build; the shape is not kept then.
    private static CelEnvironment Build(ShapeSlot[] slots, int count)
    {
        var builder = new CelEnvironmentBuilder();
        for (int i = 0; i < count; i++)
        {
            builder.AddVariable(slots[i].Name, slots[i].Type);
        }

        return builder.Build().GetEnvironmentOrThrow();
    }

    private static void CheckName(string name, string parameter)
    {
        if (name.Length == 0)
        {
            throw new ArgumentException("The name of a variable is empty.", parameter);
        }
    }

    private static void ThrowIf(List<CelError>? errors)
    {
        if (errors is null)
        {
            return;
        }

        // The errors in the order of their messages, which name the variable, not of the enumeration of the dictionary.
        errors.Sort(static (left, right) => string.CompareOrdinal(left.Message, right.Message));
        throw CelException.FromErrors([.. errors]);
    }

    private static CelError NotDeclared(string kind, string name, Type type, string hint) =>
        BuildErrors.TypeNotDeclared(kind, name, type.FullName ?? type.Name, hint: hint);

    private static string ValueHint(Type type) =>
        IsDecimal(type)
            ? "; the simple entry does not read decimal: build a CelEnvironment with the extension set 'decimal'"
            : "; the simple entry reads only the values of the type mapping: build a CelEnvironment with a type source or an extension set";

    private static string DataHint(Type type) =>
        type.Name.StartsWith(AnonymousTypePrefix, StringComparison.Ordinal)
            ? "; pass the anonymous object to CelExpression.Evaluate directly, or pass a dictionary"
            : "; the simple entry reads a dictionary or an anonymous object: pass a class of the application as a property of an anonymous object, such as new { order }, or build a CelEnvironment with a type source and UseInput";

    private static bool IsDecimal(Type type) => type == typeof(decimal) || string.Equals(type.FullName, "System.Numerics.Decimal128", StringComparison.Ordinal);
}
