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

        // Registry sub keys below 'TIAP{version}' that hold the installation path, most specific first.
        private static readonly string[] PreferredInstallPathSubKeys = { "TIA_Opns", "Global", "EditionMain" };

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

            var assemblyPath = FindAssembly(tiaInstallPath!, TiaMajorVersion, assemblyName.Name + ".dll");

            return assemblyPath != null ? Assembly.LoadFrom(assemblyPath) : null;
        }

        /// <summary>
        /// Locates a Siemens.Engineering.* assembly below a TIA Portal installation, skipping the
        /// PublicAPI folders of all other major versions.
        /// </summary>
        public static string? FindAssembly(string installPath, int majorVersion, string fileName)
        {
            var searchDirectories = new[]
            {
                Path.Combine(installPath, "PublicAPI", $"V{majorVersion}"),
                Path.Combine(installPath, "Bin", "PublicAPI")
            };

            // IEnumerable without given majorVersionString
            var excludedTiaMajorVersions = new[] { "V13", "V14", "V15", "V16", "V17", "V18", "V19", "V20", "V21" }
                                    .Where(v => v != $"V{majorVersion}");

            foreach (var dir in searchDirectories)
            {
                var assemblyPath = FindAssemblyRecursive(dir, fileName, excludedTiaMajorVersions);
                if (assemblyPath != null)
                {
                    return assemblyPath;
                }
            }

            return null;
        }

        private static string? GetTiaPortalInstallPath()
        {
            var registryPath = GetTiaPortalInstallPath(TiaMajorVersion);
            if (!string.IsNullOrEmpty(registryPath))
            {
                return registryPath;
            }

            // Same variable the Siemens Openness resolver package uses.
            var envPath = Environment.GetEnvironmentVariable("TiaPortalLocation");
            return Directory.Exists(envPath) ? envPath : null;
        }

        /// <summary>
        /// Reads the installation path of a specific TIA Portal major version from the registry.
        /// Returns <c>null</c> when that version is not installed.
        /// </summary>
        public static string? GetTiaPortalInstallPath(int majorVersion)
        {
            var subKeyName = $@"SOFTWARE\Siemens\Automation\_InstalledSW\TIAP{majorVersion}";

            using (var regBaseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (var tiapKey = regBaseKey.OpenSubKey(subKeyName))
            {
                if (tiapKey == null)
                {
                    return null;
                }

                // Not every installation writes 'TIA_Opns'. Fall back to the other product keys of
                // the same version - they all carry the same installation path.
                var candidates = PreferredInstallPathSubKeys
                    .Concat(tiapKey.GetSubKeyNames())
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var candidate in candidates)
                {
                    using (var productKey = tiapKey.OpenSubKey(candidate))
                    {
                        var registryPath = productKey?.GetValue("Path")?.ToString();
                        if (string.IsNullOrEmpty(registryPath))
                        {
                            continue;
                        }

                        var installPath = registryPath!.TrimEnd(Path.DirectorySeparatorChar);

                        // A partially uninstalled product can leave a stale 'Path' behind. Only accept
                        // a candidate that actually looks like an installation root, so that callers
                        // can still fall back to the 'TiaPortalLocation' environment variable.
                        if (Directory.Exists(Path.Combine(installPath, "Bin")))
                        {
                            return installPath;
                        }
                    }
                }

                return null;
            }
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
