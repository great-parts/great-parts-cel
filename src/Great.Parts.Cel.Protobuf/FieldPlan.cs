using System;
using System.Collections;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// How presence of a field is decided.
/// </summary>
internal enum FieldPresence
{
    /// <summary>The field accessor knows: the field has explicit presence.</summary>
    Accessor,

    /// <summary>The typed extension object knows.</summary>
    Extension,

    /// <summary>A repeated or a map field is present when it is not empty.</summary>
    NonEmpty,

    /// <summary>A proto3 field without explicit presence is present when its value is not the default of its type.</summary>
    NotDefault,
}

/// <summary>
/// Everything that reading, writing and testing one field needs, decided once when the description is made: the accessor, how the
/// field holds its values, the conversion of a value in each direction and the rule of presence.
/// </summary>
internal sealed class FieldPlan
{
    private static readonly CelList EmptyList = CelList.Create();

    private static readonly CelMap EmptyMap = CelMap.Create([]);

    private readonly IFieldAccessor? _accessor;
    private readonly ExtensionField? _extension;
    private readonly MessageType _owner;
    private readonly int _number;
    private readonly FieldContainer _container;
    private readonly FieldKind _keyKind;
    private readonly ValueShape _valueShape;
    private readonly FieldPresence _presence;
    private readonly FieldReader _reader;
    private readonly FieldWriter _writer;
    private MessageType? _nested;

    private FieldPlan(
        MessageType owner,
        int number,
        IFieldAccessor? accessor,
        ExtensionField? extension,
        FieldShape shape,
        FieldPresence presence,
        FieldReader reader)
    {
        _owner = owner;
        _number = number;
        _accessor = accessor;
        _extension = extension;
        _container = shape.Container;
        _keyKind = shape.Key;
        _valueShape = shape.Value;
        _presence = presence;
        _reader = reader;
        _writer = new FieldWriter(shape.Value, owner, number);
    }

    /// <summary>
    /// Whether the field is single, repeated or a map.
    /// </summary>
    internal FieldContainer Container => _container;

    /// <summary>
    /// The shape of a single value, of an element of a repeated field, or of a value of a map field.
    /// </summary>
    internal ValueShape Value => _valueShape;

    /// <summary>
    /// For a field whose values are messages of the provider, the description of that message; otherwise <see langword="null"/>.
    /// </summary>
    internal MessageType? Nested => _nested;

    /// <summary>
    /// Makes the plan of one field of a description.
    /// </summary>
    /// <param name="owner">The description that has the field.</param>
    /// <param name="number">The number of the field in the description.</param>
    /// <param name="field">The descriptor of the field, or of the extension.</param>
    /// <param name="extension">The access to the extension when the field is one; otherwise <see langword="null"/>.</param>
    /// <param name="shape">The shape of the field.</param>
    /// <param name="empty">For a field whose values are messages, the empty message that an unset field reads as.</param>
    /// <returns>The plan.</returns>
    internal static FieldPlan Create(MessageType owner, int number, FieldDescriptor field, ExtensionField? extension, FieldShape shape, IMessage? empty)
    {
        var reader = new FieldReader(shape.Value, owner, number, empty);
        return new FieldPlan(owner, number, extension is null ? field.Accessor : null, extension, shape, PresenceOf(field, extension, shape), reader);
    }

    /// <summary>
    /// Gives the plan the description of the message of its values, once every description of the provider exists and before any
    /// is shared.
    /// </summary>
    /// <param name="registry">The registry of the provider.</param>
    internal void Link(ProviderTypes registry)
    {
        if (_valueShape.Message is MessageDescriptor message)
        {
            _ = registry.TryGetType(message, out _nested);
        }
    }

