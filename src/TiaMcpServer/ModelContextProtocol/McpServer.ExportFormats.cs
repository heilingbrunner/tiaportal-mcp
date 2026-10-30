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
        // interface > Export formats). Openness cannot read them, so they are kept in
        // '<exportPath>/tia.json', written by the user; ExportPlcProgram needs the same file.
        //
        // Callers: the MCP host, through tool registration. Affected API: none existing - the tool
        // is new. File I/O: reads '<exportPath>/tia.json'.

        #region export formats

        [McpServerTool(Name = "GetExportFormats", Title = "Get export formats", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to see which export format TIA Portal is configured for each object type (Version control interface > Export formats) and which export tool writes it, before choosing an export tool. Openness cannot read these settings, so they are read from '<exportPath>/tia.json', which the user maintains: {\"version control interface\": {\"code blocks-LAD\": \"SIMATIC SD(.s7dcl, .s7res)\", \"code blocks-STL\": \".awl\", ...}}. When the file does not exist it is created with the defaults of TIA Portal for a new workspace and 'Hint' asks the user to check it; tell the user. 'ExportPlcProgram' uses the same file")]
        public static ResponseExportFormats GetExportFormats(
            [Description("exportPath: the export folder that holds tia.json")] string exportPath)
        {
            try
            {
                var settings = ExportFormatSettings.Load(exportPath);

                var formats = settings.Values
                    .Select(v =>
                    {
                        var kind = ExportFormats.ParseKind(v.Value);

                        return new ResponseExportFormat
                        {
                            ObjectType = v.Key,
                            Format = v.Value,
                            Kind = kind.ToString(),
                            RecommendedTool = ExportFormats.RecommendedTool(v.Key, kind)
                        };
                    })
                    .ToList();

                return new ResponseExportFormats
                {
                    Message = $"{formats.Count} export format(s) read from '{settings.File}'" +
                              (settings.Hint != null ? " " + settings.Hint : string.Empty),
                    File = settings.File,
                    Created = settings.WasCreated,
                    Hint = settings.Hint,
                    Formats = formats,
                    Meta = Ok(new JsonObject
                    {
                        ["formats"] = formats.Count
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
