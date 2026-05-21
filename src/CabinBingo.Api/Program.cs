using System.Security.Claims;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.Extensions.NETCore.Setup;
using CabinBingo.Api.Models;
using CabinBingo.Api.Options;
using CabinBingo.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
var appOptions = builder.Configuration.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions();

builder.Services.AddSingleton(appOptions);
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Services.AddAWSService<IAmazonDynamoDB>();
builder.Services.AddSingleton<DynamoDataStore>();
builder.Services.AddSingleton<BingoService>();

var cognitoAuthority = appOptions.CognitoAuthority.TrimEnd('/');
var cognitoAudience = appOptions.CognitoAudience;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = cognitoAuthority;
        options.MetadataAddress = $"{cognitoAuthority}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cognitoAuthority,
            ValidateAudience = false
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var clientId = ctx.Principal?.FindFirst("client_id")?.Value;
                var aud = ctx.Principal?.FindFirst("aud")?.Value;
                if (!string.Equals(aud, cognitoAudience, StringComparison.Ordinal)
                    && !string.Equals(clientId, cognitoAudience, StringComparison.Ordinal))
                {
                    ctx.Fail("Invalid token audience / client_id for this API.");
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.MapGet("/guests", async (HttpContext http, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out var sub))
        return Results.Unauthorized();

    var guests = await store.ScanGuestsAsync(ct);
    var dtos = guests
        .Where(g => g.Active)
        .Select(g => new GuestDto(
            g.GuestId,
            g.DisplayName,
            g.SortOrder,
            ClaimedByOther: g.ClaimedBySub is not null && !g.ClaimedBySub.Equals(sub, StringComparison.Ordinal)))
        .Where(dto => !dto.ClaimedByOther)
        .ToArray();
    return Results.Ok(dtos);
}).RequireAuthorization();

app.MapGet("/profile", async (HttpContext http, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out var sub))
        return Results.Unauthorized();

    var profile = await store.GetProfileAsync(sub, ct);
    var onboardingComplete = profile is not null;
    return Results.Ok(new ProfileResponse(profile?.GuestId, profile?.GuestDisplayName, onboardingComplete));
}).RequireAuthorization();

app.MapPut("/profile", async (HttpContext http, PutProfileRequest body, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out var sub))
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(body.GuestId))
        return Results.BadRequest(new { message = "guestId is required." });

    try
    {
        await store.ClaimGuestAsync(sub, body.GuestId.Trim(), ct);
        return Results.NoContent();
    }
    catch (ClaimConflictException)
    {
        return Results.Conflict(new { message = "That cabin guest name is already claimed by someone else." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapGet("/preferences/catalog", async (DynamoDataStore store, CancellationToken ct) =>
{
    var rows = await store.ScanPreferenceCatalogAsync(ct);
    var dto = rows.Select(r => new PreferenceCatalogItemDto(
        r.PreferenceId,
        r.Question,
        r.AnswerType,
        r.Options.Select(o => new PreferenceOptionDto(o.Value, o.Label)).ToList(),
        r.SortOrder)).ToArray();
    return Results.Ok(dto);
}).RequireAuthorization();

app.MapGet("/preferences/me", async (HttpContext http, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out var sub))
        return Results.Unauthorized();

    var answers = await store.GetAnswersAsync(sub, ct);
    return Results.Ok(answers);
}).RequireAuthorization();

app.MapPut("/preferences/me", async (HttpContext http, PutPreferencesRequest body, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out var sub))
        return Results.Unauthorized();

    var catalog = await store.ScanPreferenceCatalogAsync(ct);
    var catalogIds = new HashSet<string>(catalog.Select(c => c.PreferenceId), StringComparer.OrdinalIgnoreCase);
    var filteredAnswers = body.Answers
        .Where(kvp => catalogIds.Contains(kvp.Key))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

    try
    {
        PreferenceAnswersValidator.ValidateOrThrow(filteredAnswers, catalog);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }

    await store.PutAnswersAsync(sub, filteredAnswers, ct);
    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/bingo/me", async (HttpContext http, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out var sub))
        return Results.Unauthorized();

    var state = await store.GetBingoStateAsync(sub, ct);
    return Results.Json(state?.ToResponse());
}).RequireAuthorization();

app.MapPost("/bingo/cards", async (HttpContext http, PostBingoRequest? body, DynamoDataStore store, BingoService bingo, CancellationToken ct) =>
{
    if (!TryGetSub(http, out var sub))
        return Results.Unauthorized();

    var profile = await store.GetProfileAsync(sub, ct);
    if (profile is null)
        return Results.BadRequest(new { message = "Complete your cabin guest profile before generating bingo cards." });

    var answers = await store.LoadAnswersStateAsync(sub, ct);
    var guests = await store.ScanGuestsAsync(ct);
    var existing = await store.GetBingoStateAsync(sub, ct);
    if (existing is not null && !(body?.ReplaceExisting ?? false))
        return Results.Conflict(new { message = "Generating new cards will replace your current cards and all marked progress." });

    try
    {
        var cards = bingo.BuildTwoCards(sub, profile.GuestId, answers, guests, body?.Seed);
        var state = new PersistedBingoState(
            body?.Seed ?? "",
            DateTimeOffset.UtcNow.ToString("O"),
            cards.Card1,
            cards.Card2,
            Enumerable.Repeat(false, cards.Card1.Cells.Count).ToArray(),
            Enumerable.Repeat(false, cards.Card2.Cells.Count).ToArray());
        await store.PutBingoStateAsync(sub, state, ct);
        return Results.Ok(state.ToResponse());
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
}).RequireAuthorization();

app.MapPut("/bingo/me", async (HttpContext http, PutBingoProgressRequest body, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out var sub))
        return Results.Unauthorized();

    if (body.MarkedCard1.Count != 25 || body.MarkedCard2.Count != 25)
        return Results.BadRequest(new { message = "Both marked card arrays must contain exactly 25 values." });

    var existing = await store.GetBingoStateAsync(sub, ct);
    if (existing is null)
        return Results.BadRequest(new { message = "Generate bingo cards before saving bingo progress." });

    var updated = existing with
    {
        MarkedCard1 = body.MarkedCard1.ToArray(),
        MarkedCard2 = body.MarkedCard2.ToArray()
    };

    await store.PutBingoStateAsync(sub, updated, ct);
    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/admin/overview", async (HttpContext http, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out _))
        return Results.Unauthorized();

    var overview = await store.ScanAdminOverviewAsync(ct);
    return Results.Ok(overview);
}).RequireAuthorization();

app.MapGet("/admin/users/{userSub}/bingo", async (HttpContext http, string userSub, DynamoDataStore store, CancellationToken ct) =>
{
    if (!TryGetSub(http, out _))
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(userSub))
        return Results.BadRequest(new { message = "userSub is required." });

    var bingo = await store.GetBingoStateAsync(userSub, ct);
    if (bingo is null)
        return Results.NotFound(new { message = "No active bingo cards found for that user." });

    var profile = await store.GetProfileAsync(userSub, ct);
    return Results.Ok(new AdminUserBingoDetailResponse(
        userSub,
        profile?.GuestId,
        profile?.GuestDisplayName,
        bingo.GeneratedAt,
        bingo.Card1,
        bingo.Card2,
        bingo.MarkedCard1,
        bingo.MarkedCard2));
}).RequireAuthorization();

app.Run();

static bool TryGetSub(HttpContext http, out string sub)
{
    sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
          ?? http.User.FindFirstValue("sub")
          ?? "";
    return !string.IsNullOrEmpty(sub);
}
