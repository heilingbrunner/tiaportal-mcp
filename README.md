# TIA-Portal MCP-Server

A MCP server which connects to Siemens TIA Portal.

## Features

- Connect to a TIA Portal instance
- Browse and interact with TIA Portal projects
- Perform basic project operations from within VS Code
- Read the full PLC software: program blocks, PLC data types, tags and constants, watch and
  force tables, external source files, and cross references
- Optionally create, rename, delete, move and import project objects (see [Write mode](#write-mode))

## Command Line Arguments

| Argument | Description |
| --- | --- |
| `--tia-major-version <n>` | TIA Portal major version to bind against. Default `21`. |
| `--logging <1\|2\|3>` | `1` stderr, `2` debug output, `3` Windows event log. Omit for no logging. |
| `--doctor` | Print the environment report and exit without starting the MCP server. |
| `--allow-write` | Register the project-mutating tools. Omitted by default; see below. |

## Write mode

The server is read-only unless it is started with `--allow-write`.

- Without the flag the 37 project-mutating tools are **not registered at all**, so they never
  appear in `tools/list`. A model cannot call what it cannot see, and the tool list stays small.
- With the flag they are registered and annotated `destructiveHint: true`, so a client can still
  prompt before each call.
- `GetState` and the `--doctor` report both expose `AllowWrite`, so a client can tell whether the
  tools are missing by configuration rather than by version.
- Write operations change the project **in memory only**. Every write response says so and names
  the tool that persists it: `SaveProject`, or `SaveSession` when a multiuser local session is open.

Export tools are intentionally *not* gated. `ExportBlock`, `ExportTagTable`, `ExportWatchTable`
and friends only write files on the machine running the server; they never modify the project.
They are annotated `destructiveHint: true` because they can overwrite files on disk.

To enable write mode, add the argument to your client configuration, for example
`"args": ["--allow-write"]`.

## Tools

Read-only tools (59) are always available.

| Area | Tools |
| --- | --- |
| Portal and state | `Connect`, `Disconnect`, `GetState`, `Doctor` |
| Project and session | `GetProject`, `OpenProject`, `SaveProject`, `SaveAsProject`, `CloseProject` |
| Devices | `GetProjectTree`, `GetDevices`, `GetDeviceInfo`, `GetDeviceItemInfo` |
| PLC software | `GetSoftwareInfo`, `GetSoftwareTree`, `CompileSoftware` |
| Blocks | `GetBlocks`, `GetBlockInfo`, `GetBlocksWithHierarchy`, `ExportBlock`, `ExportBlocks`, `ImportBlock` |
| Types | `GetTypes`, `GetTypeInfo`, `ExportType`, `ExportTypes`, `ImportType` |
| Tags and constants | `GetTagTables`, `GetTagTableInfo`, `GetTags`, `GetTagInfo`, `GetConstants`, `ExportTagTable` |
| Watch and force tables | `GetWatchTables`, `GetWatchTableInfo`, `GetForceTables`, `ExportWatchTable` |
| External sources | `GetExternalSources`, `GetExternalSourceInfo`, `GenerateBlockSource`, `GenerateTypeSource` |
| Cross references | `GetCrossReferences` |
| Block documents (V20+) | `ExportAsDocuments`, `ExportBlocksAsDocuments`, `ImportFromDocuments`, `ImportBlocksFromDocuments` |
| Type documents (V21+) | `ExportTypeAsDocuments`, `ExportTypesAsDocuments` |
| Understand and read (comfort) | `GetPlcSummary`, `ResolveObjectPath`, `WhereUsed`, `FindInCode`, `GetBlockSource`, `GetTypeSource`, `GetBlockInterface`, `PreviewImport`, `OpenTiaProject`, `ExportPlcAsSourceTree`, `GenerateSources` |

Write tools (39) require `--allow-write`.

| Area | Tools |
| --- | --- |
| Block and type groups | `CreateBlockGroup`, `DeleteBlockGroup`, `CreateTypeGroup`, `DeleteTypeGroup` |
| Blocks and types | `DeleteBlock`, `RenameBlock`, `DeleteType`, `RenameType`, `CreateFB`, `CreateInstanceDB` |
| Copy and move | `CopyBlock`, `MoveBlock`, `CopyType`, `MoveType` |
| Tag tables | `CreateTagTable`, `DeleteTagTable`, `RenameTagTable`, `CreateTagTableGroup`, `DeleteTagTableGroup`, `ImportTagTable` |
| Tags and constants | `CreateTag`, `UpdateTag`, `DeleteTag`, `CreateUserConstant`, `UpdateUserConstant`, `DeleteUserConstant` |
| Watch tables | `CreateWatchTable`, `RenameWatchTable`, `DeleteWatchTable`, `CreateWatchTableGroup`, `DeleteWatchTableGroup`, `ImportWatchTable` |
| External sources | `CreateExternalSourceFromFile`, `DeleteExternalSource`, `CreateExternalSourceGroup`, `DeleteExternalSourceGroup`, `GenerateBlocksFromSource` |
| Type documents (V21+) | `ImportTypeFromDocuments`, `ImportTypesFromDocuments` |

`GetSoftwareTree` accepts a `sections` argument - any comma separated subset of
`blocks,types,tags,watch,sources`, default `all` - to keep the output small on a large PLC.
`GetCrossReferences` accepts `maxDepth` (1-3, default 1) for the same reason.

Paths used by these tools are **root-relative**: `1_Tests/FC_Block_1`, not
`Program blocks/1_Tests/FC_Block_1`. Use `GetProjectTree` and `GetSoftwareTree` to discover them.

## Resources

- [TIA Portal Openness API Documentation](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows)
- [TIA Portal Openness API Overview](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/tia-portal-openness-api)
- [TIA Portal Openness API for automation of engineering workflows](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows)
- [TIA Portal Openness API for automation of engineering workflows - Export/Import Documentation](https://docs.tia.siemens.cloud/r/en-us/v21/tia-portal-openness-api-for-automation-of-engineering-workflows/export/import)

## Requirements

- __.net Framework 4.8__ installed
- __Siemens TIA Portal V21__ installed and running on your machine
- Check if under `Environment Variables/User variable for user <name>` the variable `TiaPortalLocation` is set to `C:\Program Files\Siemens\Automation\Portal V21`
- User must be in Windows User Group `Siemens TIA Openness`

### Diagnose the environment

Run the server with `--doctor` to check all of the above without starting the MCP server:

```text
> TiaMcpServer.exe --doctor
Diagnose:
├─ Connected = False
├─ Project: No project open
├─ Active Version: V21
├─ Installed TIA Portal versions:
│  └─ V21: C:\Program Files\Siemens\Automation\Portal V21
│     ├─ Engineering: OK
│     └─ Portal:      OK
├─ User in 'Siemens TIA Openness' user group: True
└─ Write mode (--allow-write): disabled, read-only tools only
```

The same report is available to MCP clients through the `Doctor` tool, which additionally returns
the findings as structured content. Both are read-only: they never connect to TIA Portal, open a
project, or change user group membership.

## TIA-Portal Versions

- __V21__ is the default version.
- Previous versions are also supported, but must use the `--tia-major-version` argument to specify the version.
- Export as documents (.s7dcl/.s7res) via `ExportAsDocuments`/`ExportBlocksAsDocuments` requires TIA Portal V20 or newer.
- Import from documents (.s7dcl/.s7res) via `ImportFromDocuments`/`ImportBlocksFromDocuments` also requires TIA Portal V20 or newer.
- The same for PLC data types - `ExportTypeAsDocuments`, `ExportTypesAsDocuments`,
  `ImportTypeFromDocuments`, `ImportTypesFromDocuments` - requires TIA Portal **V21** or newer:
  Openness only added `PlcType.ExportAsDocuments` and `PlcTypeComposition.ImportFromDocuments`
  in V21.

## SIMATIC Source Documents

A source document is the readable, git-diffable form of an object: `<Name>.s7dcl` holds the
declaration and body as SCL/LAD/STL text, the optional `<Name>.s7res` holds comments and
language resources. Every other export in this server writes SimaticML XML instead, which
diffs poorly.

The file names come from TIA Portal, not from this server: an export response lists the files
that were actually written, and a batch import discovers a document set by base name rather
than assuming one extension. Tag tables and watch tables have no document API in Openness V21
and remain XML-only.

With `preservePath` the export mirrors the project tree below the system folder - `Program
blocks` for blocks, `PLC data types` for types - using the folder name as TIA Portal reports it
in the current interface language. `ImportTypeFromDocuments` and `ImportTypesFromDocuments`
accept that folder name back as a leading segment of `groupPath`, so an export can be fed
straight back in.

A PLC data type name is unique across the whole PLC, not just within its group. Importing a
name that already exists into a *different* group therefore fails with "an object with the name
... already exists in the plc", even with `importOption: Override`; point `groupPath` at the
group the type already lives in to replace it.

## Known Limitations

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.
 - `ExportBlock` requires a fully qualified `blockPath` like `Group/Subgroup/Name`. If only a name is provided, the tool fails with an error result that may include suggestions for likely full paths.

### Limits imposed by the Openness API itself

These are not gaps in this server - the underlying API offers no operation for them.

- __No move or copy for blocks and types.__ `CopyBlock`, `MoveBlock`, `CopyType` and `MoveType`
  are composed from export, import and (for a move) deleting the source once the import
  succeeds. Two consequences are visible: the object must be consistent, because TIA Portal
  refuses to export an inconsistent one, and its block number travels with it, so importing
  into the same PLC can hit a number collision. Renaming during a copy is not offered - it
  would mean rewriting the exported XML.
- __No generic "create block".__ Only `CreateFB` and `CreateInstanceDB` exist; every other kind
  of block has to arrive through `ImportBlock`.
- __Read-only objects.__ System constants cannot be created or changed, the force table cannot
  be created or deleted (the system owns one per PLC), the default tag table cannot be deleted,
  and the system groups (`Program blocks`, `PLC data types`, `PLC tags`, ...) cannot be renamed
  or deleted. These all fail with a `NotSupported` message rather than an opaque Openness error.
- __No cross references__ for watch tables, force tables or external sources; `GetCrossReferences`
  reports `NotSupported` for them.
- __Watch table entries__ cannot be created or deleted through this server yet. The Openness
  composition holding them exposes only a comment-row creator, so adding a real entry needs an
  untyped creation call whose required attribute names must first be read from the live API.
- __Safety programs.__ F-blocks and safety tags reject most edits, sometimes requiring the safety
  password. The server does not pre-guess Siemens' rules: the underlying error is passed through
  with its original message.
- __Know-how protected__ blocks and types are rejected before any edit, with a message asking you
  to remove the protection in TIA Portal first.

## Testing

- See `tests/TiaMcpServer.Test/README.md` for environment prerequisites and test asset setup.
- Standard command: `dotnet test` (run from the repo root).
- Test execution policy: offer to run tests, but only execute after explicit user confirmation. Details in `AGENTS.md`.

## Contributing

- See `agents.md` for guidance on working with agentic assistants and the test execution policy (offer to run tests only with explicit user confirmation).

## Error Handling

- The Portal layer throws `PortalException` with a short message and `PortalErrorCode`
  (`NotFound`, `InvalidParams`, `InvalidState`, `ExportFailed`, `ImportFailed`, `CreateFailed`,
  `DeleteFailed`, `RenameFailed`, `NotSupported`, `WriteDisabled`), and attaches `softwarePath`, `blockPath`, `exportPath` in `Exception.Data` while preserving `InnerException` on export failures.
- The MCP layer rethrows these as `McpException`. Since SDK 2.x, an `McpException` thrown from a tool is returned to the client as a `CallToolResult` with `isError: true` and the message as text content, rather than as a JSON-RPC error, so the model can read the reason and self-correct. For `ExportFailed` the message includes a concise reason from the underlying error; for `NotFound` it may suggest likely full block paths if a bare name was provided.
- Consistency required: TIA Portal never exports inconsistent blocks/types. Single export returns `InvalidParams` with a message to compile first. Bulk export skips inconsistent items and returns them in an `Inconsistent` list alongside `Items`.
- Standardization: Exception context metadata is attached in a single catch per portal method right before rethrow, not at inline throw sites. See `docs/error-model.md`.
- Since 0.2.0 this is implemented once, in the `Operation.Run` helper, which every portal method
  added for tags, watch tables, external sources, cross references and the write operations
  routes through. The older export/import methods still carry their hand-written equivalent of
  the same block; migrating them is tracked in `TODO.md`.
- `Operation.Run` also serializes all Openness calls behind a lock. Openness objects are not
  thread-safe and the MCP SDK may dispatch tool calls concurrently; with write tools enabled an
  unsynchronized race could corrupt project state rather than merely return stale data.

## MCP Protocol

- Built on the [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) .NET SDK **2.2.0**.
- Protocol revisions negotiated during `initialize`: `2024-11-05`, `2025-03-26`, `2025-06-18`, `2025-11-25` (the SDK picks the highest the client also supports).
- Every tool advertises a human-readable `title` and behaviour annotations (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`).
- 27 read tools and all 37 write tools publish an `outputSchema` and return `structuredContent`.
- Long-running export/import tools report progress via `notifications/progress` when the client supplies a `progressToken`.
- Tool failures are returned as tool results with `isError: true` (not JSON-RPC errors), so the model can read the message and retry.

## Transports

- Supported today: `stdio`
  - Program wires `AddMcpServer().WithStdioServerTransport()`.
  - For stdio, logs must go to stderr to avoid corrupting JSON-RPC.
- Available via SDK: `stream` (custom streams)
  - The SDK exposes `WithStreamServerTransport(Stream input, Stream output)` which can be used to host over TCP sockets or other streams.
  - Not wired in this repo yet.
- HTTP/Streamable HTTP: not implemented yet
  - `ModelContextProtocol` 2.2.0 ships its Streamable HTTP server transport in `ModelContextProtocol.AspNetCore`, which targets .NET 8+.
  - This server targets `net48` (required by TIA Openness), so Streamable HTTP cannot be hosted from this process.
  - A separate .NET 8+ proxy process would be required to expose this server over HTTP.

## Copilot Chat

- Example mcp.json, when using VS Code extension [TIA-Portal MCP-Server](https://marketplace.visualstudio.com/items?itemName=JHeilingbrunner.vscode-tiaportal-mcp) and TIA-Portal V18
  ```json
  {
      "servers": {
          "vscode-tiaportal-mcp": {
          "command": "c:\\Users\\<user>\\.vscode\\extensions\\jheilingbrunner.vscode-tiaportal-mcp-<version>\\srv\\net48\\TiaMcpServer.exe",
          "args": [
              "--tia-major-version",
              "18"
          ],
          "env": {}
          }
      }
  }
  ```

## Claude Desktop

- Create/Edit to add/remove server to `C:\Users\<user>\AppData\Roaming\Claude\claude_desktop_config.json`:

  ```json
  {
    "mcpServers": {
      "vscode-tiaportal-mcp": {
        "command": "<path-to>\\TiaMcpServer.exe",
        "args": [],
        "env": {}
      }
    }
  }
  ```
