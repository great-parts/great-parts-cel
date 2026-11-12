using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// A message literal T{f: v, …} of a constructible description. The values are evaluated from left to right onto the
// argument stack, and the first error value is the result. Then the node charges 1 + n + ⌈L/10⌉ — n entries as written, L the
// lengths of the values that are strings, in UTF-16 units, and bytes, in bytes — a field number that occurs more than once
// keeps the last value, and the description creates the instance inside the exception boundary of application code: an
// exception is ApplicationException at the literal, and an error value without a place gets the place of the literal. With type
// checking the description and the field numbers come from the reference map; without it the name of the type resolves at
// evaluation by the candidates of the container, and the field names by the description found. An entry with ? is evaluated like
// the others, and its value must be an optional: an empty one drops the entry, which is then not among the entries of the creation
// and not counted in n or L, and a non-empty one passes its held value on. The charge is then 1 + m + ⌈L/10⌉ for the m entries stored.
internal sealed class MessagePlanNode : PlanNode
{
    // A literal after type checking: the description and the field number of each entry.
    private readonly CelObjectType? _description;
    private readonly ImmutableArray<int> _numbers;
    private readonly bool _hasRepeat;

    // A literal without type checking: the name as written, its candidates in the container, longest first, and the field
    // names of the entries.
    private readonly string? _name;
    private readonly ImmutableArray<string> _candidates;
    private readonly ImmutableArray<string> _fieldNames;

    private readonly ImmutableArray<PlanNode> _values;

    // Whether each entry has a ?; default for a literal with none, which charges 1 + n at once.
    private readonly ImmutableArray<bool> _optional;

    internal MessagePlanNode(
        int offset,
        CelObjectType description,
        ImmutableArray<int> numbers,
        ImmutableArray<PlanNode> values,
        ImmutableArray<bool> optional = default)
        : base(offset, optional.IsDefault ? 1 + (ulong)values.Length : 1)
    {
        _description = description;
        _numbers = numbers;
        _hasRepeat = HasRepeat(numbers.AsSpan());
        _values = values;
        _optional = optional;
    }

    internal MessagePlanNode(
        int offset,
        string name,
        ImmutableArray<string> candidates,
        ImmutableArray<string> fieldNames,
        ImmutableArray<PlanNode> values,
        ImmutableArray<bool> optional = default)
        : base(offset, optional.IsDefault ? 1 + (ulong)values.Length : 1)
    {
        _name = name;
        _candidates = candidates;
        _fieldNames = fieldNames;
        _values = values;
        _optional = optional;
    }

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        if (!TryEvaluateValues(frame, out int stored, out int[]? kept, out error))
        {
            return error;
        }

        Begin(frame);
        if (frame.Metered)
        {
            ulong units = ((LengthOfWrittenData(frame.Peek(stored)) + 9) / 10) + (_optional.IsDefault ? 0UL : (ulong)stored);
            if (units != 0)
            {
                frame.Charge(units);
            }
        }

