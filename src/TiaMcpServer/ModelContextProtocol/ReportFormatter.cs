using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// Renders the result of a bulk tool (pull, push, hardware, compile) as markdown that the
    /// calling model shows to the user unchanged. The structured fields of a response are
    /// condensed by a model into a short summary; a finished table is not. Tables are never
    /// shortened: every skipped object, failure and message gets its own row.
    /// </summary>
    // Callers: GeneratedMany (McpServer.Sources.cs), ImportedProgram (McpServer.Plc.cs),
    // HardwareResponse (McpServer.Hardware.cs) and BuildCompileResponse (McpServer.Software.Core.cs).
    // Data: pure functions on strings and counts, no state and no access to TIA Portal.
    public static class ReportFormatter
    {
        private const string EntrySeparator = ": ";

        /// <summary>A markdown table. Pipes and line breaks inside a cell are made harmless; there is no row limit.</summary>
        public static string Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
        {
            var lines = new List<string>
            {
                "| " + string.Join(" | ", headers.Select(Cell)) + " |",
                "| " + string.Join(" | ", headers.Select(_ => "---")) + " |"
            };

            lines.AddRange(rows.Select(r => "| " + string.Join(" | ", r.Select(Cell)) + " |"));

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>A titled table with the row count in the title, or null when there are no rows.</summary>
        public static string? Section(string title, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
        {
            var list = rows.ToList();

            if (list.Count == 0)
            {
                return null;
            }

            return "### " + title + " (" + list.Count + ")" + Environment.NewLine + Environment.NewLine + Table(headers, list);
        }

        /// <summary>Joins the parts that exist with a blank line between them.</summary>
        public static string Compose(params string?[] parts)
        {
            return string.Join(Environment.NewLine + Environment.NewLine, parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        /// <summary>
        /// Splits an entry of the form "{kind} {path}: {reason}" as the portal layer writes Skipped
        /// and Failures. Without the separator the whole text is the reason; without a space before
        /// it, there is no kind.
        /// </summary>
        public static (string Kind, string Subject, string Reason) SplitEntry(string entry)
        {
            var (subject, reason) = SplitReason(entry);

            if (subject.Length == 0)
            {
                return (string.Empty, string.Empty, reason);
            }

            var space = subject.IndexOf(' ');

            return space < 0
                ? (string.Empty, subject, reason)
                : (subject.Substring(0, space), subject.Substring(space + 1), reason);
        }

        /// <summary>Splits "{subject}: {reason}" at the first separator.</summary>
        public static (string Subject, string Reason) SplitReason(string entry)
        {
            var index = entry.IndexOf(EntrySeparator, StringComparison.Ordinal);

            return index < 0
                ? (string.Empty, entry)
                : (entry.Substring(0, index), entry.Substring(index + EntrySeparator.Length));
        }

        /// <summary>Report of a pull or export: counts per kind, then every skipped object, failure and note.</summary>
        public static string GeneratedSources(
            string directory, int totalWritten, IDictionary<string, int>? written,
            IReadOnlyList<string> skipped, IReadOnlyList<string> failures, IReadOnlyList<string>? notes)
        {
            var summary = new List<IReadOnlyList<string?>> { new[] { "Files written", totalWritten.ToString() } };

            if (written != null)
            {
                summary.AddRange(written.Select(w => (IReadOnlyList<string?>)new[] { "  " + w.Key, w.Value.ToString() }));
            }

            summary.Add(new[] { "Skipped", skipped.Count.ToString() });
            summary.Add(new[] { "Failed", failures.Count.ToString() });

            return Compose(
                Section("Summary of '" + directory + "'", new[] { "Item", "Count" }, summary),
                Section("Skipped", new[] { "Kind", "Object", "Reason" }, skipped.Select(ToKindRow)),
                Section("Failed", new[] { "Kind", "Object", "Error" }, failures.Select(ToKindRow)),
                Section("Notes", new[] { "Note" }, (notes ?? new List<string>()).Select(n => (IReadOnlyList<string?>)new[] { n })));
        }

        /// <summary>Report of a push or import: counts, then every failed, ignored and deleted file.</summary>
        public static string Imported(
            int imported, int planned, int rounds,
            IReadOnlyList<string> failures, IReadOnlyList<string> ignored, IReadOnlyList<string> deleted)
        {
            var summary = new List<IReadOnlyList<string?>>
            {
                new[] { "Planned", planned.ToString() },
                new[] { "Imported", imported.ToString() },
                new[] { "Failed", failures.Count.ToString() },
                new[] { "Ignored", ignored.Count.ToString() },
                new[] { "Deleted in git", deleted.Count.ToString() },
                new[] { "Rounds", rounds.ToString() }
            };

            return Compose(
                Section("Summary", new[] { "Item", "Count" }, summary),
                Section("Failed", new[] { "File", "Error" }, failures.Select(ToFileRow)),
                Section("Ignored", new[] { "File", "Reason" }, ignored.Select(ToFileRow)),
                Section("Deleted", new[] { "File" }, deleted.Select(d => (IReadOnlyList<string?>)new[] { d })));
        }

        /// <summary>Report of a hardware export or import: state and counts, then every message of TIA Portal.</summary>
        public static string Hardware(string device, string? state, int errors, int warnings, IReadOnlyList<string> messages)
        {
            var summary = new List<IReadOnlyList<string?>>
            {
                new[] { "Device", device },
                new[] { "State", state },
                new[] { "Errors", errors.ToString() },
                new[] { "Warnings", warnings.ToString() }
            };

            return Compose(
                Section("Summary", new[] { "Item", "Value" }, summary),
                Section("Messages", new[] { "Message" }, messages.Select(m => (IReadOnlyList<string?>)new[] { m })));
        }

        /// <summary>Report of a compile: state and counts, then every compiler message with its object.</summary>
        public static string Compile(string target, string? state, int errors, int warnings, IReadOnlyList<CompileMessage> messages)
        {
            var summary = new List<IReadOnlyList<string?>>
            {
                new[] { "Target", target },
                new[] { "State", state },
                new[] { "Errors", errors.ToString() },
                new[] { "Warnings", warnings.ToString() }
            };

            return Compose(
                Section("Summary", new[] { "Item", "Value" }, summary),
                Section("Compiler messages", new[] { "State", "Object", "Description" },
                    messages.Select(m => (IReadOnlyList<string?>)new[] { m.State, m.Path, m.Description })));
        }

        private static IReadOnlyList<string?> ToKindRow(string entry)
        {
            var (kind, subject, reason) = SplitEntry(entry);

            return new[] { kind, subject, reason };
        }

        private static IReadOnlyList<string?> ToFileRow(string entry)
        {
            var (subject, reason) = SplitReason(entry);

            return new[] { subject, reason };
        }

        private static string Cell(string? text)
        {
            return (text ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Replace("|", "\\|");
        }
    }
}
