﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GRPBackendWV
{
    public class RMCPacketResponseStatisticsService_Method1 : RMCPacketReply
    {
        public class DesignerStatistics
        {
            public uint m_ID;
            public uint m_AggregationType;
            public uint m_Flags;
            public uint m_DefaultValue;
            public uint m_OasisNameId;
            public uint m_OasisDescriptionId;
            public string m_Expression;
            public string m_Name;
            public void toBuffer(Stream s)
            {
                Helper.WriteU32(s, m_ID);
                Helper.WriteU32(s, m_AggregationType);
                Helper.WriteU32(s, m_Flags);
                Helper.WriteU32(s, m_DefaultValue);
                Helper.WriteU32(s, m_OasisNameId);
                Helper.WriteU32(s, m_OasisDescriptionId);
                Helper.WriteString(s, m_Expression);
                Helper.WriteString(s, m_Name);
            }
        }

        public List<DesignerStatistics> list = new List<DesignerStatistics>();

        public override byte[] ToBuffer()
        {
            MemoryStream m = new MemoryStream();
            Helper.WriteU32(m, (uint)list.Count);
            foreach (DesignerStatistics ds in list)
                ds.toBuffer(m);
            return m.ToArray();
        }

        public override string ToString()
        {
            return "[RMCPacketResponseStatisticsService_Method1]";
        }
    }

}
