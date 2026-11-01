# TiaMcpServer

This document provides a comprehensive overview of the TiaMcpServer project, a C# application that acts as a Model Context Protocol (MCP) server to expose the Siemens TIA Portal API to Large Language Models (LLMs).

## Resources

- [TIA Portal Openness API Overview](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api)
- [TIA Portal Openness API Documentation](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows)
- [TIA Portal Openness API for automation of engineering workflows](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows)
- [TIA Portal Openness API for automation of engineering workflows - Export/Import Documentation](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/export/import)

## 1. Project Overview

The TiaMcpServer project is a .NET 4.8 console application that enables communication between an LLM and the Siemens TIA Portal. It achieves this by implementing an MCP server that exposes a set of tools for interacting with the TIA Portal. The project is divided into two main parts:

*   **MCP Server:** This part of the project is responsible for handling communication with the LLM. It uses the `ModelContextProtocol` library to create an MCP server that listens for requests from the LLM and executes the corresponding tools.
*   **TIA Portal Interfacing API:** This part of the project is responsible for interacting with the TIA Portal. It uses the Siemens TIA Portal Openness API to perform tasks such as connecting to the TIA Portal, opening and closing projects, and working with devices, blocks, and types.

## 2. Project Structure

The project is organized into the following directories:

Both `Portal` and `McpServer` are `partial` classes split by functional area, so no single file
carries the whole surface.

*   **`ModelContextProtocol/`**: This directory contains the implementation of the MCP server.
    *   `McpServer.cs` and the partials beside it: all MCP tools, as partials of one `McpServer` type, split the same way as `Portal` in `Siemens/` below. The project-mutating tools are marked `[WriteTool]` and sit next to the read-only tools of their area; `Program.BuildTools` registers them only under `--allow-write`, which keeps them out of `tools/list` rather than merely refusing them when called.
        *   `McpServer.cs`: connection, state, project and session tools, the project tree, `OpenTiaProject` and `PreviewImport`, plus the shared write plumbing (the `Guarded` wrapper, `SaveHint` and the response builders).
        *   `McpServer.Devices.cs`, `McpServer.Blocks.cs`, `McpServer.Types.cs`, `McpServer.Tags.cs`: devices; program blocks (including `GetBlockInterface`) and PLC data types, with their create, rename, delete, copy and move tools; tag, watch and force tables with their write tools.
        *   `McpServer.Plc.cs`: the export and import tools of a whole PLC program: `ExportPlcProgram`, `ExportBlocks`, `ExportTypes`, `ExportTagTables`, `ExportWatchTables`, the single object tools `ExportBlock`, `ExportType`, `ExportTagTable`, `ExportWatchTable` and `ExportForceTable`, and the write side `OverwritePlcProgram` and `ImportPlcProgramChanges`. Blocks and PLC data types are written in the format `<exportPath>/tia.json` gives (source, XML or SIMATIC SD documents); tag, watch and force tables are always XML. The export tools only write files and are not `[WriteTool]`.
        *   `McpServer.Porcelain.cs`: the git-like short commands for the chat: `TiaConnect`, `TiaPull*` (one per export tool above) and `TiaPush` / `TiaPushPreview`. Each only delegates to the tool it stands for; the prompts are in `Prompts/McpPrompts.Porcelain.cs`.
        *   `McpServer.ExportFormats.cs`: `GetExportFormats`, which shows what `tia.json` says and which export tool writes each object type. `McpServer.OpcUa.cs`: `ExportOpcUaXml`. `McpServer.Libraries.cs`: `GetOpenLibraries`, `GetReferencedLibraries`. `McpServer.Marking.cs`: `GetMarkedObjects`. `McpServer.Commit.cs`: `CheckIsUpToDate`, `CommitChanges`.
        *   `McpServer.Sources.cs`: the inline readers `GetXmlBlock`, `GetSourceBlock`, `GetDocumentsBlock` and the type equivalents (they export to a scratch directory and return the text), the external source tools (`GetExternalSources`, `GetExternalSourceInfo` and the write tools that create and delete sources and source groups), and `GeneratedMany`, the response builder the export tools share.
        *   `Software/` (`McpServer.Software.*.cs`): software info and compile, the software tree, `ResolveObjectPath`, `FindInCode`, `GetPlcSummary`, and cross references (`GetCrossReferences`, `WhereUsed`).
    *   `WritePolicy.cs`: the `--allow-write` gate.
    *   `Prompts/McpPrompts*.cs`: These partial class files contain the prompts that are used to guide the LLM.
    *   `Responses/`: one file per response object returned by the tools. The write side shares `ResponseCreated`, `ResponseDeleted`, `ResponseRenamed`, `ResponseImported` and `ResponseGenerateBlocks` across the write tools rather than minting one DTO per operation; the export tools return `ResponseGeneratedSources`.
    *   `Types.cs`: This file defines the data types that are used by the MCP server.
    *   `ToolDefaults.cs`: the presets of `--software-path`, `--export-path` and the other CLI options, filled into tool and prompt arguments by parameter name. `ToolOrdering.cs`: sorts `tools/list`. `PortalSelection.cs`: picks the TIA Portal instance a request runs on. `PromptArguments.cs`: lets a slash command carry quoted values with spaces.
    *   `Helper.cs`: `GetAttributeList(IEngineeringObject)` reflects over any Openness object's attributes, so each new `Get*Info` tool is a few typed fields plus that call.
