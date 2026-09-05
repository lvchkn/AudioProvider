using System.Diagnostics;

namespace AudioProvider;

public static class AudioService
{
    public static async Task<string> GetYTAudioFilePath(string downloadUrl, string outputPath, CancellationToken cancellationToken)
    {
        var processStartInfo = new ProcessStartInfo()
        {
            FileName = "yt-dlp",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        processStartInfo.ArgumentList.Add("--no-playlist");
        processStartInfo.ArgumentList.Add("-x");
        processStartInfo.ArgumentList.Add("--audio-format");
        processStartInfo.ArgumentList.Add("mp3");
        processStartInfo.ArgumentList.Add(downloadUrl);
        processStartInfo.ArgumentList.Add("-o");
        processStartInfo.ArgumentList.Add(Path.Combine(outputPath, "%(title)s.%(ext)s"));

        using var process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start yt-dlp");

        using var registration = cancellationToken.Register(() =>
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        });

        string stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        string stderr = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new Exception(stderr);
        }

        string audioFilePath = Directory.GetFiles(outputPath, "*.mp3").Single();

        return audioFilePath;
    }
}
