# TiaMcpServer

This document provides a comprehensive overview of the TiaMcpServer project, a C# application that acts as a Model Context Protocol (MCP) server to expose the Siemens TIA Portal API to Large Language Models (LLMs).

## 1. Project Overview

The TiaMcpServer project is a .NET 4.8 console application that enables communication between an LLM and the Siemens TIA Portal. It achieves this by implementing an MCP server that exposes a set of tools for interacting with the TIA Portal. The project is divided into two main parts:

*   **MCP Server:** This part of the project is responsible for handling communication with the LLM. It uses the `ModelContextProtocol` library to create an MCP server that listens for requests from the LLM and executes the corresponding tools.
*   **TIA Portal Interfacing API:** This part of the project is responsible for interacting with the TIA Portal. It uses the Siemens TIA Portal Openness API to perform tasks such as connecting to the TIA Portal, opening and closing projects, and working with devices, blocks, and types.

## 2. Project Structure

The project is organized into the following directories:

Both `Portal` and `McpServer` are `partial` classes split by functional area, so no single file
carries the whole surface.

*   **`ModelContextProtocol/`**: This directory contains the implementation of the MCP server.
    *   `McpServer.cs` and `McpServer.{Tags,WatchTables,ExternalSources,CrossReferences,Source,GenerateSource}.cs`: the read-only tools, as partials of one `McpServer` type. `McpServer.GenerateSource.cs` holds the three external-source generators; they write files but never touch the project, so they stay on this side of the `--allow-write` gate.
    *   `McpServerWrite.cs` and `McpServerWrite.{Blocks,Tags,Tables,MoveCopy}.cs`: the project-mutating tools, a separate tool type registered only under `--allow-write`. `McpServerWrite.cs` itself holds no tools, only the shared `Guarded` wrapper and response builders.
    *   `WritePolicy.cs`: the `--allow-write` gate.
    *   `McpPrompts.cs`: This file contains the prompts that are used to guide the LLM.
    *   `Responses.cs` / `Responses.Write.cs`: the response objects returned by the tools. The write side shares `ResponseCreated`, `ResponseDeleted`, `ResponseRenamed`, `ResponseImported` and `ResponseGenerateBlocks` across all 37 tools rather than minting one DTO per operation.
    *   `Types.cs`: This file defines the data types that are used by the MCP server.
    *   `Helper.cs`: `GetAttributeList(IEngineeringObject)` reflects over any Openness object's attributes, so each new `Get*Info` tool is a few typed fields plus that call.
*   **`Siemens/`**: This directory contains the implementation of the TIA Portal interfacing API.
    *   `Portal.cs`: connection, project and session lifecycle, devices, blocks and types.
    *   `Portal.Resolve.cs`: the generic path helpers (`WalkGroups`, `BuildGroupPath`, `WalkRecursive`). `PlcSoftware` exposes five look-alike group hierarchies - blocks, types, tag tables, watch and force tables, external sources - that share no common base type, so the shape is captured with generics plus selector delegates instead of inheritance.
    *   `Portal.Tree.cs`: the software tree sections.
    *   `Portal.{Tags,WatchTables,ExternalSources,CrossReferences}.cs`: the read side per area.
    *   `Portal.GenerateSource.cs`: `PlcExternalSourceSystemGroup.GenerateSource`, which writes the `.scl`/`.db`/`.awl`/`.udt` files TIA Portal can compile back into blocks. Separate from `Portal.Source.cs`, which reads text and writes source documents and SimaticML.
    *   `Portal.{BlockCrud,Write,MoveCopy}.cs`: the write side.
    *   `Operation.cs`: the single exception-decoration point (see `docs/error-model.md`), which also serializes all Openness traffic behind a reentrant lock.
    *   `State.cs`: This file defines the `State` class, which represents the state of the TIA Portal.
    *   `Openness.cs`: This file provides a wrapper around the Siemens TIA Portal Openness API.
    *   `Diagnostics.cs`: the environment report behind `--doctor` and the `Doctor` tool.

## 3. Architecture

The TiaMcpServer project follows a client-server architecture. The LLM acts as the client, and the TiaMcpServer application acts as the server. The communication between the client and the server is handled by the MCP protocol.

