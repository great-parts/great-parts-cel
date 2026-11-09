using System.Collections.Generic;
using System.Text.Json;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A <see cref="CelMap"/> over a JSON object: <c>map(string, dyn)</c> in document order. Its decisions — the counting
/// lookup and the repeated names — are in <see cref="JsonRules"/>.
/// </summary>
/// <param name="obj">The object element.</param>
internal sealed class JsonObjectMap(JsonElement obj) : CelMap
{
    private protected override int CountCore => obj.GetPropertyCount();

    internal override bool TryGetKeyError(IEvaluationMeter? meter, out CelValue error, bool leadingUnit = false) =>
        JsonRules.TryGetRepeat(obj, meter, leadingUnit, out error);

    internal override ulong ProbeUnits(ulong keyTenths) => JsonRules.ProbeUnits(obj, keyTenths);

    internal override bool TryRead(CelValue key, IEvaluationMeter? meter, bool readValue, out CelValue value)
    {
        if (key.TryGetString(out string? name))
        {
            return JsonRules.TryFind(obj, name, meter, readValue, out value);
        }

        value = default;
        return false;
    }

    internal override IEnumerator<KeyValuePair<CelValue, CelValue>> EnumerateEntries(IEvaluationMeter? meter, bool readValues) =>
        JsonRules.Entries(obj, meter, readValues);

    private protected override bool TryGetValueCore(CelValue key, out CelValue value) => TryRead(key, null, readValue: true, out value);

    private protected override IEnumerator<KeyValuePair<CelValue, CelValue>> GetEnumeratorCore() => JsonRules.Entries(obj, null, readValues: true);
}
