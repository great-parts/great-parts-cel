using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Filtering;

// The path of a field: the keys of the steps through maps, the guards that a path needs, the expression of the path and the test
// that a field is set.
internal sealed partial class FilterTranslator
{
    // A tree of a CEL expression is at most 250 levels deep; a field of more parts does not compile.
    private const int MaxPathParts = 250;

    // The field of a member written in the filter, or null after an error: too many parts, or a key of a map that does not fit.
    // The parts are counted before the names are resolved, so that the work on a member stays linear in its length.
    private FieldRef? FieldOf(FilterMember member) =>
        IsTooLong(member.Segments.Count, member) ? null : Prepare(member, _catalog.Resolve(Names(member)), new PathPlace(member, isWritten: true));

    // Tells whether a path of this many parts is longer than a tree can be deep, and reports NestingTooDeep at the member when it is.
    private bool IsTooLong(int parts, FilterMember member)
    {
        if (parts <= MaxPathParts)
        {
            return false;
        }

        Report(CelErrorCode.NestingTooDeep, "The field has more parts than the limit of 250 for the depth of a CEL expression.", member.Start, member.End);
        return true;
    }

    // The field with the key of each step through a map read by the type of the keys; null after an error: a path longer than a tree
    // can be deep, the empty name of a field, a key that does not fit.
    private FieldRef? Prepare(FilterMember member, ResolvedPath path, PathPlace place)
    {
        CelValue?[] keys = new CelValue?[path.Names.Count];
        if (IsTooLong(keys.Length, member))
        {
            return null;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            bool isKey = i >= Math.Max(path.VariableLength, 1) && path.TypeAfter(i - 1) is { Kind: CelTypeKind.Map };
            if (isKey ? !ReadKey(member, path, place, i, out keys[i]) : IsEmptyName(member, path, place, i))
            {
                return null;
            }
        }

        return new FieldRef(member, path, place, keys);
    }

    // Reads the key of the step through a map at a part by the type of the keys; false after the error InvalidFilterValue.
    private bool ReadKey(FilterMember member, ResolvedPath path, PathPlace place, int index, out CelValue? key)
    {
        CelType keyType = path.TypeAfter(index - 1)!.Parameters[0];
        FilterValue read = FilterValues.Read(keyType, path.Names[index], !place.IsQuoted(index), _catalog, null);
        key = read.Value;
        if (key is not null)
        {
            return true;
        }

        Report(
            CelErrorCode.InvalidFilterValue,
            "The key " + path.Names[index] + " does not fit the keys of the map: " + (read.Problem ?? "a key is not converted") + ".",
            place.Part(index),
            Source(member),
            keyType);
        return false;
    }

    // An empty name - a string in quotes with no text - names no field; true after the error InvalidFilterRestriction.
    private bool IsEmptyName(FilterMember member, ResolvedPath path, PathPlace place, int index)
    {
        if (path.Names[index].Length > 0)
        {
            return false;
        }

        Report(CelErrorCode.InvalidFilterRestriction, "The name of a field is empty.", place.Part(index), Source(member));
        return true;
    }

    // The guards of a path up to a part: the conditions that the composite fields on the path are set, which an item without them
    // does not match. The last part is guarded only with includeLast, and then as a part that the path goes on from when lastContinues.
    private void Guards(FieldRef field, int upTo, bool includeLast, bool lastContinues, List<CelExprNode> guards)
    {
        ResolvedPath path = field.Path;
        if (path.VariableLength == 0)
        {
            return;
        }

        int stop = includeLast ? upTo : upTo - 1;
        for (int i = path.VariableLength - 1; i <= stop; i++)
        {
            Guard(field, i, i < upTo || lastContinues, guards);
        }
    }

    // The guard of one part: a variable of an object, wrapper or dynamic type is tested against null, a field of such a type with
    // has(), a step through a map with the test that the key is in the map.
    private void Guard(FieldRef field, int index, bool pathContinues, List<CelExprNode> guards)
    {
        ResolvedPath path = field.Path;
        CelType? type = path.TypeAfter(index);
        bool needsTest = type is not null && NeedsGuard(type, pathContinues);
        if (index == path.VariableLength - 1)
        {
            if (needsTest && !path.IsIteration)
            {
                guards.Add(NotNull(field, index + 1));
            }
        }
        else if (field.KeyAt(index) is not null)
        {
            guards.Add(InMap(field, index));
            if (needsTest)
            {
                guards.Add(NotNull(field, index + 1));
            }
        }
        else if (needsTest)
        {
            guards.Add(HasField(field, index));
        }
    }

