# Error Model and Exception Metadata

This document standardizes how errors are raised in the Siemens portal layer and mapped to MCP responses, and where exception metadata is attached for consistency and observability.

## Principles

- Clear categories
  - Validation: invalid input, missing resources → `PortalErrorCode.InvalidParams` → `McpException` with a user-guidance message.
  - Invalid state: operation cannot proceed due to project or item state (e.g., inconsistent block/type) → `PortalErrorCode.InvalidState` → `McpException` with guidance on how to proceed.
  - Operation failure: environment/IO/underlying API issues → `PortalErrorCode.ExportFailed` (or similar) → `McpException` with a concise reason.

- Full set of codes

  | Code | Raised when |
  | --- | --- |
  | `NotFound` | A path does not resolve. The message names the tool that lists valid paths. |
  | `InvalidParams` | Caller input is malformed: an empty name, a name containing `/`, a path missing its tag table segment, an unknown enum value. |
  | `InvalidState` | The project or object forbids the operation: no project open, inconsistent block or type, know-how protection. |
  | `ExportFailed` | An export to the file system failed. |
  | `ImportFailed` | An import into the project failed, including block number collisions. |
  | `CreateFailed` | A create operation failed. |
  | `DeleteFailed` | A delete operation failed. |
  | `RenameFailed` | A rename or update operation failed. |
  | `NotSupported` | The Openness API offers no such operation - a system group, the default tag table, a force table, a system constant, or cross references on an object that has none. Distinct from `InvalidParams`: no input would make it succeed. |
  | `WriteDisabled` | Reserved for write-policy refusals. The write tools currently throw `McpException` directly from `WritePolicy.EnsureEnabled`, because the refusal happens above the portal layer. |

  Existing members keep their declaration order, so the numeric values of the original four are unchanged.

- Single decoration point
  - Do not attach `Exception.Data` inline at throw sites.
  - Each public portal method (e.g., `ExportBlock`, `ExportType`) attaches standard context keys in a single catch block just before rethrowing, ensuring uniform metadata on all failures:
    - `softwarePath`
    - `blockPath` / `typePath` (as applicable)
    - `exportPath` (as applicable)

- Consistency requirement (TIA Portal)
  - TIA Portal does not export inconsistent blocks/types (`IsConsistent == false`). Single-item exports throw `InvalidState` with a clear message to compile first. Bulk exports skip inconsistent items and report them in a dedicated list.

## Portal Layer Pattern

Within `src/TiaMcpServer/Siemens/Portal.cs` methods:

- Throw lightweight `PortalException` with an appropriate `Code` from locations that detect an error (validation, not-found, invalid state).
- Use a single `catch (Exception ex)` per method and funnel into the canonical wrapping pattern (see `ExportBlock`):

```csharp
catch (Exception ex)
{
    var pex = ex as PortalException ?? new PortalException(PortalErrorCode.ExportFailed, "Export failed", null, ex);

    pex.Data["softwarePath"] = softwarePath;
    pex.Data["blockPath"] = blockPath;
    pex.Data["exportPath"] = exportPath;

    _logger?.LogError(pex, "{MethodName} failed for {SoftwarePath} {BlockPath} -> {ExportPath}", softwarePath, blockPath, exportPath);
    throw pex;
}
```

  - Always attach metadata and log inside this single block; avoid branching on `ex` type or duplicating metadata assignments elsewhere.
  - Keep C# portal-layer files encoded as UTF-8 with BOM (Windows "UTF-8 signature") and CRLF line endings so Siemens tooling keeps metadata intact.

This keeps the decoration and logging in one place, avoids repeated code, and guarantees consistent context even for early-validation failures.

### `Operation.Run` - the canonical implementation

Since 0.2.0 the block above exists exactly once, in `src/TiaMcpServer/Siemens/Operation.cs`. New
portal methods do not hand-write it; they wrap their body:

```csharp
public PlcTagTable? GetTagTable(string softwarePath, string tagTablePath)
{
    return Operation.Run(_logger, nameof(GetTagTable), PortalErrorCode.NotFound,
        () =>
        {
            var (groupPath, tableName) = SplitPath(tagTablePath);
            return GetTagTableGroupByPath(softwarePath, groupPath)?.TagTables.Find(tableName);
        },
        ("softwarePath", softwarePath), ("tagTablePath", tagTablePath));
}
```

`Run` wraps a non-`PortalException` into one using the supplied fallback code, stamps each
context pair into `Exception.Data`, logs once and rethrows. Three properties matter:

- __Inner frames win.__ A context key already present is not overwritten, so when an outer method
  calls an inner one the most specific value survives.
- __Logged once.__ A `__logged` marker in `Exception.Data` stops a nested call from logging the
  same failure at every stack level.
- __Serialized.__ `Run` holds a lock for the duration of the body. Openness objects are not
  thread-safe and the MCP SDK may dispatch tool calls concurrently; with write tools enabled an
  unsynchronized race can corrupt project state, not merely return stale data. The lock is a
  `Monitor`, not a `SemaphoreSlim`, because portal methods call one another (`ExportBlock` calls
  `GetBlock`) and `Monitor` is reentrant on the same thread while a semaphore would deadlock.

Methods predating 0.2.0 still carry the hand-written block; migrating them is tracked in `TODO.md`.

## Write Policy

Project-mutating tools are refused above the portal layer, so this is an MCP-layer concern rather
than a `PortalErrorCode`:

- `Program.BuildToolTypes()` registers the `McpServerWrite` tool type only when `--allow-write`
  was passed. Without it the write tools are absent from `tools/list` entirely - the primary
  guard, because a tool that is not advertised cannot be mis-invoked.
- `WritePolicy.EnsureEnabled(toolName)` runs as the first statement of every write tool and
  throws `McpException`. This second guard is required because the tools are `public static`
  methods that the MSTest suite calls directly, which bypasses registration.
- Filesystem-only exports are not gated: they never modify the project. They are still annotated
  `destructiveHint: true`, because they can overwrite files on disk.

## MCP Mapping

- Every `PortalErrorCode` is surfaced as an `McpException`. Since SDK 2.x, an `McpException` thrown from a tool is converted by the SDK into a `CallToolResult` with `isError: true` and the message as text content, rather than a JSON-RPC error object. The model therefore sees the reason and can self-correct, so the message text carries the meaning that an error code used to.
- `InvalidParams` and `InvalidState` produce user-guidance messages; `ExportFailed` (and similar) include a concise reason from the inner exception, with full details logged.
- For `NotFound`, provide suggestions when the input is ambiguous (e.g., single-name block paths).

## Bulk Export Reporting

- Responses for bulk operations include both exported items and a list of inconsistent (skipped) items:
  - `ResponseExportBlocks`: `Items` (exported), `Inconsistent` (skipped)
  - `ResponseExportTypes`: `Items` (exported), `Inconsistent` (skipped)
- `Meta` contains counts for totals, exported, and inconsistent.

## Formatting

- Portal-layer C# files and their unit tests must retain Windows CRLF line endings to avoid newline parsing faults during deploy scripts.
- Markdown docs in this repo should also use CRLF and UTF-8 with BOM when committed from Windows to prevent the "UTF-8 signature" warnings the tooling flags.
