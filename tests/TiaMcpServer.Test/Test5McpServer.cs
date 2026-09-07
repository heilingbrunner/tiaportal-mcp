using System;
using System.Linq;
using TiaMcpServer.ModelContextProtocol;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public class Test5McpServer
    {
        private readonly bool _isInitialized = false;

        [TestInitialize]
        public void ClassInit()
        {
            if (!_isInitialized)
            {
                Openness.Initialize();
            }

            var response = McpServer.Connect();
        }

        [TestCleanup]
        public void ClassCleanup()
        {
            McpServer.Disconnect();
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath)]
        // [DataRow(Settings.Session1ProjectPath)]
        public void Test_500_McpServer_OpenCloseProject(string projectPath)
        {

            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.CloseProject();
            WriteMessage("McpServer.CloseProject()", response);

            Assert.IsTrue(success, "McpServer failed");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath)]
        // [DataRow(Settings.Session1ProjectPath)]
        public void Test_501_McpServer_GetProjectTree(string projectPath)
        {

            McpServer.Connect();
            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetProjectTree();
            WriteMessage("McpServer.GetProjectTree()", response);

            Console.WriteLine("Structure: " + response?.Tree);

            McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath)]
        // [DataRow(Settings.Session1ProjectPath)]
        public void Test_502_McpServer_GetState(string projectPath)
        {

            McpServer.Connect();
            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetState();
            WriteMessage("McpServer.GetState()", response);

            Console.WriteLine("GetState: Project = '" + response?.Project + "'");
            Console.WriteLine("GetState: Session = '" + response?.Session + "'");
            Console.WriteLine("GetState: IsConnected = '" + response?.IsConnected + "'");

            McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }

        [TestMethod]
        //[DataRow(Settings.Project1ProjectPath)]
        //// [DataRow(Settings.Session1ProjectPath)]
        public void Test_503_McpServer_GetProjects()
        {

            McpServer.Connect();
            //McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetProjects();

            if (response == null)
            {
                Console.WriteLine("McpServer.GetProjects(): response is null");
            }
            else
            {
                WriteMessage($"McpServer.GetProjects()", response);
                
                if (response.Items != null)
                {
                    response.Items.ToList().ForEach(item =>
                    {
                        Console.WriteLine($"Project {item.Name}");
                        WriteAttributes(item);
                    });
                }
                else
                {
                    Console.WriteLine("McpServer.GetProjects(): no attributes found");
                }
            }

            //McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath)]
        // [DataRow(Settings.Session1ProjectPath)]
        public void Test_504_McpServer_GetDevices(string projectPath)
        {

            McpServer.Connect();
            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetDevices();
            WriteMessage("McpServer.GetDevices()", response);

            Console.WriteLine("GetDevices:");
            response?.Items?.ToList().ForEach(item => Console.WriteLine($"- '{item}'"));

            McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0)]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath)]
        public void Test_505_McpServer_GetSoftwareInfo(string projectPath, string softwarePath)
        {

            McpServer.Connect();
            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetSoftwareInfo(softwarePath);
            WriteMessage("McpServer.GetSoftwareInfo()", response);

            McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, "HMI_0")]
        //[DataRow(Settings.Session1ProjectPath, "PC-System_1")]
        public void Test_506_McpServer_GetDeviceInfo(string projectPath, string devicePath)
        {

            McpServer.Connect();
            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetDeviceInfo(devicePath);
            WriteMessage("McpServer.GetDeviceInfo()", response);

            McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, "PC-System_0/PLC_0")]
        //[DataRow(Settings.Session1ProjectPath, "PC-System_1/Software PLC_1")]
        public void Test_507_McpServer_GetDeviceItemInfo(string projectPath, string deviceItemPath)
        {

            McpServer.Connect();
            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetDeviceItemInfo(deviceItemPath);
            WriteMessage("McpServer.GetDeviceItemInfo()", response);

            McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }


        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "0_OBs/Main_1")]
        //[DataRow(Settings.Session1ProjectPath, Settings.Session1PlcSoftwarePath, "0_OBs/Main_1")]
        public void Test_508_McpServer_GetBlockInfo(string projectPath, string softwarePath, string blockPath)
        {

            McpServer.Connect();
            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetBlockInfo(softwarePath, blockPath);
            WriteMessage("McpServer.GetBlockInfo()", response);

            McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }


        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PlcSoftwarePath0, "Common/CarrierRegister/ML_SubstratState")]
        public void Test_509_McpServer_GetTypeInfo(string projectPath, string softwarePath, string blockPath)
        {

            McpServer.Connect();
            McpServer.OpenProject(projectPath);

            var success = true;
            var response = McpServer.GetTypeInfo(softwarePath, blockPath);
            WriteMessage("McpServer.GetTypeInfo()", response);

            McpServer.CloseProject();

            Assert.IsTrue(success, "McpServer failed");
        }

        private static void WriteMessage(string method, ResponseMessage? rm)
        {
            if (rm == null)
            {
                Console.WriteLine($"{method}: content is null");
            }
            else
            {

                Console.WriteLine($"{method}: {rm.Message}");
            }
        }

        private static void WriteAttributes(ResponseAttributes? ra)
        {
            if (ra != null && ra.Attributes != null)
            {
                foreach (var attribute in ra.Attributes)
                {
                    Console.WriteLine($"- {attribute.Name}: {attribute.Value}");
                }
            }
            else
            {
                Console.WriteLine($"no attributes found");
            }
        }


    }
}
