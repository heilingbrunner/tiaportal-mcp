using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the '&lt;exportPath&gt;/&lt;softwarePath&gt;' folder every export and import uses.
    /// These tests do not connect to TIA Portal and do not open a project.
    /// </summary>
    [TestClass]
    public class Test9SoftwareRoot
    {
        [TestMethod]
        public void Test_900_ResolveSoftwareRoot_AppendsTheSoftwarePath()
        {
            // Arrange / Act
            var root = Portal.ResolveSoftwareRoot(@"D:\Export", "PLC_1");

            // Assert
            Assert.AreEqual(@"D:\Export\PLC_1", root);
        }

        [TestMethod]
        public void Test_905_ResolveSoftwareRoot_MakesARelativeBaseAbsolute()
        {
            // Arrange / Act
            var root = Portal.ResolveSoftwareRoot("Project", "PLC_1");

            // Assert: TIA Portal exports into a directory it is given, so it must not be relative
            Assert.IsTrue(System.IO.Path.IsPathRooted(root), root);
            Assert.AreEqual(System.IO.Path.Combine(System.IO.Path.GetFullPath("Project"), "PLC_1"), root);
        }

        [TestMethod]
        public void Test_901_ResolveSoftwareRoot_TurnsEverySlashIntoAFolderLevel()
        {
            // Arrange / Act
            var root = Portal.ResolveSoftwareRoot(@"D:\Export", "Group1/PC-System_1/Software PLC_1");

            // Assert
            Assert.AreEqual(@"D:\Export\Group1\PC-System_1\Software PLC_1", root);
        }

        [TestMethod]
        public void Test_902_ResolveSoftwareRoot_IgnoresEmptySegments()
        {
            // Arrange / Act
            var root = Portal.ResolveSoftwareRoot(@"D:\Export", "/Group1//PLC_1/");

            // Assert
            Assert.AreEqual(@"D:\Export\Group1\PLC_1", root);
        }

        [TestMethod]
        [DataRow("..")]
        [DataRow("../PLC_1")]
        [DataRow("Group1/../../Elsewhere")]
        [DataRow("PLC_1/.")]
        [DataRow("PLC:1")]
        [DataRow("PLC*1")]
        [DataRow(".. ")]
        [DataRow("...")]
        [DataRow("PLC_1 ")]
        [DataRow("Group1/NUL")]
        [DataRow("com1.txt")]
        public void Test_903_ResolveSoftwareRoot_RejectsSegmentsThatEscapeOrAreNotFolderNames(string softwarePath)
        {
            // Act
            var ex = Assert.ThrowsExactly<PortalException>(() => Portal.ResolveSoftwareRoot(@"D:\Export", softwarePath));

            // Assert
            Assert.AreEqual(PortalErrorCode.InvalidParams, ex.Code);
        }

        [TestMethod]
        [DataRow("", "PLC_1")]
        [DataRow("   ", "PLC_1")]
        [DataRow(@"D:\Export", "")]
        [DataRow(@"D:\Export", "  ")]
        [DataRow(@"D:\Export", "/")]
        public void Test_904_ResolveSoftwareRoot_RejectsAnEmptyBaseOrSoftwarePath(string baseDirectory, string softwarePath)
        {
            // Act
            var ex = Assert.ThrowsExactly<PortalException>(() => Portal.ResolveSoftwareRoot(baseDirectory, softwarePath));

            // Assert
            Assert.AreEqual(PortalErrorCode.InvalidParams, ex.Code);
        }
    }
}
