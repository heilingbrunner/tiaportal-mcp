namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>One file a dry run of OverwritePlcProgram / ImportPlcProgramChanges would import.</summary>
    public class ResponsePlannedProgramItem
    {
        /// <summary>The file, relative to the program folder.</summary>
        public string? File { get; set; }

        /// <summary>Object name, taken from the file name.</summary>
        public string? Name { get; set; }

        /// <summary>Root-relative path the object has or will get in the project.</summary>
        public string? Path { get; set; }

        /// <summary>'block' or 'type'.</summary>
        public string? Kind { get; set; }

        /// <summary>'source' for an external source file (.scl, .awl, .db, .udt), 'xml' for SimaticML (.xml).</summary>
        public string? Format { get; set; }

        /// <summary>'create' when the project has no such object, 'overwrite' when it has and the import would replace it, 'unknown' when the lookup failed.</summary>
        public string? Action { get; set; }
    }
}
