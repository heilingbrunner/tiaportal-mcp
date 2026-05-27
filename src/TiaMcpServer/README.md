# TiaMcpServer

This document provides a comprehensive overview of the TiaMcpServer project, a C# application that acts as a Model Context Protocol (MCP) server to expose the Siemens TIA Portal API to Large Language Models (LLMs).

## 1. Project Overview

The TiaMcpServer project is a .NET 4.8 console application that enables communication between an LLM and the Siemens TIA Portal. It achieves this by implementing an MCP server that exposes a set of tools for interacting with the TIA Portal. The project is divided into two main parts:

*   **MCP Server:** This part of the project is responsible for handling communication with the LLM. It uses the `ModelContextProtocol` library to create an MCP server that listens for requests from the LLM and executes the corresponding tools.
*   **TIA Portal Interfacing API:** This part of the project is responsible for interacting with the TIA Portal. It uses the Siemens TIA Portal Openness API to perform tasks such as connecting to the TIA Portal, opening and closing projects, and working with devices, blocks, and types.

## 2. Project Structure

The project is organized into the following directories:

*   **`ModelContextProtocol/`**: This directory contains the implementation of the MCP server.
    *   `McpServer.cs`: This file defines the MCP tools that can be called by the LLM.
    *   `McpPrompts.cs`: This file contains the prompts that are used to guide the LLM.
    *   `Responses.cs`: This file defines the response objects that are returned by the MCP tools.
    *   `Types.cs`: This file defines the data types that are used by the MCP server.
*   **`Siemens/`**: This directory contains the implementation of the TIA Portal interfacing API.
    *   `Portal.cs`: This file provides a high-level API for interacting with the TIA Portal.
    *   `State.cs`: This file defines the `State` class, which represents the state of the TIA Portal.
    *   `Openness.cs`: This file provides a wrapper around the Siemens TIA Portal Openness API.
*   **`Properties/`**: This directory contains the project's properties, such as the assembly information and launch settings.

## 3. Architecture

The TiaMcpServer project follows a client-server architecture. The LLM acts as the client, and the TiaMcpServer application acts as the server. The communication between the client and the server is handled by the MCP protocol.

The MCP server is responsible for receiving requests from the LLM, executing the corresponding tools, and returning the results. The tools are implemented as methods in the `McpServer` class. These methods use the TIA Portal interfacing API to interact with the TIA Portal.

The TIA Portal interfacing API is implemented in the `Siemens` directory. This API provides a set of classes and methods for performing common tasks, such as connecting to the TIA Portal, opening and closing projects, and working with devices, blocks, and types.

## 4. Functionality

The TiaMcpServer project provides the following functionality:

*   **Connecting and disconnecting from the TIA Portal:** The `Connect` and `Disconnect` tools allow the LLM to connect to and disconnect from the TIA Portal.
*   **Getting the state of the TIA Portal:** The `GetState` tool allows the LLM to get the current state of the TIA Portal, such as whether it is connected to a project and the name of the project.
*   **Working with projects and sessions:** The `GetProjects` (list all open), `GetProject` (info + attributes for the active project), `OpenProject`, `SaveProject`, `SaveAsProject`, and `CloseProject` tools allow the LLM to work with TIA Portal projects and sessions. **Note:** the listing tool was renamed from `GetProject` to `GetProjects` in the latest revision; the new singular `GetProject` returns the active project specifically.
*   **Working with devices:** The `GetStructure`, `GetDeviceInfo`, `GetDeviceItemInfo`, and `GetDevices` tools allow the LLM to get information about the devices in a project.
*   **Working with PLC software:** The `GetSoftwareInfo` and `CompileSoftware` tools allow the LLM to get information about and compile PLC software.
*   **Working with blocks:** The `GetBlockInfo`, `GetBlocks`, `GetBlocksWithHierarchy`, `ExportBlock`, `ImportBlock`, and `ExportBlocks` tools allow the LLM to work with blocks.
    - `ExportBlock` expects `blockPath` to be a fully qualified path like `Group/Subgroup/Name`. Passing just a name is ambiguous; the MCP layer will return `InvalidParams` and may suggest likely full paths based on project contents.
