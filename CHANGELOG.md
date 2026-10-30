# Change Log

## [Unreleased]

### Added

- __`ExportPlcProgram` follows the export formats of TIA Portal, `GetExportFormats`__: each object is written in
  the format TIA Portal is configured for (Options > Settings > Version control interface > Export formats):
  SIMATIC SD documents (`.s7dcl`, `.s7res`), SimaticML (`.xml`) or a source file (`.scl`, `.awl`, `.db`, `.udt`).
  Openness cannot read that setting (the settings tree it offers has no export formats), so it is read from
  `<exportPath>/tia.json`:
  `{"version control interface": {"code blocks-LAD": "SIMATIC SD(.s7dcl, .s7res)", "code blocks-STL": ".awl", ...}}`.
  A missing file is created with the defaults TIA Portal offers for a new workspace, and the response asks the
  user to check it (`Notes` of `ExportPlcProgram`, `Created` and `Hint` of `GetExportFormats`); an existing file is
  never overwritten. Without an entry for an object type of the PLC, or with an unknown format, the call fails
  before writing anything. SIMATIC SD documents need the object to be prepared in TIA Portal (Tools > Prepare
  for Simatic SD); an object that is not prepared is listed in `Failures` with the message of TIA Portal.
  FBD blocks are exported when the settings give them a format; GRAPH blocks have no entry and are skipped. `ExportPlcProgram` no longer takes
  `regexName`: it exports the whole program. The new read-only tool `GetExportFormats(exportPath)` shows the
  file and the export tool that writes each format. `OverwritePlcProgram` and `ImportPlcProgramChanges` still
  import only sources and XML.
- __`ExportOpcUaXml`__: exports the OPC UA data of one PLC as `<exportPath>/<softwarePath>/OPC UA/<PLC name>.xml`
  (`OpcUaExportProvider.Export`, found through `Project.HwUtilities`). Read-only with respect to the project, so
  it is not gated behind `--allow-write`; it fails with `NotSupported` when TIA Portal does not offer the OPC UA
  export provider. A prompt of the same name is included.
- __`CheckIsUpToDate`__: reports whether the open multiuser local session is up to date with the server
  project (`LocalSession.IsUptoDate()`), read-only. Without a local session it fails with a clear message.
- __`CommitChanges`__: commits the changes of the open multiuser local session to the server project
  (`LocalSession.CloseAndCommit(comment)`) and returns the new server revision. Requires `--allow-write`
  and a non-empty `comment`. It refuses to commit when the session is not up to date. Openness closes
  the session as part of the commit, so the project must be reopened afterwards. New error code
  `PortalErrorCode.CommitFailed`.
- __`GetMarkedObjects`__: lists the objects marked for check-in in an open multiuser local session
  (`LocalSession.MarkingService.GetMarkings()`), read-only. Each item reports `kind`, `name`, a `path`
  usable with the other tools (blocks, types, tag, watch and force tables, external sources) and the
  mark state: `isMarkedByMe`, `isMarkedByOthers`, `isUpToDate`. The optional `filter` is `all`
  (default), `me` or `others`. The response also carries a plain `names` list with the name of every
  marked object. Without a local session it fails with a clear message.

- __`CompileCodeBlock`__: compiles a single code block (OB, FC, FB or DB) through the block's `ICompilable`
  service and reports its compiler messages in the same structure as `CompilePlcSoftware`. Takes
  `softwarePath`, `blockPath` and an optional `password` for safety programs.

- __`GetOpenLibraries` and `GetReferencedLibraries`__: the first library support, read-only.
  `GetOpenLibraries` lists the project library and the global libraries open in TIA Portal (user,
  system, corporate) with `kind`, `name`, `path`, `isReadOnly`, `isModified` and the full attribute
  list. Global libraries belong to the TIA Portal instance, not to the project.
  `GetReferencedLibraries` reads the project library and groups its type versions by the library
  they were generated from (`LibraryTypeVersion.OriginalLibrary`), reporting version number, default
  flag and state per type version. It also names libraries that are no longer open, and marks those
  that are open with their kind and path. Neither tool takes parameters.

- __Preset tool arguments__: the new command line arguments `--project-path`, `--software-path`,
  `--export-path`, `--preserve-path` and `--with-dependencies` (the last two are flags, like `--allow-write`) preset the `path`, `softwarePath`, `exportPath`, `preservePath` and `withDependencies` arguments of every tool
  that declares them. A preset argument is no longer `required` in the tool schema and is filled in when
  a call omits it (or sends `null` or an empty string); a value in the call still wins. Without the
  arguments nothing changes. `GetState` reports the active presets. Slash-command prompts use the presets
  too: an argument with a preset is optional in `prompts/list` and filled in when left out. Claude Code
  fills prompt arguments by position, so overriding only `exportPath` still needs `softwarePath` typed first.

- __`--force-presets`__: opt-in flag that makes the `path`, `softwarePath` and `exportPath` presets (those
  that are set) binding. They are removed from `tools/list` and from the prompt arguments, and a call that
  still sends them is overwritten with the preset. `preservePath` and `withDependencies` stay overridable.
  `OpenTiaProject` can then only open the preset project. `GetState` lists them in `ForcedPresets`. It only
  fixes these arguments and is not a sandbox: the `importPath` / `filePath` of the import tools and the
  names inside `blockPath` are not restricted.

