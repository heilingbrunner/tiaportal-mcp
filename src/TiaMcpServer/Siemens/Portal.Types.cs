using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks.Interface;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        #region types

        public PlcType? GetType(string softwarePath, string typePath)
        {
            _logger?.LogInformation($"Getting type by path: {typePath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var typeGroup = plcSoftware?.TypeGroup;

                if (typeGroup != null)
                {
                    var path = typePath.Contains("/") ? typePath.Substring(0, typePath.LastIndexOf("/")) : string.Empty;
                    var regexName = typePath.Contains("/") ? typePath.Substring(typePath.LastIndexOf("/") + 1) : typePath;

                    PlcType? type = null;

                    var group = GetPlcTypeGroupByPath(softwarePath, path);
                    if (group is not null)
                    {
                        if (regexName.IndexOfAny(_regexChars) >= 0)
                        {
                            try
                            {
                                var regex = new Regex(regexName, RegexOptions.IgnoreCase);
                                type = group.Types.FirstOrDefault(t => regex.IsMatch(t.Name)) as PlcType;
                            }
                            catch (Exception)
                            {
                                // Invalid regex, return null
                                return null;
                            }
                        }
                        else
                        {
                            type = group.Types.FirstOrDefault(t => t.Name.Equals(regexName, StringComparison.OrdinalIgnoreCase));
                        }

                        return type;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Root-relative path of a PLC data type, the GetBlockPath counterpart.
        /// </summary>
        public string GetTypePath(PlcType type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.Parent is PlcTypeGroup parentGroup)
            {
                var groupPath = GetPlcTypeGroupPath(parentGroup, includeSystemRoot: false);
                return string.IsNullOrEmpty(groupPath) ? type.Name : $"{groupPath}/{type.Name}";
            }

            return type.Name;
        }

        public List<PlcType> GetTypes(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting types...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<PlcType>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.TypeGroup;

                    if (group != null)
                    {
                        GetTypesRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error getting user defined types: {ex.Message}");
            }

            return list;
        }

        public PlcType? ExportXmlType(string softwarePath, string typePath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting type by path: {typePath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                var type = GetType(softwarePath, typePath);

                if (type == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Type not found");
                }

                // TIA Portal never exports inconsistent types
                if (!type.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Type is inconsistent; TIA Portal does not export inconsistent types.");
                }

                if (preservePath)
                {
                    var groupPath = "";
                    if (type.Parent is PlcTypeGroup parentGroup)
                    {
                        groupPath = GetPlcTypeGroupPath(parentGroup);
                    }

                    exportPath = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{type.Name}.xml");
                }
                else
                {
                    exportPath = Path.Combine(exportPath, $"{type.Name}.xml");
                }

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                type.Export(new FileInfo(exportPath), ExportOptions.None);

                return type;
            }
            catch (Exception ex)
            {
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                if (!pex.Data.Contains("softwarePath")) pex.Data["softwarePath"] = softwarePath;
                if (!pex.Data.Contains("typePath")) pex.Data["typePath"] = typePath;
                if (!pex.Data.Contains("exportPath")) pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportXmlType failed for {SoftwarePath} {TypePath} -> {ExportPath}", softwarePath, typePath, exportPath);
                throw pex;
            }
        }

        public bool ImportXmlType(string softwarePath, string groupPath, string importPath)
        {
            _logger?.LogInformation($"Importing type from path: {importPath}");

            var success = false;

            if (IsProjectNull())
            {
                return success;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var typeGroup = plcSoftware?.TypeGroup;

                if (typeGroup != null)
                {
                    var group = GetPlcTypeGroupByPath(softwarePath, groupPath);
                    if (group == null)
                    {
                        return false;
                    }

                    try
                    {
                        // Correct the argument type by using FileInfo instead of FileStream  
                        var fileInfo = new FileInfo(importPath);
                        if (fileInfo.Exists)
                        {
                            var list = group.Types.Import(fileInfo, ImportOptions.Override);
                            if (list != null && list.Count > 0)
                            {
                                return true;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }

            return success;
        }

        public IEnumerable<PlcType>? ExportXmlTypes(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting types...");

            if (IsProjectNull())
            {
                return null;
            }

            var exportList = new List<PlcType>();
            var failures = new List<string>();

            PlcType[] list;

            try
            {
                list = GetTypes(softwarePath, regexName).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve type list for {SoftwarePath}", softwarePath);
                return exportList;
            }

            for (int i = 0; i < list.Count(); i++)
            {
                var type = list[i];

                _logger?.LogDebug("- Exporting type {Index}/{Total} : {Name}", i, list.Count(), type.Name);

                string path;
                if (preservePath)
                {
                    var groupPath = "";
                    if (type.Parent is PlcTypeGroup parentGroup)
                    {
                        groupPath = GetPlcTypeGroupPath(parentGroup);
                    }
                    path = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{type.Name}.xml");
                }
                else
                {
                    path = Path.Combine(exportPath, $"{type.Name}.xml");
                }

                try
                {
                    if (!type.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent type {Name}", type.Name);
                        continue;
                    }

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(path))
                    {
                        try
                        {
                            File.Delete(path);
                        }
                        catch (Exception ioEx)
                        {
                            failures.Add($"{type.Name}: cannot delete existing file ({ioEx.Message})");
                            _logger?.LogError(ioEx, "Delete failed for {File}", path);
                            continue;
                        }
                    }

                    try
                    {
                        type.Export(new FileInfo(path), ExportOptions.None);
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{type.Name}: export failed ({ex.Message})");
                        _logger?.LogError(ex, "Export failed for type {Type}", type.Name);
                        continue;
                    }

                    exportList.Add(type);
                }
                catch (Exception ex)
                {
                    failures.Add($"{type.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, "Unexpected error at type {Type}", type.Name);
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportXmlTypes completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
            }
            else
            {
                _logger?.LogInformation($"ExportXmlTypes completed successfully. Exported {exportList.Count} types.");
            }

            return exportList;
        }

        #endregion

        #region get...by path

        private PlcTypeGroup? GetPlcTypeGroupByPath(string softwarePath, string groupPath)
        {
            if (_project == null)
            {
                return null;
            }

            var plcSoftware = (GetSoftwareContainer(softwarePath)?.Software as PlcSoftware);

            return plcSoftware?.TypeGroup == null
                ? null
                : WalkGroups<PlcTypeGroup>(plcSoftware.TypeGroup, groupPath, TypeSubgroups, g => g.Name);
        }

        /// <param name="includeSystemRoot">See GetPlcBlockGroupPath.</param>
        private string GetPlcTypeGroupPath(PlcTypeGroup group, bool includeSystemRoot = true)
        {
            return BuildGroupPath<PlcTypeGroup>(
                group,
                g => g.Parent as PlcTypeGroup,
                g => g.Name,
                g => g is PlcTypeSystemGroup,
                includeSystemRoot);
        }

        #endregion

        #region get recursive ...

        private bool GetTypesRecursive(PlcTypeGroup group, List<PlcType> list, string regexName = "")
        {
            var before = list.Count;

            WalkRecursive<PlcTypeGroup, PlcType>(group, list, g => g.Types, TypeSubgroups, t => t.Name, regexName);

            return list.Count > before;
        }

        #endregion

        #region groups

        public PlcTypeUserGroup CreateTypeGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateTypeGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetPlcTypeGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteTypeGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteTypeGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    EnsureUserGroup(GetPlcTypeGroupByPath(softwarePath, groupPath), groupPath).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        #endregion

        #region types (delete, rename)

        public bool DeleteType(string softwarePath, string typePath)
        {
            return Operation.Run(_logger, nameof(DeleteType), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type not found at '{typePath}'. Use 'GetTypes' to list the available types.");

                    EnsureNotKnowHowProtected(type);
                    type.Delete();

                    return true;
                },
                ("softwarePath", softwarePath), ("typePath", typePath));
        }

        public bool RenameType(string softwarePath, string typePath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameType), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);

                    var type = GetType(softwarePath, typePath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Type not found at '{typePath}'. Use 'GetTypes' to list the available types.");

                    EnsureNotKnowHowProtected(type);
                    type.Name = newName;

                    return true;
                },
                ("softwarePath", softwarePath), ("typePath", typePath), ("newName", newName));
        }

        #endregion

        #region types

        public PlcType CopyType(string softwarePath, string typePath, string targetGroupPath, bool overwrite = false)
        {
            return Operation.Run(_logger, nameof(CopyType), PortalErrorCode.ImportFailed,
                () => TransferType(softwarePath, typePath, targetGroupPath, overwrite, false),
                ("softwarePath", softwarePath), ("typePath", typePath), ("targetGroupPath", targetGroupPath));
        }

        public PlcType MoveType(string softwarePath, string typePath, string targetGroupPath, bool overwrite = false)
        {
            return Operation.Run(_logger, nameof(MoveType), PortalErrorCode.ImportFailed,
                () => TransferType(softwarePath, typePath, targetGroupPath, overwrite, true),
                ("softwarePath", softwarePath), ("typePath", typePath), ("targetGroupPath", targetGroupPath));
        }

        private PlcType TransferType(string softwarePath, string typePath, string targetGroupPath, bool overwrite, bool deleteSource)
        {
            var source = GetType(softwarePath, typePath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Type not found at '{typePath}'. Use 'GetTypes' to list the available types.");

            EnsureNotKnowHowProtected(source);
            EnsureConsistent(source);

            var sourceGroupPath = source.Parent is PlcTypeGroup sourceGroup
                ? GetPlcTypeGroupPath(sourceGroup, false)
                : string.Empty;

            EnsureDifferentGroup(sourceGroupPath, targetGroupPath, source.Name);

            var targetGroup = GetPlcTypeGroupByPath(softwarePath, targetGroupPath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Type group not found at '{targetGroupPath}'. Use 'GetSoftwareTree' to discover valid group paths.");

            var name = source.Name;
            var directory = CreateTempExportDirectory();

            try
            {
                var file = new FileInfo(Path.Combine(directory, name + ".xml"));

                source.Export(file, ExportOptions.None);
                targetGroup.Types.Import(file, overwrite ? ImportOptions.Override : ImportOptions.None);

                if (deleteSource)
                {
                    source.Delete();
                }
            }
            finally
            {
                DeleteTempExportDirectory(directory);
            }

            return targetGroup.Types.Find(name)
                ?? throw new PortalException(PortalErrorCode.ImportFailed,
                    $"Type '{name}' was imported into '{targetGroupPath}' but could not be found afterwards.");
        }

        #endregion
    }
}
