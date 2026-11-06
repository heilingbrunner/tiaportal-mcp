using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public class Test4Software
    {
        private readonly bool _isInitialized = false;
        private Portal? _portal;

        [TestInitialize]
        public void ClassInit()
        {
            if (!_isInitialized)
            {
                Engineering.TiaMajorVersion = Settings.TiaMajorVersion;
                Openness.Initialize(Engineering.TiaMajorVersion);
            }

            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole(); // or AddDebug(), AddTraceSource(), etc.
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            ILogger<Portal> logger = loggerFactory.CreateLogger<Portal>();
            _portal ??= new(logger);

            var result = _portal.ConnectPortal();
        }

        [TestCleanup]
        public void ClassCleanup()
        {
            // ...
        }

        #region plc software

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath1, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath2, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath3, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath4, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath5, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath6, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath7, "")]
        //[DataRow(Settings.Project2ProjectPath, Settings.Project2PlcSoftwarePath, "")]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, "Safety1st")]
        public void Test_400_CompilePlcSoftware(string projectPath, string softwarePath, string password)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var state = "";
            bool success = Common.OpenProject(_portal, projectPath);

            // Pass an empty string instead of null to satisfy the non-nullable reference type requirement.  
            var result = _portal.CompilePlcSoftware(softwarePath, password);
            if (result != null)
            {
                state = result.State.ToString();
                success &= true;
            }
            else
            {
                state = "Error";
                success &= false;
            }

            success &= Common.CloseProject(_portal, projectPath);

            Console.WriteLine($"CompilePlcSoftware: result={state}");

            Assert.IsFalse(state.Equals("Error"), "Compile PlcSoftware failed");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "0_OBs/Main_1", "")]
        public void Test_401_CompileCodeBlock(string projectPath, string softwarePath, string blockPath, string password)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.CompileCodeBlock(softwarePath, blockPath, password);
            var state = result?.State.ToString() ?? "Error";

            success &= Common.CloseProject(_portal, projectPath);

            Console.WriteLine($"CompileCodeBlock: result={state}");

            Assert.IsNotNull(result, "Compile code block failed");
            Assert.IsFalse(state.Equals("Error"), "Compile code block reported errors");
        }



        #endregion

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "0_OBs/Main_1")]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, "0_OBs/Main_1")]
        public void Test_411_GetBlock(string projectPath, string softwarePath, string blockPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.GetBlock(softwarePath, blockPath);

            if (result != null && blockPath.Contains(result.Name, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Block found: '{blockPath}");
            }
            else
            {
                Console.WriteLine($"Code Block not found. Expected: '{blockPath}'");
            }

            success &= Common.CloseProject(_portal, projectPath);


            Assert.IsNotNull(result, "No code block found");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister/ML_SubstratState")]
        public void Test_412_GetType(string projectPath, string softwarePath, string typePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.GetType(softwarePath, typePath);

            if (result != null && typePath.Contains(result.Name, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Type found: '{typePath}");
            }
            else
            {
                Console.WriteLine($"Type not found. Expected: '{typePath}'");
            }

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsNotNull(result, "No types");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "^M.+")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath1, "")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath2, "")]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, "")]
        public void Test_413_GetBlocks(string projectPath, string softwarePath, string regexName)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.GetBlocks(softwarePath, regexName);

            // write list to console
            Console.WriteLine("Blocks:");
            foreach (var block in result)
            {
                try
                {
                    Console.WriteLine($"- {block.GetType().Name}, {block.Name}, IsConsistent='{block.IsConsistent}', MemoryLayout='{block.MemoryLayout}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing block: {ex.Message}");
                }
            }

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsNotNull(result, "No blocks found");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "")]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, "")]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, "DataTyp.+")]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, "ErrTyp.+")]
        public void Test_414_GetTypes(string projectPath, string softwarePath, string regexName)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.GetTypes(softwarePath, regexName);

            // write list to console
            Console.WriteLine("Types:");
            foreach (var type in result)
            {
                Console.WriteLine($"- {type.GetType().Name}, {type.Name}");
            }

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsNotNull(result, "No types");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "0_OBs/Main_1", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "0_OBs/Main_1", false)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "1_Tests/FC_Block_1", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "1_Tests/DB_Block_1", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "0_OBs/Main_1", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "0_OBs/Main_1", false)]
        //[DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "Common/CarrierRegister/GLOBAL_POSITIONING", true)]
        public void Test_415_ExportXmlBlock(string projectPath, string softwarePath, string exportPath, string blockPath, bool keepStructure)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportXmlBlock(softwarePath, blockPath, exportPath, keepStructure);

            if (result != null)
            {
                Console.WriteLine($"Exported Block: {result.GetType().Name}, {result.Name}");
                success &= true;
            }
            else
            {
                Console.WriteLine("No block exported.");
                success &= false;
            }

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(success, "Failed to export code block");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "Common/CarrierRegister/ML_SubstratState", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "Common/CarrierRegister/ML_CarrierRegisterShort", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "Common/CarrierRegister/ML_CarrierRegisterShort", false)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "Common/CarrierRegister/ML_SubstratState")]
        public void Test_416_ExportXmlType(string projectPath, string softwarePath, string exportPath, string typePath, bool keepStructure)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportXmlType(softwarePath, typePath, exportPath, keepStructure);

            if (result != null)
            {
                Console.WriteLine($"Exported Type: {result.GetType().Name}, {result.Name}");
                success &= true;
            }
            else
            {
                Console.WriteLine("No type exported.");
                success &= false;
            }

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(success, "Failed to export types");
        }

        /// <summary>
        /// ExportTagTable writes one tag table as XML: '&lt;exportPath&gt;/&lt;table&gt;.xml', or with
        /// keepStructure below the system group and the table's groups. Exports the first tag table the
        /// PLC has and checks the file, so no table name has to be hard-coded.
        /// </summary>
        [TestMethod]
        //[DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, false)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, true)]
        public void Test_419_ExportTagTable(string projectPath, string softwarePath, string exportPath, bool keepStructure)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            // The export lands below <exportPath>/<softwarePath>/.
            var softwareRoot = Path.Combine(exportPath, softwarePath);

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var candidate = _portal.GetTagTables(softwarePath).FirstOrDefault();

                if (candidate == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no tag table to export");
                }

                var tagTablePath = _portal.GetTagTablePath(candidate!);
                var result = _portal.ExportTagTable(softwarePath, tagTablePath, exportPath, keepStructure);

                Assert.IsNotNull(result, "No tag table was returned");
                Assert.AreEqual(candidate!.Name, result!.Name, "Name mismatch");

                var files = Directory.GetFiles(softwareRoot, "*.xml", SearchOption.AllDirectories);

                Console.WriteLine($"Exported tag table '{tagTablePath}': {string.Join(", ", files)}");

                //Assert.AreEqual(1, files.Length, "Exactly one XML file must be written");
                Assert.AreEqual(candidate.Name + ".xml", Path.GetFileName(files[0]), "The file is named after the table");
                Assert.IsTrue(new FileInfo(files[0]).Length > 0, "The XML file is empty");

                var relative = files[0].Substring(softwareRoot.Length).TrimStart('\\', '/');

                if (keepStructure)
                {
                    // The system group folder always leads, then the table's groups, then the file.
                    Assert.IsTrue(relative.Contains("\\") || relative.Contains("/"), "keepStructure must write below a group folder");
                    StringAssert.EndsWith(relative.Replace('\\', '/'), tagTablePath + ".xml", "The folder tree must mirror the table's path");
                }
                else
                {
                    var segments = relative.Split('\\', '/');

                    Assert.AreEqual(2, segments.Length, "Without keepStructure the file must sit directly in the system group folder");
                    Assert.AreEqual(candidate.Name + ".xml", segments[1], "The file is named after the table");
                }
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
                DeleteTempDirectory(softwareRoot);
            }
        }

        /// <summary>
        /// An unknown tag table is reported as NotFound before anything is written.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "NoSuchGroup/NoSuchTable")]
        public void Test_420_ExportTagTable_UnknownTableIsNotFound(string projectPath, string softwarePath, string exportPath, string tagTablePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var softwareRoot = Portal.ResolveSoftwareRoot(exportPath, softwarePath);

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                PortalException? thrown = null;

                try
                {
                    _portal.ExportTagTable(softwarePath, tagTablePath, exportPath);
                }
                catch (PortalException ex)
                {
                    thrown = ex;
                }

                Assert.IsNotNull(thrown, "Exporting an unknown tag table must throw a PortalException");
                Assert.AreEqual(PortalErrorCode.NotFound, thrown!.Code, "Error code mismatch");
                Assert.IsFalse(
                    Directory.Exists(softwareRoot) && Directory.GetFiles(softwareRoot, "*.*", SearchOption.AllDirectories).Length > 0,
                    "Nothing may be written for an unknown table");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
                DeleteTempDirectory(softwareRoot);
            }
        }

        /// <summary>
        /// ExportOpcUaXml writes the OPC UA data of one PLC as '&lt;exportPath&gt;/&lt;softwarePath&gt;/OPC UA/&lt;PLC name&gt;.xml'.
        /// Needs the OPC UA export provider, so it is inconclusive when TIA Portal does not offer it.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0)]
        public void Test_426_ExportOpcUaXml(string projectPath, string softwarePath, string exportPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var softwareRoot = Portal.ResolveSoftwareRoot(exportPath, softwarePath);

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                string plcName;
                string filePath;

                try
                {
                    var (plc, file) = _portal.ExportOpcUaXml(softwarePath, exportPath);
                    plcName = plc.Name;
                    filePath = file;
                }
                catch (PortalException ex) when (ex.Code == PortalErrorCode.NotSupported)
                {
                    Assert.Inconclusive(ex.Message);
                    return;
                }

                Console.WriteLine($"Exported OPC UA data of '{softwarePath}': {filePath}");

                Assert.IsTrue(File.Exists(filePath), "The XML file was not written");
                Assert.IsTrue(new FileInfo(filePath).Length > 0, "The XML file is empty");
                Assert.AreEqual(FileNames.Safe(plcName) + ".xml", Path.GetFileName(filePath), "The file is named after the PLC");
                Assert.AreEqual(Path.Combine(softwareRoot, "OPC UA"), Path.GetDirectoryName(filePath), "The file lands in the 'OPC UA' folder below <exportPath>/<softwarePath>");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
                DeleteTempDirectory(softwareRoot);
            }
        }

        /// <summary>
        /// An unknown PLC software is reported as NotFound before anything is written.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, "NoSuchPlc", Settings.Project1ExportPath0)]
        public void Test_427_ExportOpcUaXml_UnknownSoftwareIsNotFound(string projectPath, string softwarePath, string exportPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var softwareRoot = Portal.ResolveSoftwareRoot(exportPath, softwarePath);

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                PortalException? thrown = null;

                try
                {
                    _portal.ExportOpcUaXml(softwarePath, exportPath);
                }
                catch (PortalException ex)
                {
                    thrown = ex;
                }

                Assert.IsNotNull(thrown, "Exporting an unknown PLC must throw a PortalException");
                Assert.AreEqual(PortalErrorCode.NotFound, thrown!.Code, "Error code mismatch");
                Assert.IsFalse(
                    Directory.Exists(softwareRoot) && Directory.GetFiles(softwareRoot, "*.*", SearchOption.AllDirectories).Length > 0,
                    "Nothing may be written for an unknown PLC");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
                DeleteTempDirectory(softwareRoot);
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "0_OBs/Main_1", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "0_OBs/Main_1", false)]
        //[DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "1_Tests/DB_Block_1")] // no docs from DB
        //[DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "1_Tests/FC_Block_1")] // no docs from OB/FB/FC with mixed ProgrammingLanguage
        public void Test_421_ExportBlockAsDocuments(string projectPath, string softwarePath, string exportPath, string blockPath, bool keepStructure)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportAsDocuments(softwarePath, blockPath, exportPath, keepStructure);

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(result, "Failed to export blocks as documents (s7dcl/s7res)");
        }

        /// <summary>
        /// Exports the first consistent PLC data type as a SIMATIC source document set. The type
        /// is discovered rather than hardcoded so the test survives edits to the sample project.
        /// Asserts on the reported files, not on a '.s7dcl' name: Openness decides the names.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, false)]
        public void Test_423_ExportTypeAsDocuments(string projectPath, string softwarePath, string exportPath, bool keepStructure)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var candidate = _portal.GetTypes(softwarePath).FirstOrDefault(t => t.IsConsistent);

                if (candidate == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent PLC data type to export");
                }

                var typePath = _portal.GetTypePath(candidate!);
                var info = _portal.ExportTypeAsDocuments(softwarePath, typePath, exportPath, keepStructure);

                Console.WriteLine($"Exported '{typePath}' as documents, state '{info.State}':");

                foreach (var file in info.Files)
                {
                    Console.WriteLine($"- {file}");
                    Assert.IsTrue(File.Exists(file), $"Reported document '{file}' does not exist");
                }

                Assert.AreNotEqual(0, info.Files.Count, "The export reported no document files");

                if (keepStructure)
                {
                    StringAssert.Contains(
                        info.Directory,
                        "PLC data types",
                        "keepStructure must place documents below the 'PLC data types' system folder");
                }
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        /// <summary>
        /// PreviewImport must classify correctly and change nothing: a name already in the PLC
        /// but in a different group is a conflict for types, an unknown name is a create.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_481_PreviewImport(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            ModelContextProtocol.McpServer.Portal = _portal;

            var stage = Path.Combine(Path.GetTempPath(), "TiaMcpServerPreview", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path.Combine(stage, softwarePath));

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var existing = _portal.GetTypes(softwarePath).FirstOrDefault();

                if (existing == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no PLC data type");
                }

                // One file named after a type that exists, one that does not.
                File.WriteAllText(Path.Combine(stage, softwarePath, existing!.Name + ".s7dcl"), "TYPE\nEND_TYPE\n");
                File.WriteAllText(Path.Combine(stage, softwarePath, "ZZ_NewType_ZZ.s7dcl"), "TYPE\nEND_TYPE\n");

                var typeCountBefore = _portal.GetTypes(softwarePath).Count;
                var preview = ModelContextProtocol.McpServer.PreviewImport(softwarePath, stage, "type", string.Empty);

                Console.WriteLine(preview.Message);

                foreach (var item in preview.Items!)
                {
                    Console.WriteLine($"- {item.Name}: {item.Effect} -> {item.TargetPath} (existing: {item.ExistingPath}) {item.Note}");
                }

                Assert.AreEqual(1, preview.CreateCount, "The unknown name should be reported as a create");
                Assert.AreEqual(1, preview.OverwriteCount + preview.ConflictCount, "The known name should be reported as overwrite or conflict");
                Assert.AreEqual(typeCountBefore, _portal.GetTypes(softwarePath).Count, "PreviewImport changed the project");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);

                try
                {
                    Directory.Delete(stage, recursive: true);
                }
                catch (Exception)
                {
                    // Temp cleanup only.
                }
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_471_FindInCode(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                // Every source document and every SimaticML export names its own object, so a
                // search for a real block name must find at least that block.
                var block = _portal.GetBlocks(softwarePath).FirstOrDefault(b => b.IsConsistent);

                if (block == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent block");
                }

                var result = _portal.FindInCode(softwarePath, block!.Name, block.Name);

                Console.WriteLine($"'{block.Name}': {result.Items.Count} hit(s) across {result.ObjectsSearched} object(s), " +
                                  $"{result.Unsearchable.Count} unsearchable");

                foreach (var hit in result.Items.Take(5))
                {
                    Console.WriteLine($"- {hit.ObjectPath}:{hit.Line} [{hit.Format}] {hit.Text}");
                }

                Assert.IsTrue(result.Items.Count > 0, $"Searching for '{block.Name}' found nothing");

                // An impossible pattern must come back empty rather than throwing.
                Assert.AreEqual(0, _portal.FindInCode(softwarePath, "ZZ_no_such_text_ZZ").Items.Count);
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        /// <summary>
        /// The write wrapper must commit on success and roll back on failure. The project is
        /// closed without saving either way, so nothing survives this test.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_461_TransactionCommitsAndRollsBack(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            var committed = "McpTx_Committed_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            var rolledBack = "McpTx_RolledBack_" + Guid.NewGuid().ToString("N").Substring(0, 6);

            try
            {
                // Commit path: a body that returns normally must leave its edit in place.
                _portal.InTransaction("test: commit", () => _portal.CreateTagTable(softwarePath, string.Empty, committed));

                Assert.IsNotNull(
                    _portal.GetTagTable(softwarePath, committed),
                    "A committed transaction did not keep its tag table");

                // Rollback path: a body that throws must leave nothing behind.
                try
                {
                    _portal.InTransaction<bool>("test: rollback", () =>
                    {
                        _portal.CreateTagTable(softwarePath, string.Empty, rolledBack);

                        throw new InvalidOperationException("deliberate failure inside the transaction");
                    });

                    Assert.Fail("The deliberate failure did not propagate out of InTransaction");
                }
                catch (InvalidOperationException)
                {
                    // expected
                }

                var survivor = _portal.GetTagTable(softwarePath, rolledBack);

                if (survivor != null)
                {
                    Assert.Inconclusive(
                        "The tag table survived a failed transaction, so this TIA Portal did not grant a transaction " +
                        "and the wrapper fell back to an unwrapped write. Atomicity cannot be verified in this environment.");
                }
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_451_GetPlcSummary(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var summary = _portal.GetPlcSummary(softwarePath);

                Console.WriteLine($"{summary.Name}: {summary.BlockCount} blocks, {summary.TypeCount} types, " +
                                  $"{summary.TagCount} tags in {summary.TagTableCount} tables, " +
                                  $"{summary.InconsistentObjects.Count} inconsistent, last modified {summary.LastModified}");
                Console.WriteLine($"  languages: {string.Join(", ", summary.BlocksByLanguage.Select(p => $"{p.Key}={p.Value}"))}");
                Console.WriteLine($"  kinds: {string.Join(", ", summary.BlocksByKind.Select(p => $"{p.Key}={p.Value}"))}");

                // The counts must agree with the collectors they are built from.
                Assert.AreEqual(_portal.GetBlocks(softwarePath).Count, summary.BlockCount, "Block count disagrees with GetBlocks");
                Assert.AreEqual(_portal.GetTypes(softwarePath).Count, summary.TypeCount, "Type count disagrees with GetTypes");
                Assert.AreEqual(summary.BlockCount, summary.BlocksByKind.Values.Sum(), "Kind histogram does not add up");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "source")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "documents")]
        public void Test_441_GetBlockAs(string projectPath, string softwarePath, string kind)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var candidates = _portal.GetBlocks(softwarePath).Where(b => b.IsConsistent).ToList();

                if (candidates.Count == 0)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent block");
                }

                // LAD/FBD/GRAPH have no source form and STL/mixed have no document. Both are
                // rejected on purpose, so walk the blocks until one has the requested form.
                foreach (var block in candidates)
                {
                    var blockPath = _portal.GetBlockPath(block);
                    SourceTextResult result;

                    try
                    {
                        result = kind switch
                        {
                            "xml" => _portal.GetXmlBlock(softwarePath, blockPath),
                            "source" => _portal.GetSourceBlock(softwarePath, blockPath),
                            _ => _portal.GetDocumentsBlock(softwarePath, blockPath)
                        };
                    }
                    catch (PortalException pex) when (pex.Code == PortalErrorCode.NotSupported)
                    {
                        continue;
                    }

                    Assert.AreEqual(kind, result.Format, "The reader returned another kind than requested");

                    Console.WriteLine($"{result.Path} [{result.Format}] {result.TotalChars} chars, files: {string.Join(", ", result.FileNames)}");
                    Console.WriteLine(result.Text.Length > 600 ? result.Text.Substring(0, 600) : result.Text);

                    Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text), "No source text was returned");
                    StringAssert.Contains(result.Text, block.Name, "The source does not mention the block name");

                    return;
                }

                Assert.Inconclusive($"No consistent block of '{softwarePath}' has a '{kind}' form");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "xml", null)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "source", ".udt")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "documents", null)]
        public void Test_444_GetTypeAs(string projectPath, string softwarePath, string kind, string? expectedExtension)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var type = _portal.GetTypes(softwarePath).FirstOrDefault(t => t.IsConsistent);

                if (type == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent PLC data type");
                }

                var typePath = _portal.GetTypePath(type!);
                SourceTextResult result;

                try
                {
                    result = kind switch
                    {
                        "xml" => _portal.GetXmlType(softwarePath, typePath),
                        "source" => _portal.GetSourceType(softwarePath, typePath),
                        _ => _portal.GetDocumentsType(softwarePath, typePath)
                    };
                }
                catch (PortalException pex) when (kind == "documents" && pex.Code == PortalErrorCode.NotSupported)
                {
                    // Type documents need TIA Portal V21; older versions reject them on purpose.
                    Assert.Inconclusive($"No type documents on this TIA Portal version: {pex.Message}");

                    return;
                }

                Console.WriteLine($"{result.Path} [{result.Format}] {result.TotalChars} chars, files: {string.Join(", ", result.FileNames)}");

                Assert.AreEqual(kind, result.Format, "The reader returned another kind than requested");
                Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text), "No text was returned");
                StringAssert.Contains(result.Text, type!.Name, "The text does not mention the type name");

                if (expectedExtension != null)
                {
                    Assert.IsTrue(
                        result.FileNames.Any(f => f.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase)),
                        $"Expected a '{expectedExtension}' file, got: {string.Join(", ", result.FileNames)}");
                }
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        /// <summary>Truncation must cut on a line boundary and report the full length.</summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_445_GetXmlBlockTruncatesOnLineBoundary(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var block = _portal.GetBlocks(softwarePath).FirstOrDefault(b => b.IsConsistent);

                if (block == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent block");
                }

                var result = _portal.GetXmlBlock(softwarePath, _portal.GetBlockPath(block!), maxChars: 200);

                Assert.IsTrue(result.Truncated, "A SimaticML export is longer than 200 characters");
                Assert.IsTrue(result.TotalChars > 200, "TotalChars must report the untruncated length");
                Assert.IsTrue(result.Text.Length <= 200, "The text exceeds maxChars");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        /// <summary>LAD, FBD and GRAPH blocks have no external source form and must be rejected, not converted.</summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_446_GetSourceBlockRejectsBlockWithoutSourceForm(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var block = _portal.GetBlocks(softwarePath)
                    .FirstOrDefault(b => b.IsConsistent && b.ProgrammingLanguage.ToString() is "LAD" or "FBD" or "GRAPH");

                if (block == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent LAD, FBD or GRAPH block");
                }

                var blockPath = _portal.GetBlockPath(block!);

                var exception = Assert.ThrowsException<PortalException>(() => _portal.GetSourceBlock(softwarePath, blockPath));

                Assert.AreEqual(PortalErrorCode.NotSupported, exception.Code);
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        /// <summary>STL blocks have no source document: the reader says so instead of returning XML.</summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_447_GetDocumentsBlockRejectsStlWithoutFallback(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var block = _portal.GetBlocks(softwarePath)
                    .FirstOrDefault(b => b.IsConsistent && b.ProgrammingLanguage.ToString() == "STL");

                if (block == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent STL block");
                }

                var blockPath = _portal.GetBlockPath(block!);

                var exception = Assert.ThrowsException<PortalException>(() => _portal.GetDocumentsBlock(softwarePath, blockPath));

                Assert.AreEqual(PortalErrorCode.NotSupported, exception.Code);
                StringAssert.Contains(exception.Message, "GetXmlBlock", "The error must point to the XML reader");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        /// <summary>Reading source must not leave scratch directories behind.</summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_442_GetXmlBlockCleansUp(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var scratchRoot = Path.Combine(Path.GetTempPath(), "TiaMcpServer");
                var before = Directory.Exists(scratchRoot) ? Directory.GetDirectories(scratchRoot).Length : 0;

                var block = _portal.GetBlocks(softwarePath).FirstOrDefault(b => b.IsConsistent);

                if (block == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent block");
                }

                _portal.GetXmlBlock(softwarePath, _portal.GetBlockPath(block!));

                var after = Directory.Exists(scratchRoot) ? Directory.GetDirectories(scratchRoot).Length : 0;

                Assert.AreEqual(before, after, "GetXmlBlock left a scratch directory behind");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_443_GetBlockInterface(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var dataBlock = _portal.GetBlocks(softwarePath)
                    .FirstOrDefault(b => b is DataBlock);

                if (dataBlock == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no data block");
                }

                var members = _portal.GetBlockInterface(softwarePath, _portal.GetBlockPath(dataBlock!));

                foreach (var member in members)
                {
                    Console.WriteLine($"- {member.Name} : {member.DataTypeName} ({member.Attributes.Count} attributes)");
                }

                Assert.IsNotNull(members, "No interface members were returned");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_431_ResolveObjectPath(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var block = _portal.GetBlocks(softwarePath).FirstOrDefault();

                if (block == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no block to resolve");
                }

                var expected = _portal.GetBlockPath(block!);
                var matches = _portal.ResolveObjectPath(softwarePath, block!.Name);

                foreach (var match in matches)
                {
                    Console.WriteLine($"- {match.Kind}: {match.Path}");
                }

                Assert.IsTrue(
                    matches.Any(m => m.Kind == "block" && m.Path == expected),
                    $"Resolving '{block.Name}' did not yield '{expected}'");

                Assert.AreEqual(0, _portal.ResolveObjectPath(softwarePath, "ZZ_does_not_exist_ZZ").Count);
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath1)]
        public void Test_432_GetSoftwarePaths(string projectPath, string expectedPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var paths = _portal.GetSoftwarePaths();

                foreach (var path in paths)
                {
                    Console.WriteLine($"- {path}");
                }

                CollectionAssert.Contains(paths, expectedPath, $"'{expectedPath}' was not enumerated");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        #region source files

        private static string NewTempDirectory(string purpose) =>
            Path.Combine(Path.GetTempPath(), "TiaMcpServerSourceTests", purpose, Guid.NewGuid().ToString("N"));

        private static void DeleteTempDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (Exception)
            {
                // Temp cleanup only.
            }
        }

        private static string GroupOf(string objectPath) =>
            objectPath.Contains("/") ? objectPath.Substring(0, objectPath.LastIndexOf('/')) : string.Empty;

        /// <summary>
        /// ExportSourceBlock writes one block as an external source file in the format the compiler
        /// reads back; a data block comes out as '.db'. Without keepStructure the file lands directly
        /// in the export folder, with it below the block's group.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "1_Tests/DB_Block_1", false)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "1_Tests/DB_Block_1", true)]
        public void Test_491_ExportSourceBlock(string projectPath, string softwarePath, string exportPath, string blockPath, bool keepStructure)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            exportPath = Path.Combine(exportPath, "SourceBlock");

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var result = _portal.ExportSourceBlock(softwarePath, blockPath, exportPath, keepStructure: keepStructure);

                Console.WriteLine($"Exported '{result.Path}' as {result.Format} ({result.Language}): {result.File}");

                Assert.AreEqual(Path.GetFileName(blockPath), result.Name, "Name mismatch");
                Assert.AreEqual(blockPath, result.Path, "Path mismatch");
                Assert.AreEqual(".db", result.Format, "A data block must be generated as '.db'");
                Assert.AreEqual(".db", Path.GetExtension(result.File), "The file extension must follow the format");
                Assert.IsTrue(File.Exists(result.File), "The source file does not exist");

                if (keepStructure)
                {
                    StringAssert.Contains(result.File, GroupOf(blockPath), "keepStructure must keep the block's group in the folder tree");
                }
                else
                {
                    Assert.IsFalse(result.File.Contains(GroupOf(blockPath)), "Without keepStructure the file must not sit in a group folder");
                }
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
                DeleteTempDirectory(exportPath);
            }
        }

        #endregion

    }
}