The MCP server is responsible for receiving requests from the LLM, executing the corresponding tools, and returning the results. The tools are implemented as methods in the `McpServer` class. These methods use the TIA Portal interfacing API to interact with the TIA Portal.

The TIA Portal interfacing API is implemented in the `Siemens` directory. This API provides a set of classes and methods for performing common tasks, such as connecting to the TIA Portal, opening and closing projects, and working with devices, blocks, and types.

## 4. Functionality

The TiaMcpServer project provides the following functionality:

*   **Connecting and disconnecting from the TIA Portal:** The `Connect` and `Disconnect` tools allow the LLM to connect to and disconnect from the TIA Portal.
*   **Getting the state of the TIA Portal:** The `GetState` tool allows the LLM to get the current state of the TIA Portal, such as whether it is connected to a project and the name of the project.
*   **Working with projects and sessions:** The `GetProject`, `OpenProject`, `SaveProject`, `SaveAsProject`, and `CloseProject` tools allow the LLM to work with TIA Portal projects and sessions.
*   **Working with devices:** The `GetProjectTree`, `GetDeviceInfo`, `GetDeviceItemInfo`, and `GetDevices` tools allow the LLM to get information about the devices in a project.
*   **Working with PLC software:** The `GetSoftwareInfo` and `CompileSoftware` tools allow the LLM to get information about and compile PLC software. `GetSoftwareTree` renders the whole PLC software - program blocks, PLC data types, PLC tags, watch and force tables and external source files - and takes a `sections` argument to narrow the output.
*   **Working with blocks:** The `GetBlockInfo`, `GetBlocks`, `GetBlocksWithHierarchy`, `ExportBlock`, `ImportBlock`, and `ExportBlocks` tools allow the LLM to work with blocks.
    - `ExportBlock` expects `blockPath` to be a fully qualified path like `Group/Subgroup/Name`. Passing just a name is ambiguous; the tool fails with an error result and may suggest likely full paths based on project contents.
*   **Working with types:** The `GetTypeInfo`, `GetTypes`, `ExportType`, `ImportType`, and `ExportTypes` tools allow the LLM to work with types.
*   **Exporting blocks as documents (V20+):** The `ExportAsDocuments` and `ExportBlocksAsDocuments` tools export blocks as SIMATIC SD documents (.s7dcl/.s7res). Requires TIA Portal V20 or newer.
*   **Importing blocks from documents (V20+):** The `ImportFromDocuments` and `ImportBlocksFromDocuments` tools import blocks from SIMATIC SD documents into PLC software. Requires TIA Portal V20 or newer.
*   **PLC data types as documents (V21+):** `ExportTypeAsDocuments` and `ExportTypesAsDocuments` write a type as a SIMATIC Source Document set instead of XML; `ImportTypeFromDocuments` and `ImportTypesFromDocuments` read one back (both `--allow-write` only). Openness only offers `PlcType.ExportAsDocuments` from V21 on. The export response lists the files TIA Portal actually wrote rather than assuming a `.s7dcl` name.
*   **Working with PLC tags and constants:** `GetTagTables`, `GetTagTableInfo`, `GetTags`, `GetTagInfo`, `GetConstants` and `ExportTagTable`.
*   **Working with watch and force tables:** `GetWatchTables`, `GetWatchTableInfo` (including entries), `GetForceTables` and `ExportWatchTable`.
*   **Working with external source files:** `GetExternalSources` and `GetExternalSourceInfo`.
*   **Cross references:** `GetCrossReferences` for a PLC software or a single block, type, tag table, tag or block group, with `maxDepth` to bound the result size.
*   **Modifying the project (`--allow-write` only):** 37 tools that create, rename, delete, import, copy and move blocks, types, groups, tag tables, tags, user constants, watch tables and external sources. See the root `README.md` section "Write mode" for the gating rules and the API limits that shape them.

## 5. Conclusion

The TiaMcpServer project is a powerful tool that allows LLMs to interact with the Siemens TIA Portal. The project is well-structured and easy to understand. The code is well-commented and follows best practices.

## 6. Future Improvements

*   **Session Path Reliability:** The `GetOpenSessions` method has been updated to return the full path of the session project. However, the TIA Portal Openness API's behavior with multiuser sessions can vary. Future testing should confirm the reliability of retrieving the `Path` for all types of local and remote sessions to ensure the information is always accurate.

## Known Issues

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.

