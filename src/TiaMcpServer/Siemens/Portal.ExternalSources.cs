using Siemens.Engineering.SW.ExternalSources;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// PLC external source files (read side).
    ///
    /// Callers: the GetExternalSources / GetExternalSourceInfo tools in
    /// McpServer.ExternalSources.cs. Affected API: none existing - all members are new.
    /// Reads/writes no data files: the source files themselves live inside the project.
    ///
    /// PlcExternalSource exposes only Name and Parent as typed properties, so anything else a
    /// caller needs (file path, timestamps) has to come from the generic attribute bag via
    /// Helper.GetAttributeList. No attribute name is hard-coded here for that reason.
    /// </summary>
    public partial class Portal
    {
        public PlcExternalSourceSystemGroup? GetExternalSourceRootGroup(string softwarePath)
        {
            return Operation.Run(_logger, nameof(GetExternalSourceRootGroup), PortalErrorCode.NotFound,
                () => GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup,
                ("softwarePath", softwarePath));
        }

        public PlcExternalSourceGroup? GetExternalSourceGroupByPath(string softwarePath, string groupPath)
        {
            return Operation.Run(_logger, nameof(GetExternalSourceGroupByPath), PortalErrorCode.NotFound,
                () =>
                {
                    var root = GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup;

                    return root == null
                        ? null
                        : WalkGroups<PlcExternalSourceGroup>(root, groupPath, g => g.Groups, g => g.Name);
                },
                ("softwarePath", softwarePath), ("groupPath", groupPath));
        }

        /// <summary>Root-relative path of an external source, e.g. "SourceGroup1/Source_1".</summary>
        public string GetExternalSourcePath(PlcExternalSource source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            if (source.Parent is PlcExternalSourceGroup group)
            {
                var groupPath = BuildGroupPath<PlcExternalSourceGroup>(
                    group,
                    g => g.Parent as PlcExternalSourceGroup,
                    g => g.Name,
                    g => g is PlcExternalSourceSystemGroup,
                    includeSystemRoot: false);

                return string.IsNullOrEmpty(groupPath) ? source.Name : $"{groupPath}/{source.Name}";
            }

            return source.Name;
        }

        public List<PlcExternalSource> GetExternalSources(string softwarePath, string regexName = "")
        {
            return Operation.Run(_logger, nameof(GetExternalSources), PortalErrorCode.NotFound,
                () =>
                {
                    var list = new List<PlcExternalSource>();
                    var root = GetPlcSoftwareOrThrow(softwarePath).ExternalSourceGroup;

                    if (root != null)
                    {
                        WalkRecursive<PlcExternalSourceGroup, PlcExternalSource>(
                            root, list, g => g.ExternalSources, g => g.Groups, s => s.Name, regexName);
                    }

                    return list;
                },
                ("softwarePath", softwarePath), ("regexName", regexName));
        }

        public PlcExternalSource? GetExternalSource(string softwarePath, string sourcePath)
        {
            return Operation.Run(_logger, nameof(GetExternalSource), PortalErrorCode.NotFound,
                () =>
                {
                    var (groupPath, sourceName) = SplitPath(sourcePath);

                    return GetExternalSourceGroupByPath(softwarePath, groupPath)?.ExternalSources.Find(sourceName);
                },
                ("softwarePath", softwarePath), ("sourcePath", sourcePath));
        }
    }
}