- __`ExportPlcProgram` and `OverwritePlcProgram`__: the whole program of a PLC in one call, each object in the
  format that suits it. SCL and STL blocks, data blocks and PLC data types are written and read as source files
  (`.scl`, `.awl`, `.db`, `.udt`), LAD blocks as XML (`.xml`). Blocks in other languages (FBD, GRAPH, ...) are
  skipped and listed with the reason. Both use the folder tree of `ExportSources`
  (`<exportPath>/<softwarePath>/Program blocks/...` and `PLC data types/...`), so the narrower tools can work on
  the same folder. `OverwritePlcProgram` needs `--allow-write`, replaces objects of the same name in the open
  project (the name and description say so, and point agents to `ImportPlcProgramChanges` for edited files), puts each file into the group
  its folder implies, and imports in rounds: an object that needs another one (a LAD block calling an SCL block,
  an instance data block) is retried until nothing more can be imported; the response reports `Rounds` and the
  files that never succeeded in `Failures`. Prompts of the same names are included. `ExportPlcProgram` takes the
  `--software-path` and `--export-path` presets like every export.

- __`dryRun` first__: `OverwritePlcProgram` and `ImportPlcProgramChanges` default to `dryRun=true`. A call
  without the argument imports nothing and returns `Planned`: every file that would be imported, with its object
  and `create` or `overwrite`. The tool descriptions and the prompts tell the agent to show that list to the user
  and to call again with `dryRun=false` only after the user agrees. A real run is then confirmed by the server
  itself: it opens a confirmation dialog in the client (MCP elicitation) and imports only when the user types
  "yes, import"; declining or cancelling imports nothing, and a client without elicitation is refused. The agent is
  told not to save by itself and to call `SaveProject` only after the user says "save project"; that part is an
  instruction to the agent and not enforced.

- __Dry run lookups per group__: the preview reads each group's object names once instead of looking up every file,
  which made a preview of several hundred objects take minutes. A group that cannot be read reports `unknown`
  instead of `create`, so a failed lookup no longer looks like a new object.

- __`ImportPlcProgramChanges`__: imports only the files below `<exportPath>/<softwarePath>/` that git reports as
  changed since the last commit (modified, staged and new files), instead of the whole tree. Deleted files are
  listed in `Deleted`, files with an open merge conflict in `Ignored`, and `Changed` counts what git reported.
  It needs git on `PATH` and an export folder inside a git repository, and takes the `--export-path` and
  `--software-path` presets. The git access lives in the new namespace `TiaMcpServer.Git` (class `GitRepo`, folder
  `src/TiaMcpServer/Git/`) and only runs `rev-parse` and `status`.

### Changed

- __Breaking: `CompileSoftware` renamed to `CompilePlcSoftware`__: the name now says it compiles the whole
  PLC software, as opposed to the new `CompileCodeBlock`. Behaviour and parameters are unchanged; the tool
  and the prompt both carry the new name and no alias for the old one is kept.

- __`CompileSoftware` returns the full result on compile errors__: it used to throw an exception naming only
  the first 5 errors, discarding the message list it had already built. It now returns `Messages[]`,
  `ErrorCount` and `WarningCount` with `meta.success = false` and does not throw, so every error location
  is available in one call. Clients must check `meta.success` / `ErrorCount` instead of an error result.

- __Breaking: `GetBlockSource` / `GetTypeSource` replaced by one inline reader per kind__: the two tools took
  `format: document | xml` and fell back to XML silently, and "Source" in their name meant source documents
  while every `Export*Source*` tool means compilable external source files. They now follow the names of the
  export tools, one kind each and no fallback, so the caller always knows what it gets:

  | Kind | Block | Type | Counterpart |
  |---|---|---|---|
  | Xml (SimaticML) | `GetXmlBlock` | `GetXmlType` | `ExportXmlBlock` / `ExportXmlType` |
  | Source (`.scl` / `.db` / `.awl` / `.udt`, new) | `GetSourceBlock` | `GetSourceType` | `ExportSourceBlock` / `ExportSourceType` |
  | Documents (`.s7dcl` / `.s7res`) | `GetDocumentsBlock` | `GetDocumentsType` | `ExportAsDocuments` / `ExportTypeAsDocuments` |

  Migration: `GetBlockSource` with `format: document` -> `GetDocumentsBlock`, with `format: xml` ->
  `GetXmlBlock`; same for types. STL and mixed-language blocks, and types before V21, are now rejected by
  the Documents readers with a hint to the XML reader instead of returning XML. `FindInCode` keeps its own
  XML fallback for them. The prompts were renamed the same way.

### Fixed

