using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;
using SdkMcpServer = global::ModelContextProtocol.Server.McpServer;

namespace TiaMcpServer.ModelContextProtocol
{
    public static partial class McpServer
    {
        // Hardware (AutomationML, *.aml) export and import of the device that owns a PLC software.
        //
        // Callers: registered through Program.BuildTools() and invoked directly by the tests;
        // TiaPullHardware and TiaPushHardware in McpServer.Porcelain.cs delegate here.
        // Data: returns ResponseHardwareTransfer as MCP structuredContent. ExportHardware writes
        // one .aml file below exportPath and does not modify the project, so it is not a
        // [WriteTool]. ImportHardware changes the project and is only registered, and only runs,
        // with '--allow-write'. A real import asks the user to confirm in a dialog of the client.

        #region hardware

        [McpServerTool(Name = "ExportHardware", Title = "Export hardware as AML", Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to export the hardware configuration of the device that owns a PLC software (CPU, modules, network settings) as an AutomationML file '<exportPath>/<softwarePath>/Hardware/<device name>.aml'. An existing file of that name is overwritten. It does not change the project; 'ImportHardware' reads the file back. HMI devices (except push button and key panels) and drives are not supported by TIA Portal. 'Messages' holds the messages TIA Portal gave, 'State' the result (Success, Information or Warning; an error fails the call)")]
        public static ResponseHardwareTransfer ExportHardware(
            [Description("softwarePath: defines the path in the project structure to the PLC software; its device is exported")] string softwarePath,
            [Description("exportPath: directory on this machine that receives the file; it is written to <exportPath>/<softwarePath>/Hardware/")] string exportPath)
        {
            return HardwareCall(() => HardwareResponse(Portal.ExportHardware(softwarePath, exportPath), "exported"));
        }

        [WriteTool]
        [McpServerTool(Name = "ImportHardware", Title = "Import hardware from AML", Destructive = true, OpenWorld = false, UseStructuredContent = true),
         Description("Use this to import the AutomationML file '<importPath>/<softwarePath>/Hardware/<device name>.aml' that 'ExportHardware' wrote back into the open TIA Portal project, so the hardware configuration of the device changes. 'importOption' decides what happens to a device that exists: RetainTiaDevice (default) keeps it, OverwriteTiaDevice replaces it, MoveToParkingLot moves it to the parking lot. By default 'dryRun' is true and only checks the file. With dryRun=false the server asks the user itself to confirm by ticking the 'Yes, import' checkbox in a dialog of the client and imports nothing without it, so do not answer for the user; a client without such dialogs cannot run a real import. Never save on your own: after a real import tell the user the change is only in memory, and call SaveProject (SaveSession in a local session) only after the user explicitly says 'save project'")]
        public static Task<ResponseHardwareTransfer> ImportHardware(
            SdkMcpServer server,
            [Description("softwarePath: defines the path in the project structure to the PLC software; its device receives the import")] string softwarePath,
            [Description("importPath: directory on this machine that holds the file 'ExportHardware' wrote; read from <importPath>/<softwarePath>/Hardware/")] string importPath,
            [Description("importOption: RetainTiaDevice (default), OverwriteTiaDevice or MoveToParkingLot; what happens to a device that already exists")] string importOption = "RetainTiaDevice",
            [Description("dryRun: default true, only checks the file and changes nothing. Set false to import: the server then asks the user to tick 'Yes, import' in a dialog, so do not ask in the chat first")] bool dryRun = true,
            CancellationToken cancellationToken = default)
        {
            return ImportHardwareAsync(server, nameof(ImportHardware), softwarePath, importPath, importOption, dryRun, cancellationToken);
        }

        private static async Task<ResponseHardwareTransfer> ImportHardwareAsync(
            SdkMcpServer server, string toolName, string softwarePath, string importPath, string importOption, bool dryRun,
            CancellationToken cancellationToken)
        {
            WritePolicy.EnsureEnabled(toolName);

            if (dryRun)
            {
                return HardwareCall(() => HardwareResponse(Portal.ImportHardware(softwarePath, importPath, importOption, dryRun: true), "checked"));
            }

            if (server?.ClientCapabilities?.Elicitation == null)
            {
                throw new McpException(
                    $"'{toolName}' with dryRun=false is refused: this client cannot show the confirmation dialog in which the user ticks '{ImportConfirmation}' and submits (MCP elicitation). " +
                    "Use dryRun=true to check the file, and run the import from a client that supports confirmation dialogs.");
            }

            var preview = HardwareCall(() => Portal.ImportHardware(softwarePath, importPath, importOption, dryRun: true));

            if (!await ConfirmHardwareImportAsync(server, softwarePath, preview, importOption, cancellationToken).ConfigureAwait(false))
            {
                var declined = HardwareResponse(preview, "checked");

                declined.Message = $"Nothing was imported: the user did not tick '{ImportConfirmation}' and submit. Do not ask again unless the user asks for the import.";

                return declined;
            }

            // No MCP transaction (Guarded): TIA Portal refuses a CAx import inside exclusive access.
            return HardwareCall(() => HardwareResponse(Portal.ImportHardware(softwarePath, importPath, importOption, dryRun: false), "imported"));
        }

        private static async Task<bool> ConfirmHardwareImportAsync(
            SdkMcpServer server, string softwarePath, HardwareTransferResult preview, string importOption, CancellationToken cancellationToken)
        {
            var option = string.IsNullOrWhiteSpace(importOption) ? Portal.HardwareImportOptions[0] : importOption.Trim();

            var result = await server.ElicitAsync(new ElicitRequestParams
            {
                Message =
                    $"Import the hardware file '{preview.FilePath}' into the device '{preview.DeviceName}' of '{softwarePath}' in the open TIA Portal project? " +
                    $"Import option: {option}.",
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>
                    {
                        [ConfirmationField] = new ElicitRequestParams.UntitledMultiSelectEnumSchema
                        {
                            Title = "Confirmation",
                            MinItems = 1,
                            Items = new ElicitRequestParams.UntitledEnumItemsSchema
                            {
                                Enum = new List<string> { ConfirmationValue }
                            }
                        }
                    },
                    Required = new List<string> { ConfirmationField }
                }
            }, cancellationToken).ConfigureAwait(false);

            return IsImportAccepted(result);
        }

        private static ResponseHardwareTransfer HardwareResponse(HardwareTransferResult result, string verb)
        {
            var warnings = result.WarningCount > 0 ? $" with {result.WarningCount} warning(s)" : string.Empty;

            return new ResponseHardwareTransfer
            {
                Message = result.DryRun
                    ? $"DRY RUN, nothing was imported: the hardware file '{result.FilePath}' of '{result.DeviceName}' exists and would be imported."
                    : $"Hardware of '{result.DeviceName}' {verb} ('{result.FilePath}'){warnings}.",
                Name = result.DeviceName,
                Path = result.FilePath,
                DryRun = result.DryRun,
                State = result.State,
                ErrorCount = result.ErrorCount,
                WarningCount = result.WarningCount,
                Messages = result.Messages.ToList(),
                Meta = Ok(new JsonObject { ["dryRun"] = result.DryRun })
            };
        }

        private static T HardwareCall<T>(Func<T> body)
        {
            try
            {
                return body();
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error in the hardware transfer: {ex.Message}", ex);
            }
        }

        #endregion
    }
}
