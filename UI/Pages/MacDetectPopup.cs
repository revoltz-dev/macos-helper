using System.Numerics;
using ImGuiNET;
using MacOSHelper.Core;
using MacOSHelper.Models;

namespace MacOSHelper.UI.Pages;

public sealed class MacDetectPopup
{
    private string     _input           = "";
    private MacModel?  _result;
    private bool       _detectAttempted;

    public void Render(App app)
    {
        var center = ImGui.GetMainViewport().GetCenter();
        ImGui.SetNextWindowPos(center, ImGuiCond.Always, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(new Vector2(560, 360), ImGuiCond.Always);

        bool open = app.ShowDetect;
        if (!ImGui.BeginPopupModal("##detect", ref open,
            ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoTitleBar))
        { app.ShowDetect = open; return; }
        app.ShowDetect = open;

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.55f, 0.80f, 1.00f, 1f));
        ImGui.Text(T.DetectMacTitle);
        ImGui.PopStyleColor();
        ImGui.SameLine(ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX() - 22);
        if (ImGui.Button("X##det", new System.Numerics.Vector2(22, 22)))
        {
            app.ShowDetect = false;
            open = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.75f, 0.75f, 0.75f, 1f));
        ImGui.TextWrapped(T.DetectInstructions);
        ImGui.PopStyleColor();
        ImGui.Spacing();

        RenderCmd("sysctl hw.model");
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.50f, 0.50f, 0.50f, 1f));
        ImGui.Text(T.Or);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        RenderCmd("system_profiler SPHardwareDataType | grep 'Model Identifier'");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.70f, 0.70f, 0.70f, 1f));
        ImGui.Text(T.PasteHere);
        ImGui.PopStyleColor();

        var avail = ImGui.GetContentRegionAvail();
        ImGui.SetNextItemWidth(avail.X);
        ImGui.InputTextMultiline("##macpaste",
            ref _input, 4096,
            new Vector2(avail.X, 80),
            ImGuiInputTextFlags.None);

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Button,        new Vector4(0.18f, 0.52f, 0.20f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.22f, 0.64f, 0.25f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new Vector4(0.14f, 0.40f, 0.16f, 1f));
        if (ImGui.Button(T.Detect, new Vector2(100, 28)))
        {
            _detectAttempted = true;
            _result = MacModelDatabase.Parse(_input);
            if (_result != null)
                app.DetectedModel = _result;
        }
        ImGui.PopStyleColor(3);

        ImGui.Spacing();
        if (_detectAttempted || app.DetectedModel != null)
        {
            var m = _result ?? app.DetectedModel;
            if (m != null)
            {
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.07f, 0.17f, 0.09f, 1f));
                ImGui.BeginChild("##detectcard", new Vector2(avail.X, 80), ImGuiChildFlags.Borders);

                float lw = 140f;
                LabelValue(T.Identifier, m.Identifier,   lw, new Vector4(1f, 1f, 1f, 1f));
                LabelValue(T.Model,      m.MarketingName, lw, new Vector4(0.90f, 0.90f, 0.90f, 1f));
                LabelValue(T.MaxMacOS,   $"{m.MaxMacOSName} ({m.MaxVersionString})", lw, new Vector4(0.40f, 0.85f, 1f, 1f));

                ImGui.EndChild();
                ImGui.PopStyleColor();
            }
            else if (_detectAttempted)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.35f, 0.35f, 1f));
                ImGui.TextWrapped(T.ModelNotRecognized);
                ImGui.PopStyleColor();
            }
        }

        ImGui.EndPopup();
    }

    private static void RenderCmd(string cmd)
    {
        string buf = cmd;
        float w = Math.Min(ImGui.CalcTextSize(cmd).X + 20, 280);
        ImGui.PushStyleColor(ImGuiCol.FrameBg,       new Vector4(0.06f, 0.12f, 0.06f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text,           new Vector4(0.35f, 0.95f, 0.40f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.08f, 0.16f, 0.08f, 1f));
        ImGui.SetNextItemWidth(w);
        ImGui.InputText($"##{cmd.GetHashCode()}", ref buf, (uint)cmd.Length + 1, ImGuiInputTextFlags.ReadOnly);
        ImGui.PopStyleColor(3);
    }

    private static void LabelValue(string label, string value, float lw, Vector4 valColor)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.60f, 0.60f, 0.60f, 1f));
        ImGui.Text(label);
        ImGui.PopStyleColor();
        ImGui.SameLine(lw);
        ImGui.PushStyleColor(ImGuiCol.Text, valColor);
        ImGui.Text(value);
        ImGui.PopStyleColor();
    }
}
