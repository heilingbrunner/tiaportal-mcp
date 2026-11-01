using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region blocks

        [McpServerTool(Name = "GetBlockInfo", Title = "Get block info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Show one program block: path, type, programming language, memory layout, consistency, know-how protection, modified date and attributes")]
        public static ResponseBlockInfo GetBlockInfo(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: defines the path in the project structure to the block")] string blockPath)
        {
            try
            {
                var block = Portal.GetBlock(softwarePath, blockPath);
                if (block is not null)
                {
                    var attributes = Helper.GetAttributeList(block);

                    return new ResponseBlockInfo
                    {
                        Path = Portal.GetBlockPath(block),
                        Message = $"Block info retrieved from '{blockPath}' in '{softwarePath}'",
                        Name = block.Name,
                        TypeName = block.GetType().Name,
                        Namespace = block.Namespace,
                        ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage),block.ProgrammingLanguage),
                        MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                        IsConsistent = block.IsConsistent,
                        HeaderName = block.HeaderName,
                        ModifiedDate = block.ModifiedDate,
                        IsKnowHowProtected = block.IsKnowHowProtected,
                        Attributes = attributes,
                        Description = block.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Block not found at '{blockPath}' in '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving block info from '{blockPath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetBlocks", Title = "Get blocks", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("List the program blocks of a PLC software, optionally filtered by a name or regular expression")]
        public static ResponseBlocks GetBlocks(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the block. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetBlocks(softwarePath, regexName);

                var responseList = new List<ResponseBlockInfo>();
                foreach (var block in list)
                {
                    if (block != null)
                    {
                        var attributes = Helper.GetAttributeList(block);

                        responseList.Add(new ResponseBlockInfo
                        {
                            Path = Portal.GetBlockPath(block),
                            Name = block.Name,
                            TypeName = block.GetType().Name,
                            Namespace = block.Namespace,
                            ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                            MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                            IsConsistent = block.IsConsistent,
                            HeaderName = block.HeaderName,
                            ModifiedDate = block.ModifiedDate,
                            IsKnowHowProtected = block.IsKnowHowProtected,
                            Attributes = attributes,
                            Description = block.ToString()
                        });
                    }
                }

                if (list != null)
                {
                    return new ResponseBlocks
                    {
                        Message = $"Blocks with regex '{regexName}' retrieved from '{softwarePath}'",
                        Items = responseList,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Failed retrieving blocks with regex '{regexName}' in '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving blocks with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetBlocksWithHierarchy", Title = "Get blocks with hierarchy", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Get a list of all blocks with their group hierarchy from the PLC software.")]
        public static ResponseBlocksWithHierarchy GetBlocksWithHierarchy(
        [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath)
        {
            try
            {
                var rootGroup = Portal.GetBlockRootGroup(softwarePath);
                if (rootGroup is not null)
                {
                    var hierarchy = Helper.BuildBlockHierarchy(rootGroup, Portal);
                    return new ResponseBlocksWithHierarchy
                    {
                        Message = $"Block hierarchy retrieved from '{softwarePath}'",
                        Root = hierarchy,
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    // Specific failure: root group could not be resolved
                    throw new McpException($"Block root group not found for '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                // Generic unexpected failure wrapper
                throw new McpException($"Unexpected error retrieving block hierarchy for '{softwarePath}': {ex.Message}", ex);
            }
        }

        #endregion

        #region source

        [McpServerTool(Name = "GetBlockInterface", Title = "Get block interface", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("List the members of a data block with their data type and every attribute TIA Portal reports. Needs no export and works on inconsistent blocks. Data blocks only: Openness offers no interface accessor for FB, FC or OB, whose declarations come from 'GetSourceBlock' or 'GetDocumentsBlock' instead")]
        public static ResponseBlockInterface GetBlockInterface(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: root-relative path of the data block, e.g. '1_Tests/DB_Block_1'")] string blockPath)
        {
            try
            {
                var members = Portal.GetBlockInterface(softwarePath, blockPath);

                return new ResponseBlockInterface
                {
                    Message = $"'{blockPath}' has {members.Count} member(s)",
                    Path = blockPath,
                    Items = members.Select(m => new ResponseInterfaceMember
                    {
                        Name = m.Name,
                        DataTypeName = m.DataTypeName,
                        Attributes = m.Attributes
                    }).ToList(),
                    Meta = Ok(new JsonObject { ["memberCount"] = members.Count })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error reading the interface of '{blockPath}': {ex.Message}", ex);
            }
        }

        #endregion

        // From the former McpServerWrite.Blocks.cs:
        // Write tools for program blocks, PLC data types and their groups.
        //
        // Callers: registered by Program.BuildTools() under '--allow-write'; also invoked
        // directly by the write test class. Affected API: additive - a partial of McpServer; the tools are marked [WriteTool].
        // No file I/O; changes are in memory until SaveProject / SaveSession.
        //
        // Openness offers no generic "create block" and no move/copy, so the creation surface here
        // is CreateFB and CreateInstanceDB only - anything else arrives through ImportPlcProgramChanges or OverwritePlcProgram.

        // From the former McpServerWrite.MoveCopy.cs:
        // Write tools that copy or move program blocks and PLC data types between groups.
        //
        // Callers: registered by Program.BuildTools() under '--allow-write'; also invoked
        // directly by the write test class. Affected API: additive - a partial of McpServer; the tools are marked [WriteTool].
        // File I/O: none of its own; Portal writes and removes a temporary XML per call.
        //
        // Openness exposes no move or copy operation for these objects, so each tool is composed
        // from export, import and (for a move) deleting the source. The tool descriptions say so,
        // because the composition is observable: the object must be consistent, and its block
        // number travels with it, so importing into the same PLC can collide.

        #region groups (write)

        [WriteTool]
        [McpServerTool(Name = "CreateBlockGroup", Title = "Create block group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the Program blocks root of the PLC software")]
        public static ResponseCreated CreateBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below Program blocks")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateBlockGroup), () =>
            {
                Portal.CreateBlockGroup(softwarePath, parentGroupPath, name);
                return Created("Block group", name, JoinPath(parentGroupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteBlockGroup", Title = "Delete block group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a block group and everything inside it. The Program blocks system group itself cannot be deleted")]
        public static ResponseDeleted DeleteBlockGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete, e.g. Common/CarrierRegister")] string groupPath)
        {
            return Guarded(nameof(DeleteBlockGroup), () =>
            {
                Portal.DeleteBlockGroup(softwarePath, groupPath);
                return Deleted("Block group", groupPath);
            });
        }

        #endregion

        #region blocks and types (write)

        [WriteTool]
        [McpServerTool(Name = "DeleteBlock", Title = "Delete block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a program block. Know-how protected blocks are rejected: remove the protection in TIA Portal first")]
        public static ResponseDeleted DeleteBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1")] string blockPath)
        {
            return Guarded(nameof(DeleteBlock), () =>
            {
                Portal.DeleteBlock(softwarePath, blockPath);
                return Deleted("Block", blockPath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "RenameBlock", Title = "Rename block", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a program block. Know-how protected blocks are rejected")]
        public static ResponseRenamed RenameBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: root-relative path of the block, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("newName: the new block name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameBlock), () =>
            {
                Portal.RenameBlock(softwarePath, blockPath, newName);
                return Renamed("Block", blockPath, newName, ReplaceLeaf(blockPath, newName));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "CreateFB", Title = "Create function block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an empty function block. Openness has no generic create-block operation: FB and instance DB are the only kinds creatable without importing XML")]
        public static ResponseCreated CreateFB(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative block group that receives the FB; empty uses the Program blocks root")] string groupPath,
            [Description("name: name of the new function block, without a slash")] string name,
            [Description("language: programming language such as LAD (default), FBD, STL, SCL or GRAPH")] string language = "LAD",
            [Description("autoNumber: let TIA Portal assign the block number (default true)")] bool autoNumber = true,
            [Description("number: explicit block number, used only when autoNumber is false")] int number = 0)
        {
            return Guarded(nameof(CreateFB), () =>
            {
                var block = Portal.CreateFB(softwarePath, groupPath, name, autoNumber, number, language);
                return Created("FB", block.Name, JoinPath(groupPath, block.Name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "CreateInstanceDB", Title = "Create instance data block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create an instance data block for an existing function block")]
        public static ResponseCreated CreateInstanceDB(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative block group that receives the DB; empty uses the Program blocks root")] string groupPath,
            [Description("name: name of the new instance data block, without a slash")] string name,
            [Description("instanceOfName: name of the function block this instance DB belongs to")] string instanceOfName,
            [Description("autoNumber: let TIA Portal assign the block number (default true)")] bool autoNumber = true,
            [Description("number: explicit block number, used only when autoNumber is false")] int number = 0)
        {
            return Guarded(nameof(CreateInstanceDB), () =>
            {
                var block = Portal.CreateInstanceDB(softwarePath, groupPath, name, instanceOfName, autoNumber, number);
                return Created("InstanceDB", block.Name, JoinPath(groupPath, block.Name));
            });
        }

        #endregion

        #region move copy (write)

        [WriteTool]
        [McpServerTool(Name = "CopyBlock", Title = "Copy block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Copy a program block into another block group of the same PLC software. Implemented as export plus import because Openness has no copy operation, so the block must be consistent and keeps its block number")]
        public static ResponseCreated CopyBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: root-relative path of the block to copy, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("targetGroupPath: root-relative block group that receives the copy; empty means the Program blocks root")] string targetGroupPath,
            [Description("overwrite: replace a block of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(CopyBlock), () =>
            {
                var block = Portal.CopyBlock(softwarePath, blockPath, targetGroupPath, overwrite);
                var newPath = JoinPath(targetGroupPath, block.Name);

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

        [WriteTool]
        [McpServerTool(Name = "MoveBlock", Title = "Move block", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Move a program block into another block group of the same PLC software. Implemented as export, import and deleting the original; the original is only removed after the import succeeds")]
        public static ResponseRenamed MoveBlock(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("blockPath: root-relative path of the block to move, e.g. 1_Tests/FC_Block_1")] string blockPath,
            [Description("targetGroupPath: root-relative block group that receives the block; empty means the Program blocks root")] string targetGroupPath,
            [Description("overwrite: replace a block of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(MoveBlock), () =>
            {
                var block = Portal.MoveBlock(softwarePath, blockPath, targetGroupPath, overwrite);
                var newPath = JoinPath(targetGroupPath, block.Name);

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

        #endregion
    }
}
