using Siemens.Engineering;
using Siemens.Engineering.Compiler;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.Hmi.Screen;
using Siemens.Engineering.Hmi.Tag;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // Screens and HMI tag tables of a WinCC Basic/Comfort/Advanced HMI (HmiTarget). WinCC
        // Unified uses another object model and is not covered.
        //
        // Callers: the HMI tools in McpServer.Hmi.cs. Affected API: none existing - all members
        // are new. File I/O: the exports write '<exportPath>/<hmiPath>/Screens/...' and
        // '<exportPath>/<hmiPath>/HMI tags/...'; the imports read one caller-named file below
        // exportPath. Project changes stay in memory until SaveProject / SaveSession.

        public const string HmiScreensFolder = "Screens";
        public const string HmiTagsFolder = "HMI tags";

        #region resolve

        private HmiTarget GetHmiTargetOrThrow(string hmiPath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
            }

            if (GetSoftwareContainer(hmiPath)?.Software is not HmiTarget hmiTarget)
            {
                throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"No WinCC Basic/Comfort/Advanced HMI found at '{hmiPath}'. Use 'GetProjectTree' to discover valid paths (the device and the device item of the HmiTarget, e.g. 'HMI_1/HMI_RT_1').");
            }

            return hmiTarget;
        }

        private ScreenFolder GetScreenFolderOrThrow(HmiTarget hmi, string folderPath)
        {
            ScreenFolder folder = hmi.ScreenFolder;

            foreach (var segment in NormalizeGroupPath(folderPath).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                folder = folder.Folders.Find(segment)
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"Screen folder '{segment}' of '{folderPath}' not found. Use 'ExportHmiScreens' to see the folder structure.");
            }

            return folder;
        }

        private TagFolder GetTagFolderOrThrow(HmiTarget hmi, string folderPath)
        {
            TagFolder folder = hmi.TagFolder;

            foreach (var segment in NormalizeGroupPath(folderPath).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                folder = folder.Folders.Find(segment)
                    ?? throw new PortalException(PortalErrorCode.NotFound,
                        $"HMI tag folder '{segment}' of '{folderPath}' not found. Use 'ExportHmiTagTables' to see the folder structure.");
            }

            return folder;
        }

        /// <summary>
        /// Resolves a file name relative to exportPath and refuses anything outside it, so an
        /// import can only read what lives in the configured export tree.
        /// </summary>
        private static string ResolveFileBelow(string exportPath, string file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The file name is empty.");
            }

            var root = Path.GetFullPath(exportPath).TrimEnd('\\') + "\\";
            var full = Path.GetFullPath(Path.Combine(root, file));

            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'{file}' is outside the export path '{exportPath}'.");
            }

            if (!File.Exists(full))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"Import file '{full}' does not exist on the machine running this server.");
            }

            return full;
        }

        #endregion

        #region export

        /// <summary>
        /// Exports every screen whose name matches the regular expression to
        /// '&lt;exportPath&gt;/&lt;hmiPath&gt;/Screens/&lt;folders&gt;/&lt;screen&gt;.xml'. Returns the written files.
        /// </summary>
        public List<string> ExportHmiScreens(string hmiPath, string exportPath, string regexName)
        {
            return Operation.Run(_logger, nameof(ExportHmiScreens), PortalErrorCode.ExportFailed,
                () =>
                {
                    var hmi = GetHmiTargetOrThrow(hmiPath);
                    var root = Path.Combine(ResolveSoftwareRoot(exportPath, hmiPath), HmiScreensFolder);
                    var filter = new Regex(string.IsNullOrEmpty(regexName) ? ".*" : regexName, RegexOptions.IgnoreCase);
                    var written = new List<string>();

                    void Walk(ScreenFolder folder, string directory)
                    {
                        foreach (var screen in folder.Screens.Where(s => filter.IsMatch(s.Name)))
                        {
                            written.Add(ExportEngineeringObject(screen, Path.Combine(directory, FileNames.Safe(screen.Name) + ".xml"), screen.Export));
                        }

                        foreach (var sub in folder.Folders)
                        {
                            Walk(sub, Path.Combine(directory, FileNames.Safe(sub.Name)));
                        }
                    }

                    Walk(hmi.ScreenFolder, root);
                    return written;
                },
                ("hmiPath", hmiPath), ("exportPath", exportPath), ("regexName", regexName));
        }

        /// <summary>
        /// Exports every HMI tag table whose name matches the regular expression to
        /// '&lt;exportPath&gt;/&lt;hmiPath&gt;/HMI tags/&lt;folders&gt;/&lt;table&gt;.xml'. Returns the written files.
        /// </summary>
        public List<string> ExportHmiTagTables(string hmiPath, string exportPath, string regexName)
        {
            return Operation.Run(_logger, nameof(ExportHmiTagTables), PortalErrorCode.ExportFailed,
                () =>
                {
                    var hmi = GetHmiTargetOrThrow(hmiPath);
                    var root = Path.Combine(ResolveSoftwareRoot(exportPath, hmiPath), HmiTagsFolder);
                    var filter = new Regex(string.IsNullOrEmpty(regexName) ? ".*" : regexName, RegexOptions.IgnoreCase);
                    var written = new List<string>();

                    void Walk(TagFolder folder, string directory)
                    {
                        foreach (var table in folder.TagTables.Where(t => filter.IsMatch(t.Name)))
                        {
                            written.Add(ExportEngineeringObject(table, Path.Combine(directory, FileNames.Safe(table.Name) + ".xml"), table.Export));
                        }

                        foreach (var sub in folder.Folders)
                        {
                            Walk(sub, Path.Combine(directory, FileNames.Safe(sub.Name)));
                        }
                    }

                    Walk(hmi.TagFolder, root);
                    return written;
                },
                ("hmiPath", hmiPath), ("exportPath", exportPath), ("regexName", regexName));
        }

        private static string ExportEngineeringObject(object item, string target, Action<FileInfo, ExportOptions> export)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (File.Exists(target))
            {
                File.Delete(target);
            }

            export(new FileInfo(target), ExportOptions.WithDefaults);
            return target;
        }

        #endregion

        #region import

        /// <summary>
        /// Imports one screen XML (a file below exportPath) into a screen folder. Returns the
        /// names of the imported screens.
        /// </summary>
        public List<string> ImportHmiScreen(string hmiPath, string folderPath, string exportPath, string file, bool overwrite)
        {
            return Operation.Run(_logger, nameof(ImportHmiScreen), PortalErrorCode.ImportFailed,
                () =>
                {
                    var hmi = GetHmiTargetOrThrow(hmiPath);
                    var folder = GetScreenFolderOrThrow(hmi, folderPath);
                    var full = ResolveFileBelow(exportPath, file);

                    return folder.Screens
                        .Import(new FileInfo(full), overwrite ? ImportOptions.Override : ImportOptions.None)
                        .Select(s => s.Name)
                        .ToList();
                },
                ("hmiPath", hmiPath), ("folderPath", folderPath), ("file", file));
        }

        /// <summary>
        /// Imports one HMI tag table XML (a file below exportPath) into an HMI tag folder.
        /// Returns the names of the imported tables.
        /// </summary>
        public List<string> ImportHmiTagTable(string hmiPath, string folderPath, string exportPath, string file, bool overwrite)
        {
            return Operation.Run(_logger, nameof(ImportHmiTagTable), PortalErrorCode.ImportFailed,
                () =>
                {
                    var hmi = GetHmiTargetOrThrow(hmiPath);
                    var folder = GetTagFolderOrThrow(hmi, folderPath);
                    var full = ResolveFileBelow(exportPath, file);

                    return folder.TagTables
                        .Import(new FileInfo(full), overwrite ? ImportOptions.Override : ImportOptions.None)
                        .Select(t => t.Name)
                        .ToList();
                },
                ("hmiPath", hmiPath), ("folderPath", folderPath), ("file", file));
        }

        #endregion

        #region compile

        public CompilerResult CompileHmi(string hmiPath)
        {
            return Operation.Run(_logger, nameof(CompileHmi), PortalErrorCode.InvalidState,
                () => GetHmiTargetOrThrow(hmiPath).GetService<ICompilable>()?.Compile()
                    ?? throw new PortalException(PortalErrorCode.NotSupported, $"The HMI at '{hmiPath}' cannot be compiled through Openness."),
                ("hmiPath", hmiPath));
        }

        #endregion
    }
}
