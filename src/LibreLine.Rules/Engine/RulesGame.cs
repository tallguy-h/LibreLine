// SPDX-License-Identifier: GPL-2.0-only
using LibreLine.Rules.Maps;
using LibreLine.Rules.Scoring;

namespace LibreLine.Rules.Engine;

/// <summary>Where the seat to move is within its turn.</summary>
public enum TurnPhase
{
    /// <summary>The turn hasn't started: any move.</summary>
    Start,

    /// <summary>One train card drawn; the second is due.</summary>
    SecondCard,

    /// <summary>France: the draws are done; a track piece (or a pass) ends the turn.</summary>
    TrackPiece,

    /// <summary>Tickets drawn; the player returns those they don't keep.</summary>
    ReturnTickets,

    /// <summary>A tunnel's extra cost is due, or the player gives up.</summary>
    DigTunnel,

    /// <summary>Pennsylvania: a route with logos was claimed; the claimer's share is due.</summary>
    OwnShare,

    /// <summary>Pennsylvania, 2 players: the dummy shareholder's share is due.</summary>
    DummyShare,

    /// <summary>Pennsylvania, 3+ players: a dummy share may follow, or the turn is over.</summary>
    OptionalDummyShare,

    /// <summary>UK: the turn's move is done; <c>REMOVE_SINGLE_USE_TECHNOLOGIES</c> ends it.</summary>
    EndOfTurn,

    Over,
}

/// <summary>
/// The server's model of one game (<see href="../../docs/RULES.md">RULES.md</see>): it deals from the seed, then
/// applies each opening choice and move, refusing any that isn't legal.
/// </summary>
public sealed class RulesGame
{

    private readonly MinStdRandom random;
    private readonly List<int> shortDeck = [];
    private readonly List<int> longDeck = [];
    private readonly List<int> trainDeck = [];
    private readonly List<int> discard = [];
    private readonly int?[] faceUp = new int?[5];
    private readonly PlayerState[] players;
    private readonly List<int>[] routeOwners;
    private readonly int?[] paintedPiece;
    private readonly bool[] pieceInSupply;
    private readonly int[] technologyStock;
    private readonly int[] dummyShares;
    private readonly int?[] stationOwner;

    // The turn in progress.
    private int draws;
    private bool pickedFaceUp;
    private bool mainWasDraw;
    private bool mainWasClaim;
    private bool boughtTechnology;
    private int claimsThisTurn;
    private int? pendingRoute;
    private List<int> tunnelBase = [];
    private List<int> tunnelRevealed = [];
    private CardColour? tunnelColour;
    private int tunnelExtra;
    private int? finalTurnsLeft;

    public RulesGame(MapDefinition map, GameSettings settings)
    {
        if (settings.Players < 1 || settings.Players > Math.Max(map.Rules.MaxPlayers, 1))
        {
            throw new RuleException($"{settings.Players} players can't play {map.Name}");
        }

        Map = map;
        Settings = settings;
        random = new MinStdRandom(settings.Seed);
        players = Enumerable.Range(1, settings.Players)
            .Select(seat => new PlayerState(seat, map.Rules.Trains, map.Rules.Stations, map.Railroads.Count))
            .ToArray();
        routeOwners = map.Routes.Select(_ => new List<int>()).ToArray();
        paintedPiece = new int?[map.Routes.Count];
        pieceInSupply = map.TrackPieces.Select(_ => true).ToArray();
        technologyStock = new int[map.Technologies.Count == 0 ? 0 : map.Technologies.Max(t => t.Id) + 1];
        foreach (var technology in map.Technologies)
        {
            technologyStock[technology.Id] = technology.Copies;
        }

        dummyShares = new int[map.Railroads.Count];
        stationOwner = new int?[map.Cities.Count];
        CurrentPlayer = Math.Clamp(settings.FirstPlayer, 0, settings.Players - 1) + 1;
        SetUp();
    }

    private RulesGame(RulesGame other)
    {
        Map = other.Map;
        Settings = other.Settings;
        random = other.random.Clone();
        shortDeck = [.. other.shortDeck];
        longDeck = [.. other.longDeck];
        trainDeck = [.. other.trainDeck];
        discard = [.. other.discard];
        faceUp = (int?[])other.faceUp.Clone();
        players = other.players.Select(p => p.Clone()).ToArray();
        routeOwners = other.routeOwners.Select(o => new List<int>(o)).ToArray();
        paintedPiece = (int?[])other.paintedPiece.Clone();
        pieceInSupply = (bool[])other.pieceInSupply.Clone();
        technologyStock = (int[])other.technologyStock.Clone();
        dummyShares = (int[])other.dummyShares.Clone();
        stationOwner = (int?[])other.stationOwner.Clone();
        draws = other.draws;
        pickedFaceUp = other.pickedFaceUp;
        mainWasDraw = other.mainWasDraw;
        mainWasClaim = other.mainWasClaim;
        boughtTechnology = other.boughtTechnology;
        claimsThisTurn = other.claimsThisTurn;
        pendingRoute = other.pendingRoute;
        tunnelBase = [.. other.tunnelBase];
        tunnelRevealed = [.. other.tunnelRevealed];
        tunnelColour = other.tunnelColour;
        tunnelExtra = other.tunnelExtra;
        finalTurnsLeft = other.finalTurnsLeft;
        CurrentPlayer = other.CurrentPlayer;
        Phase = other.Phase;
        MovesApplied = other.MovesApplied;
        TurnsTaken = other.TurnsTaken;
        DeckReshuffled = other.DeckReshuffled;
    }

