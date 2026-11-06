using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the porcelain tools ('tia pull ...', 'tia push ...'): which of them are registered
    /// with and without '--allow-write', what arguments they take, that the push tools are refused
    /// without '--allow-write', and how a tag export is counted. These tests do not connect to TIA
    /// Portal and do not open a project.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test11Porcelain
    {
        private static readonly string[] PullTools = { "TiaPull", "TiaPullBlocks", "TiaPullTypes", "TiaPullTagTables" };

        private static readonly string[] PushTools = { "TiaPush", "TiaPushPreview" };

        private static readonly string[] RegexPullTools = { "TiaPullBlocks", "TiaPullTypes", "TiaPullTagTables" };

        [TestInitialize]
        public void ClassInit()
        {
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);
        }

        private static List<string> RegisteredNames(bool allowWrite) =>
            Program.BuildTools(allowWrite).Select(t => t.ProtocolTool.Name).ToList();

        private static global::ModelContextProtocol.Protocol.Tool Registered(string name, bool allowWrite = true) =>
            Program.BuildTools(allowWrite).Single(t => t.ProtocolTool.Name == name).ProtocolTool;

        private static MethodInfo ToolMethod(string name) =>
            typeof(McpServer).GetMethod(name, BindingFlags.Public | BindingFlags.Static)
                ?? throw new AssertFailedException($"'{name}' is not a method of McpServer");

        // 'portalId' is added to every tool by the portal selection, so it is not one of the tool's own arguments.
        private static List<string> ArgumentNames(string name) =>
            Registered(name).InputSchema.GetProperty("properties").EnumerateObject()
                .Select(p => p.Name).Where(n => n != "portalId").ToList();

        private static List<string> RequiredArguments(string name) =>
            Registered(name).InputSchema.TryGetProperty("required", out var required)
                ? required.EnumerateArray().Select(r => r.GetString()!).ToList()
                : new List<string>();

        [TestMethod]
        public void Test_1100_BuildTools_WithoutAllowWrite_RegistersThePullToolsOnly()
        {
            // Arrange / Act
            var names = RegisteredNames(allowWrite: false);

            // Assert
            foreach (var pull in PullTools)
            {
                Assert.IsTrue(names.Contains(pull), $"'{pull}' only writes files and must always be registered");
            }

            foreach (var push in PushTools)
            {
                Assert.IsFalse(names.Contains(push), $"'{push}' works on the project and must not be registered without --allow-write");
            }
        }

        [TestMethod]
        public void Test_1101_BuildTools_WithAllowWrite_AddsThePushTools()
        {
            // Arrange / Act
            var names = RegisteredNames(allowWrite: true);

            // Assert
            foreach (var name in PullTools.Concat(PushTools))
            {
                Assert.IsTrue(names.Contains(name), $"'{name}' must be registered with --allow-write");
            }
        }

        [TestMethod]
        public void Test_1102_WriteToolAttribute_IsOnThePushToolsOnly()
        {
            // Arrange / Act
            var marked = PullTools.Concat(PushTools)
                .Where(name => ToolMethod(name).GetCustomAttribute<WriteToolAttribute>() != null)
                .ToList();

            // Assert
            CollectionAssert.AreEquivalent(PushTools, marked);
        }

        [TestMethod]
        public void Test_1103_PullTools_TakeTheSameArgumentsAsTheTreeTheyWrite()
        {
            // Arrange / Act / Assert
            CollectionAssert.AreEqual(new[] { "softwarePath", "exportPath" }, ArgumentNames("TiaPull"));

            foreach (var name in RegexPullTools)
            {
                CollectionAssert.AreEqual(new[] { "softwarePath", "exportPath", "regexName" }, ArgumentNames(name), name);
                CollectionAssert.AreEquivalent(new[] { "softwarePath", "exportPath" }, RequiredArguments(name), $"'regexName' of '{name}' must stay optional");
            }
        }

        [TestMethod]
        public void Test_1104_PushTools_OfferNoDryRunBecauseTheNameDecidesIt()
        {
            // Arrange / Act / Assert
            foreach (var name in PushTools)
            {
                var arguments = ArgumentNames(name);

                Assert.IsFalse(arguments.Contains("dryRun"), $"'{name}': preview or real push is chosen by the tool, not by an argument");
                Assert.IsTrue(arguments.Contains("regexName"), name);
                CollectionAssert.AreEquivalent(new[] { "softwarePath", "exportPath" }, RequiredArguments(name), name);
            }
        }

        [TestMethod]
        public void Test_1105_Titles_ReadLikeTheGitCommandsTheyStandFor()
        {
            // Arrange
            var expected = new Dictionary<string, string>
            {
                ["TiaPull"] = "Tia pull",
                ["TiaPullBlocks"] = "Tia pull blocks",
                ["TiaPullTypes"] = "Tia pull types",
                ["TiaPullTagTables"] = "Tia pull tag tables",
                ["TiaPullWatchTables"] = "Tia pull watch tables",
                ["TiaPullWatchTable"] = "Tia pull watch table",
                ["TiaPullForceTable"] = "Tia pull force table",
                ["TiaPush"] = "Tia push",
                ["TiaPushPreview"] = "Tia push preview"
            };

            // Act / Assert
            foreach (var pair in expected)
            {
                Assert.AreEqual(pair.Value, Registered(pair.Key).Title, pair.Key);
            }
        }

        [TestMethod]
        public async System.Threading.Tasks.Task Test_1106_PushTools_WriteDisabled_Throw()
        {
            // Arrange
            var previous = WritePolicy.AllowWrite;
            WritePolicy.AllowWrite = false;

            try
            {
                // Act / Assert: the guard runs before any Portal or client access, so no TIA Portal is needed
                var calls = new (string Name, Func<System.Threading.Tasks.Task> Call)[]
                {
                    ("TiaPush", () => McpServer.TiaPush(null!, "sw", "dir")),
                    ("TiaPushPreview", () => McpServer.TiaPushPreview(null!, "sw", "dir"))
                };

                foreach (var (name, call) in calls)
                {
                    var ex = await Assert.ThrowsExactlyAsync<global::ModelContextProtocol.McpException>(call);

                    StringAssert.Contains(ex.Message, "--allow-write", $"'{name}' must be refused without --allow-write");
                    StringAssert.Contains(ex.Message, name);
                }
            }
            finally
            {
                WritePolicy.AllowWrite = previous;
            }
        }

        [TestMethod]
        public void Test_1107_Written_CountsTagTablesApartFromBlocksAndTypes()
        {
            // Arrange
            var result = new GeneratedSourcesResult();
            result.Files.Add(new GeneratedSource { Name = "FB_1", Format = ".scl", Language = "SCL" });
            result.Files.Add(new GeneratedSource { Name = "FC_1", Format = ".xml", Language = "LAD" });
            result.Files.Add(new GeneratedSource { Name = "Type_1", Format = ".udt", Language = "UDT" });
            result.Files.Add(new GeneratedSource { Name = "Table_1", Format = ".xml", Language = GeneratedSourcesResult.TagTableLanguage });
            result.Files.Add(new GeneratedSource { Name = "Table_2", Format = ".xml", Language = GeneratedSourcesResult.TagTableLanguage });

            // Act
            var written = result.Written;

            // Assert: tag table XML files must not inflate the block count
            Assert.AreEqual(2, written["blocks"]);
            Assert.AreEqual(1, written["types"]);
            Assert.AreEqual(2, written["tagTables"]);
        }

        [TestMethod]
        public void Test_1108_Written_WithoutTagTables_KeepsTheTwoAreasOfBefore()
        {
            // Arrange
            var result = new GeneratedSourcesResult();
            result.Files.Add(new GeneratedSource { Name = "FB_1", Format = ".scl", Language = "SCL" });
            result.Files.Add(new GeneratedSource { Name = "Type_1", Format = ".udt", Language = "UDT" });

            // Act
            var written = result.Written;

            // Assert: the response of every export that is not a tag export stays as it was
            CollectionAssert.AreEquivalent(new[] { "blocks", "types" }, written.Keys.ToList());
            Assert.AreEqual(1, written["blocks"]);
            Assert.AreEqual(1, written["types"]);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(".+HMI")]
        [DataRow("^FB_[0-9]+$")]
        public void Test_1111_RequireValidRegex_AcceptsEmptyAndValidPatterns(string pattern)
        {
            // Arrange / Act / Assert: no exception
            Portal.RequireValidRegex(pattern);
        }

        [TestMethod]
        [DataRow("(HMI")]
        [DataRow("[a-")]
        public void Test_1112_RequireValidRegex_RejectsABrokenPatternInsteadOfPullingNothing(string pattern)
        {
            // Arrange / Act
            var ex = Assert.ThrowsExactly<PortalException>(() => Portal.RequireValidRegex(pattern));

            // Assert
            StringAssert.Contains(ex.Message, pattern);
            StringAssert.Contains(ex.Message, "regular expression");
        }

        [TestMethod]
        public void Test_1113_TiaConnect_IsRegisteredWithoutAllowWriteAndIsNotAWriteTool()
        {
            // Arrange / Act
            var names = RegisteredNames(allowWrite: false);

            // Assert: it only opens a project, like OpenTiaProject, which it wraps
            Assert.IsTrue(names.Contains("TiaConnect"));
            Assert.IsNull(ToolMethod("TiaConnect").GetCustomAttribute<WriteToolAttribute>());
            Assert.AreEqual("Tia connect", Registered("TiaConnect", allowWrite: false).Title);
        }

        [TestMethod]
        public void Test_1114_TiaConnect_TakesTheArgumentsOfOpenTiaProject()
        {
            // Arrange / Act / Assert
            CollectionAssert.AreEqual(ArgumentNames("OpenTiaProject"), ArgumentNames("TiaConnect"));
            CollectionAssert.AreEqual(new[] { "path" }, RequiredArguments("TiaConnect"), "'portalId' must stay optional");
        }

        [TestMethod]
        public void Test_1115_TiaConnectPrompt_PassesThePathAndTheOptionalPortalId()
        {
            // Arrange / Act
            var withoutPortal = McpPrompts.TiaConnect(@"D:\projects\Demo.ap20");
            var withPortal = McpPrompts.TiaConnect(@"D:\projects\Demo.ap20", "4711");

            // Assert
            StringAssert.Contains(withoutPortal, "Use the TiaConnect tool");
            StringAssert.EndsWith(withoutPortal, @"- path: D:\projects\Demo.ap20", "an empty portalId must not be passed on");
            StringAssert.Contains(withPortal, "portalId: 4711");
        }

        [TestMethod]
        public void Test_1130_TiaInit_IsRegisteredWithoutAllowWriteAndIsNotAWriteTool()
        {
            // Arrange / Act
            var names = RegisteredNames(allowWrite: false);

            // Assert: it only writes tia.json into the export folder and never touches the project
            Assert.IsTrue(names.Contains("TiaInit"));
            Assert.IsNull(ToolMethod("TiaInit").GetCustomAttribute<WriteToolAttribute>());
            Assert.AreEqual("Tia init", Registered("TiaInit", allowWrite: false).Title);
        }

        [TestMethod]
        public void Test_1131_TiaInit_TakesOnlyOptionalArgumentsThatTheServerPresetsFill()
        {
            // Arrange / Act / Assert
            CollectionAssert.AreEqual(new[] { "exportPath", "path", "softwarePath", "keepStructure", "withUserInterface", "preservePath" }, ArgumentNames("TiaInit"));
            Assert.AreEqual(0, RequiredArguments("TiaInit").Count, "the dialog asks for what is missing");
        }

        [TestMethod]
        public void Test_1132_TiaInitPrompt_PassesTheThreeStartValuesOfTheDialog()
        {
            // Arrange / Act
            var body = McpPrompts.TiaInit(@"D:\export", @"D:\projects\Demo.ap20", "PLC_1");

            // Assert
            StringAssert.Contains(body, "Use the TiaInit tool");
            StringAssert.Contains(body, @"- exportPath: D:\export");
            StringAssert.Contains(body, @"- path: D:\projects\Demo.ap20");
            StringAssert.Contains(body, "- softwarePath: PLC_1");
        }

        [TestMethod]
        public void Test_1116_PlannedTable_ListsPlannedAndDeletedFilesAsMarkdownRows()
        {
            // Arrange
            var result = new PlcProgramImportResult { DryRun = true };
            result.Planned.Add(new PlannedProgramItem
            {
                File = "Program blocks/FB_1.scl", Name = "FB_1", Path = "Program blocks/FB_1",
                Kind = "block", Format = "source", Action = PlannedProgramItem.Overwrite, GitStatus = "M"
            });
            result.Planned.Add(new PlannedProgramItem
            {
                File = "PLC data types/Udt_1.udt", Name = "Udt_1", Path = "PLC data types/Udt_1",
                Kind = "type", Format = "source", Action = PlannedProgramItem.Create, GitStatus = "U"
            });
            result.Deleted.Add("Program blocks/Old.scl");

            // Act
            var lines = McpServer.PlannedTable(result)!.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            // Assert
            Assert.AreEqual(5, lines.Length);
            Assert.AreEqual("| Git | Type | File |", lines[0]);
            Assert.AreEqual("| --- | --- | --- |", lines[1]);
            Assert.AreEqual("| M | block | Program blocks/FB_1.scl |", lines[2]);
            Assert.AreEqual("| U | type | PLC data types/Udt_1.udt |", lines[3]);
            Assert.AreEqual("| D |  | Program blocks/Old.scl |", lines[4]);
        }

        [TestMethod]
        public void Test_1117_PlannedTable_WithNothingAffected_IsNull()
        {
            // Arrange / Act / Assert
            Assert.IsNull(McpServer.PlannedTable(new PlcProgramImportResult { DryRun = true }));
        }

        [TestMethod]
        public void Test_1118_PlannedTable_EscapesThePipeCharacterOfACell()
        {
            // Arrange
            var result = new PlcProgramImportResult { DryRun = true };
            result.Planned.Add(new PlannedProgramItem { File = "a|b.scl", Action = PlannedProgramItem.Create, GitStatus = "A" });

            // Act
            var table = McpServer.PlannedTable(result)!;

            // Assert: an unescaped pipe would add a column
            StringAssert.Contains(table, @"a\|b.scl");
        }

        [TestMethod]
        [DataRow("ExportBlocks", "Export blocks")]
        [DataRow("ExportTypes", "Export PLC data types")]
        [DataRow("ExportTagTables", "Export tag tables")]
        [DataRow("ExportWatchTables", "Export watch tables")]
        public void Test_1119_ExportTools_AreRegisteredWithoutAllowWriteWithAnOptionalRegex(string name, string title)
        {
            // Arrange / Act
            var registered = Registered(name, allowWrite: false);

            // Assert
            Assert.AreEqual(title, registered.Title);
            Assert.IsNull(ToolMethod(name).GetCustomAttribute<WriteToolAttribute>(), $"'{name}' only writes files");
            CollectionAssert.AreEqual(new[] { "softwarePath", "exportPath", "regexName" }, ArgumentNames(name));
            CollectionAssert.AreEquivalent(new[] { "softwarePath", "exportPath" }, RequiredArguments(name));
        }

        [TestMethod]
        public void Test_1120_PullTools_TakeTheSameArgumentsAsTheExportToolsTheyWrap()
        {
            // Arrange / Act / Assert
            CollectionAssert.AreEqual(ArgumentNames("ExportBlocks"), ArgumentNames("TiaPullBlocks"));
            CollectionAssert.AreEqual(ArgumentNames("ExportTypes"), ArgumentNames("TiaPullTypes"));
            CollectionAssert.AreEqual(ArgumentNames("ExportTagTables"), ArgumentNames("TiaPullTagTables"));
            CollectionAssert.AreEqual(ArgumentNames("ExportWatchTables"), ArgumentNames("TiaPullWatchTables"));
        }

        [TestMethod]
        [DataRow("ExportBlock", "TiaPullBlock", "blockPath")]
        [DataRow("ExportType", "TiaPullType", "typePath")]
        [DataRow("ExportTagTable", "TiaPullTagTable", "tagTablePath")]
        [DataRow("ExportWatchTable", "TiaPullWatchTable", "watchTablePath")]
        [DataRow("ExportForceTable", "TiaPullForceTable", "forceTablePath")]
        public void Test_1122_SingleObjectTools_TakeTheSameThreeArgumentsInTheSameOrder(string exportTool, string pullTool, string pathArgument)
        {
            // Arrange
            var expected = new[] { "softwarePath", "exportPath", pathArgument };

            // Act / Assert: the path of the object comes last, so the first two match every porcelain tool
            foreach (var name in new[] { exportTool, pullTool })
            {
                CollectionAssert.AreEqual(expected, ArgumentNames(name), name);
                CollectionAssert.AreEquivalent(expected, RequiredArguments(name), name);
                Assert.IsNull(ToolMethod(name).GetCustomAttribute<WriteToolAttribute>(), $"'{name}' only writes files");
                Assert.IsTrue(RegisteredNames(allowWrite: false).Contains(name), name);
            }
        }

        [TestMethod]
        public void Test_1123_SingleObjectPrompts_PassThePathOfTheObject()
        {
            // Arrange / Act
            var block = McpPrompts.ExportBlock("PLC_1", @"D:\export", "Group/FB_1");
            var type = McpPrompts.TiaPullType("PLC_1", @"D:\export", "Group/Udt_1");
            var tag = McpPrompts.TiaPullTagTable("PLC_1", @"D:\export", "TagGroup1/Table1");

            // Assert
            StringAssert.Contains(block, "Use the ExportBlock tool");
            StringAssert.EndsWith(block, "- blockPath: Group/FB_1");
            StringAssert.Contains(type, "Use the TiaPullType tool");
            StringAssert.EndsWith(type, "- typePath: Group/Udt_1");
            StringAssert.Contains(tag, "Use the TiaPullTagTable tool");
            StringAssert.EndsWith(tag, "- tagTablePath: TagGroup1/Table1");

            var watch = McpPrompts.TiaPullWatchTable("PLC_1", @"D:\export", "WatchGroup1/Watch_1");
            var force = McpPrompts.ExportForceTable("PLC_1", @"D:\export", "Force_1");

            StringAssert.Contains(watch, "Use the TiaPullWatchTable tool");
            StringAssert.EndsWith(watch, "- watchTablePath: WatchGroup1/Watch_1");
            StringAssert.Contains(force, "Use the ExportForceTable tool");
            StringAssert.EndsWith(force, "- forceTablePath: Force_1");
        }

        [TestMethod]
        public void Test_1121_ExportPrompts_PassTheRegularExpressionOnToTheTool()
        {
            // Arrange / Act
            var blocks = McpPrompts.ExportBlocks("PLC_1", @"D:\export", ".+HMI");
            var types = McpPrompts.ExportTypes("PLC_1", @"D:\export", ".+HMI");
            var tags = McpPrompts.ExportTagTables("PLC_1", @"D:\export");

            // Assert
            StringAssert.Contains(blocks, "Use the ExportBlocks tool");
            StringAssert.Contains(blocks, "- regexName: .+HMI");
            StringAssert.Contains(types, "Use the ExportTypes tool");
            StringAssert.Contains(types, "- regexName: .+HMI");
            StringAssert.Contains(tags, "Use the ExportTagTables tool");
            StringAssert.EndsWith(tags, "- regexName: ");
        }

        [TestMethod]
        public void Test_1109_Prompts_PassTheRegularExpressionOnToTheTool()
        {
            // Arrange / Act
            var blocks = McpPrompts.TiaPullBlocks("PLC_1", @"D:\export", ".+HMI");
            var types = McpPrompts.TiaPullTypes("PLC_1", @"D:\export", ".+HMI");
            var tags = McpPrompts.TiaPullTagTables("PLC_1", @"D:\export", ".+HMI");

            // Assert
            StringAssert.Contains(blocks, "Use the TiaPullBlocks tool");
            StringAssert.Contains(blocks, "- regexName: .+HMI");
            StringAssert.Contains(types, "Use the TiaPullTypes tool");
            StringAssert.Contains(types, "- regexName: .+HMI");
            StringAssert.Contains(tags, "Use the TiaPullTagTables tool");
            StringAssert.Contains(tags, "- regexName: .+HMI");
        }

        [TestMethod]
        public void Test_1110_Prompts_NormalizeKeepOnErrorOfThePushTools()
        {
            // Arrange / Act
            var push = McpPrompts.TiaPush("PLC_1", @"D:\export", string.Empty, "yes");
            var preview = McpPrompts.TiaPushPreview("PLC_1", @"D:\export");

            // Assert
            StringAssert.Contains(push, "Use the TiaPush tool");
            StringAssert.Contains(push, "- keepOnError: true");
            StringAssert.Contains(preview, "Use the TiaPushPreview tool");
            StringAssert.Contains(preview, "- keepOnError: false");
        }
    }
}
