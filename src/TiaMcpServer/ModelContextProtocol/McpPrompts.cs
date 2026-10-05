using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    [McpServerPromptType]
    public static class McpPrompts
    {
        #region Basic Connection Templates

        [McpServerPrompt(Name = "Doctor"), Description("Diagnose the TIA Portal environment (connection, versions, user group)")]
        public static string Doctor()
        {
            return @"Diagnose the TIA Portal environment.

The report shows:
- Connected: whether this server currently holds a TIA Portal connection
- Project: name and path of the open project, or 'No project open'
- Active Version: the TIA major version this server was started with (see the --tia-major-version argument)
- Installed TIA Portal versions: every installed version >= V21 with its installation path, plus a check
  that the Openness assemblies ('Engineering') and the Portal executable ('Portal') are present
- User in 'Siemens TIA Openness' user group: required for any Openness access

Start here when a connection fails: a missing user group membership, a missing installation or an
active version that is not installed are the usual causes.

Use the Doctor tool to run the diagnosis. It is read-only: it does not connect to TIA Portal,
does not open a project and does not change user group membership.";
        }

        [McpServerPrompt(Name = "Connect"), Description("Connect to a running TIA Portal instance, or start one")]
        public static string Connect(string portalId = "")
        {
            var usage = string.IsNullOrWhiteSpace(portalId)
                ? "Use the Connect tool to initiate the connection."
                : "Use the Connect tool with this parameter:" + OptionalParameter("portalId", portalId);

            return $@"Connect to TIA Portal.

This attaches to the only running TIA Portal instance, or starts a new one when none is running. With several instances running it fails and lists them; call the GetPortals tool and pass the chosen portalId.

Common parameter values:
- portalId: process id of the TIA Portal instance (see GetPortals); leave empty when at most one instance is running

{usage}";
        }

        [McpServerPrompt(Name = "GetPortals"), Description("List the running TIA Portal instances and their portalId")]
        public static string GetPortals()
        {
            return @"List the running TIA Portal instances.

The result shows, for every instance, its portalId (process id), the open project path, the mode and whether this server is attached. The top-level portalId is the instance the other tools use when no portalId is passed.

Use the GetPortals tool to choose the portalId when more than one instance is running.";
        }

        [McpServerPrompt(Name = "OpenProject"), Description("Open a local project (.apXX) or session (.alsXX) in TIA Portal")]
        public static string OpenProject(string projectPath)
        {
            return $@"Open the TIA Portal project.

Common parameter values:
- projectPath: the full path to the project file (.ap19, .ap20, .ap21, etc.) or local session file (.als19, .als20, .als21, etc.).

Use the OpenProject tool with this parameter:
- path: {projectPath}";
        }

        [McpServerPrompt(Name = "CloseProject"), Description("Close the current project or session")]
        public static string CloseProject()
        {
            return @"Close the currently open TIA Portal project.

This will close the active project and return TIA Portal to the main screen.

Use the CloseProject tool to close the current project.";
        }

        [McpServerPrompt(Name = "Disconnect"), Description("Disconnect from the attached TIA Portal instance")]
        public static string Disconnect()
        {
            return @"Disconnect from TIA Portal.

Use the Disconnect tool to remove the connection.";
        }

        #endregion

        #region Project Information Templates

        [McpServerPrompt(Name = "GetProjectTree"), Description("Show the project structure: devices, groups and PLC/HMI software")]
        public static string GetProjectTree()
        {
            return @"Retrieve the complete structure of the current TIA Portal project.

The hierarchical tree will display:
- All devices
- Device items
- Groups
- PLC/HMI software

Use the GetProjectTree tool to display the project organization and locate software paths for other operations.";
        }

        [McpServerPrompt(Name = "GetSoftwareTree"), Description("Show the tree of a PLC software: blocks, types, tags, tables, sources")]
        public static string GetSoftwareTree(string softwarePath, string sections = "")
        {
            return $@"Retrieve the complete structure of PLC software.

The hierarchical tree will display:
- Function (OB, FB, FC) and data (ArrayDB, GlobalDB, InstanceDB) blocks (organized by groups and subgroups)
- User-defined data types (organized by groups and subgroups)
- Hierarchical organization with proper tree formatting

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- sections: optional comma separated subset of 'blocks,types,tags,watch,sources' to keep the output small; leave empty for all

Use the GetSoftwareTree tool with these parameters:
- softwarePath: {softwarePath}{OptionalParameter("sections", sections)}";
        }

        #endregion

        #region Export Templates

        [McpServerPrompt(Name = "ExportXmlBlocks"), Description("Export the blocks of a PLC software as XML files")]
        public static string ExportXmlBlocks(string softwarePath, string exportPath, string regexName = "", string preservePath = "false")
        {
            return $@"Export blocks from PLC software.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export/Program blocks' is a good default
- regexName: Use empty string """" for all blocks, or patterns like ""FB_.*"" for function blocks
- preservePath: Use false for flat export, true to maintain folder structure

Use the ExportXmlBlocks tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ExportXmlTypes"), Description("Export the PLC data types of a PLC software as XML files")]
        public static string ExportXmlTypes(string softwarePath, string exportPath, string regexName = "", string preservePath = "false")
        {
            return $@"Export user-defined types from PLC software.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export/Plc data types' is a good default
- regexName: Use empty string """" for all types, or patterns like ""Typ_.*""
- preservePath: Use false for flat export, true to maintain folder structure

Use the ExportXmlTypes tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ExportBlocksAsDocuments"), Description("Export the blocks of a PLC software as documents (.s7dcl/.s7res)")]
        public static string ExportBlocksAsDocuments(string softwarePath, string exportPath, string regexName = "", string preservePath = "false")
        {
            return $@"Export blocks as SIMATIC SD documents (.s7dcl/.s7res format) from PLC software.
Requires TIA Portal V20 or newer.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export/Plc' is a good default
- regexName: Use empty string """" for all blocks, or patterns like ""FB_.*""
- preservePath: Use false for flat export, true to maintain folder structure

Use the ExportBlocksAsDocuments tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ExportXmlTagTable"), Description("Export one PLC tag table as XML")]
        public static string ExportXmlTagTable(string softwarePath, string tagTablePath, string exportPath, string preservePath = "false")
        {
            return $@"Export a PLC tag table from PLC software to an XML file.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'. Use the GetTagTables tool to list the tag tables of a PLC software
- exportPath: '${{workspacefolder}}/export/PLC tags' is a good default
- preservePath: Use false for a flat export, true to maintain the tag table group structure

Use the ExportXmlTagTable tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- exportPath: {exportPath}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ExportSourceBlock"), Description("Export one block as a source file (.db, .scl or .awl)")]
        public static string ExportSourceBlock(string softwarePath, string blockPath, string exportPath, string withDependencies = "false", string preservePath = "false")
        {
            return $@"Export one program block from PLC software as a TIA Portal external source file, in the format the compiler reads back.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use the ResolveObjectPath tool if you only know the name
- exportPath: '${{workspacefolder}}/export/Sources' is a good default
- withDependencies: Use true to also write called blocks, instance DBs and UDTs into the same file so it compiles on its own, false for one file per object
- preservePath: Use false to write straight into exportPath, true to mirror the project groups below 'Program blocks'

The extension follows the block: '.db' for data blocks, '.scl' for SCL blocks, '.awl' for STL blocks. LAD, FBD and GRAPH blocks have no source form; use the ExportXmlBlock tool for those.

Use the ExportSourceBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- exportPath: {exportPath}
- withDependencies: {NormalizeBool(withDependencies)}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ExportSourceType"), Description("Export one PLC data type as a source file (.udt)")]
        public static string ExportSourceType(string softwarePath, string typePath, string exportPath, string withDependencies = "false", string preservePath = "false")
        {
            return $@"Export one PLC data type from PLC software as a '.udt' TIA Portal external source file, in the format the compiler reads back.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use the ResolveObjectPath tool if you only know the name
- exportPath: '${{workspacefolder}}/export/Sources' is a good default
- withDependencies: Use true to also write every data type this one uses into the same file, false for one file per type
- preservePath: Use false to write straight into exportPath, true to mirror the project groups below 'PLC data types'

Use the ExportSourceType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- exportPath: {exportPath}
- withDependencies: {NormalizeBool(withDependencies)}
- preservePath: {NormalizeBool(preservePath)}";
        }

        #endregion

        #region Convenience Export Templates

        [McpServerPrompt(Name = "ExportAllBlocksFlattened"), Description("Export all blocks of a PLC software as XML into one folder")]
        public static string ExportAllBlocksFlattened(string softwarePath, string exportPath)
        {
            return ExportXmlBlocks(softwarePath, exportPath, "", "false");
        }

        [McpServerPrompt(Name = "ExportAllBlocksStructured"), Description("Export all blocks of a PLC software as XML, keeping the group folders")]
        public static string ExportAllBlocksStructured(string softwarePath, string exportPath)
        {
            return ExportXmlBlocks(softwarePath, exportPath, "", "true");
        }

        [McpServerPrompt(Name = "ExportAllTypesFlattened"), Description("Export all PLC data types as XML into one folder")]
        public static string ExportAllTypesFlattened(string softwarePath, string exportPath)
        {
            return ExportXmlTypes(softwarePath, exportPath, "", "false");
        }

        [McpServerPrompt(Name = "ExportAllTypesStructured"), Description("Export all PLC data types as XML, keeping the group folders")]
        public static string ExportAllTypesStructured(string softwarePath, string exportPath)
        {
            return ExportXmlTypes(softwarePath, exportPath, "", "true");
        }

        [McpServerPrompt(Name = "ExportAllBlocksAsDocumentsFlattened"), Description("Export all blocks as documents (.s7dcl/.s7res) into one folder")]
        public static string ExportAllBlocksAsDocumentsFlattened(string softwarePath, string exportPath)
        {
            return ExportBlocksAsDocuments(softwarePath, exportPath, "", "false");
        }

        [McpServerPrompt(Name = "ExportAllBlocksAsDocumentsStructured"), Description("Export all blocks as documents (.s7dcl/.s7res), keeping the group folders")]
        public static string ExportAllBlocksAsDocumentsStructured(string softwarePath, string exportPath)
        {
            return ExportBlocksAsDocuments(softwarePath, exportPath, "", "true");
        }

        [McpServerPrompt(Name = "ExportTypesAsDocuments"), Description("Export PLC data types as documents (.s7dcl/.s7res, V21+)")]
        public static string ExportTypesAsDocuments(string softwarePath, string exportPath, string regexName = "", string preservePath = "false")
        {
            return $@"Export PLC data types (UDTs) as SIMATIC Source Documents instead of XML.
The documents are readable text that git can diff. Requires TIA Portal V21 or newer.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export/Plc' is a good default
- regexName: Use empty string """" for all types, or patterns like ""UDT_.*""
- preservePath: Use false for flat export, true to mirror the project tree below the 'PLC data types' folder

The response lists the files TIA Portal actually wrote, so do not assume an extension.

Use the ExportTypesAsDocuments tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ExportAllTypesAsDocumentsStructured"), Description("Export all PLC data types as documents, keeping the group folders")]
        public static string ExportAllTypesAsDocumentsStructured(string softwarePath, string exportPath)
        {
            return ExportTypesAsDocuments(softwarePath, exportPath, "", "true");
        }

        #endregion

        #region Import From Documents Templates

        [McpServerPrompt(Name = "ImportFromDocuments"), Description("Import one block from documents (.s7dcl/.s7res, V20+)")]
        public static string ImportFromDocuments(string softwarePath, string importPath, string fileNameWithoutExtension, string groupPath = "", string importOption = "Override")
        {
            return $@"Import a program block from documents into PLC software (requires TIA Portal V20 or newer).

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- groupPath: optional, e.g. 'Program blocks/FBs'
- importPath: folder containing .s7dcl/.s7res files
- fileNameWithoutExtension: e.g. 'FC_DateTime'
- importOption: 'Override' (default), 'None', 'SkipInactiveCultures', 'ActivateInactiveCultures'

Note: As of 2025-09-02, importing Ladder (LAD) blocks requires the companion .s7res to contain en-US tags for all items; otherwise import may fail.

Use the ImportFromDocuments tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}
- fileNameWithoutExtension: {fileNameWithoutExtension}
- importOption: {importOption}";
        }

        [McpServerPrompt(Name = "ImportTypesFromDocuments"), Description("Import PLC data types from documents (.s7dcl/.s7res, V21+)")]
        public static string ImportTypesFromDocuments(string softwarePath, string importPath, string groupPath = "", string regexName = "", string importOption = "Override")
        {
            return $@"Import types (UDTs) from documents into PLC software (requires TIA Portal V21 or newer and the server started with '--allow-write').

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- groupPath: optional target group, empty for the 'PLC data types' root. A leading 'PLC data types' segment from a preservePath export is accepted
- importPath: folder holding the document files
- regexName: empty for all, or e.g. 'UDT_.*'
- importOption: 'Override' (default), 'None', 'SkipInactiveCultures', 'ActivateInactiveCultures'

Use ImportTypeFromDocuments instead when you want a single named document set.

Use the ImportTypesFromDocuments tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}
- regexName: {regexName}
- importOption: {importOption}";
        }

        [McpServerPrompt(Name = "ImportBlocksFromDocuments"), Description("Import blocks from documents (.s7dcl/.s7res, V20+)")]
        public static string ImportBlocksFromDocuments(string softwarePath, string importPath, string groupPath = "", string regexName = "", string importOption = "Override")
        {
            return $@"Import multiple program blocks from SIMATIC SD documents into PLC software (requires TIA Portal V20 or newer).

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- groupPath: optional target group path, empty for root
- importPath: folder containing .s7dcl/.s7res files
- regexName: empty for all, or e.g. 'FB_.*'
- importOption: 'Override' (default), 'None', 'SkipInactiveCultures', 'ActivateInactiveCultures'

Note: As of 2025-09-02, importing Ladder (LAD) blocks requires the companion .s7res to contain en-US tags for all items; otherwise import may fail.

Use the ImportBlocksFromDocuments tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}
- regexName: {regexName}
- importOption: {importOption}";
        }

        #endregion

        #region Import From Sources Templates

        [McpServerPrompt(Name = "ImportSources"), Description("Import a folder tree of source files (.db, .scl, .awl, .udt)")]
        public static string ImportSources(string softwarePath, string importPath, string regexName = "", string keepOnError = "false")
        {
            return $@"Import every block and PLC data type source file (.db, .awl, .scl, .udt) under a folder tree into PLC software (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- importPath: folder to walk recursively, typically the tree the GenerateSources tool wrote. Each file goes into the block or PLC data type group its folder path implies; the group must already exist
- regexName: empty for all, or e.g. 'FB_.*' to select files by base name (use one file name to import a single object)
- keepOnError: Use false (default) to roll back a whole file on any error, true to keep the objects of a file that were generated successfully

Use the ImportSources tool with these parameters:
- softwarePath: {softwarePath}
- importPath: {importPath}
- regexName: {regexName}
- keepOnError: {NormalizeBool(keepOnError)}";
        }

        [McpServerPrompt(Name = "ImportSourceBlocks"), Description("Import blocks and PLC data types from source files")]
        public static string ImportSourceBlocks(string softwarePath, string sourcePath, string targetGroupPath = "", string keepOnError = "false")
        {
            return $@"Compile/import an external source that is already registered in PLC software into program blocks and PLC data types (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: e.g. 'PLC_1' for hardware PLC
- sourcePath: root-relative path of the external source, e.g. 'SourceGroup1/Source_1'. Use the GetExternalSources tool to list them
- targetGroupPath: optional block user group that receives the blocks, empty for the source's default location. The 'Program blocks' root itself is not allowed
- keepOnError: Use false (default) to generate nothing when any object fails, true to keep the objects that were generated successfully

Use the ImportSources tool instead to import a whole folder of source files in one go.

Use the ImportSourceBlocks tool with these parameters:
- softwarePath: {softwarePath}
- sourcePath: {sourcePath}
- targetGroupPath: {targetGroupPath}
- keepOnError: {NormalizeBool(keepOnError)}";
        }

        #endregion

        #region Convenience Import Templates

        [McpServerPrompt(Name = "ImportAllSources"), Description("Import all source files of a folder tree")]
        public static string ImportAllSources(string softwarePath, string importPath)
        {
            return ImportSources(softwarePath, importPath, "", "false");
        }

        [McpServerPrompt(Name = "ImportAllSourcesKeepOnError"), Description("Import all sources; keep the objects generated successfully on error")]
        public static string ImportAllSourcesKeepOnError(string softwarePath, string importPath)
        {
            return ImportSources(softwarePath, importPath, "", "true");
        }

        #endregion

        #region Project and Session Templates

        [McpServerPrompt(Name = "GetState"), Description("Show connection, open project and write mode")]
        public static string GetState()
        {
            return $@"Get the state of the TIA Portal MCP server.

Use the GetState tool (it takes no parameters).";
        }

        [McpServerPrompt(Name = "GetProject"), Description("Show the open project or session")]
        public static string GetProject()
        {
            return $@"Get open local project/session.

Use the GetProject tool (it takes no parameters).";
        }

        [McpServerPrompt(Name = "SaveProject"), Description("Save the open project or session")]
        public static string SaveProject()
        {
            return $@"Save the current TIA Portal local project/session.

Use the SaveProject tool (it takes no parameters).";
        }

        [McpServerPrompt(Name = "SaveAsProject"), Description("Save the open project under a new name")]
        public static string SaveAsProject(string newProjectPath)
        {
            return $@"Save current TIA Portal project/session with a new name.

Common parameter values:
- newProjectPath: defines the new path where to save the project

Use the SaveAsProject tool with these parameters:
- newProjectPath: {newProjectPath}";
        }

        [McpServerPrompt(Name = "OpenTiaProject"), Description("Connect and open a project in one step; returns the software paths")]
        public static string OpenTiaProject(string projectPath, string portalId = "")
        {
            return $@"Connect to TIA Portal if not already connected, open the given project or session, and return the device and PLC software paths the other tools need. Replaces the Connect then OpenProject then GetProjectTree sequence.

Common parameter values:
- projectPath: full path of the .apXX project or .alsXX session file on the machine running this server
- portalId: process id of the TIA Portal instance to open it in (see GetPortals); leave empty when at most one instance is running

Use the OpenTiaProject tool with these parameters:
- path: {projectPath}{OptionalParameter("portalId", portalId)}";
        }

        [McpServerPrompt(Name = "PreviewImport"), Description("Preview what importing a folder would create or overwrite")]
        public static string PreviewImport(string softwarePath, string importPath, string kind, string groupPath = "")
        {
            return $@"Report what importing a directory would create, overwrite or collide with, without touching the project. Checks each file against the objects already in the PLC, including the rule that a PLC data type name must be unique across the whole PLC - importing an existing type name into a different group fails even with importOption 'Override'.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- importPath: directory holding the files to import (.s7dcl source documents or .xml)
- kind: what the files contain - 'type' for PLC data types, 'block' for program blocks
- groupPath: the group the import would target; empty means the root of that area. A leading system folder segment is accepted

Use the PreviewImport tool with these parameters:
- softwarePath: {softwarePath}
- importPath: {importPath}
- kind: {kind}
- groupPath: {groupPath}";
        }

        #endregion

        #region Device Templates

        [McpServerPrompt(Name = "GetDeviceInfo"), Description("Show details of one device")]
        public static string GetDeviceInfo(string devicePath)
        {
            return $@"Get info from a device from the current project/session.

Common parameter values:
- devicePath: defines the path in the project structure to the device

Use the GetDeviceInfo tool with these parameters:
- devicePath: {devicePath}";
        }

        [McpServerPrompt(Name = "GetDeviceItemInfo"), Description("Show details of one device item")]
        public static string GetDeviceItemInfo(string deviceItemPath)
        {
            return $@"Get info from a device item from the current project/session.

Common parameter values:
- deviceItemPath: defines the path in the project structure to the device item

Use the GetDeviceItemInfo tool with these parameters:
- deviceItemPath: {deviceItemPath}";
        }

        [McpServerPrompt(Name = "GetDevices"), Description("List the devices of the project or session")]
        public static string GetDevices()
        {
            return $@"Get a list of all devices in the project/session.

Use the GetDevices tool (it takes no parameters).";
        }

        #endregion

        #region Software Templates

        [McpServerPrompt(Name = "GetSoftwareInfo"), Description("Show details of one PLC software")]
        public static string GetSoftwareInfo(string softwarePath)
        {
            return $@"Get PLC software info.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software

Use the GetSoftwareInfo tool with these parameters:
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "CompileSoftware"), Description("Compile a PLC software and list every compiler message")]
        public static string CompileSoftware(string softwarePath, string password = "")
        {
            return $@"Compile the software and report every compiler message with the object it belongs to, so errors can be fixed without re-reading the whole PLC. Warnings are reported as a successful compile with detail; only errors fail the call.

Common parameter values:
|- softwarePath: defines the path in the project structure to the software
- password: the password to access administration, default: no password

Use the CompileSoftware tool with these parameters:
- softwarePath: {softwarePath}
- password: {password}";
        }

        [McpServerPrompt(Name = "ResolveObjectPath"), Description("Turn a partial object name into the full path other tools need")]
        public static string ResolveObjectPath(string softwarePath, string name, string kind = "any")
        {
            return $@"Turn a bare or partial object name into the root-relative path the other tools need, searching program blocks, data types, tags, tag tables, watch tables and external sources. Exact matches win; substring matches are only reported when nothing matches exactly.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- name: the object name to look for, e.g. 'FC_Block_1'. A full path may be passed; only its last segment is matched
- kind: restrict the search to 'block', 'type', 'tag', 'tagTable', 'watchTable' or 'source'. Default 'any' searches all of them

Use the ResolveObjectPath tool with these parameters:
- softwarePath: {softwarePath}
- name: {name}
- kind: {kind}";
        }

        [McpServerPrompt(Name = "FindInCode"), Description("Search block and type source text with a regular expression")]
        public static string FindInCode(string softwarePath, string pattern, string nameFilter = "", string maxResults = "200")
        {
            return $@"Search the actual source text of program blocks and PLC data types with a regular expression - every other filter in this server matches object names only. Returns the object path, line number and the matching line. Each call exports the candidate objects behind the scenes, so narrow a large PLC with 'nameFilter'.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- pattern: regular expression matched against each line, case-insensitive. Plain text works too
- nameFilter: optional regular expression on object names, to limit which objects are searched. Empty (default) searches all of them
- maxResults: stop after this many matching lines (default 200)

Use the FindInCode tool with these parameters:
- softwarePath: {softwarePath}
- pattern: {pattern}
- nameFilter: {nameFilter}
- maxResults: {maxResults}";
        }

        [McpServerPrompt(Name = "GetPlcSummary"), Description("Summarize one PLC software: counts, languages and health")]
        public static string GetPlcSummary(string softwarePath)
        {
            return $@"Counts, programming languages and health of one PLC software in a single call - replaces listing blocks, types, tags, tag tables and watch tables separately just to see what is there. Also names the inconsistent objects (which refuse to export until compiled) and the know-how protected ones (whose content cannot be read).

Common parameter values:
- softwarePath: defines the path in the project structure to the software

Use the GetPlcSummary tool with these parameters:
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "GetCrossReferences"), Description("List cross references of a PLC software or one object")]
        public static string GetCrossReferences(string softwarePath, string objectPath = "", string objectKind = "auto", string filter = "AllObjects", string maxDepth = "1")
        {
            return $@"Get cross references for a PLC software or for one block, type, tag table, tag or block group inside it. Watch tables, force tables and external sources have no cross references.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- objectPath: optional root-relative path of a block, type, tag table, tag or block group; empty targets the whole PLC software
- objectKind: 'auto' (default), 'block', 'type', 'tagTable', 'tag' or 'blockGroup'
- filter: 'AllObjects' (default), 'ObjectsWithReferences', 'ObjectsWithoutReferences' or 'UnusedObjects'
- maxDepth: 1 = sources and their references (default), 2 = also source children, 3 = also reference locations. Keeps large results manageable

Use the GetCrossReferences tool with these parameters:
- softwarePath: {softwarePath}
- objectPath: {objectPath}
- objectKind: {objectKind}
- filter: {filter}
- maxDepth: {maxDepth}";
        }

        [McpServerPrompt(Name = "WhereUsed"), Description("Find where a tag, block, type or tag table is used")]
        public static string WhereUsed(string softwarePath, string name, string kind = "any")
        {
            return $@"Answer 'what uses this?' for a tag, block, PLC data type or tag table by name. Resolves the name, picks the right object kind and flattens the cross-reference tree to a plain list of users. Use 'GetCrossReferences' instead when the full nested result or a specific filter is needed.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- name: the object to look up, by bare name or by full root-relative path
- kind: restrict resolution to 'block', 'type', 'tag' or 'tagTable'. Default 'any' picks the single match, and reports the candidates when the name is ambiguous

Use the WhereUsed tool with these parameters:
- softwarePath: {softwarePath}
- name: {name}
- kind: {kind}";
        }

        #endregion

        #region Block Templates

        [McpServerPrompt(Name = "GetBlockInfo"), Description("Show details of one block")]
        public static string GetBlockInfo(string softwarePath, string blockPath)
        {
            return $@"Get a block info, which is located in the software.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- blockPath: defines the path in the project structure to the block

Use the GetBlockInfo tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}";
        }

        [McpServerPrompt(Name = "GetBlocks"), Description("List the blocks of a PLC software")]
        public static string GetBlocks(string softwarePath, string regexName = "")
        {
            return $@"Get a list of blocks, which are located in the software.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- regexName: defines the name or regular expression to find the block. Use empty string (default) to find all

Use the GetBlocks tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "GetBlocksWithHierarchy"), Description("List all blocks of a PLC software with their group hierarchy")]
        public static string GetBlocksWithHierarchy(string softwarePath)
        {
            return $@"Get a list of all blocks with their group hierarchy from the software.

Common parameter values:
- softwarePath: defines the path in the project structure to the software

Use the GetBlocksWithHierarchy tool with these parameters:
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "ExportXmlBlock"), Description("Export one block as XML")]
        public static string ExportXmlBlock(string softwarePath, string blockPath, string exportPath, string preservePath = "false")
        {
            return $@"Export a block from the software to file.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- blockPath: full path to the block in the project structure, e.g. 'Group/Subgroup/Name' (single names are ambiguous)
- exportPath: defines the path where to export the block
- preservePath: preserves the path/structure of the PLC software

Use the ExportXmlBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- exportPath: {exportPath}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ImportXmlBlock"), Description("Import one block from an XML file")]
        public static string ImportXmlBlock(string softwarePath, string groupPath, string importPath)
        {
            return $@"Import a block file to the software.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- groupPath: defines the path in the project structure to the group, where to import the block
- importPath: defines the path of the xml file from where to import the block

Use the ImportXmlBlock tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}";
        }

        [McpServerPrompt(Name = "GetBlockInterface"), Description("List the members of a data block with type and attributes")]
        public static string GetBlockInterface(string softwarePath, string blockPath)
        {
            return $@"List the members of a data block with their data type and every attribute TIA Portal reports. Needs no export and works on inconsistent blocks. Data blocks only: Openness offers no interface accessor for FB, FC or OB, whose declarations come from 'GetBlockSource' instead.

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- blockPath: root-relative path of the data block, e.g. '1_Tests/DB_Block_1'

Use the GetBlockInterface tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}";
        }

        [McpServerPrompt(Name = "CreateBlockGroup"), Description("Create a block group")]
        public static string CreateBlockGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create a group below the Program blocks root of the PLC software (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the software
- parentGroupPath: root-relative path of the parent group; empty creates directly below Program blocks
- name: name of the new group, without a slash

Use the CreateBlockGroup tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "DeleteBlockGroup"), Description("Delete a block group and everything inside it")]
        public static string DeleteBlockGroup(string softwarePath, string groupPath)
        {
            return $@"Delete a block group and everything inside it. The Program blocks system group itself cannot be deleted (requires the server started with '--allow-write').
- softwarePath: defines the path in the project structure to the software

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative path of the group to delete, e.g. Common/CarrierRegister

Use the DeleteBlockGroup tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        [McpServerPrompt(Name = "DeleteBlock"), Description("Delete a block")]
        public static string DeleteBlock(string softwarePath, string blockPath)
        {
            return $@"Delete a program block. Know-how protected blocks are rejected: remove the protection in TIA Portal first (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1

Use the DeleteBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}";
        }

        [McpServerPrompt(Name = "RenameBlock"), Description("Rename a block")]
        public static string RenameBlock(string softwarePath, string blockPath, string newName)
        {
            return $@"Rename a program block. Know-how protected blocks are rejected (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1
- newName: the new block name, without a slash

Use the RenameBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- newName: {newName}";
        }

        [McpServerPrompt(Name = "CreateFB"), Description("Create an empty function block")]
        public static string CreateFB(string softwarePath, string groupPath, string name, string language = "LAD", string autoNumber = "true", string number = "0")
        {
            return $@"Create an empty function block. Openness has no generic create-block operation: FB and instance DB are the only kinds creatable without importing XML (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative block group that receives the FB; empty uses the Program blocks root
- name: name of the new function block, without a slash
- language: programming language such as LAD (default), FBD, STL, SCL or GRAPH
- autoNumber: let TIA Portal assign the block number (default true)
- number: explicit block number, used only when autoNumber is false

Use the CreateFB tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}
- language: {language}
- autoNumber: {NormalizeBool(autoNumber)}
- number: {number}";
        }

        [McpServerPrompt(Name = "CreateInstanceDB"), Description("Create an instance data block for a function block")]
        public static string CreateInstanceDB(string softwarePath, string groupPath, string name, string instanceOfName, string autoNumber = "true", string number = "0")
        {
            return $@"Create an instance data block for an existing function block (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative block group that receives the DB; empty uses the Program blocks root
- name: name of the new instance data block, without a slash
- instanceOfName: name of the function block this instance DB belongs to
- autoNumber: let TIA Portal assign the block number (default true)
- number: explicit block number, used only when autoNumber is false

Use the CreateInstanceDB tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}
- instanceOfName: {instanceOfName}
- autoNumber: {NormalizeBool(autoNumber)}
- number: {number}";
        }

        [McpServerPrompt(Name = "CopyBlock"), Description("Copy a block into another block group")]
        public static string CopyBlock(string softwarePath, string blockPath, string targetGroupPath, string overwrite = "false")
        {
            return $@"Copy a program block into another block group of the same PLC software. Implemented as export plus import because Openness has no copy operation, so the block must be consistent and keeps its block number (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: root-relative path of the block to copy, e.g. 1_Tests/FC_Block_1
- targetGroupPath: root-relative block group that receives the copy; empty means the Program blocks root
- overwrite: replace a block of the same name already in the target group (default false)

Use the CopyBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- targetGroupPath: {targetGroupPath}
- overwrite: {NormalizeBool(overwrite)}";
        }

        [McpServerPrompt(Name = "MoveBlock"), Description("Move a block into another block group")]
        public static string MoveBlock(string softwarePath, string blockPath, string targetGroupPath, string overwrite = "false")
        {
            return $@"Move a program block into another block group of the same PLC software. Implemented as export, import and deleting the original; the original is only removed after the import succeeds (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: root-relative path of the block to move, e.g. 1_Tests/FC_Block_1
- targetGroupPath: root-relative block group that receives the block; empty means the Program blocks root
- overwrite: replace a block of the same name already in the target group (default false)

Use the MoveBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- targetGroupPath: {targetGroupPath}
- overwrite: {NormalizeBool(overwrite)}";
        }

        #endregion

        #region Type Templates

        [McpServerPrompt(Name = "GetTypeInfo"), Description("Show details of one PLC data type")]
        public static string GetTypeInfo(string softwarePath, string typePath)
        {
            return $@"Get a type info from the PLC software.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: defines the path in the project structure to the type

Use the GetTypeInfo tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}";
        }

        [McpServerPrompt(Name = "GetTypes"), Description("List the PLC data types of a PLC software")]
        public static string GetTypes(string softwarePath, string regexName = "")
        {
            return $@"Get a list of types from the PLC software.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- regexName: defines the name or regular expression to find the block. Use empty string (default) to find all

Use the GetTypes tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "ExportXmlType"), Description("Export one PLC data type as XML")]
        public static string ExportXmlType(string softwarePath, string exportPath, string typePath, string preservePath = "false")
        {
            return $@"Export a type from the PLC software.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- exportPath: defines the path where export the type
- typePath: defines the path in the project structure to the type
- preservePath: preserves the path/structure of the PLC software

Use the ExportXmlType tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- typePath: {typePath}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ImportXmlType"), Description("Import one PLC data type from an XML file")]
        public static string ImportXmlType(string softwarePath, string groupPath, string importPath)
        {
            return $@"Import a type from file into the PLC software.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: defines the path in the project structure to the group, where to import the type
- importPath: defines the path of the xml file from where to import the type

Use the ImportXmlType tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}";
        }

        [McpServerPrompt(Name = "CreateTypeGroup"), Description("Create a PLC data type group")]
        public static string CreateTypeGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create a group below the PLC data types root of the PLC software (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- parentGroupPath: root-relative path of the parent group; empty creates directly below PLC data types
- name: name of the new group, without a slash

Use the CreateTypeGroup tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "DeleteTypeGroup"), Description("Delete a PLC data type group and everything inside it")]
        public static string DeleteTypeGroup(string softwarePath, string groupPath)
        {
            return $@"Delete a PLC data type group and everything inside it. The PLC data types system group itself cannot be deleted (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative path of the group to delete

Use the DeleteTypeGroup tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        [McpServerPrompt(Name = "DeleteType"), Description("Delete a PLC data type")]
        public static string DeleteType(string softwarePath, string typePath)
        {
            return $@"Delete a PLC data type (UDT). Know-how protected types are rejected (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the type, e.g. Common/CarrierRegister/ML_SubstratState

Use the DeleteType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}";
        }

        [McpServerPrompt(Name = "RenameType"), Description("Rename a PLC data type")]
        public static string RenameType(string softwarePath, string typePath, string newName)
        {
            return $@"Rename a PLC data type (UDT). Know-how protected types are rejected (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the type
- newName: the new type name, without a slash

Use the RenameType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- newName: {newName}";
        }

        [McpServerPrompt(Name = "CopyType"), Description("Copy a PLC data type into another type group")]
        public static string CopyType(string softwarePath, string typePath, string targetGroupPath, string overwrite = "false")
        {
            return $@"Copy a PLC data type (UDT) into another type group of the same PLC software. Implemented as export plus import because Openness has no copy operation, so the type must be consistent (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the type to copy, e.g. Common/CarrierRegister/ML_SubstratState
- targetGroupPath: root-relative type group that receives the copy; empty means the PLC data types root
- overwrite: replace a type of the same name already in the target group (default false)

Use the CopyType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- targetGroupPath: {targetGroupPath}
- overwrite: {NormalizeBool(overwrite)}";
        }

        [McpServerPrompt(Name = "MoveType"), Description("Move a PLC data type into another type group")]
        public static string MoveType(string softwarePath, string typePath, string targetGroupPath, string overwrite = "false")
        {
            return $@"Move a PLC data type (UDT) into another type group of the same PLC software. Implemented as export, import and deleting the original; the original is only removed after the import succeeds (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the type to move
- targetGroupPath: root-relative type group that receives the type; empty means the PLC data types root
- overwrite: replace a type of the same name already in the target group (default false)

Use the MoveType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- targetGroupPath: {targetGroupPath}
- overwrite: {NormalizeBool(overwrite)}";
        }

        #endregion

        #region Tag and Table Templates

        [McpServerPrompt(Name = "GetTagTables"), Description("List the PLC tag tables of a PLC software")]
        public static string GetTagTables(string softwarePath, string regexName = "")
        {
            return $@"List the PLC tag tables of a PLC software, optionally filtered by a regular expression on the table name.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- regexName: optional regular expression to filter the tag table names

Use the GetTagTables tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "GetTagTableInfo"), Description("Show details of one PLC tag table")]
        public static string GetTagTableInfo(string softwarePath, string tagTablePath)
        {
            return $@"Get the details of a single tag table, including its tag and constant counts.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: root-relative path of the tag table, e.g. 'TagGroup1/Table1'

Use the GetTagTableInfo tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}";
        }

        [McpServerPrompt(Name = "GetTags"), Description("List PLC tags of one tag table or of all tag tables")]
        public static string GetTags(string softwarePath, string tagTablePath = "", string regexName = "")
        {
            return $@"List PLC tags, either of one tag table or of every tag table of the PLC software, optionally filtered by a regular expression on the tag name.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: optional root-relative tag table path; empty searches every tag table
- regexName: optional regular expression to filter the tag names

Use the GetTags tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "GetTagInfo"), Description("Show details of one PLC tag")]
        public static string GetTagInfo(string softwarePath, string tagPath)
        {
            return $@"Get the details of a single PLC tag, including data type, logical address and external access flags.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagPath: path of the tag including its table, e.g. 'TagGroup1/Table1/Tag_1'

Use the GetTagInfo tool with these parameters:
- softwarePath: {softwarePath}
- tagPath: {tagPath}";
        }

        [McpServerPrompt(Name = "GetConstants"), Description("List PLC user and system constants")]
        public static string GetConstants(string softwarePath, string tagTablePath = "", string kind = "all", string regexName = "")
        {
            return $@"List PLC user and/or system constants, either of one tag table or of every tag table of the PLC software.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: optional root-relative tag table path; empty searches every tag table
- kind: which constants to return - 'all' (default), 'user' or 'system'
- regexName: optional regular expression to filter the constant names

Use the GetConstants tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- kind: {kind}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "GetWatchTables"), Description("List the PLC watch tables of a PLC software")]
        public static string GetWatchTables(string softwarePath, string regexName = "")
        {
            return $@"List the PLC watch tables of a PLC software, optionally filtered by a regular expression on the table name. Entries are omitted; use GetWatchTableInfo for one table's rows.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- regexName: optional regular expression to filter the watch table names

Use the GetWatchTables tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "GetWatchTableInfo"), Description("Show one PLC watch table with its entries")]
        public static string GetWatchTableInfo(string softwarePath, string watchTablePath)
        {
            return $@"Get a single PLC watch table including all of its entries (address, display format, monitor and modify settings).

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'

Use the GetWatchTableInfo tool with these parameters:
- softwarePath: {softwarePath}
- watchTablePath: {watchTablePath}";
        }

        [McpServerPrompt(Name = "GetForceTables"), Description("List the PLC force tables with their entries")]
        public static string GetForceTables(string softwarePath)
        {
            return $@"List the PLC force tables of a PLC software including their entries. A force table is created and owned by the system: it cannot be created or deleted through Openness.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software

Use the GetForceTables tool with these parameters:
- softwarePath: {softwarePath}";
        }

        [McpServerPrompt(Name = "ExportXmlWatchTable"), Description("Export one PLC watch table as XML")]
        public static string ExportXmlWatchTable(string softwarePath, string watchTablePath, string exportPath, string preservePath = "false")
        {
            return $@"Export a PLC watch table to an XML file on the file system of the machine running this server. Does not modify the project.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- watchTablePath: root-relative path of the watch table, e.g. 'WatchGroup1/WatchTable_1'
- exportPath: directory on this machine that receives the XML file
- preservePath: recreate the watch table group structure below exportPath

Use the ExportXmlWatchTable tool with these parameters:
- softwarePath: {softwarePath}
- watchTablePath: {watchTablePath}
- exportPath: {exportPath}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "CreateTagTable"), Description("Create a PLC tag table")]
        public static string CreateTagTable(string softwarePath, string groupPath, string name)
        {
            return $@"Create a PLC tag table in a tag table group (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative tag table group; empty creates directly below the PLC tags root
- name: name of the new tag table, without a slash

Use the CreateTagTable tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "DeleteTagTable"), Description("Delete a PLC tag table with its tags and constants")]
        public static string DeleteTagTable(string softwarePath, string tagTablePath)
        {
            return $@"Delete a PLC tag table with all of its tags and user constants. The default tag table cannot be deleted (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1

Use the DeleteTagTable tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}";
        }

        [McpServerPrompt(Name = "RenameTagTable"), Description("Rename a PLC tag table")]
        public static string RenameTagTable(string softwarePath, string tagTablePath, string newName)
        {
            return $@"Rename a PLC tag table (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: root-relative path of the tag table
- newName: the new tag table name, without a slash

Use the RenameTagTable tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- newName: {newName}";
        }

        [McpServerPrompt(Name = "CreateTagTableGroup"), Description("Create a tag table group")]
        public static string CreateTagTableGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create a group below the PLC tags root of the PLC software (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- parentGroupPath: root-relative path of the parent group; empty creates directly below PLC tags
- name: name of the new group, without a slash

Use the CreateTagTableGroup tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "DeleteTagTableGroup"), Description("Delete a tag table group and everything inside it")]
        public static string DeleteTagTableGroup(string softwarePath, string groupPath)
        {
            return $@"Delete a tag table group and everything inside it. The PLC tags system group itself cannot be deleted (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative path of the group to delete

Use the DeleteTagTableGroup tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        [McpServerPrompt(Name = "ImportXmlTagTable"), Description("Import a PLC tag table from an XML file")]
        public static string ImportXmlTagTable(string softwarePath, string groupPath, string importPath, string overwrite = "true")
        {
            return $@"Import a PLC tag table from an XML file on the file system of the machine running this server (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative tag table group that receives the table; empty uses the PLC tags root. A leading 'PLC tags' segment, as written by preservePath exports, is accepted and ignored
- importPath: full path of the XML file to import
- overwrite: replace an existing tag table of the same name (default true)

Use the ImportXmlTagTable tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}
- overwrite: {NormalizeBool(overwrite)}";
        }

        [McpServerPrompt(Name = "CreateTag"), Description("Create a PLC tag in a tag table")]
        public static string CreateTag(string softwarePath, string tagTablePath, string name, string dataTypeName = "", string logicalAddress = "")
        {
            return $@"Create a PLC tag in a tag table (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: root-relative path of the tag table, e.g. TagGroup1/Table1
- name: name of the new tag, without a slash
- dataTypeName: PLC data type such as Bool, Int or Word; empty creates the tag with its default type
- logicalAddress: absolute address such as %I0.0, %QW4 or %M10.1

Use the CreateTag tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- name: {name}
- dataTypeName: {dataTypeName}
- logicalAddress: {logicalAddress}";
        }

        [McpServerPrompt(Name = "UpdateTag"), Description("Change properties of an existing PLC tag")]
        public static string UpdateTag(string softwarePath, string tagPath, string newName = "", string dataTypeName = "", string logicalAddress = "", string externalAccessible = "", string externalVisible = "", string externalWritable = "")
        {
            return $@"Change one or more properties of an existing PLC tag. Every argument left empty or null keeps the current value (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1
- newName: optional new tag name, without a slash
- dataTypeName: optional new PLC data type
- logicalAddress: optional new absolute address
- externalAccessible: optional new value for accessibility from HMI/OPC UA
- externalVisible: optional new value for visibility in HMI/OPC UA
- externalWritable: optional new value for writability from HMI/OPC UA

Use the UpdateTag tool with these parameters:
- softwarePath: {softwarePath}
- tagPath: {tagPath}
- newName: {newName}
- dataTypeName: {dataTypeName}
- logicalAddress: {logicalAddress}
- externalAccessible: {externalAccessible}
- externalVisible: {externalVisible}
- externalWritable: {externalWritable}";
        }

        [McpServerPrompt(Name = "DeleteTag"), Description("Delete a PLC tag")]
        public static string DeleteTag(string softwarePath, string tagPath)
        {
            return $@"Delete a PLC tag from its tag table (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagPath: path of the tag including its table, e.g. TagGroup1/Table1/Tag_1

Use the DeleteTag tool with these parameters:
- softwarePath: {softwarePath}
- tagPath: {tagPath}";
        }

        [McpServerPrompt(Name = "CreateUserConstant"), Description("Create a user constant in a tag table")]
        public static string CreateUserConstant(string softwarePath, string tagTablePath, string name, string dataTypeName = "", string value = "")
        {
            return $@"Create a user constant in a tag table. System constants are read-only and cannot be created (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: root-relative path of the tag table
- name: name of the new constant, without a slash
- dataTypeName: PLC data type such as Int or Real; empty creates the constant with its default type
- value: the constant value as text, e.g. 42 or 3.14

Use the CreateUserConstant tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- name: {name}
- dataTypeName: {dataTypeName}
- value: {value}";
        }

        [McpServerPrompt(Name = "UpdateUserConstant"), Description("Change an existing user constant")]
        public static string UpdateUserConstant(string softwarePath, string tagTablePath, string name, string newName = "", string dataTypeName = "", string value = "")
        {
            return $@"Change the name, data type or value of an existing user constant. Every argument left null keeps the current value (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: root-relative path of the tag table
- name: current name of the constant
- newName: optional new constant name, without a slash
- dataTypeName: optional new PLC data type
- value: optional new value as text

Use the UpdateUserConstant tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- name: {name}
- newName: {newName}
- dataTypeName: {dataTypeName}
- value: {value}";
        }

        [McpServerPrompt(Name = "DeleteUserConstant"), Description("Delete a user constant")]
        public static string DeleteUserConstant(string softwarePath, string tagTablePath, string name)
        {
            return $@"Delete a user constant from a tag table. System constants cannot be deleted (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- tagTablePath: root-relative path of the tag table
- name: name of the constant to delete

Use the DeleteUserConstant tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- name: {name}";
        }

        [McpServerPrompt(Name = "CreateWatchTable"), Description("Create a PLC watch table")]
        public static string CreateWatchTable(string softwarePath, string groupPath, string name)
        {
            return $@"Create watch table. Force tables cannot be created: the system owns the single force table per PLC (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative watch table group; empty creates directly below the Watch and force tables root
- name: name of the new watch table, without a slash

Use the CreateWatchTable tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "RenameWatchTable"), Description("Rename a PLC watch table")]
        public static string RenameWatchTable(string softwarePath, string watchTablePath, string newName)
        {
            return $@"Rename watch table (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- watchTablePath: root-relative path of the watch table
- newName: the new watch table name, without a slash

Use the RenameWatchTable tool with these parameters:
- softwarePath: {softwarePath}
- watchTablePath: {watchTablePath}
- newName: {newName}";
        }

        [McpServerPrompt(Name = "DeleteWatchTable"), Description("Delete a PLC watch table with its entries")]
        public static string DeleteWatchTable(string softwarePath, string watchTablePath)
        {
            return $@"Delete watch table with all of its entries. Force tables cannot be deleted (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- watchTablePath: root-relative path of the watch table

Use the DeleteWatchTable tool with these parameters:
- softwarePath: {softwarePath}
- watchTablePath: {watchTablePath}";
        }

        [McpServerPrompt(Name = "CreateWatchTableGroup"), Description("Create a watch table group")]
        public static string CreateWatchTableGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create watch table group below the Watch and force tables root of the PLC software (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- parentGroupPath: root-relative path of the parent group; empty creates directly below the root
- name: name of the new group, without a slash

Use the CreateWatchTableGroup tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "DeleteWatchTableGroup"), Description("Delete a watch table group and everything inside it")]
        public static string DeleteWatchTableGroup(string softwarePath, string groupPath)
        {
            return $@"Delete watch table group and everything inside it. The Watch and force tables system group itself cannot be deleted (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative path of the group to delete

Use the DeleteWatchTableGroup tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        [McpServerPrompt(Name = "ImportWatchTable"), Description("Import a PLC watch table from an XML file")]
        public static string ImportWatchTable(string softwarePath, string groupPath, string importPath, string overwrite = "true")
        {
            return $@"Import a PLC watch table from an XML file on the file system of the machine running this server (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative watch table group that receives the table; empty uses the root
- importPath: full path of the XML file to import
- overwrite: replace an existing watch table of the same name (default true)

Use the ImportWatchTable tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}
- overwrite: {NormalizeBool(overwrite)}";
        }

        #endregion

        #region Document Templates

        [McpServerPrompt(Name = "ExportTypeAsDocuments"), Description("Export one PLC data type as documents (.s7dcl/.s7res)")]
        public static string ExportTypeAsDocuments(string softwarePath, string typePath, string exportPath, string preservePath = "false")
        {
            return $@"Export type as a SIMATIC Source Document set (.s7dcl plus an optional .s7res) instead of XML. The response lists the files TIA Portal actually wrote. Requires TIA Portal V21 or newer.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'GetTypes' to list them
- exportPath: directory on this machine that receives the document files
- preservePath: recreate the group structure below exportPath, inside the 'PLC data types' system folder as TIA Portal names it in the current interface language

Use the ExportTypeAsDocuments tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- exportPath: {exportPath}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ImportTypeFromDocuments"), Description("Import one PLC data type from documents (.s7dcl/.s7res)")]
        public static string ImportTypeFromDocuments(string softwarePath, string groupPath, string importPath, string fileNameWithoutExtension, string importOption = "Override")
        {
            return $@"Import one type from a SIMATIC Source Document set (.s7dcl plus an optional .s7res) on the file system of the machine running this server. A type name is unique across the whole PLC, so importing an existing name into a different group fails even with importOption 'Override' - target the group the type already lives in. Requires TIA Portal V21 or newer (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative type group that receives the type; empty uses the PLC data types root. A leading 'PLC data types' segment, as written by preservePath exports, is accepted and ignored
- importPath: directory containing the document files
- fileNameWithoutExtension: base name of the document set, e.g. 'BtnTyp_X'
- importOption: ImportDocumentOptions value (None, Override, SkipInactiveCultures, ActivateInactiveCultures)

Use the ImportTypeFromDocuments tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- importPath: {importPath}
- fileNameWithoutExtension: {fileNameWithoutExtension}
- importOption: {importOption}";
        }

        [McpServerPrompt(Name = "ExportAsDocuments"), Description("Export one block as documents (.s7dcl/.s7res)")]
        public static string ExportAsDocuments(string softwarePath, string blockPath, string exportPath, string preservePath = "false")
        {
            return $@"Export as documents (.s7dcl/.s7res) from a block in the PLC software to path.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: defines the path in the project structure to the block
- exportPath: defines the path where to export the documents
- preservePath: preserves the path/structure of the PLC software

Use the ExportAsDocuments tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- exportPath: {exportPath}
- preservePath: {NormalizeBool(preservePath)}";
        }

        #endregion

        #region Source File Templates

        [McpServerPrompt(Name = "GetBlockSource"), Description("Show the source text of one block without exporting a file")]
        public static string GetBlockSource(string softwarePath, string blockPath, string format = "document", string maxChars = "40000")
        {
            return $@"Return the source text of one program block directly, instead of exporting a file and reading it back. 'document' gives readable SCL/LAD/STL (SIMATIC Source Document, V20+); objects TIA Portal cannot represent that way - STL and mixed-language blocks - fall back to XML automatically, and the response says which format was produced.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name
- format: 'document' (default) for readable source text, or 'xml' for the SimaticML export
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the GetBlockSource tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- format: {format}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "GetTypeSource"), Description("Show the source text of one PLC data type without exporting a file")]
        public static string GetTypeSource(string softwarePath, string typePath, string format = "document", string maxChars = "40000")
        {
            return $@"Return the source text of one PLC data type directly, instead of exporting a file and reading it back. 'document' gives the readable TYPE ... END_TYPE declaration (SIMATIC Source Document, requires TIA Portal V21); 'xml' gives the SimaticML export.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name
- format: 'document' (default) for readable source text, or 'xml' for the SimaticML export
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the GetTypeSource tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- format: {format}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "ExportPlcAsDocuments"), Description("Export a whole PLC software as one folder tree of documents")]
        public static string ExportPlcAsDocuments(string softwarePath, string exportPath)
        {
            return $@"Write a whole PLC software to one folder tree that mirrors the project, ready to commit: program blocks and PLC data types as readable SIMATIC Source Documents where TIA Portal supports them, tag tables and watch tables as XML, each below its localised system folder. Replaces running the four bulk exports separately. Objects that cannot be exported are reported instead of failing the snapshot.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten

Use the ExportPlcAsDocuments tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}";
        }

        [McpServerPrompt(Name = "ExportSourceBlocks"), Description("Export all blocks below a group as source files")]
        public static string ExportSourceBlocks(string softwarePath, string groupPath, string exportPath, string withDependencies = "false", string preservePath = "false")
        {
            return $@"Write every program block below a block group, including all subgroups, as TIA Portal external source files. Blocks with no source form (LAD, FBD, GRAPH), inconsistent blocks and know-how protected ones are reported in 'Skipped' instead of failing the run.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative path of the block group to export recursively, e.g. '0_OBs'. Empty means all blocks below 'Program blocks'
- exportPath: directory on this machine that receives the files; existing files of the same name are overwritten
- withDependencies: also write every object each block uses into its file. Default false
- preservePath: mirror the project groups below '<exportPath>/Program blocks'. Default false, which writes straight into exportPath

Use the ExportSourceBlocks tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- exportPath: {exportPath}
- withDependencies: {withDependencies}
- preservePath: {preservePath}";
        }

        [McpServerPrompt(Name = "ExportSourceTypes"), Description("Export all PLC data types below a group as .udt source files")]
        public static string ExportSourceTypes(string softwarePath, string groupPath, string exportPath, string withDependencies = "false", string preservePath = "false")
        {
            return $@"Write every PLC data type below a type group, including all subgroups, as '*.udt' external source files. Inconsistent and know-how protected types are reported in 'Skipped' instead of failing the run.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative path of the type group to export recursively, e.g. 'Common'. Empty means all types below 'PLC data types'
- exportPath: directory on this machine that receives the files; existing files of the same name are overwritten
- withDependencies: also write every data type each one uses into its file. Default false
- preservePath: mirror the project groups below '<exportPath>/PLC data types'. Default false, which writes straight into exportPath

Use the ExportSourceTypes tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- exportPath: {exportPath}
- withDependencies: {withDependencies}
- preservePath: {preservePath}";
        }

        [McpServerPrompt(Name = "GenerateSources"), Description("Generate source files for all blocks and PLC data types")]
        public static string GenerateSources(string softwarePath, string exportPath, string regexName = "", string withDependencies = "false")
        {
            return $@"Write every block and PLC data type of one PLC software as external source files into a folder tree that mirrors the project groups: '<exportPath>/Program blocks/...' and '<exportPath>/PLC data types/...', one file per object. The compilable counterpart to 'ExportPlcAsDocuments'. Objects with no source form (LAD, FBD, GRAPH), inconsistent objects and know-how protected ones are reported in 'Skipped' instead of failing the run.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- exportPath: directory on this machine that receives the tree; existing files of the same name are overwritten
- regexName: optional regular expression, generates only objects whose name matches. Empty means all
- withDependencies: also write every object each one uses into its file. Default false, which keeps one object per file

Use the GenerateSources tool with these parameters:
- softwarePath: {softwarePath}
- exportPath: {exportPath}
- regexName: {regexName}
- withDependencies: {NormalizeBool(withDependencies)}";
        }

        [McpServerPrompt(Name = "GetExternalSources"), Description("List the external source files of a PLC software")]
        public static string GetExternalSources(string softwarePath, string regexName = "")
        {
            return $@"List the external source files of a PLC software, optionally filtered by a regular expression on the source name.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- regexName: optional regular expression to filter the external source names

Use the GetExternalSources tool with these parameters:
- softwarePath: {softwarePath}
- regexName: {regexName}";
        }

        [McpServerPrompt(Name = "GetExternalSourceInfo"), Description("Show details of one external source file")]
        public static string GetExternalSourceInfo(string softwarePath, string sourcePath)
        {
            return $@"Get a single external source file. Beyond its name, all metadata is returned in the generic Attributes list.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- sourcePath: root-relative path of the external source, e.g. 'SourceGroup1/Source_1'

Use the GetExternalSourceInfo tool with these parameters:
- softwarePath: {softwarePath}
- sourcePath: {sourcePath}";
        }

        [McpServerPrompt(Name = "CreateExternalSourceFromFile"), Description("Add a source file to the external source files of a PLC software")]
        public static string CreateExternalSourceFromFile(string softwarePath, string groupPath, string name, string filePath)
        {
            return $@"Add a source file (for example an SCL file) from the file system into the external source files of the PLC software (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative external source group; empty uses the External source files root
- name: name the source gets in the project, without a slash
- filePath: full path of the source file on the machine running this server

Use the CreateExternalSourceFromFile tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}
- name: {name}
- filePath: {filePath}";
        }

        [McpServerPrompt(Name = "DeleteExternalSource"), Description("Delete an external source file")]
        public static string DeleteExternalSource(string softwarePath, string sourcePath)
        {
            return $@"Remove an external source file from the PLC software (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- sourcePath: root-relative path of the external source, e.g. SourceGroup1/Source_1

Use the DeleteExternalSource tool with these parameters:
- softwarePath: {softwarePath}
- sourcePath: {sourcePath}";
        }

        [McpServerPrompt(Name = "CreateExternalSourceGroup"), Description("Create an external source group")]
        public static string CreateExternalSourceGroup(string softwarePath, string parentGroupPath, string name)
        {
            return $@"Create a group below the External source files root of the PLC software (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- parentGroupPath: root-relative path of the parent group; empty creates directly below the root
- name: name of the new group, without a slash

Use the CreateExternalSourceGroup tool with these parameters:
- softwarePath: {softwarePath}
- parentGroupPath: {parentGroupPath}
- name: {name}";
        }

        [McpServerPrompt(Name = "DeleteExternalSourceGroup"), Description("Delete an external source group and everything inside it")]
        public static string DeleteExternalSourceGroup(string softwarePath, string groupPath)
        {
            return $@"Delete an external source group and everything inside it. The External source files system group itself cannot be deleted (requires the server started with '--allow-write').

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- groupPath: root-relative path of the group to delete

Use the DeleteExternalSourceGroup tool with these parameters:
- softwarePath: {softwarePath}
- groupPath: {groupPath}";
        }

        #endregion

        // Optional prompt arguments arrive as empty text when the user leaves them out; only real
        // values are passed on to the tool, so the tool's own default applies otherwise.
        private static string OptionalParameter(string name, string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : $"\n- {name}: {value!.Trim()}";
        }

        // MCP prompt arguments are always strings, so boolean flags arrive as text.
        private static string NormalizeBool(string? value)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                return "false";
            }

            return text.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                || text.Equals("1", System.StringComparison.Ordinal)
                || text.Equals("yes", System.StringComparison.OrdinalIgnoreCase)
                    ? "true"
                    : "false";
        }
    }
}