    /// <summary>An independent copy, e.g. to try a move without touching this model.</summary>
    public RulesGame Clone() => new(this);

    /// <summary>Tests: moves the given cards into a seat's hand from wherever they are.</summary>
    internal void GiveCards(int seat, IEnumerable<int> cards)
    {
        foreach (var card in cards)
        {
            trainDeck.Remove(card);
            discard.Remove(card);
            foreach (var player in players)
            {
                player.HandList.Remove(card);
            }

            for (var slot = 0; slot < faceUp.Length; slot++)
            {
                if (faceUp[slot] == card)
                {
                    faceUp[slot] = null;
                }
            }

            Player(seat).HandList.Add(card);
        }
    }

    /// <summary>True if the move would be legal now.</summary>
    public bool IsLegal(Move move)
    {
        try
        {
            Clone().Apply(move);
            return true;
        }
        catch (RuleException)
        {
            return false;
        }
    }

    public MapDefinition Map { get; }

    public GameSettings Settings { get; }

    /// <summary>The route being claimed, while a tunnel or a share is pending.</summary>
    public int? PendingRoute => pendingRoute;

    /// <summary>The colour of the tunnel claim in progress (null when paid only with locomotives).</summary>
    public CardColour? TunnelColour => tunnelColour;

    /// <summary>UK: a technology has been bought this turn.</summary>
    public bool BoughtTechnologyThisTurn => boughtTechnology;

    /// <summary>France: the track piece is in the supply.</summary>
    public bool PieceInSupply(int piece) => pieceInSupply[piece];

    /// <summary>UK: copies of the technology left in the stock.</summary>
    public int TechnologyStock(int technology) => technology < technologyStock.Length ? technologyStock[technology] : 0;

    public IReadOnlyList<PlayerState> Players => players;

    /// <summary>The seat to move (1-based).</summary>
    public int CurrentPlayer { get; private set; }

    public TurnPhase Phase { get; private set; } = TurnPhase.Start;

    public bool IsOver => Phase == TurnPhase.Over;

    /// <summary>Moves applied so far.</summary>
    public int MovesApplied { get; private set; }

    /// <summary>Completed turns.</summary>
    public int TurnsTaken { get; private set; }

    /// <summary>True once the discard pile has been shuffled back into the train deck.</summary>
    public bool DeckReshuffled { get; private set; }

    public IReadOnlyList<int?> FaceUp => faceUp;

    public int TrainDeckCount => trainDeck.Count;

    public IReadOnlyList<int> TrainDeck => trainDeck;

    public IReadOnlyList<int> Discard => discard;

    public IReadOnlyList<int> ShortTicketDeck => shortDeck;

    public IReadOnlyList<int> LongTicketDeck => longDeck;

    public IReadOnlyList<int> RevealedTunnelCards => tunnelRevealed;

    public int TunnelExtraCost => tunnelExtra;

    public IReadOnlyList<int> DummyShares => dummyShares;

    public bool AllOpeningsDone => players.All(p => p.OpeningDone);

    public PlayerState Player(int seat) =>
        seat >= 1 && seat <= players.Length ? players[seat - 1] : throw new RuleException($"no seat {seat}");

    public IReadOnlyList<int> Owners(int route) => routeOwners[route];

    public int? PaintedPiece(int route) => paintedPiece[route];

    public CardColour ColourOf(int card) => Map.Cards[card];

    // ----- Setting up --------------------------------------------------------------------------------------------

    private void SetUp()
    {
        BuildTicketDeck(shortDeck, Map.Tickets.Where(t => !t.IsLong));
        BuildTicketDeck(longDeck, Map.Tickets.Where(t => t.IsLong));

        trainDeck.AddRange(Enumerable.Range(0, Map.Cards.Count));

        ReshuffleCheck(faceUp.Length);
        for (var slot = 0; slot < faceUp.Length; slot++)
        {
            faceUp[slot] = trainDeck.Count > 0 ? DrawTrainCard() : null;
        }

        LocomotiveCheck();

        foreach (var player in players)
        {
            for (var i = 0; i < Map.Rules.HandLocomotives; i++)
            {
                var loco = trainDeck.Where(c => Map.Cards[c] == CardColour.Locomotive).DefaultIfEmpty(-1).Min();
                if (loco >= 0)
                {
                    trainDeck.Remove(loco);
                    player.HandList.Add(loco);
                }
            }

            ReshuffleCheck(Map.Rules.HandCards);
            for (var i = 0; i < Map.Rules.HandCards && trainDeck.Count > 0; i++)
            {
                player.HandList.Add(DrawTrainCard());
            }
        }

        if (!Map.Rules.ChosenSplit)
        {
            foreach (var player in players)
            {
                player.PendingList.AddRange(TakeTickets(shortDeck, Map.Rules.OpeningShort));
                player.PendingList.AddRange(TakeTickets(longDeck, Map.Rules.OpeningLong));
                player.OpeningDealt = true;
            }
        }
    }

