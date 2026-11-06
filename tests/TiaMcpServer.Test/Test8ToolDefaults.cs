using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the command line presets of 'softwarePath', 'exportPath' and 'keepStructure'.
    /// These tests do not connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test8ToolDefaults
    {
        private const string ExportTool = "ExportBlocks";

        [TestInitialize]
        public void ClassInit()
        {
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);
        }

        [TestCleanup]
        public void Cleanup()
        {
            ToolDefaults.Configure(new CliOptions());
        }

        [TestMethod]
        public void Test_800_ParseArgs_ReadsThePresets()
        {
            // Arrange
            var args = new[] { "--project-path", @"D:\Projects\My Plant\Plant.ap21", "--software-path", "PC-System_1/Software PLC_1", "-export-path", @"D:\My Export", "--keep-structure", "--with-dependencies" };

            // Act
            var options = CliOptions.ParseArgs(args);

            // Assert
            Assert.AreEqual(@"D:\Projects\My Plant\Plant.ap21", options.ProjectPath);
            Assert.AreEqual("PC-System_1/Software PLC_1", options.SoftwarePath);
            Assert.AreEqual(@"D:\My Export", options.ExportPath);
            Assert.AreEqual(true, options.KeepStructure);
            Assert.AreEqual(true, options.WithDependencies);
        }

        [TestMethod]
        public void Test_801_ParseArgs_FlagsAreOffWhenAbsentAndHonourAnExplicitValue()
        {
            // Act
            var absent = CliOptions.ParseArgs(new[] { "--allow-write" });
            var explicitFalse = CliOptions.ParseArgs(new[] { "--keep-structure", "false", "--with-dependencies" });

            // Assert
            Assert.IsFalse(absent.KeepStructure, "an absent flag is false, the tool default");
            Assert.IsFalse(absent.WithDependencies);
            Assert.AreEqual(false, explicitFalse.KeepStructure);
            Assert.AreEqual(true, explicitFalse.WithDependencies);
        }

        [TestMethod]
        [DataRow("--preserve-path")]
        [DataRow("-preserve-path")]
        public void Test_816_ParseArgs_TheDeprecatedPreservePathStillTurnsKeepStructureOn(string oldName)
        {
            // Act
            var options = CliOptions.ParseArgs(new[] { oldName });

            // Assert
            Assert.IsTrue(options.KeepStructure);
            Assert.IsTrue(options.UsedDeprecatedPreservePath);
        }

        [TestMethod]
        public void Test_817_ParseArgs_TheDeprecatedPreservePathHonoursAnExplicitFalse()
        {
            // Act
            var options = CliOptions.ParseArgs(new[] { "--preserve-path", "false" });

            // Assert
            Assert.IsFalse(options.KeepStructure, "an older '--preserve-path false' must not read as on");
            Assert.IsTrue(options.UsedDeprecatedPreservePath, "the old name was still used, the doctor says so");
        }

        [TestMethod]
        public void Test_818_ParseArgs_NewNameDoesNotMarkTheDeprecatedNameAsUsed()
        {
            // Act
            var options = CliOptions.ParseArgs(new[] { "--keep-structure" });

            // Assert
            Assert.IsTrue(options.KeepStructure);
            Assert.IsFalse(options.UsedDeprecatedPreservePath);
        }

        [TestMethod]
        public void Test_819_ParseArgs_BothNamesTogether_EitherTrueTurnsItOn()
        {
            // Act
            var oldFalseNewTrue = CliOptions.ParseArgs(new[] { "--preserve-path", "false", "--keep-structure" });
            var newFalseOldTrue = CliOptions.ParseArgs(new[] { "--keep-structure", "false", "--preserve-path" });

            // Assert
            Assert.IsTrue(oldFalseNewTrue.KeepStructure);
            Assert.IsTrue(newFalseOldTrue.KeepStructure);
        }

        [TestMethod]
        public void Test_820_Describe_ListsKeepStructureAndTheDeprecatedNameForTheDoctor()
        {
            // Arrange
            var fresh = CliOptions.ParseArgs(new[] { "--keep-structure" }).Describe().ToDictionary(p => p.Key, p => p.Value);
            var old = CliOptions.ParseArgs(new[] { "--preserve-path" }).Describe().ToDictionary(p => p.Key, p => p.Value);
            const string deprecated = "--preserve-path (deprecated, use --keep-structure)";

            // Assert
            Assert.AreEqual("true", fresh["--keep-structure"]);
            Assert.AreEqual("not used", fresh[deprecated], "the deprecated name is always listed so users learn it");
            Assert.AreEqual("true", old["--keep-structure"]);
            Assert.AreEqual("used", old[deprecated]);
            Assert.IsFalse(fresh.ContainsKey("--preserve-path"), "the plain old name is only shown as deprecated");
        }

        [TestMethod]
        public void Test_802_FillMissing_UsesThePresetOnlyForOmittedOrNullArguments()
        {
            // Arrange
            var presets = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_1"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(@"D:\Export"),
                [ToolDefaults.KeepStructure] = JsonSerializer.SerializeToElement(true)
            };
            var arguments = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_2"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement((string?)null)
            };

            // Act
            ToolDefaults.FillMissing(arguments, presets.Keys, presets);

            // Assert
            Assert.AreEqual("PLC_2", arguments[ToolDefaults.SoftwarePath].GetString(), "A value from the call wins");
            Assert.AreEqual(@"D:\Export", arguments[ToolDefaults.ExportPath].GetString(), "null is treated as omitted");
            Assert.IsTrue(arguments[ToolDefaults.KeepStructure].GetBoolean());
        }

        [TestMethod]
        public void Test_803_Apply_WithPresets_DropsThemFromRequired()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1", ExportPath = @"D:\Export" });

            // Act
            var tool = Program.BuildTools(allowWrite: false).Single(t => t.ProtocolTool.Name == ExportTool);
            var required = RequiredOf(tool);

            // Assert
            CollectionAssert.DoesNotContain(required, ToolDefaults.SoftwarePath);
            CollectionAssert.DoesNotContain(required, ToolDefaults.ExportPath);
        }

        [TestMethod]
        public void Test_804_Apply_WithoutPresets_KeepsTheRequiredArguments()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions());

            // Act
            var required = RequiredOf(Program.BuildTools(allowWrite: false).Single(t => t.ProtocolTool.Name == ExportTool));

            // Assert
            CollectionAssert.Contains(required, ToolDefaults.SoftwarePath);
            CollectionAssert.Contains(required, ToolDefaults.ExportPath);
        }

        [TestMethod]
        public void Test_805_Apply_WithPresets_EveryExportToolTakesThem()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1", ExportPath = @"D:\Export" });

            // Act
            var exportTools = Program.BuildTools(allowWrite: true)
                .Where(t => t.ProtocolTool.Name.StartsWith("Export"))
                .ToList();

            // Assert
            Assert.IsTrue(exportTools.Count > 0, "no export tools found");

            foreach (var tool in exportTools)
            {
                var required = RequiredOf(tool);

                CollectionAssert.DoesNotContain(required, ToolDefaults.SoftwarePath, tool.ProtocolTool.Name);
                CollectionAssert.DoesNotContain(required, ToolDefaults.ExportPath, tool.ProtocolTool.Name);
            }
        }

        [TestMethod]
        public void Test_806_FillMissing_TreatsBlankStringsAsOmitted()
        {
            // Arrange
            var presets = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_1"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(@"D:\Export"),
                [ToolDefaults.KeepStructure] = JsonSerializer.SerializeToElement(true)
            };
            var arguments = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("  "),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(string.Empty),
                [ToolDefaults.KeepStructure] = JsonSerializer.SerializeToElement(false)
            };

            // Act
            ToolDefaults.FillMissing(arguments, presets.Keys, presets);

            // Assert
            Assert.AreEqual("PLC_1", arguments[ToolDefaults.SoftwarePath].GetString());
            Assert.AreEqual(@"D:\Export", arguments[ToolDefaults.ExportPath].GetString());
            Assert.IsFalse(arguments[ToolDefaults.KeepStructure].GetBoolean(), "an explicit false is a value, not an omission");
        }

        [TestMethod]
        public void Test_807_MarkPresetsOptional_OnlyWithPresetsAndOnlyOnce()
        {
            // Arrange
            var presets = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_1"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(@"D:\Export")
            };
            var prompt = new global::ModelContextProtocol.Protocol.Prompt
            {
                Name = ExportTool,
                Arguments = new List<global::ModelContextProtocol.Protocol.PromptArgument>
                {
                    new() { Name = "softwarePath", Description = "the PLC", Required = true },
                    new() { Name = "regexName", Description = "the filter", Required = true }
                }
            };

            // Act
            PromptArguments.MarkPresetsOptional(new[] { prompt }, presets);
            PromptArguments.MarkPresetsOptional(new[] { prompt }, presets);

            // Assert
            var preset = prompt.Arguments![0];
            var other = prompt.Arguments![1];

            Assert.AreEqual(false, preset.Required);
            Assert.AreEqual(1, preset.Description!.Split(new[] { "Optional: the server has a preset" }, System.StringSplitOptions.None).Length - 1, "the hint is added once");
            Assert.AreEqual(true, other.Required, "an argument without preset stays required");
            Assert.AreEqual("the filter", other.Description);
        }

        [TestMethod]
        public void Test_808_FillPresets_FillsOmittedArgumentsAsTextAndKeepsGivenOnes()
        {
            // Arrange
            var presets = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_1"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(@"D:\Export"),
                [ToolDefaults.KeepStructure] = JsonSerializer.SerializeToElement(true)
            };
            var declared = new[] { "softwarePath", "exportPath", "regexName", "keepStructure" };
            var arguments = new Dictionary<string, JsonElement>
            {
                ["softwarePath"] = JsonSerializer.SerializeToElement("PLC_2"),
                ["exportPath"] = JsonSerializer.SerializeToElement(string.Empty)
            };

            // Act
            PromptArguments.FillPresets(arguments, declared, presets);

            // Assert
            Assert.AreEqual("PLC_2", arguments["softwarePath"].GetString(), "a given value wins");
            Assert.AreEqual(@"D:\Export", arguments["exportPath"].GetString(), "blank is treated as omitted");
            Assert.AreEqual("true", arguments["keepStructure"].GetString(), "flags reach a prompt as text");
            Assert.IsFalse(arguments.ContainsKey("regexName"), "no preset, no entry");
        }

        [TestMethod]
        public void Test_809_Apply_WithSoftwarePathPreset_NoToolOffersTheArgumentAnyMore()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1" });

            // Act
            var tools = Program.BuildTools(allowWrite: true).ToList();

            // Assert
            foreach (var name in new[] { "GetBlocks", "GetBlocksWithHierarchy", "GetTypes" })
            {
                var tool = tools.Single(t => t.ProtocolTool.Name == name);

                CollectionAssert.DoesNotContain(PropertiesOf(tool), ToolDefaults.SoftwarePath, name);
                CollectionAssert.DoesNotContain(RequiredOf(tool), ToolDefaults.SoftwarePath, name);
            }

            Assert.IsFalse(
                tools.Any(t => PropertiesOf(t).Contains(ToolDefaults.SoftwarePath)),
                "a preset is binding: the model cannot pick another software path");
        }

        [TestMethod]
        public void Test_810_ParseArgs_ForcePresetsIsDeprecatedAndOnlyRemembered()
        {
            // Act
            var forced = CliOptions.ParseArgs(new[] { "--software-path", "PLC_1", "-force-presets" });
            var explicitFalse = CliOptions.ParseArgs(new[] { "--force-presets", "false", "--export-path", @"D:\Export" });
            var absent = CliOptions.ParseArgs(new[] { "--software-path", "PLC_1" });

            // Assert
            Assert.IsTrue(forced.UsedDeprecatedForcePresets);
            Assert.AreEqual("PLC_1", forced.SoftwarePath, "the option consumes nothing else");
            Assert.IsTrue(explicitFalse.UsedDeprecatedForcePresets, "the old option was still used, the doctor says so");
            Assert.AreEqual(@"D:\Export", explicitFalse.ExportPath, "its optional true/false is consumed");
            Assert.IsFalse(absent.UsedDeprecatedForcePresets);
        }

        [TestMethod]
        public void Test_811_Configure_EveryPresetThatIsSetIsBinding()
        {
            // Act
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1", KeepStructure = true });
            var onlySoftware = ToolDefaults.Forced.OrderBy(n => n).ToList();
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1", ExportPath = @"D:\Export" });
            var withExport = ToolDefaults.Forced.OrderBy(n => n).ToList();
            ToolDefaults.Configure(new CliOptions());
            var none = ToolDefaults.Forced.ToList();

            // Assert
            CollectionAssert.AreEqual(new[] { ToolDefaults.KeepStructure, ToolDefaults.SoftwarePath }, onlySoftware, "no --force-presets needed");
            CollectionAssert.AreEqual(
                new[] { ToolDefaults.ExportPath, ToolDefaults.ImportPath, ToolDefaults.SoftwarePath }, withExport,
                "importPath follows the export path");
            Assert.AreEqual(0, none.Count, "without presets nothing is fixed");
        }

        [TestMethod]
        public void Test_821_Configure_ImportPathFollowsTheExportPathAndNeverStandsAlone()
        {
            // Act
            ToolDefaults.Configure(new CliOptions { ExportPath = @"D:\Export" });
            var withExport = ToolDefaults.Presets[ToolDefaults.ImportPath].GetString();
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1" });
            var withoutExport = ToolDefaults.Presets.ContainsKey(ToolDefaults.ImportPath);

            // Assert
            Assert.AreEqual(@"D:\Export", withExport);
            Assert.IsFalse(withoutExport, "no export path, no importPath preset");
        }

        [TestMethod]
        public void Test_822_Apply_WithExportPath_ImportToolsDoNotOfferImportPath()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions { ExportPath = @"D:\Export", SoftwarePath = "PLC_1" });

            // Act
            var tools = Program.BuildTools(allowWrite: true).ToList();

            // Assert
            foreach (var name in new[] { "OverwritePlcProgram", "ImportHardware" })
            {
                var tool = tools.Single(t => t.ProtocolTool.Name == name);

                CollectionAssert.DoesNotContain(PropertiesOf(tool), ToolDefaults.ImportPath, name);
                CollectionAssert.DoesNotContain(PropertiesOf(tool), ToolDefaults.ExportPath, name);
            }
        }

        [TestMethod]
        public void Test_823_Describe_ListsTheDeprecatedForcePresetsForTheDoctor()
        {
            // Arrange
            const string key = "--force-presets (deprecated, presets are always binding)";
            var used = CliOptions.ParseArgs(new[] { "--force-presets" }).Describe().ToDictionary(p => p.Key, p => p.Value);
            var notUsed = new CliOptions().Describe().ToDictionary(p => p.Key, p => p.Value);

            // Assert
            Assert.AreEqual("used", used[key]);
            Assert.AreEqual("not used", notUsed[key], "always listed, so users learn it");
            Assert.IsFalse(notUsed.ContainsKey("--force-presets"));
        }

        [TestMethod]
        public void Test_812_Apply_WithPresets_HidesThemFromTheSchema()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions
            {
                ProjectPath = @"D:\Plant.ap21",
                SoftwarePath = "PLC_1",
                ExportPath = @"D:\Export",
                KeepStructure = true
            });

            // Act
            var tools = Program.BuildTools(allowWrite: true).ToList();

            // Assert
            foreach (var name in new[] { "GetBlocks", "GetTypes", "OpenProject", ExportTool })
            {
                var tool = tools.Single(t => t.ProtocolTool.Name == name);

                foreach (var forced in ToolDefaults.Forced)
                {
                    CollectionAssert.DoesNotContain(PropertiesOf(tool), forced, name);
                    CollectionAssert.DoesNotContain(RequiredOf(tool), forced, name);
                }
            }

            CollectionAssert.Contains(PropertiesOf(tools.Single(t => t.ProtocolTool.Name == ExportTool)), "regexName");
        }

        [TestMethod]
        public void Test_813_Apply_WithoutPresets_KeepsTheArgumentsInTheSchema()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions());

            // Act
            var properties = PropertiesOf(Program.BuildTools(allowWrite: false).Single(t => t.ProtocolTool.Name == ExportTool));

            // Assert
            CollectionAssert.Contains(properties, ToolDefaults.SoftwarePath);
            CollectionAssert.Contains(properties, ToolDefaults.ExportPath);
        }

        [TestMethod]
        public void Test_814_FillMissing_ForcedArgumentsAlwaysTakeThePreset()
        {
            // Arrange
            var presets = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_1"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(@"D:\Export")
            };
            var forced = new HashSet<string> { ToolDefaults.ExportPath };
            var arguments = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_2"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(@"C:\Elsewhere")
            };

            // Act
            ToolDefaults.FillMissing(arguments, presets.Keys, presets, forced);

            // Assert
            Assert.AreEqual(@"D:\Export", arguments[ToolDefaults.ExportPath].GetString(), "a forced preset replaces the sent value");
            Assert.AreEqual("PLC_2", arguments[ToolDefaults.SoftwarePath].GetString(), "an unforced preset keeps the sent value");
        }

        [TestMethod]
        public void Test_815_Prompts_ForcedArgumentsAreHiddenAndOverwritten()
        {
            // Arrange
            var presets = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_1"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(@"D:\Export")
            };
            var forced = new HashSet<string> { ToolDefaults.SoftwarePath, ToolDefaults.ExportPath };
            var prompt = new global::ModelContextProtocol.Protocol.Prompt
            {
                Name = ExportTool,
                Arguments = new List<global::ModelContextProtocol.Protocol.PromptArgument>
                {
                    new() { Name = "softwarePath", Required = true },
                    new() { Name = "exportPath", Required = true },
                    new() { Name = "regexName", Required = false }
                }
            };
            var arguments = new Dictionary<string, JsonElement>
            {
                ["softwarePath"] = JsonSerializer.SerializeToElement("Other")
            };

            // Act
            PromptArguments.MarkPresetsOptional(new[] { prompt }, presets, forced);
            PromptArguments.FillPresets(arguments, new[] { "softwarePath", "exportPath", "regexName" }, presets, forced);

            // Assert
            CollectionAssert.AreEqual(new[] { "regexName" }, prompt.Arguments!.Select(a => a.Name).ToList());
            Assert.AreEqual("PLC_1", arguments["softwarePath"].GetString(), "the forced preset replaces the typed value");
            Assert.AreEqual(@"D:\Export", arguments["exportPath"].GetString());
        }

        private static List<string> PropertiesOf(global::ModelContextProtocol.Server.McpServerTool tool)
        {
            var schema = tool.ProtocolTool.InputSchema;

            return schema.TryGetProperty("properties", out var properties)
                ? properties.EnumerateObject().Select(p => p.Name).ToList()
                : new List<string>();
        }

        private static List<string> RequiredOf(global::ModelContextProtocol.Server.McpServerTool tool)
        {
            var schema = tool.ProtocolTool.InputSchema;

            return schema.TryGetProperty("required", out var required)
                ? required.EnumerateArray().Select(e => e.GetString()!).ToList()
                : new List<string>();
        }
    }
}
