using AudioProvider;

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

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = false;
    cts.Cancel();
};

var bot = new TelegramBotService(token, allowedUserId, cts.Token);
await bot.Run();
