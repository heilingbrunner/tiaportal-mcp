using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseExportFormats : ResponseMessage
    {
        /// <summary>
        /// Every setting of the TIA Portal settings tree. Only filled when the call asked for it
        /// ('listAll'); it is how the names of the export format settings are found.
        /// </summary>
        public List<ResponseSettingItem>? Settings { get; set; }
    }

    public class ResponseSettingItem
    {
        public string Path { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Value { get; set; }
    }
}
