using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Blocks;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region plc software

        [McpServerTool(Name = "GetSoftwareTree", Title = "Get software tree", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get the structure/tree of a given PLC software showing program blocks, PLC data types, PLC tags, watch and force tables, and external source files")]
        public static ResponseSoftwareTree GetSoftwareTree(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("sections: optional comma separated subset of 'blocks,types,tags,watch,sources' to keep the output small; defaults to 'all'")] string sections = "all")
        {
            try
            {
                var tree = Portal.GetSoftwareTree(softwarePath, sections);

                if (!string.IsNullOrEmpty(tree))
                {
                    return new ResponseSoftwareTree
                    {
                        Message = $"Software tree retrieved from '{softwarePath}'",
                        Tree = "```\n" + tree + "\n```",
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving software tree from '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving software tree from '{softwarePath}': {ex.Message}", ex);
            }
        }

        #endregion
    }
}