    /// <summary>
    /// Reads the field of a message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The value of the field.</returns>
    internal CelValue Read(IMessage message)
    {
        object? raw = GetRaw(message);
        switch (_container)
        {
            case FieldContainer.Repeated:
                return CelValue.FromList(raw is IList { Count: > 0 } list ? CelList.CreateView(new ObjectListView(list), _reader.Converter) : EmptyList);
            case FieldContainer.Map:
                return CelValue.FromMap(raw is IDictionary { Count: > 0 } dictionary ? MapView(dictionary) : EmptyMap);
            default:
                return _reader.Convert(raw);
        }
    }

    /// <summary>
    /// Reads the field of a message as the object that the accessor gives, for the comparison of two messages.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The boxed value, the list of a repeated field, the dictionary of a map field.</returns>
    internal object? ReadRaw(IMessage message) => GetRaw(message);

    /// <summary>
    /// Tests whether the field of a message is present, by the rule of the field.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> when the field is present.</returns>
    internal bool Has(IMessage message)
    {
        switch (_presence)
        {
            case FieldPresence.Accessor:
                return _accessor!.HasValue(message);
            case FieldPresence.Extension:
                return _extension!.Has(message);
            case FieldPresence.NonEmpty:
                return GetRaw(message) switch
                {
                    IList list => list.Count > 0,
                    IDictionary dictionary => dictionary.Count > 0,
                    _ => false,
                };
            default:
                return IsNotDefault(_accessor!.GetValue(message));
        }
    }

    /// <summary>
    /// Tells how many cost units reading the field costs beyond the unit of the selection: the units of unpacking each
    /// <c>Any</c> that the field holds, known from the message before the field is read; 0 for any other field.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The units.</returns>
    internal long ReadCost(IMessage message)
    {
        if (_valueShape.Kind != FieldKind.Any)
        {
            return 0;
        }

        object? raw = GetRaw(message);
        switch (_container)
        {
            case FieldContainer.Repeated:
                return raw is IList list ? UnitsOfList(list) : 0;
            case FieldContainer.Map:
                return raw is IDictionary dictionary ? UnitsOfValues(dictionary) : 0;
            default:
                return raw is Any any ? UnpackUnits(any) : 0;
        }
    }

    /// <summary>
    /// Sets the field of a message from the value of an object literal. A value that does not fit is an error value.
    /// </summary>
    /// <param name="message">The message that is being created.</param>
    /// <param name="value">The value.</param>
    /// <param name="error">The error value when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the field is set.</returns>
    internal bool TryWrite(IMessage message, CelValue value, out CelValue error)
    {
        switch (_container)
        {
            case FieldContainer.Repeated:
                return TryWriteList(message, value, out error);
            case FieldContainer.Map:
                return TryWriteMap(message, value, out error);
            default:
                if (!_writer.TryConvert(value, false, out object? converted, out error))
                {
                    return false;
                }

                if (converted is not null)
                {
                    SetSingle(message, converted);
                }

                return true;
        }
    }

    // How presence of a field is decided, in the order of the rules: an extension, a container, explicit presence, the default.
    private static FieldPresence PresenceOf(FieldDescriptor field, ExtensionField? extension, FieldShape shape)
    {
        if (shape.Container != FieldContainer.Single)
        {
            return FieldPresence.NonEmpty;
        }

        if (extension is not null)
        {
            return FieldPresence.Extension;
        }

        return field.HasPresence ? FieldPresence.Accessor : FieldPresence.NotDefault;
    }

    // The units of unpacking one Any: 1 and one for every ten of the length of the address in UTF-16 units and the bytes of the content.
    private static long UnpackUnits(Any any) => 1 + (((long)any.TypeUrl.Length) + any.Value.Length + 9) / 10;

    private static long UnitsOfList(IList list)
    {
        long units = 0;
        for (int index = 0; index < list.Count; index++)
        {
            if (list[index] is Any any)
            {
                units += UnpackUnits(any);
            }
        }

        return units;
    }

