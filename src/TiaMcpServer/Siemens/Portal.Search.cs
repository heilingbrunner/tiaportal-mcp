using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Searching the program text, not just object names.
    ///
    /// Callers: the FindInCode tool in McpServer.Insight.cs. Affected API: none existing - every
    /// member here is new.
    ///
    /// File I/O: like the source readers, this has to export before it can search, because
    /// Openness offers no in-memory access to a block body. Everything is written to a scratch
    /// directory under the temp path and removed again.
    ///
    /// Cost: one export per candidate object, per call. A whole PLC of this project's size
    /// exports in roughly ten seconds, so no persistent cache is kept - invalidating one
    /// correctly is not worth the complexity at that scale. Narrow the work with 'nameFilter'
    /// on a large PLC.
    /// </summary>
    public partial class Portal
    {
        /// <summary>
        /// Finds a regular expression in the source text of blocks and PLC data types.
        /// </summary>
        /// <param name="pattern">Regular expression matched against each line, case-insensitive.</param>
        /// <param name="nameFilter">Optional regex on object names, to limit what gets exported.</param>
        /// <param name="maxResults">Stops once this many matches are found.</param>
        public CodeSearchResult FindInCode(
            string softwarePath,
            string pattern,
            string nameFilter = "",
            int maxResults = 200)
        {
            return Operation.Run(_logger, nameof(FindInCode), PortalErrorCode.ExportFailed,
                () =>
                {
                    if (string.IsNullOrWhiteSpace(pattern))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "'pattern' is empty. Pass the text or regular expression to search for.");
                    }

                    if (maxResults <= 0)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams, "'maxResults' must be greater than zero.");
                    }

                    Regex regex;

                    try
                    {
                        regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"'{pattern}' is not a valid regular expression: {ex.Message}", null, ex);
                    }

                    var result = new CodeSearchResult { Pattern = pattern };

                    // Blocks and types are read through the same readers the source tools use, so
                    // a hit always corresponds to text the caller can fetch with GetBlockSource.
                    var targets = new List<(string Path, bool IsBlock)>();

                    targets.AddRange(GetBlocks(softwarePath, nameFilter)
                        .Where(b => b.IsConsistent)
                        .Select(b => (GetBlockPath(b), true)));

                    targets.AddRange(GetTypes(softwarePath, nameFilter)
                        .Where(t => t.IsConsistent)
                        .Select(t => (GetTypePath(t), false)));

                    result.ObjectsSearched = targets.Count;

                    foreach (var target in targets)
                    {
                        if (result.Items.Count >= maxResults)
                        {
                            result.Truncated = true;

                            break;
                        }

                        SourceTextResult source;

                        try
                        {
                            source = target.IsBlock
                                ? GetBlockSource(softwarePath, target.Path, "document", int.MaxValue)
                                : GetTypeSource(softwarePath, target.Path, "document", int.MaxValue);
                        }
                        catch (Exception ex)
                        {
                            // One unreadable object must not end the search; know-how protection
                            // and export refusals are both normal on a real PLC.
                            result.Unsearchable.Add($"{target.Path}: {ex.Message}");
                            _logger?.LogDebug(ex, "Skipping '{Path}' during code search", target.Path);

                            continue;
                        }

                        AddMatches(result, target.Path, source, regex, maxResults);
                    }

                    _logger?.LogInformation(
                        "FindInCode '{Pattern}' searched {Count} object(s) in '{Software}': {Hits} hit(s), {Skipped} unsearchable",
                        pattern, result.ObjectsSearched, softwarePath, result.Items.Count, result.Unsearchable.Count);

                    return result;
                },
                ("softwarePath", softwarePath), ("pattern", pattern), ("nameFilter", nameFilter));
        }

        private static void AddMatches(CodeSearchResult result, string objectPath, SourceTextResult source, Regex regex, int maxResults)
        {
            var lines = source.Text.Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                if (result.Items.Count >= maxResults)
                {
                    result.Truncated = true;

                    return;
                }

                if (!regex.IsMatch(lines[i]))
                {
                    continue;
                }

                result.Items.Add(new CodeMatch
                {
                    ObjectPath = objectPath,
                    Format = source.Format,
                    Line = i + 1,
                    Text = lines[i].Trim('\r').Trim()
                });
            }
        }
    }

    /// <summary>One line of source text that matched the search.</summary>
    public class CodeMatch
    {
        /// <summary>Root-relative path, ready to pass to GetBlockSource or GetTypeSource.</summary>
        public string ObjectPath { get; set; } = string.Empty;

        /// <summary>'document' or 'xml' - which representation was searched.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>1-based line number within that representation.</summary>
        public int Line { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    /// <summary>Outcome of a code search.</summary>
    public class CodeSearchResult
    {
        public string Pattern { get; set; } = string.Empty;

        public List<CodeMatch> Items { get; set; } = new List<CodeMatch>();

        /// <summary>How many objects were exported and scanned.</summary>
        public int ObjectsSearched { get; set; }

        /// <summary>Objects that could not be read, with the reason.</summary>
        public List<string> Unsearchable { get; set; } = new List<string>();

        /// <summary>True when maxResults cut the search short, so the list is incomplete.</summary>
        public bool Truncated { get; set; }
    }
}
