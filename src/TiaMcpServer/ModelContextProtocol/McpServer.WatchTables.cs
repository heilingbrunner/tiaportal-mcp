using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.WatchAndForceTables;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Read-only MCP tools for PLC watch and force tables.
    ///
    /// Callers: registered through Program.BuildToolTypes() and invoked directly by the watch
    /// table test class. Affected API: additive only - a new partial of the existing McpServer
    /// type. Data: returns ResponseWatchTable*/TableEntryInfo as MCP structuredContent; only
    /// ExportWatchTable touches the file system, writing one .xml.
    /// </summary>
    public static partial class McpServer
    {
        #region watch and force tables

        [McpServerTool(Name = "GetWatchTables", Title = "Get PLC watch tables", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the PLC watch tables of a plc software, optionally filtered by a regular expression on the table name. Entries are omitted; use GetWatchTableInfo for one table's rows")]
        public static ResponseWatchTables GetWatchTables(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("regexName: optional regular expression to filter the watch table names")] string regexName = "")
        {
            try
            {
                var tables = Portal.GetWatchTables(softwarePath, regexName);

                return new ResponseWatchTables
                {
                    Message = $"{tables.Count} watch table(s) retrieved from '{softwarePath}'",
                    Items = tables.Select(t => ToWatchTableInfo(t, includeEntries: false)).ToList(),
                    Meta = Ok(new JsonObject { ["totalWatchTables"] = tables.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving watch tables from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetWatchTableInfo", Title = "Get PLC watch table info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Get a single PLC watch table including all of its entries (address, display format, monitor and modify settings)")]
        public static ResponseWatchTableInfo GetWatchTableInfo(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'")] string watchTablePath)
        {
            try
            {
                var table = Portal.GetWatchTable(softwarePath, watchTablePath)
                    ?? throw new McpException($"Watch table not found at '{watchTablePath}' in '{softwarePath}'. Use 'GetWatchTables' to list the available tables.");

                var info = ToWatchTableInfo(table, includeEntries: true);
                info.Message = $"Watch table info retrieved from '{watchTablePath}' in '{softwarePath}'";
                info.Attributes = Helper.GetAttributeList(table);
                info.Meta = Ok();

                return info;
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving watch table info from '{watchTablePath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetForceTables", Title = "Get PLC force tables", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the PLC force tables of a plc software including their entries. A force table is created and owned by the system: it cannot be created or deleted through Openness")]
        public static ResponseWatchTables GetForceTables(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath)
        {
            try
            {
                var tables = Portal.GetForceTables(softwarePath);

                return new ResponseWatchTables
                {
                    Message = $"{tables.Count} force table(s) retrieved from '{softwarePath}'",
                    Items = tables.Select(ToForceTableInfo).ToList(),
                    Meta = Ok(new JsonObject { ["totalForceTables"] = tables.Count })
                };
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving force tables from '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "ExportWatchTable", Title = "Export a PLC watch table", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export a PLC watch table to an XML file on the file system of the machine running this server. Does not modify the project")]
        public static ResponseExportWatchTable ExportWatchTable(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'")] string watchTablePath,
            [Description("exportPath: directory on this machine that receives the XML file")] string exportPath,
            [Description("preservePath: recreate the watch table group structure below exportPath")] bool preservePath = false)
        {
            try
            {
                var table = Portal.ExportWatchTable(softwarePath, watchTablePath, exportPath, preservePath)
                    ?? throw new McpException($"Failed exporting watch table '{watchTablePath}' from '{softwarePath}'");

                return new ResponseExportWatchTable
                {
                    Message = $"Watch table '{watchTablePath}' exported to '{exportPath}'",
                    Name = table.Name,
                    Path = Portal.GetWatchTablePath(table),
                    Meta = Ok()
                };
            }
            catch (TiaMcpServer.Siemens.PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error exporting watch table '{watchTablePath}' from '{softwarePath}': {ex.Message}", ex);
            }
        }

        #endregion

        #region mappers

        private static ResponseWatchTableInfo ToWatchTableInfo(PlcWatchTable table, bool includeEntries)
        {
            return new ResponseWatchTableInfo
            {
                Name = table.Name,
                Path = Portal.GetWatchTablePath(table),
                Kind = "Watch",
                IsConsistent = table.IsConsistent,
                EntryCount = table.Entries.Count,
                Entries = includeEntries ? ToEntryList(table.Entries) : null
            };
        }

        private static ResponseWatchTableInfo ToForceTableInfo(PlcForceTable table)
        {
            return new ResponseWatchTableInfo
            {
                Name = table.Name,
                Path = Portal.GetForceTablePath(table),
                Kind = "Force",
                IsConsistent = table.IsConsistent,
                EntryCount = table.Entries.Count,
                Entries = ToEntryList(table.Entries)
            };
        }

        /// <summary>
        /// Projects the shared entry composition. Watch, force and plain comment rows all live in
        /// a PlcTableCommentEntryComposition, so the concrete row type decides which fields apply.
        /// </summary>
        private static List<TableEntryInfo> ToEntryList(PlcTableCommentEntryComposition entries)
        {
            var list = new List<TableEntryInfo>();

            foreach (var entry in entries)
            {
                if (entry is PlcWatchTableEntry watch)
                {
                    list.Add(new TableEntryInfo
                    {
                        Kind = "Watch",
                        Name = watch.Name,
                        Address = watch.Address,
                        DisplayFormat = watch.DisplayFormat.ToString(),
                        MonitorTrigger = watch.MonitorTrigger.ToString(),
                        ModifyTrigger = watch.ModifyTrigger.ToString(),
                        ModifyValue = watch.ModifyValue,
                        ModifyIntention = watch.ModifyIntention.ToString()
                    });
                }
                else if (entry is PlcForceTableEntry force)
                {
                    list.Add(new TableEntryInfo
                    {
                        Kind = "Force",
                        Name = force.Name,
                        Address = force.Address,
                        DisplayFormat = force.DisplayFormat.ToString(),
                        MonitorTrigger = force.MonitorTrigger.ToString(),
                        ForceValue = force.ForceValue,
                        ForceIntention = force.ForceIntention.ToString()
                    });
                }
                else
                {
                    // A plain comment row exposes nothing but Parent and Delete, so its text is
                    // only reachable through the generic attribute bag.
                    list.Add(new TableEntryInfo
                    {
                        Kind = "Comment",
                        Name = Helper.GetAttributeList(entry)
                            .FirstOrDefault(a => a.Name == "Name")?.Value?.ToString()
                    });
                }
            }

            return list;
        }

        #endregion
    }
}
