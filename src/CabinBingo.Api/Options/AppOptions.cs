namespace CabinBingo.Api.Options;

public sealed class AppOptions
{
    public const string SectionName = "CabinBingo";

    public string CabinGuestsTable { get; set; } = "";
    public string PreferenceCatalogTable { get; set; } = "";
    public string UserDataTable { get; set; } = "";
    public string CognitoAuthority { get; set; } = "";
    public string CognitoAudience { get; set; } = "";
}
