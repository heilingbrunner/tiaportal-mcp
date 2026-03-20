using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseMessage
    {
        public string? Message { get; set; }
        public JsonObject? Meta { get; set; }
    }

    public class ResponseAttributes : ResponseMessage
    {
        public IEnumerable<Attribute>? Attributes { get; set; }
    }

    public class ResponseSoftwareInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseDeviceInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseDeviceItemInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseBlockInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? TypeName { get; set; }
        public string? Name { get; set; }
        public string? Namespace { get; set; }
        public string? ProgrammingLanguage { get; set; }
        public string? MemoryLayout { get; set; }
        public bool? IsConsistent { get; set; }
        public string? HeaderName { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsKnowHowProtected { get; set; }
        public string? Description { get; set; }
    }
    public class ResponseBlocksWithHierarchy : ResponseMessage
    {
        public BlockGroupInfo? Root { get; set; }
    }

    public class ResponseTypeInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? Name { get; set; }
        public string? TypeName { get; set; }
        public string? Namespace { get; set; }
        public bool? IsConsistent { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool? IsKnowHowProtected { get; set; }
        public string? Description { get; set; }
    }

    public class ResponseProjectInfo : ResponseAttributes
    {
        //public string? Path { get; set; }
        public string? Name { get; set; }
    }

    public class ResponseConnect : ResponseMessage
    {
    }

    public class ResponseDisconnect : ResponseMessage
    {
    }

    public class ResponseState : ResponseMessage
    {
        public bool? IsConnected { get; set; }
        public string? Project { get; set; }
        public string? Session { get; set; }
    }

    public class ResponseGetProjects : ResponseMessage
    {
        public IEnumerable<ResponseProjectInfo>? Items { get; set; }
    }

    public class ResponseOpenProject : ResponseMessage
    {
    }

    public class ResponseSaveProject : ResponseMessage
    {
    }

    public class ResponseSaveAsProject : ResponseMessage
    {
    }

    public class ResponseCloseProject : ResponseMessage
    {
    }

    public class ResponseTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseProjectTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseSoftwareTree : ResponseMessage
    {
        public string? Tree { get; set; }
    }

    public class ResponseDevices : ResponseMessage
    {
        public IEnumerable<ResponseDeviceInfo>? Items { get; set; }
    }
    
    public class ResponseCompileSoftware : ResponseMessage
    {
    }
    
    public class ResponseBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseExportBlock : ResponseMessage
    {
    }

    public class ResponseImportBlock : ResponseMessage
    {
    }

    public class ResponseExportBlocks : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
        public IEnumerable<ResponseBlockInfo>? Inconsistent { get; set; }
    }

    public class ResponseTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
    }

    public class ResponseExportType : ResponseMessage
    {
    }

    public class ResponseImportType : ResponseMessage
    {
    }

    public class ResponseExportTypes : ResponseMessage
    {
        public IEnumerable<ResponseTypeInfo>? Items { get; set; }
        public IEnumerable<ResponseTypeInfo>? Inconsistent { get; set; }
    }

    public class ResponseExportAsDocuments : ResponseMessage
    {
    }

    public class ResponseExportBlocksAsDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    public class ResponseImportFromDocuments : ResponseMessage
    {
    }

    public class ResponseImportBlocksFromDocuments : ResponseMessage
    {
        public IEnumerable<ResponseBlockInfo>? Items { get; set; }
    }

    #region HMI Responses

    public class ResponseHmiTagTableInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public int? TagCount { get; set; }
    }

    public class ResponseHmiTagTables : ResponseMessage
    {
        public IEnumerable<ResponseHmiTagTableInfo>? Items { get; set; }
    }

    public class ResponseHmiTagInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Connection { get; set; }
        public string? PlcTag { get; set; }
    }

    public class ResponseHmiTags : ResponseMessage
    {
        public IEnumerable<ResponseHmiTagInfo>? Items { get; set; }
    }

    public class ResponseExportHmiTagTable : ResponseMessage
    {
    }

    public class ResponseImportHmiTagTable : ResponseMessage
    {
    }

    public class ResponseScreenInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? ScreenType { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
    }

    public class ResponseScreens : ResponseMessage
    {
        public IEnumerable<ResponseScreenInfo>? Items { get; set; }
    }

    public class ResponseExportScreen : ResponseMessage
    {
    }

    public class ResponseImportScreen : ResponseMessage
    {
    }

    public class ResponseHmiConnectionInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? Partner { get; set; }
        public string? ConnectionType { get; set; }
    }

    public class ResponseHmiConnections : ResponseMessage
    {
        public IEnumerable<ResponseHmiConnectionInfo>? Items { get; set; }
    }

    public class ResponseCreateHmiConnection : ResponseMessage
    {
    }

    public class ResponseAlarmInfo : ResponseAttributes
    {
        public string? Name { get; set; }
        public string? AlarmClass { get; set; }
        public string? TriggerTag { get; set; }
        public string? AlarmText { get; set; }
    }

    public class ResponseDiscreteAlarms : ResponseMessage
    {
        public IEnumerable<ResponseAlarmInfo>? Items { get; set; }
    }

    public class ResponseAnalogAlarms : ResponseMessage
    {
        public IEnumerable<ResponseAlarmInfo>? Items { get; set; }
    }

    public class ResponseTextListInfo : ResponseAttributes
    {
        public string? Name { get; set; }
    }

    public class ResponseTextLists : ResponseMessage
    {
        public IEnumerable<ResponseTextListInfo>? Items { get; set; }
    }

    public class ResponseExportTextList : ResponseMessage
    {
    }

    public class ResponseImportTextList : ResponseMessage
    {
    }

    #endregion
}
