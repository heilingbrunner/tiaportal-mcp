using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.CrossReference;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Read-only MCP tool for PLC cross references.
    ///
    /// Callers: registered through Program.BuildToolTypes() and invoked directly by the cross
    /// reference test class. Affected API: additive only - a new partial of the existing
    /// McpServer type. Data: returns ResponseCrossReferences as MCP structuredContent. No file I/O.
    ///
    /// Output size is the real constraint here: Sources -> References -> Locations nests three
    /// deep and SourceObject.Children recurses, so an unfiltered AllObjects query on a real PLC
    /// produces megabytes. maxDepth defaults to 1 and the response reports Truncated.
    /// </summary>
    public static partial class McpServer
    {
        /// <summary>
        /// Mutable counters for the recursive projection. A class rather than 'ref' parameters
        /// because ref locals cannot be captured by the lambdas used below.
        /// </summary>
        private sealed class CrossRefTally
        {
            public bool Truncated;
            public int ReferenceCount;
        }

        [McpServerTool(Name = "GetCrossReferences", Title = "Get PLC cross references", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get cross references for a PLC software or for one block, type, tag table, tag or block group inside it. Watch tables, force tables and external sources have no cross references")]
        public static ResponseCrossReferences GetCrossReferences(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("objectPath: optional root-relative path of a block, type, tag table, tag or block group; empty targets the whole plc software")] string objectPath = "",
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
    }
}
