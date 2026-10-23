using System.Text.Json.Serialization;

namespace AudioProvider;

public record DownloadMetadata
{
    [JsonPropertyName("duration")]
    public int Duration { get; init; }
    [JsonPropertyName("filesize_approx")]
    public long FileSize { get; init; }

    public DownloadMetadata(int duration, long fileSize)
    {
        Duration = duration;
        FileSize = fileSize;
    }
}
