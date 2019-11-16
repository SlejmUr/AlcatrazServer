﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GRPBackendWV
{
    public class RMCPacketResponsePveArchetypeService_Method1 : RMCPacketReply
    {
        public class PveArchetype
        {
            public uint m_ArchetypeID;
            public uint m_WeaponID;
            public float m_InitialHealth;
            public float m_InitialArmor;
            public uint m_BankNumber;
            public uint m_IndexInBank;
            public float m_WpnDmgMultiplier;
            public float m_WpnDispersionMultiplier;
            public float m_WpnBloomMultiplier;
            public byte m_WpnBurstCount;
            public float m_WpnBurstDelay;
            public void toBuffer(Stream s)
            {
                Helper.WriteU32(s, m_ArchetypeID);
                Helper.WriteU32(s, m_WeaponID);
                Helper.WriteFloat(s, m_InitialHealth);
                Helper.WriteFloat(s, m_InitialArmor);
                Helper.WriteU32(s, m_BankNumber);
                Helper.WriteU32(s, m_IndexInBank);
                Helper.WriteFloat(s, m_WpnDmgMultiplier);
                Helper.WriteFloat(s, m_WpnDispersionMultiplier);
                Helper.WriteFloat(s, m_WpnBloomMultiplier);
                Helper.WriteU8(s, m_WpnBurstCount);
                Helper.WriteFloat(s, m_WpnBurstDelay);
            }
        }

        public List<PveArchetype> types = new List<PveArchetype>();
        public override byte[] ToBuffer()
        {
            MemoryStream m = new MemoryStream();
            Helper.WriteU32(m, (uint)types.Count);
            foreach (PveArchetype t in types)
                t.toBuffer(m);
            return m.ToArray();
        }

        public override string ToString()
        {
            return "[RMCPacketResponsePveArchetypeService_Method1]";
        }
    }
}
