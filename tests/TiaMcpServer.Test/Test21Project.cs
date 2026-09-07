using Microsoft.Extensions.Logging;
using System;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    [TestClass]
    [DoNotParallelize]
    public sealed class Test21Project
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

            Assert.IsTrue(result, "TiaPortal instance is not initialized");
        }

        [TestCleanup]
        public void ClassCleanup()
        {
            if (_portal != null)
            {
                _portal.CloseProject();
            }
        }

        [TestMethod]
        public void Test_211_GetOpenProjects()
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var projects = _portal.GetProjects();

            projects?.ForEach(project =>
            {
                Console.WriteLine($"Project: Name = '{project.Name}', Author = '{project.Author}'");
                TiaMcpServer.ModelContextProtocol.Helper.GetAttributeList(project).ForEach(attribute =>
                {
                    Console.WriteLine($"- {attribute.Name}: {attribute.Value}");
                });
            });

            Assert.IsNotNull(projects, "Failed to retrieve open projects");

            // Assert.IsTrue(projects?.Count > 0, "No open projects found");
        }

        

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath)]
        public void Test_212_OpenProject(string path)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var result = _portal.OpenProject(path);

            Console.WriteLine($"OpenProject: {path}, result={result}");

            Assert.IsTrue(result, "Failed to open project");
        }

        [TestMethod]
        public void Test_213_GetOpenProjects()
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var projects = _portal.GetProjects();

            projects?.ForEach(project =>
            {
                Console.WriteLine($"Project: Name = '{project.Name}', Author = '{project.Author}'");
                TiaMcpServer.ModelContextProtocol.Helper.GetAttributeList(project).ForEach(attribute =>
                {
                    Console.WriteLine($"- {attribute.Name}: {attribute.Value}");
                });
            });

            Assert.IsNotNull(projects, "Failed to retrieve open projects");

            // Assert.IsTrue(projects?.Count > 0, "No open projects found");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath)]
        public void Test_214_CloseProject(string path)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var result = _portal.OpenProject(path);
            result &= _portal.CloseProject();

            Console.WriteLine($"CloseProject: {path}, result={result}");

            Assert.IsTrue(result, "Failed to close project");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath)]
        public void Test_215_SaveProject(string path)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var result = _portal.OpenProject(path);
            result &= _portal.SaveProject();

            Console.WriteLine($"SaveProject: {path}, result={result}");

            Assert.IsTrue(result, "Failed to save project");
        }

        [TestMethod]
        [DataRow(Settings.Project1ProjectPath, Settings.Project1PathNew)]
        public void Test_216_SaveAsProject(string path, string newPath)
        {
            if (_portal == null)
            {
                Assert.Fail("TiaPortal instance is not initialized");
            }

            var result = _portal.OpenProject(path);
            result &= _portal.SaveAsProject(newPath);

            Assert.IsTrue(result, "Failed to save project as new project");
        }
    }
}
