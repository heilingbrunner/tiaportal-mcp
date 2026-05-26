namespace TiaMcpServer
{
    public class CliOptions
    {
        public int? TiaMajorVersion { get; set; }
        public int? Logging { get; set; } // 1=stderr, 2=Debug output, 3=Windows Event Log

        public string Transport { get; set; } = "stdio"; // "stdio" or "http"
        public string HttpPrefix { get; set; } = "http://127.0.0.1:8765/";
        public string? HttpApiKey { get; set; }

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

                    case "-logging":
                    case "--logging":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int l))
                        {
                            options.Logging = l;
                            i++;
                        }
                        break;

                    case "-transport":
                    case "--transport":
                        if (i + 1 < args.Length)
                        {
                            options.Transport = args[i + 1].ToLowerInvariant();
                            i++;
                        }
                        break;

                    case "-http-prefix":
                    case "--http-prefix":
                        if (i + 1 < args.Length)
                        {
                            options.HttpPrefix = args[i + 1];
                            i++;
                        }
                        break;

                    case "-http-api-key":
                    case "--http-api-key":
                        if (i + 1 < args.Length)
                        {
                            options.HttpApiKey = args[i + 1];
                            i++;
                        }
                        break;
                }
            }
            return options;
        }
    }
}
