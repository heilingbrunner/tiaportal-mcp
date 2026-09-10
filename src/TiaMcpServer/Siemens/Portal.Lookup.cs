using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Tags;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Name-to-path resolution across every PLC software area.
    ///
    /// Callers: the ResolveObjectPath tool in McpServer.Lookup.cs and the NotFound branch of
    /// ExportBlock. Affected API: none existing - every member here is new. Reads and writes no
    /// data files.
    ///
    /// Why this exists: every other tool takes a root-relative path ("Common/BtnTyp_X"), but a
    /// caller - human or model - normally knows only the bare name. Before this, the only way
    /// to turn one into the other was to list a whole area and post-process it, and the
    /// "did you mean" helper that did so was implemented twice inside ExportBlock, once as dead
    /// code. Resolution lives here once and covers all six areas.
    /// </summary>
    public partial class Portal
    {
        /// <summary>The object areas ResolveObjectPath can search, as accepted in 'kind'.</summary>
        private static readonly string[] LookupKinds =
            { "block", "type", "tag", "tagTable", "watchTable", "source" };

        /// <summary>
        /// Finds objects whose name matches <paramref name="name"/>, exactly first and by
        /// substring only if that finds nothing, so an exact hit is never buried under partial
        /// ones. An empty result is returned rather than thrown: "nothing matched" is an answer.
        /// </summary>
        /// <param name="kind">One of LookupKinds, or "any" (default) to search every area.</param>
        public List<ObjectMatch> ResolveObjectPath(string softwarePath, string name, string kind = "any")
        {
            return Operation.Run(_logger, nameof(ResolveObjectPath), PortalErrorCode.NotFound,
                () =>
                {
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            "'name' is empty. Pass the object name to look for, e.g. 'FC_Block_1'.");
                    }

                    if (!kind.Equals("any", StringComparison.OrdinalIgnoreCase)
                        && !LookupKinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Unknown kind '{kind}'. Allowed: any, {string.Join(", ", LookupKinds)}.");
                    }

                    // A caller may paste a full path back in; only the leaf can match a name.
                    var leaf = name.Contains("/") ? name.Substring(name.LastIndexOf('/') + 1) : name;

                    var exact = Collect(softwarePath, kind, leaf, exactOnly: true);

                    return exact.Count > 0 ? exact : Collect(softwarePath, kind, leaf, exactOnly: false);
                },
                ("softwarePath", softwarePath), ("name", name), ("kind", kind));
        }

        private List<ObjectMatch> Collect(string softwarePath, string kind, string name, bool exactOnly)
        {
            var wanted = new Func<string, bool>(candidate => exactOnly
                ? candidate.Equals(name, StringComparison.OrdinalIgnoreCase)
                : candidate.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0);

            var matches = new List<ObjectMatch>();
            var all = kind.Equals("any", StringComparison.OrdinalIgnoreCase);

            bool Wanted(string k) => all || kind.Equals(k, StringComparison.OrdinalIgnoreCase);

            if (Wanted("block"))
            {
                foreach (var block in GetBlocks(softwarePath).Where(b => wanted(b.Name)))
                {
                    matches.Add(new ObjectMatch { Kind = "block", Name = block.Name, Path = GetBlockPath(block) });
                }
            }

            if (Wanted("type"))
            {
                foreach (var type in GetTypes(softwarePath).Where(t => wanted(t.Name)))
                {
                    matches.Add(new ObjectMatch { Kind = "type", Name = type.Name, Path = GetTypePath(type) });
                }
            }

            if (Wanted("tagTable"))
            {
                foreach (var table in GetTagTables(softwarePath).Where(t => wanted(t.Name)))
                {
                    matches.Add(new ObjectMatch { Kind = "tagTable", Name = table.Name, Path = GetTagTablePath(table) });
                }
            }

            if (Wanted("tag"))
            {
                foreach (var tag in GetTags(softwarePath).Where(t => wanted(t.Name)))
                {
                    var table = tag.Parent as PlcTagTable;

                    matches.Add(new ObjectMatch
                    {
                        Kind = "tag",
                        Name = tag.Name,
                        Path = table == null ? tag.Name : $"{GetTagTablePath(table)}/{tag.Name}"
                    });
                }
            }

            if (Wanted("watchTable"))
            {
                foreach (var table in GetWatchTables(softwarePath).Where(t => wanted(t.Name)))
                {
                    matches.Add(new ObjectMatch { Kind = "watchTable", Name = table.Name, Path = GetWatchTablePath(table) });
                }
            }

            if (Wanted("source"))
            {
                foreach (var source in GetExternalSources(softwarePath).Where(s => wanted(s.Name)))
                {
                    matches.Add(new ObjectMatch { Kind = "source", Name = source.Name, Path = GetExternalSourcePath(source) });
                }
            }

            return matches
                .Where(m => !string.IsNullOrEmpty(m.Path))
                .OrderBy(m => m.Kind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Every PLC software path in the open project, in the exact form the other tools
        /// accept. Candidates are generated from the device tree and then verified by feeding
        /// them back through GetSoftwareContainer, so a path that is returned always resolves -
        /// the naming rules differ between hardware PLCs (device item only) and PC systems
        /// (device/device item) and are not worth duplicating here.
        /// </summary>
        public List<string> GetSoftwarePaths()
        {
            return Operation.Run(_logger, nameof(GetSoftwarePaths), PortalErrorCode.NotFound,
                () =>
                {
                    var paths = new List<string>();

                    if (_project == null)
                    {
                        return paths;
                    }

                    if (_project.Devices != null)
                    {
                        CollectSoftwarePaths(_project.Devices, string.Empty, paths);
                    }

                    if (_project.DeviceGroups != null)
                    {
                        CollectSoftwarePaths(_project.DeviceGroups, string.Empty, paths);
                    }

                    if (_project.UngroupedDevicesGroup?.Devices != null)
                    {
                        CollectSoftwarePaths(_project.UngroupedDevicesGroup.Devices, string.Empty, paths);
                    }

                    return paths;
                });
        }

        private void CollectSoftwarePaths(DeviceUserGroupComposition groups, string prefix, List<string> paths)
        {
            foreach (var group in groups)
            {
                var groupPrefix = $"{prefix}{group.Name}/";

                if (group.Devices != null)
                {
                    CollectSoftwarePaths(group.Devices, groupPrefix, paths);
                }

                if (group.Groups != null)
                {
                    CollectSoftwarePaths(group.Groups, groupPrefix, paths);
                }
            }
        }

        private void CollectSoftwarePaths(DeviceComposition devices, string prefix, List<string> paths)
        {
            foreach (var device in devices)
            {
                foreach (var item in device.DeviceItems)
                {
                    if (item.GetService<SoftwareContainer>()?.Software is not PlcSoftware)
                    {
                        continue;
                    }

                    // Qualified first: a PC system needs its station in front and the bare item
                    // name would be ambiguous between two stations. A hardware PLC station name
                    // is not addressable (it can even contain a slash), so that candidate simply
                    // fails to resolve and the bare device item name is used instead.
                    foreach (var candidate in new[] { $"{prefix}{device.Name}/{item.Name}", $"{prefix}{item.Name}" })
                    {
                        if (GetSoftwareContainer(candidate) != null && !paths.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                        {
                            paths.Add(candidate);

                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>One object found by ResolveObjectPath.</summary>
    public class ObjectMatch
    {
        /// <summary>block, type, tag, tagTable, watchTable or source.</summary>
        public string Kind { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Root-relative path, ready to pass to the tools of that area.</summary>
        public string Path { get; set; } = string.Empty;
    }
}
