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
        // Tools for WinCC Basic/Comfort/Advanced HMIs: export and import of screens, the global
        // screen, HMI tag tables and text lists as XML, deleting a screen, and compiling the HMI.
        //
        // Callers: registered by Program.BuildTools(); the imports and the delete are [WriteTool]
        // and need '--allow-write'. Affected API: additive - a partial of McpServer. File I/O: the exports
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

        [McpServerTool(Name = "ExportHmiTextLists", Title = "Export HMI text lists", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export the text lists of a WinCC Basic/Comfort/Advanced HMI as XML into '<exportPath>/<hmiPath>/Text lists/'. An existing file is overwritten. Use the files as templates for 'ImportHmiTextList'")]
        public static ResponseHmiFiles ExportHmiTextLists(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>'")] string hmiPath,
            [Description("exportPath: directory on this machine that receives the files; files are written below <exportPath>/<hmiPath>/")] string exportPath,
            [Description("regexName: regular expression on the text list name, empty exports all lists")] string regexName = "")
        {
            return HmiCall(nameof(ExportHmiTextLists), () => Files($"Text lists of '{hmiPath}' exported", Portal.ExportHmiTextLists(hmiPath, exportPath, regexName), false));
        }

        [McpServerTool(Name = "ExportHmiGlobalScreen", Title = "Export HMI global screen", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Export the global screen (alarm windows, alarm indicator) of a WinCC Basic/Comfort/Advanced HMI as XML to '<exportPath>/<hmiPath>/Screens/Global screen.xml'. An existing file is overwritten. Use it as template for 'ImportHmiGlobalScreen'")]
        public static ResponseHmiFiles ExportHmiGlobalScreen(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>'")] string hmiPath,
            [Description("exportPath: directory on this machine that receives the file; it is written below <exportPath>/<hmiPath>/")] string exportPath)
        {
            return HmiCall(nameof(ExportHmiGlobalScreen), () => Files($"Global screen of '{hmiPath}' exported", Portal.ExportHmiGlobalScreen(hmiPath, exportPath), false));
        }

        [WriteTool]
        [McpServerTool(Name = "ImportHmiTextList", Title = "Import HMI text list", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import one text list XML file into a WinCC Basic/Comfort/Advanced HMI. With overwrite false an existing text list of the same name makes the import fail")]
        public static ResponseHmiFiles ImportHmiTextList(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>'")] string hmiPath,
            [Description("exportPath: directory on this machine that 'file' is relative to")] string exportPath,
            [Description("file: the XML file, relative to exportPath, e.g. 'HMI Loods/HMI_RT_1/Text lists/TextList_ScreenNames.xml'")] string file,
            [Description("overwrite: replace an existing text list of the same name (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(ImportHmiTextList), () => Files($"Text list imported from '{file}'", Portal.ImportHmiTextList(hmiPath, exportPath, file, overwrite), true));
        }

        [WriteTool]
        [McpServerTool(Name = "ImportHmiGlobalScreen", Title = "Import HMI global screen", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Import the global screen XML file into a WinCC Basic/Comfort/Advanced HMI. It always replaces the existing global screen")]
        public static ResponseHmiFiles ImportHmiGlobalScreen(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>'")] string hmiPath,
            [Description("exportPath: directory on this machine that 'file' is relative to")] string exportPath,
            [Description("file: the XML file, relative to exportPath, e.g. 'HMI Loods/HMI_RT_1/Screens/Global screen.xml'")] string file)
        {
            return Guarded(nameof(ImportHmiGlobalScreen), () => Files($"Global screen imported from '{file}'", Portal.ImportHmiGlobalScreen(hmiPath, exportPath, file), true));
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteHmiScreen", Title = "Delete HMI screen", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete one screen of a WinCC Basic/Comfort/Advanced HMI by name. Screens that navigate to it keep a dangling reference and fail to compile")]
        public static ResponseHmiFiles DeleteHmiScreen(
            [Description("hmiPath: path of the HmiTarget as 'GetProjectTree' shows it, '<device>/<device item>'")] string hmiPath,
            [Description("screenName: name of the screen, e.g. 'Lettertest'")] string screenName,
            [Description("folderPath: screen folder below 'Screens' that holds the screen, e.g. 'Systeem'; empty for the root")] string folderPath = "")
        {
            return Guarded(nameof(DeleteHmiScreen), () => Files($"Screen deleted from '{hmiPath}'", Portal.DeleteHmiScreen(hmiPath, folderPath, screenName), true));
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