## Transports

- Current transport: `stdio`
  - The server is hosted with `AddMcpServer().WithStdioServerTransport()`.
  - For stdio, all logs must go to stderr.
- Streams transport: available in SDK (not wired here)
  - The SDK also exposes `WithStreamServerTransport(Stream input, Stream output)` which can be used to host over TCP or other custom streams.
- HTTP (planned)
  - This repo does not yet include an HTTP or SSE transport. The plan is to add a CLI flag `--transport http` and host a loopback `HttpListener` that forwards POST `/mcp` to the MCP request handler, then iterate towards MCP Streamable HTTP compliance.

## Error Handling Standard (ExportBlock)

- Portal layer
  - Throws `PortalException` with a short message and `PortalErrorCode`.
  - Attaches context via `Exception.Data` keys: `softwarePath`, `blockPath`, `exportPath`.
  - Preserves the original exception as `InnerException` for `ExportFailed` and logs full details.
- MCP layer
  - Rethrows as `McpException`. Since SDK 2.x an `McpException` thrown from a tool becomes a `CallToolResult` with `isError: true` carrying the message as text, instead of a JSON-RPC error, so the model can read the reason and retry.
  - For `NotFound`, if `blockPath` is a single name, it suggests likely full paths by scanning blocks.
  - For `ExportFailed`, includes a concise reason from `InnerException.Message`.
  - Consistency: TIA Portal does not export inconsistent blocks/types. Single-item exports fail with a message advising to compile first. Bulk exports skip inconsistent items and include them in an `Inconsistent` list in the response.
  - Keeps user messages concise; structured details live in logs and context.
  - Current standardization is applied to `ExportBlock` and will be rolled out to other methods incrementally.
  - Exception metadata: Context keys (e.g., `softwarePath`, `blockPath`/`typePath`, `exportPath`) are attached in a single catch per portal method just before rethrow, not at inline throw sites. See `docs/error-model.md`.

## Contributing

- See root `AGENTS.md` for agent guidance and the test execution policy (offer to run tests only with explicit user confirmation).

## Comfort Functions

Tools added to shorten the path between a question and an answer. All are read-only and none
change an existing tool's contract; `CompileSoftware` was enriched in place rather than
duplicated. Every one is verified against a live TIA Portal V21.

