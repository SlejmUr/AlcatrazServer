﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QNetZ
{
    public static class DO_FetchRequestMessage
    {
        public static byte[] HandleMessage(QClient client, byte[] data)
        {
			ClientInfo ci = client.info;
			List<byte[]> msgs;
            QLog.WriteLine(2, "[DO] Handling DO_FetchRequestMessage...");
            MemoryStream m = new MemoryStream(data);
            m.Seek(3, 0);
            uint dupObj = Helper.ReadU32(m);
            switch (dupObj)
            {
                case 0x5C00001:
                    msgs = new List<byte[]>();
                    if (!ci.bootStrapDone)
                    {
                        foreach (DupObj obj in DO_Session.DupObjs)
                            msgs.Add(DO_CreateDuplicaMessage.Create(obj, 2));
						ci.bootStrapDone = true;
                    }
                    msgs.Add(DO_MigrationMessage.Create(client.seqCounterOut++, 
						new DupObj(DupObjClass.Station, 1), 
						new DupObj(DupObjClass.Station, ci.stationID),
						new DupObj(DupObjClass.Station, ci.stationID), 3, 
						new List<uint>() { new DupObj(DupObjClass.Station, ci.stationID) }));
                    return DO_BundleMessage.Create(ci, msgs);
                default:
                    QLog.WriteLine(1, "[DO] Handling DO_FetchRequest unknown dupObj 0x" + dupObj.ToString("X8") + "!");
                    return new byte[0];
            }
        }

        public static byte[] Create(ushort callID, DupObj obj)
        {
            QLog.WriteLine(2, "[DO] Creating DO_FetchRequestMessage");
            MemoryStream m = new MemoryStream();
            m.WriteByte(0xD);
            Helper.WriteU16(m, callID);
            Helper.WriteU32(m, obj);
            Helper.WriteU32(m, obj.Master);
            return m.ToArray();
        }
    }
}
