using System.Runtime.InteropServices;
using AudioProvider;
using Telegram.Bot;

string? token = Environment.GetEnvironmentVariable("TELEGRAM_API_TOKEN");
if (string.IsNullOrEmpty(token))
{
    throw new ArgumentException("TELEGRAM_API_TOKEN env variable is not set correctly!");
}

string? allowedUserIdValue = Environment.GetEnvironmentVariable("ALLOWED_USER_ID");
if (!long.TryParse(allowedUserIdValue, out long allowedUserId))
{
    throw new ArgumentException("ALLOWED_USER_ID env variable is not set correctly!");
}

using var shutdownCts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdownCts.Cancel();
};

using var sigtermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, (ctx) =>
{
    ctx.Cancel = true;
    shutdownCts.Cancel();
});

using var sigIntRegistration = PosixSignalRegistration.Create(PosixSignal.SIGINT, (ctx) => {
    ctx.Cancel = true;
    shutdownCts.Cancel();
});

using var telegramCts = new CancellationTokenSource();
var client = new TelegramBotClient(token, cancellationToken: telegramCts.Token);
var bot = new TelegramBotService(client, allowedUserId, telegramCts.Token);

try
{
    await bot.Run(shutdownCts.Token);
}
finally
{
    telegramCts.Cancel();
}
