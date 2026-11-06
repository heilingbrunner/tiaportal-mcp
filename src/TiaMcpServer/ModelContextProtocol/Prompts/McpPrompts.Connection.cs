using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
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
- Write mode: whether the server was started with --allow-write
- Command line options: every option the server was started with (the args of the MCP client's JSON config),
  with its effective value or '(not set)'. Variables such as ${workspaceFolder} are already resolved by the client.

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
        public static string OpenProject(string path)
        {
            return $@"Open the TIA Portal project.

Common parameter values:
- path: the full path to the project file (.ap19, .ap20, .ap21, etc.) or local session file (.als19, .als20, .als21, etc.).

Use the OpenProject tool with this parameter:
- path: {path}";
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
        public static string OpenTiaProject(string path, string portalId = "")
        {
            return $@"Connect to TIA Portal if not already connected, open the given project or session, and return the device and PLC software paths the other tools need. Replaces the Connect then OpenProject then GetProjectTree sequence.

Common parameter values:
- path: full path of the .apXX project or .alsXX session file on the machine running this server
- portalId: process id of the TIA Portal instance to open it in (see GetPortals); leave empty when at most one instance is running

Use the OpenTiaProject tool with these parameters:
- path: {path}{OptionalParameter("portalId", portalId)}";
        }

        [McpServerPrompt(Name = "PreviewImport"), Description("Preview what importing a folder would create or overwrite")]
        public static string PreviewImport(string softwarePath, string importPath, string kind, string groupPath = "")
        {
            return $@"Report what importing a directory would create, overwrite or collide with, without touching the project. Checks each file against the objects already in the PLC, including the rule that a PLC data type name must be unique across the whole PLC - importing an existing type name into a different group fails even with importOption 'Override'.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- importPath: directory holding the files to import (.s7dcl source documents or .xml); read from <importPath>/<softwarePath>/
- kind: what the files contain - 'type' for PLC data types, 'block' for program blocks
- groupPath: the group the import would target; empty means the root of that area. A leading system folder segment is accepted

Use the PreviewImport tool with these parameters:
- softwarePath: {softwarePath}
- importPath: {importPath}
- kind: {kind}
- groupPath: {groupPath}";
        }

        #endregion
    }
}
