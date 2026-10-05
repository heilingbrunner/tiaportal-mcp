using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Returns 'tools/list' in ascending order of the name a client displays.
    ///
    /// Callers: Program.cs (Filter as the list-tools filter) and Test7ToolRegistration (Sort).
    /// Affected API: only the order of the 'tools/list' response; no tool changes. Reads/writes no
    /// data files.
    ///
    /// The SDK keeps registered tools in a ConcurrentDictionary keyed by name and lists them in
    /// its hash order, so sorting them in Program.BuildTools would not survive registration -
    /// the response itself has to be sorted.
    /// </summary>
    public static class ToolOrdering
    {
        /// <summary>
        /// Sorts by Title (what Claude Code shows), falling back to Name for tools without one,
        /// ignoring case; Name breaks ties so the order is stable.
        /// </summary>
        public static List<Tool> Sort(IEnumerable<Tool> tools)
        {
            return tools
                .OrderBy(DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(t => t.Name, StringComparer.Ordinal)
                .ToList();
        }

        public static McpRequestFilter<ListToolsRequestParams, ListToolsResult> Filter =>
            next => async (context, cancellationToken) =>
            {
                var result = await next(context, cancellationToken);

                if (result.Tools != null)
                {
                    result.Tools = Sort(result.Tools);
                }

                return result;
            };

        public static string DisplayName(Tool tool)
        {
            return string.IsNullOrWhiteSpace(tool.Title) ? tool.Name : tool.Title!;
        }
    }
}
