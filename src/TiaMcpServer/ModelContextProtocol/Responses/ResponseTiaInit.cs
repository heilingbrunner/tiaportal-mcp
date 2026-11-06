using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseTiaInit : ResponseMessage
    {
        /// <summary>The settings file that was created or completed.</summary>
        public string File { get; set; } = string.Empty;

        /// <summary>True when the file did not exist and was created.</summary>
        public bool Created { get; set; }

        /// <summary>False when the file already had everything and was not rewritten.</summary>
        public bool Changed { get; set; }

        /// <summary>The 'args' keys that were added.</summary>
        public List<string> AddedArgs { get; set; } = new List<string>();

        /// <summary>The 'args' keys that already had another value, which was kept.</summary>
        public List<string> KeptArgs { get; set; } = new List<string>();

        /// <summary>The deprecated 'args' keys that were renamed in place (value kept), e.g. 'preserve-path' to 'keep-structure'.</summary>
        public List<string> RenamedArgs { get; set; } = new List<string>();

        /// <summary>The export format keys that were added.</summary>
        public List<string> AddedFormats { get; set; } = new List<string>();

        /// <summary>Asks the user to check the export formats; set only when formats were added.</summary>
        public string? Hint { get; set; }
    }
}
