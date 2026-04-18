using System.Security.Cryptography;
using System.Text;
using CabinBingo.Api.Models;

namespace CabinBingo.Api.Services;

public sealed class BingoService
{
    public const string CenterSlotId = "CENTER_FIXED";
    public const string CenterText = "Give a fello cabin member a hug";

    private static readonly BingoSlotDefinition[] Slots =
    [
        new("hike_group", "Go on a hike with at least 3 people", s => UserAnswersState.IsYes(s.Hike)),
        new("hottub_five", "Be in a hot tub with at least 5 other people at one time", s => UserAnswersState.IsYes(s.HotTub)),
        new("mtg_eight", "Cast a spell costing at least 8 mana", s => UserAnswersState.IsYes(s.Mtg)),
        new("toast_ten", "Make a toast with at least 10 people present", s => UserAnswersState.IsYes(s.Drink)),
        new("win_game", "Win a game", _ => true),
        new("learn_game", "Learn a new game", _ => true),
        new("teach_game", "Teach someone a new game", _ => true),
        new("group_photo", "Get a group photo with at least 8 people", _ => true),
        new("stargaze", "Stargaze for 15 minutes with at least one other person", _ => true),
        new("cook_together", "Cook or prep a meal with at least 2 other people", _ => true),
        new("board_heavy", "Play a heavy board game to completion", s => s.BoardStyles.Contains("Heavy")),
        new("board_light", "Play three different light games in one day", s => s.BoardStyles.Contains("Light")),
        new("board_medium", "Play a medium-weight game you have not played before", s => s.BoardStyles.Contains("Medium")),
        new("coffee_morning", "Share a morning coffee or tea with someone you rarely talk to", _ => true),
        new("story_time", "Tell a 3-minute story to the group", _ => true),
        new("playlist", "Add 5 songs to a shared cabin playlist", _ => true),
        new("clean_kitchen", "Help clean the kitchen after a meal", _ => true),
        new("firepit", "Spend 30 minutes at a fire pit or outdoor hangout", _ => true),
        new("card_trick", "Learn and perform a simple card trick", _ => true),
        new("puzzle", "Finish a 100+ piece puzzle as a group", _ => true),
        new("snack_run", "Organize a snack run for the group", _ => true),
        new("nap_approved", "Take a guilt-free 20-minute nap", _ => true),
        new("sunrise", "Catch a sunrise from outside the cabin", _ => true),
        new("laugh_contagious", "Get the room laughing with a single joke", _ => true),
        new("compliment_chain", "Start a compliment chain with at least 5 people", _ => true),
        new("hike_short", "Take a short walk outside with at least one other person", s => UserAnswersState.IsYes(s.Hike)),
        new("hot_tub_chat", "Have a 10-minute conversation in the hot tub", s => UserAnswersState.IsYes(s.HotTub)),
        new("mtg_trade", "Trade at least one Magic card with someone", s => UserAnswersState.IsYes(s.Mtg)),
        new("drinks_mocktail", "Make a fun mocktail or cocktail for the group", s => UserAnswersState.IsYes(s.Drink)),
    ];

    public BingoCardsResponse BuildTwoCards(string userSub, UserAnswersState answers, string? seedSuffix)
    {
        var eligible = Slots.Where(s => s.IsEligible(answers)).ToList();
        if (eligible.Count < 24)
            throw new InvalidOperationException(
                $"Not enough eligible bingo slots ({eligible.Count}). Add more generic slots or adjust answers.");

        var seed = ComputeSeed(userSub, seedSuffix);
        var rng1 = new Random(seed);
        var rng2 = new Random(seed ^ 0x5EED1234);

        return new BingoCardsResponse(
            BuildCard(eligible, rng1),
            BuildCard(eligible, rng2));
    }

    private static BingoCardDto BuildCard(List<BingoSlotDefinition> eligible, Random rng)
    {
        var pool = eligible.ToList();
        Shuffle(pool, rng);
        var chosen = pool.Take(24).ToList();

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
            cells[i] = new BingoCellDto(slot.Id, slot.Text, IsFixedCenter: false);
        }

        return new BingoCardDto(cells);
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

    private sealed record BingoSlotDefinition(string Id, string Text, Func<UserAnswersState, bool> IsEligible);
}
