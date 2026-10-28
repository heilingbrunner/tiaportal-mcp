using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the command line presets of 'softwarePath', 'exportPath' and 'preservePath'.
    /// These tests do not connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test8ToolDefaults
    {
        private const string ExportTool = "ExportXmlBlocks";

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
            var args = new[] { "--project-path", @"D:\Projects\My Plant\Plant.ap21", "--software-path", "PC-System_1/Software PLC_1", "-export-path", @"D:\My Export", "--preserve-path", "--with-dependencies" };

            // Act
            var options = CliOptions.ParseArgs(args);

            // Assert
            Assert.AreEqual(@"D:\Projects\My Plant\Plant.ap21", options.ProjectPath);
            Assert.AreEqual("PC-System_1/Software PLC_1", options.SoftwarePath);
            Assert.AreEqual(@"D:\My Export", options.ExportPath);
            Assert.AreEqual(true, options.PreservePath);
            Assert.AreEqual(true, options.WithDependencies);
        }

        [TestMethod]
        public void Test_801_ParseArgs_FlagsAreOffWhenAbsentAndHonourAnExplicitValue()
        {
            // Act
            var absent = CliOptions.ParseArgs(new[] { "--allow-write" });
            var explicitFalse = CliOptions.ParseArgs(new[] { "--preserve-path", "false", "--with-dependencies" });

            // Assert
            Assert.IsFalse(absent.PreservePath, "an absent flag is false, the tool default");
            Assert.IsFalse(absent.WithDependencies);
            Assert.AreEqual(false, explicitFalse.PreservePath);
            Assert.AreEqual(true, explicitFalse.WithDependencies);
        }

        [TestMethod]
        public void Test_802_FillMissing_UsesThePresetOnlyForOmittedOrNullArguments()
        {
            // Arrange
            var presets = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("PLC_1"),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(@"D:\Export"),
                [ToolDefaults.PreservePath] = JsonSerializer.SerializeToElement(true)
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
            Assert.IsTrue(arguments[ToolDefaults.PreservePath].GetBoolean());
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
                [ToolDefaults.PreservePath] = JsonSerializer.SerializeToElement(true)
            };
            var arguments = new Dictionary<string, JsonElement>
            {
                [ToolDefaults.SoftwarePath] = JsonSerializer.SerializeToElement("  "),
                [ToolDefaults.ExportPath] = JsonSerializer.SerializeToElement(string.Empty),
                [ToolDefaults.PreservePath] = JsonSerializer.SerializeToElement(false)
            };

            // Act
            ToolDefaults.FillMissing(arguments, presets.Keys, presets);

            // Assert
            Assert.AreEqual("PLC_1", arguments[ToolDefaults.SoftwarePath].GetString());
            Assert.AreEqual(@"D:\Export", arguments[ToolDefaults.ExportPath].GetString());
            Assert.IsFalse(arguments[ToolDefaults.PreservePath].GetBoolean(), "an explicit false is a value, not an omission");
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
                [ToolDefaults.PreservePath] = JsonSerializer.SerializeToElement(true)
            };
            var declared = new[] { "softwarePath", "exportPath", "regexName", "preservePath" };
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
            Assert.AreEqual("true", arguments["preservePath"].GetString(), "flags reach a prompt as text");
            Assert.IsFalse(arguments.ContainsKey("regexName"), "no preset, no entry");
        }

        [TestMethod]
        public void Test_809_Apply_WithSoftwarePathPreset_EveryToolWithThatArgumentTakesIt()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1" });

            // Act
            var tools = Program.BuildTools(allowWrite: true)
                .Where(t => t.ProtocolTool.InputSchema.TryGetProperty("properties", out var properties)
                    && properties.TryGetProperty(ToolDefaults.SoftwarePath, out _))
                .ToList();

            // Assert
            CollectionAssert.IsSubsetOf(
                new[] { "GetBlocks", "GetBlocksWithHierarchy", "GetTypes" },
                tools.Select(t => t.ProtocolTool.Name).ToList());

            foreach (var tool in tools)
            {
                CollectionAssert.DoesNotContain(RequiredOf(tool), ToolDefaults.SoftwarePath, tool.ProtocolTool.Name);
            }
        }

        [TestMethod]
        public void Test_810_ParseArgs_ReadsForcePresets()
        {
            // Act
            var forced = CliOptions.ParseArgs(new[] { "--software-path", "PLC_1", "-force-presets" });
            var absent = CliOptions.ParseArgs(new[] { "--software-path", "PLC_1" });

            // Assert
            Assert.IsTrue(forced.ForcePresets);
            Assert.IsFalse(absent.ForcePresets, "forcing is opt-in");
        }

        [TestMethod]
        public void Test_811_Configure_ForcesOnlyThePathPresetsThatAreSet()
        {
            // Act
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1", PreservePath = true, ForcePresets = true });
            var forcedSet = ToolDefaults.Forced.ToList();
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1", ExportPath = @"D:\Export" });
            var withoutOption = ToolDefaults.Forced.ToList();

            // Assert
            CollectionAssert.AreEqual(new[] { ToolDefaults.SoftwarePath }, forcedSet, "only a set path preset is forced, never the flags");
            Assert.AreEqual(0, withoutOption.Count, "no --force-presets, nothing forced");
        }

        [TestMethod]
        public void Test_812_Apply_WithForcedPresets_HidesThemFromTheSchema()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions
            {
                ProjectPath = @"D:\Plant.ap21",
                SoftwarePath = "PLC_1",
                ExportPath = @"D:\Export",
                PreservePath = true,
                ForcePresets = true
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

            CollectionAssert.Contains(PropertiesOf(tools.Single(t => t.ProtocolTool.Name == ExportTool)), ToolDefaults.PreservePath, "flags stay overridable");
            CollectionAssert.Contains(PropertiesOf(tools.Single(t => t.ProtocolTool.Name == ExportTool)), "regexName");
        }

        [TestMethod]
        public void Test_813_Apply_WithoutForcing_KeepsThePresetArgumentsInTheSchema()
        {
            // Arrange
            ToolDefaults.Configure(new CliOptions { SoftwarePath = "PLC_1", ExportPath = @"D:\Export" });

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
