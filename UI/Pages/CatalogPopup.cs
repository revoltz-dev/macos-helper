using System.Numerics;
using ImGuiNET;
using MacOSHelper.Core;
using MacOSHelper.Models;
using MacOSHelper.UI.Widgets;

namespace MacOSHelper.UI.Pages;

public sealed class CatalogPopup
{
    private readonly CatalogService _catalog    = new();
    private          Downloader?    _downloader;

    private List<MacOsProduct> _products = new();
    private bool               _loading;
    private string             _status   = "Clique em 'Carregar' para buscar o catálogo da Apple.";
    private DownloadEntry?     _activeDownload;
    private readonly object    _lock     = new();

    private CatalogSeed _seed = CatalogSeed.PublicRelease;
    private readonly string[] _seedLabels = { "Release Público", "Beta Público", "Customer Seed", "Developer Beta" };

    private string? _downloadsDir;
    private bool    _initialized;

    public void Render(App app)
    {
        if (!_initialized)
        {
            _downloadsDir = app.DownloadsDir;
            _downloader   = new Downloader(_downloadsDir);
            Directory.CreateDirectory(_downloadsDir);
            _initialized  = true;
        }

        var vp = ImGui.GetMainViewport();
        var center = vp.GetCenter();
        var popW   = Math.Max(vp.WorkSize.X * 0.90f, 680f);
        var popH   = Math.Max(vp.WorkSize.Y * 0.88f, 420f);
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(popW, popH), ImGuiCond.Always);

        bool open = app.ShowCatalog;
        if (!ImGui.BeginPopupModal("##catalog", ref open,
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
        { app.ShowCatalog = open; return; }
        app.ShowCatalog = open;

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.80f, 1.00f, 1f));
        ImGui.Text("Catálogo macOS");
        ImGui.PopStyleColor();
        ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - 22);
        if (ImGui.Button("X##cat", new System.Numerics.Vector2(22, 22)))
        {
            app.ShowCatalog = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.Separator();
        ImGui.Spacing();

        bool canLoad = !_loading;
        if (!canLoad) ImGui.BeginDisabled();
        if (ImGui.Button("Carregar Catálogo", new Vector2(140, 26)))
            LoadAsync(app);
        if (!canLoad) ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(140);
        int seedIdx = (int)_seed;
        if (ImGui.Combo("##seed", ref seedIdx, _seedLabels, _seedLabels.Length))
            _seed = (CatalogSeed)seedIdx;

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.60f, 0.60f, 0.60f, 1f));
        ImGui.Text(_loading ? "Carregando..." : _status);
        ImGui.PopStyleColor();

        ImGui.Spacing();

        if (app.DetectedModel != null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.40f, 0.85f, 1f, 1f));
            ImGui.Text($"Filtrado para {app.DetectedModel.Identifier}  (máx {app.DetectedModel.MaxMacOSName})");
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.85f, 0.75f, 0.20f, 1f));
            ImGui.Text("Dica: Detecte seu Mac primeiro para filtrar versões compatíveis.");
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();

        var avail  = ImGui.GetContentRegionAvail();
        float progH  = _activeDownload != null ? 72f : 4f;
        float tableH = avail.Y - progH - 8f;

        if (ImGui.BeginTable("##catagtbl", 5,
            ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit,
            new Vector2(avail.X, tableH)))
        {
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableSetupColumn("Versão",   ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Build",    ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Tamanho",  ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Data",     ImGuiTableColumnFlags.WidthFixed, 82);
            ImGui.TableSetupColumn("Ação",     ImGuiTableColumnFlags.WidthFixed, 90);
            ImGui.TableHeadersRow();

            List<MacOsProduct> visible;
            lock (_lock) visible = Filter(_products, app.DetectedModel);

            foreach (var p in visible)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0); ImGui.Text(p.Title);
                ImGui.TableSetColumnIndex(1); ImGui.Text(p.Build);
                ImGui.TableSetColumnIndex(2); ImGui.Text(p.SizeDisplay);
                ImGui.TableSetColumnIndex(3); ImGui.Text(p.PostDate.ToString("yyyy-MM-dd"));
                ImGui.TableSetColumnIndex(4);

                var isActive    = _activeDownload?.ProductId == p.ProductId;
                var isCompleted = IsCompleted(p);

                if (isCompleted)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.30f, 0.90f, 0.40f, 1f));
                    ImGui.Text("Pronto");
                    ImGui.PopStyleColor();
                }
                else if (isActive)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.65f, 0.15f, 0.15f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.80f, 0.20f, 0.20f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.50f, 0.10f, 0.10f, 1f));
                    if (ImGui.Button($"Cancelar##{p.ProductId}", new Vector2(82, 20)))
                        _activeDownload?.Cts.Cancel();
                    ImGui.PopStyleColor(3);
                }
                else
                {
                    bool busy = _activeDownload != null &&
                                _activeDownload.Status == DownloadStatus.Downloading;
                    if (busy) ImGui.BeginDisabled();
                    if (ImGui.Button($"Baixar##{p.ProductId}", new Vector2(82, 20)))
                        StartDownload(p);
                    if (busy) ImGui.EndDisabled();
                }
            }

            ImGui.EndTable();
        }

        if (_activeDownload != null)
        {
            ImGui.Spacing();
            ProgressWidget.Render(_activeDownload);
        }

        ImGui.EndPopup();
    }

    private static List<MacOsProduct> Filter(List<MacOsProduct> all, MacModel? model)
    {
        if (model == null) return all;
        return all.Where(p =>
        {
            if (p.MajorVersion == 10)
                return p.MajorVersion <= model.MaxMajor &&
                       (model.MaxMajor > 10 || p.MinorVersion <= model.MaxMinor);
            return p.MajorVersion <= model.MaxMajor;
        }).ToList();
    }

    private bool IsCompleted(MacOsProduct p)
    {
        var folder = Path.Combine(_downloadsDir!, SafeFolderName(p));
        return Directory.Exists(folder) &&
               Directory.GetFiles(folder).Length > 0;
    }

    private static string SafeFolderName(MacOsProduct p)
    {
        var name = !string.IsNullOrWhiteSpace(p.Build) ? $"{p.Title} ({p.Build})" : p.Title;
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Trim();
    }

    private void LoadAsync(App app)
    {
        _loading = true;
        _status  = "Carregando...";
        _products.Clear();

        Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg =>
                {
                    lock (_lock) _status = msg;
                });
                var products = await _catalog.FetchProductsAsync(progress, _seed);
                lock (_lock)
                {
                    _products = products;
                    _status   = $"Carregado: {products.Count} versões encontradas.";
                }
            }
            catch (Exception ex)
            {
                lock (_lock) _status = $"Erro: {ex.Message}";
            }
            finally { _loading = false; }
        });
    }

    private void StartDownload(MacOsProduct product)
    {
        var folder = Path.Combine(_downloadsDir!, SafeFolderName(product));
        var entry  = new DownloadEntry
        {
            ProductId    = product.ProductId,
            ProductTitle = product.Title,
            DestFolder   = folder,
            Packages     = product.Packages
        };

        _activeDownload = entry;

        Task.Run(async () =>
        {
            await _downloader!.DownloadProductAsync(entry, entry.Cts.Token);
            if (entry.Status != DownloadStatus.Downloading)
                _activeDownload = null;
        });
    }
}
