using System.Numerics;
using ImGuiNET;
using MacOSHelper.Core;
using MacOSHelper.Models;
using MacOSHelper.UI.Widgets;

namespace MacOSHelper.UI.Pages;

public sealed class UsbPage
{
    private List<DiskInfo>  _drives           = new();
    private DiskInfo?       _selectedDrive;
    private int             _selectedDriveIdx = -1;

    private List<string>    _downloadedProducts = new();
    private int             _selectedProductIdx  = -1;

    private List<string>    _log           = new();
    private readonly object _logLock       = new();
    private long            _progressCur;
    private long            _progressTot;
    private string          _progressLabel = "";
    private bool            _running;
    private bool            _showConfirm;
    private bool            _refreshed;

    private string? _downloadsDir;

    public void Render(App app)
    {
        if (!_refreshed) { _downloadsDir = app.DownloadsDir; RefreshDrives(); RefreshProducts(); _refreshed = true; }

        var avail  = ImGui.GetContentRegionAvail();
        float lw   = 110f;
        float comboW = avail.X - lw - 86;

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1f));
        ImGui.Text(T.UsbDriveLabel);
        ImGui.PopStyleColor();
        ImGui.SameLine(lw);

        ImGui.SetNextItemWidth(comboW);
        string drivePrev = _selectedDrive?.DisplayName ?? T.UsbDrivePlaceholder;
        if (ImGui.BeginCombo("##drives", drivePrev))
        {
            for (int i = 0; i < _drives.Count; i++)
            {
                var  d   = _drives[i];
                bool sel = (i == _selectedDriveIdx);
                if (!d.IsRemovable)
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
                string icon = d.IsRemovable ? "[USB]  " : "[EXT]  ";
                if (ImGui.Selectable($"{icon}{d.DisplayName}##{i}", sel))
                { _selectedDriveIdx = i; _selectedDrive = d; }
                if (!d.IsRemovable) ImGui.PopStyleColor();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button($"{T.Refresh}##drives", new Vector2(78, 0)))
            RefreshDrives();

        if (_selectedDrive != null)
        {
            string parts = string.Join("  ", _selectedDrive.Partitions
                .Where(p => p.DriveLetter != null)
                .Select(p => $"{p.DriveLetter} ({p.FileSystem ?? "?"})"));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.45f, 0.45f, 0.45f, 1f));
            ImGui.Text(T.PartitionsLabel(string.IsNullOrEmpty(parts) ? T.NoPartitions : parts));
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1f));
        ImGui.Text(T.InstallerLabel);
        ImGui.PopStyleColor();
        ImGui.SameLine(lw);

        ImGui.SetNextItemWidth(comboW);
        string prodPrev = _selectedProductIdx >= 0 && _selectedProductIdx < _downloadedProducts.Count
            ? Path.GetFileName(_downloadedProducts[_selectedProductIdx])
            : T.InstallerPlaceholder;
        if (ImGui.BeginCombo("##products", prodPrev))
        {
            for (int i = 0; i < _downloadedProducts.Count; i++)
            {
                bool sel = (i == _selectedProductIdx);
                if (ImGui.Selectable(Path.GetFileName(_downloadedProducts[i]) + $"##{i}", sel))
                    _selectedProductIdx = i;
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button($"{T.Search}##prod", new Vector2(78, 0)))
            RefreshProducts();

        if (_downloadedProducts.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.85f, 0.75f, 0.20f, 1f));
            ImGui.Text(T.NoInstallerHint);
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        bool canCreate = _selectedDrive != null &&
                         _selectedProductIdx >= 0 &&
                         !_running;

        if (!canCreate) ImGui.BeginDisabled();
        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.70f, 0.16f, 0.16f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.85f, 0.20f, 0.20f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.55f, 0.12f, 0.12f, 1f));
        if (ImGui.Button(T.CreateBootableUsb, new Vector2(220, 32)))
            _showConfirm = true;
        ImGui.PopStyleColor(3);
        if (!canCreate) ImGui.EndDisabled();

        if (_running)
        {
            ImGui.SameLine();
            if (_progressTot > 0)
            {
                float frac = Math.Clamp((float)(_progressCur / (double)_progressTot), 0f, 1f);
                double mb = _progressCur / (1024.0 * 1024);
                double mt = _progressTot / (1024.0 * 1024);

                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.95f, 0.70f, 0.15f, 1f));
                ImGui.PushStyleColor(ImGuiCol.FrameBg,       new Vector4(0.18f, 0.18f, 0.18f, 1f));
                ImGui.ProgressBar(frac, new Vector2(220, 0), "");
                ImGui.PopStyleColor(2);

                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.20f, 1f));
                ImGui.Text($" {mb:F0} / {mt:F0} MB");
                ImGui.PopStyleColor();

                if (!string.IsNullOrEmpty(_progressLabel))
                {
                    ImGui.SameLine();
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.55f, 0.55f, 1f));
                    ImGui.Text("— " + _progressLabel);
                    ImGui.PopStyleColor();
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.20f, 1f));
                ImGui.Text("  " + T.Working);
                ImGui.PopStyleColor();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        LogWidget.Render(_log, _logLock);

        if (_showConfirm) { ImGui.OpenPopup("##confirm_usb"); _showConfirm = false; }

        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        bool dummyOpen = true;
        if (ImGui.BeginPopupModal("##confirm_usb", ref dummyOpen,
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoTitleBar))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.35f, 0.35f, 1f));
            ImGui.Text(T.ConfirmWipeTitle);
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.85f, 0.20f, 1f));
            ImGui.Text($"  {_selectedDrive?.DisplayName}");
            ImGui.PopStyleColor();
            ImGui.Spacing();
            ImGui.Text(T.ConfirmContinue);
            ImGui.Spacing();

            ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.70f, 0.16f, 0.16f, 1f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.85f, 0.20f, 0.20f, 1f));
            if (ImGui.Button(T.ConfirmYesErase, new Vector2(190, 28)))
            { ImGui.CloseCurrentPopup(); StartCreation(); }
            ImGui.PopStyleColor(2);

            ImGui.SameLine();
            if (ImGui.Button($"{T.Cancel}##conf", new Vector2(90, 28)))
                ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }
    }

    private void RefreshDrives()
    {
        Task.Run(() =>
        {
            var list = DiskService.GetRemovableDisks();
            lock (_logLock) _drives = list;
            _selectedDrive    = null;
            _selectedDriveIdx = -1;
        });
    }

    private void RefreshProducts()
    {
        _downloadedProducts.Clear();
        _selectedProductIdx = -1;
        if (_downloadsDir == null || !Directory.Exists(_downloadsDir)) return;
        foreach (var dir in Directory.GetDirectories(_downloadsDir))
            if (Directory.GetFiles(dir).Length > 0)
                _downloadedProducts.Add(dir);
    }

    private void StartCreation()
    {
        if (_selectedDrive == null || _selectedProductIdx < 0) return;
        var drive   = _selectedDrive;
        var folder  = _downloadedProducts[_selectedProductIdx];
        var creator = new UsbCreator(LogLine, OnProgress);
        _running       = true;
        _progressCur   = 0;
        _progressTot   = 0;
        _progressLabel = "";
        lock (_logLock) _log.Clear();
        Task.Run(async () =>
        {
            try   { await creator.CreateAsync(drive, folder, CancellationToken.None); }
            catch (Exception ex) { LogLine($"[ERROR] {ex.Message}"); }
            finally
            {
                _running       = false;
                _progressCur   = 0;
                _progressTot   = 0;
                _progressLabel = "";
            }
        });
    }

    private void LogLine(string line) { lock (_logLock) _log.Add(line); }

    private void OnProgress(long cur, long tot, string label)
    {
        _progressCur   = cur;
        _progressTot   = tot;
        _progressLabel = label;
    }
}
