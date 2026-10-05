using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Marks a tool that must run without a TIA Portal instance being resolved first, such as
    /// 'GetPortals', which has to work while several instances are attached.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class PortalIndependentAttribute : System.Attribute
    {
    }

    /// <summary>
    /// Lets every request name the TIA Portal instance it targets, because the stateless MCP
    /// protocol has no session that could remember it.
    ///
    /// Callers: Program.cs (Apply for each registered tool, Filter as the call-tool filter).
    /// Affected API: every tool except [PortalIndependent] ones and tools that declare 'portalId'
    /// themselves (Connect) gains an optional integer 'portalId' in its input schema; existing
    /// arguments are unchanged. Reads/writes no data files.
    ///
    /// One filter rather than a parameter on ~100 tool methods: the filter takes 'portalId' out
    /// of the arguments, resolves and locks that instance, and runs the tool inside the scope.
    /// Tools invoked directly (the MSTest suite) bypass it and use the single attached instance.
    /// </summary>
    public static class PortalSelection
    {
        public const string ArgumentName = "portalId";

        private const string ArgumentDescription =
            "portalId: process id of the TIA Portal instance to use (from 'Connect' or 'GetPortals'). " +
            "Only needed when more than one instance is attached";

        private static readonly HashSet<string> _scopedTools = new(StringComparer.Ordinal);

        public static McpServerTool Apply(MethodInfo method, McpServerTool tool)
        {
            if (method.GetCustomAttribute<PortalIndependentAttribute>() != null ||
                method.GetParameters().Any(p => p.Name == ArgumentName))
            {
                return tool;
            }

            var schema = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText()) as JsonObject
                ?? new JsonObject { ["type"] = "object" };

            if (schema["properties"] is not JsonObject properties)
            {
                properties = new JsonObject();
                schema["properties"] = properties;
            }

            properties[ArgumentName] = new JsonObject
            {
                ["type"] = "integer",
                ["description"] = ArgumentDescription
            };

            using (var document = JsonDocument.Parse(schema.ToJsonString()))
            {
                tool.ProtocolTool.InputSchema = document.RootElement.Clone();
            }

            lock (_scopedTools)
            {
                _scopedTools.Add(tool.ProtocolTool.Name);
            }

            return tool;
        }

        public static McpRequestFilter<CallToolRequestParams, CallToolResult> Filter =>
            next => async (context, cancellationToken) =>
            {
                var name = context.Params?.Name;

                bool isScoped;
                lock (_scopedTools)
                {
                    isScoped = name != null && _scopedTools.Contains(name);
                }

                if (!isScoped)
                {
                    return await next(context, cancellationToken);
                }

                int? portalId;

                try
                {
                    portalId = TakePortalId(context.Params!.Arguments);
                }
                catch (FormatException ex)
                {
                    return Error(ex.Message);
                }

                try
                {
                    return await McpServer.Portal.RunScopedAsync(portalId, () => next(context, cancellationToken), cancellationToken);
                }
                catch (PortalException pex)
                {
                    return Error(Describe(pex));
                }
            };

        /// <summary>
        /// The exception message plus the instances to choose from, so the client can retry
        /// with a 'portalId' without calling 'GetPortals' first.
        /// </summary>
        public static string Describe(PortalException pex)
        {
            var candidates = pex.Candidates?.ToList();

            return candidates is { Count: > 0 }
                ? $"{pex.Message} Candidates: {string.Join("; ", candidates)}"
                : pex.Message;
        }

        private static int? TakePortalId(IDictionary<string, JsonElement>? arguments)
        {
            if (arguments == null || !arguments.TryGetValue(ArgumentName, out var value))
            {
                return null;
            }

            // the tool method itself does not declare it
            arguments.Remove(ArgumentName);

            switch (value.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;

                case JsonValueKind.Number when value.TryGetInt32(out var number):
                    return number;

                case JsonValueKind.String when int.TryParse(value.GetString(), out var parsed):
                    return parsed;

                default:
                    throw new FormatException($"'{ArgumentName}' must be an integer process id, got {value.GetRawText()}.");
            }
        }

        private static CallToolResult Error(string message)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = message }]
            };
        }
    }
}
