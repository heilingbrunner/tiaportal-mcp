namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One block or PLC data type imported by ImportPlcProgram.</summary>
    public class ResponseImportedProgramItem
    {
        public string? Name { get; set; }

        /// <summary>Root-relative path of the new object in the project.</summary>
        public string? Path { get; set; }

        /// <summary>'block' or 'type'.</summary>
        public string? Kind { get; set; }

        /// <summary>'source' for an external source file (.scl, .awl, .db, .udt), 'xml' for SimaticML (.xml).</summary>
        public string? Format { get; set; }
    }
}
