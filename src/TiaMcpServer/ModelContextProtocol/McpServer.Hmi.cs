using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // Tools for WinCC Basic/Comfort/Advanced HMIs: export and import of screens and HMI tag
        // tables as XML, and compiling the HMI.
        //
        // Callers: registered by Program.BuildTools(); the imports are [WriteTool] and need
        // '--allow-write'. Affected API: additive - a partial of McpServer. File I/O: the exports
        // write below '<exportPath>/<hmiPath>/'; the imports read one file below exportPath.
        // The HMI path is named 'hmiPath', not 'softwarePath', so a PLC preset never applies to it.

        #region hmi

        [McpServerTool(Name = "ExportHmiScreens", Title = "Export HMI screens", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export the screens of a WinCC Basic/Comfort/Advanced HMI as XML into '<exportPath>/<hmiPath>/Screens/<folders>/'. An existing file is overwritten. Use the files as templates for 'ImportHmiScreen'")]
        public static ResponseHmiFiles ExportHmiScreens(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>', e.g. 'HMI Woning/HMI_RT_3'")] string hmiPath,
            [Description("exportPath: directory on this machine that receives the files; files are written below <exportPath>/<hmiPath>/")] string exportPath,
            [Description("regexName: regular expression on the screen name, empty exports all screens")] string regexName = "")
        {
            return HmiCall(nameof(ExportHmiScreens), () => Files($"Screens of '{hmiPath}' exported", Portal.ExportHmiScreens(hmiPath, exportPath, regexName), false));
        }

        [McpServerTool(Name = "ExportHmiTagTables", Title = "Export HMI tag tables", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export the HMI tag tables of a WinCC Basic/Comfort/Advanced HMI as XML into '<exportPath>/<hmiPath>/HMI tags/<folders>/'. An existing file is overwritten. Use the files as templates for 'ImportHmiTagTable'")]
        public static ResponseHmiFiles ExportHmiTagTables(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>', e.g. 'HMI Woning/HMI_RT_3'")] string hmiPath,
            [Description("exportPath: directory on this machine that receives the files; files are written below <exportPath>/<hmiPath>/")] string exportPath,
            [Description("regexName: regular expression on the tag table name, empty exports all tables")] string regexName = "")
        {
            return HmiCall(nameof(ExportHmiTagTables), () => Files($"HMI tag tables of '{hmiPath}' exported", Portal.ExportHmiTagTables(hmiPath, exportPath, regexName), false));
        }

        [WriteTool]
        [McpServerTool(Name = "ImportHmiScreen", Title = "Import HMI screen", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import one screen XML file into a screen folder of a WinCC Basic/Comfort/Advanced HMI. The HMI tags the screen uses must exist first ('ImportHmiTagTable'). With overwrite false an existing screen of the same name makes the import fail")]
        public static ResponseHmiFiles ImportHmiScreen(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>'")] string hmiPath,
            [Description("exportPath: directory on this machine that 'file' is relative to")] string exportPath,
            [Description("file: the XML file, relative to exportPath, e.g. 'HMI Woning/HMI_RT_3/Screens/Afwezigheid.xml'")] string file,
            [Description("folderPath: screen folder below 'Screens' that receives the screen, e.g. 'Systeem'; empty for the root")] string folderPath = "",
            [Description("overwrite: replace an existing screen of the same name (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(ImportHmiScreen), () => Files($"Screen imported from '{file}'", Portal.ImportHmiScreen(hmiPath, folderPath, exportPath, file, overwrite), true));
        }

        [WriteTool]
        [McpServerTool(Name = "ImportHmiTagTable", Title = "Import HMI tag table", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import one HMI tag table XML file into an HMI tag folder of a WinCC Basic/Comfort/Advanced HMI. The connections the tags name must exist. With overwrite false an existing table of the same name makes the import fail")]
        public static ResponseHmiFiles ImportHmiTagTable(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>'")] string hmiPath,
            [Description("exportPath: directory on this machine that 'file' is relative to")] string exportPath,
            [Description("file: the XML file, relative to exportPath, e.g. 'HMI Woning/HMI_RT_3/HMI tags/Afwezigheid.xml'")] string file,
            [Description("folderPath: HMI tag folder below 'HMI tags' that receives the table; empty for the root")] string folderPath = "",
            [Description("overwrite: replace an existing tag table of the same name (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(ImportHmiTagTable), () => Files($"HMI tag table imported from '{file}'", Portal.ImportHmiTagTable(hmiPath, folderPath, exportPath, file, overwrite), true));
        }

        [McpServerTool(Name = "CompileHmi", Title = "Compile HMI", Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Compile a WinCC Basic/Comfort/Advanced HMI (software only, no download) and report every compiler message with the object it belongs to. Errors fail the call, and its text holds the complete report. Show the 'Report' to the user as tables, unchanged")]
        public static ResponseCompile CompileHmi(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>'")] string hmiPath)
        {
            return HmiCall(nameof(CompileHmi), () => BuildCompileResponse(Portal.CompileHmi(hmiPath), hmiPath));
        }

        private static ResponseHmiFiles Files(string message, System.Collections.Generic.List<string> items, bool pendingSave)
        {
            var meta = pendingSave ? OkMeta() : new JsonObject { ["timestamp"] = DateTime.Now, ["success"] = true };

            return new ResponseHmiFiles
            {
                Message = pendingSave ? $"{message}: {items.Count}. {SaveHint}" : $"{message}: {items.Count}",
                Items = items,
                Meta = meta
            };
        }

        private static T HmiCall<T>(string toolName, Func<T> body)
        {
            try
            {
                return body();
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error in '{toolName}': {ex.Message}", ex);
            }
        }

        #endregion
    }
}
