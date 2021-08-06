﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuazalWV
{
    public enum RMCProtocol
    {
        NATTraversalService 			= 3,
        TicketGrantingService 			= 10,	// U
        SecureConnectionService 		= 11,	// U
        NotificationEventManager 		= 14,
        FriendsService 					= 20,	// U
        MatchMakingService 				= 21,
        PersistentStoreService 			= 24,
        AccountManagementService 		= 25,
        UbiAccountManagementService 	= 29,	// U
        NewsService 					= 31,
        UbiNewsService 					= 33,
        PrivilegesService 				= 35,	// U
        TrackingProtocol3Client 		= 36,
        LocalizationService 			= 39,
		GameSessionService				= 42,
        MatchMakingProtocolExtClient 	= 50,
        LSPService 						= 81,
        PlayerStatsService           	= 108,
        RichPresenceService          	= 109,
        ClansService                 	= 110,
        MetaSessionService           	= 112,
        GameInfoService              	= 113,
        ContactsExtensionsService    	= 114,
        UbiMatchmakingService           = 115,
        AchievementsService          	= 116,
        PartyService                 	= 117,
        DriverUniqueIDService        	= 118,
        SocialNetworksService        	= 119,
        UplayWinService                 = 120,
        DriverG2WService             	= 121,
        Game2WebService              	= 122,
        UplayPassService             	= 123,
        OverlordFriendsService       	= 5005,
    }

}
