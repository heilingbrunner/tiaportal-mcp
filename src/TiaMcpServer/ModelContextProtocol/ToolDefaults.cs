using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Lets the server's command line preset the tool arguments 'path' (the project file of
    /// OpenProject), 'softwarePath', 'exportPath', 'preservePath' and 'withDependencies', so a client configures them once (the "args" of its MCP server entry)
    /// instead of the model passing them on every call.
    ///
    /// Callers: Program.cs (Configure at startup, Apply for each registered tool, Filter as the
    /// call-tool filter) and McpServer.GetState (Presets). Affected API: for every tool that
    /// declares a preset argument, the argument is dropped from the schema's 'required' list and
    /// filled from the preset when a call omits it; a value passed in the call always wins.
    /// Without any preset nothing changes. Reads/writes no data files.
    ///
    /// Same shape as PortalSelection: one filter rather than a default on every tool method.
    /// Tools invoked directly (the MSTest suite) bypass it.
    /// </summary>
    public static class ToolDefaults
    {
        public const string Path = "path";
        public const string SoftwarePath = "softwarePath";
        public const string ExportPath = "exportPath";
        public const string PreservePath = "preservePath";
        public const string WithDependencies = "withDependencies";

        private static readonly IReadOnlyDictionary<string, JsonElement> _none =
            new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        private static readonly Dictionary<string, string[]> _presetArguments = new(StringComparer.Ordinal);

        private static readonly ISet<string> _noneForced = new HashSet<string>(StringComparer.Ordinal);

        public static IReadOnlyDictionary<string, JsonElement> Presets { get; private set; } = _none;

        /// <summary>
        /// The preset arguments a call cannot override ('--force-presets'): those of 'path',
        /// 'softwarePath' and 'exportPath' that have a preset. They are hidden from the tool schema.
        /// Empty without the option.
        /// </summary>
        public static ISet<string> Forced { get; private set; } = _noneForced;

        public static void Configure(CliOptions options)
        {
            var presets = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

            if (options.ProjectPath != null)
            {
                presets[Path] = JsonSerializer.SerializeToElement(options.ProjectPath);
            }

            if (options.SoftwarePath != null)
            {
                presets[SoftwarePath] = JsonSerializer.SerializeToElement(options.SoftwarePath);
            }

            if (options.ExportPath != null)
            {
                presets[ExportPath] = JsonSerializer.SerializeToElement(options.ExportPath);
            }

            // The tools default both flags to false, so only 'true' needs a preset.
            if (options.PreservePath)
            {
                presets[PreservePath] = JsonSerializer.SerializeToElement(true);
            }

            if (options.WithDependencies)
            {
                presets[WithDependencies] = JsonSerializer.SerializeToElement(true);
            }

            Presets = presets;

            Forced = options.ForcePresets
                ? new HashSet<string>(new[] { Path, SoftwarePath, ExportPath }.Where(presets.ContainsKey), StringComparer.Ordinal)
                : _noneForced;
        }

        public static McpServerTool Apply(MethodInfo method, McpServerTool tool)
        {
            var names = method.GetParameters()
                .Select(p => p.Name!)
                .Where(Presets.ContainsKey)
                .ToArray();

            if (names.Length == 0)
            {
                return tool;
            }

            var schema = JsonNode.Parse(tool.ProtocolTool.InputSchema.GetRawText()) as JsonObject;

            if (schema == null)
            {
                return tool;
            }

            if (schema["required"] is JsonArray required)
            {
                foreach (var item in required.Where(n => n != null && names.Contains(n.GetValue<string>())).ToList())
                {
                    required.Remove(item);
                }

                if (required.Count == 0)
                {
                    schema.Remove("required");
                }
            }

            if (schema["properties"] is JsonObject properties)
            {
                foreach (var name in names)
                {
                    if (Forced.Contains(name))
                    {
                        properties.Remove(name);
                    }
                    else if (properties[name] is JsonObject property)
                    {
                        var description = property["description"]?.GetValue<string>();
                        property["description"] = (description == null ? string.Empty : description + ". ") +
                            "Optional: the server has a preset that is used when omitted or empty";
                    }
                }
            }

            using (var document = JsonDocument.Parse(schema.ToJsonString()))
            {
                tool.ProtocolTool.InputSchema = document.RootElement.Clone();
            }

            lock (_presetArguments)
            {
                _presetArguments[tool.ProtocolTool.Name] = names;
            }

            return tool;
        }

        public static McpRequestFilter<CallToolRequestParams, CallToolResult> Filter =>
            next => (context, cancellationToken) =>
            {
                var name = context.Params?.Name;

                string[]? names = null;
                lock (_presetArguments)
                {
                    if (name != null)
                    {
                        _presetArguments.TryGetValue(name, out names);
                    }
                }

                if (names != null)
                {
                    var arguments = context.Params!.Arguments ?? new Dictionary<string, JsonElement>();

                    FillMissing(arguments, names, Presets, Forced);
                    context.Params.Arguments = arguments;
                }

                return next(context, cancellationToken);
            };

        /// <summary>
        /// Adds the preset of each named argument that the call omitted or sent as null or blank.
        /// A value the caller did send is never replaced, except for the forced arguments, which
        /// always take the preset.
        /// </summary>
        public static void FillMissing(
            IDictionary<string, JsonElement> arguments,
            IEnumerable<string> names,
            IReadOnlyDictionary<string, JsonElement> presets,
            ISet<string>? forced = null)
        {
            foreach (var name in names)
            {
                if (!presets.TryGetValue(name, out var preset))
                {
                    continue;
                }

                // A forced argument takes the preset whatever the call sent
                if (forced != null && forced.Contains(name))
                {
                    arguments[name] = preset;
                    continue;
                }

                if (!arguments.TryGetValue(name, out var given) || IsOmitted(given))
                {
                    arguments[name] = preset;
                }
            }
        }

        /// <summary>
        /// True for a value that counts as "not given": null, undefined, or a blank string (models
        /// often send "" instead of leaving the argument out).
        /// </summary>
        public static bool IsOmitted(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => true,
                JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()),
                _ => false
            };
        }
    }
}
