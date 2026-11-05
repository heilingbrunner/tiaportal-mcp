using Siemens.Engineering;
using Siemens.Engineering.Cax;
using Siemens.Engineering.HW;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>What a CAx (AutomationML) export or import of one device did.</summary>
    public sealed class HardwareTransferResult
    {
        public string DeviceName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool DryRun { get; set; }
        public string State { get; set; } = string.Empty;
        public int ErrorCount { get; set; }
        public int WarningCount { get; set; }
        public IReadOnlyList<string> Messages { get; set; } = Array.Empty<string>();
    }

    public partial class Portal
    {
        // Hardware (AutomationML, *.aml) export and import of the device that owns a PLC software.
        //
        // Callers: ExportHardware / ImportHardware and the TiaPullHardware / TiaPushHardware
        // tools in McpServer.Hardware.cs. Affected API: CaxProvider (Project.GetService),
        // CaxProvider.Export(Device, FileInfo) and CaxProvider.Import(FileInfo, CaxImportOptions).
        // File I/O: the export writes '<exportPath>/<softwarePath>/Hardware/<device name>.aml'
        // and replaces a file of that name; the import only reads that same file. The import
        // changes the project, so the tools around it are gated behind '--allow-write'.

        public const string HardwareFolder = "Hardware";
        public const string HardwareExtension = ".aml";

        /// <summary>The import options the tools offer, in the words of 'CaxImportOptions'.</summary>
        public static readonly string[] HardwareImportOptions =
            { nameof(CaxImportOptions.RetainTiaDevice), nameof(CaxImportOptions.OverwriteTiaDevice), nameof(CaxImportOptions.MoveToParkingLot) };

        /// <summary>
        /// Exports the device of a PLC software to '&lt;exportPath&gt;/&lt;softwarePath&gt;/Hardware/&lt;device&gt;.aml'.
        /// </summary>
        public HardwareTransferResult ExportHardware(string softwarePath, string exportPath)
        {
            return Operation.Run(_logger, nameof(ExportHardware), PortalErrorCode.ExportFailed,
                () =>
                {
                    var device = GetHardwareDeviceOrThrow(softwarePath);
                    var target = HardwareFile(device, softwarePath, exportPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                    if (File.Exists(target))
                    {
                        File.Delete(target);
                    }

                    var transfer = GetCaxProviderOrThrow().Export(device, new FileInfo(target));

                    return ToHardwareResult(device, target, dryRun: false, transfer, PortalErrorCode.ExportFailed, "export");
                },
                ("softwarePath", softwarePath), ("exportPath", exportPath));
        }

        /// <summary>
        /// Imports '&lt;importPath&gt;/&lt;softwarePath&gt;/Hardware/&lt;device&gt;.aml' into the open project. A dry run only
        /// checks that the file exists and changes nothing.
        /// </summary>
        public HardwareTransferResult ImportHardware(string softwarePath, string importPath, string importOption, bool dryRun)
        {
            return Operation.Run(_logger, nameof(ImportHardware), PortalErrorCode.ImportFailed,
                () =>
                {
                    var option = (CaxImportOptions)Enum.Parse(typeof(CaxImportOptions), NormalizeHardwareImportOption(importOption));
                    var device = GetHardwareDeviceOrThrow(softwarePath);
                    var source = HardwareFile(device, softwarePath, importPath);

                    if (!File.Exists(source))
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"No hardware file '{source}'. Pull the hardware first with 'ExportHardware' (tia pull hardware) or put the .aml file there.");
                    }

                    if (dryRun)
                    {
                        return new HardwareTransferResult { DeviceName = device.Name, FilePath = source, DryRun = true, State = "DryRun" };
                    }

                    var transfer = GetCaxProviderOrThrow().Import(new FileInfo(source), option);

                    return ToHardwareResult(device, source, dryRun: false, transfer, PortalErrorCode.ImportFailed, "import");
                },
                ("softwarePath", softwarePath), ("importPath", importPath), ("importOption", importOption), ("dryRun", dryRun));
        }

        /// <summary>The file a device's hardware lives in below an export or import folder.</summary>
        private static string HardwareFile(Device device, string softwarePath, string baseDirectory)
        {
            return Path.Combine(ResolveSoftwareRoot(baseDirectory, softwarePath), HardwareFolder,
                FileNames.Safe(device.Name) + HardwareExtension);
        }

        /// <summary>
        /// The name of the CaxImportOptions value a tool argument stands for (empty means
        /// RetainTiaDevice). Returns the name, not the enum, so it does not need the Openness assembly.
        /// </summary>
        public static string NormalizeHardwareImportOption(string? importOption)
        {
            if (string.IsNullOrWhiteSpace(importOption))
            {
                return HardwareImportOptions[0];
            }

            var match = HardwareImportOptions.FirstOrDefault(o => o.Equals(importOption.Trim(), StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                throw new PortalException(PortalErrorCode.InvalidParams,
                    $"Unknown import option '{importOption}'. Use one of: {string.Join(", ", HardwareImportOptions)}.");
            }

            return match;
        }

        private Device GetHardwareDeviceOrThrow(string softwarePath)
        {
            var plcSoftware = GetPlcSoftwareOrThrow(softwarePath);

            return FindDevice(GetSoftwareContainer(softwarePath), plcSoftware)
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"No device found for '{softwarePath}'. Use 'GetSoftwareTree' to list the PLC software.");
        }

        private CaxProvider GetCaxProviderOrThrow()
        {
            return _project!.GetService<CaxProvider>()
                ?? throw new PortalException(PortalErrorCode.NotSupported,
                    "Hardware (AML) export and import is not available in this TIA Portal installation or project.");
        }

        private static HardwareTransferResult ToHardwareResult(
            Device device, string file, bool dryRun, TransferResult transfer, PortalErrorCode failure, string verb)
        {
            var messages = FlattenMessages(transfer.Messages, 0).ToList();

            if (transfer.State == TransferResultState.Error)
            {
                throw new PortalException(failure,
                    $"The hardware {verb} of '{device.Name}' failed with {transfer.ErrorCount} error(s): {string.Join(" | ", messages)}");
            }

            return new HardwareTransferResult
            {
                DeviceName = device.Name,
                FilePath = file,
                DryRun = dryRun,
                State = transfer.State.ToString(),
                ErrorCount = transfer.ErrorCount,
                WarningCount = transfer.WarningCount,
                Messages = messages
            };
        }

        // Transfer messages nest (a message has its own Messages); one line per message, indented by depth.
        private static IEnumerable<string> FlattenMessages(IEnumerable<TransferResultMessage> messages, int depth)
        {
            foreach (var message in messages)
            {
                yield return $"{new string(' ', depth * 2)}{message.State}: {message.Message}";

                foreach (var child in FlattenMessages(message.Messages, depth + 1))
                {
                    yield return child;
                }
            }
        }

        // The software container and the software are children of the device item of the PLC in
        // the engineering object model; walk up until the device is found.
        private static Device? FindDevice(params object?[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var current = (candidate as IEngineeringObject)?.Parent;
                while (current != null)
                {
                    if (current is Device device)
                    {
                        return device;
                    }

                    current = (current as IEngineeringObject)?.Parent;
                }
            }

            return null;
        }
    }
}
