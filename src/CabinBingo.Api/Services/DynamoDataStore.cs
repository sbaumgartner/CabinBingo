using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using CabinBingo.Api.Options;

namespace CabinBingo.Api.Services;

public sealed class DynamoDataStore
{
    private readonly IAmazonDynamoDB _ddb;
    private readonly AppOptions _opt;

    public DynamoDataStore(IAmazonDynamoDB ddb, AppOptions opt)
    {
        _ddb = ddb;
        _opt = opt;
    }

    private static string UserPk(string sub) => $"USER#{sub}";
    private const string ProfileSk = "PROFILE";

    public async Task<IReadOnlyList<GuestRow>> ScanGuestsAsync(CancellationToken ct)
    {
        var resp = await _ddb.ScanAsync(new ScanRequest
        {
            TableName = _opt.CabinGuestsTable,
            FilterExpression = "active = :t",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":t"] = new AttributeValue { BOOL = true }
            }
        }, ct);

        return resp.Items.Select(GuestRow.FromItem).Where(g => g is not null).Cast<GuestRow>().OrderBy(g => g.SortOrder).ToList();
    }

    public async Task<GuestRow?> GetGuestAsync(string guestId, CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _opt.CabinGuestsTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["guestId"] = new AttributeValue { S = guestId }
            },
            ConsistentRead = true
        }, ct);

        return resp.Item.Count == 0 ? null : GuestRow.FromItem(resp.Item);
    }

    public async Task<ProfileRow?> GetProfileAsync(string sub, CancellationToken ct)
    {
        var resp = await _ddb.GetItemAsync(new GetItemRequest
        {
            TableName = _opt.UserDataTable,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = UserPk(sub) },
                ["SK"] = new AttributeValue { S = ProfileSk }
            },
            ConsistentRead = true
        }, ct);

        return resp.Item.Count == 0 ? null : ProfileRow.FromItem(resp.Item);
    }

    public async Task ClaimGuestAsync(string sub, string newGuestId, CancellationToken ct)
    {
        var guest = await GetGuestAsync(newGuestId, ct);
        if (guest is null || !guest.Active)
            throw new InvalidOperationException("Guest not found or inactive.");

        if (guest.ClaimedBySub is { } claimer && !claimer.Equals(sub, StringComparison.Ordinal))
            throw new ClaimConflictException();

        var profile = await GetProfileAsync(sub, ct);
        var oldGuestId = profile?.GuestId;

        if (string.Equals(oldGuestId, newGuestId, StringComparison.Ordinal))
        {
            // Ensure claim row is consistent with this user.
            if (!string.Equals(guest.ClaimedBySub, sub, StringComparison.Ordinal))
            {
                await _ddb.UpdateItemAsync(new UpdateItemRequest
                {
                    TableName = _opt.CabinGuestsTable,
                    Key = new Dictionary<string, AttributeValue> { ["guestId"] = new AttributeValue { S = newGuestId } },
                    UpdateExpression = "SET claimedBySub = :s, claimedAt = :t",
                    ConditionExpression = "attribute_not_exists(claimedBySub) OR claimedBySub = :s",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":s"] = new AttributeValue { S = sub },
                        [":t"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
                    }
                }, ct);
            }

            await PutProfileItemAsync(sub, newGuestId, guest.DisplayName, ct);
            return;
        }

        var transact = new List<TransactWriteItem>();

        if (!string.IsNullOrEmpty(oldGuestId) && !string.Equals(oldGuestId, newGuestId, StringComparison.Ordinal))
        {
            transact.Add(new TransactWriteItem
            {
                Update = new Update
                {
                    TableName = _opt.CabinGuestsTable,
                    Key = new Dictionary<string, AttributeValue> { ["guestId"] = new AttributeValue { S = oldGuestId } },
                    UpdateExpression = "REMOVE claimedBySub, claimedAt",
                    ConditionExpression = "claimedBySub = :s",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        [":s"] = new AttributeValue { S = sub }
                    }
                }
            });
        }

        transact.Add(new TransactWriteItem
        {
            Update = new Update
            {
                TableName = _opt.CabinGuestsTable,
                Key = new Dictionary<string, AttributeValue> { ["guestId"] = new AttributeValue { S = newGuestId } },
                UpdateExpression = "SET claimedBySub = :s, claimedAt = :t",
                ConditionExpression = "attribute_not_exists(claimedBySub) OR claimedBySub = :s",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":s"] = new AttributeValue { S = sub },
                    [":t"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
                }
            }
        });

        transact.Add(new TransactWriteItem
        {
            Put = new Put
            {
                TableName = _opt.UserDataTable,
                Item = BuildProfileItem(sub, newGuestId, guest.DisplayName)
            }
        });

        try
        {
            await _ddb.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems = transact,
                ClientRequestToken = $"{sub}:{newGuestId}:{Guid.NewGuid():N}"
            }, ct);
        }
        catch (TransactionCanceledException)
        {
            throw new ClaimConflictException();
        }
    }

    private async Task PutProfileItemAsync(string sub, string guestId, string displayName, CancellationToken ct)
    {
        await _ddb.PutItemAsync(new PutItemRequest
        {
            TableName = _opt.UserDataTable,
            Item = BuildProfileItem(sub, guestId, displayName)
        }, ct);
    }

    private static Dictionary<string, AttributeValue> BuildProfileItem(string sub, string guestId, string displayName) =>
        new()
        {
            ["PK"] = new AttributeValue { S = UserPk(sub) },
            ["SK"] = new AttributeValue { S = ProfileSk },
            ["guestId"] = new AttributeValue { S = guestId },
            ["guestDisplayName"] = new AttributeValue { S = displayName },
            ["updatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
        };

    public async Task<IReadOnlyList<PreferenceCatalogRow>> ScanPreferenceCatalogAsync(CancellationToken ct)
    {
        var resp = await _ddb.ScanAsync(new ScanRequest
        {
            TableName = _opt.PreferenceCatalogTable,
            FilterExpression = "active = :t",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":t"] = new AttributeValue { BOOL = true }
            }
        }, ct);

        return resp.Items.Select(PreferenceCatalogRow.FromItem).Where(p => p is not null).Cast<PreferenceCatalogRow>()
            .OrderBy(p => p.SortOrder).ToList();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetAnswersAsync(string sub, CancellationToken ct)
    {
        var resp = await _ddb.QueryAsync(new QueryRequest
        {
            TableName = _opt.UserDataTable,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :prefix)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = UserPk(sub) },
                [":prefix"] = new AttributeValue { S = "ANSWER#" }
            },
            ConsistentRead = true
        }, ct);

        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in resp.Items)
        {
            if (!item.TryGetValue("SK", out var sk)) continue;
            var skv = sk.S;
            if (!skv.StartsWith("ANSWER#", StringComparison.Ordinal)) continue;
            var prefId = skv["ANSWER#".Length..];
            if (!item.TryGetValue("values", out var values)) continue;
            dict[prefId] = values.L.Select(v => v.S).ToList();
        }

        return dict.ToDictionary(k => k.Key, IReadOnlyList<string> (v) => v.Value);
    }

    public async Task PutAnswersAsync(string sub, IReadOnlyDictionary<string, IReadOnlyList<string>> answers, CancellationToken ct)
    {
        // Simple approach: write each answer as its own Put (no transaction across many - acceptable for v1)
        foreach (var (prefId, values) in answers)
        {
            await _ddb.PutItemAsync(new PutItemRequest
            {
                TableName = _opt.UserDataTable,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["PK"] = new AttributeValue { S = UserPk(sub) },
                    ["SK"] = new AttributeValue { S = $"ANSWER#{prefId}" },
                    ["values"] = new AttributeValue { L = values.Select(v => new AttributeValue { S = v }).ToList() },
                    ["updatedAt"] = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") }
                }
            }, ct);
        }
    }

    public async Task<UserAnswersState> LoadAnswersStateAsync(string sub, CancellationToken ct)
    {
        var raw = await GetAnswersAsync(sub, ct);
        var s = new UserAnswersState();
        if (raw.TryGetValue("drink", out var d) && d.Count > 0) s.Drink = d[0];
        if (raw.TryGetValue("mtg", out var m) && m.Count > 0) s.Mtg = m[0];
        if (raw.TryGetValue("hot_tub", out var h) && h.Count > 0) s.HotTub = h[0];
        if (raw.TryGetValue("hike", out var hi) && hi.Count > 0) s.Hike = hi[0];
        if (raw.TryGetValue("board_style", out var bs))
        {
            foreach (var x in bs) s.BoardStyles.Add(x);
        }

        return s;
    }
}

