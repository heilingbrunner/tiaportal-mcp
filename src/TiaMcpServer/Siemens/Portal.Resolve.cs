using Siemens.Engineering;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Generic path resolution and traversal shared by the PLC software areas.
    ///
    /// Callers: the private resolvers in Portal.cs (GetPlcBlockGroupByPath, GetPlcTypeGroupByPath,
    /// GetPlcBlockGroupPath, GetPlcTypeGroupPath, GetBlocksRecursive, GetTypesRecursive) and the
    /// Portal partials added for tags, watch tables, external sources and cross references.
    /// Affected API: none - every member here is private to Portal. Reads/writes no data files.
    ///
    /// PlcSoftware exposes five look-alike group hierarchies (BlockGroup, TypeGroup,
    /// TagTableGroup, WatchAndForceTableGroup, ExternalSourceGroup) that share no common base
    /// type, so the shape is captured with generics plus selector delegates rather than
    /// inheritance.
    /// </summary>
    public partial class Portal
    {
        /// <summary>
        /// Resolves a software path to its PlcSoftware, throwing rather than returning null so
        /// callers wrapped in Operation.Run get a decorated PortalException.
        /// </summary>
        private PlcSoftware GetPlcSoftwareOrThrow(string softwarePath)
        {
            if (IsProjectNull())
            {
                throw new PortalException(PortalErrorCode.InvalidState, "No project is open in TIA Portal");
            }

            var container = GetSoftwareContainer(softwarePath);

            if (container?.Software is not PlcSoftware plcSoftware)
            {
                throw new PortalException(
                    PortalErrorCode.NotFound,
                    $"No PLC software found at '{softwarePath}'. Use 'GetProjectTree' to discover valid software paths.");
            }

            return plcSoftware;
        }

        /// <summary>
        /// Walks a '/'-separated group path down from <paramref name="root"/>.
        /// An empty path returns the root itself. Returns null when a segment does not match.
        /// </summary>
        private static T? WalkGroups<T>(
            T root,
            string groupPath,
            Func<T, IEnumerable<T>> children,
            Func<T, string> name)
            where T : class
        {
            T? current = root;

            foreach (var segment in groupPath.Split(['/'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (current == null)
                {
                    return null;
                }

                current = children(current)
                    .FirstOrDefault(g => name(g).Equals(segment, StringComparison.OrdinalIgnoreCase));
            }

            return current;
        }

        /// <summary>
        /// Builds the '/'-separated path of a group by walking Parent upwards.
        /// </summary>
        /// <param name="includeSystemRoot">
        /// True keeps the system group name (e.g. "Program blocks") as the first segment, which
        /// is what the preservePath export layout expects and what Test_415_ImportBlock asserts.
        /// False yields a root-relative path that round-trips back into the ...ByPath resolvers.
        /// </param>
        private static string BuildGroupPath<T>(
            T group,
            Func<T, T?> parent,
            Func<T, string> name,
            Func<T, bool> isSystemRoot,
            bool includeSystemRoot)
            where T : class
        {
            if (group == null)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            T? current = group;

            while (current != null)
            {
                if (isSystemRoot(current))
                {
                    if (includeSystemRoot)
                    {
                        segments.Insert(0, name(current));
                    }

                    break;
                }

                segments.Insert(0, name(current));

                try
                {
                    current = parent(current);
                }
                catch (Exception)
                {
                    // The parent chain leaves the group hierarchy (or Openness refuses the
                    // access); the path collected so far is the best answer available.
                    break;
                }
            }

            return string.Join("/", segments);
        }

        /// <summary>
        /// Depth-first collection of every item in a group tree, optionally filtered by a regex
        /// on the item name. An invalid regex skips items rather than throwing, matching the
        /// behaviour of the resolvers this replaces.
        /// </summary>
        private static void WalkRecursive<TGroup, TItem>(
            TGroup group,
            List<TItem> sink,
            Func<TGroup, IEnumerable<TItem>> items,
            Func<TGroup, IEnumerable<TGroup>> groups,
            Func<TItem, string> name,
            string regexName = "")
            where TGroup : class
        {
            foreach (var item in items(group))
            {
                if (item == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(regexName))
                {
                    try
                    {
                        if (!Regex.IsMatch(name(item), regexName, RegexOptions.IgnoreCase))
                        {
                            continue;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Invalid regex pattern - skip this item.
                        continue;
                    }
                }

                sink.Add(item);
            }

            foreach (var subgroup in groups(group))
            {
                WalkRecursive(subgroup, sink, items, groups, name, regexName);
            }
        }

        /// <summary>
        /// First available translation of a MultilingualText, or null when there is none.
        /// </summary>
        private static string? Text(MultilingualText? text)
        {
            return text?.Items?.FirstOrDefault()?.Text;
        }

        /// <summary>
        /// Splits "Group/Sub/Leaf" into ("Group/Sub", "Leaf"). A path without '/' yields an
        /// empty group path, meaning the system root.
        /// </summary>
        private static (string GroupPath, string LeafName) SplitPath(string path)
        {
            var trimmed = (path ?? string.Empty).Trim('/');
            var index = trimmed.LastIndexOf('/');

            return index < 0
                ? (string.Empty, trimmed)
                : (trimmed.Substring(0, index), trimmed.Substring(index + 1));
        }

        #region selectors for the block and type hierarchies

        private static IEnumerable<PlcBlockGroup> BlockSubgroups(PlcBlockGroup group) => group.Groups;

        private static IEnumerable<PlcTypeGroup> TypeSubgroups(PlcTypeGroup group) => group.Groups;

        #endregion
    }
}
