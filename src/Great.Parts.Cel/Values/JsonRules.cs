using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The rules of <see cref="JsonElement"/> as a CEL value: the kind of an element decides its value, a number is a
/// <c>double</c>, and a repeated property name of an object is <see cref="CelErrorCode.DuplicateMapKey"/> in every
/// operation that touches the key and in every operation that looks at all keys.
/// </summary>
internal static class JsonRules
{
    private const string Callee = "a JSON element";

    /// <summary>
    /// The value of an element by its kind. An array and an object are lazy adapters of the element; a string is
    /// created at each call.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">What the message names when the element is undefined; <see langword="null"/> for none.</param>
    /// <returns>
    /// The value; <c>ConversionRange</c> for a number that is not a finite <c>double</c>; <c>InvalidArgument</c> for an
    /// undefined element; <c>ApplicationException</c> for an element of a disposed document. An error value has no
    /// place.
    /// </returns>
    internal static CelValue FromElement(JsonElement element, string? name = null)
    {
        try
        {
            return Convert(element, name, null);
        }
        catch (ObjectDisposedException exception)
        {
            return ConversionRules.ApplicationError(exception, Callee);
        }
    }

    /// <summary>
    /// The value of an element that an operation of an evaluation reads: a string is charged, 1 + ⌈L/10⌉ for the length L
    /// of its raw token, before it is created.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <returns>The value, as <see cref="FromElement"/> gives it.</returns>
    internal static CelValue Read(JsonElement element, IEvaluationMeter? meter)
    {
        try
        {
            return Convert(element, null, meter, charged: true);
        }
        catch (ObjectDisposedException exception)
        {
            return ConversionRules.ApplicationError(exception, Callee);
        }
    }

    /// <summary>
    /// The element at an index of an array, as a value.
    /// </summary>
    /// <param name="array">The array element.</param>
    /// <param name="index">The index, already checked against the length.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit, which charges the string of the element before it is created.</param>
    /// <returns>The value of the element.</returns>
    internal static CelValue ItemOf(JsonElement array, int index, IEvaluationMeter? meter)
    {
        try
        {
            return Read(array[index], meter);
        }
        catch (ObjectDisposedException exception)
        {
            return ConversionRules.ApplicationError(exception, Callee);
        }
    }

    /// <summary>
    /// The counting lookup of a name: a name is compared ordinally with every property name of the object, escape
    /// sequences decoded, and the matches are counted.
    /// </summary>
    /// <param name="obj">The object element.</param>
    /// <param name="name">The name.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit, which charges the string of the match before it is created.</param>
    /// <param name="readValue"><see langword="false"/> to learn only whether the name is present: the value of a single match is then <c>null</c> and no string is created.</param>
    /// <param name="value">The value of the single match; the error value <c>DuplicateMapKey</c> for two or more; <c>ApplicationException</c> for a disposed document.</param>
    /// <returns><see langword="false"/> when no property has the name.</returns>
    internal static bool TryFind(JsonElement obj, string name, IEvaluationMeter? meter, bool readValue, out CelValue value)
    {
        value = default;
        try
        {
            int matches = 0;
            JsonElement found = default;
            foreach (JsonProperty property in obj.EnumerateObject())
            {
                if (property.NameEquals(name) && ++matches == 1)
                {
                    found = property.Value;
                }
                else if (matches > 1)
                {
                    value = DuplicateKey(name);
                    return true;
                }
            }

            if (matches == 0)
            {
                return false;
            }

            value = readValue ? Read(found, meter) : CelValue.Null;
            return true;
        }
        catch (ObjectDisposedException exception)
        {
            value = ConversionRules.ApplicationError(exception, Callee);
            return true;
        }
    }