        CelValue result = CreateFromStored(frame, kept, stored);
        frame.Pop(stored);
        return result;
    }

    // Evaluates the values from left to right onto the stack. The values of the entries that are stored are on the stack and
    // counted in stored; the entries of a literal with ? that were dropped are not. After the first dropped entry, kept holds the
    // index of each stored one; before it, it is null and the stored entries are the first ones. False with the error value that
    // ended the evaluation, and nothing left on the stack.
    private bool TryEvaluateValues(Frame frame, out int stored, out int[]? kept, out CelValue error)
    {
        stored = 0;
        kept = null;
        error = default;
        for (int i = 0; i < _values.Length; i++)
        {
            CelValue value = _values[i].Evaluate(frame);
            if (value.Kind == CelValueKind.Error)
            {
                frame.Pop(stored);
                error = value;
                return false;
            }

            if (!_optional.IsDefault && _optional[i])
            {
                if (!value.TryGetOptional(out CelOptional<CelValue> optional))
                {
                    frame.Pop(stored);
                    error = EvaluationErrors.NotAnOptional(value, frame, _values[i].Offset);
                    return false;
                }

                if (!optional.HasValue)
                {
                    kept ??= FirstIndices(_values.Length, stored);
                    continue;
                }

                value = optional.Value;
            }

            if (kept is not null)
            {
                kept[stored] = i;
            }

            frame.Push(value);
            stored++;
        }

        return true;
    }

    // Creates the instance from the values that are on the stack, one for each field number of the entries that were stored.
    private CelValue CreateFromStored(Frame frame, int[]? kept, int stored)
    {
        if (_description is not null)
        {
            if (kept is null)
            {
                return Create(frame, _description, _numbers.AsSpan(), _hasRepeat, stored);
            }

            int[] storedNumbers = Gather(_numbers.AsSpan(), kept, stored);
            return Create(frame, _description, storedNumbers, HasRepeat(storedNumbers), stored);
        }

        if (!TryResolve(frame, out CelObjectType? description, out int[]? resolved, out CelValue error))
        {
            return error;
        }

        int[] numbers = kept is null ? resolved : Gather(resolved, kept, stored);
        return Create(frame, description, numbers, HasRepeat(numbers), stored);
    }

    // The field numbers of the entries that were stored, in the order of the text.
    private static int[] Gather(ReadOnlySpan<int> numbers, int[] kept, int stored)
    {
        int[] gathered = stored == 0 ? [] : new int[stored];
        for (int i = 0; i < gathered.Length; i++)
        {
            gathered[i] = numbers[kept[i]];
        }

        return gathered;
    }

    // Creates the instance from the values that are on the stack, one for each field number.
    private CelValue Create(Frame frame, CelObjectType description, ReadOnlySpan<int> numbers, bool hasRepeat, int count) =>
        hasRepeat ? CreateWithoutRepeats(frame, description, numbers, count) : Create(frame, description, numbers, frame.Peek(count));

    // Creates the instance from distinct field numbers and their values.
    private CelValue Create(Frame frame, CelObjectType description, ReadOnlySpan<int> numbers, ReadOnlySpan<CelValue> values)
    {
        CelValue result;
        try
        {
            frame.Event();
            result = description.CreateInstance(numbers, values);
        }
        catch (Exception exception) when (frame.IsApplicationException(exception))
        {
            return EvaluationErrors.CreationException(exception, description, frame.Lines, Offset);
        }

        return EvaluationErrors.Placed(result, frame.Lines, Offset);
    }

    // A field number that occurs more than once keeps the value of its last occurrence; the numbers are then distinct and in the
    // order of their last occurrence.
    private CelValue CreateWithoutRepeats(Frame frame, CelObjectType description, ReadOnlySpan<int> numbers, int count)
    {
        ReadOnlySpan<CelValue> pushed = frame.Peek(count);
        Dictionary<int, int> last = new(count);
        for (int i = 0; i < count; i++)
        {
            last[numbers[i]] = i;
        }

        int[] distinctNumbers = new int[last.Count];
        CelValue[] distinctValues = new CelValue[last.Count];
        int next = 0;
        for (int i = 0; i < count; i++)
        {
            if (last[numbers[i]] == i)
            {
                distinctNumbers[next] = numbers[i];
                distinctValues[next++] = pushed[i];
            }
        }

        return Create(frame, description, distinctNumbers, distinctValues);
    }

    // The description by the name as written and the field numbers by the field names, without type checking. No candidate that
    // is a description, or one that is not constructible, is Unbound for the name as written; a field the description does not
    // have is EvalNoSuchField at the literal.
    private bool TryResolve(
        Frame frame,
        [NotNullWhen(true)] out CelObjectType? description,
        [NotNullWhen(true)] out int[]? numbers,
        out CelValue error)
    {
        description = null;
        numbers = null;
        foreach (string candidate in _candidates)
        {
            if (frame.Types.TryGetObjectType(candidate, out CelObjectType? found))
            {
                description = found.IsConstructible ? found : null;
                break;
            }
        }

        if (description is null)
        {
            error = EvaluationErrors.Unbound(_name!, frame.Lines, Offset);
            return false;
        }

        int[] resolved = _fieldNames.Length == 0 ? [] : new int[_fieldNames.Length];
        for (int i = 0; i < resolved.Length; i++)
        {
            if (!description.TryGetField(_fieldNames[i], out CelObjectField? field))
            {
                error = EvaluationErrors.NoSuchField(_fieldNames[i], description, frame.Lines, Offset);
                description = null;
                return false;
            }

            resolved[i] = field.Number;
        }

        numbers = resolved;
        error = default;
        return true;
    }

    // The lengths of the values written directly in the fields that are strings, in UTF-16 units, and bytes, in bytes; not the
    // elements of a list or a map.
    private static ulong LengthOfWrittenData(ReadOnlySpan<CelValue> values)
    {
        ulong length = 0;
        foreach (CelValue value in values)
        {
            if (value.TryGetString(out string? text))
            {
                length += (ulong)text.Length;
            }
            else if (value.TryGetBytes(out ReadOnlyMemory<byte> bytes))
            {
                length += (ulong)bytes.Length;
            }
        }

        return length;
    }

    // Whether a number occurs twice: a scan of the pairs for a few entries, and a set for more, so that the time stays linear in the
    // number of entries.
    private static bool HasRepeat(ReadOnlySpan<int> numbers)
    {
        if (numbers.Length <= 16)
        {
            for (int i = 1; i < numbers.Length; i++)
            {
                if (numbers[..i].Contains(numbers[i]))
                {
                    return true;
                }
            }

            return false;
        }

        HashSet<int> seen = new(numbers.Length);
        foreach (int number in numbers)
        {
            if (!seen.Add(number))
            {
                return true;
            }
        }

        return false;
    }
}
