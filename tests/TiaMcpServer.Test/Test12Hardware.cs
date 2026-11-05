using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;
using McpException = global::ModelContextProtocol.McpException;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the hardware (AML) tools: ExportHardware and ImportHardware and their porcelain
    /// commands 'tia pull hardware' and 'tia push hardware'. They check registration, the
    /// '--allow-write' gating, the arguments, the import option and the prompts. These tests do not
    /// connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test12Hardware
    {
        private static readonly string[] ReadTools = { "ExportHardware", "TiaPullHardware" };

        private static readonly string[] WriteTools = { "ImportHardware", "TiaPushHardware" };

        [TestInitialize]
        public void ClassInit()
        {
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);
        }

        private static List<string> RegisteredNames(bool allowWrite) =>
            Program.BuildTools(allowWrite).Select(t => t.ProtocolTool.Name).ToList();

        private static global::ModelContextProtocol.Protocol.Tool Registered(string name) =>
            Program.BuildTools(true).Single(t => t.ProtocolTool.Name == name).ProtocolTool;

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
        public void Test_1200_BuildTools_WithoutAllowWrite_RegistersTheExportsOnly()
        {
            // Arrange / Act
            var names = RegisteredNames(allowWrite: false);

            // Assert
            foreach (var tool in ReadTools)
            {
                Assert.IsTrue(names.Contains(tool), $"'{tool}' only writes a file and must always be registered");
            }

            foreach (var tool in WriteTools)
            {
                Assert.IsFalse(names.Contains(tool), $"'{tool}' changes the project and must not be registered without --allow-write");
            }
        }

        [TestMethod]
        public void Test_1201_BuildTools_WithAllowWrite_AddsTheImports()
        {
            // Arrange / Act
            var names = RegisteredNames(allowWrite: true);

            // Assert
            foreach (var tool in ReadTools.Concat(WriteTools))
            {
                Assert.IsTrue(names.Contains(tool), $"'{tool}' must be registered with --allow-write");
            }
        }

        [TestMethod]
        public void Test_1202_WriteToolAttribute_IsOnTheImportsOnly()
        {
            // Arrange / Act
            var marked = ReadTools.Concat(WriteTools)
                .Where(name => ToolMethod(name).GetCustomAttribute<WriteToolAttribute>() != null)
                .ToList();

            // Assert
            CollectionAssert.AreEquivalent(WriteTools, marked);
        }

        [TestMethod]
        public void Test_1203_Arguments_FollowThePullAndPushToolsOfThePlcProgram()
        {
            // Arrange / Act / Assert
            foreach (var name in new[] { "ExportHardware", "TiaPullHardware" })
            {
                CollectionAssert.AreEqual(new[] { "softwarePath", "exportPath" }, ArgumentNames(name), $"{name}: {string.Join(",", ArgumentNames(name))}");
            }

            CollectionAssert.AreEqual(new[] { "softwarePath", "importPath", "importOption", "dryRun" }, ArgumentNames("ImportHardware"));
            CollectionAssert.AreEquivalent(new[] { "softwarePath", "importPath" }, RequiredArguments("ImportHardware"));

            CollectionAssert.AreEqual(new[] { "softwarePath", "exportPath", "importOption" }, ArgumentNames("TiaPushHardware"));
            CollectionAssert.AreEquivalent(new[] { "softwarePath", "exportPath" }, RequiredArguments("TiaPushHardware"));
            Assert.IsFalse(ArgumentNames("TiaPushHardware").Contains("dryRun"), "the push always imports; the confirmation dialog is the safety");
        }

        [TestMethod]
        public void Test_1204_Titles_ReadLikeTheCommandsTheyStandFor()
        {
            // Arrange / Act / Assert
            Assert.AreEqual("Tia pull hardware", Registered("TiaPullHardware").Title);
            Assert.AreEqual("Tia push hardware", Registered("TiaPushHardware").Title);
        }

        [TestMethod]
        public async Task Test_1205_ImportTools_WriteDisabled_Throw()
        {
            // Arrange
            var previous = WritePolicy.AllowWrite;
            WritePolicy.AllowWrite = false;

            try
            {
                // Act / Assert: the guard runs before any Portal or client access, so no TIA Portal is needed
                var calls = new (string Name, Func<Task> Call)[]
                {
                    ("ImportHardware", () => McpServer.ImportHardware(null!, "sw", "dir")),
                    ("TiaPushHardware", () => McpServer.TiaPushHardware(null!, "sw", "dir"))
                };

                foreach (var (name, call) in calls)
                {
                    var ex = await Assert.ThrowsExactlyAsync<McpException>(call);

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
        public async Task Test_1206_RealImport_WithoutConfirmationDialog_IsRefused()
        {
            // Arrange
            var previous = WritePolicy.AllowWrite;
            WritePolicy.AllowWrite = true;

            try
            {
                // Act / Assert: a client without elicitation cannot confirm, so nothing may be imported
                var push = await Assert.ThrowsExactlyAsync<McpException>(() => McpServer.TiaPushHardware(null!, "sw", "dir"));
                var import = await Assert.ThrowsExactlyAsync<McpException>(() => McpServer.ImportHardware(null!, "sw", "dir", dryRun: false));

                StringAssert.Contains(push.Message, McpServer.ImportConfirmation);
                StringAssert.Contains(import.Message, McpServer.ImportConfirmation);
            }
            finally
            {
                WritePolicy.AllowWrite = previous;
            }
        }

        [TestMethod]
        [DataRow(null, "RetainTiaDevice")]
        [DataRow("", "RetainTiaDevice")]
        [DataRow("RetainTiaDevice", "RetainTiaDevice")]
        [DataRow("overwritetiadevice", "OverwriteTiaDevice")]
        [DataRow(" MoveToParkingLot ", "MoveToParkingLot")]
        public void Test_1207_ParseHardwareImportOption_AcceptsTheOptionsCaseInsensitive(string? text, string expected)
        {
            // Arrange / Act
            var option = Portal.NormalizeHardwareImportOption(text);

            // Assert
            Assert.AreEqual(expected, option);
        }

        [TestMethod]
        public void Test_1208_ParseHardwareImportOption_UnknownOption_ListsTheValidOnes()
        {
            // Arrange / Act
            var ex = Assert.ThrowsExactly<PortalException>(() => Portal.NormalizeHardwareImportOption("Replace"));

            // Assert
            foreach (var option in Portal.HardwareImportOptions)
            {
                StringAssert.Contains(ex.Message, option);
            }
        }

        [TestMethod]
        public void Test_1209_Prompts_UseTheToolNameAndTheAmlFolder()
        {
            // Arrange / Act
            var bodies = new Dictionary<string, string>
            {
                ["ExportHardware"] = McpPrompts.ExportHardware("PLC_1", @"D:\export"),
                ["ImportHardware"] = McpPrompts.ImportHardware("PLC_1", @"D:\export"),
                ["TiaPullHardware"] = McpPrompts.TiaPullHardware("PLC_1", @"D:\export"),
                ["TiaPushHardware"] = McpPrompts.TiaPushHardware("PLC_1", @"D:\export")
            };

            // Assert
            foreach (var pair in bodies)
            {
                StringAssert.Contains(pair.Value, $"Use the {pair.Key} tool", pair.Key);
                StringAssert.Contains(pair.Value, "Hardware/<device name>.aml", pair.Key);
            }

            StringAssert.Contains(bodies["ImportHardware"], "- dryRun: true");
            StringAssert.Contains(bodies["TiaPushHardware"], "Yes, import");
        }
    }
}
