# Change Log

## [Unreleased]

- New: `--transport http` MVP. Hosts an `HttpListener` on `--http-prefix` (default `http://127.0.0.1:8765/`); bridges `POST /mcp` requests into the MCP SDK via a `System.IO.Pipelines` pair backing `WithStreamServerTransport`. Optional `--http-api-key <secret>` enforces the `X-API-Key` header.
- New: Tag tools `GetTagTables`, `GetTags`, `ExportTagTable` for the `Siemens.Engineering.SW.Tags` surface. Follows the established `GetBlocks` / `ExportBlock` shape and canonical `PortalException` template.
- New: Singular `GetProject` tool that returns info + attributes for the *active* project. The previous `GetProject` tool (which actually returned a list) is renamed to `GetProjects` to match its behavior. **Breaking change** for any client calling the old `GetProject` expecting list-of-projects shape.
- Fix: `Helper.GetAttributeList` is now resilient: per-attribute try/catch around `obj.GetAttribute(name)`, plus value normalization (Enum → name, MultilingualText → first non-empty translation, COM-wrapped types → `.ToString()`) so System.Text.Json no longer chokes at the SDK boundary. Resolves the persistent `-32603 "An error occurred."` from `GetProject` / `GetProjects` / `GetDevices`.
- Fix: HTTP bridge bounds the SDK response read with a 60s linked-CTS timeout; malformed but JSON-parseable requests (e.g. missing `jsonrpc` field) used to permanently lock the `SemaphoreSlim` gate and DoS the server. They now return `504 Gateway Timeout` and the server stays responsive.
- Fix: Defensive guards in `Portal.GetDevices`, `Portal.GetProjects`, `Portal.GetSessions` — a single broken/transitioning item is logged and skipped instead of aborting the whole listing.
- Refactor: `Portal.GetProjects`, `GetSessions`, and `GetDevices` now throw `PortalException(InvalidState)` on fundamental failure ("Not attached to TIA Portal" / "No project is open") instead of silently returning empty. McpServer maps these to clean `McpException(InvalidParams)`.
- New helper: `Portal.GetCurrentProject()` returns the active `ProjectBase` (preferring `_project`, falling back to `_session?.Project`), throwing `PortalException(InvalidState)` if none.

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