*   **`Siemens/`**: This directory contains the implementation of the TIA Portal interfacing API.
    *   `Portal.cs`: connection, project and session lifecycle, the project tree, and the `InTransaction` helpers.
    *   `Portal.Devices.cs`: devices and device items, resolved by path.
    *   `Software/` (`Portal.Software.*.cs`): everything about one PLC software, split by concern.
        *   `Portal.Software.Core.cs`: software lookup (`GetPlcSoftware` and the container resolvers) and `CompilePlcSoftware`.
        *   `Portal.Software.Tree.cs`: the software tree, including the section renderers.
        *   `Portal.Software.Resolve.cs`: the generic path helpers (`WalkGroups`, `BuildGroupPath`, `WalkRecursive`). `PlcSoftware` exposes five look-alike group hierarchies - blocks, types, tag tables, watch and force tables, external sources - that share no common base type, so the shape is captured with generics plus selector delegates instead of inheritance.
        *   `Portal.Software.Lookup.cs`: `ResolveObjectPath` and `GetSoftwarePaths`.
        *   `Portal.Software.Search.cs`: `FindInCode`.
        *   `Portal.Software.Summary.cs`: `GetPlcSummary`.
        *   `Portal.Software.CrossReferences.cs`: `GetCrossReferences`.
    *   `Portal.Blocks.cs` / `Portal.Types.cs`: program blocks and PLC data types respectively - read, XML export/import, create/delete/rename, copy/move, and source text (`GetXml*`, `GetSource*`, `GetDocuments*`). Helpers shared by both (the guards and the scratch-directory handling) live in `Portal.Blocks.cs`.
    *   `Portal.Plc.cs`: the whole-program and single object exports (`ExportPlcProgram`, `ExportPlcBlock`, `ExportPlcType`, `ExportPlcTagTables`, `ExportPlcWatchTables` and the matching single ones), `OverwritePlcProgram` and `ImportPlcProgramChanges`. `PlcProgram.cs` plans which object goes into which format and `ExportFormats.cs` reads and creates `tia.json`.
    *   `Portal.Tags.cs`: tag tables, tags and constants, watch and force tables - both the read side and the write side.
    *   `Portal.Documents.cs`: SIMATIC source documents for PLC data types and blocks - the `ExportAsDocuments` and `ExportTypeAsDocuments` exports behind `GetDocumentsBlock` / `GetDocumentsType`, and the `...FromDocuments` imports used by `OverwritePlcProgram` and `ImportPlcProgramChanges`.
    *   `Portal.Sources.cs`: everything about TIA Portal source files. The source text readers (`GetXml*`, `GetSource*`, `GetDocuments*`) with their scratch directory, the source generation and compile steps the whole-program tools use (`GenerateSource`, `PlcExternalSource.GenerateBlocksFromSource`), and the external source methods.
    *   `Operation.cs`: the single exception-decoration point (see `docs/error-model.md`), which also serializes all Openness traffic behind a reentrant lock.
    *   `State.cs`: This file defines the `State` class, which represents the state of the TIA Portal.
    *   `Openness.cs`: This file provides a wrapper around the Siemens TIA Portal Openness API.
    *   `Diagnostics.cs`: the environment report behind `--doctor` and the `Doctor` tool.
    *   `Portal.Instances.cs` / `PortalContext.cs`: several TIA Portal instances can run on one machine, so each attached instance gets a `PortalContext` keyed by its process id and a request names the instance it wants. `Portal.Libraries.cs`, `Portal.Marking.cs`, `Portal.Commit.cs`, `Portal.OpcUa.cs`: libraries, check-in markings, session commit and the OPC UA export.
    *   `FileNames.cs`, `ImportErrors.cs`, `SourceFileEncoding.cs`, `Engineering.cs`, `PortalException.cs` / `PortalErrorCode.cs`: file name sanitizing, one message for an Openness import error, source file re-encoding, the `Siemens.Engineering.dll` resolver, and the error model.
