using Microsoft.Extensions.Logging;
using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test3Devices
    {
        private bool _isInitialized = false;
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
            if (_portal != null)
            {
                _portal.CloseSession();
            }
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, "HMI_0")]
        [DataRow(Settings.Project1ProjectPath, "PC-System_0")]
        [DataRow(Settings.Project1ProjectPath, "Group1/PC-System_1")]
        [DataRow(Settings.Project1ProjectPath, "Group1/Group1.1/PC-System_1.1")]
        [DataRow(Settings.Project1ProjectPath, "Group1/Group1.1/Group1.1.1/PC-System_1.1.1")]
        public void Test_302_GetDevice(string projectPath, string devicePath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.GetDevice(devicePath);
            if (result != null)
            {
                Console.WriteLine($"Device: {result?.Name} found under {devicePath}");
            }
            else
            {
                Console.WriteLine($"Device not found under {devicePath}");
            }

            success &= result != null;

            success &= Common.CloseProject(_portal, projectPath);

            Console.WriteLine($"success={success}");

            Assert.IsTrue(success, "No Device found");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, "PLC_0")]
        [DataRow(Settings.Project1ProjectPath, "PC-System_0/Software PLC_0")]
        [DataRow(Settings.Project1ProjectPath, "HMI_0/HMI_RT_1")]
        [DataRow(Settings.Project1ProjectPath, "Group1/PLC_1")]
        [DataRow(Settings.Project1ProjectPath, "Group1/PC-System_1/Software PLC_1")]
        [DataRow(Settings.Project1ProjectPath, "Group1/Group1.1/PLC_1.1")]
        [DataRow(Settings.Project1ProjectPath, "Group1/Group1.1/PC-System_1.1/Software PLC_1.1")]
        public void Test_302_GetDeviceItem(string projectPath, string deviceItemPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var result = _portal.GetDeviceItem(deviceItemPath);
            if (result != null)
            {
                Console.WriteLine($"DeviceItem: {result?.Name} found under {deviceItemPath}");
            }
            else
            {
                Console.WriteLine($"DeviceItem not found under {deviceItemPath}");
            }

            success &= result != null;

            success &= Common.CloseProject(_portal, projectPath);

            Console.WriteLine($"success={success}");

            Assert.IsTrue(success, "No DeviceItem found");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath)]
        // [DataRow(Settings.Session1ProjectPath)]
        public void Test_303_GetDevices(string projectPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            bool success = Common.OpenProject(_portal, projectPath);

            var list = _portal.GetDevices();
            if (list != null)
            {
                Console.WriteLine($"Devices: {list.Count} found");

                list.ForEach(device =>
                {
                    Console.WriteLine($"Device: {device.Name}");
                });
            }
            else
            {
                Console.WriteLine($"Devices not found");
            }

            success &= list != null;

            success &= Common.CloseProject(_portal, projectPath);

            Console.WriteLine($"success={success}");

            Assert.IsTrue(success, "No Devices found");
        }
    }
}
