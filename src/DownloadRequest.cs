namespace AudioProvider;

public record DownloadRequest
{
    public long ChatId { get; init; }
    public int MessageId { get; init; }
    public string MessageText { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public int TopicStarterMessageId { get; init; }

    public DownloadRequest(long chatId,
        int messageId,
        string messageText,
        string downloadUrl,
        string filename,
        int originalMessageId)
    {
        ChatId = chatId;
        MessageId = messageId;
        MessageText = messageText;
        DownloadUrl = downloadUrl;
        FileName = filename;
        TopicStarterMessageId = originalMessageId;
    }
}
