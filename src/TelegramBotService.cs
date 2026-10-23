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

        if (ValidationUtils.IsYTLink(message.Text))
        {
            (string url, string filename) = ValidationUtils.GetRequestInfo(message.Text);
            long chatId = message.Chat.Id;
            Message downloadMessage = await _bot.SendMessage(chatId, "Queued...");

            var request = new DownloadRequest(chatId, downloadMessage.Id, message.Text!, url, filename, message.Id);
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
            Directory.CreateDirectory(outputDir);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(workerCancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(5));

            await _bot.EditMessageText(request.ChatId, request.MessageId, "Validating...", cancellationToken: _telegramCancellationToken);
            DownloadMetadata? metadata = await AudioService.GetMetadata(request.DownloadUrl, timeoutCts.Token);
            if (!ValidationUtils.IsRequestValid(metadata))
            {
                throw new ArgumentException("Wrong arguments passed in", metadata?.ToString());
            }

            await _bot.EditMessageText(request.ChatId, request.MessageId, "Downloading...", cancellationToken: _telegramCancellationToken);
            string audio = await AudioService.GetYTAudioFilePath(request.DownloadUrl, outputDir, timeoutCts.Token);

            await using FileStream fs = File.OpenRead(audio);

            string filename = request.FileName == string.Empty
                ? Path.GetFileNameWithoutExtension(audio)
                : request.FileName;

            Console.WriteLine($"""File "{filename}" downloaded. Metadata: {metadata}""");

            var inputFile = InputFile.FromStream(fs, filename);
            await _bot.SendAudio(request.ChatId, inputFile, replyParameters: new() { MessageId = request.TopicStarterMessageId }, cancellationToken: _telegramCancellationToken);
            await _bot.DeleteMessage(request.ChatId, request.MessageId, cancellationToken: _telegramCancellationToken);
        }
        catch (Exception ex)
        {
            ConsoleUtils.PrintException(ex);

            string errorMessage = ex is ArgumentException
                ? "Wrong arguments. Likely reasons: duration is too long or file size is too big"
                : "Download failed!";

            try
            {
                await _bot.EditMessageText(request.ChatId, request.MessageId, errorMessage, cancellationToken: _telegramCancellationToken);
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
