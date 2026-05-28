using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using MacOSHelper.Helpers;
using MacOSHelper.Models;

namespace MacOSHelper.Core;

public class UsbCreator
{
    private readonly Action<string> _log;
    private readonly Action<long, long, string> _progress;

    public UsbCreator(Action<string> log, Action<long, long, string> progress)
    {
        _log      = log;
        _progress = progress;
    }

    private static string L(string pt, string en) => T.IsEn ? en : pt;

    public async Task CreateAsync(DiskInfo disk, string productFolder, CancellationToken ct)
    {
        using var autoPlay = new AutoPlaySuppressor();

        _log(L($"[INFO] Iniciando criação do pendrive para disco {disk.DiskIndex} ({disk.Model})",
               $"[INFO] Starting USB creation for disk {disk.DiskIndex} ({disk.Model})"));
        _log(L($"[INFO] Pasta do instalador: {productFolder}",
               $"[INFO] Installer folder: {productFolder}"));

        await Stage1_FormatDisk(disk.DiskIndex, ct);

        _log(L("[INFO] Aguardando partições...", "[INFO] Waiting for partitions..."));
        await Task.Delay(3000, ct);

        var tmpBase = Path.Combine(Path.GetTempPath(), "macos_helper_extract");
        try
        {
            var hfsPath = await Stage2_ExtractHfs(productFolder, tmpBase, ct);
            await Stage3_WriteImage(disk.DiskIndex, hfsPath, ct);
        }
        finally
        {
            if (Directory.Exists(tmpBase))
                try { Directory.Delete(tmpBase, true); } catch { }
        }

        await Stage4_InstallBootloader(disk.DiskIndex, ct);

        _log(L("[OK] Pendrive criado com sucesso! Ejete com segurança.",
               "[OK] USB created successfully! Eject safely."));
        _log("");
        _log(L("=== Como usar no Mac ===", "=== How to use on the Mac ==="));
        _log(L("1) Plugue o pendrive e ligue o Mac segurando a tecla Option (⌥).",
               "1) Plug in the USB and power on the Mac holding the Option (⌥) key."));
        _log(L("2) Selecione o ícone de instalador macOS no menu de boot.",
               "2) Pick the macOS installer icon from the boot menu."));
        _log(L("3) Em 'macOS Utilities' clique em 'Reinstalar macOS'.",
               "3) From 'macOS Utilities' click 'Reinstall macOS'."));
        _log("");
        _log(L("Se aparecer 'O servidor de recuperação não pôde ser contatado',",
               "If you see 'The recovery server could not be contacted',"));
        _log(L("veja como resolver neste vídeo:",
               "watch the fix in this video:"));
        _log("https://www.youtube.com/watch?v=w7tHXSohdxU");
    }

