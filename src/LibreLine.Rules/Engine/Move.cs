// SPDX-License-Identifier: GPL-2.0-only
namespace LibreLine.Rules.Engine;

/// <summary>Move types, numbered as <c>pb.Event.Type</c>.</summary>
public enum MoveType
{
    ClaimRoute = 1,
    ClaimStation = 2,
    AskForTickets = 3,
    PickOpenTrainCard = 4,
    PickDeckTrainCard = 5,
    ReturnTicketsPublic = 6,
    ReturnTicketsPrivate = 7,
    ShuffleTrainDeck = 8,
    ReceiveTrainCardPublic = 9,
    ReceiveTrainCardPrivate = 10,
    ReceiveTicketCardPublic = 11,
    ReceiveTicketCardPrivate = 12,
    VisibleTrainCardReplaced = 13,
    DigTunnel = 14,
    ChooseFirstTicketsPrivate = 15,
    BuyStockShare = 16,
    AskForFirstTickets = 17,
    PickTechnology = 18,
    RemoveSingleUseTechnologies = 19,
    PutTrackPieceOnRoute = 20,
    PassTurnWithoutPuttingTrackPieceOnRoute = 21,
    ReceiveCountriesBonuses = 22,
}

/// <summary>One move: the rules engine's copy of a <c>pb.Event</c>. <see cref="Player"/> is a 1-based seat.</summary>
public sealed record Move
{
    public required MoveType Type { get; init; }
    public int Player { get; init; }
    public int? Route { get; init; }
    public int? City { get; init; }
    public IReadOnlyList<int> Cards { get; init; } = [];
    public int? Slot { get; init; }
    public IReadOnlyList<int> Tickets { get; init; } = [];
    public int? Railroad { get; init; }
    public bool ForDummy { get; init; }
    public int NbrShort { get; init; }
    public int NbrLong { get; init; }
    public int? Technology { get; init; }
    public int? TrackPiece { get; init; }

    public override string ToString() => $"{Type} by seat {Player}";
}

/// <summary>A game's settings (<c>pb.Settings</c>). <see cref="FirstPlayer"/> counts from 0.</summary>
public sealed record GameSettings(int Seed, int MapEnum, int Players, int FirstPlayer, int RulesEngineVersion);

/// <summary>
/// A seat's private opening: Germany's request for short and long tickets, and the tickets kept. Either may be
/// missing while the opening round is still going.
/// </summary>
public sealed record OpeningChoice(int Seat, Move? Ask, Move? Choose);

/// <summary>Why a move or an opening choice isn't legal.</summary>
/// <param name="MoveIndex">The index into the move history, or -1 for an opening choice.</param>
public sealed record RuleViolation(int MoveIndex, int Seat, string Reason)
{
    public override string ToString() =>
        MoveIndex < 0 ? $"opening of seat {Seat}: {Reason}" : $"move {MoveIndex} (seat {Seat}): {Reason}";
}

public sealed class RuleException(string message) : Exception(message);
