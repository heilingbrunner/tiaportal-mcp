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
        public bool? PreservePath { get; set; } // preset for the 'preservePath' tool argument
        public bool? WithDependencies { get; set; } // preset for the 'withDependencies' tool argument

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

                    case "-preserve-path":
                    case "--preserve-path":
                        options.PreservePath = ReadFlag(args, ref i);
                        break;

                    case "-with-dependencies":
                    case "--with-dependencies":
                        options.WithDependencies = ReadFlag(args, ref i);
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
                new("--logging", Logging?.ToString() ?? notSet),
                new("--allow-write", AllowWrite ? "true" : "false"),
                new("--doctor", Doctor ? "true" : "false"),
                new("--project-path", ProjectPath ?? notSet),
                new("--software-path", SoftwarePath ?? notSet),
                new("--export-path", ExportPath ?? notSet),
                // The tools default both arguments to false, so an unset flag is effectively false.
                new("--preserve-path", (PreservePath ?? false) ? "true" : "false"),
                new("--with-dependencies", (WithDependencies ?? false) ? "true" : "false")
            };
        }

        // A flag like '--allow-write': its presence turns it on. A following 'true'/'false' is still
        // honoured, so an older '--preserve-path false' is not read as "on".
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
