using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Evaluation;

// The part of planning that makes the node of a message literal. The planner plans the values of the fields, which are its own nodes, and
// this class makes the node from them, so that the planner has no part in the shape of a message.
internal static class MessagePlanner
{
    // A message literal: the values stay on the stack while the next ones are evaluated. With type checking the description and
    // the field numbers are those of the reference map; without it the name of the type, with its candidates in the container,
    // and the field names resolve at evaluation. An entry with ?, ?f: v, is stored only when its optional has a value.
    internal static PlanNode Plan(Planner planner, CelStructNode message, int offset, CheckedExpression? checkedExpression, out int stackSize)
    {
        int count = message.Count;
        ImmutableArray<PlanNode>.Builder values = ImmutableArray.CreateBuilder<PlanNode>(count);
        stackSize = count;
        for (int i = 0; i < count; i++)
        {
            values.Add(planner.Visit(message.ValueOf(i), out int valueStackSize));
            stackSize = Math.Max(stackSize, i + valueStackSize);
        }

        ImmutableArray<bool> optional = message.HasOptionalEntry() ? message.OptionalFlags() : default;

        if (checkedExpression is not null)
        {
            ImmutableArray<int>.Builder numbers = ImmutableArray.CreateBuilder<int>(count);
            for (int i = 0; i < count; i++)
            {
                numbers.Add(checkedExpression.References[message.FieldId(i)].FieldNumber);
            }

            return new MessagePlanNode(offset, checkedExpression.References[message.Id].ObjectType!, numbers.MoveToImmutable(), values.MoveToImmutable(), optional);
        }

        bool leadingDot = message.TypeName[0] == '.';
        string name = leadingDot ? message.TypeName[1..] : message.TypeName;
        return new MessagePlanNode(
            offset,
            message.TypeName,
            [.. planner.Qualified(name, leadingDot)],
            message.FieldNames(),
            values.MoveToImmutable(),
            optional);
    }
}
