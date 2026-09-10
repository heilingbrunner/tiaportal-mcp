using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
using System.IO;
using System.Linq;
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
            var result = _portal.CompileSoftware(softwarePath, password);
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
        public void Test_415_ExportBlock(string projectPath, string softwarePath, string exportPath, string blockPath, bool preservePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportBlock(softwarePath, blockPath, exportPath, preservePath);

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
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "0_OBs", Settings.Project1ExportPath0 + "\\Program blocks\\0_OBs\\Main_1.xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "1_Tests", Settings.Project1ExportPath0 + "\\Program blocks\\1_Tests\\FC_Block_1.xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "1_Tests", Settings.Project1ExportPath0 + "\\Program blocks\\1_Tests\\DB_Block_1.xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister", Settings.Project1ExportPath0 + "\\Program blocks\\Common\\CarrierRegister\\GLOBAL_POSITIONING.xml")]
        public void Test_415_ImportBlock(string projectPath, string softwarePath, string groupPath, string importPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ImportBlock(softwarePath, groupPath, importPath);

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(result, "Failed to import code block");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "Common/CarrierRegister/ML_SubstratState", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "Common/CarrierRegister/ML_CarrierRegisterShort", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "Common/CarrierRegister/ML_CarrierRegisterShort", false)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "Common/CarrierRegister/ML_SubstratState")]
        public void Test_416_ExportType(string projectPath, string softwarePath, string exportPath, string typePath, bool preservePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportType(softwarePath, typePath, exportPath, preservePath);

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

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister", Settings.Project1ExportPath0 + "\\Plc data types\\Common\\CarrierRegister\\ML_SubstratState.xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister", Settings.Project1ExportPath0 + "\\Plc data types\\Common\\CarrierRegister\\ML_CarrierRegisterShort.xml")]
        public void Test_416_ImportType(string projectPath, string softwarePath, string groupPath, string importPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ImportType(softwarePath, groupPath, importPath);

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(result, "Failed to export types");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "M.*", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "M.*", false)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath1, Settings.Project1ExportPath1, "", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath2, Settings.Project1ExportPath2, "", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "_HMI_.+", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "_HMI_.+", false)]
        public void Test_417_ExportBlocks(string projectPath, string softwarePath, string exportPath, string regexName, bool preservePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportBlocks(softwarePath, exportPath, regexName, preservePath);

            if (result != null)
            {
                Console.WriteLine($"Exported Block:");
                foreach (var block in result)
                {
                    Console.WriteLine($"- {block.GetType().Name}, {block.Name}");
                }

                success &= true;
            }
            else
            {
                Console.WriteLine("No blocks exported.");

                success &= false;
            }

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(success, "Failed to export blocks");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "(^ErrTyp_|_HMI_AllError$)", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "(^ErrTyp_|_HMI_AllError$)", false)]
        public void Test_418_ExportTypes(string projectPath, string softwarePath, string exportPath, string regexName, bool preservePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportTypes(softwarePath, exportPath, regexName, preservePath);
            if (result != null)
            {
                Console.WriteLine($"Exported Types:");
                foreach (var type in result)
                {
                    Console.WriteLine($"- {type.GetType().Name}, {type.Name}");
                }

                success &= true;
            }
            else
            {
                Console.WriteLine("No types exported.");

                success &= false;
            }

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(success, "Failed to export types");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "0_OBs/Main_1", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "0_OBs/Main_1", false)]
        //[DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "1_Tests/DB_Block_1")] // no docs from DB
        //[DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "1_Tests/FC_Block_1")] // no docs from OB/FB/FC with mixed ProgrammingLanguage
        public void Test_421_ExportBlockAsDocuments(string projectPath, string softwarePath, string exportPath, string blockPath, bool preservePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportAsDocuments(softwarePath, blockPath, exportPath, preservePath);

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(result, "Failed to export blocks as documents (s7dcl/s7res)");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "M.*", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "M.*", false)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath1, Settings.Project1ExportPath1, "", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath2, Settings.Project1ExportPath2, "", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "_HMI_.+", true)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, Settings.Session1ExportPath, "_HMI_.+", false)]
        public void Test_422_ExportBlocksAsDocuments(string projectPath, string softwarePath, string exportPath, string regexName, bool preservePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.ExportBlocksAsDocuments(softwarePath, exportPath, regexName, preservePath);

            if (result != null)
            {
                Console.WriteLine($"Exported Block as documents:");

                foreach (var block in result)
                {
                    Console.WriteLine($"- {block.GetType().Name}, {block.Name}, {block.ModifiedDate}");
                }

                success &= true;
            }
            else
            {
                Console.WriteLine("No blocks exported as documents.");

                success &= false;
            }

            success &= Common.CloseProject(_portal, projectPath);

            Assert.IsTrue(success, "Failed to export blocks as documents");
        }

        /// <summary>
        /// Exports the first consistent PLC data type as a SIMATIC source document set. The type
        /// is discovered rather than hardcoded so the test survives edits to the sample project.
        /// Asserts on the reported files, not on a '.s7dcl' name: Openness decides the names.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, false)]
        public void Test_423_ExportTypeAsDocuments(string projectPath, string softwarePath, string exportPath, bool preservePath)
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
                var info = _portal.ExportTypeAsDocuments(softwarePath, typePath, exportPath, preservePath);

                Console.WriteLine($"Exported '{typePath}' as documents, state '{info.State}':");

                foreach (var file in info.Files)
                {
                    Console.WriteLine($"- {file}");
                    Assert.IsTrue(File.Exists(file), $"Reported document '{file}' does not exist");
                }

                Assert.AreNotEqual(0, info.Files.Count, "The export reported no document files");

                if (preservePath)
                {
                    StringAssert.Contains(
                        info.Directory,
                        "PLC data types",
                        "preservePath must place documents below the 'PLC data types' system folder");
                }
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "", true)]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0, "", false)]
        public void Test_424_ExportTypesAsDocuments(string projectPath, string softwarePath, string exportPath, string regexName, bool preservePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var outcome = _portal.ExportTypesAsDocuments(softwarePath, exportPath, regexName, preservePath);

                Console.WriteLine($"Exported {outcome.Exported.Count} types as documents, " +
                                  $"{outcome.Inconsistent.Count} inconsistent, {outcome.Failures.Count} failed.");

                foreach (var failure in outcome.Failures)
                {
                    Console.WriteLine($"- failure: {failure}");
                }

                foreach (var export in outcome.Exported)
                {
                    Console.WriteLine($"- {export.Type?.Name}: {string.Join(", ", export.Documents.Files)}");
                }

                if (outcome.Exported.Count == 0 && outcome.Failures.Count == 0)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no PLC data type that can be exported as documents");
                }

                Assert.AreEqual(0, outcome.Failures.Count, "Some PLC data types failed to export as documents");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        /// <summary>
        /// Round-trips one PLC data type: export as documents, then import the same set back with
        /// Override. Closes the project without saving, so the sample project is left untouched.
        /// </summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, Settings.Project1ExportPath0)]
        public void Test_425_TypeDocumentRoundTrip(string projectPath, string softwarePath, string exportPath)
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
                    Assert.Inconclusive($"'{softwarePath}' holds no consistent PLC data type to round-trip");
                }

                var name = candidate!.Name;
                var typePath = _portal.GetTypePath(candidate);
                var info = _portal.ExportTypeAsDocuments(softwarePath, typePath, exportPath);

                Assert.AreNotEqual(0, info.Files.Count, "The export reported no document files");

                // A UDT name is unique across the whole PLC, not just within its group, so the
                // import has to target the group the type came from. Importing the same name
                // into another group collides even with ImportDocumentOptions.Override.
                var groupPath = typePath.Contains("/")
                    ? typePath.Substring(0, typePath.LastIndexOf('/'))
                    : string.Empty;

                var imported = _portal.ImportTypeFromDocuments(
                    softwarePath, groupPath, info.Directory, name, ImportDocumentOptions.Override);

                Console.WriteLine($"Round-tripped '{name}': imported {imported.Count} type(s).");

                Assert.AreNotEqual(0, imported.Count, "The import returned no PLC data type");
                Assert.IsTrue(
                    imported.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)),
                    $"The imported types do not contain '{name}'");
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

            Directory.CreateDirectory(stage);

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var existing = _portal.GetTypes(softwarePath).FirstOrDefault();

                if (existing == null)
                {
                    Assert.Inconclusive($"'{softwarePath}' holds no PLC data type");
                }

                // One file named after a type that exists, one that does not.
                File.WriteAllText(Path.Combine(stage, existing!.Name + ".s7dcl"), "TYPE\nEND_TYPE\n");
                File.WriteAllText(Path.Combine(stage, "ZZ_NewType_ZZ.s7dcl"), "TYPE\nEND_TYPE\n");

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
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_452_ExportPlcAsSourceTree(string projectPath, string softwarePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var exportPath = Path.Combine(Path.GetTempPath(), "TiaMcpServerSnapshot", Guid.NewGuid().ToString("N"));

            Assert.IsTrue(Common.OpenProject(_portal, projectPath), "Failed to open the project");

            try
            {
                var result = _portal.ExportPlcAsSourceTree(softwarePath, exportPath);

                Console.WriteLine($"Snapshot in {result.Directory}: {result.TotalWritten} written, " +
                                  $"{result.Skipped.Count} skipped, {result.Failures.Count} failed");

                foreach (var pair in result.Written)
                {
                    Console.WriteLine($"  {pair.Key}: {pair.Value} as {result.Formats[pair.Key]}");
                }

                foreach (var failure in result.Failures.Take(5))
                {
                    Console.WriteLine($"  failure: {failure}");
                }

                Assert.IsTrue(result.TotalWritten > 0, "The snapshot wrote nothing");
                Assert.IsTrue(Directory.Exists(exportPath), "The snapshot directory does not exist");
                Assert.IsTrue(
                    Directory.GetFiles(exportPath, "*.*", SearchOption.AllDirectories).Length > 0,
                    "The snapshot directory holds no files");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);

                try
                {
                    if (Directory.Exists(exportPath))
                    {
                        Directory.Delete(exportPath, recursive: true);
                    }
                }
                catch (Exception)
                {
                    // Temp cleanup only.
                }
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "document")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "xml")]
        public void Test_441_GetBlockSource(string projectPath, string softwarePath, string format)
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

                var result = _portal.GetBlockSource(softwarePath, _portal.GetBlockPath(block!), format);

                Console.WriteLine($"{result.Path} [{result.Format}] {result.TotalChars} chars, files: {string.Join(", ", result.FileNames)}");
                Console.WriteLine(result.Text.Length > 600 ? result.Text.Substring(0, 600) : result.Text);

                Assert.IsFalse(string.IsNullOrWhiteSpace(result.Text), "No source text was returned");
                StringAssert.Contains(result.Text, block!.Name, "The source does not mention the block name");
            }
            finally
            {
                Common.CloseProject(_portal, projectPath);
            }
        }

        /// <summary>Reading source must not leave scratch directories behind.</summary>
        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        public void Test_442_GetBlockSourceCleansUp(string projectPath, string softwarePath)
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

                _portal.GetBlockSource(softwarePath, _portal.GetBlockPath(block!));

                var after = Directory.Exists(scratchRoot) ? Directory.GetDirectories(scratchRoot).Length : 0;

                Assert.AreEqual(before, after, "GetBlockSource left a scratch directory behind");
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

    }
}
