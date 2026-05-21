namespace CabinBingo.Api.Models;

public sealed record GuestDto(string GuestId, string DisplayName, int SortOrder, bool ClaimedByOther);

public sealed record PutProfileRequest(string GuestId);

public sealed record ProfileResponse(string? GuestId, string? GuestDisplayName, bool OnboardingComplete);

public sealed record PreferenceOptionDto(string Value, string Label);

public sealed record PreferenceCatalogItemDto(
    string PreferenceId,
    string Question,
    string AnswerType,
    IReadOnlyList<PreferenceOptionDto> Options,
    int SortOrder);

public sealed record PutPreferencesRequest(IReadOnlyDictionary<string, IReadOnlyList<string>> Answers);

public sealed record BingoCellDto(string SlotId, string Text, bool IsFixedCenter, string? PreferenceLabel = null);

public sealed record BingoCardDto(IReadOnlyList<BingoCellDto> Cells);

public sealed record BingoCardsResponse(BingoCardDto Card1, BingoCardDto Card2);

public sealed record BingoStateResponse(
    string Seed,
    string GeneratedAt,
    BingoCardDto Card1,
    BingoCardDto Card2,
    IReadOnlyList<bool> MarkedCard1,
    IReadOnlyList<bool> MarkedCard2);

public sealed record PostBingoRequest(string? Seed, bool ReplaceExisting);

public sealed record PutBingoProgressRequest(
    IReadOnlyList<bool> MarkedCard1,
    IReadOnlyList<bool> MarkedCard2);

public sealed record AdminUserOverviewDto(
    string UserSub,
    bool HasProfile,
    string? GuestId,
    string? GuestDisplayName,
    bool HasBingoCards,
    string? BingoGeneratedAt,
    int Card1MarkedCount,
    int Card2MarkedCount,
    int Card1TotalCount,
    int Card2TotalCount);

public sealed record AdminUserBingoDetailResponse(
    string UserSub,
    string? GuestId,
    string? GuestDisplayName,
    string GeneratedAt,
    BingoCardDto Card1,
    BingoCardDto Card2,
    IReadOnlyList<bool> MarkedCard1,
    IReadOnlyList<bool> MarkedCard2);
