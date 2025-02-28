﻿using DSFServices.DDL.Models;
using QNetZ;
using QNetZ.Attributes;
using QNetZ.DDL;
using QNetZ.Interfaces;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace DSFServices.Services
{
	/// <summary>
	/// Game session 
	///		Implements the sessions responsible for the gameplay process
	/// </summary>
	[RMCService(RMCProtocolId.GameSessionService)]
	public class GameSessionService : RMCServiceBase
	{
		static uint GameSessionCounter = 22110;

		[RMCMethod(1)]
		public RMCResult CreateSession(GameSession gameSession)
		{
			var plInfo = Context.Client.Info;
			var newSession = new GameSessionData();
			GameSessions.SessionList.Add(newSession);

			newSession.Id = ++GameSessionCounter;
			newSession.HostPID = plInfo.PID;
			newSession.TypeID = gameSession.m_typeID;

			foreach (var attr in gameSession.m_attributes)
				newSession.Attributes[attr.ID] = attr.Value;

			GameSessions.UpdateSessionParticipation(plInfo, newSession.Id, newSession.TypeID, true);

			newSession.Attributes[(uint)GameSessionAttributeType.PublicSlots] = 0;
			newSession.Attributes[(uint)GameSessionAttributeType.PrivateSlots] = 8;
			newSession.Attributes[(uint)GameSessionAttributeType.FilledPublicSlots] = (uint)newSession.PublicParticipants.Count;
			newSession.Attributes[(uint)GameSessionAttributeType.FilledPrivateSlots] = (uint)newSession.Participants.Count;

			// TODO: give names to attributes
			newSession.Attributes[100] = 0;
			newSession.Attributes[101] = 0;
			newSession.Attributes[104] = 0;
			newSession.Attributes[113] = 0;

			// return key
			var result = new GameSessionKey();
			result.m_sessionID = newSession.Id;
			result.m_typeID = newSession.TypeID;

			return Result(result);
		}


		[RMCMethod(2)]
		public RMCResult UpdateSession(GameSessionUpdate gameSessionUpdate)
		{
			var session = GameSessions.SessionList
				.FirstOrDefault(x => x.Id == gameSessionUpdate.m_sessionKey.m_sessionID && 
									 x.TypeID == gameSessionUpdate.m_sessionKey.m_typeID);

			if(session != null)
			{
				// update or add attributes
				foreach (var attr in gameSessionUpdate.m_attributes)
				{
					session.Attributes[attr.ID] = attr.Value;
				}
			}
			else
			{
				QLog.WriteLine(1, $"Error : GameSessionService.UpdateSession - no session with id={gameSessionUpdate.m_sessionKey.m_sessionID}");
			}

			return Error(0);
		}


		[RMCMethod(3)]
		public RMCResult DeleteSession(GameSessionKey gameSessionKey)
		{
			UNIMPLEMENTED();
			return Error(0);
		}


		[RMCMethod(4)]
		public RMCResult MigrateSession(GameSessionKey gameSessionKey)
		{
			var oldSession = GameSessions.SessionList
				.FirstOrDefault(x => x.Id == gameSessionKey.m_sessionID &&
									 x.TypeID == gameSessionKey.m_typeID);

			var gameSessionKeyMigrated = new GameSessionKey();

			if (oldSession != null)
			{
				var plInfo = Context.Client.Info;
				var newSession = new GameSessionData();
				GameSessions.SessionList.Add(newSession);

				newSession.Id = ++GameSessionCounter;
				newSession.HostPID = plInfo.PID;
				newSession.TypeID = oldSession.TypeID;

				// ????
				// "notification": {
				// 	"m_pidSource": 539625,
				// 	"m_uiType": 7001,
				// 	"m_uiParam1": 31,
				// 	"m_uiParam2": 30,
				// 	"m_strParam": "",
				// 	"m_uiParam3": 1
				//   }
				
				// move all participants too
				foreach (var pid in oldSession.PublicParticipants)
				{
					var participantPlInfo = NetworkPlayers.GetPlayerInfoByPID(pid);

					if(participantPlInfo != null)
						participantPlInfo.GameData().CurrentSessionID = newSession.Id;
				}

				foreach (var pid in oldSession.Participants)
				{
					var participantPlInfo = NetworkPlayers.GetPlayerInfoByPID(pid);

					if (participantPlInfo != null)
						participantPlInfo.GameData().CurrentSessionID = newSession.Id;
				}

				newSession.Participants = oldSession.Participants;
				newSession.PublicParticipants = oldSession.PublicParticipants;

				foreach (var attr in oldSession.Attributes)
					newSession.Attributes[attr.Key] = attr.Value;

				gameSessionKeyMigrated.m_sessionID = newSession.Id;
				gameSessionKeyMigrated.m_typeID = newSession.TypeID;

				// drop old session
				QLog.WriteLine(1, $"MigrateSession - Auto-deleted session {oldSession.Id}");
				GameSessions.SessionList.Remove(oldSession);
			}
			else
			{
				QLog.WriteLine(1, $"Error : GameSessionService.MigrateSession - no session with id={gameSessionKey.m_sessionID}");
			}

			return Result(gameSessionKeyMigrated);
		}


		[RMCMethod(5)]
		public RMCResult LeaveSession(GameSessionKey gameSessionKey)
		{
			// Same as AbandonSession
			var plInfo = Context.Client.Info;
			var myPlayerId = plInfo.PID;
			var session = GameSessions.SessionList
				.FirstOrDefault(x => x.Id == gameSessionKey.m_sessionID && 
									 x.TypeID == gameSessionKey.m_typeID);

			if(session != null)
			{
				// send - could be invalid!!!
				//{
				//  "notification": {
				//	"m_pidSource": 25447,	// ???
				//	"m_uiType": 7004,		// GameSessionEvent
				//	"m_uiParam1": 539625,	// participantID
				//	"m_uiParam2": 27,		// gameSessionKey.m_sessionID
				//	"m_strParam": "",
				//	"m_uiParam3": 1			// gameSessionKey.m_typeID ??? not sure...
				//  }
				//}

				// send to all session members
				foreach (var pid in session.Participants)
				{
					var qclient = Context.Handler.GetQClientByClientPID(pid);

					if (qclient != null)
					{
						var leaveNotification = new NotificationEvent(NotificationEventsType.GameSessionEvent, 4)
						{
							m_pidSource = plInfo.PID,
							m_uiParam1 = plInfo.PID,
							m_uiParam2 = session.Id,
							m_strParam = "",
							m_uiParam3 = session.TypeID
						};

						NotificationQueue.SendNotification(Context.Handler, qclient, leaveNotification);
					}
				}

				GameSessions.UpdateSessionParticipation(plInfo, uint.MaxValue, uint.MaxValue, false);
			}
			else
			{
				QLog.WriteLine(1, $"Error : GameSessionService.LeaveSession - no session with id={gameSessionKey.m_sessionID}");
			}

			return Error(0);
		}


		[RMCMethod(6)]
		public RMCResult GetSession(GameSessionKey gameSessionKey)
		{
			var searchResult = new GameSessionSearchResult();

			var session = GameSessions.SessionList.FirstOrDefault(x => x.Id == gameSessionKey.m_sessionID && x.TypeID == gameSessionKey.m_typeID);

			if (session != null)
			{
				searchResult = new GameSessionSearchResult()
				{
					m_hostPID = session.HostPID,
					m_hostURLs = session.HostURLs,
					m_attributes = session.Attributes.Select(x => new GameSessionProperty { ID = x.Key, Value = x.Value}).ToArray(),
					m_sessionKey = new GameSessionKey()
					{
						m_sessionID = session.Id,
						m_typeID = session.TypeID
					}
				};
			}

			return Result(searchResult);
		}


		[RMCMethod(7)]
		public RMCResult SearchSessions(uint m_typeID, uint m_queryID, IEnumerable<GameSessionProperty> m_parameters)
		{
			var sessions = GameSessions.SessionList.Where(x => x.TypeID == m_typeID).ToArray();

			var resultList = new List<GameSessionSearchResult>();

			// BUG BUG: this works incorrectly
			// reproduction:
			//		first player searches for Racing, second searches for Team or Takedown
			// result:
			//		they will find each other:
			// expected:
			//		the will not find each other

			foreach (var ses in sessions)
			{
				// if any parameters match the attributes, add a search result
				if (m_parameters.Any(p => ses.Attributes.Any(sa => p.ID == sa.Key && p.Value == sa.Value)))
				{
					resultList.Add(new GameSessionSearchResult()
					{
						m_hostPID = ses.HostPID,
						m_hostURLs = ses.HostURLs,
						m_attributes = ses.Attributes.Select(x => new GameSessionProperty { ID = x.Key, Value = x.Value }).ToArray(),
						m_sessionKey = new GameSessionKey()
						{
							m_sessionID = ses.Id,
							m_typeID = ses.TypeID
						},
					});
				}
			}

			return Result(resultList);
		}


		[RMCMethod(8)]
		public RMCResult AddParticipants(GameSessionKey gameSessionKey, IEnumerable<uint> publicParticipantIDs, IEnumerable<uint> privateParticipantIDs)
		{
			var session = GameSessions.SessionList
				.FirstOrDefault(x => x.Id == gameSessionKey.m_sessionID && 
									 x.TypeID == gameSessionKey.m_typeID);

			if(session != null)
			{
				foreach (var pid in publicParticipantIDs)
				{
					session.PublicParticipants.Add(pid);

					var player = NetworkPlayers.GetPlayerInfoByPID(pid);
					if (player != null)
					{
						GameSessions.UpdateSessionParticipation(player, session.Id, session.TypeID, false);
					}
				}

				foreach (var pid in privateParticipantIDs)
				{
					session.Participants.Add(pid);

					var player = NetworkPlayers.GetPlayerInfoByPID(pid);
					if (player != null)
					{
						GameSessions.UpdateSessionParticipation(player, session.Id, session.TypeID, true);
					}
				}

				session.Attributes[(uint)GameSessionAttributeType.FilledPublicSlots] = (uint)session.PublicParticipants.Count;
				session.Attributes[(uint)GameSessionAttributeType.FilledPrivateSlots] = (uint)session.Participants.Count;
			}
			else
			{
				QLog.WriteLine(1, $"Error : GameSessionService.AddParticipants - no session with id={gameSessionKey.m_sessionID}");
			}

			return Error(0);
		}


		[RMCMethod(9)]
		public RMCResult RemoveParticipants(GameSessionKey gameSessionKey, IEnumerable<uint> participantIDs)
		{
			var session = GameSessions.SessionList
				.FirstOrDefault(x => x.Id == gameSessionKey.m_sessionID &&
									 x.TypeID == gameSessionKey.m_typeID);

			if (session != null)
			{
				// TODO: send
				//{
				//  "notification": {
				//	"m_pidSource": 25447,	// ???
				//	"m_uiType": 7004,		// GameSessionEvent
				//	"m_uiParam1": 539625,	// participantID
				//	"m_uiParam2": 27,		// gameSessionKey.m_sessionID
				//	"m_strParam": "",
				//	"m_uiParam3": 1			// gameSessionKey.m_typeID
				//  }
				//}

				foreach (var pid in participantIDs)
				{
					var player = NetworkPlayers.GetPlayerInfoByPID(pid);
					if (player != null)
					{
						GameSessions.UpdateSessionParticipation(player, uint.MaxValue, uint.MaxValue, false);
					}
					else
					{
						if (GameSessions.RemovePlayerFromSession(session, pid))
						{
							QLog.WriteLine(1, $"RemoveParticipants - Auto-deleted session {session.Id}");
							GameSessions.SessionList.Remove(session);
						}
					}
				}

				foreach (var pid in participantIDs)
				{
					var player = NetworkPlayers.GetPlayerInfoByPID(pid);
					if (player != null)
					{
						GameSessions.UpdateSessionParticipation(player, uint.MaxValue, uint.MaxValue, true);
					}
					else
					{
						if (GameSessions.RemovePlayerFromSession(session, pid))
						{
							QLog.WriteLine(1, $"RemoveParticipants - Auto-deleted session {session.Id}");
							GameSessions.SessionList.Remove(session);
						}
					}
				}

				session.Attributes[(uint)GameSessionAttributeType.FilledPublicSlots] = (uint)session.PublicParticipants.Count;
				session.Attributes[(uint)GameSessionAttributeType.FilledPrivateSlots] = (uint)session.Participants.Count;
			}
			else
			{
				QLog.WriteLine(1, $"Error : GameSessionService.RemoveParticipants - no session with id={gameSessionKey.m_sessionID}");
			}

			return Error(0);
		}


		[RMCMethod(10)]
		public RMCResult GetParticipantCount(GameSessionKey gameSessionKey, IEnumerable<uint> participantIDs)
		{
			UNIMPLEMENTED();
			return Error(0);
		}


		[RMCMethod(11)]
		public void GetParticipants()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(12)]
		public void SendInvitation()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(13)]
		public void GetInvitationReceivedCount()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(14)]
		public void GetInvitationsReceived()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(15)]
		public void GetInvitationSentCount()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(16)]
		public void GetInvitationsSent()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(17)]
		public void AcceptInvitation()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(18)]
		public void DeclineInvitation()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(19)]
		public void CancelInvitation()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(20)]
		public void SendTextMessage()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(21)]
		public RMCResult RegisterURLs(IEnumerable<StationURL> stationURLs)
		{
			var plInfo = Context.Client.Info;
			var myPlayerId = plInfo.PID;
			var session = GameSessions.SessionList.FirstOrDefault(x => x.HostPID == myPlayerId);

			if (session != null)
			{
				session.HostURLs.Clear();
				session.HostURLs.AddRange(stationURLs);
			}
			else
			{
				QLog.WriteLine(1, $"Error : GameSessionService.RegisterURLs - no session hosted by pid={myPlayerId}");
			}

			return Error(0);
		}


		[RMCMethod(22)]
		public void JoinSession()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(23)]
		public RMCResult AbandonSession(GameSessionKey gameSessionKey)
		{
			return LeaveSession(gameSessionKey);
		}


		[RMCMethod(24)]
		public void SearchSessionsWithParticipants()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(25)]
		public void GetSessions()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(26)]
		public void GetParticipantsURLs()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(27)]
		public void MigrateSessionHost()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(28)]
		public void SplitSession()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(29)]
		public void SearchSocialSessions()
		{
			UNIMPLEMENTED();
		}


		[RMCMethod(30)]
		public void ReportUnsuccessfulJoinSessions()
		{
			UNIMPLEMENTED();
		}


	}
}
