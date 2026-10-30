using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseExportFormats : ResponseMessage
    {
        /// <summary>The settings file the formats were read from.</summary>
        public string File { get; set; } = string.Empty;

        /// <summary>True when the file did not exist and was created with the defaults of TIA Portal.</summary>
        public bool Created { get; set; }

        /// <summary>Asks the user to check the file; set only when it was just created.</summary>
        public string? Hint { get; set; }

        /// <summary>One entry per object type of the settings file.</summary>
        public List<ResponseExportFormat> Formats { get; set; } = new List<ResponseExportFormat>();
    }

    public class ResponseExportFormat
    {
        /// <summary>The object type as the settings file names it, e.g. 'code blocks-LAD'.</summary>
        public string ObjectType { get; set; } = string.Empty;

        /// <summary>The format text of the settings file, e.g. 'SIMATIC SD(.s7dcl, .s7res)'.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>SimaticSd, Xml, Source or Unknown.</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>The export tool that writes this format for the object type; null when there is none.</summary>
        public string? RecommendedTool { get; set; }
    }
}