- __Block and type names with characters that are invalid in a file name__: TIA Portal allows names such as
  `T_Data->HMI` or `GTS_EQ_I/FLower`, but the exports used the name as the file name. On .NET Framework a `>`,
  `<`, `|` or `"` made `Path.Combine` throw, and in `ExportXmlBlocks` / `ExportXmlTypes` that happened before the
  per-object error handling, so one such object aborted the whole batch. A `/` silently wrote the file into an
  extra subfolder. The XML, document and generated-source exports, including the scratch exports behind the
  `GetXml*` and source readers, now replace invalid characters with `_` (`T_Data-_HMI.xml`). Valid names keep
  their file name. Two objects that would share one file name are no longer written over each other: the
  second is reported as a failure of the export. Reported on GitHub, with a fix in p-7347/tiaportal-mcp@240aaa4.
- __`ImportXmlBlock` / `ImportXmlType` hid TIA Portal's reason__: both caught every exception and returned
  `false`, so the tool only said "Failed importing". The error now carries the messages of the whole inner
  exception chain, for example "The elements must be sorted according to the current flow" with the UId of
  the part, and for that message a hint to put each new `<Part>` directly after the part that feeds it. A
  missing file, group or open project is reported as such instead of as a failed import. Reported on GitHub
  as #37, with fixes in p-7347/tiaportal-mcp@51d2ed9 and @178e631.
- __Four import tools ignored read-only mode__: `ImportXmlBlock`, `ImportXmlType`, `ImportFromDocuments` and
  `ImportBlocksFromDocuments` change the project (the XML imports replace an existing object) but were neither
  marked `[WriteTool]` nor checked `WritePolicy.EnsureEnabled`, so they were listed and ran without
  `--allow-write`. They now behave like the other import tools: hidden from `tools/list` and refused when
  called directly. Reported on GitHub with a fix in p-7347/tiaportal-mcp@51e759a.
- __Devices in "Ungrouped devices" were invisible__: `GetDevices` only walked the top-level devices and the
  user groups, never the project's `UngroupedDevicesGroup` (a system group), so GSD based IO devices,
  SCALANCE switches, ET200SP/eco stations and the like were missing from every device list. The group is
  now included, and `regexName` is applied to all devices, also the top-level ones.
- __Device names containing `/`__: `GetDeviceInfo` and the other device lookups split the path on `/`
  immediately, so a station such as `S7-1500/ET200MP station_1`, copied from `GetProjectTree`, was reported
  as "Device not found". The whole input is now tried as a device name first (top level and ungrouped), and
  inside a group the remaining segments are tried as one name.
- __`GetTypeSource` with `format: xml`__: the type XML export was called with `typePath` and the scratch
  directory swapped, so the XML fallback for types without a source document could not work.
- __`ExportSourceBlocks` / `ExportSourceTypes` filter by name__: both tools now take an optional `regexName`
  (also on the prompts) and apply it while collecting the objects. Before, only `ExportSources` could filter,
  and it always exports blocks and types together, so a request like "export all blocks matching `.+HMI_.+`"
  ended in exporting everything to a temporary folder and copying the matching files.
- __`OpenProject` / `OpenTiaProject` reuse an already open project__: the tool used to close the current
  project before looking for the requested one, so an open project was always closed and reopened. The
  lookup now runs first and matches by file path (not by name), so a project already open in the TIA Portal
  instance, also one opened in the UI, is used as it is; the response says so. The same applies to local
  sessions (`.alsXX`). Another project is still closed only when a different one has to be opened.

### Removed

- __Breaking: the `ExportAll*Flattened` / `ExportAll*Structured` prompts__: `ExportAllBlocksFlattened`,
  `ExportAllBlocksStructured`, `ExportAllTypesFlattened`, `ExportAllTypesStructured`,
  `ExportAllBlocksAsDocumentsFlattened`, `ExportAllBlocksAsDocumentsStructured` and
  `ExportAllTypesAsDocumentsStructured`. They only called `ExportXmlBlocks`, `ExportXmlTypes`,
  `ExportBlocksAsDocuments` or `ExportTypesAsDocuments` with an empty `regexName` and a fixed
  `preservePath`, so nothing is lost: use those prompts (or tools) and set `preservePath` yourself.
  No tools were affected.

### Changed

- __BREAKING: exports and imports use `<exportPath>/<softwarePath>/...`__: every export and every
  folder-based import now works below a folder named after the PLC software, so several PLCs of one
  project no longer share `Program blocks`, `PLC data types` and `PLC tags`. Each `/` of `softwarePath`
  is a folder level (`Group1/PLC_1` gives `<exportPath>/Group1/PLC_1/Program blocks/...`). `exportPath`
  and `importPath` are therefore the base directory, shared by all PLCs; with `--export-path` set once,
  nothing else changes for the caller. Affects the XML, document and source exports
  (`ExportXml*`, `Export*AsDocuments`, `ExportSource*`, `ExportSources`, `ExportPlcAsDocuments`),
  the directory imports (`Import*FromDocuments`, `ImportSources`, `ImportSourceBlocks`,
  `ImportSourceTypes`), `PreviewImport` and the relative `importPath` of `ImportXmlTagTable`.
  Imports that take the full path of one file (`ImportXmlBlock`, `ImportXmlType`, `ImportSourceBlock`,
  `ImportSourceType`, `ImportWatchTable`) are unchanged. Existing exports must be moved into the new
  folder. A `softwarePath` segment of `.` or `..`, or with characters that are not valid in a folder name,
  is rejected with `InvalidParams`. The document imports read the files directly in
  `<importPath>/<softwarePath>`, so they match the flat export layout; a `preservePath` document export
  has its files in group subfolders and is not read back by them.
