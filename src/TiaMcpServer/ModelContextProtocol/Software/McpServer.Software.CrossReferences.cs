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
    public static partial class McpServer
    {
        // From the former McpServer.CrossReferences.cs:
        // Read-only MCP tool for PLC cross references.
        //
        // Callers: registered through Program.BuildTools() and invoked directly by the cross
        // reference test class. Affected API: additive only - a new partial of the existing
        // McpServer type. Data: returns ResponseCrossReferences as MCP structuredContent. No file I/O.
        //
        // Output size is the real constraint here: Sources -> References -> Locations nests three
        // deep and SourceObject.Children recurses, so an unfiltered AllObjects query on a real PLC
        // produces megabytes. maxDepth defaults to 1 and the response reports Truncated.

        #region cross references

        /// <summary>
        /// Mutable counters for the recursive projection. A class rather than 'ref' parameters
        /// because ref locals cannot be captured by the lambdas used below.
        /// </summary>
        private sealed class CrossRefTally
        {
            public bool Truncated;
            public int ReferenceCount;
        }

        [McpServerTool(Name = "GetCrossReferences", Title = "Get cross references", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get cross references for a PLC software or for one block, type, tag table, tag or block group inside it. Watch tables, force tables and external sources have no cross references")]
        public static ResponseCrossReferences GetCrossReferences(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("objectPath: optional root-relative path of a block, type, tag table, tag or block group; empty targets the whole PLC software")] string objectPath = "",
            [Description("objectKind: 'auto' (default), 'block', 'type', 'tagTable', 'tag' or 'blockGroup'")] string objectKind = "auto",
            [Description("filter: 'AllObjects' (default), 'ObjectsWithReferences', 'ObjectsWithoutReferences' or 'UnusedObjects'")] string filter = "AllObjects",
            [Description("maxDepth: 1 = sources and their references (default), 2 = also source children, 3 = also reference locations. Keeps large results manageable")] int maxDepth = 1)
        {
            try
            {
                if (!Enum.TryParse<CrossReferenceFilter>(filter, ignoreCase: true, out var parsedFilter))
                {
                    throw new McpException(
                        $"Invalid filter '{filter}'. Allowed values are: {string.Join(", ", Enum.GetNames(typeof(CrossReferenceFilter)))}.");
                }

                var depth = Math.Max(1, Math.Min(3, maxDepth));
                var tally = new CrossRefTally();

                var result = Portal.GetCrossReferences(softwarePath, objectPath, objectKind, parsedFilter);

                var sources = result.Sources.Select(s => ToSource(s, depth, tally)).ToList();

                var target = string.IsNullOrEmpty(objectPath) ? softwarePath : objectPath;

                return new ResponseCrossReferences
                {
                    Message = $"{sources.Count} cross reference source(s) with {tally.ReferenceCount} reference(s) retrieved for '{target}'"
                              + (tally.Truncated ? $" (truncated at maxDepth {depth})" : string.Empty),
                    Sources = sources,
                    SourceCount = sources.Count,
                    ReferenceCount = tally.ReferenceCount,
                    Truncated = tally.Truncated,
                    Meta = Ok(new JsonObject
                    {
                        ["filter"] = parsedFilter.ToString(),
                        ["maxDepth"] = depth,
                        ["sourceCount"] = sources.Count,
                        ["referenceCount"] = tally.ReferenceCount
                    })
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving cross references from '{softwarePath}': {ex.Message}", ex);
            }
        }

        private static CrossRefSource ToSource(SourceObject source, int maxDepth, CrossRefTally tally)
        {
            var references = new List<CrossRefReference>();

            foreach (var reference in source.References)
            {
                tally.ReferenceCount++;

                List<CrossRefLocation>? locations = null;

                if (maxDepth >= 3)
                {
                    locations = reference.Locations.Select(ToLocation).ToList();
                }
                else if (reference.Locations.Count > 0)
                {
                    tally.Truncated = true;
                }

                references.Add(new CrossRefReference
                {
                    Name = reference.Name,
                    Path = reference.Path,
                    Address = reference.Address,
                    Device = reference.Device,
                    TypeName = reference.TypeName,
                    Locations = locations
                });
            }

            List<CrossRefSource>? children = null;

            if (maxDepth >= 2)
            {
                children = source.Children.Select(c => ToSource(c, maxDepth, tally)).ToList();
            }
            else if (source.Children.Count > 0)
            {
                tally.Truncated = true;
            }

            return new CrossRefSource
            {
                Name = source.Name,
                Path = source.Path,
                Address = source.Address,
                Device = source.Device,
                TypeName = source.TypeName,
                References = references,
                Children = children
            };
        }

        private static CrossRefLocation ToLocation(Location location)
        {
            return new CrossRefLocation
            {
                Name = location.Name,
                Address = location.Address,
                TypeName = location.TypeName,
                Access = location.Access.ToString(),
                ReferenceType = location.ReferenceType.ToString(),
                ReferenceLocation = location.ReferenceLocation,
                ReferencedAsName = location.ReferencedAsName
            };
        }

        #endregion

        #region insight

        [McpServerTool(Name = "WhereUsed", Title = "Where used", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Answer 'what uses this?' for a tag, block, PLC data type or tag table by name. Resolves the name, picks the right object kind and flattens the cross-reference tree to a plain list of users. Use 'GetCrossReferences' instead when the full nested result or a specific filter is needed")]
        public static ResponseWhereUsed WhereUsed(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
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

        /// <summary>Object kinds Openness can produce cross references for.</summary>
        private static readonly HashSet<string> CrossReferenceKinds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "block", "type", "tag", "tagTable" };

        #endregion
    }
}
