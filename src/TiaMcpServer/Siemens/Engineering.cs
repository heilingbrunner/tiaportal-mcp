using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TiaMcpServer.Siemens
{
    // Manual Siemens.Engineering.dll resolve
    public class Engineering
    {
        public static int TiaMajorVersion { get; set; }

        public static Assembly? Resolver(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);
            if (!assemblyName.Name.StartsWith("Siemens.Engineering"))
            {
                return null;
            }

            var tiaInstallPath = GetTiaPortalInstallPath();
            if (string.IsNullOrEmpty(tiaInstallPath))
            {
                return null;
            }

            var tiaMajorVersionString = TiaMajorVersion.ToString();
            var searchDirectories = new[]
            {
                Path.Combine(tiaInstallPath, "PublicAPI", $"V{tiaMajorVersionString}"),
                Path.Combine(tiaInstallPath, "Bin", "PublicAPI")
            };

            // IEnumerable without given majorVersionString
            var excludedTiaMajorVersions = new[] { "V13", "V14", "V15", "V16", "V17", "V18", "V19", "V20", "V21" }
                                    .Where(v => v != $"V{tiaMajorVersionString}");

            foreach (var dir in searchDirectories)
            {
                var assemblyPath = FindAssemblyRecursive(dir, assemblyName.Name + ".dll", excludedTiaMajorVersions);
                if (assemblyPath != null)
                {
                    return Assembly.LoadFrom(assemblyPath);
                }
            }

            return null;
        }

        private static string? GetTiaPortalInstallPath()
        {
            var subKeyName = $@"SOFTWARE\Siemens\Automation\_InstalledSW\TIAP{TiaMajorVersion}\TIA_Opns";

            using (var regBaseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var tiaOpnsKey = regBaseKey.OpenSubKey(subKeyName))
            {
                var registryPath = tiaOpnsKey?.GetValue("Path")?.ToString();
                if (!string.IsNullOrEmpty(registryPath))
                {
                    return registryPath;
                }
            }

            // Same variable the Siemens Openness resolver package uses.
            var envPath = Environment.GetEnvironmentVariable("TiaPortalLocation");
            return Directory.Exists(envPath) ? envPath : null;
        }

        private static string? FindAssemblyRecursive(string directory, string fileName, IEnumerable<string> excludedTiaMajorVersions)
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }

            var filePath = Path.Combine(directory, fileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var subDirName = new DirectoryInfo(subDir).Name;
                if (excludedTiaMajorVersions.Contains(subDirName))
                {
                    continue;
                }

                var result = FindAssemblyRecursive(subDir, fileName, excludedTiaMajorVersions);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
