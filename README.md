# TIA-Portal MCP-Server

A MCP server which connects to Siemens TIA Portal.

## Features

- Connect to a TIA Portal instance
- Browse and interact with TIA Portal projects
- Perform basic project operations from within VS Code
- Inspect PLC tag tables and tags (`GetTagTables`, `GetTags`, `ExportTagTable`)
- Choose `stdio` or `http` transport (see [Transports](#transports))

## Requirements

- __.net Framework 4.8__ installed
- __Siemens TIA Portal V20__ installed and running on your machine
- Check if under `Environment Variables/User variable for user <name>` the variable `TiaPortalLocation` is set to `C:\Program Files\Siemens\Automation\Portal V20`
- User must be in Windows User Group `Siemens TIA Openness`

## TIA-Portal Versions

- __V20__ is the default version.
- Previous versions are also supported, but must use the `--tia-major-version` argument to specify the version.
- Export as documents (.s7dcl/.s7res) via `ExportAsDocuments`/`ExportBlocksAsDocuments` requires TIA Portal V20 or newer.
- Import from documents (.s7dcl/.s7res) via `ImportFromDocuments`/`ImportBlocksFromDocuments` also requires TIA Portal V20 or newer.

## Known Limitations

- As of 2025-09-02: Importing Ladder (LAD) blocks from SIMATIC SD documents requires the companion `.s7res` file to contain en-US tags for all items; otherwise import may fail. This is a known limitation/bug in TIA Portal Openness.
 - `ExportBlock` requires a fully qualified `blockPath` like `Group/Subgroup/Name`. If only a name is provided, the MCP server returns `InvalidParams` and may include suggestions for likely full paths.

## Testing

- See `tests/TiaMcpServer.Test/README.md` for environment prerequisites and test asset setup.
- Standard command: `dotnet test` (run from the repo root).
- Test execution policy: offer to run tests, but only execute after explicit user confirmation. Details in `AGENTS.md`.

## Contributing

- See `agents.md` for guidance on working with agentic assistants and the test execution policy (offer to run tests only with explicit user confirmation).

## Error Handling (ExportBlock)

- The Portal layer throws `PortalException` with a short message and `PortalErrorCode` (e.g., NotFound, ExportFailed), and attaches `softwarePath`, `blockPath`, `exportPath` in `Exception.Data` while preserving `InnerException` on export failures.
- The MCP layer maps these to `McpException` codes. For `ExportFailed`, it includes a concise reason from the underlying error; for `NotFound`, it returns `InvalidParams` and may suggest likely full block paths if a bare name was provided.
- Consistency required: TIA Portal never exports inconsistent blocks/types. Single export returns `InvalidParams` with a message to compile first. Bulk export skips inconsistent items and returns them in an `Inconsistent` list alongside `Items`.
- Standardization: Exception context metadata is attached in a single catch per portal method right before rethrow, not at inline throw sites. See `docs/error-model.md`.
- This standardized pattern currently applies to `ExportBlock`, `ExportTagTable`, `GetProjects`, `GetProject`, and `GetDevices`. Rollout to remaining methods is incremental.

## Transports

- `stdio` (default)
  - Program wires `AddMcpServer().WithStdioServerTransport()`.
  - For stdio, logs must go to stderr to avoid corrupting JSON-RPC.
- `http` (MVP, loopback)
  - Select with `--transport http`. Defaults: `--http-prefix http://127.0.0.1:8765/`, no auth.
  - Optional `--http-api-key <secret>` enforces the `X-API-Key` request header (401 on mismatch).
  - Endpoint: `POST /mcp` with `Content-Type: application/json`. Each request flows through a `System.IO.Pipelines` bridge into the SDK's `StreamServerTransport`; the MCP session is persistent across HTTP requests so attached state (Portal singleton, current project) survives.
  - Status codes: 200 OK on response; 202 Accepted on JSON-RPC notifications; 400 on malformed JSON; 401 on bad/missing API key; 404 on wrong path; 405 on non-POST; 504 if the SDK takes longer than 60s to respond; 500 on unexpected errors.
  - Example: `curl -X POST -H 'Content-Type: application/json' -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}' http://127.0.0.1:8765/mcp`
- `stream` (custom streams)
  - The SDK exposes `WithStreamServerTransport(Stream input, Stream output)`. Used internally by the HTTP bridge; not exposed as a separate CLI mode.
- Follow-ups tracked in TODO.md: align with MCP Streamable HTTP spec (`Mcp-Session-Id`, SSE for server-to-client notifications/requests), and prefer the SDK's HTTP transport if/when it ships for net48.

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