- __`Portal` partial classes consolidated__: the 18 `Portal.*.cs` files are now `Portal.cs`,
  `Portal.Blocks.cs`, `Portal.Types.cs`, `Portal.Tags.cs` and `Portal.Devices.cs`, plus two folders:
  `Siemens/Documents/` (`Portal.Documents.*.cs`, six files) and `Siemens/Software/`
  (`Portal.Software.*.cs`, seven files). Members were moved verbatim; no signature, visibility or
  behaviour changes. The class-level notes of the retired files are kept as comments above their
  former sections.

- __`McpServer` partial classes follow the same layout__: the ten `McpServer.*.cs` partials are now
  `McpServer.cs`, `McpServer.Devices.cs`, `McpServer.Blocks.cs`, `McpServer.Types.cs` and
  `McpServer.Tags.cs`, plus `ModelContextProtocol/Documents/` (`McpServer.Documents.*.cs`) and
  `ModelContextProtocol/Software/` (`McpServer.Software.*.cs`). Tool names, attributes and behaviour are
  unchanged.
- __Document and source methods regrouped__: `Portal` and `McpServer` now have `Portal.Documents.cs` /
  `McpServer.Documents.cs` (the `...AsDocuments` exports and the `...FromDocuments` imports) and
  `Portal.Sources.cs` / `McpServer.Sources.cs` (every source file method, including `GetBlockSource` and
  `GetTypeSource`, which moved out of the Blocks/Types partials). The `Documents/` subfolders are gone.
  Members were moved verbatim.
- __`McpServerWrite` merged into `McpServer`__: the project-mutating tools now live in the matching
  `McpServer` partials (`McpServer.Blocks.cs`, `.Types.cs`, `.Tags.cs`, `Documents/McpServer.Documents.*.cs`),
  and the shared write plumbing (`Guarded`, `SaveHint`, response builders) in `McpServer.cs`. Tool
  names, attributes and behaviour are unchanged. The `--allow-write` gate is preserved: the 40 write tools
  carry the new `[WriteTool]` attribute and `Program.BuildTools` registers them only under
  `--allow-write`, so they stay out of `tools/list` as before. `WritePolicy.EnsureEnabled` still runs
  first in every write tool. The write-side `Join` helper is now `JoinPath` (it does not trim a
  trailing slash, unlike the preview `Join`). New tests: `Test7ToolRegistration`.
- __Breaking: XML tools renamed__. The tools and the `Portal` methods that read and write TIA Portal
  XML files now say so in their name: `ExportBlock` to `ExportXmlBlock`, `ExportBlocks` to
  `ExportXmlBlocks`, `ImportBlock` to `ImportXmlBlock`, `ExportType` to `ExportXmlType`, `ExportTypes` to
  `ExportXmlTypes` and `ImportType` to `ImportXmlType`. Parameters and results are unchanged. MCP clients,
  prompts and permission allow-lists that name the old tools must be updated. The prompts
  `ExportBlocks` and `ExportTypes` are renamed to `ExportXmlBlocks` and `ExportXmlTypes` as well.
  Likewise the source file exports follow the Export naming: `GenerateBlockSource` to
  `ExportSourceBlock`, `GenerateTypeSource` to `ExportSourceType` and `GenerateSources` to
  `ExportSources` (tools, prompts and `Portal` methods; `ImportSources` keeps its name; the
  tool title is now "Export sources"). `GenerateBlocksFromSource` is now
  `ImportExternalSource` (tool, prompt and `Portal` method; the Siemens API method of the same name is
  unaffected). It compiles a source that is already registered in the project; the name `ImportSourceBlocks`
  now belongs to the new folder import described under Added.
  The tag and watch table exports follow suit: `ExportTagTable` is now `ExportXmlTagTable` and
  `ExportWatchTable` is now `ExportXmlWatchTable` (tool, response type, `Portal` method and
  prompt).

### Added

- __Source file imports that mirror the exports__: `ImportSourceBlock`, `ImportSourceType`,
  `ImportSourceBlocks` and `ImportSourceTypes` (tools, prompts and `Portal` methods) complete the set next to
  the existing `ImportSources`, one to one with `ExportSourceBlock`, `ExportSourceType`,
  `ExportSourceBlocks`, `ExportSourceTypes` and `ExportSources`. The single-file tools take a target group and the
  file path, like `ImportXmlBlock` and `ImportXmlType`; the folder tools take a group, a folder, an optional
  `regexName` and `preservePath`, which reads the subfolders into the matching subgroups. All of them take
  `keepOnError`, return the same result as `ImportSources` and are `[WriteTool]`s. `ImportSources` now shares
  its walk with them; its behaviour is unchanged. New tests: `Test_495` to `Test_499` in `Test4Software`.
