using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpPrompts
    {
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

        [McpServerPrompt(Name = "GetBlockInterface"), Description("List the members of a data block with type and attributes")]
        public static string GetBlockInterface(string softwarePath, string blockPath)
        {
            return $@"List the members of a data block with their data type and every attribute TIA Portal reports. Needs no export and works on inconsistent blocks. Data blocks only: Openness offers no interface accessor for FB, FC or OB, whose declarations come from 'GetSourceBlock' or 'GetDocumentsBlock' instead.

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
    }
}
