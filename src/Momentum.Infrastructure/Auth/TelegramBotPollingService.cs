using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Momentum.Domain.Auth;
using Momentum.Infrastructure.Persistence;

namespace Momentum.Infrastructure.Auth;

/// <summary>
/// Long-polls the Telegram Bot API (getUpdates) and answers /start with a
/// one-time 6-digit login code, which POST /auth/telegram/code exchanges for a
/// token pair. Polling needs no public URL — it works behind NAT/firewalls,
/// unlike webhooks. No-op when Telegram__BotToken is empty or
/// Telegram__EnablePolling=false (tests).
/// </summary>
public sealed class TelegramBotPollingService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IOptions<TelegramOptions> options,
    TimeProvider timeProvider,
    ILogger<TelegramBotPollingService> logger) : BackgroundService
{
    private const int PollTimeoutSeconds = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.EnablePolling || string.IsNullOrWhiteSpace(settings.BotToken))
        {
            logger.LogInformation("Telegram bot polling disabled (no token or EnablePolling=false).");
            return;
        }

        logger.LogInformation("Telegram bot polling started.");

        long offset = 0;
        var http = httpClientFactory.CreateClient(nameof(TelegramBotPollingService));
        http.Timeout = TimeSpan.FromSeconds(PollTimeoutSeconds + 15);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var url = $"https://api.telegram.org/bot{settings.BotToken}/getUpdates" +
                          $"?timeout={PollTimeoutSeconds}&offset={offset}&allowed_updates=%5B%22message%22%5D";

                using var response = await http.GetAsync(url, stoppingToken);
                var payload = await response.Content.ReadAsStringAsync(stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning(
                        "Telegram getUpdates returned {StatusCode}: {Body}. Retrying in 10s.",
                        (int)response.StatusCode, payload);
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    continue;
                }

                using var document = JsonDocument.Parse(payload);
                if (!document.RootElement.TryGetProperty("result", out var updates))
                {
                    continue;
                }

                foreach (var update in updates.EnumerateArray())
                {
                    offset = Math.Max(offset, update.GetProperty("update_id").GetInt64() + 1);
                    await HandleUpdateAsync(http, settings, update, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Telegram polling iteration failed. Retrying in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleUpdateAsync(
        HttpClient http,
        TelegramOptions settings,
        JsonElement update,
        CancellationToken cancellationToken)
    {
        if (!update.TryGetProperty("message", out var message)
            || !message.TryGetProperty("text", out var textElement)
            || !message.TryGetProperty("from", out var from))
        {
            return;
        }

        var text = textElement.GetString() ?? string.Empty;
        if (!text.StartsWith("/start", StringComparison.Ordinal))
        {
            return;
        }

        var telegramId = from.GetProperty("id").GetInt64();
        var firstName = from.TryGetProperty("first_name", out var fn) ? fn.GetString() ?? "User" : "User";
        var lastName = from.TryGetProperty("last_name", out var ln) ? ln.GetString() : null;
        var username = from.TryGetProperty("username", out var un) ? un.GetString() : null;
        var languageCode = from.TryGetProperty("language_code", out var lc) ? lc.GetString() : null;
        var chatId = message.GetProperty("chat").GetProperty("id").GetInt64();

        // Cryptographically random, uniform 6-digit code.
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var now = timeProvider.GetUtcNow();

        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.TelegramLoginCodes.Add(new TelegramLoginCode(
                code, telegramId, firstName, lastName, username, languageCode,
                now.AddMinutes(settings.LoginCodeMinutes)));
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var reply = BuildCodeMessage(code, languageCode, settings.LoginCodeMinutes);

        using var sendResponse = await http.PostAsync(
            $"https://api.telegram.org/bot{settings.BotToken}/sendMessage",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId.ToString(),
                ["text"] = reply,
            }),
            cancellationToken);

        if (!sendResponse.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Failed to send login code to Telegram chat {ChatId}: {StatusCode}",
                chatId, (int)sendResponse.StatusCode);
        }

        logger.LogInformation("Issued login code for Telegram user {TelegramId}.", telegramId);
    }

    private static string BuildCodeMessage(string code, string? languageCode, int minutes) => languageCode switch
    {
        "ru" => $"Ваш код для входа: {code}\nКод действителен {minutes} минут.",
        "en" => $"Your login code: {code}\nThe code is valid for {minutes} minutes.",
        _ => $"Kirish kodingiz: {code}\nKod {minutes} daqiqa amal qiladi.",
    };
}
