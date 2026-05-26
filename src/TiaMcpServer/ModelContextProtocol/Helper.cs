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

            if (obj != null)
            {
                foreach (var attr in obj.GetAttributeInfos())
                {
                    object value = obj.GetAttribute(attr.Name);
                    attributes.Add(new Attribute
                    {
                        Name = attr.Name,
                        Value = value,
                        AccessMode = Enum.GetName(typeof(EngineeringAttributeAccessMode), attr.AccessMode)
                    });
                }
            }

            return attributes;
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
