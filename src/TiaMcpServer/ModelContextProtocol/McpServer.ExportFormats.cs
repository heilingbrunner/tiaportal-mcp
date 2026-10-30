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
        // The export formats TIA Portal is configured for (Options > Settings > Version control
        // interface > Export formats), read from the global TIA Portal settings. Openness has no
        // typed access to them, so the settings tree is read and the entries are looked up by name.
        //
        // Callers: the MCP host, through tool registration. Affected API: none existing - the tool
        // is new. File I/O: none yet.

        #region export formats

        [McpServerTool(Name = "GetExportFormats", Title = "Get export formats", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to see every setting of the TIA Portal settings tree (Options > Settings) with its folder path, name and value. Pass listAll=true; it is how the names of the export format settings (Version control interface > Export formats) are found")]
        public static ResponseExportFormats GetExportFormats(
            [Description("listAll: true returns every setting of the settings tree")] bool listAll = false)
        {
            try
            {
                var settings = listAll ? Portal.GetSettings() : null;

                return new ResponseExportFormats
                {
                    Message = settings == null
                        ? "Export formats retrieved"
                        : $"{settings.Count} TIA Portal setting(s) retrieved",
                    Settings = settings?
                        .Select(s => new ResponseSettingItem { Path = s.Path, Name = s.Name, Value = s.Value })
                        .ToList(),
                    Meta = Ok(new JsonObject
                    {
                        ["settings"] = settings?.Count
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error reading the export formats: {ex.Message}", ex);
            }
        }

        #endregion
    }
}
