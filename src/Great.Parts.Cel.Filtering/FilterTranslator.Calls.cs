using System.Text;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Filtering;

// The translation of a function of the environment: a call is one global call with the name that was written, dots and all.
internal sealed partial class FilterTranslator
{
    // name(arguments): the name of the function is the dotted name as one global name.
    private CelCallNode FunctionCall(FilterCall call)
    {
        CelExprNode[] arguments = new CelExprNode[call.Arguments.Count];
        for (int i = 0; i < arguments.Length; i++)
        {
            arguments[i] = Argument(call.Arguments[i]);
        }

        _factory.Position = call.Name[0].Start..call.Name[^1].End;
        return _factory.Call(FunctionName(call), arguments);
    }

    private static string FunctionName(FilterCall call)
    {
        if (call.Name.Count == 1)
        {
            return call.Name[0].Text;
        }

        StringBuilder name = new(call.Name[0].Text);
        for (int i = 1; i < call.Name.Count; i++)
        {
            name.Append('.').Append(call.Name[i].Text);
        }

        return name.ToString();
    }

    // An argument: a call, a composite - a boolean expression -, a field of the environment, or a value by the form of its text.
    private CelExprNode Argument(FilterExpr argument)
    {
        switch (argument)
        {
            case FilterCall call:
                return FunctionCall(call);
            case FilterGroup group:
                return Node(group.Inner);
            default:
                return ArgumentValue((FilterMember)argument);
        }
    }

    // A member that starts with a variable of the environment is a field, without guards: the function takes the value as it is.
    // Any other text is a value: true or false, a number, or a string; a string in quotes is a string.
    private CelExprNode ArgumentValue(FilterMember member)
    {
        if (member.Segments.Count > 1 || member.Segments[0].Quoted is null)
        {
            if (IsTooLong(member.Segments.Count, member))
            {
                return Placeholder();
            }

            ResolvedPath path = _catalog.Resolve(Names(member));
            if (path.VariableLength > 0)
            {
                FieldRef? field = Prepare(member, path, new PathPlace(member, isWritten: true));
                return field is null ? Placeholder() : PathExpression(field, path.Names.Count, wholePath: false);
            }
        }

        FilterValue value = FilterValues.Read(null, member, _catalog, null);
        _factory.Position = member.Start..member.End;
        return value.Value is { } constant ? _factory.Constant(constant) : Placeholder();
    }
}