    private static long UnitsOfValues(IDictionary dictionary)
    {
        long units = 0;
        IDictionaryEnumerator entries = dictionary.GetEnumerator();
        while (entries.MoveNext())
        {
            if (entries.Value is Any any)
            {
                units += UnpackUnits(any);
            }
        }

        return units;
    }

    private object? GetRaw(IMessage message) => _extension is null ? _accessor!.GetValue(message) : _extension.Get(message);

    private void SetSingle(IMessage message, object value)
    {
        if (_extension is null)
        {
            _accessor!.SetValue(message, value);
        }
        else
        {
            _extension.Set(message, value);
        }
    }

    // A list is written element by element; an element that is null of a kind that is left out is not added.
    private bool TryWriteList(IMessage message, CelValue value, out CelValue error)
    {
        error = default;
        if (!value.TryGetList(out CelList? list))
        {
            error = FieldErrors.Mismatch(_owner, _number, value);
            return false;
        }

        IList target = _extension is null ? (IList)_accessor!.GetValue(message) : _extension.GetList(message);
        for (int index = 0; index < list.Count; index++)
        {
            if (!_writer.TryConvert(list[index], true, out object? converted, out error))
            {
                return false;
            }

            if (converted is not null)
            {
                target.Add(converted);
            }
        }

        return true;
    }

    // A map is written pair by pair, in the order of the map.
    private bool TryWriteMap(IMessage message, CelValue value, out CelValue error)
    {
        error = default;
        if (!value.TryGetMap(out CelMap? map))
        {
            error = FieldErrors.Mismatch(_owner, _number, value);
            return false;
        }

        var target = (IDictionary)_accessor!.GetValue(message);
        foreach (KeyValuePair<CelValue, CelValue> entry in map)
        {
            if (!_writer.TryConvertKey(_keyKind, entry.Key, out object? key, out error)
                || !_writer.TryConvert(entry.Value, true, out object? converted, out error))
            {
                return false;
            }

            if (converted is not null)
            {
                target[key!] = converted;
            }
        }

        return true;
    }

    // The view of a map field: one instantiation for each of the six key types that protobuf allows, written out so that no generic
    // type is made at run time.
    private CelMap MapView(IDictionary dictionary)
    {
        return _keyKind switch
        {
            FieldKind.Int32 => CelMap.CreateView(new ObjectMapView<int>(dictionary), _reader.Converter, keepSourceOrder: true),
            FieldKind.Int64 => CelMap.CreateView(new ObjectMapView<long>(dictionary), _reader.Converter, keepSourceOrder: true),
            FieldKind.UInt32 => CelMap.CreateView(new ObjectMapView<uint>(dictionary), _reader.Converter, keepSourceOrder: true),
            FieldKind.UInt64 => CelMap.CreateView(new ObjectMapView<ulong>(dictionary), _reader.Converter, keepSourceOrder: true),
            FieldKind.Bool => CelMap.CreateView(new ObjectMapView<bool>(dictionary), _reader.Converter, keepSourceOrder: true),
            _ => CelMap.CreateView(new ObjectMapView<string>(dictionary), _reader.Converter, keepSourceOrder: true),
        };
    }

    // A proto3 scalar that has no explicit presence is present when its value is not the default of its type; a floating point number
    // is the default only when its bit pattern is that of +0.0, so -0.0 and a NaN are present.
    private bool IsNotDefault(object? value)
    {
        return _valueShape.Kind switch
        {
            FieldKind.Int32 or FieldKind.Enum => (int)value! != 0,
            FieldKind.Int64 => (long)value! != 0,
            FieldKind.UInt32 => (uint)value! != 0,
            FieldKind.UInt64 => (ulong)value! != 0,
            FieldKind.Float => BitConverter.SingleToInt32Bits((float)value!) != 0,
            FieldKind.Double => BitConverter.DoubleToInt64Bits((double)value!) != 0,
            FieldKind.Bool => (bool)value!,
            FieldKind.String => value is string { Length: > 0 },
            _ => value is ByteString { Length: > 0 },
        };
    }
}
