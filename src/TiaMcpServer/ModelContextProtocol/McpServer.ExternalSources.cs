using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.ExternalSources;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Read-only MCP tools for PLC external source files.
    ///
    /// Callers: registered through Program.BuildToolTypes() and invoked directly by the external
    /// source test class. Affected API: additive only - a new partial of the existing McpServer
    /// type. Data: returns ResponseExternalSource* as MCP structuredContent. No file I/O.
    ///
    /// PlcExternalSource types only Name, so GetExternalSourceInfo leans on the generic
    /// attribute bag for everything else rather than guessing at attribute names.
    /// </summary>
    public static partial class McpServer
    {
        [McpServerTool(Name = "GetExternalSources", Title = "Get PLC external sources", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the external source files of a plc software, optionally filtered by a regular expression on the source name")]
        public static ResponseExternalSources GetExternalSources(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: optional regular expression to filter the external source names")] string regexName = "")
        {
            try
            {
                var sources = Portal.GetExternalSources(softwarePath, regexName);

                return new ResponseExternalSources
                {
                    Message = $"{sources.Count} external source(s) retrieved from '{softwarePath}'",
                    Items = sources.Select(ToExternalSourceInfo).ToList(),
                    Meta = Ok(new JsonObject { ["totalExternalSources"] = sources.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving external sources from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetExternalSourceInfo", Title = "Get PLC external source info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a single external source file. Beyond its name, all metadata is returned in the generic Attributes list")]
        public static ResponseExternalSourceInfo GetExternalSourceInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("sourcePath: root-relative path of the external source, e.g. 'SourceGroup1/Source_1'")] string sourcePath)
        {
            try
            {
                var source = Portal.GetExternalSource(softwarePath, sourcePath)
                    ?? throw new McpException($"External source not found at '{sourcePath}' in '{softwarePath}'. Use 'GetExternalSources' to list the available sources.");

                var info = ToExternalSourceInfo(source);
                info.Message = $"External source info retrieved from '{sourcePath}' in '{softwarePath}'";
                info.Attributes = Helper.GetAttributeList(source);
                info.Meta = Ok();

                return info;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving external source info from '{sourcePath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        private static ResponseExternalSourceInfo ToExternalSourceInfo(PlcExternalSource source)
        {
            return new ResponseExternalSourceInfo
            {
                Name = source.Name,
                Path = Portal.GetExternalSourcePath(source)
            };
        }
    }
}
