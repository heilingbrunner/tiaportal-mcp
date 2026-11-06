using System.Collections.Generic;

namespace TiaMcpServer
{
    public class CliOptions
    {
        public int? TiaMajorVersion { get; set; }
        public int? Logging { get; set; } // "stdio" or "http"
        public bool Doctor { get; set; } // print environment diagnostics and exit
        public bool AllowWrite { get; set; } // register the project-mutating tools
        public string? ProjectPath { get; set; } // preset for the 'path' tool argument (project file)
        public string? SoftwarePath { get; set; } // preset for the 'softwarePath' tool argument
        public string? ExportPath { get; set; } // preset for the 'exportPath' tool argument
        public bool KeepStructure { get; set; } // preset for the 'keepStructure' tool argument (only when true)
        public bool UsedDeprecatedPreservePath { get; set; } // '--preserve-path', the old name of '--keep-structure', was given
        public bool WithDependencies { get; set; } // preset for the 'withDependencies' tool argument (only when true)
        public bool UsedDeprecatedForcePresets { get; set; } // '--force-presets' was given; it does nothing, every preset is binding

        /// <summary>The options this process was started with; set by Program.Main so the Doctor tool can report them.</summary>
        public static CliOptions Current { get; set; } = new CliOptions();

        public static CliOptions ParseArgs(string[] args)
        {
            var options = new CliOptions();
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "-tia-major-version":
                    case "--tia-major-version":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int v))
                        {
                            options.TiaMajorVersion = v;
                            i++;
                        }
                        break;

                    case "-doctor":
                    case "--doctor":
                        options.Doctor = true;
                        break;

                    case "-allow-write":
                    case "--allow-write":
                        options.AllowWrite = true;
                        break;

                    case "-project-path":
                    case "--project-path":
                        if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                        {
                            options.ProjectPath = args[++i];
                        }
                        break;

                    case "-software-path":
                    case "--software-path":
                        if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                        {
                            options.SoftwarePath = args[++i];
                        }
                        break;

                    case "-export-path":
                    case "--export-path":
                        if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                        {
                            options.ExportPath = args[++i];
                        }
                        break;

                    case "-keep-structure":
                    case "--keep-structure":
                        options.KeepStructure |= ReadFlag(args, ref i);
                        break;

                    // Deprecated name of '--keep-structure', still accepted so existing configurations keep working.
                    case "-preserve-path":
                    case "--preserve-path":
                        options.KeepStructure |= ReadFlag(args, ref i);
                        options.UsedDeprecatedPreservePath = true;
                        break;

                    case "-with-dependencies":
                    case "--with-dependencies":
                        options.WithDependencies = ReadFlag(args, ref i);
                        break;

                    // Deprecated and without effect: every preset is binding now. Still accepted (with its
                    // optional true/false) so existing configurations keep working.
                    case "-force-presets":
                    case "--force-presets":
                        ReadFlag(args, ref i);
                        options.UsedDeprecatedForcePresets = true;
                        break;

                    case "-logging":
                    case "--logging":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int l))
                        {
                            options.Logging = l;
                            i++;
                        }
                        break;
                }
            }
            return options;
        }

        /// <summary>
        /// Every command line option with its effective value, for the diagnostics. Options that
        /// were not given are listed as "(not set)" so the report shows the full set.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> Describe()
        {
            const string notSet = "(not set)";

            return new List<KeyValuePair<string, string>>
            {
                new("--tia-major-version", TiaMajorVersion?.ToString() ?? notSet),
                new("--project-path", ProjectPath ?? notSet),
                new("--software-path", SoftwarePath ?? notSet),
                new("--export-path", ExportPath ?? notSet),
                new("--logging", Logging?.ToString() ?? notSet),
                new("--doctor", Doctor ? "true" : "false"),
                new("--allow-write", AllowWrite ? "true" : "false"),
                new("--keep-structure", KeepStructure ? "true" : "false"),
                new("--preserve-path (deprecated, use --keep-structure)", UsedDeprecatedPreservePath ? "used" : "not used"),
                new("--with-dependencies", WithDependencies ? "true" : "false"),
                new("--force-presets (deprecated, presets are always binding)", UsedDeprecatedForcePresets ? "used" : "not used")
            };
        }

        // A flag like '--allow-write': its presence turns it on. A following 'true'/'false' is still
        // honoured, so an older '--preserve-path false' (or '--keep-structure false') is not read as "on".
        private static bool ReadFlag(string[] args, ref int i)
        {
            if (i + 1 < args.Length && bool.TryParse(args[i + 1], out bool value))
            {
                i++;

                return value;
            }

            return true;
        }
    }
}
