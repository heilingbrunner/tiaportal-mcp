using ModelContextProtocol.Server;
using System.ComponentModel;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Write tools that copy or move program blocks and PLC data types between groups.
    ///
    /// Callers: registered by Program.BuildToolTypes() under '--allow-write'; also invoked
    /// directly by the write test class. Affected API: additive - a partial of McpServerWrite.
    /// File I/O: none of its own; Portal writes and removes a temporary XML per call.
    ///
    /// Openness exposes no move or copy operation for these objects, so each tool is composed
    /// from export, import and (for a move) deleting the source. The tool descriptions say so,
    /// because the composition is observable: the object must be consistent, and its block
    /// number travels with it, so importing into the same PLC can collide.
    /// </summary>
    public static partial class McpServerWrite
    {
        [McpServerTool(Name = "CopyBlock", Title = "Copy a block to another group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Copy a program block into another block group of the same plc software. Implemented as export plus import because Openness has no copy operation, so the block must be consistent and keeps its block number")]
        public static ResponseCreated CopyBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block to copy, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("targetGroupPath: root-relative block group that receives the copy; empty means the Program blocks root")] string targetGroupPath,
            [Description("overwrite: replace a block of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(CopyBlock), () =>
            {
                var block = Portal.CopyBlock(softwarePath, blockPath, targetGroupPath, overwrite);
                var newPath = Join(targetGroupPath, block.Name);

                return new ResponseCreated
                {
                    Kind = "Block",
                    Name = block.Name,
                    Path = newPath,
                    Message = $"Block '{blockPath}' copied to '{newPath}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [McpServerTool(Name = "MoveBlock", Title = "Move a block to another group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Move a program block into another block group of the same plc software. Implemented as export, import and deleting the original; the original is only removed after the import succeeds")]
        public static ResponseRenamed MoveBlock(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("blockPath: root-relative path of the block to move, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("targetGroupPath: root-relative block group that receives the block; empty means the Program blocks root")] string targetGroupPath,
            [Description("overwrite: replace a block of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(MoveBlock), () =>
            {
                var block = Portal.MoveBlock(softwarePath, blockPath, targetGroupPath, overwrite);
                var newPath = Join(targetGroupPath, block.Name);

                return new ResponseRenamed
                {
                    Kind = "Block",
                    OldPath = blockPath,
                    NewName = block.Name,
                    NewPath = newPath,
                    Message = $"Block '{blockPath}' moved to '{newPath}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [McpServerTool(Name = "CopyType", Title = "Copy a PLC data type to another group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Copy a PLC data type (UDT) into another type group of the same plc software. Implemented as export plus import because Openness has no copy operation, so the type must be consistent")]
        public static ResponseCreated CopyType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the type to copy, e.g. Common/CarrierRegister/ML_SubstratState")] string typePath,
            [Description("targetGroupPath: root-relative type group that receives the copy; empty means the PLC data types root")] string targetGroupPath,
            [Description("overwrite: replace a type of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(CopyType), () =>
            {
                var type = Portal.CopyType(softwarePath, typePath, targetGroupPath, overwrite);
                var newPath = Join(targetGroupPath, type.Name);

                return new ResponseCreated
                {
                    Kind = "Type",
                    Name = type.Name,
                    Path = newPath,
                    Message = $"Type '{typePath}' copied to '{newPath}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }

        [McpServerTool(Name = "MoveType", Title = "Move a PLC data type to another group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Move a PLC data type (UDT) into another type group of the same plc software. Implemented as export, import and deleting the original; the original is only removed after the import succeeds")]
        public static ResponseRenamed MoveType(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("typePath: root-relative path of the type to move")] string typePath,
            [Description("targetGroupPath: root-relative type group that receives the type; empty means the PLC data types root")] string targetGroupPath,
            [Description("overwrite: replace a type of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(MoveType), () =>
            {
                var type = Portal.MoveType(softwarePath, typePath, targetGroupPath, overwrite);
                var newPath = Join(targetGroupPath, type.Name);

                return new ResponseRenamed
                {
                    Kind = "Type",
                    OldPath = typePath,
                    NewName = type.Name,
                    NewPath = newPath,
                    Message = $"Type '{typePath}' moved to '{newPath}'. {SaveHint}",
                    Meta = OkMeta()
                };
            });
        }
    }
}
