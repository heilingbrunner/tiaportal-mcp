using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        #region types

        [McpServerTool(Name = "GetTypeInfo", Title = "Get type info", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("Show one PLC data type: path, namespace, consistency, know-how protection, modified date and attributes")]
        public static ResponseTypeInfo GetTypeInfo(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("typePath: defines the path in the project structure to the type")] string typePath)
        {
            try
            {
                var type = Portal.GetType(softwarePath, typePath);
                if (type is not null)
                {
                    var attributes = Helper.GetAttributeList(type);

                    return new ResponseTypeInfo
                    {
                        Path = Portal.GetTypePath(type),
                        Message = $"Type info retrieved from '{typePath}' in '{softwarePath}'",
                        Name = type.Name,
                        TypeName = type.GetType().Name,
                        Namespace = type.Namespace,
                        IsConsistent = type.IsConsistent,
                        ModifiedDate = type.ModifiedDate,
                        IsKnowHowProtected = type.IsKnowHowProtected,
                        Attributes = attributes,
                        Description = type.ToString(),
                        Meta = new JsonObject
                        {
                            ["timestamp"] = DateTime.Now,
                            ["success"] = true
                        }
                    };
                }
                else
                {
                    throw new McpException($"Type not found at '{typePath}' in '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving type info from '{typePath}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        [McpServerTool(Name = "GetTypes", Title = "Get types", ReadOnly = true, OpenWorld = false, UseStructuredContent = true), Description("List the PLC data types of a PLC software, optionally filtered by a name or regular expression")]
        public static ResponseTypes GetTypes(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("regexName: defines the name or regular expression to find the type. Use empty string (default) to find all")] string regexName = "")
        {
            try
            {
                var list = Portal.GetTypes(softwarePath, regexName);

                var responseList = new List<ResponseTypeInfo>();
                foreach (var type in list)
                {
                    if (type != null)
                    {
                        var attributes = Helper.GetAttributeList(type);

                        responseList.Add(new ResponseTypeInfo
                        {
                            Path = Portal.GetTypePath(type),
                            Name = type.Name,
                            TypeName = type.GetType().Name,
                            Namespace = type.Namespace,
                            IsConsistent = type.IsConsistent,
                            ModifiedDate = type.ModifiedDate,
                            IsKnowHowProtected = type.IsKnowHowProtected,
                            Attributes = attributes,
                            Description = type.ToString()
                        });
                    }
                }

                if (list != null)
                {
                    return new ResponseTypes
                    {
                        Message = $"Types with regex '{regexName}' retrieved from '{softwarePath}'",
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
                    throw new McpException($"Failed retrieving user defined types with regex '{regexName}' in '{softwarePath}'");
                }
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error retrieving user defined types with regex '{regexName}' in '{softwarePath}': {ex.Message}", ex);
            }
        }

        #endregion

        // From the former McpServerWrite.Types.cs:

        #region groups (write)

        [WriteTool]
        [McpServerTool(Name = "CreateTypeGroup", Title = "Create type group", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Create a group below the PLC data types root of the PLC software")]
        public static ResponseCreated CreateTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("parentGroupPath: root-relative path of the parent group; empty creates directly below PLC data types")] string parentGroupPath,
            [Description("name: name of the new group, without a slash")] string name)
        {
            return Guarded(nameof(CreateTypeGroup), () =>
            {
                Portal.CreateTypeGroup(softwarePath, parentGroupPath, name);
                return Created("Type group", name, JoinPath(parentGroupPath, name));
            });
        }

        [WriteTool]
        [McpServerTool(Name = "DeleteTypeGroup", Title = "Delete type group", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC data type group and everything inside it. The PLC data types system group itself cannot be deleted")]
        public static ResponseDeleted DeleteTypeGroup(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("groupPath: root-relative path of the group to delete")] string groupPath)
        {
            return Guarded(nameof(DeleteTypeGroup), () =>
            {
                Portal.DeleteTypeGroup(softwarePath, groupPath);
                return Deleted("Type group", groupPath);
            });
        }

        #endregion

        #region blocks and types (write)

        [WriteTool]
        [McpServerTool(Name = "DeleteType", Title = "Delete type", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Delete a PLC data type (UDT). Know-how protected types are rejected")]
        public static ResponseDeleted DeleteType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("typePath: root-relative path of the type, e.g. Common/CarrierRegister/ML_SubstratState")] string typePath)
        {
            return Guarded(nameof(DeleteType), () =>
            {
                Portal.DeleteType(softwarePath, typePath);
                return Deleted("Type", typePath);
            });
        }

        [WriteTool]
        [McpServerTool(Name = "RenameType", Title = "Rename type", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Rename a PLC data type (UDT). Know-how protected types are rejected")]
        public static ResponseRenamed RenameType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("typePath: root-relative path of the type")] string typePath,
            [Description("newName: the new type name, without a slash")] string newName)
        {
            return Guarded(nameof(RenameType), () =>
            {
                Portal.RenameType(softwarePath, typePath, newName);
                return Renamed("Type", typePath, newName, ReplaceLeaf(typePath, newName));
            });
        }

        #endregion

        #region move copy (write)

        [WriteTool]
        [McpServerTool(Name = "CopyType", Title = "Copy type", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Copy a PLC data type (UDT) into another type group of the same PLC software. Implemented as export plus import because Openness has no copy operation, so the type must be consistent")]
        public static ResponseCreated CopyType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("typePath: root-relative path of the type to copy, e.g. Common/CarrierRegister/ML_SubstratState")] string typePath,
            [Description("targetGroupPath: root-relative type group that receives the copy; empty means the PLC data types root")] string targetGroupPath,
            [Description("overwrite: replace a type of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(CopyType), () =>
            {
                var type = Portal.CopyType(softwarePath, typePath, targetGroupPath, overwrite);
                var newPath = JoinPath(targetGroupPath, type.Name);

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

        [WriteTool]
        [McpServerTool(Name = "MoveType", Title = "Move type", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Move a PLC data type (UDT) into another type group of the same PLC software. Implemented as export, import and deleting the original; the original is only removed after the import succeeds")]
        public static ResponseRenamed MoveType(
            [Description("softwarePath: defines the path in the project structure to the PLC software")] string softwarePath,
            [Description("typePath: root-relative path of the type to move")] string typePath,
            [Description("targetGroupPath: root-relative type group that receives the type; empty means the PLC data types root")] string targetGroupPath,
            [Description("overwrite: replace a type of the same name already in the target group (default false)")] bool overwrite = false)
        {
            return Guarded(nameof(MoveType), () =>
            {
                var type = Portal.MoveType(softwarePath, typePath, targetGroupPath, overwrite);
                var newPath = JoinPath(targetGroupPath, type.Name);

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

        #endregion
    }
}
