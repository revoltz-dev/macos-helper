using System.Runtime.InteropServices;
using System.Numerics;
using ImGuiNET;
using MacOSHelper.Core;

namespace MacOSHelper.UI.Widgets;

public static class LogWidget
{
    private static int _prevLineCount;

    [DllImport("user32.dll")]   static extern bool OpenClipboard(nint hwnd);
    [DllImport("user32.dll")]   static extern bool EmptyClipboard();
    [DllImport("user32.dll")]   static extern nint SetClipboardData(uint fmt, nint mem);
    [DllImport("user32.dll")]   static extern bool CloseClipboard();
    [DllImport("kernel32.dll")] static extern nint GlobalAlloc(uint flags, nint size);
    [DllImport("kernel32.dll")] static extern nint GlobalLock(nint mem);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(nint mem);

    public static void Render(List<string> log, object lockObj, float width = -1f, float height = -1f)
    {
        var avail  = ImGui.GetContentRegionAvail();
        float w    = width  < 0 ? avail.X : width;
        float spacing = ImGui.GetStyle().ItemSpacing.Y;
        float barH = ImGui.GetFrameHeight() + spacing;
        float h    = (height < 0 ? avail.Y : height) - barH;
        h = MathF.Max(h, 40f);

        string[] lines;
        lock (lockObj) lines = log.ToArray();

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.40f, 0.40f, 0.40f, 1f));
        ImGui.Text(T.LinesCount(lines.Length));
        ImGui.PopStyleColor();

        ImGui.SameLine(w - 60f);
        if (ImGui.SmallButton($"{T.Copy}##logcopy") && lines.Length > 0)
            CopyToClipboard(string.Join("\n", lines));

        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.07f, 0.07f, 0.07f, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4f);
        ImGui.BeginChild("##log_widget", new Vector2(w, h), ImGuiChildFlags.Borders,
            ImGuiWindowFlags.HorizontalScrollbar);

        foreach (var line in lines)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, LineColor(line));
            ImGui.TextUnformatted(line);
            ImGui.PopStyleColor();
        }

        if (lines.Length != _prevLineCount)
        {
            _prevLineCount = lines.Length;
            ImGui.SetScrollHereY(1f);
        }

        ImGui.EndChild();
        ImGui.PopStyleVar();
        ImGui.PopStyleColor();
    }

    private static void CopyToClipboard(string text)
    {
        if (!OpenClipboard(nint.Zero)) return;
        try
        {
            EmptyClipboard();
            int charCount = text.Length + 1;
            var hMem = GlobalAlloc(0x42, (nint)(charCount * 2)); // GMEM_MOVEABLE|GMEM_ZEROINIT
            if (hMem == nint.Zero) return;
            var ptr = GlobalLock(hMem);
            if (ptr == nint.Zero) return;
            try { Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length); }
            finally { GlobalUnlock(hMem); }
            SetClipboardData(13, hMem); // CF_UNICODETEXT
        }
        finally { CloseClipboard(); }
    }

    private static Vector4 LineColor(string line)
    {
        if (string.IsNullOrEmpty(line)) return new Vector4(0.75f, 0.75f, 0.75f, 1f);
        var t = line.TrimStart();
        if (t.StartsWith("[OK]",      StringComparison.OrdinalIgnoreCase)) return new Vector4(0.25f, 0.88f, 0.30f, 1f);
        if (t.StartsWith("[ERROR]",   StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("[ERR]",     StringComparison.OrdinalIgnoreCase)) return new Vector4(1.00f, 0.32f, 0.32f, 1f);
        if (t.StartsWith("[WARN]",    StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("[WARNING]", StringComparison.OrdinalIgnoreCase)) return new Vector4(1.00f, 0.82f, 0.18f, 1f);
        if (t.StartsWith("[INFO]",    StringComparison.OrdinalIgnoreCase)) return new Vector4(0.55f, 0.75f, 1.00f, 1f);
        return new Vector4(0.85f, 0.85f, 0.85f, 1f);
    }
}