    // An object, a wrapper and a dynamic value can be missing; a wrapper at the end of the path is compared as its primitive and
    // needs the test too, an object or a dynamic value at the end is not compared.
    private static bool NeedsGuard(CelType type, bool pathContinues) =>
        type.Kind switch
        {
            CelTypeKind.Wrapper => true,
            CelTypeKind.Object or CelTypeKind.Dyn or CelTypeKind.Any => pathContinues,
            _ => false,
        };

    // The expression of the first parts of a path: the variable, the selections of the fields and the lookups of the keys of the
    // maps. The places are those of the parts, or the place of the whole path for a guard.
    private CelExprNode PathExpression(FieldRef field, int count, bool wholePath)
    {
        IReadOnlyList<string> names = field.Path.Names;
        PathPlace place = field.Place;
        _factory.Position = wholePath ? place.Whole : place.Part(0);
        CelExprNode node = _factory.Ident(names[0]);
        for (int i = 1; i < count; i++)
        {
            _factory.Position = wholePath ? place.Whole : place.Part(i);
            if (field.KeyAt(i) is { } key)
            {
                CelExprNode constant = _factory.Constant(key);
                _factory.Position = place.Whole;
                node = _factory.Call("_[_]", node, constant);
            }
            else
            {
                node = _factory.Select(node, names[i]);
            }
        }

        return node;
    }

    // The part, as a test: the first parts of the path are not null.
    private CelCallNode NotNull(FieldRef field, int count)
    {
        CelExprNode value = PathExpression(field, count, wholePath: true);
        _factory.Position = field.Place.Whole;
        CelExprNode none = _factory.Constant(CelValue.Null);
        return _factory.Call("_!=_", value, none);
    }

    // has() of a part of the path, a field of an object.
    private CelExprNode HasField(FieldRef field, int index)
    {
        CelExprNode owner = PathExpression(field, index, wholePath: true);
        _factory.Position = field.Place.Whole;
        return _factory.ExpandMacro("has", null, _factory.Select(owner, field.Path.Names[index]));
    }

    // The test that the key of a step through a map is in the map.
    private CelCallNode InMap(FieldRef field, int index)
    {
        CelExprNode map = PathExpression(field, index, wholePath: true);
        _factory.Position = field.Place.Whole;
        CelExprNode key = _factory.Constant(field.KeyAt(index)!.Value);
        return _factory.Call("@in", key, map);
    }

    // size(path) > 0: the collection at the end of the path has an element.
    private CelCallNode NotEmpty(FieldRef field)
    {
        CelExprNode collection = PathExpression(field, field.Path.Names.Count, wholePath: true);
        _factory.Position = field.Place.Whole;
        CelExprNode size = _factory.Call("size", collection);
        CelExprNode zero = _factory.Constant(CelValue.FromInt64(0));
        return _factory.Call("_>_", size, zero);
    }

    // The test that the field is set - or, with negated, that it is not - added to the terms: by the end of the path, a key of a map is
    // in the map, a field of an object has(), a variable is not null, a collection is not empty, and a variable of any other type is
    // always set. A collection is also non-empty when withSize.
    private void AddPresence(FieldRef field, bool negated, bool withSize, List<CelExprNode> terms)
    {
        ResolvedPath path = field.Path;
        int last = path.Names.Count - 1;
        if (last <= Math.Max(path.VariableLength - 1, 0))
        {
            terms.Add(VariablePresence(field, negated));
            return;
        }

        if (field.KeyAt(last) is not null)
        {
            terms.Add(Negate(InMap(field, last), negated));
            return;
        }

        terms.Add(Negate(HasField(field, last), negated));
        if (withSize && !negated && path.FinalType is { Kind: CelTypeKind.List or CelTypeKind.Map })
        {
            terms.Add(NotEmpty(field));
        }
    }

    // The test that a variable is set.
    private CelExprNode VariablePresence(FieldRef field, bool negated)
    {
        CelType? type = field.Path.FinalType;
        Range whole = field.Place.Whole;
        if (type is { Kind: CelTypeKind.List or CelTypeKind.Map })
        {
            CelExprNode filled = NotEmpty(field);
            return negated ? Negate(filled, negated: true) : filled;
        }

        if (type is null or { Kind: CelTypeKind.Object or CelTypeKind.Wrapper or CelTypeKind.Dyn or CelTypeKind.Any })
        {
            CelExprNode value = PathExpression(field, field.Path.Names.Count, wholePath: true);
            _factory.Position = whole;
            CelExprNode none = _factory.Constant(CelValue.Null);
            return _factory.Call(negated ? "_==_" : "_!=_", value, none);
        }

        _factory.Position = whole;
        return _factory.Constant(CelValue.FromBoolean(!negated));
    }

    // !node at the place of the whole path, or the node.
    private CelExprNode Negate(CelExprNode node, bool negated) => negated ? _factory.Call("!_", node) : node;
}
