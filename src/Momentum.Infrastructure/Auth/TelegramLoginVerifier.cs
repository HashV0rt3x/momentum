using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Momentum.Application.Auth;
using Momentum.Application.Common.Exceptions;

namespace Momentum.Infrastructure.Auth;

/// <summary>
/// Verifies both Telegram login flows against the bot token:
///   - Login Widget: secret = SHA256(botToken); hash = HMAC-SHA256(dataCheckString, secret).
///   - Mini App initData: secret = HMAC-SHA256(botToken, key="WebAppData"); same check.
/// The data-check-string is every received field except `hash`, sorted by key,
/// joined as "key=value" lines. Payloads older than 24h are rejected.
/// </summary>
public sealed class TelegramLoginVerifier(IOptions<TelegramOptions> options, TimeProvider timeProvider)
    : ITelegramLoginVerifier
{
    private static readonly TimeSpan MaxAuthAge = TimeSpan.FromHours(24);

    public TelegramUserData Verify(TelegramLoginRequest request)
    {
        var botToken = options.Value.BotToken;
        if (string.IsNullOrWhiteSpace(botToken))
        {
            throw new InvalidOperationException(
                "Missing configuration 'Telegram:BotToken' (env: Telegram__BotToken) — required for Telegram login.");
        }

        return request.Source switch
        {
            "widget" => VerifyWidget(request.Data!.Value, botToken),
            "webapp" => VerifyWebApp(request.InitData!, botToken),
            _ => throw new UnauthorizedException("Unsupported auth source.", "TELEGRAM_SIGNATURE_INVALID"),
        };
    }

    private TelegramUserData VerifyWidget(JsonElement data, string botToken)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        string? providedHash = null;

        foreach (var property in data.EnumerateObject())
        {
            if (property.Name == "hash")
            {
                providedHash = property.Value.GetString();
                continue;
            }

            fields[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                _ => property.Value.GetRawText(),
            };
        }

        // Widget flow: secret key is the plain SHA-256 of the bot token.
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        ValidateSignature(fields, providedHash, secretKey);
        ValidateFreshness(fields);

        return new TelegramUserData(
            TelegramId: ParseTelegramId(fields.GetValueOrDefault("id")),
            FirstName: fields.GetValueOrDefault("first_name") ?? string.Empty,
            LastName: fields.GetValueOrDefault("last_name"),
            Username: fields.GetValueOrDefault("username"),
            PhotoUrl: fields.GetValueOrDefault("photo_url"),
            LanguageCode: null);
    }

    private TelegramUserData VerifyWebApp(string initData, string botToken)
    {
        var parsed = QueryHelpers.ParseQuery(initData);

        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal);
        string? providedHash = null;

        foreach (var (key, values) in parsed)
        {
            if (key == "hash")
            {
                providedHash = values.ToString();
                continue;
            }

            fields[key] = values.ToString();
        }

        // Mini App flow: secret key is HMAC-SHA256 of the bot token with the
        // constant key "WebAppData" (note the reversed roles vs. the widget flow).
        using var secretHmac = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = secretHmac.ComputeHash(Encoding.UTF8.GetBytes(botToken));
        ValidateSignature(fields, providedHash, secretKey);
        ValidateFreshness(fields);

        if (!fields.TryGetValue("user", out var userJson))
        {
            throw new UnauthorizedException("initData is missing the 'user' field.", "TELEGRAM_SIGNATURE_INVALID");
        }

        using var document = JsonDocument.Parse(userJson);
        var user = document.RootElement;

        return new TelegramUserData(
            TelegramId: user.GetProperty("id").GetInt64(),
            FirstName: user.TryGetProperty("first_name", out var fn) ? fn.GetString() ?? string.Empty : string.Empty,
            LastName: user.TryGetProperty("last_name", out var ln) ? ln.GetString() : null,
            Username: user.TryGetProperty("username", out var un) ? un.GetString() : null,
            PhotoUrl: user.TryGetProperty("photo_url", out var pu) ? pu.GetString() : null,
            LanguageCode: user.TryGetProperty("language_code", out var lc) ? lc.GetString() : null);
    }

    private static void ValidateSignature(
        SortedDictionary<string, string> fields,
        string? providedHash,
        byte[] secretKey)
    {
        if (string.IsNullOrWhiteSpace(providedHash))
        {
            throw new UnauthorizedException("Missing Telegram hash.", "TELEGRAM_SIGNATURE_INVALID");
        }

        var dataCheckString = string.Join('\n', fields.Select(kv => $"{kv.Key}={kv.Value}"));

        using var hmac = new HMACSHA256(secretKey);
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));

        byte[] provided;
        try
        {
            provided = Convert.FromHexString(providedHash);
        }
        catch (FormatException)
        {
            throw new UnauthorizedException("Malformed Telegram hash.", "TELEGRAM_SIGNATURE_INVALID");
        }

        if (!CryptographicOperations.FixedTimeEquals(computed, provided))
        {
            throw new UnauthorizedException("Telegram signature verification failed.", "TELEGRAM_SIGNATURE_INVALID");
        }
    }

    private void ValidateFreshness(SortedDictionary<string, string> fields)
    {
        if (!fields.TryGetValue("auth_date", out var raw)
            || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authDateUnix))
        {
            throw new UnauthorizedException("Missing or malformed auth_date.", "TELEGRAM_SIGNATURE_INVALID");
        }

        var authDate = DateTimeOffset.FromUnixTimeSeconds(authDateUnix);
        if (timeProvider.GetUtcNow() - authDate > MaxAuthAge)
        {
            throw new UnauthorizedException("Telegram auth payload has expired.", "TELEGRAM_AUTH_EXPIRED");
        }
    }

    private static long ParseTelegramId(string? raw)
    {
        if (raw is null || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            throw new UnauthorizedException("Missing or malformed Telegram user id.", "TELEGRAM_SIGNATURE_INVALID");
        }

        return id;
    }
}
