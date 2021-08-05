﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuazalWV
{
	public class ClientInfo
	{
		public uint PID;
		public uint sPID;
		public ushort sPort;

		public uint IDrecv;
		public uint IDsend;

		public IPEndPoint endpoint;

		public byte sessionID;
		public byte[] sessionKey;

		public ushort seqCounter;
		public ushort seqCounterOut;
		public ushort seqCounterDO;

		public ushort callCounterDO_RMC;

		public uint callCounterRMC;
		public uint stationID;
		public string accountId;
		public string name;
		public string pass;


		public bool bootStrapDone = false;
		public bool matchStartSent = false;
		public bool playerCreateStuffSent1 = false;
		public bool playerCreateStuffSent2 = false;
		public byte netRulesState = 3;
		public byte playerAbstractState = 2;
		public Payload_PlayerParameter settings = new Payload_PlayerParameter(new byte[0x40]);
		public int newsMsgId = -1;
		public List<GR5_NewsMessage> systemNews = new List<GR5_NewsMessage>();
		public List<GR5_NewsMessage> personaNews = new List<GR5_NewsMessage>();
		public List<GR5_NewsMessage> friendNews = new List<GR5_NewsMessage>();

		public void ClearSystemNews()
		{
			systemNews = new List<GR5_NewsMessage>();
		}

		public void ClearPersonaNews()
		{
			personaNews = new List<GR5_NewsMessage>();
		}

		public void ClearFriendNews()
		{
			friendNews = new List<GR5_NewsMessage>();
		}
	}
}