public sealed class ClaimConflictException : Exception;

public sealed record GuestRow(string GuestId, string DisplayName, int SortOrder, bool Active, string? ClaimedBySub)
{
    public static GuestRow? FromItem(Dictionary<string, AttributeValue> item)
    {
        if (!item.TryGetValue("guestId", out var id)) return null;
        var display = item.TryGetValue("displayName", out var dn) ? dn.S : id.S;
        var sort = item.TryGetValue("sortOrder", out var so) && int.TryParse(so.N, out var n) ? n : 0;
        var active = !item.TryGetValue("active", out var ac) || (ac.BOOL ?? true);
        var claimed = item.TryGetValue("claimedBySub", out var cl) ? cl.S : null;
        return new GuestRow(id.S, display, sort, active, claimed);
    }
}

public sealed record ProfileRow(string GuestId, string GuestDisplayName)
{
    public static ProfileRow? FromItem(Dictionary<string, AttributeValue> item)
    {
        if (!item.TryGetValue("guestId", out var gid)) return null;
        var name = item.TryGetValue("guestDisplayName", out var gdn) ? gdn.S : gid.S;
        return new ProfileRow(gid.S, name);
    }
}

public sealed record PreferenceCatalogRow(
    string PreferenceId,
    string Question,
    string AnswerType,
    IReadOnlyList<(string Value, string Label)> Options,
    int SortOrder)
{
    public static PreferenceCatalogRow? FromItem(Dictionary<string, AttributeValue> item)
    {
        if (!item.TryGetValue("preferenceId", out var pid)) return null;
        var q = item.TryGetValue("question", out var qq) ? qq.S : "";
        var at = item.TryGetValue("answerType", out var atv) ? atv.S : "single";
        var sort = item.TryGetValue("sortOrder", out var so) && int.TryParse(so.N, out var n) ? n : 0;
        var options = new List<(string, string)>();
        if (item.TryGetValue("options", out var opts) && opts.L is { } list)
        {
            foreach (var o in list)
            {
                if (o.M is null) continue;
                var v = o.M.TryGetValue("value", out var vv) ? vv.S : "";
                var l = o.M.TryGetValue("label", out var ll) ? ll.S : v;
                options.Add((v, l));
            }
        }

        return new PreferenceCatalogRow(pid.S, q, at, options, sort);
    }
}
