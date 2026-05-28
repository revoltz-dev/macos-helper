using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MacOSHelper.Core;

public static class XarExtractor
{
    public static async Task<bool> IsXarAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return false;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        var magic = new byte[4];
        if (await fs.ReadAsync(magic.AsMemory(0, 4), ct) != 4) return false;
        return magic[0] == (byte)'x' && magic[1] == (byte)'a' &&
               magic[2] == (byte)'r' && magic[3] == (byte)'!';
    }

    public static async Task<string?> ExtractFirstAsync(
        string xarPath,
        string destDir,
        Func<string, bool> filter,
        Action<string>? log = null,
        Action<long, long, string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(destDir);

        using var fs = new FileStream(xarPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4 * 1024 * 1024, useAsync: true);

        var hdr = new byte[28];
        await fs.ReadExactlyAsync(hdr, ct);

        if (hdr[0] != (byte)'x' || hdr[1] != (byte)'a' ||
            hdr[2] != (byte)'r' || hdr[3] != (byte)'!')
            throw new Exception($"Não é um arquivo XAR (.pkg): {Path.GetFileName(xarPath)}");

        int headerSize  = (hdr[4] << 8) | hdr[5];
        long tocCompLen = ReadBE64(hdr, 8);

        if (headerSize > 28) fs.Position = headerSize;

        var tocBytes = new byte[tocCompLen];
        await fs.ReadExactlyAsync(tocBytes, ct);

        string tocXml;
        using (var ms   = new MemoryStream(tocBytes))
        using (var zlib = new ZLibStream(ms, CompressionMode.Decompress))
        using (var sr   = new StreamReader(zlib, Encoding.UTF8))
        {
            tocXml = await sr.ReadToEndAsync(ct);
        }

        long heapStart = headerSize + tocCompLen;

        var doc = XDocument.Parse(tocXml);
        var toc = doc.Root?.Element("toc")
            ?? throw new Exception("XAR: <toc> não encontrado.");

        foreach (var node in toc.Descendants("file"))
        {
            var nameElem = node.Element("name");
            if (nameElem == null) continue;
            var name = nameElem.Value;
            if (!filter(name)) continue;

            var dataNode = node.Element("data");
            if (dataNode == null) continue;

            long offset = long.Parse(dataNode.Element("offset")?.Value ?? "0");
            long length = long.Parse(dataNode.Element("length")?.Value ?? "0");
            string enc  = dataNode.Element("encoding")?.Attribute("style")?.Value
                        ?? "application/octet-stream";

            fs.Position = heapStart + offset;

            var destPath = Path.Combine(destDir, Path.GetFileName(name));
            var label    = $"XAR {Path.GetFileName(xarPath)}";
            log?.Invoke($"[INFO] XAR: extraindo {Path.GetFileName(name)} " +
                        $"({length / (1024.0*1024):F0} MB, {enc.Replace("application/", "")})");

            using var dest = new FileStream(destPath, FileMode.Create, FileAccess.Write,
                FileShare.None, 4 * 1024 * 1024, useAsync: true);

            switch (enc)
            {
                case "application/octet-stream":
                    await CopyExactAsync(fs, dest, length, label, progress, ct);
                    break;

                case "application/x-gzip":
                    using (var limited = new LimitedStream(fs, length))
                    using (var zlib = new ZLibStream(limited, CompressionMode.Decompress))
                        await zlib.CopyToAsync(dest, 4 * 1024 * 1024, ct);
                    break;

                default:
                    throw new Exception($"XAR: encoding '{enc}' não suportado.");
            }

            return destPath;
        }

        return null;
    }

    private static long ReadBE64(byte[] b, int o) =>
        ((long)b[o]   << 56) | ((long)b[o+1] << 48) |
        ((long)b[o+2] << 40) | ((long)b[o+3] << 32) |
        ((long)b[o+4] << 24) | ((long)b[o+5] << 16) |
        ((long)b[o+6] << 8)  | b[o+7];

    private static async Task CopyExactAsync(Stream src, Stream dest, long count,
        string label, Action<long, long, string>? progress, CancellationToken ct)
    {
        var buf = new byte[4 * 1024 * 1024];
        long remaining = count, written = 0;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buf.Length, remaining);
            int n = await src.ReadAsync(buf.AsMemory(0, toRead), ct);
            if (n == 0) throw new Exception("XAR: EOF inesperado no heap.");
            await dest.WriteAsync(buf.AsMemory(0, n), ct);
            remaining -= n;
            written   += n;
            progress?.Invoke(written, count, label);
        }
    }

    private sealed class LimitedStream : Stream
    {
        private readonly Stream _base;
        private long _remaining;
        public LimitedStream(Stream b, long len) { _base = b; _remaining = len; }
        public override bool CanRead  => true;
        public override bool CanSeek  => false;
        public override bool CanWrite => false;
        public override long Length   => _remaining;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override int Read(byte[] buf, int off, int cnt)
        {
            if (_remaining == 0) return 0;
            int n = _base.Read(buf, off, (int)Math.Min(cnt, _remaining));
            _remaining -= n;
            return n;
        }
        public override async ValueTask<int> ReadAsync(Memory<byte> buf, CancellationToken ct = default)
        {
            if (_remaining == 0) return 0;
            int toRead = (int)Math.Min(buf.Length, _remaining);
            int n = await _base.ReadAsync(buf.Slice(0, toRead), ct);
            _remaining -= n;
            return n;
        }
        public override void Flush() { }
        public override long Seek(long o, SeekOrigin or) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}
