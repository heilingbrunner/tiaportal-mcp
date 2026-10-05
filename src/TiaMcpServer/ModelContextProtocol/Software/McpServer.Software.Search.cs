using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region insight

        [McpServerTool(Name = "FindInCode", Title = "Find in code", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Search the actual source text of program blocks and PLC data types with a regular expression - every other filter in this server matches object names only. Returns the object path, line number and the matching line. Each call exports the candidate objects behind the scenes, so narrow a large PLC with 'nameFilter'")]
        public static ResponseCodeSearch FindInCode(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
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

        #endregion
    }
}
