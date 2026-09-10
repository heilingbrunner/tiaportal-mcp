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
    *   `McpServer.cs` and `McpServer.{Tags,WatchTables,ExternalSources,CrossReferences}.cs`: the read-only tools, as partials of one `McpServer` type.
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
