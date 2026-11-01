using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
    {
        #region Document Templates

        #endregion

        #region Source File Templates

        [McpServerPrompt(Name = "GetXmlBlock"), Description("Show one block as SimaticML XML without exporting a file")]
        public static string GetXmlBlock(string softwarePath, string blockPath, string maxChars = "40000")
        {
            return $@"Return one program block as SimaticML XML text directly, instead of exporting a file and reading it back. Works for every consistent block, whatever its language.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the GetXmlBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "GetSourceBlock"), Description("Show one block as external source text (.scl/.db/.awl) without exporting a file")]
        public static string GetSourceBlock(string softwarePath, string blockPath, string withDependencies = "false", string maxChars = "40000")
        {
            return $@"Return one program block as TIA Portal external source text ('.scl' for SCL, '.db' for data blocks, '.awl' for STL) directly, in the format the compiler reads back. LAD, FBD and GRAPH blocks have no source form and are rejected - use GetXmlBlock or GetDocumentsBlock for those.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name
- withDependencies: also include every object the block uses, so the text compiles on its own. Default false
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the GetSourceBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- withDependencies: {NormalizeBool(withDependencies)}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "GetDocumentsBlock"), Description("Show one block as SIMATIC Source Document text without exporting a file")]
        public static string GetDocumentsBlock(string softwarePath, string blockPath, string maxChars = "40000")
        {
            return $@"Return one program block as readable SIMATIC Source Document text (.s7dcl/.s7res, TIA Portal V20+) directly. STL and mixed-language blocks have no source document and are rejected - use GetXmlBlock or GetSourceBlock for those.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use 'ResolveObjectPath' if you only know the name
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the GetDocumentsBlock tool with these parameters:
- softwarePath: {softwarePath}
- blockPath: {blockPath}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "GetXmlType"), Description("Show one PLC data type as SimaticML XML without exporting a file")]
        public static string GetXmlType(string softwarePath, string typePath, string maxChars = "40000")
        {
            return $@"Return one PLC data type as SimaticML XML text directly, instead of exporting a file and reading it back.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the GetXmlType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "GetSourceType"), Description("Show one PLC data type as external source text (.udt) without exporting a file")]
        public static string GetSourceType(string softwarePath, string typePath, string withDependencies = "false", string maxChars = "40000")
        {
            return $@"Return one PLC data type as TIA Portal external source text ('.udt', the TYPE ... END_TYPE declaration) directly, in the format the compiler reads back. Needs no TIA Portal V21.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name
- withDependencies: also include every data type this one uses. Default false
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the GetSourceType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- withDependencies: {NormalizeBool(withDependencies)}
- maxChars: {maxChars}";
        }

        [McpServerPrompt(Name = "GetDocumentsType"), Description("Show one PLC data type as SIMATIC Source Document text without exporting a file")]
        public static string GetDocumentsType(string softwarePath, string typePath, string maxChars = "40000")
        {
            return $@"Return one PLC data type as readable SIMATIC Source Document text (.s7dcl/.s7res) directly. Requires TIA Portal V21; on older versions use GetSourceType or GetXmlType.

Common parameter values:
- softwarePath: defines the path in the project structure to the PLC software
- typePath: root-relative path of the PLC data type, e.g. 'Common/BtnTyp_X'. Use 'ResolveObjectPath' if you only know the name
- maxChars: truncate the text at this many characters, on a line boundary (default 40000)

Use the GetDocumentsType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- maxChars: {maxChars}";
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
    }
}