*   **Working with types:** The `GetTypeInfo`, `GetTypes`, `ExportType`, `ImportType`, and `ExportTypes` tools allow the LLM to work with types.
*   **Working with tag tables:** The `GetTagTables`, `GetTags`, and `ExportTagTable` tools allow the LLM to inspect and export PLC tag tables and the tags inside them. `tagTablePath` is `Group/Subgroup/Name` (single name allowed at root). `ExportTagTable` follows the same `PortalException` template as `ExportBlock` (no `IsConsistent` gate -- `PlcTagTable` does not expose one).
*   **Exporting blocks as documents (V20+):** The `ExportAsDocuments` and `ExportBlocksAsDocuments` tools export blocks as SIMATIC SD documents (.s7dcl/.s7res). Requires TIA Portal V20 or newer.
*   **Importing blocks from documents (V20+):** The `ImportFromDocuments` and `ImportBlocksFromDocuments` tools import blocks from SIMATIC SD documents into PLC software. Requires TIA Portal V20 or newer.

## 5. Conclusion

The TiaMcpServer project is a powerful tool that allows LLMs to interact with the Siemens TIA Portal. The project is well-structured and easy to understand. The code is well-commented and follows best practices.

## 6. Future Improvements

*   **Session Path Reliability:** The `GetOpenSessions` method has been updated to return the full path of the session project. However, the TIA Portal Openness API's behavior with multiuser sessions can vary. Future testing should confirm the reliability of retrieving the `Path` for all types of local and remote sessions to ensure the information is always accurate.

## Known Issues

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.

## Transports

- `stdio` (default)
  - Hosted with `AddMcpServer().WithStdioServerTransport()`.
  - All logs must go to stderr to keep stdout clean for JSON-RPC.
- `http` (MVP, loopback by default)
  - Selected with `--transport http`. Defaults: `--http-prefix http://127.0.0.1:8765/`, no auth.
  - Optional `--http-api-key <secret>` enforces `X-API-Key` (401 on mismatch).
  - Endpoint: `POST /mcp`, `Content-Type: application/json`. The bridge writes each JSON-RPC frame newline-delimited into a `System.IO.Pipelines` pair backing `WithStreamServerTransport`, then reads the first id-bearing response line and returns it as HTTP 200 (or 202 for client notifications). Per-request budget of 60s caps the response read so a malformed input can't lock the bridge.
  - Single shared MCP session for all HTTP clients — Portal singleton state (current project, current attach) is preserved across requests.
  - Known caveats (see root `TODO.md`):
    - Server-to-client notifications (progress, logging) are not forwarded back to HTTP clients.
    - Server-initiated requests (sampling/createMessage, roots/list) are not supported.
    - One slow tool call blocks all other in-flight HTTP requests (head-of-line). Patch 3 (id demuxer) is the fix.
- `stream` (custom streams)
  - The SDK exposes `WithStreamServerTransport(Stream input, Stream output)`. Used internally by the HTTP bridge; not exposed as a separate CLI mode.

## Error Handling Standard (ExportBlock)

- Portal layer
  - Throws `PortalException` with a short message and `PortalErrorCode`.
  - Attaches context via `Exception.Data` keys: `softwarePath`, `blockPath`, `exportPath`.
  - Preserves the original exception as `InnerException` for `ExportFailed` and logs full details.
- MCP layer
  - Maps `NotFound` to `McpException` with `InvalidParams`. If `blockPath` is a single name, it suggests likely full paths by scanning blocks.
  - Maps `ExportFailed` to `InternalError` and includes a concise reason from `InnerException.Message`.
  - Consistency: TIA Portal does not export inconsistent blocks/types. Single-item exports return `InvalidParams` advising to compile first. Bulk exports skip inconsistent items and include them in an `Inconsistent` list in the response.
  - Keeps user messages concise; structured details live in logs and context.
  - Current standardization is applied to `ExportBlock`, `ExportTagTable`, `GetProjects`, `GetProject`, and `GetDevices`. Rollout to remaining methods is incremental.
  - Exception metadata: Context keys (e.g., `softwarePath`, `blockPath`/`typePath`, `exportPath`) are attached in a single catch per portal method just before rethrow, not at inline throw sites. See `docs/error-model.md`.

## Contributing

- See root `AGENTS.md` for agent guidance and the test execution policy (offer to run tests only with explicit user confirmation).
