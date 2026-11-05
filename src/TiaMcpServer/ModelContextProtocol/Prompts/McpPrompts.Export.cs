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

        [McpServerPrompt(Name = "GetExportFormats"), Description("Show the export formats of tia.json and the export tool per object type")]
        public static string GetExportFormats(string exportPath)
        {
            return $@"Show which export format tia.json of an export folder sets for blocks and PLC data types, and which export tool writes each object type.

Common parameter values:
- exportPath: '${{workspacefolder}}/export/Program' is a good default; the folder that holds tia.json

Use the GetExportFormats tool with these parameters:
- exportPath: {exportPath}";
        }

        [McpServerPrompt(Name = "ExportHardware"), Description("Export the hardware configuration of a PLC device as an AML file")]
        public static string ExportHardware(string softwarePath, string exportPath)
        {
            return $@"Export the hardware configuration (AutomationML) of the device that owns a PLC software to an .aml file. This does not change the project.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC; in a slash command write a value with spaces in double quotes
- exportPath: '${{workspacefolder}}/export' is a good default; the file is written to <exportPath>/<softwarePath>/Hardware/<device name>.aml

Use the ExportHardware tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}";
        }

        [McpServerPrompt(Name = "ImportHardware"), Description("Import the hardware configuration of a PLC device from an AML file")]
        public static string ImportHardware(string softwarePath, string importPath, string importOption = "RetainTiaDevice", string dryRun = "true")
        {
            return $@"Import the hardware configuration (AutomationML) of the device that owns a PLC software from the .aml file ExportHardware wrote (requires the server started with '--allow-write').

With dryRun true only the file is checked. With dryRun false the server asks the user itself: a dialog of the client has a 'Yes, import' checkbox and a Submit button; do not answer it for the user. Do not save on your own: tell the user the change is only in memory and call SaveProject (or SaveSession in a local session) only after the user explicitly says 'save project'.

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- importPath: folder that holds the file; it is read from <importPath>/<softwarePath>/Hardware/<device name>.aml
- importOption: RetainTiaDevice (default) keeps an existing device, OverwriteTiaDevice replaces it, MoveToParkingLot moves it to the parking lot
- dryRun: Use true (default) to only check the file, false to really import

Use the ImportHardware tool with these parameters:
- softwarePath: {softwarePath}
- importPath: {importPath}
- importOption: {importOption}
- dryRun: {NormalizeBool(dryRun)}";
        }

        #endregion
    }
}