    /// <summary>
    /// The units of a counting lookup of a key beyond the constant unit of the operation: it walks all n properties and
    /// compares the key with each, n · (1 + ⌈L/10⌉) for a key of L code units.
    /// </summary>
    /// <param name="obj">The object element.</param>
    /// <param name="keyTenths">⌈L/10⌉ of the key.</param>
    /// <returns>The units; 0 for a disposed document, whose lookup is then the error value of the document.</returns>
    internal static ulong ProbeUnits(JsonElement obj, ulong keyTenths)
    {
        try
        {
            return (ulong)obj.GetPropertyCount() * (1 + keyTenths);
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
    }

    /// <summary>
    /// The number of elements of an array, which an index charges beyond the constant unit: an element is found by a pass
    /// over the array when it holds composite elements, so n bounds it.
    /// </summary>
    /// <param name="array">The array element.</param>
    /// <returns>The length; 0 for a disposed document.</returns>
    internal static ulong IndexUnits(JsonElement array)
    {
        try
        {
            return (ulong)array.GetArrayLength();
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Looks at all property names of an object for a repeat, by hashing them with the random seed of the process. With a
    /// meter, each property is charged before it is processed: 1 + ⌈Lᵢ/10⌉ for the length of its raw name in bytes, which
    /// the document gives without a copy.
    /// </summary>
    /// <param name="obj">The object element.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="leadingUnit">Whether to charge the leading 1 of the row first, before the properties.</param>
    /// <param name="error">The error value <c>DuplicateMapKey</c> with the first name, in document order, that repeats an earlier one; <c>ApplicationException</c> for a disposed document.</param>
    /// <returns><see langword="true"/> when a name repeats or the document is disposed.</returns>
    internal static bool TryGetRepeat(JsonElement obj, IEvaluationMeter? meter, bool leadingUnit, out CelValue error)
    {
        error = default;
        if (leadingUnit)
        {
            meter?.Charge(1);
        }

        try
        {
            int count = obj.GetPropertyCount();
            if (count == 0)
            {
                return false;
            }

            if (count == 1)
            {
                if (meter is not null)
                {
                    foreach (JsonProperty only in obj.EnumerateObject())
                    {
                        meter.Charge(NameUnits(only));
                    }
                }

                return false;
            }

            // Without a meter the set is made for all names at once; with one it grows with the names that were charged, so a
            // stop at the limit leaves nothing allocated for the names that follow.
            var seen = meter is null ? new HashSet<string>(count, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in obj.EnumerateObject())
            {
                meter?.Charge(NameUnits(property));
                string name = property.Name;
                if (!seen.Add(name))
                {
                    error = DuplicateKey(name);
                    return true;
                }
            }

            return false;
        }
        catch (ObjectDisposedException exception)
        {
            error = ConversionRules.ApplicationError(exception, Callee);
            return true;
        }
    }

    /// <summary>
    /// Creates the enumerator of the entries of an object in document order: the name as a string and the value of
    /// the property.
    /// </summary>
    /// <param name="obj">The object element.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit, which charges the string of a value before it is created.</param>
    /// <param name="readValues">
    /// <see langword="false"/> for an operation that reads only the names, such as the range of a comprehension: the value of
    /// an entry is then <c>null</c> and no string is created.
    /// </param>
    /// <returns>The enumerator.</returns>
    internal static IEnumerator<KeyValuePair<CelValue, CelValue>> Entries(JsonElement obj, IEvaluationMeter? meter, bool readValues) =>
        new EntryEnumerator(obj, meter, readValues);

    // The units of one property of the check of repeats: 1 + ⌈Lᵢ/10⌉, Lᵢ the length of the raw name in bytes with its escapes.
    private static ulong NameUnits(JsonProperty property) => 1 + (((ulong)JsonMarshal.GetRawUtf8PropertyName(property).Length + 9) / 10);

    // The error value of an element of a disposed document that the enumeration met: the entry is the error.
    private static KeyValuePair<CelValue, CelValue> FailedEntry(ObjectDisposedException exception) =>
        new(CelValue.FromString(string.Empty), ConversionRules.ApplicationError(exception, Callee));

    // The units of the read of a string element: 1 + ⌈L/10⌉, L the length of the raw token in bytes without its quotes, escape
    // sequences included, which the document gives without decoding.
    private static ulong StringUnits(JsonElement element) => 1 + (((ulong)JsonMarshal.GetRawUtf8Value(element).Length - 2 + 9) / 10);

    // The value of an element. A string of a binding keeps the units of its reads; a string that an operation reads is charged
    // first, so that the limit stops it before the string is created.
    private static CelValue Convert(JsonElement element, string? name, IEvaluationMeter? meter, bool charged = false)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Null:
                return CelValue.Null;
            case JsonValueKind.True:
                return CelValue.FromBoolean(true);
            case JsonValueKind.False:
                return CelValue.FromBoolean(false);
            case JsonValueKind.Number:
                return element.TryGetDouble(out double number) && double.IsFinite(number)
                    ? CelValue.FromDouble(number)
                    : Error(CelErrorCode.ConversionRange, element.GetRawText() + " is out of the range of double");
            case JsonValueKind.String:
                if (!charged)
                {
                    return CelValue.FromJsonString(element.GetString()!, StringUnits(element));
                }

                meter?.Charge(StringUnits(element));
                return CelValue.FromString(element.GetString()!);
            case JsonValueKind.Array:
                return CelValue.FromList(new JsonArrayList(element));
            case JsonValueKind.Object:
                return CelValue.FromMap(new JsonObjectMap(element));
            default:
                return Error(
                    CelErrorCode.InvalidArgument,
                    name is null ? "undefined JSON element" : "invalid argument to '" + name + "': undefined JSON element");
        }
    }

    private static CelValue DuplicateKey(string key) => Error(CelErrorCode.DuplicateMapKey, "duplicate map key: " + Literal(key));

    private static CelValue Error(CelErrorCode code, string message) =>
        CelValue.FromError(new CelError(code, CelError.FirstPart, null, 0, message));

    // A string in double quotes with escapes for the backslash, the quote and the control characters.
    private static string Literal(string value)
    {
        var text = new StringBuilder(value.Length + 2).Append('"');
        foreach (char c in value)
        {
            if (c is '\\' or '"')
            {
                text.Append('\\').Append(c);
            }
            else if (c == '\n')
            {
                text.Append("\\n");
            }
            else if (c == '\r')
            {
                text.Append("\\r");
            }
            else if (c == '\t')
            {
                text.Append("\\t");
            }
            else if (c is < ' ' or '\u007F')
            {
                text.Append("\\u00").Append(((int)c).ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                text.Append(c);
            }
        }

        return text.Append('"').ToString();
    }

    private sealed class EntryEnumerator(JsonElement obj, IEvaluationMeter? meter, bool readValues) : IEnumerator<KeyValuePair<CelValue, CelValue>>
    {
        private JsonElement.ObjectEnumerator _inner;
        private bool _started;
        private bool _failed;

        public KeyValuePair<CelValue, CelValue> Current { get; private set; }

        object System.Collections.IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_failed)
            {
                return false;
            }

            try
            {
                if (!_started)
                {
                    _inner = obj.EnumerateObject();
                    _started = true;
                }

                if (!_inner.MoveNext())
                {
                    return false;
                }

                JsonProperty property = _inner.Current;
                Current = new(CelValue.FromString(property.Name), readValues ? Read(property.Value, meter) : CelValue.Null);
                return true;
            }
            catch (ObjectDisposedException exception)
            {
                _failed = true;
                Current = FailedEntry(exception);
                return true;
            }
        }

        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