    private void BuildTicketDeck(List<int> deck, IEnumerable<Ticket> tickets)
    {
        var remaining = tickets.Select(t => t.Id).ToList();
        while (remaining.Count > 0)
        {
            var index = random.Uniform(0, remaining.Count - 1);
            deck.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
    }

    private static List<int> TakeTickets(List<int> deck, int count)
    {
        var taken = deck.Take(count).ToList();
        deck.RemoveRange(0, taken.Count);
        return taken;
    }

    private int DrawTrainCard()
    {
        var index = random.Uniform(0, trainDeck.Count - 1);
        var card = trainDeck[index];
        trainDeck.RemoveAt(index);
        return card;
    }

    /// <summary>Before drawing <paramref name="count"/> cards: shuffle the discard pile back in if the deck is short.</summary>
    private void ReshuffleCheck(int count)
    {
        if (count >= trainDeck.Count && discard.Count > 0)
        {
            trainDeck.AddRange(discard);
            discard.Clear();
            DeckReshuffled = true;
        }
    }

    private bool IsLocomotive(int card) => Map.Cards[card] == CardColour.Locomotive;

    /// <summary>While the face-up row holds too many locomotives, discard all five and deal five more.</summary>
    private void LocomotiveCheck()
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (faceUp.Count(c => c is { } card && IsLocomotive(card)) < Map.Rules.FaceUpRedealAt)
            {
                return;
            }

            // Give up when the cards left can't make a legal row (only locomotives remain).
            var nonLocos = trainDeck.Concat(discard).Concat(faceUp.OfType<int>()).Count(c => !IsLocomotive(c));
            if (nonLocos <= faceUp.Length - Map.Rules.FaceUpRedealAt)
            {
                return;
            }

            for (var slot = 0; slot < faceUp.Length; slot++)
            {
                if (faceUp[slot] is { } card)
                {
                    discard.Add(card);
                    faceUp[slot] = null;
                }
            }

            ReshuffleCheck(faceUp.Length);
            for (var slot = 0; slot < faceUp.Length; slot++)
            {
                faceUp[slot] = trainDeck.Count > 0 ? DrawTrainCard() : null;
            }
        }
    }

    // ----- The opening -------------------------------------------------------------------------------------------

    /// <summary>Germany: a seat says how many short and long tickets it wants.</summary>
    public void ApplyOpeningAsk(Move ask)
    {
        if (!Map.Rules.ChosenSplit)
        {
            throw new RuleException("this map has no opening ticket request");
        }

        var player = Player(ask.Player);
        if (player.OpeningAsk is not null)
        {
            throw new RuleException("the opening tickets were already requested");
        }

        if (ask.NbrShort < 0 || ask.NbrLong < 0 || ask.NbrShort + ask.NbrLong != Map.Rules.OpeningTotal)
        {
            throw new RuleException($"must ask for {Map.Rules.OpeningTotal} tickets in all");
        }

        player.OpeningAsk = (ask.NbrShort, ask.NbrLong);

        // Tickets are dealt in seat order, once every earlier seat has asked.
        foreach (var seat in players)
        {
            if (seat.OpeningDealt)
            {
                continue;
            }

            if (seat.OpeningAsk is not { } request)
            {
                break;
            }

            seat.PendingList.AddRange(TakeTickets(shortDeck, request.Short));
            seat.PendingList.AddRange(TakeTickets(longDeck, request.Long));
            seat.OpeningDealt = true;
        }
    }

    /// <summary>True when the seat's opening tickets have been dealt, so its choice can be checked.</summary>
    public bool CanCheckOpeningChoice(int seat) => Player(seat).OpeningDealt;

    /// <summary>A seat keeps some of its opening tickets.</summary>
    public void ApplyOpeningChoice(Move choose)
    {
        var player = Player(choose.Player);
        if (player.OpeningDone)
        {
            throw new RuleException("the opening tickets were already chosen");
        }

        if (!player.OpeningDealt)
        {
            throw new RuleException("the opening tickets haven't been dealt yet");
        }

        var kept = Distinct(choose.Tickets, "ticket");
        if (kept.Any(t => !player.PendingList.Contains(t)))
        {
            throw new RuleException("kept a ticket that wasn't dealt to this seat");
        }

        if (kept.Count < Math.Min(Map.Rules.OpeningKeep, player.PendingList.Count))
        {
            throw new RuleException($"must keep at least {Map.Rules.OpeningKeep} tickets");
        }

        if (Map.Rules.OpeningMaxLong is { } maxLong && kept.Count(t => Map.Tickets[t].IsLong) > maxLong)
        {
            throw new RuleException($"may keep at most {maxLong} long tickets");
        }

        var unkept = player.PendingList.Where(t => !kept.Contains(t)).ToList();
        player.TicketList.AddRange(kept);
        player.PendingList.Clear();
        if (!Map.Rules.UnkeptOpeningLeaveGame)
        {
            ReturnUnderDecks(unkept);
        }

        player.OpeningDone = true;
    }

    private void ReturnUnderDecks(IEnumerable<int> tickets)
    {
        foreach (var ticket in tickets)
        {
            (Map.Tickets[ticket].IsLong ? longDeck : shortDeck).Add(ticket);
        }
    }

    // ----- Moves -------------------------------------------------------------------------------------------------

    /// <summary>Applies one move, or throws <see cref="RuleException"/> if it isn't legal (leaving the model unusable).</summary>
    public void Apply(Move move)
    {
        if (!AllOpeningsDone)
        {
            throw new RuleException("the opening round isn't over");
        }

        if (Phase == TurnPhase.Over)
        {
            throw new RuleException("the game is over");
        }

        if (Phase == TurnPhase.OptionalDummyShare && move.Player != CurrentPlayer)
        {
            EndTurn();
            if (Phase == TurnPhase.Over)
            {
                throw new RuleException("the game is over");
            }
        }

        if (move.Player != CurrentPlayer)
        {
            throw new RuleException($"it is seat {CurrentPlayer}'s turn");
        }

        switch (move.Type)
        {
            case MoveType.PickDeckTrainCard:
                PickDeck();
                break;
            case MoveType.PickOpenTrainCard:
                PickOpen(move);
                break;
            case MoveType.ClaimRoute:
                ClaimRoute(move);
                break;
            case MoveType.DigTunnel:
                DigTunnel(move);
                break;
            case MoveType.ClaimStation:
                ClaimStation(move);
                break;
            case MoveType.AskForTickets:
                AskForTickets(move);
                break;
            case MoveType.ReturnTicketsPrivate:
                ReturnTickets(move);
                break;
            case MoveType.BuyStockShare:
                BuyStockShare(move);
                break;
            case MoveType.PickTechnology:
                PickTechnology(move);
                break;
            case MoveType.RemoveSingleUseTechnologies:
                RemoveSingleUseTechnologies();
                break;
            case MoveType.PutTrackPieceOnRoute:
                PutTrackPiece(move);
                break;
            case MoveType.PassTurnWithoutPuttingTrackPieceOnRoute:
                Expect(TurnPhase.TrackPiece);
                EndTurn();
                break;
            default:
                throw new RuleException($"{move.Type} isn't a move this server knows");
        }

        MovesApplied++;
    }

    /// <summary>Ends a turn that may end where it stands (a Pennsylvania turn with an optional dummy share).</summary>
    public void CloseOpenTurn()
    {
        if (Phase == TurnPhase.OptionalDummyShare)
        {
            EndTurn();
        }
    }

    private void Expect(params TurnPhase[] phases)
    {
        if (!phases.Contains(Phase))
        {
            throw new RuleException($"not allowed now (the turn is at {Phase})");
        }
    }

    private PlayerState Current => players[CurrentPlayer - 1];

    private bool HasWaterTenderDraw =>
        Phase == TurnPhase.EndOfTurn && mainWasDraw && draws == 2 && !pickedFaceUp
        && Current.Has(TechnologyId.WaterTenders);

    private void PickDeck()
    {
        var waterTenders = HasWaterTenderDraw;
        if (!waterTenders)
        {
            Expect(TurnPhase.Start, TurnPhase.SecondCard);
        }

        ReshuffleCheck(1);
        if (trainDeck.Count == 0)
        {
            throw new RuleException("the train deck is empty");
        }

        Current.HandList.Add(DrawTrainCard());
        draws++;
        if (waterTenders)
        {
            return;
        }

        if (Phase == TurnPhase.Start && CanDrawSecondCard())
        {
            Phase = TurnPhase.SecondCard;
        }
        else
        {
            CompleteMainMove(draw: true);
        }
    }

    private void PickOpen(Move move)
    {
        Expect(TurnPhase.Start, TurnPhase.SecondCard);
        if (move.Slot is not { } slot || slot < 1 || slot > faceUp.Length || faceUp[slot - 1] is not { } card)
        {
            throw new RuleException("no card in that face-up slot");
        }

        var locomotive = IsLocomotive(card);
        if (locomotive && Phase == TurnPhase.SecondCard && Map.Rules.FaceUpLocomotiveEndsTurn)
        {
            throw new RuleException("a face-up locomotive can't be the second card");
        }

        ReshuffleCheck(1);
        faceUp[slot - 1] = trainDeck.Count > 0 ? DrawTrainCard() : null;
        Current.HandList.Add(card);
        LocomotiveCheck();
        draws++;
        pickedFaceUp = true;

        if (Phase == TurnPhase.Start && !(locomotive && Map.Rules.FaceUpLocomotiveEndsTurn) && CanDrawSecondCard())
        {
            Phase = TurnPhase.SecondCard;
        }
        else
        {
            CompleteMainMove(draw: true);
        }
    }

    private bool CanDrawSecondCard() =>
        trainDeck.Count + discard.Count > 0
        || faceUp.Any(c => c is { } card && !(IsLocomotive(card) && Map.Rules.FaceUpLocomotiveEndsTurn));

    private void ClaimRoute(Move move)
    {
        var thermocompressor = Phase == TurnPhase.EndOfTurn && mainWasClaim && claimsThisTurn == 1
            && Current.Has(TechnologyId.Thermocompressor);
        if (!thermocompressor)
        {
            Expect(TurnPhase.Start);
        }

        var route = RouteAt(move.Route);
        var player = Current;
        var rightOfWay = CheckRouteOpen(route, player);
        CheckTechnologiesFor(route, player);

        if (player.Trains < route.Trains)
        {
            throw new RuleException("not enough trains");
        }

        var cards = CardsFromHand(player, move.Cards);
        var colour = CheckRoutePayment(route, RouteColourFor(route), cards, player);

        foreach (var card in cards)
        {
            player.HandList.Remove(card);
        }

        if (thermocompressor)
        {
            player.UsedTechnologies.Add((int)TechnologyId.Thermocompressor);
        }

        if (rightOfWay)
        {
            player.UsedTechnologies.Add((int)TechnologyId.RightOfWay);
        }

        if (route.Tunnel)
        {
            ReshuffleCheck(Map.Rules.TunnelReveal);
            var revealed = new List<int>();
            for (var i = 0; i < Map.Rules.TunnelReveal && trainDeck.Count > 0; i++)
            {
                revealed.Add(DrawTrainCard());
            }

            var extra = revealed.Count(c => IsLocomotive(c) || (colour is { } claimColour && Map.Cards[c] == claimColour));
            if (extra > 0)
            {
                pendingRoute = route.Id;
                tunnelBase = cards.ToList();
                tunnelRevealed = revealed;
                tunnelColour = colour;
                tunnelExtra = extra;
                Phase = TurnPhase.DigTunnel;
                return;
            }

            discard.AddRange(revealed);
        }

        discard.AddRange(cards);
        PlaceRoute(route, player);
        AfterClaim(route);
    }

    private void DigTunnel(Move move)
    {
        Expect(TurnPhase.DigTunnel);
        var route = Map.Routes[pendingRoute!.Value];
        var player = Current;

        if (move.Cards.Count == 0)
        {
            // Giving up: the base payment goes back to the hand and only the revealed cards are discarded.
            player.HandList.AddRange(tunnelBase);
            discard.AddRange(tunnelRevealed);
            ClearTunnel();
            CompleteMainMove(draw: false);
            return;
        }

        var extra = CardsFromHand(player, move.Cards);
        if (extra.Count != tunnelExtra)
        {
            throw new RuleException($"the tunnel costs {tunnelExtra} more cards");
        }

        foreach (var card in extra)
        {
            var ok = IsLocomotive(card) || (tunnelColour is { } colour && Map.Cards[card] == colour);
            if (!ok)
            {
                throw new RuleException("the tunnel's extra cards must match the claim's colour, or be locomotives");
            }
        }

        foreach (var card in extra)
        {
            player.HandList.Remove(card);
        }

        discard.AddRange(tunnelRevealed);
        discard.AddRange(tunnelBase);
        discard.AddRange(extra);
        ClearTunnel();
        PlaceRoute(route, player);
        AfterClaim(route);
    }

    private void ClearTunnel()
    {
        pendingRoute = null;
        tunnelBase = [];
        tunnelRevealed = [];
        tunnelColour = null;
        tunnelExtra = 0;
    }

    private void AfterClaim(Route route)
    {
        if (Map.Rules.StockShares && route.Shares.Count > 0)
        {
            pendingRoute = route.Id;
            mainWasClaim = true;
            Phase = TurnPhase.OwnShare;
            return;
        }

        CompleteMainMove(draw: false, claim: true);
    }

    private Route RouteAt(int? id) =>
        id is { } routeId && routeId >= 0 && routeId < Map.Routes.Count
            ? Map.Routes[routeId]
            : throw new RuleException($"no route {id}");

    /// <summary>Checks that the route can be claimed by this player; returns true when it uses Right of Way.</summary>
    private bool CheckRouteOpen(Route route, PlayerState player)
    {
        if (Map.Rules.TrackPieces)
        {
            if (route.Colour == RouteColour.Blank && paintedPiece[route.Id] is null)
            {
                throw new RuleException("the route has no track piece yet");
            }

            if (IsCrossBlocked(route))
            {
                throw new RuleException("a route drawn across this one has been taken");
            }
        }

        var rightOfWay = false;
        var owners = routeOwners[route.Id];
        if (owners.Count > 0)
        {
            if (owners.Contains(player.Seat))
            {
                throw new RuleException("the player already owns this route");
            }

            if (!player.Has(TechnologyId.RightOfWay) || player.UsedTechnologies.Contains((int)TechnologyId.RightOfWay))
            {
                throw new RuleException("the route is taken");
            }

            rightOfWay = true;
        }

        var parallels = Map.Parallels[route.Id];
        if (parallels.Any(p => routeOwners[p].Contains(player.Seat)))
        {
            throw new RuleException("the player already owns the other half of this double route");
        }

        if (!rightOfWay && players.Length <= Map.Rules.DoubleRouteMaxPlayers && !route.ParallelOpen)
        {
            // With few players a double route has one usable side. For triple routes (not yet seen), the last
            // one only closes when both others are taken.
            var taken = parallels.Count(p => routeOwners[p].Count > 0 && !Map.Routes[p].ParallelOpen);
            if (parallels.Count > 0 && taken >= parallels.Count)
            {
                throw new RuleException("the other side of this double route is taken");
            }
        }

        return rightOfWay;
    }

    private bool IsCrossBlocked(Route route) =>
        Map.Crossings[route.Id].Any(other => paintedPiece[other] is not null || routeOwners[other].Count > 0);

    private void CheckTechnologiesFor(Route route, PlayerState player)
    {
        if (!Map.Rules.Technologies || route.Length > 6)
        {
            // Southampton-New York needs no technology.
            return;
        }

        foreach (var end in new[] { route.From, route.To })
        {
            var city = Map.Cities[end];
            var needed = (city.Region ?? city.Name) switch
            {
                "Ireland" or "France" => TechnologyId.IrelandFranceConcession,
                "Scotland" => TechnologyId.ScotlandConcession,
                "Wales" => TechnologyId.WalesConcession,
                _ => (TechnologyId?)null,
            };
            if (needed is { } concession && !player.Has(concession))
            {
                throw new RuleException($"needs {concession}");
            }
        }

        if (route.Length == 3 && !player.Has(TechnologyId.MechanicalStoker))
        {
            throw new RuleException("needs Mechanical Stoker");
        }

        if (route.Length is >= 4 and <= 6 && !player.Has(TechnologyId.SuperheatedSteamBoiler))
        {
            throw new RuleException("needs Superheated Steam Boiler");
        }

        if (route.Ferry > 0 && !player.Has(TechnologyId.Propellers))
        {
            throw new RuleException("needs Propellers");
        }
    }

    private RouteColour RouteColourFor(Route route) =>
        route.Colour == RouteColour.Blank && paintedPiece[route.Id] is { } piece
            ? (RouteColour)(int)Map.TrackPieces[piece].Colour
            : route.Colour;

    private List<int> CardsFromHand(PlayerState player, IReadOnlyList<int> cards)
    {
        var distinct = Distinct(cards, "card");
        if (distinct.Any(c => !player.HandList.Contains(c)))
        {
            throw new RuleException("paid with a card the player doesn't hold");
        }

        return distinct;
    }

    private static List<int> Distinct(IReadOnlyList<int> ids, string what)
    {
        var list = ids.ToList();
        if (list.Distinct().Count() != list.Count)
        {
            throw new RuleException($"the same {what} is listed twice");
        }

        return list;
    }

    /// <summary>Checks a claim's payment and returns the claim's colour (null when paid only with locomotives).</summary>
    internal CardColour? CheckRoutePayment(Route route, RouteColour colour, IReadOnlyList<int> cards, PlayerState player)
    {
        var required = route.Length;
        if (player.Has(TechnologyId.DieselPower))
        {
            required = Math.Max(1, required - 1);
        }

        var locos = cards.Count(IsLocomotive);
        var colours = cards.Where(c => !IsLocomotive(c)).Select(c => Map.Cards[c]).Distinct().ToList();

        if (Map.Rules.LocomotiveSubstitute > 0)
        {
            return CheckSubstitutePayment(route, colour, cards, required, locos, player);
        }

        if (colours.Count > 1)
        {
            throw new RuleException("the cards must all be one colour");
        }

        CardColour? claimColour = colours.Count == 1 ? colours[0] : null;
        if (claimColour is { } c && colour != RouteColour.Grey && (int)c != (int)colour)
        {
            throw new RuleException("the cards don't match the route's colour");
        }

        if (locos > 0)
        {
            var allowed = Map.Rules.Locomotives switch
            {
                LocomotiveUse.TunnelsOnly => route.Tunnel,
                LocomotiveUse.TunnelsAndFerries => route.Tunnel || route.Ferry > 0,
                _ => true,
            };
            if (!allowed)
            {
                throw new RuleException("locomotives can't be played on this route");
            }
        }

        if (cards.Count != required)
        {
            throw new RuleException($"the route takes {required} cards");
        }

        if (locos < route.Ferry)
        {
            throw new RuleException($"the ferry needs {route.Ferry} locomotives");
        }

        return claimColour;
    }

    /// <summary>
    /// UK: real locomotives, plus groups of cards that each stand in for one, plus cards of one colour, must cover the
    /// route exactly, with at least the ferry's number of locomotives.
    /// </summary>
    private CardColour? CheckSubstitutePayment(Route route, RouteColour colour, IReadOnlyList<int> cards, int required,
        int locos, PlayerState player)
    {
        var group = player.Has(TechnologyId.Booster) ? 3 : 4;
        var total = cards.Count;
        var candidates = colour == RouteColour.Grey
            ? cards.Where(c => !IsLocomotive(c)).Select(c => (CardColour?)Map.Cards[c]).Distinct().Append(null).ToList()
            : [(CardColour)(int)colour];

        foreach (var candidate in candidates)
        {
            var matching = candidate is { } c ? cards.Count(card => Map.Cards[card] == c) : 0;
            var offColour = total - locos - matching;
            for (var groups = 0; groups * group <= total; groups++)
            {
                var inGroups = groups * group;
                if (inGroups < offColour)
                {
                    continue;
                }

                var remaining = total - inGroups;
                if (remaining + groups != required)
                {
                    continue;
                }

                // Groups take the off-colour cards first, then matching cards, and locomotives last.
                var spare = inGroups - offColour;
                var locosLeft = spare <= matching ? locos : locos - (spare - matching);
                if (locosLeft + groups >= route.Ferry)
                {
                    return candidate;
                }
            }
        }

        throw new RuleException("the cards don't pay for this route");
    }

    private void PlaceRoute(Route route, PlayerState player)
    {
        routeOwners[route.Id].Add(player.Seat);
        player.Trains -= route.Trains;
        player.RouteList.Add(route.Id);
        claimsThisTurn++;

        if (paintedPiece[route.Id] is { } piece)
        {
            pieceInSupply[piece] = true;
        }

        if (player.Has(TechnologyId.BoilerLagging))
        {
            player.BoilerLaggingClaims++;
        }

        if (route.Ferry > 0 && player.Has(TechnologyId.SteamTurbine))
        {
            player.SteamTurbineFerries++;
        }
    }

    private void ClaimStation(Move move)
    {
        Expect(TurnPhase.Start);
        var player = Current;
        if (Map.Rules.Stations == 0 || player.StationsLeft == 0)
        {
            throw new RuleException("no station left");
        }

        if (move.City is not { } city || city < 0 || city >= Map.Cities.Count)
        {
            throw new RuleException($"no city {move.City}");
        }

        if (stationOwner[city] is not null)
        {
            throw new RuleException("the city already has a station");
        }

        var cards = CardsFromHand(player, move.Cards);
        var needed = Map.Rules.Stations - player.StationsLeft + 1;
        if (cards.Count != needed)
        {
            throw new RuleException($"this station costs {needed} cards");
        }

        if (cards.Where(c => !IsLocomotive(c)).Select(c => Map.Cards[c]).Distinct().Count() > 1)
        {
            throw new RuleException("the cards must all be one colour");
        }

        foreach (var card in cards)
        {
            player.HandList.Remove(card);
        }

        discard.AddRange(cards);
        stationOwner[city] = player.Seat;
        player.StationList.Add(city);
        player.StationsLeft--;
        CompleteMainMove(draw: false);
    }

    private void AskForTickets(Move move)
    {
        Expect(TurnPhase.Start);
        var nShort = move.NbrShort;
        var nLong = move.NbrLong;
        if (!Map.Rules.ChosenSplit)
        {
            if (nLong != 0)
            {
                throw new RuleException("draws on this map are short tickets only");
            }

            if (nShort == 0)
            {
                nShort = Map.Rules.DrawCount;
            }
        }

        if (nShort < 0 || nLong < 0 || nShort + nLong == 0 || nShort + nLong > Map.Rules.DrawCount)
        {
            throw new RuleException($"must ask for up to {Map.Rules.DrawCount} tickets");
        }

        var player = Current;
        player.PendingList.AddRange(TakeTickets(shortDeck, nShort));
        player.PendingList.AddRange(TakeTickets(longDeck, nLong));
        if (player.PendingList.Count == 0)
        {
            throw new RuleException("no tickets left to draw");
        }

        Phase = TurnPhase.ReturnTickets;
    }

    private void ReturnTickets(Move move)
    {
        Expect(TurnPhase.ReturnTickets);
        var player = Current;
        var returned = Distinct(move.Tickets, "ticket");
        if (returned.Any(t => !player.PendingList.Contains(t)))
        {
            throw new RuleException("returned a ticket that wasn't drawn");
        }

        var kept = player.PendingList.Where(t => !returned.Contains(t)).ToList();
        if (kept.Count < Math.Min(Map.Rules.DrawKeep, player.PendingList.Count))
        {
            throw new RuleException($"must keep at least {Map.Rules.DrawKeep} tickets");
        }

        player.TicketList.AddRange(kept);
        player.PendingList.Clear();
        if (!Map.Rules.ReturnedLeaveGame)
        {
            ReturnUnderDecks(returned);
        }

        CompleteMainMove(draw: false);
    }

    private void BuyStockShare(Move move)
    {
        var route = pendingRoute is { } id ? Map.Routes[id] : null;
        if (route is null || move.Railroad is not { } railroad || !route.Shares.Contains(railroad))
        {
            throw new RuleException("the claimed route doesn't offer that railroad");
        }

        if (!move.ForDummy)
        {
            Expect(TurnPhase.OwnShare);
            Current.SharesList[railroad]++;
            Phase = players.Length == 2 ? TurnPhase.DummyShare : TurnPhase.OptionalDummyShare;
            return;
        }

        Expect(TurnPhase.DummyShare, TurnPhase.OptionalDummyShare);
        dummyShares[railroad]++;
        EndTurn();
    }

    private void PickTechnology(Move move)
    {
        if (!Map.Rules.Technologies)
        {
            throw new RuleException("this map has no technologies");
        }

        Expect(TurnPhase.Start, TurnPhase.EndOfTurn);
        if (boughtTechnology)
        {
            throw new RuleException("only one technology per turn");
        }

        var technology = Map.Technologies.FirstOrDefault(t => t.Id == move.Technology)
            ?? throw new RuleException($"no technology {move.Technology}");
        var player = Current;
        if (player.TechnologySet.Contains(technology.Id))
        {
            throw new RuleException("the player already has this technology");
        }

        if (technologyStock[technology.Id] == 0)
        {
            throw new RuleException("none of this technology left");
        }

        if (DeckReshuffled && technology.Kind is TechnologyId.EqualisingBeam or TechnologyId.RiskyContracts)
        {
            throw new RuleException("can't be bought once the train deck has been reshuffled");
        }

        var cards = CardsFromHand(player, move.Cards);
        var group = player.Has(TechnologyId.Booster) ? 3 : 4;
        var locos = cards.Count(IsLocomotive);
        var paid = Enumerable.Range(0, technology.Cost + 1)
            .Any(groups => technology.Cost - groups <= locos && cards.Count == technology.Cost - groups + (groups * group));
        if (!paid)
        {
            throw new RuleException($"costs {technology.Cost} locomotives (or {group} cards for each)");
        }

        foreach (var card in cards)
        {
            player.HandList.Remove(card);
        }

        discard.AddRange(cards);
        technologyStock[technology.Id]--;
        player.TechnologySet.Add(technology.Id);
        boughtTechnology = true;
    }

    private void RemoveSingleUseTechnologies()
    {
        Expect(TurnPhase.EndOfTurn);
        var player = Current;
        foreach (var used in player.UsedTechnologies.ToList())
        {
            player.TechnologySet.Remove(used);
            player.UsedTechnologies.Remove(used);
            technologyStock[used]++;
        }

        EndTurn();
    }

    private void PutTrackPiece(Move move)
    {
        Expect(TurnPhase.TrackPiece);
        var route = RouteAt(move.Route);
        if (route.Colour != RouteColour.Blank || paintedPiece[route.Id] is not null || routeOwners[route.Id].Count > 0)
        {
            throw new RuleException("the route can't take a track piece");
        }

        if (IsCrossBlocked(route))
        {
            throw new RuleException("a route drawn across this one has been taken");
        }

        if (move.TrackPiece is not { } piece || piece < 0 || piece >= Map.TrackPieces.Count || !pieceInSupply[piece])
        {
            throw new RuleException("that track piece isn't in the supply");
        }

        if (Map.TrackPieces[piece].Length != route.Length)
        {
            throw new RuleException("the track piece is the wrong length");
        }

        pieceInSupply[piece] = false;
        paintedPiece[route.Id] = piece;
        EndTurn();
    }

    private void CompleteMainMove(bool draw, bool claim = false)
    {
        mainWasDraw = draw;
        mainWasClaim = claim || mainWasClaim;
        if (Map.Rules.Technologies)
        {
            Phase = TurnPhase.EndOfTurn;
        }
        else if (draw && Map.Rules.TrackPieces)
        {
            Phase = TurnPhase.TrackPiece;
        }
        else
        {
            EndTurn();
        }
    }

    private void EndTurn()
    {
        var empty = faceUp.Count(c => c is null);
        if (empty > 0)
        {
            ReshuffleCheck(empty);
            for (var slot = 0; slot < faceUp.Length; slot++)
            {
                if (faceUp[slot] is null && trainDeck.Count > 0)
                {
                    faceUp[slot] = DrawTrainCard();
                }
            }

            LocomotiveCheck();
        }

        TurnsTaken++;
        if (finalTurnsLeft is { } left)
        {
            finalTurnsLeft = left - 1;
        }
        else if (Current.Trains <= 2)
        {
            // Every player, this one included, has one more turn.
            finalTurnsLeft = players.Length;
        }

        draws = 0;
        pickedFaceUp = false;
        mainWasDraw = false;
        mainWasClaim = false;
        boughtTechnology = false;
        claimsThisTurn = 0;
        pendingRoute = null;
        CurrentPlayer = CurrentPlayer % players.Length + 1;
        Phase = finalTurnsLeft == 0 ? TurnPhase.Over : TurnPhase.Start;
    }

    // ----- Scoring -----------------------------------------------------------------------------------------------

    /// <summary>The scores as they stand (final once <see cref="IsOver"/>).</summary>
    public IReadOnlyList<PlayerScore> Score()
    {
        var inputs = players.Select(p => new ScoringInput(
            p.Seat,
            p.RouteList.ToList(),
            p.TicketList.ToList(),
            p.StationList.ToList(),
            p.StationsLeft,
            p.SharesList.ToList(),
            p.TechnologySet.ToList(),
            p.BoilerLaggingClaims,
            p.SteamTurbineFerries)).ToList();
        return Scorer.Score(Map, inputs);
    }

}
