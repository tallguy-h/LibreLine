// SPDX-License-Identifier: GPL-2.0-only
using Com.Daysofwonder.Async;
using Google.Protobuf;
using LibreLine.Rules.Engine;
using Pb;

namespace LibreLine.Games;

/// <summary>Reads the game payloads (<c>pb.T2RCommit</c> and friends) into the rules engine's types.</summary>
internal static class Payloads
{
    /// <summary>Field 13 of <c>GameConfiguration.data</c> holds <c>pb.Settings</c>.</summary>
    private const int SettingsField = 13;

    public static Settings? ReadConfigurationSettings(ByteString data)
    {
        if (data.IsEmpty)
        {
            return null;
        }

        try
        {
            var input = data.CreateCodedInput();
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                if (WireFormat.GetTagFieldNumber(tag) == SettingsField
                    && WireFormat.GetTagWireType(tag) == WireFormat.WireType.LengthDelimited)
                {
                    return Settings.Parser.ParseFrom(input.ReadBytes());
                }

                input.SkipLastField();
            }
        }
        catch (InvalidProtocolBufferException)
        {
            return null;
        }

        return null;
    }

    public static GameSettings ToSettings(Settings settings) =>
        new(settings.Seed, settings.MapEnumIndex, settings.NbrPlayers, settings.FirstPlayer, settings.RulesEngineVersion);

    public static T2RCommit? ParseCommit(byte[] state)
    {
        if (state.Length == 0)
        {
            return null;
        }

        try
        {
            return T2RCommit.Parser.ParseFrom(state);
        }
        catch (InvalidProtocolBufferException)
        {
            return null;
        }
    }

    public static T2RPlayerState? ParsePlayerState(byte[]? data)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }

        try
        {
            return T2RPlayerState.Parser.ParseFrom(data);
        }
        catch (InvalidProtocolBufferException)
        {
            return null;
        }
    }

    public static T2RMulticast? ParseMulticast(ByteString data)
    {
        try
        {
            return T2RMulticast.Parser.ParseFrom(data);
        }
        catch (InvalidProtocolBufferException)
        {
            return null;
        }
    }

    public static Move ToMove(Event e) => new()
    {
        Type = (MoveType)(int)e.Type,
        Player = e.Player,
        Route = e.HasRoute ? e.Route : null,
        City = e.HasCity ? e.City : null,
        Cards = e.Card.ToList(),
        Slot = e.HasSlot ? e.Slot : null,
        Tickets = e.Ticket.ToList(),
        Railroad = e.HasRailroad ? e.Railroad : null,
        ForDummy = e.ForDummy,
        NbrShort = e.NbrShort,
        NbrLong = e.NbrLong,
        Technology = e.HasTechnology ? e.Technology : null,
        TrackPiece = e.HasTrackPiece ? e.TrackPiece : null,
    };

    /// <summary>The opening choices recorded in a committed state (<c>player_state</c>, seats counted from 0).</summary>
    public static List<OpeningChoice> OpeningsFrom(T2RCommit commit) =>
        commit.PlayerState
            .Select(p => new OpeningChoice(
                p.PlayerId + 1,
                p.AskForFirstTickets is { } ask ? ToMove(ask) : null,
                p.ChooseFirstTickets is { } choose ? ToMove(choose) : null))
            .ToList();

    /// <summary>The opening choices the server holds before the first commit.</summary>
    public static List<OpeningChoice> OpeningsFrom(Game game) =>
        game.Seats
            .Select(seat => (seat.Number, State: ParsePlayerState(seat.OpeningData)))
            .Where(x => x.State is not null)
            .Select(x => new OpeningChoice(
                x.Number,
                x.State!.AskForFirstTickets is { } ask ? ToMove(ask) : null,
                x.State.ChooseFirstTickets is { } choose ? ToMove(choose) : null))
            .ToList();

    public static GameOptions ToOptions(GameConfiguration configuration, Settings settings) => new()
    {
        Name = configuration.Name,
        Private = configuration.Private,
        Lurkable = configuration.Lurkable,
        Rated = configuration.Rated,
        MinPlayers = configuration.HasMinPlayers ? configuration.MinPlayers : Math.Max(2, settings.NbrPlayers),
        MaxPlayers = configuration.HasMaxPlayers ? configuration.MaxPlayers : Math.Max(2, settings.NbrPlayers),
        MinKarma = configuration.MinKarma,
        MinRankScore = configuration.MinRankscore,
        FirstPlayer = configuration.HasFirstPlayer && configuration.FirstPlayer > 0 ? configuration.FirstPlayer : null,
        Synchronous = configuration.GameMode == GameConfiguration.Types.GameMode.Synchronous,
        TimeoutSeconds = Math.Max(0, configuration.Timeout),
        RequireOpeningRound = configuration.RequireFirstUserDataRound,
        ObservableBy = !configuration.HasObservableBy ? Observers.None
            : configuration.ObservableBy == GameConfiguration.Types.Observers.Buddies ? Observers.Buddies
            : Observers.All,
        Settings = ToSettings(settings),
        RawConfiguration = configuration.ToByteArray(),
    };
}