*   **`Git/`**: a small, read-only wrapper around the `git` command line. It answers one question for `ImportPlcProgramChanges` (and `TiaPush` / `TiaPushPreview`): which files of an export folder changed since the last commit, so only those are imported instead of the whole tree. It uses the `git` executable from `PATH` rather than a library, so the server gains no dependency, and it never writes to the repository.
    *   `Git.Changes.cs`: `GitRepo.GetChanges` and `GetChangeSet` (modified, staged and untracked files below a folder; the change set also reports the files elsewhere in the repository that were left out), `TryGetRepositoryRoot`, and `ParseStatus`, which reads the output of `git status --porcelain=v1 -z` (paths are never quoted and always relative to the repository root, so umlauts and spaces survive). `GitChangeKind` says what happened to a file (modified, added, untracked, renamed, deleted, conflict); `Letter()` gives the one-letter status shown in the `Git` column of the push preview.
    *   `Git.Process.cs`: `GitRepo.Run` and `RunCommand` start the process. The call runs inside the exclusive access of a TIA Portal write tool, so a hanging git would freeze TIA Portal behind its "Exclusive access" box. Therefore the timeout applies to the process (which is killed together with its children), git gets no stdin because the server's own stdin is the MCP channel, stdout and stderr are read by background pumps, and fsmonitor and the untracked cache are switched off. A failure is reported as a `GitException` with a message a tool caller can act on, for example when the folder is not in a repository.

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
*   **Working with PLC software:** The `GetSoftwareInfo`, `CompilePlcSoftware` and `CompileCodeBlock` tools allow the LLM to get information about PLC software and compile it or a single block. `GetSoftwareTree` renders the whole PLC software - program blocks, PLC data types, PLC tags, watch and force tables and external source files - and takes a `sections` argument to narrow the output.
*   **Working with libraries:** `GetOpenLibraries` lists the project library and the global libraries that are open in TIA Portal, with name, path, state and all attributes. Global libraries belong to the TIA Portal instance, not to the project. `GetReferencedLibraries` reads the project library and groups its type versions by the library they came from (`LibraryTypeVersion.OriginalLibrary`), so it also reports libraries that are no longer open.
*   **Working with blocks:** The `GetBlockInfo`, `GetBlocks`, `GetBlocksWithHierarchy` and `GetBlockInterface` tools read blocks; `ExportBlock` and `ExportBlocks` write them in the format of `tia.json`.
    - `ExportBlock` expects `blockPath` to be a fully qualified path like `Group/Subgroup/Name`. Passing just a name is ambiguous; use `ExportBlocks` with a regular expression to select by name.
