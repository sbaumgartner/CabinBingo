using System.Security.Cryptography;
using System.Text;
using CabinBingo.Api.Models;

namespace CabinBingo.Api.Services;

public sealed class BingoService
{
    public const string CenterSlotId = "CENTER_FIXED";
    public const string CenterText = "Give a fellow cabin member a hug";
    private const int CardNonCenterCellCount = 24;
    private const int SharedDuplicateSquaresAcrossCards = 5;
    private const int UniqueSquaresPerCard = CardNonCenterCellCount - SharedDuplicateSquaresAcrossCards;
    private const int MaxDuplicateSquaresAcrossCards = 5;
    private const string ClocktowerLabel = "Blood on the Clocktower";
    private const string HikeLabel = "on a hike";
    private const string HotTubLabel = "hot tub";
    private const string MtgLabel = "MTG";
    private const string DrinkLabel = "drinking";

    private static readonly BingoSlotDefinition[] Slots =
    [
        new("clocktower_vote_death", "Be killed by the town (vote)", s => UserAnswersState.IsYes(s.Clocktower), ClocktowerLabel),
        new("clocktower_ability_death", "Be killed by an ability", s => UserAnswersState.IsYes(s.Clocktower), ClocktowerLabel),
        new("clocktower_dead_win", "Win a game after being killed", s => UserAnswersState.IsYes(s.Clocktower), ClocktowerLabel),
        new("clocktower_survive_end", "Survive until the end of a game", s => UserAnswersState.IsYes(s.Clocktower), ClocktowerLabel),
        new("hike_group", "Go on a hike with at least 3 people", s => UserAnswersState.IsYes(s.Hike), HikeLabel),
        new("hottub_five", "Be in a hot tub with at least 5 other people at one time", s => UserAnswersState.IsYes(s.HotTub), HotTubLabel),
        new("mtg_eight", "Cast a spell costing at least 8 mana", s => UserAnswersState.IsYes(s.Mtg), MtgLabel),
        new("mtg_four_spells", "Cast 4 spells in one turn", s => UserAnswersState.IsYes(s.Mtg), MtgLabel),
        new("mtg_draw_eight", "Draw 8+ cards in one turn", s => UserAnswersState.IsYes(s.Mtg), MtgLabel),
        new("mtg_low_deck", "Have less than 5 cards left in your deck", s => UserAnswersState.IsYes(s.Mtg), MtgLabel),
        new("toast_ten", "Make a toast with at least 10 people present", s => UserAnswersState.IsYes(s.Drink), DrinkLabel),
        new("drink_made_by_other", "Have a drink made by someone else", s => UserAnswersState.IsYes(s.Drink), DrinkLabel),
        new("win_game", "Win a game", _ => true),
        new("learn_game", "Learn a new game", _ => true),
        new("teach_game", "Teach someone a new game", _ => true),
        new("group_photo", "Get a group photo with at least 8 people", _ => true),
        new("board_marathon", "Play a board game for at least 2 hours", _ => true),
        new("board_variety", "Play three different board or card games in one day", _ => true),
        new("clean_kitchen", "Help clean the kitchen after a meal", _ => true),
        new("outside_thirty", "Spend 30 minutes outside", _ => true),
        new("deck_board_game", "Play a board game on the deck", _ => true),
        new("post_picture_memories", "Take a picture and post it in cabin-2026-pics-and-memories", _ => true),
        new("post_quote_memories", "Post a quote to cabin-2026-pics-and-memories", _ => true),
        new("drafting_game", "Play a game with drafting in it", _ => true),
        new("bidding_game", "Play a game with bidding in it", _ => true),
        new("close_game", "Win or lose a close game (within 5% points)", _ => true),
        new("share_space_game", "Share a space in a game with another player", _ => true),
        new("worker_placement_game", "Play a worker placement game", _ => true),
        new("score_sixty_nine", "Have a score of 69 at some point during a game", _ => true),
        new("puzzle", "Finish a 100+ piece puzzle as a group", _ => true),
        new("puzzle_table", "Spend 10 minutes at the puzzle table", _ => true),
        new("could_have_won", "Hear someone talking about how they \"could have won if...\"", _ => true),
        new("pick_a_game", "Make a decision about what game to play with a group", _ => true),
        new("game_replay", "Play the same game multiple times", _ => true),
        new("six_seven_joke", "Hear someone make a 6-7 joke", _ => true),
        new("called_beautiful_or_cute", "Be called \"Beautiful\" or \"Cute\" by an opponent during a game", _ => true),
        new("cabin_person_game", "Play a game with someone you only ever see at cabin", _ => true),
        new("hike_photos", "Take photos during a hike with a group", s => UserAnswersState.IsYes(s.Hike), HikeLabel),
        new("hot_tub_twice", "Hot tub twice within 24 hours", s => UserAnswersState.IsYes(s.HotTub), HotTubLabel),
        new("mtg_mana_screwed", "Hear someone complain about getting mana screwed", s => UserAnswersState.IsYes(s.Mtg), MtgLabel),
        new("mtg_blowout", "Get blown out by a timely counter spell or removal", s => UserAnswersState.IsYes(s.Mtg), MtgLabel),
        new("drinks_mix_three", "Mix up drinks for at least 3 other people", s => UserAnswersState.IsYes(s.Drink), DrinkLabel),
    ];