- __`ExportXmlTagTable` prompt__: a prompt template for exporting a PLC tag table to XML through the
  `ExportXmlTagTable` tool, next to the existing block and type export prompts. Likewise prompts for the source
  file tools: `ExportSourceBlock`, `ExportSourceType`, `ImportSources` and `ImportExternalSource`, plus the
  convenience prompts `ImportAllSources` and `ImportAllSourcesKeepOnError`. Every other tool now has a prompt
  of the same name as well (80 prompts, generated from the tool descriptions and parameter descriptions), so
  all 99 tools are reachable as prompts.

## [0.3.0] - 2026-09-11

Generate TIA Portal external source files - the format the compiler reads back - from blocks and
PLC data types. The read tool surface grows from 56 to 59.

### Added

- __`GenerateBlockSource`__: writes one program block as an external source file. The extension
  is dictated by the object, not chosen by the caller: `.db` for data blocks, `.awl` for STL
  blocks, `.scl` for SCL blocks. Openness throws on a mismatch, so the mapping is derived rather
  than passed in. Data blocks are recognised by type instead of by `ProgrammingLanguage`, because
  the DB family spans several language values (`DB`, `CPU_DB`, `F_DB`, `Motion_DB`) that all
  write `.db`.
- __`GenerateTypeSource`__: writes one PLC data type as a `*.udt` external source file. Unlike
  `GetTypeSource` and `ExportTypeAsDocuments` this needs no TIA Portal V21, and the result can be
  imported again.
- __`GenerateSources`__: the comfort function - writes every block and PLC data type of one PLC
  software into a folder tree that mirrors the project groups, `<exportPath>/Program blocks/...`
  and `<exportPath>/PLC data types/...`, one file per object. Accepts `regexName` to narrow the
  set. This is the compilable counterpart to `ExportPlcAsSourceTree`, which snapshots the same
  tree as source documents and XML.
- All three take `withDependencies`, which maps to `GenerateOptions.WithDependencies` and pulls
  every object the subject uses - called blocks, instance DBs, UDTs - into the same file, so it
  compiles on its own. Off by default, which keeps one object per file and therefore a tree that
  diffs cleanly.

### Notes

- Openness generates sources only from data blocks and STL or SCL blocks. LAD, FBD, GRAPH and the
  rest have no textual form: the single-object tools reject them with the reason and a pointer to
  `ExportBlock` or `ExportAsDocuments`, and `GenerateSources` reports them in `Skipped` rather
  than failing the whole run. Inconsistent and know-how protected objects are handled the same
  way.
- Openness treats an existing file at the target path as an error, not an overwrite. Every other
  exporter in this server overwrites, so the target file is deleted first and a second run
  succeeds instead of failing.
- Filesystem only: generating a source does not modify the project, so these tools are not gated
  behind `--allow-write`. They are marked destructive, like the `Export*` tools, because they
  overwrite files.
- New files: `Siemens/Portal.GenerateSource.cs` and
  `ModelContextProtocol/McpServer.GenerateSource.cs`, kept apart from the `*.Source.cs` pair,
  which deals in source documents and SimaticML rather than in compilable sources.

### Added (write side)

- __`ImportSources`__: the bulk-import counterpart to `GenerateSources`. Walks a folder tree of
  `.db`/`.awl`/`.scl`/`.udt` files and compiles each back into a block or PLC data type, placed
  into the group its folder path implies - the same layout `GenerateSources` writes. Requires
  `--allow-write`, since unlike the `Generate*` tools this mutates the project.
- Each file goes through the two Openness steps there is no shortcut around: register it as a
  scratch `PlcExternalSource` (reusing the existing `CreateExternalSourceFromFile`), call
  `PlcExternalSource.GenerateBlocksFromSource` - which returns a mix of `PlcBlock` and `PlcType`
  objects from a single file, since the source's own content decides what comes out - then
  delete the scratch source again (reusing `DeleteExternalSource`), regardless of outcome.
- `keepOnError` maps to `GenerateBlockOption.KeepOnError`: successfully generated objects from a
  file are kept even when others in the same file fail. Openness gives up per-object
  success/failure reporting in that mode, which is a limitation of the underlying API, not of
  this wrapper.
- A folder whose implied group does not exist in the project fails that one file (reported in
  `Failures`) rather than being created automatically; the group structure is expected to
  already exist, since these files were themselves generated from objects that lived in it.
- New files: `Siemens/Portal.ImportSources.cs` and
  `ModelContextProtocol/McpServerWrite.ImportSources.cs`. Reuses
  `GetPlcBlockGroupByPath`/`GetPlcTypeGroupByPath` and the private `StripSystemRootSegment`
  helper (`Portal.Documents.cs`) rather than re-deriving the same "does this folder name match
  the localized system root" logic a third time.

## [0.2.0] - 2026-09-10

Complete the PLC software area of the Openness API: tags, constants, watch and force tables,
external sources, cross references, and create/rename/delete/move for blocks and types. The tool
surface grows from 31 tools to 44 read tools plus 37 project-mutating tools.

### Added

