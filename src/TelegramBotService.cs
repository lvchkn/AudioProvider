using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AudioProvider;

public class TelegramBotService
{
    private readonly TelegramBotClient _bot;
    private readonly long _allowedUserId;
    private readonly SemaphoreSlim _downloadSemaphore = new(1, 1);
    private readonly CancellationToken _cancellationToken;

    public TelegramBotService(string token, long allowedUserId, CancellationToken cancellationToken)
    {
        _bot = new TelegramBotClient(token, cancellationToken: cancellationToken);
        _allowedUserId = allowedUserId;
        _cancellationToken = cancellationToken;
        _bot.OnMessage += HandleMessage;
        _bot.OnError += HandleError;
    }

    public async Task Run()
    {
        Console.WriteLine("Bot is running... Press Enter to terminate");
        await Task.Delay(Timeout.Infinite);
    }

    private async Task HandleMessage(Message message, UpdateType updateType)
    {
        if (message.From?.Id != _allowedUserId)
        {
            Console.WriteLine("Invocation not allowed");
            return;
        }

        if (message.Text == "/start")
        {
            await _bot.SendMessage(message.Chat, "Welcome! Send a link to audio");
            return;
        }

        bool isYTLink = message.Text?.StartsWith("https://www.youtube.com/watch?") ?? false;

        if (isYTLink)
        {
            long chatId = message.Chat.Id;
            Message downloadMessage = await _bot.SendMessage(chatId, "Downloading...");

            string outputDir = Path.Combine(Path.GetTempPath(), "audio-provider-bot", Guid.NewGuid().ToString());
            Directory.CreateDirectory(outputDir);
            bool isSemaphoreAcquired = false;

            try
            {
                await _downloadSemaphore.WaitAsync();
                isSemaphoreAcquired = true;

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

                string audio = await AudioService.GetYTAudioFilePath(message.Text!, outputDir, timeoutCts.Token);
                string caption = Path.GetFileName(audio);

                Console.WriteLine(audio);
                Console.WriteLine(caption);

                await using FileStream fs = File.OpenRead(audio);
                await _bot.SendAudio(chatId, fs);
                await _bot.DeleteMessage(chatId, downloadMessage.Id);
            }
            catch (Exception ex)
            {
                PrintException(ex);
                await _bot.EditMessageText(chatId, downloadMessage.Id, "Download failed!");
            }
            finally
            {
                if (isSemaphoreAcquired) _downloadSemaphore.Release();

                Directory.Delete(outputDir, true);
            }
        }
    }

    private async Task HandleError(Exception exception, HandleErrorSource source)
    {
        PrintException(exception);
    }

    private static void PrintException(Exception exception)
    {
        Console.ForegroundColor = ConsoleColor.DarkRed;
        Console.WriteLine(exception);
        Console.ResetColor();
    }
}
