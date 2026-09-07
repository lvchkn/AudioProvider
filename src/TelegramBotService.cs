using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Threading.Channels;

namespace AudioProvider;

public class TelegramBotService
{
    private readonly TelegramBotClient _bot;
    private readonly long _allowedUserId;
    private readonly Channel<DownloadRequest> _channel = Channel.CreateBounded<DownloadRequest>(
        new BoundedChannelOptions(5)
        {
            FullMode = BoundedChannelFullMode.Wait,
        }
    );
    private readonly CancellationToken _telegramCancellationToken;

    public TelegramBotService(TelegramBotClient bot, long allowedUserId, CancellationToken telegramCancellationToken)
    {
        _bot = bot;
        _bot.OnMessage += HandleMessage;
        _bot.OnError += HandleError;
        _allowedUserId = allowedUserId;
        _telegramCancellationToken = telegramCancellationToken;
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
            Message downloadMessage = await _bot.SendMessage(chatId, "Queued...");

            var request = new DownloadRequest(chatId, downloadMessage.Id, message.Text!);
            bool result = _channel.Writer.TryWrite(request);

            if (!result)
            {
                await _bot.EditMessageText(chatId, downloadMessage.Id, "New requests cannot be queued currently. Please try again later");
            }
        }
    }

    private async Task HandleError(Exception exception, HandleErrorSource source)
    {
        ConsoleUtils.PrintException(exception);
    }

    public async Task Run(CancellationToken shutdownToken)
    {
        using var workerCts = new CancellationTokenSource();
        Task downloadWorker = ProcessDownloads(workerCts.Token);

        Console.WriteLine("Bot is running...");

        try
        {
            await Task.Delay(Timeout.Infinite, shutdownToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Shutdown requested");
            _channel.Writer.TryComplete();

            Task gracePeriod = Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);

            if (await Task.WhenAny(downloadWorker, gracePeriod) == gracePeriod)
            {
                workerCts.Cancel();
            }

            try
            {
                await downloadWorker;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Download cancellation requested");
            }
        }
    }

    private async Task ProcessDownloads(CancellationToken workerCancellationToken)
    {
        await foreach (DownloadRequest request in _channel.Reader.ReadAllAsync(workerCancellationToken))
        {
            await ProcessDownload(request, workerCancellationToken);
        }
    }

    private async Task ProcessDownload(DownloadRequest request, CancellationToken workerCancellationToken)
    {
        string outputDir = Path.Combine(Path.GetTempPath(), "audio-provider-bot", Guid.NewGuid().ToString());

        try
        {
            await _bot.EditMessageText(request.ChatId, request.MessageId, "Downloading...", cancellationToken: _telegramCancellationToken);
            Directory.CreateDirectory(outputDir);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(workerCancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

            string audio = await AudioService.GetYTAudioFilePath(request.MessageText, outputDir, timeoutCts.Token);

            await using FileStream fs = File.OpenRead(audio);
            await _bot.SendAudio(request.ChatId, fs, cancellationToken: _telegramCancellationToken);
            await _bot.DeleteMessage(request.ChatId, request.MessageId, cancellationToken: _telegramCancellationToken);
        }
        catch (Exception ex)
        {
            ConsoleUtils.PrintException(ex);

            try
            {
                await _bot.EditMessageText(request.ChatId, request.MessageId, "Download failed!", cancellationToken: _telegramCancellationToken);
            }
            catch (Exception telegramException)
            {
                ConsoleUtils.PrintException(telegramException);
            }
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
            }
        }
    }
}
