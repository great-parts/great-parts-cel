using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Analyzers;

/// <summary>Strongly connected components of a directed graph (Tarjan's algorithm, without recursion).</summary>
internal static class StronglyConnectedComponents
{
    /// <summary>Returns the number of the component of every node; two nodes lie on a common cycle when the numbers are equal.</summary>
    /// <param name="successors">The successors of each node; a node that is only a target need not be a key.</param>
    internal static Dictionary<string, int> Find(Dictionary<string, List<string>> successors)
    {
        Dictionary<string, int> index = new(StringComparer.Ordinal);
        Dictionary<string, int> lowLink = new(StringComparer.Ordinal);
        Dictionary<string, int> component = new(StringComparer.Ordinal);
        HashSet<string> onStack = new(StringComparer.Ordinal);
        Stack<string> stack = new();
        Stack<(string Node, int Next)> calls = new();

        List<string> nodes = [];
        foreach (KeyValuePair<string, List<string>> pair in successors)
        {
            nodes.Add(pair.Key);
            nodes.AddRange(pair.Value);
        }

        foreach (string root in nodes)
        {
            if (index.ContainsKey(root))
            {
                continue;
            }

            Visit(root);
            while (calls.Count > 0)
            {
                (string node, int next) = calls.Pop();
                List<string> targets = successors.TryGetValue(node, out List<string>? list) ? list : [];
                if (next > 0)
                {
                    // Returned from the successor before 'next'.
                    lowLink[node] = Math.Min(lowLink[node], lowLink[targets[next - 1]]);
                }

                bool descended = false;
                for (; next < targets.Count; next++)
                {
                    string target = targets[next];
                    if (!index.ContainsKey(target))
                    {
                        calls.Push((node, next + 1));
                        Visit(target);
                        descended = true;
                        break;
                    }

                    if (onStack.Contains(target))
                    {
                        lowLink[node] = Math.Min(lowLink[node], index[target]);
                    }
                }

                if (descended || lowLink[node] != index[node])
                {
                    continue;
                }

                int number = component.Count;
                string member;
                do
                {
                    member = stack.Pop();
                    _ = onStack.Remove(member);
                    component[member] = number;
                }
                while (!string.Equals(member, node, StringComparison.Ordinal));
            }
        }

        return component;

        void Visit(string node)
        {
            index[node] = index.Count;
            lowLink[node] = index[node];
            stack.Push(node);
            _ = onStack.Add(node);
            calls.Push((node, 0));
        }
    }
}
