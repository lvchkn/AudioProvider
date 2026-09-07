namespace AudioProvider;

public record DownloadRequest
{
    public long ChatId { get; init; }
    public int MessageId { get; init; }
    public string MessageText { get; init; } = string.Empty;

    public DownloadRequest(long chatId, int messageId, string messageText)
    {
        ChatId = chatId;
        MessageId = messageId;
        MessageText = messageText;
    }
}
