using System.Text.Json;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A <see cref="CelList"/> over a JSON array: an element is read from the document and converted at each access. Its
/// decisions are in <see cref="JsonRules"/>.
/// </summary>
/// <param name="array">The array element.</param>
internal sealed class JsonArrayList(JsonElement array) : CelList
{
    private protected override int CountCore => array.GetArrayLength();

    private protected override CelValue GetCore(int index) => JsonRules.ItemOf(array, index, null);

    internal override CelValue Read(int index, IEvaluationMeter? meter) => JsonRules.ItemOf(array, index, meter);

    internal override ulong IndexUnits => JsonRules.IndexUnits(array);
}
