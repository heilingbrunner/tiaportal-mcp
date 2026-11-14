using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseHmiFiles : ResponseMessage
    {
        /// <summary>Exports: the files written. Imports: the names of the imported objects.</summary>
        public IEnumerable<string>? Items { get; set; }
    }
}