| Tool                    | What it does                                                                                                        | Replaces                                             | Limits worth knowing                                                                                                                                                                                                                                  |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GetBlockSource`        | Returns a block's source text inline                                                                                | Export a file, then open it                          | Openness has no in-memory block body, so it exports to a temp scratch directory and removes it. STL and mixed-language blocks have no SIMATIC Source Document and fall back to XML, reported in `Format`. Truncates at `maxChars` on a line boundary. |
| `GetTypeSource`         | Returns a PLC data type's `TYPE ... END_TYPE` text inline                                                           | Export a file, then open it                          | Source documents for types need V21; `format: "xml"` works on older versions.                                                                                                                                                                         |
| `GetBlockInterface`     | Lists a data block's members with data type and every attribute                                                     | Guessing, or exporting the whole block               | Data blocks only - V21 exposes `Interface` on `DataBlock` and offers no equivalent for FB, FC or OB, whose declarations come from `GetBlockSource`. Needs no export, so it works on inconsistent blocks.                                              |
| `FindInCode`            | Regex search over the actual program text                                                                           | Name-only regex filters                              | Exports each candidate per call; narrow a large PLC with `nameFilter`. Objects that cannot be read are listed in `Unsearchable`.                                                                                                                      |
| `CompileSoftware`       | Now returns the whole compiler message tree flattened to `{path, state, description}` plus error and warning counts | A single stringified sentence                        | Warnings are a successful compile with detail; only errors fail the call.                                                                                                                                                                             |
| `GetPlcSummary`         | Counts per area, a programming-language histogram, and the inconsistent and know-how-protected objects              | Six separate discovery calls                         | Built from the existing collectors, so the figures always agree with the individual tools.                                                                                                                                                            |
| `WhereUsed`             | Answers "what uses this?" from a bare name                                                                          | `GetCrossReferences` plus manual tree walking        | Blocks, types, tags and tag tables only - watch tables and external sources have no cross references. Reports the candidates when a name is ambiguous.                                                                                                |
| `ResolveObjectPath`     | Turns a bare or partial name into the root-relative path the other tools need                                       | Listing a whole area and post-processing it          | Searches blocks, types, tags, tag tables, watch tables and sources. Exact matches win; substring matches appear only when nothing matches exactly.                                                                                                    |
| `OpenTiaProject`        | Connects if needed, opens the project, and returns the device and PLC software paths                                | `Connect` then `OpenProject` then `GetProjectTree`   | Marked destructive like `OpenProject`, because it closes whatever is open.                                                                                                                                                                            |
| `ExportPlcAsSourceTree` | Snapshots a whole PLC to a git-ready folder tree                                                                    | Four bulk exports with matching `preservePath` flags | Blocks and types as source documents where supported, tag and watch tables as XML. Objects that cannot be exported are reported, not fatal.                                                                                                           |
| `PreviewImport`         | Reports what an import would create, overwrite or collide with                                                      | Finding out by failing                               | Infers the object name from the file name, which is how every exporter here names its output. Changes nothing.                                                                                                                                        |
| `GenerateBlockSource`   | Writes one block as the external source file the compiler reads back (`.db`, `.scl`, `.awl`)                        | Exporting SimaticML that cannot be compiled again    | Openness generates sources only from data blocks and STL or SCL blocks; LAD, FBD and GRAPH are rejected with the reason. The extension is not a choice - Openness throws on a mismatch.                                                              |
| `GenerateTypeSource`    | Writes one PLC data type as a `*.udt` external source file                                                          | `ExportTypeAsDocuments`, which needs V21             | Works on every supported version, and the result can be imported again.                                                                                                                                                                              |
| `GenerateSources`       | Writes every block and type of a PLC as sources into a tree mirroring the project groups                            | One `GenerateBlockSource` call per object            | The compilable counterpart to `ExportPlcAsSourceTree`. Objects with no source form, inconsistent ones and know-how protected ones land in `Skipped` rather than failing the run.                                                                      |

`GetBlocks` and `GetTypes` now also return `Path`, which had been commented out on
`ResponseBlockInfo` and `ResponseTypeInfo`. Exporting one type is a single call again instead of
list, then build the path, then export.

### Write safety

Every project-mutating tool now runs inside `ExclusiveAccess.Transaction`, applied once in the
`Guarded` helper rather than per tool. A tool call commits as a unit and appears in the TIA
Portal undo stack as one named entry (`MCP: <tool>`); a body that throws rolls back instead of
leaving the project half-edited. If TIA Portal refuses exclusive access the write still runs
unwrapped, so the wrapper can never turn a working write into a failure.

## TIA Portal Openness API Surface

The Openness API has thousands of members, so this is the set **this server actually calls**,
grouped by area, with what each one backs. It is the map to consult before adding a tool: if a
capability is not listed here, it is not wired up yet.

### Portal, project and session

| Openness member                                                                                                                      | Used for                                                  |
| ------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------- |
| `new TiaPortal(TiaPortalMode.WithUserInterface)`                                                                                     | `Connect`                                                 |
| `TiaPortal.Dispose`                                                                                                                  | `Disconnect`                                              |
| `TiaPortal.Projects.OpenWithUpgrade(FileInfo)`                                                                                       | `OpenProject`, `OpenTiaProject`                           |
| `TiaPortal.LocalSessions.Open(FileInfo)`                                                                                             | Opening an `.alsXX` multiuser session                     |
| `TiaPortal.GetProcesses()` (static)                                                                                                  | `Doctor`, and attaching to a running instance             |
| `Project.Save` / `LocalSession.Save`                                                                                                 | `SaveProject`                                             |
| `Project.SaveAs`                                                                                                                     | `SaveAsProject`                                           |
| `Project.Close` / `LocalSession.Close`                                                                                               | `CloseProject`                                            |
| `Project.Devices`, `.DeviceGroups`, `.UngroupedDevicesGroup`                                                                         | `GetProjectTree`, `GetDevices`, software path enumeration |
| `TiaPortal.ExclusiveAccess(string)`                                                                                                  | The write transaction scope                               |
| `ExclusiveAccess.Transaction(ITransactionSupport, string)`, `Transaction.CommitOnDispose`, `ExclusiveAccess.IsCancellationRequested` | Atomic, named-undo writes                                 |

### Hardware

| Openness member                                                                                                    | Used for                                            |
| ------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------- |
| `Device.DeviceItems`, `DeviceItem.DeviceItems`                                                                     | Device tree traversal                               |
| `DeviceItem.GetService<SoftwareContainer>()`                                                                       | Resolving a `softwarePath` to its `PlcSoftware`     |
| `DeviceItem.GetService<SafetyAdministration>()`, `LoginToSafetyOfflineProgram`, `IsLoggedOnToSafetyOfflineProgram` | Compiling a safety program with a password          |
| `IEngineeringObject.GetAttributeInfos()` / `GetAttribute(string)`                                                  | The generic attribute bag on every `*Info` response |

### Program blocks

| Openness member                                                                                                                       | Used for                                                                  |
| ------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `PlcSoftware.BlockGroup`, `PlcBlockGroup.Groups` / `.Blocks`                                                                          | `GetBlocks`, `GetBlocksWithHierarchy`, `GetSoftwareTree`                  |
| `PlcBlockComposition.Find`                                                                                                            | Resolving a block path                                                    |
| `PlcBlock.Export(FileInfo, ExportOptions)`                                                                                            | `ExportBlock`, `ExportBlocks`                                             |
| `PlcBlockComposition.Import(FileInfo, ImportOptions)`                                                                                 | `ImportBlock`, `CopyBlock`, `MoveBlock`                                   |
| `PlcBlock.ExportAsDocuments(DirectoryInfo, string)`                                                                                   | `ExportAsDocuments`, `ExportBlocksAsDocuments`, `GetBlockSource` (V20+)   |
| `PlcBlockComposition.ImportFromDocuments(DirectoryInfo, string, ImportDocumentOptions)`                                               | `ImportFromDocuments`, `ImportBlocksFromDocuments`                        |
| `PlcBlockGroup.Blocks.CreateFB` / `.CreateInstanceDB`                                                                                 | `CreateFB`, `CreateInstanceDB` - the only block kinds Openness can create |
| `PlcBlockUserGroup.Groups.Create` / `.Delete`                                                                                         | `CreateBlockGroup`, `DeleteBlockGroup`                                    |
| `PlcBlock.Delete`, `PlcBlock.Name` (set)                                                                                              | `DeleteBlock`, `RenameBlock`                                              |
| `PlcBlock.IsConsistent`, `.ProgrammingLanguage`, `.MemoryLayout`, `.ModifiedDate`, `.IsKnowHowProtected`, `.HeaderName`, `.Namespace` | `GetBlockInfo`, `GetPlcSummary`, export pre-checks                        |
| `DataBlock.Interface`, `PlcBlockInterface.Members`, `Member.Name` plus its attributes                                                 | `GetBlockInterface`                                                       |

### PLC data types

| Openness member                                                                        | Used for                                                                  |
| -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `PlcSoftware.TypeGroup`, `PlcTypeGroup.Groups` / `.Types`                              | `GetTypes`, `GetSoftwareTree`                                             |
| `PlcType.Export`, `PlcTypeComposition.Import`                                          | `ExportType`, `ExportTypes`, `ImportType`, `CopyType`, `MoveType`         |
| `PlcType.ExportAsDocuments(DirectoryInfo, string)`                                     | `ExportTypeAsDocuments`, `ExportTypesAsDocuments`, `GetTypeSource` (V21+) |
| `PlcTypeComposition.ImportFromDocuments(DirectoryInfo, string, ImportDocumentOptions)` | `ImportTypeFromDocuments`, `ImportTypesFromDocuments` (V21+)              |
| `PlcTypeUserGroup.Groups.Create` / `.Delete`                                           | `CreateTypeGroup`, `DeleteTypeGroup`                                      |
| `PlcType.IsConsistent`, `.ModifiedDate`, `.IsKnowHowProtected`                         | `GetTypeInfo`, `GetPlcSummary`                                            |

### Tags, constants, tables and sources

| Openness member                                                                       | Used for                                                                                  |
| ------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- |
| `PlcSoftware.TagTableGroup`, `PlcTagTableGroup.TagTables` / `.Groups`                 | `GetTagTables`, `CreateTagTable`, `CreateTagTableGroup`                                   |
| `PlcTagTable.Tags`, `.UserConstants`, `.SystemConstants` (`Create`, `Find`, `Delete`) | `GetTags`, `GetConstants`, `CreateTag`, `UpdateTag`, `DeleteTag`, the user-constant tools |
| `PlcTagTable.Export`, `PlcTagTableComposition.Import`                                 | `ExportTagTable`, `ImportTagTable`                                                        |
| `PlcSoftware.WatchAndForceTableGroup`, `PlcWatchTable`, `PlcForceTable`, `.Entries`   | The watch and force table tools                                                           |
| `PlcSoftware.ExternalSourceGroup`, `ExternalSources.CreateFromFile` / `.Find`         | `GetExternalSources`, `CreateExternalSourceFromFile`                                      |
| `PlcExternalSourceSystemGroup.GenerateBlocksFromSource`                               | `GenerateBlocksFromSource`                                                                |
| `PlcExternalSourceSystemGroup.GenerateSource(IEnumerable<IGenerateSource>, FileInfo, GenerateOptions)`, `IGenerateSource`, `GenerateOptions` | `GenerateBlockSource`, `GenerateTypeSource`, `GenerateSources`                            |

### Compile and cross references

| Openness member                                                                                                                                                | Used for                          |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------- |
| `PlcSoftware.GetService<ICompilable>()`, `ICompilable.Compile()`                                                                                               | `CompileSoftware`                 |
| `CompilerResult.State` / `.ErrorCount` / `.WarningCount` / `.Messages`, and `CompilerResultMessage.Path` / `.Description` / `.State` / `.Messages` (recursive) | The structured compile report     |
| `IEngineeringObject.GetService<CrossReferenceService>()`, `CrossReferenceFilter`                                                                               | `GetCrossReferences`, `WhereUsed` |

### Document export and import results

| Openness member                                                                                    | Used for                                      |
| -------------------------------------------------------------------------------------------------- | --------------------------------------------- |
| `DocumentExportResult.State` / `.ExportedDocuments` / `.Messages`                                  | Reporting the files TIA Portal actually wrote |
| `DocumentImportResultForBlocks.ImportedPlcBlocks`, `DocumentImportResultForTypes.ImportedPlcTypes` | What an import produced                       |
| `DocumentResultState` (`Success`, `PartialSuccess`, `Failure`), `DocumentResultMessage.Message`    | Success detection and failure detail          |
| `ImportDocumentOptions` (`None`, `Override`, `SkipInactiveCultures`, `ActivateInactiveCultures`)   | The `importOption` parameter                  |

### Available in V21 but not used

Recorded so the same research is not repeated.

| Openness member                                                                               | Why not                                                                                                                                                                     |
| --------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OnlineProvider.GoOnline` / `GoOffline`, `DownloadProvider.Download`, `StationUploadProvider` | The server is deliberately offline-only                                                                                                                                     |
| Live tag values, monitoring                                                                   | **Not possible through Openness at all** - it needs the PLCSIM Advanced API or S7 communication                                                                             |
| `PlcSoftware.CompareTo` / `CompareToOnline`, `CompareResultElement`                           | No diff tool yet; today the story is export as documents and diff outside the server                                                                                        |
| `FingerprintProvider.GetFingerprints()`, `PlcBlock.CodeModifiedDate` / `.CompileDate`         | No change-detection tool yet; this is the natural cache key for `FindInCode`                                                                                                |
| `ProjectBase.HistoryEntries`, `.IsModified`, `.LastModified`                                  | No project history tool yet                                                                                                                                                 |
| `ProjectLibrary`, `GlobalLibraries`, `MasterCopy`, `LibraryTypeVersion.FindInstances`         | Libraries are unimplemented; `CreateFrom(MasterCopy)` would also give a native copy path, replacing the export-import-delete composition behind `CopyBlock` and `MoveBlock` |
| `PlcBlockProtectionProvider.Protect` / `.Unprotect`                                           | Know-how protection is reported but never changed                                                                                                                           |
| `IEngineeringObject.GetInvocationInfos()` / `Invoke(...)`                                     | A generic escape hatch to any Openness member - rejected, because it would bypass the `--allow-write` gate                                                                  |
| `PlcSimulationSettingsProvider`, `VirtualPlcSettingsProvider`                                 | Compilation settings only; Openness has no PLCSIM start or stop API                                                                                                         |