    public BingoCardsResponse BuildTwoCards(
        string userSub,
        string currentGuestId,
        UserAnswersState answers,
        IReadOnlyList<GuestRow> guests,
        string? seedSuffix)
    {
        var eligible = Slots.Where(s => s.IsEligible(answers)).ToList();
        var attendeeSlots = BuildAttendeeSlots(currentGuestId, guests);
        if (eligible.Count + attendeeSlots.Count < CardNonCenterCellCount)
        {
            throw new InvalidOperationException(
                $"Not enough eligible bingo slots ({eligible.Count}) plus attendee squares ({attendeeSlots.Count}). Add more generic slots or more guests.");
        }

        var totalUniqueSlots = eligible.Count + attendeeSlots.Count;
        var minimumRequiredUniqueSlots = (CardNonCenterCellCount * 2) - MaxDuplicateSquaresAcrossCards;
        if (totalUniqueSlots < minimumRequiredUniqueSlots)
        {
            throw new InvalidOperationException(
                $"Not enough unique bingo slots ({totalUniqueSlots}) to build two cards with at most {MaxDuplicateSquaresAcrossCards} duplicate squares. Add more generic slots or more guests.");
        }

        var seed = ComputeSeed(userSub, seedSuffix);
        var poolRng = new Random(seed);
        var card1Rng = new Random(seed ^ 0x13572468);
        var card2Rng = new Random(seed ^ 0x5EED1234);

        var allSlots = eligible
            .Concat(attendeeSlots)
            .ToList();
        Shuffle(allSlots, poolRng);

        var selectedUniqueSlots = allSlots
            .Take((CardNonCenterCellCount * 2) - SharedDuplicateSquaresAcrossCards)
            .ToList();

        var sharedSlots = selectedUniqueSlots
            .Take(SharedDuplicateSquaresAcrossCards)
            .ToList();
        var remainingSlots = selectedUniqueSlots
            .Skip(SharedDuplicateSquaresAcrossCards)
            .ToList();

        var card1Unique = new List<BingoSlotDefinition>(UniqueSquaresPerCard);
        var card2Unique = new List<BingoSlotDefinition>(UniqueSquaresPerCard);

        var attendeeRemaining = remainingSlots.Where(s => s.IsAttendee).ToList();
        var standardRemaining = remainingSlots.Where(s => !s.IsAttendee).ToList();
        Shuffle(attendeeRemaining, poolRng);
        Shuffle(standardRemaining, poolRng);

        DealBalanced(attendeeRemaining, card1Unique, card2Unique, poolRng);
        DealBalanced(standardRemaining, card1Unique, card2Unique, poolRng);

        if (card1Unique.Count != UniqueSquaresPerCard || card2Unique.Count != UniqueSquaresPerCard)
        {
            throw new InvalidOperationException("Unable to distribute bingo squares evenly across both cards.");
        }

        var card1Slots = sharedSlots.Concat(card1Unique).ToList();
        var card2Slots = sharedSlots.Concat(card2Unique).ToList();
        Shuffle(card1Slots, card1Rng);
        Shuffle(card2Slots, card2Rng);

        return new BingoCardsResponse(
            BuildCard(card1Slots),
            BuildCard(card2Slots));
    }

    private static void DealBalanced(
        IReadOnlyList<BingoSlotDefinition> pool,
        List<BingoSlotDefinition> card1Unique,
        List<BingoSlotDefinition> card2Unique,
        Random rng)
    {
        var dealToCard1First = rng.Next(2) == 0;
        foreach (var slot in pool)
        {
            if (card1Unique.Count >= UniqueSquaresPerCard && card2Unique.Count >= UniqueSquaresPerCard)
            {
                return;
            }

            if (dealToCard1First)
            {
                if (card1Unique.Count < UniqueSquaresPerCard)
                {
                    card1Unique.Add(slot);
                }
                else if (card2Unique.Count < UniqueSquaresPerCard)
                {
                    card2Unique.Add(slot);
                }
            }
            else
            {
                if (card2Unique.Count < UniqueSquaresPerCard)
                {
                    card2Unique.Add(slot);
                }
                else if (card1Unique.Count < UniqueSquaresPerCard)
                {
                    card1Unique.Add(slot);
                }
            }

            dealToCard1First = !dealToCard1First;
        }
    }

    private static BingoCardDto BuildCard(IReadOnlyList<BingoSlotDefinition> chosen)
    {
        var cells = new BingoCellDto[25];
        var idx = 0;
        for (var i = 0; i < 25; i++)
        {
            if (i == 12)
            {
                cells[i] = new BingoCellDto(CenterSlotId, CenterText, IsFixedCenter: true);
                continue;
            }

            var slot = chosen[idx++];
            cells[i] = new BingoCellDto(slot.Id, slot.Text, IsFixedCenter: false, slot.PreferenceLabel);
        }

        return new BingoCardDto(cells);
    }

    private static List<BingoSlotDefinition> BuildAttendeeSlots(string currentGuestId, IReadOnlyList<GuestRow> guests)
    {
        return guests
            .Where(g => g.Active && !string.Equals(g.GuestId, currentGuestId, StringComparison.Ordinal))
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.DisplayName, StringComparer.Ordinal)
            .Select(g => new BingoSlotDefinition(
                $"play_with_{g.GuestId}",
                $"Play a game with {g.DisplayName}",
                _ => true,
                null,
                true))
            .ToList();
    }

    private static int ComputeSeed(string userSub, string? seedSuffix)
    {
        var payload = $"{userSub}|{seedSuffix ?? ""}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return BitConverter.ToInt32(hash, 0);
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private sealed record BingoSlotDefinition(
        string Id,
        string Text,
        Func<UserAnswersState, bool> IsEligible,
        string? PreferenceLabel = null,
        bool IsAttendee = false);
}