*   **Working with types:** The `GetTypeInfo` and `GetTypes` tools read types; `ExportType` and `ExportTypes` write them in the format of `tia.json`.
*   **The whole PLC program:** `ExportPlcProgram` writes every block and PLC data type, each in the format TIA Portal is configured for (SIMATIC SD documents, XML or a source file). Openness cannot read that setting, so it is read from `<exportPath>/tia.json` (see `GetExportFormats`). `OverwritePlcProgram` reads the folder back, `ImportPlcProgramChanges` only the files git reports as changed (see `Git/` above). SIMATIC SD documents need TIA Portal V20 or newer for blocks and V21 or newer for PLC data types.
*   **`tia` commands:** `TiaConnect`, `TiaPull*`, `TiaPush` and `TiaPushPreview` are short git-like names for the tools above. See the root `README.md`.
*   **Working with PLC tags and constants:** `GetTagTables`, `GetTagTableInfo`, `GetTags`, `GetTagInfo`, `GetConstants`, `ExportTagTable` and `ExportTagTables` (always XML).
*   **Working with watch and force tables:** `GetWatchTables`, `GetWatchTableInfo` (including entries), `GetForceTables`, `ExportWatchTable`, `ExportWatchTables` and `ExportForceTable` (always XML).
*   **Working with external source files:** `GetExternalSources` and `GetExternalSourceInfo`.
*   **Cross references:** `GetCrossReferences` for a PLC software or a single block, type, tag table, tag or block group, with `maxDepth` to bound the result size.
*   **Modifying the project (`--allow-write` only):** the tools that create, rename, delete, import, copy and move blocks, types, groups, tag tables, tags, user constants, watch tables and external sources, plus `OverwritePlcProgram`, `ImportPlcProgramChanges`, `TiaPush` and `TiaPushPreview`. See the root `README.md` section "Write mode" for the gating rules and the API limits that shape them.

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

## Error Handling Standard

- Portal layer
  - Throws `PortalException` with a short message and `PortalErrorCode`.
  - Attaches context via `Exception.Data` keys: `softwarePath`, `blockPath`, `exportPath`.
  - Preserves the original exception as `InnerException` for `ExportFailed` and logs full details.
- MCP layer
  - Rethrows as `McpException`. Since SDK 2.x an `McpException` thrown from a tool becomes a `CallToolResult` with `isError: true` carrying the message as text, instead of a JSON-RPC error, so the model can read the reason and retry.
  - For `NotFound`, the message points to the tool that lists the available objects (for example `GetBlocks`).
  - For `ExportFailed`, includes a concise reason from `InnerException.Message`.
  - Consistency: TIA Portal does not export inconsistent blocks/types. Single-item exports fail with a message advising to compile first. Bulk exports skip inconsistent items and include them in an `Inconsistent` list in the response.
  - Keeps user messages concise; structured details live in logs and context.
  - Current standardization is applied to the export methods (for example `ExportPlcBlock`) and will be rolled out to other methods incrementally.
  - Exception metadata: Context keys (e.g., `softwarePath`, `blockPath`/`typePath`, `exportPath`) are attached in a single catch per portal method just before rethrow, not at inline throw sites. See `docs/error-model.md`.

## Contributing

- See root `AGENTS.md` for agent guidance and the test execution policy (offer to run tests only with explicit user confirmation).

## Comfort Functions

Tools added to shorten the path between a question and an answer. All are read-only and none
change an existing tool's contract; `CompilePlcSoftware` was enriched in place rather than
duplicated. Every one is verified against a live TIA Portal V21.

