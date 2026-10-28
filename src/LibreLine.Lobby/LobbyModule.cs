// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using LibreLine.Games;
using LibreLine.Protocol;
using LibreLine.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace LibreLine.Lobby;

public static class LobbyServiceCollectionExtensions
{
    public static IServiceCollection AddLobby(this IServiceCollection services)
    {
        services.AddSingleton<LobbyState>();
        services.AddSingleton<ISessionListener>(p => p.GetRequiredService<LobbyState>());
        services.AddSingleton<IGameListener>(p => p.GetRequiredService<LobbyState>());

        services.AddMessageHandler<EnterLobbyRequest, EnterLobbyHandler>();
        services.AddMessageHandler<ExitLobbyRequest, ExitLobbyHandler>();
        services.AddMessageHandler<AskPlayerInfoRequest, AskPlayerInfoHandler>();
        services.AddMessageHandler<AskAnyPlayerInfoRequest, AskAnyPlayerInfoHandler>();
        services.AddMessageHandler<AsyncBuddyListRequest, AsyncBuddyListHandler>();
        services.AddMessageHandler<AsyncBuddyManagementRequest, AsyncBuddyManagementHandler>();
        services.AddMessageHandler<AsyncIgnoreListRequest, AsyncIgnoreListHandler>();
        services.AddMessageHandler<AsyncIgnoreManagementRequest, AsyncIgnoreManagementHandler>();
        services.AddMessageHandler<MulticastChatRequest, MulticastChatHandler>();
        services.AddMessageHandler<GetChatHistoryRequest, GetChatHistoryHandler>();
        services.AddMessageHandler<EngageGameWithFriendsRequest, EngageGameWithFriendsHandler>();
        services.AddMessageHandler<AnswerInvitationRequest, AnswerInvitationHandler>();
        services.AddMessageHandler<LobbyCreateGameRequest, LobbyCreateGameHandler>();
        services.AddMessageHandler<LobbyJoinGameRequest, LobbyJoinGameHandler>();
        services.AddMessageHandler<LobbyLeaveGameRequest, LobbyLeaveGameHandler>();
        services.AddMessageHandler<SubscribeToObservableGameListRequest, SubscribeObservableListHandler>();
        services.AddMessageHandler<StartObserveGameRequest, StartObserveGameHandler>();
        services.AddMessageHandler<StopObserveGameRequest, StopObserveGameHandler>();
        return services;
    }
}