    private async Task Stage1_FormatDisk(int diskIndex, CancellationToken ct)
    {
        _log(L($"[INFO] Stage 1: Formatando disco {diskIndex} (Storage Management API)...",
               $"[INFO] Stage 1: Formatting disk {diskIndex} (Storage Management API)..."));

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();

            ManagementObject GetDisk()
            {
                using var s = new ManagementObjectSearcher(scope,
                    new ObjectQuery($"SELECT * FROM MSFT_Disk WHERE Number = {diskIndex}"));
                return s.Get().Cast<ManagementObject>().FirstOrDefault()
                    ?? throw new Exception(L($"Disco {diskIndex} não encontrado pela Storage Management API.",
                                              $"Disk {diskIndex} not found via Storage Management API."));
            }

            void Check(ManagementBaseObject result, string op)
            {
                var code = Convert.ToUInt32(result["ReturnValue"]);
                if (code != 0)
                    throw new Exception(L($"Stage 1 — {op} falhou (código {code}).",
                                           $"Stage 1 — {op} failed (code {code})."));
            }

            using (var d = GetDisk())
            {
                try { d.InvokeMethod("Online", null, null); } catch { }
                var sa = d.GetMethodParameters("SetAttributes");
                sa["IsReadOnly"] = false;
                try { d.InvokeMethod("SetAttributes", sa, null); } catch { }
            }

            _log(L("[INFO]   Limpando tabela de partições...", "[INFO]   Wiping partition table..."));
            using (var d = GetDisk())
            {
                var p = d.GetMethodParameters("Clear");
                p["RemoveData"]        = true;
                p["RemoveOEM"]         = true;
                p["ZeroOutEntireDisk"] = false;
                Check(d.InvokeMethod("Clear", p, null), "Clear");
            }

            Thread.Sleep(1500);
            ct.ThrowIfCancellationRequested();

            using (var d = GetDisk())
            {
                try { d.InvokeMethod("Online", null, null); } catch { }
                var sa = d.GetMethodParameters("SetAttributes");
                sa["IsReadOnly"] = false;
                try { d.InvokeMethod("SetAttributes", sa, null); } catch { }
            }
            Thread.Sleep(500);

            _log(L("[INFO]   Inicializando como MBR...", "[INFO]   Initializing as MBR..."));
            using var disk = GetDisk();
            {
                var currentStyle = Convert.ToUInt16(disk["PartitionStyle"] ?? (ushort)0);
                if (currentStyle == 1)
                {
                    _log(L("[INFO]   Disco já está em MBR, pulando Initialize.",
                           "[INFO]   Disk is already MBR, skipping Initialize."));
                }
                else
                {
                    var p = disk.GetMethodParameters("Initialize");
                    p["PartitionStyle"] = (ushort)1;
                    var r    = disk.InvokeMethod("Initialize", p, null);
                    var code = Convert.ToUInt32(r["ReturnValue"]);
                    if (code != 0 && code != 41001 && code != 41002 && code != 42002)
                        throw new Exception(L($"Stage 1 — Initialize falhou (código {code}).",
                                               $"Stage 1 — Initialize failed (code {code})."));
                    if (code != 0)
                        _log(L($"[WARN]   Initialize retornou {code}, continuando.",
                               $"[WARN]   Initialize returned {code}, continuing."));
                }
            }

            _log(L("[INFO]   Criando partição 1 (200 MB, ativa)...",
                   "[INFO]   Creating partition 1 (200 MB, active)..."));
            var cp1 = disk.GetMethodParameters("CreatePartition");
            cp1["Size"]              = (ulong)(200 * 1024 * 1024);
            cp1["AssignDriveLetter"] = true;
            cp1["MbrType"]           = (byte)0x0B;
            cp1["IsActive"]          = true;
            Check(disk.InvokeMethod("CreatePartition", cp1, null), "CreatePartition(1)");

            Thread.Sleep(1200);
            ct.ThrowIfCancellationRequested();

            using var p1Search = new ManagementObjectSearcher(scope,
                new ObjectQuery(
                    $"SELECT * FROM MSFT_Partition " +
                    $"WHERE DiskNumber = {diskIndex} AND PartitionNumber = 1"));
            using var part1 = p1Search.Get().Cast<ManagementObject>().FirstOrDefault();

            if (part1 != null)
            {
                var letter = Convert.ToChar(part1["DriveLetter"] ?? '\0');
                _log(L($"[INFO]   Formatando {(letter != '\0' ? $"{letter}:" : "partição 1")} como FAT32...",
                       $"[INFO]   Formatting {(letter != '\0' ? $"{letter}:" : "partition 1")} as FAT32..."));

                if (letter != '\0')
                {
                    using var vs = new ManagementObjectSearcher(scope,
                        new ObjectQuery($"SELECT * FROM MSFT_Volume WHERE DriveLetter = '{letter}'"));
                    using var vol = vs.Get().Cast<ManagementObject>().FirstOrDefault();

                    if (vol != null)
                    {
                        var fmt = vol.GetMethodParameters("Format");
                        fmt["FileSystem"]      = "FAT32";
                        fmt["Force"]           = true;
                        fmt["Full"]            = false;
                        fmt["FileSystemLabel"] = "REVOLTZ";
                        Check(vol.InvokeMethod("Format", fmt, null), "Format(1)");
                    }
                }
            }

            _log(L("[INFO]   Criando partição 2 (sem formatação)...",
                   "[INFO]   Creating partition 2 (unformatted)..."));
            var cp2 = disk.GetMethodParameters("CreatePartition");
            cp2["UseMaximumSize"] = true;
            cp2["MbrType"]        = (byte)0x0B;
            Check(disk.InvokeMethod("CreatePartition", cp2, null), "CreatePartition(2)");

            try
            {
                var dh = CreateFileW(
                    $@"\\.\PhysicalDrive{diskIndex}",
                    0xC0000000u, 0x00000003u, nint.Zero, 3u, 0x80000000u, nint.Zero);
                if (!dh.IsInvalid)
                {
                    using var ms = new FileStream(dh, FileAccess.ReadWrite, 512);
                    var mbr = new byte[512];
                    ms.Position = 0;
                    ms.ReadExactly(mbr);
                    mbr[466] = 0xAB;
                    ms.Position = 0;
                    ms.Write(mbr);
                    ms.Flush();
                    _log(L("[INFO]   Partição 2 marcada como Apple Recovery HD (0xAB) no MBR.",
                           "[INFO]   Partition 2 marked as Apple Recovery HD (0xAB) in MBR."));
                }
                else
                {
                    _log(L($"[WARN]   Patch MBR: não abriu PhysicalDrive (erro {Marshal.GetLastWin32Error()}).",
                           $"[WARN]   MBR patch: couldn't open PhysicalDrive (error {Marshal.GetLastWin32Error()})."));
                }
            }
            catch (Exception ex)
            {
                _log(L($"[WARN]   Patch MBR falhou: {ex.Message}",
                       $"[WARN]   MBR patch failed: {ex.Message}"));
            }

            _log(L("[OK] Stage 1: Disco formatado.", "[OK] Stage 1: Disk formatted."));
        }, ct);
    }

    private async Task<string> Stage2_ExtractHfs(string productFolder, string tmpBase, CancellationToken ct)
    {
        _log(L("[INFO] Stage 2: Localizando imagem de instalador...",
               "[INFO] Stage 2: Locating installer image..."));
        Directory.CreateDirectory(tmpBase);

        // Apple só bênçoa o BaseSystem.dmg pra boot direto via EFI.
        // O InstallESD.dmg contém o instalador completo mas o volume HFS+ dele
        // não é bootável sozinho (precisaria do createinstallmedia da Apple
        // pra unir Base + Packages em um volume blessed — só roda no macOS).
        //
        // Resultado: nosso pendrive boota como Recovery e depois precisa
        // contactar o servidor da Apple pra baixar os pacotes.
        // Em High Sierra a Apple quebrou o HTTPS desse caminho, então o
        // usuário precisa rodar no Terminal do Recovery:
        //   nvram IASUCatalogURL="http://swscan.apple.com/content/catalogs/..."
        // (URL com http, sem o S — pula o SSL quebrado).

        // Prioridade 1: BaseSystem.dmg direto (bootável, blessed pela Apple).
        var baseDmg = Directory.GetFiles(productFolder, "BaseSystem.dmg",
                                         SearchOption.TopDirectoryOnly).FirstOrDefault()
                   ?? Directory.GetFiles(productFolder, "BaseSystem.dmg",
                                         SearchOption.AllDirectories).FirstOrDefault();

        if (baseDmg != null)
        {
            var sizeMb = new FileInfo(baseDmg).Length / (1024.0 * 1024);
            _log(L($"[INFO]   BaseSystem.dmg encontrado ({sizeMb:F0} MB) — usando como fonte HFS+.",
                   $"[INFO]   BaseSystem.dmg found ({sizeMb:F0} MB) — using as HFS+ source."));
            return await ExtractFromPkgOrDmgAsync(baseDmg, tmpBase, ct);
        }

        // Prioridade 2: BaseSystem.dmg aninhado dentro de pkg (raro, mas pode acontecer)
        _log(L("[WARN]   BaseSystem.dmg não encontrado direto, buscando dentro de .pkg...",
               "[WARN]   BaseSystem.dmg not found directly, searching inside .pkg..."));
        var pkgFiles = Directory.GetFiles(productFolder, "*.pkg", SearchOption.AllDirectories);
        var basePkg  = pkgFiles.FirstOrDefault(f =>
            Path.GetFileName(f).Contains("BaseSystem", StringComparison.OrdinalIgnoreCase));

        if (basePkg != null)
        {
            _log(L($"[INFO]   Usando {Path.GetFileName(basePkg)}",
                   $"[INFO]   Using {Path.GetFileName(basePkg)}"));
            return await ExtractFromPkgOrDmgAsync(basePkg, tmpBase, ct);
        }

        // Último recurso: qualquer .dmg que não seja AppleDiagnostics
        var anyDmg = Directory.GetFiles(productFolder, "*.dmg", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(f => !Path.GetFileName(f).Equals(
                "AppleDiagnostics.dmg", StringComparison.OrdinalIgnoreCase));
        if (anyDmg == null)
            throw new Exception(L(
                "Nenhum BaseSystem.dmg utilizável encontrado na pasta do instalador.",
                "No usable BaseSystem.dmg found in the installer folder."));

        _log(L($"[WARN]   Usando arquivo genérico: {Path.GetFileName(anyDmg)}",
               $"[WARN]   Falling back to generic file: {Path.GetFileName(anyDmg)}"));
        return await ExtractFromPkgOrDmgAsync(anyDmg, tmpBase, ct);
    }

    private async Task<string> ExtractFromPkgOrDmgAsync(string startFile, string tmpBase, CancellationToken ct)
    {
        string current = startFile;
        int    pass    = 1;
        const  int maxPasses = 6;

        // Descasca XARs aninhados (pkg → dmg, pkg → pkg → dmg, etc)
        while (await XarExtractor.IsXarAsync(current, ct))
        {
            if (pass > maxPasses)
                throw new Exception(L($"XAR aninhado demais (>{maxPasses} níveis).",
                                       $"XAR nesting too deep (>{maxPasses} levels)."));

            _log(L($"[INFO]   Pass {pass}: extraindo XAR de '{Path.GetFileName(current)}'",
                   $"[INFO]   Pass {pass}: extracting XAR from '{Path.GetFileName(current)}'"));
            var passDir   = Path.Combine(tmpBase, $"pass{pass}");
            var extracted = await XarExtractor.ExtractFirstAsync(
                current, passDir,
                n => n.EndsWith(".dmg", StringComparison.OrdinalIgnoreCase) ||
                     n.EndsWith(".pkg", StringComparison.OrdinalIgnoreCase),
                log:      m => _log(m),
                progress: _progress,
                ct:       ct);

            if (extracted == null)
                throw new Exception(L($"Pass {pass}: nenhum .dmg/.pkg encontrado dentro do XAR.",
                                       $"Pass {pass}: no .dmg/.pkg found inside XAR."));

            current = extracted;
            pass++;
        }

        if (!await DmgExtractor.IsDmgAsync(current, ct))
            throw new Exception(L(
                $"Arquivo final '{Path.GetFileName(current)}' não é UDIF DMG válido.",
                $"Final file '{Path.GetFileName(current)}' is not a valid UDIF DMG."));

        _log(L($"[INFO]   Extraindo HFS+ de '{Path.GetFileName(current)}' (UDIF)",
               $"[INFO]   Extracting HFS+ from '{Path.GetFileName(current)}' (UDIF)"));
        var hfsDir = Path.Combine(tmpBase, $"pass{pass}_hfs");
        var hfs    = await DmgExtractor.ExtractHfsAsync(
            current, hfsDir,
            log:      m => _log(m),
            progress: _progress,
            ct:       ct);
        _log(L($"[OK] Stage 2: HFS+ extraído ({new FileInfo(hfs).Length / (1024.0 * 1024):F0} MB).",
               $"[OK] Stage 2: HFS+ extracted ({new FileInfo(hfs).Length / (1024.0 * 1024):F0} MB)."));
        return hfs;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        nint lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, nint hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        nint lpInBuffer, uint nInBufferSize,
        nint lpOutBuffer, uint nOutBufferSize,
        out uint lpBytesReturned, nint lpOverlapped);

    private const uint FSCTL_LOCK_VOLUME     = 0x00090018;
    private const uint FSCTL_DISMOUNT_VOLUME = 0x00090020;

    private async Task Stage3_WriteImage(int diskIndex, string hfsPath, CancellationToken ct)
    {
        _log(L("[INFO] Stage 3: Gravando imagem no pendrive...",
               "[INFO] Stage 3: Writing image to USB..."));

        long partOffset = 0;
        long partSize   = 0;
        await Task.Run(() =>
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            using var s = new ManagementObjectSearcher(scope,
                new ObjectQuery($"SELECT * FROM MSFT_Partition " +
                                $"WHERE DiskNumber = {diskIndex} AND PartitionNumber = 2"));
            var p = s.Get().Cast<ManagementObject>().FirstOrDefault()
                ?? throw new Exception(L("Partição 2 não encontrada via WMI.",
                                          "Partition 2 not found via WMI."));
            partOffset = Convert.ToInt64(p["Offset"]);
            partSize   = Convert.ToInt64(p["Size"]);
        }, ct);

        const int BufSize = 4 * 1024 * 1024;
        var destPath = $@"\\.\PhysicalDrive{diskIndex}";

        var handle = CreateFileW(
            destPath,
            0xC0000000u,
            0x00000003u,
            nint.Zero,
            3u,
            0xC0000000u,
            nint.Zero);

        if (handle.IsInvalid)
            throw new Exception(L(
                $"Não foi possível abrir {destPath} (Win32 erro {Marshal.GetLastWin32Error()}).",
                $"Could not open {destPath} (Win32 error {Marshal.GetLastWin32Error()})."));

        using var dest = new FileStream(handle, FileAccess.ReadWrite, BufSize, isAsync: true);
        using var src  = new FileStream(hfsPath, FileMode.Open, FileAccess.Read,
                                        FileShare.Read, BufSize, FileOptions.SequentialScan);

        long total = src.Length;
        if (total > partSize)
            throw new Exception(L(
                $"Imagem ({total / (1024.0*1024):F0} MB) maior que partição 2 ({partSize / (1024.0*1024):F0} MB).",
                $"Image ({total / (1024.0*1024):F0} MB) larger than partition 2 ({partSize / (1024.0*1024):F0} MB)."));

        dest.Position = partOffset;
        _log(L(
            $"[INFO]   Escrevendo {total / (1024.0*1024):F0} MB no offset {partOffset / (1024.0*1024):F0} MB do PhysicalDrive{diskIndex}",
            $"[INFO]   Writing {total / (1024.0*1024):F0} MB at offset {partOffset / (1024.0*1024):F0} MB of PhysicalDrive{diskIndex}"));

        long written = 0;
        var  buf     = new byte[BufSize];

        int read;
        string stageLabel = L("Stage 3 — gravando no pendrive", "Stage 3 — writing to USB");
        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await dest.WriteAsync(buf.AsMemory(0, read), ct);
            written += read;
            _progress(written, total, stageLabel);
        }

        await dest.FlushAsync(ct);
        _log(L("[OK] Stage 3: Imagem gravada.", "[OK] Stage 3: Image written."));
    }

    private async Task Stage4_InstallBootloader(int diskIndex, CancellationToken ct)
    {
        _log(L("[INFO] Stage 4: Instalando bootloader (nativo)...",
               "[INFO] Stage 4: Installing bootloader (native)..."));

        long part1Offset = 0;
        string? part1Letter = null;
        await Task.Run(() =>
        {
            var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
            scope.Connect();
            using var s = new ManagementObjectSearcher(scope,
                new ObjectQuery($"SELECT * FROM MSFT_Partition " +
                                $"WHERE DiskNumber = {diskIndex} AND PartitionNumber = 1"));
            var p = s.Get().Cast<ManagementObject>().FirstOrDefault()
                ?? throw new Exception(L("Partição 1 não encontrada via WMI.",
                                          "Partition 1 not found via WMI."));
            part1Offset = Convert.ToInt64(p["Offset"]);
            var letter  = Convert.ToChar(p["DriveLetter"] ?? '\0');
            if (letter != '\0') part1Letter = letter.ToString();
        }, ct);

        if (part1Letter != null)
        {
            var destBoot = Path.Combine(part1Letter + ":\\", "boot");
            await File.WriteAllBytesAsync(destBoot, BootResources.Boot, ct);
            _log(L($"[INFO]   'boot' ({BootResources.Boot.Length / 1024} KB) copiado para {part1Letter}:\\",
                   $"[INFO]   'boot' ({BootResources.Boot.Length / 1024} KB) copied to {part1Letter}:\\"));
        }
        else
        {
            _log(L("[WARN]   Partição 1 sem letra — arquivo boot não copiado.",
                   "[WARN]   Partition 1 has no drive letter — boot file not copied."));
        }

        await Task.Run(() =>
        {
            var handle = CreateFileW(
                $@"\\.\PhysicalDrive{diskIndex}",
                0xC0000000u, 0x00000003u, nint.Zero, 3u, 0x80000000u, nint.Zero);

            if (handle.IsInvalid)
                throw new Exception(L(
                    $"Stage 4 MBR: erro Win32 {Marshal.GetLastWin32Error()}.",
                    $"Stage 4 MBR: Win32 error {Marshal.GetLastWin32Error()}."));

            using var fs = new FileStream(handle, FileAccess.ReadWrite, 512);
            var mbr = new byte[512];
            fs.Position = 0;
            fs.ReadExactly(mbr);

            Buffer.BlockCopy(BootResources.Boot0, 0, mbr, 0, 446);
            mbr[510] = 0x55;
            mbr[511] = 0xAA;

            fs.Position = 0;
            fs.Write(mbr);
            fs.Flush();
        }, ct);
        _log(L("[INFO]   MBR (boot0, 446 bytes) gravado, partition table preservada.",
               "[INFO]   MBR (boot0, 446 bytes) written, partition table preserved."));

        if (part1Letter != null)
        {
            await Task.Run(() =>
            {
                var volPath = $@"\\.\{part1Letter}:";
                var volHandle = CreateFileW(volPath,
                    0xC0000000u, 0x00000003u, nint.Zero, 3u, 0x80000000u, nint.Zero);

                if (volHandle.IsInvalid)
                    throw new Exception(L(
                        $"Stage 4 PBR: erro abrindo {volPath} ({Marshal.GetLastWin32Error()}).",
                        $"Stage 4 PBR: error opening {volPath} ({Marshal.GetLastWin32Error()})."));

                if (!DeviceIoControl(volHandle, FSCTL_LOCK_VOLUME,
                        nint.Zero, 0, nint.Zero, 0, out _, nint.Zero))
                {
                    _log(L($"[WARN]   FSCTL_LOCK_VOLUME falhou ({Marshal.GetLastWin32Error()}), tentando dismount...",
                           $"[WARN]   FSCTL_LOCK_VOLUME failed ({Marshal.GetLastWin32Error()}), trying dismount..."));
                    DeviceIoControl(volHandle, FSCTL_DISMOUNT_VOLUME,
                        nint.Zero, 0, nint.Zero, 0, out _, nint.Zero);
                    DeviceIoControl(volHandle, FSCTL_LOCK_VOLUME,
                        nint.Zero, 0, nint.Zero, 0, out _, nint.Zero);
                }

                using var fs = new FileStream(volHandle, FileAccess.ReadWrite, 512);
                var vbr = new byte[512];
                fs.Position = 0;
                fs.ReadExactly(vbr);

                Buffer.BlockCopy(BootResources.Boot1f32, 0,  vbr, 0,  3);
                Buffer.BlockCopy(BootResources.Boot1f32, 90, vbr, 90, 420);
                vbr[510] = 0x55;
                vbr[511] = 0xAA;

                fs.Position = 0;
                fs.Write(vbr);
                fs.Flush();
            }, ct);
            _log(L("[INFO]   PBR (boot1f32) gravado via volume com lock.",
                   "[INFO]   PBR (boot1f32) written via locked volume."));
        }
        else
        {
            _log(L("[WARN]   Partição 1 sem letra, PBR não gravado.",
                   "[WARN]   Partition 1 has no drive letter, PBR not written."));
        }

        _log(L("[OK] Stage 4: Bootloader instalado.", "[OK] Stage 4: Bootloader installed."));
    }
}
