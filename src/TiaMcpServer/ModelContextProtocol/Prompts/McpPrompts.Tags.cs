using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
    {
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
- preservePath: recreate the watch table group structure below <exportPath>/<softwarePath>, inside the watch and force tables system folder as TIA Portal names it in the current interface language (the file always lands in that folder)

Use the ExportXmlWatchTable tool with these parameters:
- softwarePath: {softwarePath}
- watchTablePath: {watchTablePath}
- exportPath: {exportPath}
- preservePath: {NormalizeBool(preservePath)}";
        }

        [McpServerPrompt(Name = "ExportXmlForceTable"), Description("Export the PLC force table as XML")]
        public static string ExportXmlForceTable(string softwarePath, string forceTablePath, string exportPath)
        {
            return $@"Export the PLC force table to an XML file on the file system of the machine running this server. Does not modify the project. A PLC has one force table, owned by the system.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- forceTablePath: path of the force table as GetForceTables lists it
- exportPath: directory on this machine that receives the XML file; the file lands in the watch and force tables system folder below <exportPath>/<softwarePath>, as TIA Portal names it in the current interface language, next to the watch tables

Use the ExportXmlForceTable tool with these parameters:
- softwarePath: {softwarePath}
- forceTablePath: {forceTablePath}
- exportPath: {exportPath}";
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
    }
}
