using System.Linq;
using System.Reflection;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for which tools the server registers. The project-mutating tools share the McpServer
    /// class with the read-only tools and are marked [WriteTool]; without '--allow-write' they must
    /// not be registered at all, so they stay out of 'tools/list'. These tests do not connect to
    /// TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test7ToolRegistration
    {
        private const string SampleReadTool = "GetTags";
        private const string SampleWriteTool = "CreateTag";
        private const string SampleDocumentWriteTool = "ImportSources";

        [TestInitialize]
        public void ClassInit()
        {
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);
        }

        [TestMethod]
        public void Test_700_BuildTools_WithoutAllowWrite_ExcludesEveryWriteTool()
        {
            // Arrange
            var writeMethods = WriteToolMethods();

            // Act
            var names = Program.BuildTools(allowWrite: false).Select(t => t.ProtocolTool.Name).ToList();

            // Assert
            Assert.IsTrue(writeMethods.Count > 0, "The write tools must carry [WriteTool]");
            Assert.IsTrue(names.Contains(SampleReadTool), "Read tools are always registered");
            Assert.IsFalse(names.Contains(SampleWriteTool), "A write tool must not be registered without --allow-write");
            Assert.IsFalse(names.Contains(SampleDocumentWriteTool), "A write tool must not be registered without --allow-write");
        }

        [TestMethod]
        public void Test_701_BuildTools_WithAllowWrite_AddsExactlyTheWriteTools()
        {
            // Arrange
            var writeMethods = WriteToolMethods();

            // Act
            var withoutWrite = Program.BuildTools(allowWrite: false).ToList();
            var withWrite = Program.BuildTools(allowWrite: true).Select(t => t.ProtocolTool.Name).ToList();

            // Assert
            Assert.AreEqual(writeMethods.Count, withWrite.Count - withoutWrite.Count,
                "--allow-write must add exactly the [WriteTool] tools");
            Assert.IsTrue(withWrite.Contains(SampleWriteTool));
            Assert.IsTrue(withWrite.Contains(SampleDocumentWriteTool));
            Assert.IsTrue(withWrite.Contains(SampleReadTool));
        }

        [TestMethod]
        public void Test_702_WriteToolAttribute_IsOnlyUsedOnTools()
        {
            // Arrange
            var writeMethods = WriteToolMethods();

            // Act
            var withoutToolAttribute = writeMethods
                .Where(m => m.GetCustomAttribute<global::ModelContextProtocol.Server.McpServerToolAttribute>() == null)
                .Select(m => m.Name)
                .ToList();

            // Assert
            Assert.AreEqual(0, withoutToolAttribute.Count,
                "[WriteTool] on a method that is not a tool has no effect: " + string.Join(", ", withoutToolAttribute));
        }

        [TestMethod]
        public void Test_703_BuildTools_AddsOptionalPortalIdToProjectTools()
        {
            // Arrange
            var tools = Program.BuildTools(allowWrite: true).ToList();

            // Act
            var getTags = tools.Single(t => t.ProtocolTool.Name == SampleReadTool).ProtocolTool.InputSchema;
            var hasPortalId = getTags.GetProperty("properties").TryGetProperty(PortalSelection.ArgumentName, out var portalId);
            var isRequired = getTags.TryGetProperty("required", out var required) &&
                             required.EnumerateArray().Any(r => r.GetString() == PortalSelection.ArgumentName);

            // Assert
            Assert.IsTrue(hasPortalId, "Project tools must accept 'portalId' to name their TIA Portal instance");
            Assert.AreEqual("integer", portalId.GetProperty("type").GetString());
            Assert.IsFalse(isRequired, "'portalId' must stay optional so single-instance clients need not pass it");
        }

        [TestMethod]
        public void Test_704_BuildTools_LeavesConnectAndGetPortalsSchemasAlone()
        {
            // Arrange
            var tools = Program.BuildTools(allowWrite: false).ToList();

            // Act
            var connect = tools.Single(t => t.ProtocolTool.Name == "Connect").ProtocolTool.InputSchema;
            var getPortals = tools.Single(t => t.ProtocolTool.Name == "GetPortals").ProtocolTool.InputSchema;
            var getPortalsHasPortalId = getPortals.TryGetProperty("properties", out var properties) &&
                                        properties.TryGetProperty(PortalSelection.ArgumentName, out _);

            // Assert
            Assert.IsTrue(connect.GetProperty("properties").TryGetProperty(PortalSelection.ArgumentName, out _),
                "Connect declares 'portalId' itself");
            Assert.IsFalse(getPortalsHasPortalId, "GetPortals must work while several instances are attached");
        }

        [TestMethod]
        public void Test_705_BuildTools_OpenTiaProjectDeclaresPortalIdButNotCancellationToken()
        {
            // Arrange
            var tools = Program.BuildTools(allowWrite: false).ToList();

            // Act
            var properties = tools.Single(t => t.ProtocolTool.Name == "OpenTiaProject")
                .ProtocolTool.InputSchema.GetProperty("properties");
            var names = properties.EnumerateObject().Select(p => p.Name).ToList();

            // Assert
            Assert.IsTrue(names.Contains("path"));
            Assert.IsTrue(names.Contains(PortalSelection.ArgumentName),
                "OpenTiaProject attaches to the instance it is given, so it declares 'portalId' itself");
            Assert.IsFalse(names.Any(n => n.IndexOf("cancellation", System.StringComparison.OrdinalIgnoreCase) >= 0),
                "The CancellationToken is bound by the SDK, not exposed to the client");
        }

        [TestMethod]
        public void Test_706_ToolOrdering_SortsByTitleThenName()
        {
            // Arrange
            var tools = new[]
            {
                new global::ModelContextProtocol.Protocol.Tool { Name = "Zeta", Title = "connect to TIA Portal" },
                new global::ModelContextProtocol.Protocol.Tool { Name = "Beta" },
                new global::ModelContextProtocol.Protocol.Tool { Name = "Alpha", Title = "Close project" },
                new global::ModelContextProtocol.Protocol.Tool { Name = "Gamma", Title = "Connect to TIA Portal" }
            };

            // Act
            var names = ToolOrdering.Sort(tools).Select(t => t.Name).ToList();

            // Assert: "Beta" has no title and sorts by name; equal titles (ignoring case) fall back to Name
            CollectionAssert.AreEqual(new[] { "Beta", "Alpha", "Gamma", "Zeta" }, names);
        }

        [TestMethod]
        public void Test_707_ToolOrdering_SortsAllRegisteredTools()
        {
            // Arrange
            var registered = Program.BuildTools(allowWrite: true).Select(t => t.ProtocolTool).ToList();

            // Act
            var sorted = ToolOrdering.Sort(registered);
            var displayNames = sorted.Select(ToolOrdering.DisplayName).ToList();

            // Assert
            Assert.AreEqual(registered.Count, sorted.Count, "Sorting must keep every tool");
            for (var i = 1; i < displayNames.Count; i++)
            {
                Assert.IsTrue(
                    System.StringComparer.OrdinalIgnoreCase.Compare(displayNames[i - 1], displayNames[i]) <= 0,
                    $"'{displayNames[i - 1]}' must not come after '{displayNames[i]}'");
            }
        }

        private static System.Collections.Generic.List<MethodInfo> WriteToolMethods() =>
            typeof(McpServer)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttribute<WriteToolAttribute>() != null)
                .ToList();
    }
}
