using Microsoft.CodeCoverage.Core;
using Microsoft.Extensions.Logging;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test1Portal
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
        }

        [TestCleanup]
        public void ClassCleanup()
        {
            // ...
        }

        [TestMethod]
        public void Test_101_ConnectPortal()
        {
            if (_portal == null)
            {
                Assert.Fail("TIA-Portal instance is not initialized");
            }

            var result = _portal.ConnectPortal();

            Assert.IsTrue(result, "Failed to connect to TIA-Portal");
        }

        [TestMethod]
        public void Test_102_DisconnectPortal()
        {
            if (_portal == null)
            {
                Assert.Fail("TIA-Portal instance is not initialized");
            }

            var result = _portal.DisconnectPortal();

            Assert.IsTrue(result, "Failed to disconnect from TIA-Portal");
        }

        [TestMethod]
        public void Test_103_IsConnected()
        {
            if (_portal == null)
            {
                Assert.Fail("TIA-Portal instance is not initialized");
            }

            var result = _portal.ConnectPortal();
            result &= _portal.IsConnected();
            result &= _portal.DisconnectPortal();
            result &= !_portal.IsConnected();

            Assert.IsTrue(result, "Failed to connect to TIA-Portal");
        }

        
    }
}
