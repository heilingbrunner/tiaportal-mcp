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
            var args = new[] { "--project-path", @"D:\Projects\My Plant\Plant.ap21", "--software-path", "PC-System_1/Software PLC_1", "-export-path", @"D:\My Export", "--preserve-path", "true", "--with-dependencies", "true" };

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
        public void Test_801_ParseArgs_IgnoresAnInvalidPreserveValue()
        {
            // Act
            var options = CliOptions.ParseArgs(new[] { "--preserve-path", "maybe" });

            // Assert
            Assert.IsNull(options.PreservePath);
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

        private static List<string> RequiredOf(global::ModelContextProtocol.Server.McpServerTool tool)
        {
            var schema = tool.ProtocolTool.InputSchema;

            return schema.TryGetProperty("required", out var required)
                ? required.EnumerateArray().Select(e => e.GetString()!).ToList()
                : new List<string>();
        }
    }
}
