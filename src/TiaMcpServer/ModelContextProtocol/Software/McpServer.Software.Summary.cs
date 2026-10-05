using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // From the former McpServer.Insight.cs:
        // Understanding a PLC without issuing a dozen calls: what is in it, and what uses what.
        //
        // Callers: the MCP host, through tool registration. Affected API: none existing - both
        // tools are new, and GetCrossReferences stays exactly as it was for callers that need the
        // full tree. Reads and writes no data files.

        #region insight

        [McpServerTool(Name = "GetPlcSummary", Title = "Get PLC summary", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Counts, programming languages and health of one PLC software in a single call - replaces listing blocks, types, tags, tag tables and watch tables separately just to see what is there. Also names the inconsistent objects (which refuse to export until compiled) and the know-how protected ones (whose content cannot be read)")]
        public static ResponsePlcSummary GetPlcSummary(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath)
        {
            try
            {
                var summary = Portal.GetPlcSummary(softwarePath);

                return new ResponsePlcSummary
                {
                    Message = $"'{summary.Name}': {summary.BlockCount} block(s), {summary.TypeCount} PLC data type(s), " +
                              $"{summary.TagCount} tag(s) in {summary.TagTableCount} table(s), " +
                              $"{summary.InconsistentObjects.Count} inconsistent",
                    Name = summary.Name,
                    SoftwarePath = summary.SoftwarePath,
                    BlockCount = summary.BlockCount,
                    TypeCount = summary.TypeCount,
                    TagTableCount = summary.TagTableCount,
                    TagCount = summary.TagCount,
                    UserConstantCount = summary.UserConstantCount,
                    WatchTableCount = summary.WatchTableCount,
                    ExternalSourceCount = summary.ExternalSourceCount,
                    BlocksByKind = summary.BlocksByKind,
                    BlocksByLanguage = summary.BlocksByLanguage,
                    InconsistentObjects = summary.InconsistentObjects,
                    KnowHowProtectedObjects = summary.KnowHowProtectedObjects,
                    LastModified = summary.LastModified,
                    Meta = Ok(new JsonObject
                    {
                        ["blockCount"] = summary.BlockCount,
                        ["typeCount"] = summary.TypeCount,
                        ["inconsistentCount"] = summary.InconsistentObjects.Count
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error summarising '{softwarePath}': {ex.Message}", ex);
            }
        }

        #endregion
    }
}
