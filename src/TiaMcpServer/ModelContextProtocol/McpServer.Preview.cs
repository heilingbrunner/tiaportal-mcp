using ModelContextProtocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Looking before writing.
    ///
    /// Callers: the MCP host, through tool registration. Affected API: none - PreviewImport is
    /// new and no write tool changed. File I/O: reads the caller's import directory; writes
    /// nothing, and never touches the project.
    ///
    /// This is the read-only half of write safety; the other half is the transaction wrapper in
    /// Portal.Transactions.cs, which every write tool now runs inside. It is one tool rather
    /// than a 'dryRun' flag on all 39 write tools: the flag would have changed the execution
    /// path of every write for a report that only imports really need, and imports are where
    /// the collisions actually happen.
    ///
    /// What it can and cannot know: the object name is taken from the file name, which is how
    /// every exporter in this server names its output. A hand-edited file whose content
    /// declares a different name than its file name would be reported under the file name.
    /// </summary>
    public static partial class McpServer
    {
        #region preview

        [McpServerTool(Name = "PreviewImport", Title = "Preview what an import would do", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
         Description("Report what importing a directory would create, overwrite or collide with, without touching the project. Checks each file against the objects already in the PLC, including the rule that a PLC data type name must be unique across the whole PLC - importing an existing type name into a different group fails even with importOption 'Override'")]
        public static ResponseImportPreview PreviewImport(
            [Description("softwarePath: defines the path in the project structure to the plc software")] string softwarePath,
            [Description("importPath: directory holding the files to import (.s7dcl source documents or .xml)")] string importPath,
            [Description("kind: what the files contain - 'type' for PLC data types, 'block' for program blocks")] string kind,
            [Description("groupPath: the group the import would target; empty means the root of that area. A leading system folder segment is accepted")] string groupPath = "")
        {
            try
            {
                if (!Directory.Exists(importPath))
                {
                    throw new McpException($"Import directory '{importPath}' does not exist.");
                }

                var isType = kind.Equals("type", StringComparison.OrdinalIgnoreCase);

                if (!isType && !kind.Equals("block", StringComparison.OrdinalIgnoreCase))
                {
                    throw new McpException($"Unknown kind '{kind}'. Use 'type' or 'block'.");
                }

                var names = Directory
                    .GetFiles(importPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => PreviewExtensions.Contains(Path.GetExtension(f)))
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var target = string.IsNullOrEmpty(groupPath) ? "the root" : groupPath;
                var items = new List<ResponseImportPreviewItem>();

                foreach (var name in names)
                {
                    var existing = Portal.ResolveObjectPath(softwarePath, name, isType ? "type" : "block")
                        .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (existing == null)
                    {
                        items.Add(new ResponseImportPreviewItem { Name = name, Effect = "create", TargetPath = Join(groupPath, name) });

                        continue;
                    }

                    var sameGroup = string.Equals(
                        Parent(existing.Path),
                        StripLeadingSystemFolder(groupPath),
                        StringComparison.OrdinalIgnoreCase);

                    items.Add(new ResponseImportPreviewItem
                    {
                        Name = name,
                        Effect = sameGroup ? "overwrite" : (isType ? "conflict" : "overwrite-elsewhere"),
                        TargetPath = Join(groupPath, name),
                        ExistingPath = existing.Path,
                        Note = sameGroup
                            ? "Already exists in the target group; importOption 'Override' replaces it."
                            : isType
                                ? "A PLC data type name must be unique across the whole PLC. This import fails even with " +
                                  $"'Override' - target '{Parent(existing.Path)}' instead, or rename the type."
                                : $"A block of this name already exists at '{existing.Path}'; importing here may collide on the block number."
                    });
                }

                var creates = items.Count(i => i.Effect == "create");
                var overwrites = items.Count(i => i.Effect == "overwrite");
                var conflicts = items.Count(i => i.Effect == "conflict" || i.Effect == "overwrite-elsewhere");

                return new ResponseImportPreview
                {
                    Message = $"Importing '{importPath}' into {target} would create {creates}, overwrite {overwrites}, " +
                              $"and hit {conflicts} conflict(s). Nothing was changed.",
                    Items = items,
                    CreateCount = creates,
                    OverwriteCount = overwrites,
                    ConflictCount = conflicts,
                    Meta = Ok(new JsonObject
                    {
                        ["files"] = names.Count,
                        ["create"] = creates,
                        ["overwrite"] = overwrites,
                        ["conflict"] = conflicts
                    })
                };
            }
            catch (PortalException pex)
            {
                throw new McpException(pex.Message, pex);
            }
            catch (Exception ex) when (ex is not McpException)
            {
                throw new McpException($"Unexpected error previewing the import of '{importPath}': {ex.Message}", ex);
            }
        }

        /// <summary>File kinds an export of this server produces, and therefore an import consumes.</summary>
        private static readonly HashSet<string> PreviewExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".s7dcl", ".xml" };

        private static string Join(string groupPath, string name) =>
            string.IsNullOrEmpty(groupPath) ? name : $"{groupPath.TrimEnd('/')}/{name}";

        private static string Parent(string path)
        {
            var slash = path.LastIndexOf('/');

            return slash < 0 ? string.Empty : path.Substring(0, slash);
        }

        /// <summary>
        /// A groupPath copied from a preservePath export starts with the localised system folder
        /// ('PLC data types', 'Program blocks'), which the resolved paths do not carry.
        /// </summary>
        private static string StripLeadingSystemFolder(string groupPath)
        {
            if (string.IsNullOrEmpty(groupPath) || !groupPath.Contains("/"))
            {
                return string.Empty;
            }

            return groupPath.Substring(groupPath.IndexOf('/') + 1);
        }

        #endregion
    }
}
