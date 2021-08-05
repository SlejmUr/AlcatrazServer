﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace QuazalWV
{
	public partial class QPacketHandlerPRUDP
	{
		public QPacketHandlerPRUDP(UdpClient udp, uint pid, ushort port, string sourceName = "PRUDP Handler")
		{
			UDP = udp;
			SourceName = sourceName;
			PID = pid;
			Port = port;
			NATPingTimeToIgnore = new List<ulong>();
			AccumulatedPackets = new List<QPacket>();
			CachedResponses = new List<QReliableResponse>();
		}

		private readonly UdpClient UDP;
		public string SourceName;
		public readonly uint PID;
		public readonly ushort Port;
		private List<QPacket> AccumulatedPackets = new List<QPacket>();
		private List<QReliableResponse> CachedResponses = new List<QReliableResponse>();
		private readonly List<ulong> NATPingTimeToIgnore;

		private QPacket ProcessSYN(QPacket p, IPEndPoint from, out ClientInfo client)
		{
			client = Global.GetClientByEndPoint(from);
			if (client == null)
			{
				Log.WriteLine(2, "[QUAZAL] Creating new client data...");
				client = new ClientInfo();
				client.endpoint = from;
				client.IDrecv = Global.idCounter++;
				client.PID = Global.pidCounter++;
				Global.clients.Add(client);
			}

			p.m_uiConnectionSignature = client.IDrecv;
			client.seqCounterOut = 0;

			return MakeACK(p, client);
		}

		private QPacket ProcessCONNECT(ClientInfo client, QPacket p)
		{
			client.IDsend = p.m_uiConnectionSignature;

			var reply = MakeACK(p, client);

			if (p.payload != null && p.payload.Length > 0)
				reply.payload = MakeConnectPayload(client, p);

			return reply;
		}

		private byte[] MakeConnectPayload(ClientInfo client, QPacket p)
		{
			MemoryStream m = new MemoryStream(p.payload);
			uint size = Helper.ReadU32(m);
			byte[] buff = new byte[size];
			m.Read(buff, 0, (int)size);
			size = Helper.ReadU32(m) - 16;
			buff = new byte[size];
			m.Read(buff, 0, (int)size);
			buff = Helper.Decrypt(client.sessionKey, buff);
			m = new MemoryStream(buff);
			Helper.ReadU32(m);
			Helper.ReadU32(m);
			uint responseCode = Helper.ReadU32(m);
			Log.WriteLine(2, "[QAZAL] Got response code 0x" + responseCode.ToString("X8"));
			m = new MemoryStream();
			Helper.WriteU32(m, 4);
			Helper.WriteU32(m, responseCode + 1);
			return m.ToArray();
		}

		private QPacket ProcessDISCONNECT(ClientInfo client, QPacket p)
		{
			QPacket reply = new QPacket();
			reply.m_oSourceVPort = p.m_oDestinationVPort;
			reply.m_oDestinationVPort = p.m_oSourceVPort;
			reply.flags = new List<QPacket.PACKETFLAG>() { QPacket.PACKETFLAG.FLAG_ACK };
			reply.type = QPacket.PACKETTYPE.DISCONNECT;
			reply.m_bySessionID = p.m_bySessionID;
			reply.m_uiSignature = client.IDsend - 0x10000;
			reply.uiSeqId = p.uiSeqId;
			reply.payload = new byte[0];
			return reply;
		}

		private QPacket ProcessPING(ClientInfo client, QPacket p)
		{
			QPacket reply = new QPacket();
			reply.m_oSourceVPort = p.m_oDestinationVPort;
			reply.m_oDestinationVPort = p.m_oSourceVPort;
			reply.flags = new List<QPacket.PACKETFLAG>() { QPacket.PACKETFLAG.FLAG_ACK };
			reply.type = QPacket.PACKETTYPE.PING;
			reply.m_bySessionID = p.m_bySessionID;
			reply.m_uiSignature = client.IDsend;
			reply.uiSeqId = p.uiSeqId;
			reply.m_uiConnectionSignature = client.IDrecv;
			reply.payload = new byte[0];
			return reply;
		}

		public void Send(QPacket reqPacket, QPacket sendPacket, IPEndPoint ep)
		{
			byte[] data = sendPacket.toBuffer();
			StringBuilder sb = new StringBuilder();

			CacheResponse(reqPacket, new QPacket(data));

			foreach (byte b in data)
				sb.Append(b.ToString("X2") + " ");

			Log.WriteLine(5, "[" + SourceName + "] send : " + sendPacket.ToStringShort());
			Log.WriteLine(10, "[" + SourceName + "] send : " + sb.ToString());
			Log.WriteLine(10, "[" + SourceName + "] send : " + sendPacket.ToStringDetailed());

			UDP.Send(data, data.Length, ep);

			Log.LogPacket(true, data);
		}

		public QPacket MakeACK(QPacket p, ClientInfo client)
		{
			QPacket np = new QPacket(p.toBuffer());
			np.flags = new List<QPacket.PACKETFLAG>() { QPacket.PACKETFLAG.FLAG_ACK, QPacket.PACKETFLAG.FLAG_HAS_SIZE };

			np.m_oSourceVPort = p.m_oDestinationVPort;
			np.m_oDestinationVPort = p.m_oSourceVPort;
			np.m_uiSignature = client.IDsend;
			np.payload = new byte[0];
			np.payloadSize = 0;
			return np;
		}

		public void SendACK(QPacket p, ClientInfo client)
		{
			var np = MakeACK(p, client);
			var data = np.toBuffer();

			UDP.Send(data, data.Length, client.endpoint);
		}

		public void MakeAndSend(ClientInfo client, QPacket reqPacket, QPacket newPacket, byte[] data)
		{
			var stream = new MemoryStream(data);

			int numFragments = 0;

			// Houston, we have a problem...
			// BUG: Can't send lengthy messages through PRUDP, game simply doesn't accept them :(

			if (stream.Length > Global.packetFragmentSize)
				newPacket.flags.AddRange(new[] { QPacket.PACKETFLAG.FLAG_HAS_SIZE });

			// var fragmentBytes = new MemoryStream();

			newPacket.uiSeqId = client.seqCounterOut;
			newPacket.m_byPartNumber = 1;
			while (stream.Position < stream.Length)
			{
				int payloadSize = (int)(stream.Length - stream.Position);

				if (payloadSize <= Global.packetFragmentSize)
				{
					newPacket.m_byPartNumber = 0;  // indicate last packet
				}
				else
					payloadSize = Global.packetFragmentSize;

				byte[] buff = new byte[payloadSize];
				stream.Read(buff, 0, payloadSize);

				newPacket.uiSeqId++;
				newPacket.payload = buff;
				newPacket.payloadSize = (ushort)newPacket.payload.Length;

				// send a fragment
				/*{
                    var packetBuf = np.toBuffer();

                    // print debug stuff
                    var sb = new StringBuilder();
                    foreach (byte b in packetBuf)
                        sb.Append(b.ToString("X2") + " ");

                    WriteLog(5, "send : " + np.ToStringShort());
                    WriteLog(10, "send : " + sb.ToString());
                    WriteLog(10, "send : " + np.ToStringDetailed());

                    Log.LogPacket(true, packetBuf);

                    fragmentBytes.Write(packetBuf, 0, packetBuf.Length);

                    if (numFragments % 2 == 1)
                    {
                        client.udp.Send(fragmentBytes.GetBuffer(), (int)fragmentBytes.Length, client.ep);
                        fragmentBytes = new MemoryStream();
                    }
                }*/

				Send(reqPacket, newPacket, client.endpoint);

				newPacket.m_byPartNumber++;
				numFragments++;
			}

			client.seqCounterOut = newPacket.uiSeqId;

			// send last packets
			//if(fragmentBytes.Length > 0)
			//    client.udp.Send(fragmentBytes.GetBuffer(), (int)fragmentBytes.Length, client.ep);

			
			Log.WriteLine(10, $"[{ SourceName }] sent { numFragments } packets");
		}

		public void RetrySend(QReliableResponse cache, ClientInfo client)
		{
			Log.WriteLine(5, "Re-sending reliable packets...");

			foreach (var crp in cache.ResponseList.Where(x => x.GotAck == false))
			{
				var data = crp.Packet.toBuffer();
				UDP.Send(data, data.Length, client.endpoint);
			}
		}

		//-------------------------------------------------------------------------------------------

		public void ProcessPacket(byte[] data, IPEndPoint from, bool removeConnectPayload = false)
		{
			var sb = new StringBuilder();

			foreach (byte b in data)
				sb.Append(b.ToString("X2") + " ");

			while (true)
			{
				var p = new QPacket(data);

				{
					var m = new MemoryStream(data);

					byte[] buff = new byte[(int)p.realSize];
					m.Read(buff, 0, buff.Length);

					Log.LogPacket(false, buff);
					Log.WriteLine(5, "[" + SourceName + "] received : " + p.ToStringShort());
					Log.WriteLine(10, "[" + SourceName + "] received : " + sb.ToString());
					Log.WriteLine(10, "[" + SourceName + "] received : " + p.ToStringDetailed());
				}

				QPacket reply = null;
				ClientInfo client = null;

				if (p.type != QPacket.PACKETTYPE.SYN && p.type != QPacket.PACKETTYPE.NATPING)
					client = Global.GetClientByIDrecv(p.m_uiSignature);

				switch (p.type)
				{
					case QPacket.PACKETTYPE.SYN:
						reply = ProcessSYN(p, from, out client);
						break;
					case QPacket.PACKETTYPE.CONNECT:
						if (client != null && !p.flags.Contains(QPacket.PACKETFLAG.FLAG_ACK))
						{
							client.sPID = PID;
							client.sPort = Port;

							if (removeConnectPayload)
							{
								p.payload = new byte[0];
								p.payloadSize = 0;
							}

							reply = ProcessCONNECT(client, p);
						}
						break;
					case QPacket.PACKETTYPE.DATA:
						{
							if (Defrag(client, p) == false)
								break;

							// ack for reliable packets
							if (p.flags.Contains(QPacket.PACKETFLAG.FLAG_ACK))
							{
								OnGotAck(p);
								break;
							}

							// resend?
							var cache = GetCachedResponseByRequestPacket(p);
							if (cache != null)
							{
								SendACK(p, client);
								RetrySend(cache, client);
								break;
							}

							if (p.m_oSourceVPort.type == QPacket.STREAMTYPE.RVSecure)
								RMC.HandlePacket(this, p);

							if (p.m_oSourceVPort.type == QPacket.STREAMTYPE.DO)
								DO.HandlePacket(this, p);
						}
						break;
					case QPacket.PACKETTYPE.DISCONNECT:
						if (client != null)
							reply = ProcessDISCONNECT(client, p);
						break;
					case QPacket.PACKETTYPE.PING:
						if (client != null)
							reply = ProcessPING(client, p);
						break;
					case QPacket.PACKETTYPE.NATPING:

						ulong time = BitConverter.ToUInt64(p.payload, 5);

						if (NATPingTimeToIgnore.Contains(time))
						{
							NATPingTimeToIgnore.Remove(time);
						}
						else
						{
							reply = p;
							var m = new MemoryStream();
							byte b = (byte)(reply.payload[0] == 1 ? 0 : 1);

							m.WriteByte(b);

							Helper.WriteU32(m, 0x1234); //RVCID
							Helper.WriteU64(m, time);

							reply.payload = m.ToArray();

							Send(p, reply, from);

							m = new MemoryStream();
							b = (byte)(b == 1 ? 0 : 1);

							m.WriteByte(b);
							Helper.WriteU32(m, 0x1234); //RVCID

							time = Helper.MakeTimestamp();

							NATPingTimeToIgnore.Add(time);

							Helper.WriteU64(m, Helper.MakeTimestamp());
							reply.payload = m.ToArray();
						}
						break;
				}

				if (reply != null)
					Send(p, reply, from);

				// more packets in data stream?
				if (p.realSize != data.Length)
				{
					var m = new MemoryStream(data);

					int left = (int)(data.Length - p.realSize);
					byte[] newData = new byte[left];

					m.Seek(p.realSize, 0);
					m.Read(newData, 0, left);

					data = newData;
				}
				else
					break;
			}
		}
	}
}
