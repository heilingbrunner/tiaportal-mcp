using System;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the 'doctor' diagnostics. These do not connect to TIA Portal and do not open a
    /// project - the diagnostics are read-only by design.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test6Diagnostics
    {
        [TestInitialize]
        public void ClassInit()
        {
            Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
            Openness.Initialize(Engineering.TiaMajorVersion);
        }

        [TestMethod]
        public void Test_600_Run_ReportsDisconnectedPortalWithoutProject()
        {
            // Arrange
            var portal = new Portal();

            // Act
            var report = Siemens.Diagnostics.Run(portal);

            // Assert
            Assert.IsFalse(report.IsConnected, "A fresh portal must not be reported as connected");
            Assert.IsNull(report.ProjectName, "No project can be open without a connection");
            Assert.IsNull(report.ProjectPath, "No project can be open without a connection");
            Assert.AreEqual(Settings.TiaMajorVersion, report.ActiveTiaMajorVersion, "Active version mismatch");

            Assert.IsNotNull(report.Text);
            StringAssert.StartsWith(report.Text, "Diagnose:");
            StringAssert.Contains(report.Text, "Connected = False");
            StringAssert.Contains(report.Text, "No project open");
            StringAssert.Contains(report.Text, "Active Version: V" + Settings.TiaMajorVersion);
            StringAssert.Contains(report.Text, "Siemens TIA Openness");

            Console.WriteLine(report.Text);
        }

        [TestMethod]
        public void Test_601_GetInstalledTiaPortalVersions_FindsTheConfiguredVersion()
        {
            // Act
            var installations = Siemens.Diagnostics.GetInstalledTiaPortalVersions();

            // Assert
            Assert.IsNotNull(installations);

            var configured = installations.FirstOrDefault(i => i.MajorVersion == Settings.TiaMajorVersion);
            Assert.IsNotNull(configured, $"TIA Portal V{Settings.TiaMajorVersion} was not discovered");
            Assert.IsFalse(string.IsNullOrEmpty(configured!.InstallPath), "Install path must not be empty");
            Assert.IsTrue(configured.EngineeringExists, "Openness assembly not found in the installation");
            Assert.IsTrue(configured.PortalExeExists, "Portal executable not found in the installation");

            foreach (var installation in installations)
            {
                Console.WriteLine($"V{installation.MajorVersion}: {installation.InstallPath}");
            }
        }

        [TestMethod]
        public void Test_602_McpServer_Doctor_ReturnsReportAndStructuredContent()
        {
            // Act
            var response = McpServer.Doctor();

            // Assert
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Report);
            StringAssert.StartsWith(response.Report, "Diagnose:");
            Assert.AreEqual(Settings.TiaMajorVersion, response.ActiveTiaMajorVersion);
            Assert.IsNotNull(response.Installations);
            Assert.IsTrue(response.Installations!.Any(i => i.MajorVersion == Settings.TiaMajorVersion),
                $"TIA Portal V{Settings.TiaMajorVersion} missing from the structured content");

            Console.WriteLine(response.Report);
        }
    }
}
