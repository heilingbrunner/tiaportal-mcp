using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
    {
        #region Export Templates

        [McpServerPrompt(Name = "ExportXmlBlocks"), Description("Export the blocks of a PLC software as XML files")]
        public static string ExportXmlBlocks(string softwarePath, string exportPath, string regexName = "", string preservePath = "false")
        {
            return $@"Export blocks from PLC software.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- exportPath: '${{workspacefolder}}/export' is a good default; files are written below <exportPath>/<softwarePath>/
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
- exportPath: '${{workspacefolder}}/export' is a good default; files are written below <exportPath>/<softwarePath>/
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
- exportPath: '${{workspacefolder}}/export' is a good default; files are written below <exportPath>/<softwarePath>/
- preservePath: Use false for a flat export, true to maintain the tag table group structure

Use the ExportXmlTagTable tool with these parameters:
- softwarePath: {softwarePath}
- tagTablePath: {tagTablePath}
- exportPath: {exportPath}
- preservePath: {NormalizeBool(preservePath)}";
        }

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

        [McpServerPrompt(Name = "ExportSourceBlock"), Description("Export one block as a source file (.db, .scl or .awl)")]
        public static string ExportSourceBlock(string softwarePath, string blockPath, string exportPath, string withDependencies = "false", string preservePath = "false")
        {
            return $@"Export one program block from PLC software as a TIA Portal external source file, in the format the compiler reads back.

Common parameter values:
- softwarePath: normally something like 'PLC_1' for hardware PLC, 'PC-System_1/Software PLC_1' for PC based PLC; in a slash command write a value with spaces in double quotes, e.g. ""PC-System_1/Software PLC_1""
- blockPath: root-relative path of the block, e.g. '0_OBs/Main'. Use the ResolveObjectPath tool if you only know the name
- exportPath: '${{workspacefolder}}/export/Sources' is a good default
- withDependencies: Use true to also write called blocks, instance DBs and UDTs into the same file so it compiles on its own, false for one file per object
- preservePath: Use false to write straight into <exportPath>/<softwarePath>, true to mirror the project groups below 'Program blocks'

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
- preservePath: Use false to write straight into <exportPath>/<softwarePath>, true to mirror the project groups below 'PLC data types'

Use the ExportSourceType tool with these parameters:
- softwarePath: {softwarePath}
- typePath: {typePath}
- exportPath: {exportPath}
- withDependencies: {NormalizeBool(withDependencies)}
- preservePath: {NormalizeBool(preservePath)}";
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

        #endregion
    }
}
