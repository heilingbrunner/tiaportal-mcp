using Siemens.Engineering;
using Siemens.Engineering.SW.Blocks;
using System;
using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class Helper
    {
        public static List<Attribute> GetAttributeList(IEngineeringObject obj)
        {
            var attributes = new List<Attribute>();
            if (obj == null)
            {
                return attributes;
            }

            IEnumerable<EngineeringAttributeInfo>? infos = null;
            try
            {
                infos = obj.GetAttributeInfos();
            }
            catch
            {
                // Whole attribute-info enumeration unavailable -- return empty rather than poisoning the response.
                return attributes;
            }

            foreach (var attr in infos)
            {
                // Per-attribute guard: a single unreadable attribute must not abort the rest,
                // and the captured value must be normalized to a JSON-serializable shape so
                // System.Text.Json doesn't blow up at the SDK boundary and produce -32603.
                try
                {
                    object? raw = obj.GetAttribute(attr.Name);
                    attributes.Add(new Attribute
                    {
                        Name = attr.Name,
                        Value = NormalizeAttributeValue(raw),
                        AccessMode = Enum.GetName(typeof(EngineeringAttributeAccessMode), attr.AccessMode)
                    });
                }
                catch
                {
                    // Skip this attribute; we already have the others.
                }
            }

            return attributes;
        }

        private static object? NormalizeAttributeValue(object? value)
        {
            if (value == null)
            {
                return null;
            }

            // Types System.Text.Json handles natively and we trust to be safe.
            if (value is string) return value;
            if (value is bool) return value;
            if (value is sbyte || value is byte || value is short || value is ushort) return value;
            if (value is int || value is uint || value is long || value is ulong) return value;
            if (value is float || value is double || value is decimal) return value;
            if (value is DateTime || value is DateTimeOffset || value is Guid) return value;
            if (value is TimeSpan ts) return ts.ToString();

            // Enums: render as their name so the wire payload is self-describing.
            if (value is Enum) return value.ToString();

            // MultilingualText (and similar Openness wrappers) ToString() the TYPE name
            // rather than the content; pull the first localized text instead.
            if (value is MultilingualText mt) return MultilingualTextToString(mt);

            // Fallback: anything else (Siemens COM-wrapped Openness objects) collapses to its
            // ToString() representation so the response stays JSON-serializable.
            try { return value.ToString(); }
            catch { return "<unreadable>"; }
        }

        public static string? MultilingualTextToString(MultilingualText? text)
        {
            if (text == null)
            {
                return null;
            }

            foreach (var item in text.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.Text))
                {
                    return item.Text;
                }
            }

            return null;
        }

        public static BlockGroupInfo BuildBlockHierarchy(PlcBlockGroup group)
        {
            var groupInfo = new BlockGroupInfo
            {
                Name = group.Name
            };

            var blockList = new List<ResponseBlockInfo>();
            foreach (var block in group.Blocks)
            {
                var attributes = Helper.GetAttributeList(block);
                blockList.Add(new ResponseBlockInfo
                {
                    Name = block.Name,
                    TypeName = block.GetType().Name,
                    Namespace = block.Namespace,
                    ProgrammingLanguage = Enum.GetName(typeof(ProgrammingLanguage), block.ProgrammingLanguage),
                    MemoryLayout = Enum.GetName(typeof(MemoryLayout), block.MemoryLayout),
                    IsConsistent = block.IsConsistent,
                    HeaderName = block.HeaderName,
                    ModifiedDate = block.ModifiedDate,
                    IsKnowHowProtected = block.IsKnowHowProtected,
                    Attributes = attributes,
                    Description = block.ToString()
                });
            }
            groupInfo.Blocks = blockList;

            var groupList = new List<BlockGroupInfo>();
            foreach (var subGroup in group.Groups)
            {
                groupList.Add(BuildBlockHierarchy(subGroup));
            }
            groupInfo.Groups = groupList;

            return groupInfo;
        }
    }
}