- __Write mode__, opt-in through the new `--allow-write` command line argument. The 37
  project-mutating tools live in a separate `McpServerWrite` tool type that is only registered
  when the flag is present, so without it they are absent from `tools/list` rather than merely
  refused when called. `WritePolicy.EnsureEnabled` additionally guards every write tool at
  runtime, because these are `public static` methods that the test suite invokes directly and
  that path bypasses tool registration. Filesystem-only exports are deliberately not gated: they
  never modify the project. Both `GetState` and the `--doctor` report now show `AllowWrite`.
- __PLC tags and constants__: `GetTagTables`, `GetTagTableInfo`, `GetTags`, `GetTagInfo`,
  `GetConstants` (user and/or system) and `ExportTagTable`. Write side: `CreateTagTable`,
  `DeleteTagTable`, `RenameTagTable`, `CreateTagTableGroup`, `DeleteTagTableGroup`,
  `ImportTagTable`, `CreateTag`, `UpdateTag`, `DeleteTag`, `CreateUserConstant`,
  `UpdateUserConstant`, `DeleteUserConstant`.
- __Watch and force tables__: `GetWatchTables`, `GetWatchTableInfo` (including entries),
  `GetForceTables` and `ExportWatchTable`. Write side: `CreateWatchTable`, `RenameWatchTable`,
  `DeleteWatchTable`, `CreateWatchTableGroup`, `DeleteWatchTableGroup`, `ImportWatchTable`.
- __External source files__: `GetExternalSources`, `GetExternalSourceInfo`,
  `CreateExternalSourceFromFile`, `DeleteExternalSource`, `CreateExternalSourceGroup`,
  `DeleteExternalSourceGroup` and `GenerateBlocksFromSource`.
- __Cross references__: `GetCrossReferences` for a whole PLC software or for one block, type,
  tag table, tag or block group. Because `Sources -> References -> Locations` nests three deep
  and source children recurse, the tool takes `maxDepth` (default 1, maximum 3) and reports a
  `Truncated` flag with counts instead of returning megabytes.
- __Blocks and types__: `CreateBlockGroup`, `DeleteBlockGroup`, `CreateTypeGroup`,
  `DeleteTypeGroup`, `DeleteBlock`, `RenameBlock`, `DeleteType`, `RenameType`, `CreateFB`,
  `CreateInstanceDB`, plus `CopyBlock`, `MoveBlock`, `CopyType` and `MoveType`.
- `Portal.GetTypePath(PlcType)`, the counterpart to `GetBlockPath(PlcBlock)`.
- `Operation.Run`, the single exception-decoration point that `docs/error-model.md` prescribes.
  It wraps a non-`PortalException` into one, stamps context into `Exception.Data`, logs once
  (nested calls do not re-log) and rethrows. It also serializes all Openness traffic behind a
  `Monitor`; a `SemaphoreSlim` would self-deadlock, because portal methods call one another.
- `PortalErrorCode` gains `ImportFailed`, `CreateFailed`, `DeleteFailed`, `RenameFailed`,
  `NotSupported` and `WriteDisabled`. Existing members keep their order and values.

- __PLC data types as SIMATIC Source Documents__ (TIA Portal V21+): `ExportTypeAsDocuments` and
  `ExportTypesAsDocuments`, plus the write-gated `ImportTypeFromDocuments` and
  `ImportTypesFromDocuments`. Until now only program blocks could be written as documents - the
  readable, git-diffable form where `<Name>.s7dcl` holds the SCL/LAD source text and the
  optional `<Name>.s7res` the comments; every other export produced SimaticML XML. Openness only
  added `PlcType.ExportAsDocuments` and `PlcTypeComposition.ImportFromDocuments` in V21, hence
  the higher version gate than the V20 block tools. Tag tables and watch tables have no document
  API in V21 and remain XML-only.
  File names come from TIA Portal rather than from a hardcoded extension: an export reports the
  files it actually wrote (`DocumentExportResult.ExportedDocuments`, unioned with what is on
  disk) and a batch import discovers a document set by base name across a known extension set.
  Deleting a previous export stays restricted to `.s7dcl`/`.s7res`, so a hand-written `.scl` or
  `.udt` in the same directory is never removed.
  Unlike `ExportBlocksAsDocuments`, which only logs its failures, the bulk type export returns
  its inconsistent and failed types so the response can name them. The two type import tools sit
  in the `--allow-write` surface, where project-mutating tools belong; the older
  `ImportFromDocuments` and `ImportBlocksFromDocuments` remain ungated, which is a known
  inconsistency in those block tools rather than a pattern the new tools follow.

