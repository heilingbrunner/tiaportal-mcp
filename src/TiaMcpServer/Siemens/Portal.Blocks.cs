using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.Hmi;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
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
        #region blocks

        public PlcBlock? GetBlock(string softwarePath, string blockPath)
        {
            _logger?.LogInformation($"Getting block by path: {blockPath}");

            if (IsProjectNull())
            {
                return null;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var blockGroup = plcSoftware?.BlockGroup;

                if (blockGroup is not null)
                {
                    var path = blockPath.Contains("/") ? blockPath.Substring(0, blockPath.LastIndexOf("/")) : string.Empty;
                    var regexName = blockPath.Contains("/") ? blockPath.Substring(blockPath.LastIndexOf("/") + 1) : blockPath;

                    PlcBlock? block = null;

                    var group = GetPlcBlockGroupByPath(softwarePath, path);
                    if (group is not null)
                    {
                        if (regexName.IndexOfAny(_regexChars) >= 0)
                        {
                            try
                            {
                                var regex = new Regex(regexName, RegexOptions.IgnoreCase);
                                block = group.Blocks.FirstOrDefault(b => regex.IsMatch(b.Name)) as PlcBlock;
                            }
                            catch (Exception)
                            {
                                // Invalid regex, return null
                                return null;
                            }
                        }
                        else
                        {
                            block = group.Blocks.FirstOrDefault(b => b.Name.Equals(regexName, StringComparison.OrdinalIgnoreCase));
                        }

                        return block;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Root-relative path of a block, e.g. "Common/CarrierRegister/GLOBAL_POSITIONING".
        /// The system group ("Program blocks") is deliberately excluded so the result can be fed
        /// straight back into GetBlock/ExportXmlBlock as a blockPath.
        /// </summary>
        public string GetBlockPath(PlcBlock block)
        {
            if (block == null)
            {
                return string.Empty;
            }

            if (block.Parent is PlcBlockGroup parentGroup)
            {
                var groupPath = GetPlcBlockGroupPath(parentGroup, includeSystemRoot: false);
                return string.IsNullOrEmpty(groupPath) ? block.Name : $"{groupPath}/{block.Name}";
            }

            return block.Name;
        }

        public List<PlcBlock> GetBlocks(string softwarePath, string regexName = "")
        {
            _logger?.LogInformation("Getting blocks...");

            if (IsProjectNull())
            {
                return [];
            }

            var list = new List<PlcBlock>();

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    var group = plcSoftware?.BlockGroup;

                    if (group != null)
                    {
                        GetBlocksRecursive(group, list, regexName);
                    }
                }
            }
            catch (Exception)
            {
                // Console.WriteLine($"Error getting blocks: {ex.Message}");
            }

            return list;
        }

        public PlcBlockGroup? GetBlockRootGroup(string softwarePath)
        {
            _logger?.LogInformation("Getting block root group...");

            if (IsProjectNull())
            {
                return null;
            }

            try
            {
                var softwareContainer = GetSoftwareContainer(softwarePath);
                if (softwareContainer?.Software is PlcSoftware plcSoftware)
                {
                    return plcSoftware.BlockGroup;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting block root group");
            }

            return null;
        }

        public PlcBlock? ExportXmlBlock(string softwarePath, string blockPath, string exportPath, bool preservePath = false)
        {
            _logger?.LogInformation($"Exporting block by path: {blockPath}");

            try
            {
                if (IsProjectNull())
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
                }

                var block = GetBlock(softwarePath, blockPath);

                if (block == null)
                {
                    throw new PortalException(PortalErrorCode.NotFound, "Block not found");
                }

                if (preservePath)
                {
                    var groupPath = "";
                    if (block.Parent is PlcBlockGroup parentGroup)
                    {
                        groupPath = GetPlcBlockGroupPath(parentGroup);
                    }

                    exportPath = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{block.Name}.xml");
                }
                else
                {
                    exportPath = Path.Combine(exportPath, $"{block.Name}.xml");
                }

                // TIA Portal never exports inconsistent blocks
                if (!block.IsConsistent)
                {
                    throw new PortalException(PortalErrorCode.InvalidState, "Block is inconsistent; TIA Portal does not export inconsistent blocks.");
                }

                if (File.Exists(exportPath))
                {
                    File.Delete(exportPath);
                }

                block.Export(new FileInfo(exportPath), ExportOptions.None);

                return block;
            }
            catch (Exception ex)
            {
                //If the exception is already a PortalException, use it; otherwise, wrap it in a new PortalException
                var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

                pex.Data["softwarePath"] = softwarePath;
                pex.Data["blockPath"] = blockPath;
                pex.Data["exportPath"] = exportPath;

                _logger?.LogError(pex, "ExportXmlBlock failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
                throw pex;
            }
        }

        public bool ImportXmlBlock(string softwarePath, string groupPath, string importPath)
        {
            _logger?.LogInformation($"Importing block from path: {importPath}");

            if (IsProjectNull())
            {
                return false;
            }

            var softwareContainer = GetSoftwareContainer(softwarePath);
            if (softwareContainer?.Software is PlcSoftware plcSoftware)
            {
                var blockGroup = plcSoftware?.BlockGroup;

                if (blockGroup != null)
                {

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath);
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
                            var list = group.Blocks.Import(fileInfo, ImportOptions.Override);
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

            return false;
        }

        public IEnumerable<PlcBlock>? ExportXmlBlocks(string softwarePath, string exportPath, string regexName = "", bool preservePath = false)
        {
            _logger?.LogInformation("Exporting blocks...");

            if (IsProjectNull())
            {
                return null;
            }

            var exportList = new List<PlcBlock>();
            var failures = new List<string>();
            
            PlcBlock[] list;

            try
            {
                list = GetBlocks(softwarePath, regexName).ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to retrieve block list for {SoftwarePath}", softwarePath);
                return exportList;
            }

            for (int k = 0; k < list.Count(); k++)
            {
                var block = list[k];

                _logger?.LogDebug($"- Exporting block {k}/{list.Count()} : {block.Name}");

                string path;
                if (preservePath)
                {
                    var groupPath = "";
                    if (block.Parent is PlcBlockGroup parentGroup)
                    {
                        groupPath = GetPlcBlockGroupPath(parentGroup);
                    }
                    path = Path.Combine(exportPath, groupPath.Replace('/', '\\'), $"{block.Name}.xml");
                }
                else
                {
                    path = Path.Combine(exportPath, $"{block.Name}.xml");
                }

                try
                {
                    if (!block.IsConsistent)
                    {
                        _logger?.LogWarning("Skipping inconsistent block {Name}", block.Name);

                        continue;
                    }

                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    if (File.Exists(path))
                    {
                        try { File.Delete(path); }
                        catch (Exception ioEx)
                        {
                            failures.Add($"{block.Name}: cannot delete existing file ({ioEx.Message})");
                            _logger?.LogError(ioEx, "Delete failed for {File}", path);

                            continue;
                        }
                    }

                    try
                    {
                        block.Export(new FileInfo(path), ExportOptions.None);
                    }
                    catch (LicenseNotFoundException licEx)
                    {
                        failures.Add($"{block.Name}: license not found ({licEx.Message})");
                        _logger?.LogError(licEx, "License issue exporting {Block}", block.Name);

                        continue;
                    }
                    catch (EngineeringTargetInvocationException engEx)
                    {
                        failures.Add($"{block.Name}: target invocation failed ({engEx.Message})");
                        _logger?.LogError(engEx, "TargetInvocationException exporting {Block}", block.Name);

                        continue;
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{block.Name}: export failed ({ex.Message})");
                        _logger?.LogError(ex, "Export failed for {Block}", block.Name);

                        continue;
                    }

                    exportList.Add(block);
                }
                catch (Exception ex)
                {
                    // Catch only truly unexpected wrapper-level errors
                    failures.Add($"{block.Name}: unexpected exception ({ex.Message})");
                    _logger?.LogError(ex, "Unexpected error at block {Block}", block.Name);
                    // continue with next block
                }
            }

            if (failures.Count > 0)
            {
                _logger?.LogWarning($"ExportXmlBlocks completed with {failures.Count} failures out of {list.Count()}. First failure: {failures[0]}");
                // Optionally: _logger?.LogDebug("All failures: {Failures}", string.Join("; ", failures));
            }
            else
            {
                _logger?.LogInformation($"ExportXmlBlocks completed successfully. Exported {exportList.Count} blocks.");
            }

            return exportList;
        }

        #endregion

        #region get...by path

        private PlcBlockGroup? GetPlcBlockGroupByPath(string softwarePath, string groupPath)
        {
            if (_project == null)
            {
                return null;
            }

            var plcSoftware = (GetSoftwareContainer(softwarePath)?.Software as PlcSoftware);

            return plcSoftware?.BlockGroup == null
                ? null
                : WalkGroups<PlcBlockGroup>(plcSoftware.BlockGroup, groupPath, BlockSubgroups, g => g.Name);
        }

        /// <param name="includeSystemRoot">
        /// True (the default) prefixes the system group name, e.g. "Program blocks/1_Tests".
        /// That is the layout the preservePath exports write and Test_415_ImportBlock reads back.
        /// False yields "1_Tests", which round-trips into GetPlcBlockGroupByPath.
        /// </param>
        private string GetPlcBlockGroupPath(PlcBlockGroup group, bool includeSystemRoot = true)
        {
            return BuildGroupPath<PlcBlockGroup>(
                group,
                g => g.Parent as PlcBlockGroup,
                g => g.Name,
                g => g is PlcBlockSystemGroup,
                includeSystemRoot);
        }

        #endregion

        #region get recursive ...

        private bool GetBlocksRecursive(PlcBlockGroup group, List<PlcBlock> list, string regexName = "")
        {
            var before = list.Count;

            WalkRecursive<PlcBlockGroup, PlcBlock>(group, list, g => g.Blocks, BlockSubgroups, b => b.Name, regexName);

            // The previous implementation reported only the last subgroup's outcome; report
            // whether anything at all was collected.
            return list.Count > before;
        }

        #endregion

        // From the former Portal.BlockCrud.cs:
        // Create, rename and delete for program blocks, PLC data types and their groups.
        // The type half of this set (type groups, DeleteType, RenameType) lives in Portal.Types.cs.
        //
        // Callers: the corresponding tools in McpServer.Blocks.cs and McpServer.Types.cs, which are only registered when the
        // server runs with '--allow-write'. Affected API: none existing - all members are new.
        // Reads/writes no data files; these mutate the open project in memory until SaveProject.
        //
        // Openness constraints that shape this file:
        // - PlcBlockComposition has no generic Create(name) and no CreateFrom(PlcBlock), so the
        // only ways to make a block are CreateFB, CreateInstanceDB and XML import.
        // - Only user groups can be created or deleted; the system roots cannot.
        // - Name has a public setter on blocks, types and user groups, so rename is assignment.

        #region guards

        /// <summary>
        /// Know-how protected objects reject edits deep inside Openness with an opaque error;
        /// failing here gives the caller something actionable instead.
        /// </summary>
        private static void EnsureNotKnowHowProtected(PlcBlock block)
        {
            if (block.IsKnowHowProtected)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Block '{block.Name}' is know-how protected. Remove the protection in TIA Portal before modifying it.");
            }
        }

        private static void EnsureNotKnowHowProtected(PlcType type)
        {
            if (type.IsKnowHowProtected)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Type '{type.Name}' is know-how protected. Remove the protection in TIA Portal before modifying it.");
            }
        }

        private static PlcBlockUserGroup EnsureUserGroup(PlcBlockGroup? group, string groupPath)
        {
            if (group == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"Block group not found at '{groupPath}'. Use 'GetSoftwareTree' to discover valid group paths.");
            }

            return group as PlcBlockUserGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{groupPath}' is the system group 'Program blocks', which cannot be created, renamed or deleted.");
        }

        private static PlcTypeUserGroup EnsureUserGroup(PlcTypeGroup? group, string groupPath)
        {
            if (group == null)
            {
                throw new PortalException(PortalErrorCode.NotFound,
                    $"Type group not found at '{groupPath}'. Use 'GetSoftwareTree' to discover valid group paths.");
            }

            return group as PlcTypeUserGroup
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    $"'{groupPath}' is the system group 'PLC data types', which cannot be created, renamed or deleted.");
        }

        private static void EnsureValidName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new PortalException(PortalErrorCode.InvalidParams, "The name must not be empty.");
            }

            if (name.Contains("/"))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{name}' must be a plain name, not a path: '/' separates path segments.");
            }
        }

        #endregion

        #region groups

        /// <param name="parentGroupPath">Empty creates the group directly under the system root.</param>
        public PlcBlockUserGroup CreateBlockGroup(string softwarePath, string parentGroupPath, string name)
        {
            return Operation.Run(_logger, nameof(CreateBlockGroup), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    var parent = GetPlcBlockGroupByPath(softwarePath, parentGroupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{parentGroupPath}'.");

                    return parent.Groups.Create(name);
                },
                ("softwarePath", softwarePath), ("parentGroupPath", parentGroupPath), ("name", name));
        }

        public bool DeleteBlockGroup(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(DeleteBlockGroup), PortalErrorCode.DeleteFailed,
                () =>
                {
                    EnsureUserGroup(GetPlcBlockGroupByPath(softwarePath, groupPath), groupPath).Delete();
                    return true;
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        #endregion

        #region blocks (create, rename, delete)

        public bool DeleteBlock(string softwarePath, string blockPath)
        {
            return Operation.Run(_logger, nameof(DeleteBlock), PortalErrorCode.DeleteFailed,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'GetBlocks' to list the available blocks.");

                    EnsureNotKnowHowProtected(block);
                    block.Delete();

                    return true;
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        public bool RenameBlock(string softwarePath, string blockPath, string newName)
        {
            return Operation.Run(_logger, nameof(RenameBlock), PortalErrorCode.RenameFailed,
                () =>
                {
                    EnsureValidName(newName);

                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'GetBlocks' to list the available blocks.");

                    EnsureNotKnowHowProtected(block);
                    block.Name = newName;

                    return true;
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("newName", newName));
        }

        /// <summary>
        /// Creates an empty function block. Openness offers no generic "create block", so FB and
        /// instance DB are the only two kinds creatable without importing XML.
        /// </summary>
        public FB CreateFB(string softwarePath, string groupPath, string name, bool autoNumber = true, int number = 0, string language = "LAD")
        {
            return Operation.Run(_logger, nameof(CreateFB), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    if (!Enum.TryParse<ProgrammingLanguage>(language, true, out var parsedLanguage))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Unknown programming language '{language}'. Allowed values are: {string.Join(", ", Enum.GetNames(typeof(ProgrammingLanguage)))}.");
                    }

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{groupPath}'.");

                    return group.Blocks.CreateFB(name, autoNumber, number, parsedLanguage);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name), ("language", language));
        }

        /// <param name="instanceOfName">Name of the FB this instance DB is created for.</param>
        public InstanceDB CreateInstanceDB(string softwarePath, string groupPath, string name, string instanceOfName, bool autoNumber = true, int number = 0)
        {
            return Operation.Run(_logger, nameof(CreateInstanceDB), PortalErrorCode.CreateFailed,
                () =>
                {
                    EnsureValidName(name);

                    if (string.IsNullOrWhiteSpace(instanceOfName))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "instanceOfName must name the function block this instance DB belongs to.");
                    }

                    var group = GetPlcBlockGroupByPath(softwarePath, groupPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block group not found at '{groupPath}'.");

                    return group.Blocks.CreateInstanceDB(name, autoNumber, number, instanceOfName);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath), ("name", name), ("instanceOfName", instanceOfName));
        }

        #endregion

        // From the former Portal.MoveCopy.cs:
        // Copy and move for program blocks and PLC data types.
        // CopyType, MoveType and TransferType live in Portal.Types.cs; the guards and temp-directory
        // helpers below are shared by both.
        //
        // Callers: the CopyBlock / MoveBlock / CopyType / MoveType tools in
        // McpServer.Blocks.cs and McpServer.Types.cs, registered only under '--allow-write'. Affected API: none
        // existing - all members are new. File I/O: each call writes one temporary .xml under the
        // OS temp directory and removes it in a finally block; no caller-visible file is produced.
        //
        // Openness has no move or copy API for blocks and types: PlcBlockComposition.CreateFrom
        // only accepts a library MasterCopy or a library type version, never another block. These
        // operations are therefore composed from export -> import -> (for move) delete the source.
        // Two consequences the caller sees:
        // - the object must be consistent, because TIA Portal refuses to export inconsistent ones;
        // - the exported XML carries the block number, so importing into the same PLC can hit a
        // number collision, which surfaces as ImportFailed rather than being pre-validated.
        // Renaming during a copy is deliberately not offered - it would mean rewriting the XML.

        #region guards

        private static void EnsureConsistent(PlcBlock block)
        {
            if (!block.IsConsistent)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Block '{block.Name}' is inconsistent and TIA Portal will not export it. Compile the software first.");
            }
        }

        private static void EnsureConsistent(PlcType type)
        {
            if (!type.IsConsistent)
            {
                throw new PortalException(PortalErrorCode.InvalidState,
                    $"Type '{type.Name}' is inconsistent and TIA Portal will not export it. Compile the software first.");
            }
        }

        private static void EnsureDifferentGroup(string sourceGroupPath, string targetGroupPath, string itemName)
        {
            var source = NormalizeGroupPath(sourceGroupPath);
            var target = NormalizeGroupPath(targetGroupPath);

            if (source.Equals(target, StringComparison.OrdinalIgnoreCase))
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"'{itemName}' already lives in '{(target.Length == 0 ? "the root group" : target)}'. " +
                    "Choose a different target group, or use the rename tool to change its name.");
            }
        }

        #endregion

        #region temp scaffolding

        /// <summary>
        /// A private directory per call, so two concurrent transfers of same-named objects
        /// cannot collide on the intermediate file.
        /// </summary>
        private static string CreateTempExportDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "tiamcp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private void DeleteTempExportDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (Exception ex)
            {
                // A leftover temp file must never fail an otherwise successful copy or move.
                _logger?.LogWarning(ex, "Could not remove temporary export directory {Directory}", directory);
            }
        }

        #endregion

        #region blocks

        /// <param name="overwrite">Replace an object of the same name already in the target group.</param>
        public PlcBlock CopyBlock(string softwarePath, string blockPath, string targetGroupPath, bool overwrite = false)
        {
            return Operation.Run(_logger, nameof(CopyBlock), PortalErrorCode.ImportFailed,
                () => TransferBlock(softwarePath, blockPath, targetGroupPath, overwrite, false),
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("targetGroupPath", targetGroupPath));
        }

        public PlcBlock MoveBlock(string softwarePath, string blockPath, string targetGroupPath, bool overwrite = false)
        {
            return Operation.Run(_logger, nameof(MoveBlock), PortalErrorCode.ImportFailed,
                () => TransferBlock(softwarePath, blockPath, targetGroupPath, overwrite, true),
                ("softwarePath", softwarePath), ("blockPath", blockPath), ("targetGroupPath", targetGroupPath));
        }

        private PlcBlock TransferBlock(string softwarePath, string blockPath, string targetGroupPath, bool overwrite, bool deleteSource)
        {
            var source = GetBlock(softwarePath, blockPath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Block not found at '{blockPath}'. Use 'GetBlocks' to list the available blocks.");

            EnsureNotKnowHowProtected(source);
            EnsureConsistent(source);

            var sourceGroupPath = source.Parent is PlcBlockGroup sourceGroup
                ? GetPlcBlockGroupPath(sourceGroup, false)
                : string.Empty;

            EnsureDifferentGroup(sourceGroupPath, targetGroupPath, source.Name);

            var targetGroup = GetPlcBlockGroupByPath(softwarePath, targetGroupPath)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"Block group not found at '{targetGroupPath}'. Use 'GetSoftwareTree' to discover valid group paths.");

            var name = source.Name;
            var directory = CreateTempExportDirectory();

            try
            {
                var file = new FileInfo(Path.Combine(directory, name + ".xml"));

                source.Export(file, ExportOptions.None);
                targetGroup.Blocks.Import(file, overwrite ? ImportOptions.Override : ImportOptions.None);

                // Only after a successful import, so a failed move never loses the original.
                if (deleteSource)
                {
                    source.Delete();
                }
            }
            finally
            {
                DeleteTempExportDirectory(directory);
            }

            return targetGroup.Blocks.Find(name)
                ?? throw new PortalException(PortalErrorCode.ImportFailed,
                    $"Block '{name}' was imported into '{targetGroupPath}' but could not be found afterwards.");
        }

        #endregion

        #region interface

        /// <summary>
        /// The members of a data block: every entry with its attributes (data type, start value,
        /// comment, retain, accessibility - Openness exposes these as attributes rather than
        /// typed properties, so they are reported as-is and stay correct across versions).
        /// Needs no export and works on inconsistent blocks.
        ///
        /// Data blocks only: V21 Openness puts 'Interface' on DataBlock and offers no equivalent
        /// accessor on CodeBlock, so the VAR_INPUT/VAR_OUTPUT declarations of an FB, FC or OB
        /// have to be read from the declaration part of GetBlockSource instead.
        /// </summary>
        public List<InterfaceMember> GetBlockInterface(string softwarePath, string blockPath)
        {
            return Operation.Run(_logger, nameof(GetBlockInterface), PortalErrorCode.NotFound,
                () =>
                {
                    var block = GetBlock(softwarePath, blockPath)
                        ?? throw new PortalException(PortalErrorCode.NotFound,
                            $"Block not found at '{blockPath}'. Use 'ResolveObjectPath' or 'GetBlocks' to find its path.");

                    if (block is not DataBlock dataBlock)
                    {
                        throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{block.Name}' is a {block.GetType().Name}, and Openness exposes an interface only for data blocks. " +
                            "Use 'GetBlockSource' and read its declaration part instead.");
                    }

                    var members = dataBlock.Interface?.Members
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"Data block '{block.Name}' exposes no members. Know-how protected blocks hide theirs.");

                    return members.Select(ToInterfaceMember).ToList();
                },
                ("softwarePath", softwarePath), ("blockPath", blockPath));
        }

        private static InterfaceMember ToInterfaceMember(Member member)
        {
            var info = new InterfaceMember { Name = member.Name };

            foreach (var attribute in member.GetAttributeInfos())
            {
                try
                {
                    var value = member.GetAttribute(attribute.Name);

                    info.Attributes[attribute.Name] = value?.ToString() ?? string.Empty;
                }
                catch (Exception)
                {
                    // A member can advertise an attribute it cannot currently produce; skipping
                    // one attribute is better than losing the whole signature.
                }
            }

            info.DataTypeName = info.Attributes.TryGetValue("DataTypeName", out var dataType) ? dataType : null;

            return info;
        }

        #endregion
    }

    /// <summary>One member of a block interface.</summary>
    public class InterfaceMember
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Convenience copy of the DataTypeName attribute when the member has one.</summary>
        public string? DataTypeName { get; set; }

        /// <summary>Every attribute Openness reports for the member, stringified.</summary>
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }
}
