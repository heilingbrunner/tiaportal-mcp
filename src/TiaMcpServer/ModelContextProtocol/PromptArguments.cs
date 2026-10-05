using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Lets a slash command carry values with spaces, written in double quotes:
    /// /mcp__tia-mcp-server__ExportXmlBlocks "PC-System_1/Software PLC_1" "D:\My Export"
    ///
    /// Callers: Program.cs (Filter as the get-prompt filter) and Test7ToolRegistration (Requote).
    /// Affected API: only how the arguments of 'prompts/get' reach the McpPrompts methods; no prompt
    /// changes. Reads/writes no data files.
    ///
    /// Claude Code splits the text after a slash command at every whitespace, without any quote
    /// handling, matches the pieces to the prompt's argument names by position and drops pieces
    /// beyond the last argument. A quoted value therefore arrives cut in pieces with the quote
    /// marks still attached. The server sees the pieces in order, so this filter joins them again,
    /// reads the line with double quotes honoured and assigns the values by position. The values
    /// themselves are never changed.
    ///
    /// Nothing happens unless an argument starts or ends with a double quote, so values without
    /// quotes (and clients that pass real named arguments) are never touched. Limit: a value of n
    /// words needs a prompt with at least n arguments, because Claude Code drops the rest before
    /// the server sees it; an unbalanced quote is left as typed so the failure stays visible.
    /// </summary>
    public static class PromptArguments
    {
        private static readonly Lazy<Dictionary<string, string[]>> _declared = new(() =>
        {
            var result = new Dictionary<string, string[]>(StringComparer.Ordinal);

            foreach (var method in typeof(McpPrompts).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                var attribute = method.GetCustomAttribute<McpServerPromptAttribute>();
                if (attribute != null)
                {
                    result[attribute.Name ?? method.Name] = method.GetParameters().Select(p => p.Name!).ToArray();
                }
            }

            return result;
        });

        public static McpRequestFilter<GetPromptRequestParams, GetPromptResult> Filter =>
            next => (context, cancellationToken) =>
            {
                var parameters = context.Params;

                if (parameters?.Arguments is { Count: > 0 } arguments
                    && _declared.Value.TryGetValue(parameters.Name, out var declaredNames))
                {
                    var typed = arguments.ToDictionary(
                        a => a.Key,
                        a => a.Value.ValueKind == JsonValueKind.String ? a.Value.GetString() ?? string.Empty : a.Value.GetRawText());

                    var requoted = Requote(declaredNames, typed);

                    if (requoted != null)
                    {
                        arguments.Clear();

                        foreach (var pair in requoted)
                        {
                            arguments[pair.Key] = JsonSerializer.SerializeToElement(pair.Value);
                        }
                    }
                }

                return next(context, cancellationToken);
            };

        /// <summary>
        /// Joins the pieces of double-quoted values and assigns the result by position. Returns
        /// null when nothing has to change: no quote at the edge of an argument, arguments that
        /// are not the first ones in declared order (a client that names them itself), or an
        /// unbalanced quote.
        /// </summary>
        public static Dictionary<string, string>? Requote(
            IReadOnlyList<string> declaredNames,
            IReadOnlyDictionary<string, string> arguments)
        {
            // Claude Code supplies the first k arguments, in order
            var pieces = new List<string>();

            foreach (var name in declaredNames)
            {
                if (!arguments.TryGetValue(name, out var value))
                {
                    break;
                }

                pieces.Add(value);
            }

            if (pieces.Count == 0 || pieces.Count != arguments.Count)
            {
                return null;
            }

            if (!pieces.Any(p => p.StartsWith("\"", StringComparison.Ordinal) || p.EndsWith("\"", StringComparison.Ordinal)))
            {
                return null;
            }

            var values = SplitQuoted(string.Join(" ", pieces));
            if (values == null)
            {
                return null;
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 0; i < values.Count && i < declaredNames.Count; i++)
            {
                result[declaredNames[i]] = values[i];
            }

            return result;
        }

        /// <summary>
        /// Splits at whitespace outside double quotes and removes the quote marks; "" is an
        /// empty value. Returns null when a quote is left open.
        /// </summary>
        public static List<string>? SplitQuoted(string line)
        {
            var values = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            var hasValue = false;

            foreach (var c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    hasValue = true;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (hasValue)
                    {
                        values.Add(current.ToString());
                        current.Clear();
                        hasValue = false;
                    }
                }
                else
                {
                    current.Append(c);
                    hasValue = true;
                }
            }

            if (inQuotes)
            {
                return null;
            }

            if (hasValue)
            {
                values.Add(current.ToString());
            }

            return values;
        }
    }
}
