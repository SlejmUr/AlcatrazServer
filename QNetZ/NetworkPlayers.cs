﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

namespace QNetZ
{
	public static class NetworkPlayers
	{
		public static uint RVCIDCounter = 0xBB98E;

		public static readonly List<PlayerInfo> Players = new List<PlayerInfo>();

		public static PlayerInfo GetPlayerInfoByPID(uint pid)
		{
			foreach (PlayerInfo pl in Players)
			{
				if (pl.PID == pid)
					return pl;
			}
			return null;
		}

		public static PlayerInfo GetPlayerInfoByUsername(string userName)
		{
			foreach (PlayerInfo pl in Players)
			{
				if (pl.Name == userName)
					return pl;
			}
			return null;
		}

		public static PlayerInfo CreatePlayerInfo(QClient connection)
		{
			var plInfo = new PlayerInfo();

			plInfo.Client = connection;
			plInfo.PID = 0;
			plInfo.RVCID = RVCIDCounter++;

			Players.Add(plInfo);

			return plInfo;
		}

		public static void PurgeAllPlayers()
		{
			Players.Clear();
		}

		public static void DropPlayerInfo(PlayerInfo plInfo)
		{
			if(plInfo.Client != null)
			{
				plInfo.Client.Info = null;
			}

			plInfo.OnDropped();
			QLog.WriteLine(1, $"dropping player: {plInfo.Name}");
			
			Players.Remove(plInfo);
		}

		public static void DropPlayers()
		{
			Players.RemoveAll(plInfo => { 
				if(plInfo.Client.State == QClient.StateType.Dropped &&
					(DateTime.UtcNow - plInfo.Client.LastPacketTime).TotalSeconds > Constants.ClientTimeoutSeconds)
				{
					plInfo.OnDropped();
					QLog.WriteLine(1, $"auto-dropping player: {plInfo.Name}");

					return true;
				}
				return false;
			});

		}
	}
}
