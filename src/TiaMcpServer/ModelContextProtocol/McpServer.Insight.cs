using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.CrossReference;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Understanding a PLC without issuing a dozen calls: what is in it, and what uses what.
    ///
    /// Callers: the MCP host, through tool registration. Affected API: none existing - both
    /// tools are new, and GetCrossReferences stays exactly as it was for callers that need the
    /// full tree. Reads and writes no data files.
    /// </summary>
    public static partial class McpServer
    {
        #region insight

        [McpServerTool(Name = "GetPlcSummary", Title = "Summarise a PLC software", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Counts, programming languages and health of one PLC software in a single call - replaces listing blocks, types, tags, tag tables and watch tables separately just to see what is there. Also names the inconsistent objects (which refuse to export until compiled) and the know-how protected ones (whose content cannot be read)")]
        public static ResponsePlcSummary GetPlcSummary(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
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

        [McpServerTool(Name = "WhereUsed", Title = "Find what uses an object", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Answer 'what uses this?' for a tag, block, PLC data type or tag table by name. Resolves the name, picks the right object kind and flattens the cross-reference tree to a plain list of users. Use 'GetCrossReferences' instead when the full nested result or a specific filter is needed")]
        public static ResponseWhereUsed WhereUsed(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("name: the object to look up, by bare name or by full root-relative path")] string name,
            [Description("kind: restrict resolution to 'block', 'type', 'tag' or 'tagTable'. Default 'any' picks the single match, and reports the candidates when the name is ambiguous")] string kind = "any")
        {
            try
            {
                var matches = Portal.ResolveObjectPath(softwarePath, name, kind)
                    .Where(m => CrossReferenceKinds.Contains(m.Kind))
                    .ToList();

                if (matches.Count == 0)
                {
                    throw new McpException(
                        $"No block, PLC data type, tag or tag table named '{name}' in '{softwarePath}'. " +
                        "Watch tables and external source files have no cross references.");
                }

                if (matches.Count > 1)
                {
                    throw new McpException(
                        $"'{name}' is ambiguous in '{softwarePath}': " +
                        string.Join(", ", matches.Select(m => $"{m.Path} ({m.Kind})")) +
                        ". Pass the full path, or narrow it with 'kind'.");
                }

                var target = matches[0];
                var result = Portal.GetCrossReferences(softwarePath, target.Path, target.Kind, CrossReferenceFilter.AllObjects);

                var users = result.Sources
                    .SelectMany(source => source.References.Select(reference => new ResponseUsage
                    {
                        UsedBy = reference.Name,
                        Path = reference.Path,
                        Address = reference.Address,
                        TypeName = reference.TypeName
                    }))
                    .GroupBy(u => $"{u.Path}|{u.UsedBy}|{u.Address}")
                    .Select(g => g.First())
                    .OrderBy(u => u.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new ResponseWhereUsed
                {
                    Message = users.Count == 0
                        ? $"'{target.Path}' ({target.Kind}) is not used anywhere in '{softwarePath}'"
                        : $"'{target.Path}' ({target.Kind}) is used by {users.Count} object(s)",
                    Path = target.Path,
                    Kind = target.Kind,
                    Items = users,
                    Meta = Ok(new JsonObject { ["userCount"] = users.Count, ["kind"] = target.Kind })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error resolving usages of '{name}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "FindInCode", Title = "Search the program text", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Search the actual source text of program blocks and PLC data types with a regular expression - every other filter in this server matches object names only. Returns the object path, line number and the matching line. Each call exports the candidate objects behind the scenes, so narrow a large PLC with 'nameFilter'")]
        public static ResponseCodeSearch FindInCode(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("pattern: regular expression matched against each line, case-insensitive. Plain text works too")] string pattern,
            [Description("nameFilter: optional regular expression on object names, to limit which objects are searched. Empty (default) searches all of them")] string nameFilter = "",
            [Description("maxResults: stop after this many matching lines (default 200)")] int maxResults = 200)
        {
            try
            {
                var result = Portal.FindInCode(softwarePath, pattern, nameFilter, maxResults);

                return new ResponseCodeSearch
                {
                    Message = $"'{pattern}' matched {result.Items.Count} line(s) across {result.ObjectsSearched} object(s)" +
                              (result.Truncated ? $", stopped at {maxResults}" : string.Empty),
                    Pattern = result.Pattern,
                    Items = result.Items.Select(m => new ResponseCodeMatch
                    {
                        ObjectPath = m.ObjectPath,
                        Format = m.Format,
                        Line = m.Line,
                        Text = m.Text
                    }).ToList(),
                    ObjectsSearched = result.ObjectsSearched,
                    Unsearchable = result.Unsearchable,
                    Truncated = result.Truncated,
                    Meta = Ok(new JsonObject
                    {
                        ["matches"] = result.Items.Count,
                        ["objectsSearched"] = result.ObjectsSearched,
                        ["truncated"] = result.Truncated
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error searching '{softwarePath}' for '{pattern}': {ex.Message}", ex);
            }
        }

        /// <summary>Object kinds Openness can produce cross references for.</summary>
        private static readonly HashSet<string> CrossReferenceKinds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "block", "type", "tag", "tagTable" };

        #endregion
    }
}
