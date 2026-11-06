using System.Collections.Generic;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the markdown reports the tools hand to the user. They do not connect to TIA Portal.
    /// </summary>
    [TestClass]
    public class Test13Report
    {
        private static int RowCount(string markdown, string title)
        {
            var lines = markdown.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None).ToList();
            var start = lines.FindIndex(l => l.StartsWith("### " + title));

            Assert.IsTrue(start >= 0, "Section '" + title + "' is missing.");

            // header + separator + rows, up to the next blank line
            return lines.Skip(start + 2).TakeWhile(l => l.StartsWith("|")).Count() - 2;
        }

        [TestMethod]
        public void Table_PipeAndNewlineInCell_AreEscaped()
        {
            var table = ReportFormatter.Table(new[] { "A", "B" }, new[] { new[] { "x|y", "line1\r\nline2" } });

            StringAssert.Contains(table, "x\\|y");
            Assert.IsFalse(table.Contains("line1\r\n"), "A newline inside a cell would break the table.");
        }

        [TestMethod]
        public void Table_NullCell_BecomesEmpty()
        {
            var table = ReportFormatter.Table(new[] { "A", "B" }, new[] { new string?[] { null, "b" } });

            StringAssert.Contains(table, "|  | b |");
        }

        [TestMethod]
        public void Section_NoRows_ReturnsNull()
        {
            Assert.IsNull(ReportFormatter.Section("Skipped", new[] { "A" }, new List<string[]>()));
        }

        [TestMethod]
        public void Table_ManyRows_AreNeverTruncated()
        {
            var rows = Enumerable.Range(0, 600).Select(i => new[] { "row" + i }).ToList();

            var table = ReportFormatter.Table(new[] { "A" }, rows);

            Assert.AreEqual(602, table.Split(new[] { "\r\n", "\n" }, System.StringSplitOptions.None).Length);
        }

        [TestMethod]
        public void SplitEntry_KindPathAndReason_AreSeparated()
        {
            var (kind, subject, reason) = ReportFormatter.SplitEntry("FB Folder/Block_1: inconsistent");

            Assert.AreEqual("FB", kind);
            Assert.AreEqual("Folder/Block_1", subject);
            Assert.AreEqual("inconsistent", reason);
        }

        [TestMethod]
        public void SplitEntry_ReasonWithColon_KeepsWholeReason()
        {
            var (_, subject, reason) = ReportFormatter.SplitEntry("FB A/B: error: line 3: bad token");

            Assert.AreEqual("A/B", subject);
            Assert.AreEqual("error: line 3: bad token", reason);
        }

        [TestMethod]
        public void SplitEntry_NoSeparator_PutsEverythingInReason()
        {
            var (kind, subject, reason) = ReportFormatter.SplitEntry("something odd");

            Assert.AreEqual(string.Empty, kind);
            Assert.AreEqual(string.Empty, subject);
            Assert.AreEqual("something odd", reason);
        }

        [TestMethod]
        public void GeneratedSources_23SkippedAnd6Failures_ListsEveryRow()
        {
            var skipped = Enumerable.Range(1, 23).Select(i => "FB Group/Block_" + i + ": inconsistent").ToList();
            var failures = Enumerable.Range(1, 6).Select(i => "STL Group/Stl_" + i + ": cannot export").ToList();
            var written = new Dictionary<string, int> { ["blocks"] = 664, ["types"] = 0 };

            var report = ReportFormatter.GeneratedSources("Project/PC-System_1/Software PLC_1", 664, written, skipped, failures, null);

            StringAssert.Contains(report, "664");
            Assert.AreEqual(23, RowCount(report, "Skipped"));
            Assert.AreEqual(6, RowCount(report, "Failed"));
            StringAssert.Contains(report, "Block_23");
            StringAssert.Contains(report, "Stl_6");
        }

        [TestMethod]
        public void GeneratedSources_NothingSkippedOrFailed_OmitsProblemSections()
        {
            var report = ReportFormatter.GeneratedSources("dir", 3, new Dictionary<string, int> { ["blocks"] = 3 }, new List<string>(), new List<string>(), null);

            Assert.IsFalse(report.Contains("### Skipped"));
            Assert.IsFalse(report.Contains("### Failed"));
        }

        [TestMethod]
        public void GeneratedSources_Notes_AreListedInFull()
        {
            var report = ReportFormatter.GeneratedSources("dir", 1, null, new List<string>(), new List<string>(), new List<string> { "created tia.json", "second note" });

            StringAssert.Contains(report, "created tia.json");
            StringAssert.Contains(report, "second note");
        }

        [TestMethod]
        public void Compile_Messages_ListsEveryRow()
        {
            var messages = Enumerable.Range(1, 40)
                .Select(i => new CompileMessage { Path = "Block_" + i, State = "Warning", Description = "w" + i })
                .ToList();

            var report = ReportFormatter.Compile("PLC_1", "Warning", 0, 40, messages);

            Assert.AreEqual(40, RowCount(report, "Compiler messages"));
            StringAssert.Contains(report, "w40");
        }

        [TestMethod]
        public void Hardware_Messages_ListsEveryRow()
        {
            var report = ReportFormatter.Hardware("PLC_1", "Warning", 0, 2, new List<string> { "m1", "m2" });

            Assert.AreEqual(2, RowCount(report, "Messages"));
        }

        [TestMethod]
        public void Imported_FailuresIgnoredAndDeleted_AreListed()
        {
            var report = ReportFormatter.Imported(
                imported: 10, planned: 12, rounds: 2,
                failures: new List<string> { "a.xml: boom", "b.xml: bang" },
                ignored: new List<string> { "c.txt: not a block" },
                deleted: new List<string> { "d.xml" });

            Assert.AreEqual(2, RowCount(report, "Failed"));
            Assert.AreEqual(1, RowCount(report, "Ignored"));
            Assert.AreEqual(1, RowCount(report, "Deleted"));
        }
    }
}
