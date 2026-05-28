using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace MacOSHelper.Core;

public static class DmgExtractor
{
    public static async Task<bool> IsDmgAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return false;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        if (fs.Length < 512) return false;
        fs.Position = fs.Length - 512;
        var sig = new byte[4];
        if (await fs.ReadAsync(sig.AsMemory(0, 4), ct) != 4) return false;
        return sig[0] == (byte)'k' && sig[1] == (byte)'o' &&
               sig[2] == (byte)'l' && sig[3] == (byte)'y';
    }

    public static async Task<string> ExtractHfsAsync(
        string dmgPath, string destDir,
        Action<string>? log = null,
        Action<long, long, string>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(destDir);
        var hfsPath = Path.Combine(destDir, "image.hfs");

        using var src = new FileStream(dmgPath, FileMode.Open, FileAccess.Read,
            FileShare.Read, 4 * 1024 * 1024, useAsync: true);

        if (src.Length < 512) throw new Exception("DMG: arquivo muito pequeno.");
        src.Position = src.Length - 512;
        var trailer = new byte[512];
        await src.ReadExactlyAsync(trailer, ct);

        if (trailer[0] != (byte)'k' || trailer[1] != (byte)'o' ||
            trailer[2] != (byte)'l' || trailer[3] != (byte)'y')
            throw new Exception("DMG: trailer 'koly' não encontrado — arquivo não é UDIF.");

        long xmlOffset = ReadBE64(trailer, 216);
        long xmlLength = ReadBE64(trailer, 224);

        src.Position = xmlOffset;
        var xmlBytes = new byte[xmlLength];
        await src.ReadExactlyAsync(xmlBytes, ct);
        var doc = XDocument.Parse(Encoding.UTF8.GetString(xmlBytes));

        var blkxList = ExtractBlkxEntries(doc);

        if (blkxList.Count == 0)
            throw new Exception("DMG: nenhuma entrada blkx encontrada no plist.");

        log?.Invoke($"[INFO] DMG: {blkxList.Count} partição(ões) encontrada(s): " +
                    string.Join(", ", blkxList.Select(b => $"'{b.Name}'")));

        // Tenta encontrar partição HFS/APFS pelo nome
        var hfs = blkxList.FirstOrDefault(b =>
            b.Name.Contains("HFS",  StringComparison.OrdinalIgnoreCase) ||
            b.Name.Contains("APFS", StringComparison.OrdinalIgnoreCase));

        // Fallback 1: qualquer entrada que não seja mapa de partição / espaço livre
        if (hfs.Data == null)
        {
            hfs = blkxList.FirstOrDefault(b =>
                !b.Name.Contains("partition_map", StringComparison.OrdinalIgnoreCase) &&
                !b.Name.Contains("Extra",         StringComparison.OrdinalIgnoreCase) &&
                !b.Name.Contains("Free",          StringComparison.OrdinalIgnoreCase) &&
                !b.Name.Contains("Driver",        StringComparison.OrdinalIgnoreCase) &&
                !b.Name.Contains("Apple_Boot",    StringComparison.OrdinalIgnoreCase));
            if (hfs.Data != null)
                log?.Invoke($"[WARN] DMG: nome não contém 'HFS'/'APFS', usando '{hfs.Name}' como fallback.");
        }

        // Fallback 2: maior partição disponível (por contagem de setores)
        if (hfs.Data == null)
        {
            hfs = blkxList
                .Where(b => b.Data != null && b.Data.Length >= 24)
                .OrderByDescending(b => ReadBE64(b.Data!, 16))
                .FirstOrDefault();
            if (hfs.Data != null)
                log?.Invoke($"[WARN] DMG: usando maior partição como fallback: '{hfs.Name}'.");
        }

        if (hfs.Data == null)
            throw new Exception("DMG: nenhuma partição utilizável encontrada.");

        log?.Invoke($"[INFO] DMG: extraindo partição '{hfs.Name}'");

        long blkxSectorCount = ReadBE64(hfs.Data, 16);
        long totalBytes = blkxSectorCount * 512;

        using var dest = new FileStream(hfsPath, FileMode.Create, FileAccess.Write,
            FileShare.None, 4 * 1024 * 1024, useAsync: true);

        await ProcessBlkxAsync(hfs.Data, src, dest, totalBytes,
            "DMG " + Path.GetFileName(dmgPath), progress, ct);
        await dest.FlushAsync(ct);

        return hfsPath;
    }

    private static async Task ProcessBlkxAsync(byte[] blkx, Stream src, Stream dest,
        long totalBytes, string label,
        Action<long, long, string>? progress, CancellationToken ct)
    {
        if (blkx.Length < 204 ||
            blkx[0] != (byte)'m' || blkx[1] != (byte)'i' ||
            blkx[2] != (byte)'s' || blkx[3] != (byte)'h')
            throw new Exception("DMG: BLKX inválido (assinatura 'mish' ausente).");

        long blkxDataOffset = ReadBE64(blkx, 24);
        uint blockCount     = ReadBE32(blkx, 200);

        long currentPos = 0;
        long totalWritten = 0;

        for (uint i = 0; i < blockCount; i++)
        {
            int eo = 204 + (int)(i * 40);
            if (eo + 40 > blkx.Length) break;

            uint entryType        = ReadBE32(blkx, eo);
            long sectorNumber     = ReadBE64(blkx, eo + 8);
            long sectorCount      = ReadBE64(blkx, eo + 16);
            long compressedOffset = ReadBE64(blkx, eo + 24);
            long compressedLength = ReadBE64(blkx, eo + 32);

            if (entryType == 0xFFFFFFFFu) continue;
            if (entryType == 0x7FFFFFFEu) break;

            long expectedPos = sectorNumber * 512;
            if (currentPos < expectedPos)
                await WriteZerosAsync(dest, expectedPos - currentPos, ct);

            long outSize = sectorCount * 512;

            switch (entryType)
            {
                case 0x00000000u:
                case 0x00000001u:
                    src.Position = blkxDataOffset + compressedOffset;
                    await CopyExactAsync(src, dest, outSize, ct);
                    break;

                case 0x00000002u:
                    await WriteZerosAsync(dest, outSize, ct);
                    break;

                case 0x80000005u:
                    src.Position = blkxDataOffset + compressedOffset;
                    using (var ls = new LimitedStream(src, compressedLength))
                    using (var zl = new ZLibStream(ls, CompressionMode.Decompress))
                        await CopyExactAsync(zl, dest, outSize, ct);
                    break;

                case 0x80000004u:
                    throw new Exception("DMG: compressão ADC (UDCO) não suportada.");
                case 0x80000006u:
                    throw new Exception("DMG: compressão bzip2 (UDBZ) não suportada.");
                case 0x80000007u:
                    throw new Exception("DMG: compressão LZFSE (ULFO) não suportada.");
                case 0x80000008u:
                    throw new Exception("DMG: compressão LZMA (ULMA) não suportada.");
                default:
                    throw new Exception($"DMG: tipo de chunk 0x{entryType:X8} desconhecido.");
            }

            currentPos    = expectedPos + outSize;
            totalWritten += outSize;
            progress?.Invoke(totalWritten, totalBytes, label);
        }
    }

    private static List<(string Name, byte[] Data)> ExtractBlkxEntries(XDocument doc)
    {
        var result = new List<(string, byte[])>();

        var topDict = doc.Root?.Element("dict");
        if (topDict == null) return result;

        var rfDict = FindKeyValue(topDict, "resource-fork");
        if (rfDict?.Name != "dict") return result;

        var blkxArray = FindKeyValue(rfDict, "blkx");
        if (blkxArray?.Name != "array") return result;

        foreach (var entry in blkxArray.Elements("dict"))
        {
            var nameElem = FindKeyValue(entry, "Name");
            var dataElem = FindKeyValue(entry, "Data");
            if (dataElem == null) continue;

            var name = nameElem?.Value?.Trim() ?? "";
            var b64 = (dataElem.Value ?? "")
                .Replace("\n", "").Replace("\r", "").Replace(" ", "").Replace("\t", "");
            if (string.IsNullOrEmpty(b64)) continue;

            try { result.Add((name, Convert.FromBase64String(b64))); }
            catch { }
        }

        return result;
    }

    private static XElement? FindKeyValue(XElement parent, string keyName)
    {
        var children = parent.Elements().ToList();
        for (int i = 0; i < children.Count - 1; i++)
        {
            if (children[i].Name == "key" && children[i].Value == keyName)
                return children[i + 1];
        }
        return null;
    }

    private static uint ReadBE32(byte[] b, int o) =>
        ((uint)b[o] << 24) | ((uint)b[o+1] << 16) | ((uint)b[o+2] << 8) | b[o+3];

    private static long ReadBE64(byte[] b, int o) =>
        ((long)b[o]   << 56) | ((long)b[o+1] << 48) |
        ((long)b[o+2] << 40) | ((long)b[o+3] << 32) |
        ((long)b[o+4] << 24) | ((long)b[o+5] << 16) |
        ((long)b[o+6] << 8)  | b[o+7];

    private static async Task CopyExactAsync(Stream src, Stream dest, long count, CancellationToken ct)
    {
        var buf = new byte[1024 * 1024];
        long remaining = count;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buf.Length, remaining);
            int n = await src.ReadAsync(buf.AsMemory(0, toRead), ct);
            if (n == 0) throw new Exception("DMG: EOF inesperado durante leitura.");
            await dest.WriteAsync(buf.AsMemory(0, n), ct);
            remaining -= n;
        }
    }

    private static async Task WriteZerosAsync(Stream dest, long count, CancellationToken ct)
    {
        var zeros = new byte[64 * 1024];
        long remaining = count;
        while (remaining > 0)
        {
            int toWrite = (int)Math.Min(zeros.Length, remaining);
            await dest.WriteAsync(zeros.AsMemory(0, toWrite), ct);
            remaining -= toWrite;
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
        public override long Seek(long o, SeekOrigin r) => throw new NotSupportedException();
        public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
    }
}