| Tool                                     | What it does                                                                                                        | Replaces                                                           | Limits worth knowing                                                                                                                                                                                                            |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GetXmlBlock` / `GetXmlType`             | Returns a block's or PLC data type's SimaticML XML inline                                                           | `ExportBlock` / `ExportType` in XML, then open the file            | Openness has no in-memory block body, so it exports to a temp scratch directory and removes it. Truncates at `maxChars` on a line boundary.                                                                                     |
| `GetSourceBlock` / `GetSourceType`       | Returns external source text inline (`.scl`, `.db`, `.awl`, `.udt`), the form the compiler reads back               | `ExportBlock` / `ExportType` as source, then open the file         | LAD, FBD and GRAPH blocks have no source form and are rejected. Types need no V21. `withDependencies` adds every object used.                                                                                                   |
| `GetDocumentsBlock` / `GetDocumentsType` | Returns SIMATIC Source Document text inline (`.s7dcl` / `.s7res`)                                                   | `ExportBlock` / `ExportType` as documents, then open the files     | Blocks V20+, types V21+. STL and mixed-language blocks have no source document and are rejected, with a hint to `GetXmlBlock`; there is no silent fallback.                                                                     |
| `GetBlockInterface`                      | Lists a data block's members with data type and every attribute                                                     | Guessing, or exporting the whole block                             | Data blocks only - V21 exposes `Interface` on `DataBlock` and offers no equivalent for FB, FC or OB, whose declarations come from `GetSourceBlock` or `GetDocumentsBlock`. Needs no export, so it works on inconsistent blocks. |
| `FindInCode`                             | Regex search over the actual program text                                                                           | Name-only regex filters                                            | Exports each candidate per call; narrow a large PLC with `nameFilter`. Objects that cannot be read are listed in `Unsearchable`.                                                                                                |
| `CompilePlcSoftware`                        | Now returns the whole compiler message tree flattened to `{path, state, description}` plus error and warning counts | A single stringified sentence                                      | Warnings are a successful compile with detail; errors set `meta.success` to false and the full message list is still returned.                                                                                                                                                       |
| `GetPlcSummary`                          | Counts per area, a programming-language histogram, and the inconsistent and know-how-protected objects              | Six separate discovery calls                                       | Built from the existing collectors, so the figures always agree with the individual tools.                                                                                                                                      |
| `WhereUsed`                              | Answers "what uses this?" from a bare name                                                                          | `GetCrossReferences` plus manual tree walking                      | Blocks, types, tags and tag tables only - watch tables and external sources have no cross references. Reports the candidates when a name is ambiguous.                                                                          |
| `ResolveObjectPath`                      | Turns a bare or partial name into the root-relative path the other tools need                                       | Listing a whole area and post-processing it                        | Searches blocks, types, tags, tag tables, watch tables and sources. Exact matches win; substring matches appear only when nothing matches exactly.                                                                              |
| `OpenTiaProject`                         | Connects if needed, opens the project, and returns the device and PLC software paths                                | `Connect` then `OpenProject` then `GetProjectTree`                 | Marked destructive like `OpenProject`, because it closes whatever is open.                                                                                                                                                      |
| `ExportPlcProgram`                       | Snapshots a whole PLC to a git-ready folder tree                                                                    | Several bulk exports                                               | Every block and type in the format of `tia.json`. Objects that cannot be exported are reported in `Skipped` and `Failures`, not fatal. Tag and watch tables are written by `ExportTagTables` and `ExportWatchTables`.            |
| `PreviewImport`                          | Reports what an import would create, overwrite or collide with                                                      | Finding out by failing                                             | Infers the object name from the file name, which is how every exporter here names its output. Changes nothing.                                                                                                                  |

`GetBlocks` and `GetTypes` now also return `Path`, which had been commented out on
`ResponseBlockInfo` and `ResponseTypeInfo`. Exporting one type is a single call again instead of
list, then build the path, then export.

`OverwritePlcProgram` and `ImportPlcProgramChanges` live under `--allow-write` rather than in the table above because they mutate the project: they walk a tree of source files, XML and SIMATIC SD documents - typically one `ExportPlcProgram` just wrote - and compile each back into a block or PLC data type, in the group its folder path implies. `ImportPlcProgramChanges` takes only the files that git reports as changed (see `Git/` above).

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
| `LocalSession.MarkingService.GetMarkings()`, `Markings.AllMarkings`, `Marking.MarkedObject` / `.MarkState`, `MarkState` flags       | `GetMarkedObjects` (objects marked for check-in)          |
| `LocalSession.IsUptoDate()`                                                                                                           | `CheckIsUpToDate`                                         |
| `LocalSession.CloseAndCommit(comment)` (returns the new server revision, closes the session)                                          | `CommitChanges`                                           |
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

| Openness member                                                                                                                       | Used for                                                                   |
| ------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `PlcSoftware.BlockGroup`, `PlcBlockGroup.Groups` / `.Blocks`                                                                          | `GetBlocks`, `GetBlocksWithHierarchy`, `GetSoftwareTree`                   |
| `PlcBlockComposition.Find`                                                                                                            | Resolving a block path                                                     |
| `PlcBlock.Export(FileInfo, ExportOptions)`                                                                                            | `GetXmlBlock`, `ExportBlock`, `ExportBlocks`, `ExportPlcProgram`           |
| `PlcBlockComposition.Import(FileInfo, ImportOptions)`                                                                                 | `CopyBlock`, `MoveBlock`, `OverwritePlcProgram`, `ImportPlcProgramChanges` |
| `PlcBlock.ExportAsDocuments(DirectoryInfo, string)`                                                                                   | `GetDocumentsBlock`, `ExportBlock`, `ExportPlcProgram` (V20+)              |
| `PlcBlockComposition.ImportFromDocuments(DirectoryInfo, string, ImportDocumentOptions)`                                               | `OverwritePlcProgram`, `ImportPlcProgramChanges`                           |
| `PlcBlockGroup.Blocks.CreateFB` / `.CreateInstanceDB`                                                                                 | `CreateFB`, `CreateInstanceDB` - the only block kinds Openness can create  |
| `PlcBlockUserGroup.Groups.Create` / `.Delete`                                                                                         | `CreateBlockGroup`, `DeleteBlockGroup`                                     |
| `PlcBlock.Delete`, `PlcBlock.Name` (set)                                                                                              | `DeleteBlock`, `RenameBlock`                                               |
| `PlcBlock.IsConsistent`, `.ProgrammingLanguage`, `.MemoryLayout`, `.ModifiedDate`, `.IsKnowHowProtected`, `.HeaderName`, `.Namespace` | `GetBlockInfo`, `GetPlcSummary`, export pre-checks                         |
| `DataBlock.Interface`, `PlcBlockInterface.Members`, `Member.Name` plus its attributes                                                 | `GetBlockInterface`                                                        |

### PLC data types

| Openness member                                                                        | Used for                                                                     |
| -------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| `PlcSoftware.TypeGroup`, `PlcTypeGroup.Groups` / `.Types`                              | `GetTypes`, `GetSoftwareTree`                                                |
| `PlcType.Export`, `PlcTypeComposition.Import`                                          | `GetXmlType`, `ExportType`, `ExportTypes`, `CopyType`, `MoveType`            |
| `PlcType.ExportAsDocuments(DirectoryInfo, string)`                                     | `GetDocumentsType`, `ExportType`, `ExportPlcProgram` (V21+)                  |
| `PlcTypeComposition.ImportFromDocuments(DirectoryInfo, string, ImportDocumentOptions)` | `OverwritePlcProgram`, `ImportPlcProgramChanges` (V21+)                      |
| `PlcTypeUserGroup.Groups.Create` / `.Delete`                                           | `CreateTypeGroup`, `DeleteTypeGroup`                                         |
| `PlcType.IsConsistent`, `.ModifiedDate`, `.IsKnowHowProtected`                         | `GetTypeInfo`, `GetPlcSummary`                                               |

### Tags, constants, tables and sources

| Openness member                                                                                                                                                           | Used for                                                                                        |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `PlcSoftware.TagTableGroup`, `PlcTagTableGroup.TagTables` / `.Groups`                                                                                                     | `GetTagTables`, `CreateTagTable`, `CreateTagTableGroup`                                         |
| `PlcTagTable.Tags`, `.UserConstants`, `.SystemConstants` (`Create`, `Find`, `Delete`)                                                                                     | `GetTags`, `GetConstants`, `CreateTag`, `UpdateTag`, `DeleteTag`, the user-constant tools       |
| `PlcTagTable.Export`                                                                                                                                                      | `ExportTagTable`, `ExportTagTables`                                                             |
| `Project.HwUtilities.Find("OPCUAExportProvider")`, `OpcUaExportProvider.Export(DeviceItem, FileInfo)`                                                                     | `ExportOpcUaXml`                                                                                |
| `PlcSoftware.WatchAndForceTableGroup`, `PlcWatchTable`, `PlcForceTable`, `.Entries`, `PlcWatchTable.Export`, `PlcForceTable.Export`                                      | The watch and force table tools, `ExportWatchTable(s)`, `ExportForceTable`                      |
| `PlcSoftware.ExternalSourceGroup`, `ExternalSources.CreateFromFile` / `.Find`                                                                                             | `GetExternalSources`, `CreateExternalSourceFromFile`                                            |
| `PlcExternalSourceSystemGroup.GenerateSource(IEnumerable<IGenerateSource>, FileInfo, GenerateOptions)`, `IGenerateSource`, `GenerateOptions`                              | `GetSourceBlock`, `GetSourceType`, `ExportPlcProgram` (source format)                           |
| `PlcExternalSource.GenerateBlocksFromSource(PlcBlockUserGroup, GenerateBlockOption)`, `PlcExternalSource.GenerateBlocksFromSource(PlcTypeUserGroup, GenerateBlockOption)` | `OverwritePlcProgram`, `ImportPlcProgramChanges` - one call compiles a source into either kind, chosen by the file's own content |

### Compile and cross references

| Openness member                                                                                                                                                | Used for                          |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------- |
| `PlcSoftware.GetService<ICompilable>()`, `ICompilable.Compile()`                                                                                               | `CompilePlcSoftware`                 |
| `CompilerResult.State` / `.ErrorCount` / `.WarningCount` / `.Messages`, and `CompilerResultMessage.Path` / `.Description` / `.State` / `.Messages` (recursive) | The structured compile report     |
| `IEngineeringObject.GetService<CrossReferenceService>()`, `CrossReferenceFilter`                                                                               | `GetCrossReferences`, `WhereUsed` |

### Libraries

| Openness member                                                                                                                                                                  | Used for                                                                           |
| -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| `ProjectBase.ProjectLibrary`                                                                                                                                                     | `GetOpenLibraries` (the project library), `GetReferencedLibraries` (its types)     |
| `TiaPortal.GlobalLibraries`, `GlobalLibrary.Name` / `.Path` / `.IsReadOnly` / `.IsModified`, `UserGlobalLibrary` / `SystemGlobalLibrary` / `CorporateGlobalLibrary`             | `GetOpenLibraries` (the open global libraries; they belong to the portal instance) |
| `ILibrary.TypeFolder`, `LibraryTypeFolder.Types` / `.Folders`, `LibraryType.Versions`                                                                                            | Walking the project library for `GetReferencedLibraries`                           |
| `LibraryTypeVersion.OriginalLibrary` / `.VersionNumber` / `.IsDefault` / `.State`                                                                                                | The library each type version came from, and its version and state                 |

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
| `MasterCopy`, `LibraryTypeVersion.FindInstances`                                              | Master copies unimplemented; `CreateFrom(MasterCopy)` would also give a native copy path, replacing the export-import-delete composition behind `CopyBlock` and `MoveBlock` |
| `PlcBlockProtectionProvider.Protect` / `.Unprotect`                                           | Know-how protection is reported but never changed                                                                                                                           |
| `IEngineeringObject.GetInvocationInfos()` / `Invoke(...)`                                     | A generic escape hatch to any Openness member - rejected, because it would bypass the `--allow-write` gate                                                                  |
| `PlcSimulationSettingsProvider`, `VirtualPlcSettingsProvider`                                 | Compilation settings only; Openness has no PLCSIM start or stop API                                                                                                         |