- __Ten comfort tools__ that shorten the path between a question and an answer. All read-only
  except where noted, and none change an existing tool's contract.
  - `GetBlockSource` / `GetTypeSource` return an object's source text inline instead of making
    the caller export a file and open it. Openness has no in-memory block body, so the readers
    export into a temp scratch directory and remove it again. STL and mixed-language blocks
    have no SIMATIC Source Document at all, so those fall back to XML and say so in `Format`.
  - `GetBlockInterface` lists a data block's members from `DataBlock.Interface` without any
    export, and works on inconsistent blocks. Data blocks only - V21 Openness offers no
    interface accessor for FB, FC or OB.
  - `FindInCode` searches the program text with a regular expression. Every other filter in the
    server matches object names only.
  - `CompileSoftware` now returns the whole `CompilerResult.Messages` tree flattened to
    `{path, state, description}` with error and warning counts, instead of one stringified
    sentence. Warnings are a successful compile with detail; only errors fail the call.
  - `GetPlcSummary` replaces six discovery calls: counts per area, a programming-language
    histogram, and the inconsistent and know-how-protected objects.
  - `WhereUsed` answers "what uses this?" from a bare name, flattening the cross-reference tree.
  - `ResolveObjectPath` turns a bare or partial name into the root-relative path the other tools
    need, across all six object areas. The `Path` property is also no longer commented out on
    `ResponseBlockInfo` and `ResponseTypeInfo`, so `GetBlocks` and `GetTypes` finally return
    round-trippable paths - exporting one type is now one call instead of three.
  - `OpenTiaProject` connects, opens and returns the device and PLC software paths in one call.
  - `ExportPlcAsSourceTree` snapshots a whole PLC to a git-ready folder tree in one call.
  - `PreviewImport` reports what an import would create, overwrite or collide with, without
    touching the project - including the PLC-global data type name rule.
- __Atomic, undoable writes.__ Every one of the write tools now runs inside
  `ExclusiveAccess.Transaction`, so a tool call commits as a unit and appears in the TIA Portal
  undo stack as one named entry. A body that throws rolls back instead of leaving the project
  half-edited (verified against a live V21). If TIA Portal refuses exclusive access the write
  still runs unwrapped, so this can never turn a working write into a failure.

### Changed

- `GetSoftwareTree` renders three further sections - PLC tags, watch and force tables, and
  external source files - and takes a `sections` argument accepting any comma separated subset
  of `blocks,types,tags,watch,sources` (default `all`) so the output stays manageable on a large
  PLC. Existing single-argument callers are unaffected.
- `Portal` and `McpServer` are now `partial` and split by area (`Portal.Tags.cs`,
  `Portal.WatchTables.cs`, `Portal.MoveCopy.cs`, `McpServer.Tags.cs`, ...). The generic path
  helpers in `Portal.Resolve.cs` (`WalkGroups`, `BuildGroupPath`, `WalkRecursive`) now back the
  existing block and type resolvers as well, so all five group hierarchies share one traversal.
- `Diagnostics.Run` takes an optional `bool allowWrite`. The flag is passed in rather than read
  from `WritePolicy`, so the Siemens layer keeps no dependency on the MCP layer.
- 27 read tools and all 37 write tools publish an `outputSchema` and return `structuredContent`
  (previously 13).

### Fixed

- The software tree omitted external source files even though the `GetSoftwareTree` description
  had always promised them.
- `GetBlockPath` returned paths prefixed with the `Program blocks` system group, which
  `GetBlock` then rejected - so the "Did you mean ...?" suggestions on a failed `ExportBlock`
  named paths that could not be used. Path building now takes an `includeSystemRoot` flag:
  suggestions are root-relative and round-trip, while `preservePath` exports keep the existing
  on-disk layout unchanged.
- `ExportTagTable` with `preservePath` wrote the group structure directly below `exportPath`,
  while block and type exports place theirs below the system folder (`Program blocks`,
  `PLC data types`). Tag tables now land in `<exportPath>/PLC tags/...`, using the system group
  name as TIA Portal reports it in the current interface language. `ImportTagTable` accepts a
  leading `PLC tags` segment in `groupPath` so the exported layout can be fed straight back.
- `--doctor --allow-write` reported write mode as disabled. `WritePolicy.AllowWrite` was only
  assigned inside `RunStdioHost`, which `--doctor` returns before reaching; it is now set in
  `Main`.

### Known gaps

- `CreateWatchTableEntry` and `DeleteWatchTableEntry` are not implemented. `PlcWatchTable.Entries`
  is a `PlcTableCommentEntryComposition` whose only typed creator produces a comment row; a real
  entry requires the untyped `IEngineeringComposition.Create(typeof(PlcWatchTableEntry), ...)`
  path, whose required attribute names must first be read from `GetCreationInfos()` against a
  live project rather than guessed.
- Openness offers no move or copy operation for blocks and types, so `CopyBlock`, `MoveBlock`,
  `CopyType` and `MoveType` are composed from export, import and - for a move - deleting the
  source after the import succeeds. Two consequences are visible to callers: the object must be
  consistent, and its block number travels with it, so importing into the same PLC can collide.

## [0.1.0] - 2026-09-07

Upgrade to the current MCP .NET SDK and adopt the newer protocol surface.

### Breaking

- Tool failures are no longer JSON-RPC errors. `ModelContextProtocol` 2.x turns an
  `McpException` thrown from a tool into a `CallToolResult` with `isError: true`, carrying
  the message as text content. Clients that inspected JSON-RPC `error.code` (`InvalidParams`,
  `InternalError`, ...) must read the tool result's `isError` flag and message instead.
  `McpErrorCode` was removed from all 75 throw sites; error codes now live only on the
  derived `McpProtocolException`, which is reserved for protocol-level faults.

### Added

