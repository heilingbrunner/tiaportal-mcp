using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
    {
        #region Export Templates

        [McpServerPrompt(Name = "ExportOpcUaXml"), Description("Export all PLC tags and DB variables ('Accessible from HMI/OPC UA') as XML")]
        public static string ExportOpcUaXml(string softwarePath, string exportPath)
        {
            return $@"Export the OPC UA data of a PLC to an XML file.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export' is a good default; the file is written to <exportPath>/<softwarePath>/OPC UA/<PLC name>.xml

Use the ExportOpcUaXml tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}";
        }

        #endregion
    }
}
