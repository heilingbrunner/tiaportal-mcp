# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

TIA Portal MCP Server — a .NET Framework 4.8 Model Context Protocol (MCP) server that bridges LLM-based coding assistants (Claude, Copilot Chat) with Siemens TIA Portal for PLC software development. Exposes 40+ tools and prompt templates via the MCP protocol over stdio transport.

## Build & Test Commands

```powershell
dotnet build                                    # Build entire solution
dotnet build src/TiaMcpServer/TiaMcpServer.csproj  # Build main project only
dotnet test                                     # Run all tests (requires TIA Portal + license)
dotnet run --project src/TiaMcpServer/TiaMcpServer.csproj -- --tia-major-version 20 --logging 1
```

**Test execution policy:** Always offer to run tests and wait for explicit user confirmation before running. Tests require TIA Portal installed, licensed, and user in "Siemens TIA Openness" Windows group. State prerequisites when offering.

## Architecture

Three-layer design:

1. **MCP Layer** (`src/TiaMcpServer/ModelContextProtocol/`): Static methods on `McpServer` class decorated with `[McpServerTool]` attributes. Translates MCP protocol calls to Portal operations. Maps `PortalException` codes to MCP error codes.

2. **Portal Wrapper** (`src/TiaMcpServer/Siemens/Portal.cs`, ~2700 lines): Core wrapper around TIA Portal Openness API. Each method uses a single catch block that attaches metadata (`softwarePath`, `blockPath`, `exportPath`), logs, and rethrows as `PortalException`.

3. **Openness Init** (`src/TiaMcpServer/Siemens/Openness.cs`, `Engineering.cs`): Assembly resolution for TIA Portal V13–V20+. Version-specific configuration via `--tia-major-version` CLI flag.

Entry point: `Program.cs` — parses CLI args (`CliOptions.cs`), initializes Openness, checks group membership, runs stdio MCP host with DI.

## Error Handling Pattern

- `PortalException(code, message, candidates?, innerException?)` with codes: `NotFound`, `InvalidParams`, `InvalidState`, `ExportFailed`
- Single catch-block per Portal method: attach `Exception.Data` metadata, log, rethrow — never inline at throw sites
- MCP mapping: `InvalidParams`/`InvalidState` → MCP `InvalidParams`; `ExportFailed` → MCP `InternalError`
- Bulk operations skip inconsistent items, return them in `Inconsistent` list with counts in `Meta`

## Code Style

- .NET Framework 4.8, C# with latest language features
- Four spaces, opening braces on new line, `PascalCase` public / `camelCase` locals
- File-scoped namespaces (enforced by `.editorconfig`)
- `Microsoft.Extensions.Logging` via DI; logging modes: 1=stderr, 2=Debug, 3=Windows Event Log
- MSTest: `[TestClass]`/`[TestMethod]`, files named `Test<Area>.cs`, serial execution (`[DoNotParallelize]`)

## Encoding & Line Endings

- **Preserve UTF-8 BOM** where present in C# files
- **Preserve CRLF line endings** — required for deploy scripts and Siemens tooling
- Do not modify file encodings

## Environment Requirements

- Windows only (uses Windows groups, registry, Siemens Openness)
- TIA Portal V18–V20+ with Openness license
- User must be in Windows group "Siemens TIA Openness"
- Environment variable `TiaPortalLocation` set to TIA install path
