using Microsoft.Extensions.Logging;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using System;
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
                Openness.Initialize();
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
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "0_OBs", Settings.Project1ExportPath0 + "\\0_OBs\\Main_1.xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "1_Tests", Settings.Project1ExportPath0 + "\\1_Tests\\FC_Block_1.xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "1_Tests", Settings.Project1ExportPath0 + "\\1_Tests\\DB_Block_1.xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister", Settings.Project1ExportPath0 + "\\Common\\CarrierRegister\\GLOBAL_POSITIONING.xml")]
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
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister", Settings.Project1ExportPath0 + "\\Common\\CarrierRegister\\ML_SubstratState.xml")]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister", Settings.Project1ExportPath0 + "\\Common\\CarrierRegister\\ML_CarrierRegisterShort.xml")]
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
    }
}
