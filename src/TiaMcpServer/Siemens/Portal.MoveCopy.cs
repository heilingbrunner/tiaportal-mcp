using Microsoft.Extensions.Logging;
using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.IO;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Copy and move for program blocks and PLC data types.
    ///
    /// Callers: the CopyBlock / MoveBlock / CopyType / MoveType tools in
    /// McpServerWrite.MoveCopy.cs, registered only under '--allow-write'. Affected API: none
    /// existing - all members are new. File I/O: each call writes one temporary .xml under the
    /// OS temp directory and removes it in a finally block; no caller-visible file is produced.
    ///
    /// Openness has no move or copy API for blocks and types: PlcBlockComposition.CreateFrom
    /// only accepts a library MasterCopy or a library type version, never another block. These
    /// operations are therefore composed from export -> import -> (for move) delete the source.
    /// Two consequences the caller sees:
    ///  - the object must be consistent, because TIA Portal refuses to export inconsistent ones;
    ///  - the exported XML carries the block number, so importing into the same PLC can hit a
    ///    number collision, which surfaces as ImportFailed rather than being pre-validated.
    /// Renaming during a copy is deliberately not offered - it would mean rewriting the XML.
    /// </summary>
    public partial class Portal
    {
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
            var source = (sourceGroupPath ?? string.Empty).Trim('/');
            var target = (targetGroupPath ?? string.Empty).Trim('/');

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
