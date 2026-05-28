using System.Net.Http.Headers;
using System.Security.Cryptography;
using MacOSHelper.Models;

namespace MacOSHelper.Core;


public class Downloader
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromHours(4),
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 macOS" } }
    };

    private readonly string _baseDir;

    public Downloader(string baseDir)
    {
        _baseDir = baseDir;
        Directory.CreateDirectory(baseDir);
    }

    public async Task DownloadProductAsync(DownloadEntry entry, CancellationToken ct)
    {
        entry.Status = DownloadStatus.Downloading;
        Directory.CreateDirectory(entry.DestFolder);

        entry.TotalBytes = entry.Packages.Sum(p => p.Size);

        for (int i = 0; i < entry.Packages.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var pkg = entry.Packages[i];
            entry.CurrentPackageIndex = i;
            entry.CurrentFileName = pkg.FileName;
            entry.CurrentFileTotal = pkg.Size;
            System.Threading.Interlocked.Exchange(ref entry.CurrentFileBytes, 0);

            var destPath = Path.Combine(entry.DestFolder, pkg.FileName);

            try
            {
                await DownloadFileAsync(pkg.Url, destPath, entry, ct);

                // Verificação SHA1 (se catálogo forneceu Digest)
                if (!string.IsNullOrEmpty(pkg.Digest) && pkg.Digest.Length == 40)
                {
                    var actual = await ComputeSha1Async(destPath, ct);
                    if (!string.Equals(actual, pkg.Digest, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.Status = DownloadStatus.Failed;
                        entry.ErrorMessage = T.IsEn
                            ? $"SHA1 mismatch for {pkg.FileName} (expected {pkg.Digest}, got {actual})."
                            : $"SHA1 do arquivo {pkg.FileName} não confere (esperado {pkg.Digest}, obtido {actual}).";
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                entry.Status = DownloadStatus.Cancelled;
                return;
            }
            catch (Exception ex)
            {
                entry.Status = DownloadStatus.Failed;
                entry.ErrorMessage = ex.Message;
                return;
            }
        }

        entry.Status = DownloadStatus.Completed;
    }

    private static async Task<string> ComputeSha1Async(string path, CancellationToken ct)
    {
        using var fs   = new FileStream(path, FileMode.Open, FileAccess.Read,
                                        FileShare.Read, 1 * 1024 * 1024, useAsync: true);
        using var sha1 = SHA1.Create();
        var hash = await sha1.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash); // hex maiúsculo, comparação OrdinalIgnoreCase
    }

    private async Task DownloadFileAsync(string url, string destPath, DownloadEntry entry, CancellationToken ct)
    {
        var fileInfo = new FileInfo(destPath);
        long existingBytes = fileInfo.Exists ? fileInfo.Length : 0;

        if (existingBytes > 0 && existingBytes == entry.CurrentFileTotal)
        {
            System.Threading.Interlocked.Add(ref entry.TotalDownloadedBytes, existingBytes);
            System.Threading.Interlocked.Exchange(ref entry.CurrentFileBytes, existingBytes);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingBytes > 0)
            request.Headers.Range = new RangeHeaderValue(existingBytes, null);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        if (entry.CurrentFileTotal <= 0 && response.Content.Headers.ContentLength.HasValue)
            entry.CurrentFileTotal = response.Content.Headers.ContentLength.Value + existingBytes;

        var fileMode = (response.StatusCode == System.Net.HttpStatusCode.PartialContent)
            ? FileMode.Append : FileMode.Create;

        if (fileMode == FileMode.Create && existingBytes > 0)
            existingBytes = 0;

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var file = new FileStream(destPath, fileMode, FileAccess.Write, FileShare.None, 81920, true);

        System.Threading.Interlocked.Exchange(ref entry.CurrentFileBytes, existingBytes);
        System.Threading.Interlocked.Add(ref entry.TotalDownloadedBytes, existingBytes);

        var buffer = new byte[81920];
        while (true)
        {
            int read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            System.Threading.Interlocked.Add(ref entry.CurrentFileBytes, read);
            System.Threading.Interlocked.Add(ref entry.TotalDownloadedBytes, read);

            var now = DateTime.UtcNow;
            var elapsed = (now - entry.LastSpeedSample).TotalSeconds;
            if (elapsed >= 1.0)
            {
                var bytesDelta = entry.TotalDownloadedBytes - entry.LastSpeedBytes;
                entry.SpeedBytesPerSec = bytesDelta / elapsed;
                entry.LastSpeedBytes = entry.TotalDownloadedBytes;
                entry.LastSpeedSample = now;
            }
        }
    }

    public string GetProductFolder(string productId)
        => Path.Combine(_baseDir, productId);
}
