﻿using DSFServices.DDL.Models;
using QNetZ;
using QNetZ.DDL;
using System.Collections.Generic;
using System.Diagnostics;

namespace DSFServices
{
	public class NotificationQueueEntry
	{
		public NotificationQueueEntry(uint _timeout, QClient _client, NotificationEvent eventData)
		{
			client = _client;
			timeout = _timeout;
			data = eventData;

			timer = new Stopwatch();
			timer.Start();
		}

		public QClient client;
		public Stopwatch timer;
		public NotificationEvent data;
		public uint timeout;
	}

	public static class NotificationQueue
	{
		private static readonly object _sync = new object();
		private static List<NotificationQueueEntry> quene = new List<NotificationQueueEntry>();

		public static void AddNotification(NotificationEvent eventData, QClient client, uint timeout)
		{
			var qItem = new NotificationQueueEntry(timeout, client, eventData);

			lock (_sync)
			{
				quene.Add(qItem);
			}
		}

		public static void Update(QPacketHandlerPRUDP handler)
		{
			lock (_sync)
			{
				for (int i = 0; i < quene.Count; i++)
				{
					NotificationQueueEntry n = quene[i];
					if (n.timer.ElapsedMilliseconds > n.timeout)
					{
						SendNotification(handler, n.client, n.data);

						n.timer.Stop();
						quene.RemoveAt(i);
						i--;
					}
				}
			}
		}
		public static void SendNotification(QPacketHandlerPRUDP handler, QClient client, NotificationEvent eventData)
		{
			RMC.SendRMCCall(handler, client, RMCProtocolId.NotificationEventManager, 1, new RMCPRequestDDL<NotificationEvent>(eventData));
		}
	}
}
