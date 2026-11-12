using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Generator;

// The object types of a context by CEL name: the converter of an element that is an object names the description of its type.
internal sealed class ObjectIndex
{
    private readonly Dictionary<string, ObjectTypeModel> byName = new(StringComparer.Ordinal);

    internal ObjectIndex(EquatableArray<ObjectTypeModel> types)
    {
        foreach (ObjectTypeModel type in types.Items)
        {
            byName[type.CelName] = type;
        }
    }

    internal ObjectTypeModel? Find(string celName) => byName.TryGetValue(celName, out ObjectTypeModel? type) ? type : null;
}