- Environment diagnostics ('doctor'), available two ways: the `--doctor` command line argument
  prints a report and exits without starting the MCP server, and the new `Doctor` tool returns the
  same report plus structured content to MCP clients. Both report the connection state, the open
  project, the active TIA major version, every installed TIA Portal version >= V21 (with a check
  that its Openness assemblies and the Portal executable are present), and membership in the
  `Siemens TIA Openness` user group. Read-only throughout: unlike `Openness.IsUserInGroup`, the
  new `Openness.CheckUserInGroup` never adds the user to the group.
- `Engineering.GetTiaPortalInstallPath(int)` resolves the install path of any TIA major version and
  no longer depends on the `TIAP{version}\TIA_Opns` registry sub key alone - installations that do
  not write that key are now found via their other product sub keys, which carry the same path.

### Changed

- Siemens TIA Portal Openness updated to V21. `Siemens.Collaboration.Net.TiaPortal.Packages.Openness` 20.0.1744190253 -> 21.0.1765349347 and
  `Siemens.Collaboration.Net.TiaPortal.Openness.Resolver` 1.1.1725480302 -> 2.0.1765367256. The default
  TIA major version is now 21 (`Openness.Initialize`, `Program.Main`); older versions remain reachable via
  `--tia-major-version`. `Engineering.Resolver` now also excludes `V21` directories when targeting another
  version, and the test `App.config` probing path points at `Portal V21`. A machine with TIA Portal V21
  Openness installed is now required to build.
- `ModelContextProtocol` 0.3.0-preview.4 -> 2.2.0; `Microsoft.Extensions.Hosting`
  10.0.0-preview.4 -> 10.0.10. Negotiated protocol revisions are now `2024-11-05`,
  `2025-03-26`, `2025-06-18` and `2025-11-25`.
- `initialize` now reports `serverInfo` (name, title, version) and `instructions` telling the
  client to call `Connect` and `OpenProject` first and how to discover path arguments.
- Progress reporting for `ExportBlocks`, `ExportTypes`, `ExportBlocksAsDocuments` and
  `ImportBlocksFromDocuments` moved from hand-rolled `notifications/progress` calls to the
  SDK-injected `IProgress<ProgressNotificationValue>`. The removed `IMcpServer` and
  `RequestContext<CallToolRequestParams>` parameters were SDK-injected and never part of the
  tool input schemas, so the wire-visible schemas are unchanged. Progress notifications are
  emitted only when the client supplies a `progressToken`, as before; the non-standard
  `Error` field previously sent on failure notifications is gone.
- Tools and prompts are registered explicitly (`WithTools`/`WithPrompts`) instead of by
  assembly scanning, giving a stable `tools/list` order.

### Added

- All 30 tools carry a `title` and behaviour annotations: 13 read-only `Get*` tools are
  `readOnlyHint: true`; project mutations (`SaveProject`, `SaveAsProject`, `CloseProject`),
  all export tools (they overwrite files already present at the target path) and all import tools
  are `destructiveHint: true`. Everything is `openWorldHint: false`.
- The 13 read-only tools publish an `outputSchema` and return `structuredContent`.

### Removed

- `src/TiaMcpServer/packages.config` — a stale packages.config-era leftover pinning
  `ModelContextProtocol` 0.2.0-preview.1. The project has used SDK-style `PackageReference`
  for some time and the build ignored this file.

## [0.0.16] - 2025-09-02

- New: ImportFromDocuments and ImportBlocksFromDocuments (V20+)
- Guard: Version checks for export/import as documents (V20+)
- UX: Pre-check .s7res for missing en-US tags; warnings surfaced in responses
- Docs: README updates, prompts note V20+ and known LAD en-US limitation
- Refactor: Updated all McpException throws to SDK signature with McpErrorCode
- Chore: Added TODOs for tests/docs

## [0.0.15] - 2025-08-30

- prompts improved
- long running tasks as async tasks

## [0.0.14] - 2025-08-18

- better structure/tree format
- new GetSoftwareTree()
- bugfixes

## [0.0.13] - 2025-08-14

- logging integrated
- prompts added

## [0.0.12] - 2025-08-07

- export path fixed

## [0.0.11] - 2025-08-07

- project structure formatted as markdown code

## [0.0.10] - 2025-08-07

- tool responses improved

## [0.0.9] - 2025-08-04

- export of blocks and types with 'preservePath' option
- new tools
- some infos with attributes

## [0.0.8] - 2025-08-01

- improved jsonrpc responses
- updated dependencies

## [0.0.7] - 2025-07-18

- new GetState()
- return values fixed

## [0.0.6] - 2025-07-16

- refactored code to use new TIA Portal API
- only blocks (OB/FB/FC/DB) and types (UDT) are now retrieved from the PLC software
- use regex to filter blocks and types
- import of blocks and types to PLC software

## [0.0.5] - 2025-07-11

- locating of plc software by softwarePath. This makes it possible to access plc software in groups/subgroups
- new tool: retrieving of project structure as text
- new tool: compile plc software

## [0.0.4] - 2025-06-30

- opens local session or projects, depending on project file extension

## [0.0.3] - 2025-06-23

- Release on Visual Studio Code Narketplace

