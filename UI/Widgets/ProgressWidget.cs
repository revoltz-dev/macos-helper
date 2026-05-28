using System;
using System.Numerics;
using ImGuiNET;
using MacOSHelper.Models;

namespace MacOSHelper.UI.Widgets;

public static class ProgressWidget
{
    public static void Render(DownloadEntry? entry)
    {
        if (entry == null) return;

        var avail = ImGui.GetContentRegionAvail();
        float width = avail.X;

        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.10f, 0.10f, 0.10f, 1.0f));
        ImGui.BeginChild("##progress_widget", new Vector2(width, 110), ImGuiChildFlags.Borders);

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.85f, 0.85f, 0.85f, 1.0f));
        ImGui.Text("Baixando:");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.95f, 0.95f, 0.95f, 1.0f));
        ImGui.TextUnformatted(entry.ProductTitle);
        ImGui.PopStyleColor();

        ImGui.Spacing();

        if (!string.IsNullOrEmpty(entry.CurrentFileName))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1.0f));
            ImGui.TextUnformatted($"Arquivo: {entry.CurrentFileName}");
            ImGui.PopStyleColor();
        }

        float fileProgress = entry.CurrentFileProgress;

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.90f, 0.78f, 0.15f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,       new Vector4(0.20f, 0.20f, 0.20f, 1.0f));

        string filePct = $"{fileProgress * 100f:F1}%%  ({FormatBytes(entry.CurrentFileBytes)} / {FormatBytes(entry.CurrentFileTotal)})";
        ImGui.ProgressBar(fileProgress, new Vector2(width - 20, 16), filePct);

        ImGui.PopStyleColor(2);

        float overallProgress = entry.OverallProgress;

        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(0.22f, 0.75f, 0.28f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,       new Vector4(0.15f, 0.15f, 0.15f, 1.0f));

        string overallPct = $"Total: {overallProgress * 100f:F1}%%  ({FormatBytes(entry.TotalDownloadedBytes)} / {FormatBytes(entry.TotalBytes)})";
        ImGui.ProgressBar(overallProgress, new Vector2(width - 20, 16), overallPct);

        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        if (entry.SpeedBytesPerSec > 0)
        {
            double speedMb = entry.SpeedBytesPerSec / (1024.0 * 1024.0);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.85f, 1.0f, 1.0f));
            ImGui.Text($"Velocidade: {speedMb:F2} MB/s");
            ImGui.PopStyleColor();
            ImGui.SameLine();
        }

        var (statusText, statusColor) = entry.Status switch
        {
            DownloadStatus.Downloading => ("Baixando...", new Vector4(0.85f, 0.75f, 0.15f, 1.0f)),
            DownloadStatus.Completed   => ("Concluído",   new Vector4(0.20f, 0.85f, 0.25f, 1.0f)),
            DownloadStatus.Failed      => ("Erro",        new Vector4(1.0f,  0.30f, 0.30f, 1.0f)),
            DownloadStatus.Cancelled   => ("Cancelado",   new Vector4(0.65f, 0.65f, 0.65f, 1.0f)),
            DownloadStatus.Paused      => ("Pausado",     new Vector4(0.80f, 0.70f, 0.20f, 1.0f)),
            _                          => ("Aguardando",  new Vector4(0.55f, 0.55f, 0.55f, 1.0f))
        };

        ImGui.PushStyleColor(ImGuiCol.Text, statusColor);
        ImGui.Text(statusText);
        ImGui.PopStyleColor();

        if (entry.Status == DownloadStatus.Failed && !string.IsNullOrEmpty(entry.ErrorMessage))
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.45f, 0.45f, 1.0f));
            ImGui.TextUnformatted($" - {entry.ErrorMessage}");
            ImGui.PopStyleColor();
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        double gb = bytes / (1024.0 * 1024.0 * 1024.0);
        if (gb >= 1.0) return $"{gb:F2} GB";
        double mb = bytes / (1024.0 * 1024.0);
        if (mb >= 1.0) return $"{mb:F1} MB";
        double kb = bytes / 1024.0;
        if (kb >= 1.0) return $"{kb:F0} KB";
        return $"{bytes} B";
    }
}
