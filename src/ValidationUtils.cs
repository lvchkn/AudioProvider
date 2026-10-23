namespace AudioProvider;

public static class ValidationUtils
{
    private static readonly string[] _allowedHosts = [
        "youtube.com",
        "www.youtube.com",
        "music.youtube.com",
        "m.youtube.com",
        "youtu.be"
    ];

    public static (string url, string filename) GetRequestInfo(string? messageText)
    {
        var filename = string.Empty;
        var url = string.Empty;

        if (string.IsNullOrWhiteSpace(messageText))
        {
            return (url, filename);
        }

        string[] text = messageText.Replace("—", "--").Split("--name", 2);
        url = text[0];

        if (text.Length > 1)
        {
            filename = text[1].Trim();
        }

        return (url, filename);
    }

    public static bool IsRequestValid(DownloadMetadata? metadata)
    {
        const int DURATION_LIMIT = 3600; // 1 hr
        const int FILESIZE_LIMIT = 50_000_000; // 50 mb

        if (metadata is null
            || metadata.Duration > DURATION_LIMIT
            || metadata.FileSize > FILESIZE_LIMIT)
        {
            return false;
        }

        return true;
    }

    public static bool IsYTLink(string? messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return false;
        }

        if (!Uri.TryCreate(messageText, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!_allowedHosts.Contains(uri.Host))
        {
            return false;
        }

        return true;
    }
}
