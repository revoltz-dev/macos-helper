using System.Diagnostics;
using System.Runtime.InteropServices;
using ImGuiNET;
using MacOSHelper.Core;
using MacOSHelper.UI.Pages;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using GLFWKeys  = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using NVec2     = System.Numerics.Vector2;
using NVec4     = System.Numerics.Vector4;
using OTKVec2i  = OpenTK.Mathematics.Vector2i;

namespace MacOSHelper.UI;

public sealed class AppWindow : GameWindow
{
    private readonly App            _app;
    private ImGuiController         _controller   = null!;
    private readonly UsbPage        _usbPage      = new();
    private readonly MacDetectPopup _detectPopup  = new();
    private readonly CatalogPopup   _catalogPopup = new();

    private bool                   _wasLoading   = true;
    private readonly Stopwatch     _loadTimer    = new();

    private const int LoadW =  400;
    private const int LoadH =  220;
    private const int MainW =  760;
    private const int MainH =  480;
    private const float FooterH = 22f;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        nint hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_DARK_MODE     = 20;
    private const int DWMWA_DARK_MODE_PRE = 19;

    [DllImport("user32.dll")]   static extern bool OpenClipboard(nint hWnd);
    [DllImport("user32.dll")]   static extern bool CloseClipboard();
    [DllImport("user32.dll")]   static extern nint GetClipboardData(uint fmt);
    [DllImport("kernel32.dll")] static extern nint GlobalLock(nint hMem);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(nint hMem);
    private const uint CF_UNICODETEXT = 13;

    private static string GetClipboardText()
    {
        if (!OpenClipboard(nint.Zero)) return "";
        try
        {
            var handle = GetClipboardData(CF_UNICODETEXT);
            if (handle == nint.Zero) return "";
            var ptr = GlobalLock(handle);
            if (ptr == nint.Zero) return "";
            try   { return Marshal.PtrToStringUni(ptr) ?? ""; }
            finally { GlobalUnlock(handle); }
        }
        finally { CloseClipboard(); }
    }

    public AppWindow(App app)
        : base(
            new GameWindowSettings { UpdateFrequency = 60.0 },
            new NativeWindowSettings
            {
                Title        = "macOS Helper",
                ClientSize   = new OTKVec2i(LoadW, LoadH),
                StartVisible = false,
                StartFocused = true,
                WindowBorder = WindowBorder.Resizable,
                API          = ContextAPI.OpenGL,
                APIVersion   = new Version(3, 3),
                Profile      = ContextProfile.Core,
                Flags        = ContextFlags.ForwardCompatible
            })
    {
        _app = app;
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.ClearColor(0.11f, 0.11f, 0.11f, 1f);

        unsafe
        {
            var hwnd = (nint)GLFW.GetWin32Window(WindowPtr);
            int dark = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_DARK_MODE,     ref dark, sizeof(int));
            DwmSetWindowAttribute(hwnd, DWMWA_DARK_MODE_PRE, ref dark, sizeof(int));

            var mon = GLFW.GetPrimaryMonitor();
            GLFW.GetMonitorWorkarea(mon, out int mx, out int my, out int mw, out int mh);
            Location = new OTKVec2i(mx + (mw - LoadW) / 2, my + (mh - LoadH) / 2);
        }

        WindowBorder = WindowBorder.Fixed;
        _controller  = new ImGuiController(ClientSize.X, ClientSize.Y);
        ApplyStyle();

        GL.Clear(ClearBufferMask.ColorBufferBit);
        SwapBuffers();
        IsVisible = true;

        _loadTimer.Start();
        Task.Run(async () =>
        {
            var minDelay = Task.Delay(3000);
            try
            {
                _app.LoadingStatus = T.LoadingRes;
                _app.LoadingProgress = 0.3f;
                _ = MacOSHelper.Core.BootResources.Boot.Length;
                _app.LoadingProgress = 0.6f;
                _ = MacOSHelper.Core.BootResources.Boot0.Length;
                _ = MacOSHelper.Core.BootResources.Boot1f32.Length;
                _app.LoadingProgress = 1.0f;
                _app.LoadingStatus = T.ReadyDone;
            }
            catch (Exception ex)
            {
                _app.LoadingStatus = T.ErrorFmt(ex.Message);
            }
            await minDelay;
            _app.IsLoading = false;
        });
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        if (_wasLoading && !_app.IsLoading)
        {
            _wasLoading = false;
            ExpandToMain();
        }

        _controller.Update(this, (float)args.Time);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        if (_app.IsLoading)
            RenderLoading();
        else
            RenderMain();

        _controller.Render();
        SwapBuffers();
    }

    private void ExpandToMain()
    {
        WindowBorder = WindowBorder.Resizable;
        unsafe
        {
            var mon = GLFW.GetPrimaryMonitor();
            GLFW.GetMonitorWorkarea(mon, out int mx, out int my, out int mw, out int mh);
            ClientSize = new OTKVec2i(MainW, MainH);
            Location   = new OTKVec2i(mx + (mw - MainW) / 2, my + (mh - MainH) / 2);
        }
    }

    private void RenderLoading()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos);
        ImGui.SetNextWindowSize(vp.WorkSize);
        ImGui.SetNextWindowViewport(vp.ID);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize   | ImGuiWindowFlags.NoMove     |
            ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        ImGui.Begin("##load", flags);

        float winW = ImGui.GetWindowWidth();
        float winH = ImGui.GetWindowHeight();

        float contentH = 108f;
        float topPad   = (winH - contentH - 36f) * 0.42f;
        ImGui.Dummy(new NVec2(0, Math.Max(topPad, 8f)));

        ImGui.SetWindowFontScale(1.45f);
        const string title = "macOS Helper";
        ImGui.PushStyleColor(ImGuiCol.Text, new NVec4(0.55f, 0.80f, 1.00f, 1f));
        ImGui.SetCursorPosX((winW - ImGui.CalcTextSize(title).X) * 0.5f);
        ImGui.Text(title);
        ImGui.PopStyleColor();
        ImGui.SetWindowFontScale(1f);

        ImGui.Spacing();
        ImGui.Spacing();

        float elapsed  = (float)_loadTimer.Elapsed.TotalSeconds;
        float timeProg = Math.Min(elapsed / 3.0f, 1.0f);
        float dispProg = Math.Min(timeProg, _app.LoadingProgress);

        float barX = 24f;
        float barW = winW - barX * 2f;
        ImGui.SetCursorPosX(barX);
        ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new NVec4(0.20f, 0.43f, 0.72f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg,       new NVec4(0.16f, 0.16f, 0.16f, 1f));
        ImGui.ProgressBar(dispProg, new NVec2(barW, 5f), "");
        ImGui.PopStyleColor(2);

        ImGui.Spacing();

        var status = _app.LoadingStatus;
        ImGui.PushStyleColor(ImGuiCol.Text, new NVec4(0.48f, 0.48f, 0.48f, 1f));
        ImGui.SetCursorPosX((winW - ImGui.CalcTextSize(status).X) * 0.5f);
        ImGui.Text(status);
        ImGui.PopStyleColor();

        float footerY = winH - 26f;
        ImGui.SetCursorPosY(footerY);
        ImGui.Separator();
        ImGui.Spacing();
        string pre = T.DevelopedBy;
        float totalFW = ImGui.CalcTextSize(pre).X + ImGui.CalcTextSize("Revoltz").X;
        ImGui.SetCursorPosX((winW - totalFW) * 0.5f);
        ImGui.PushStyleColor(ImGuiCol.Text, new NVec4(0.35f, 0.35f, 0.35f, 1f));
        ImGui.Text(pre);
        ImGui.PopStyleColor();
        ImGui.SameLine(0, 0);
        RenderLink("Revoltz", "https://revoltz.dev");

        ImGui.End();
    }

    private void RenderMain()
    {
        var vp = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(vp.WorkPos);
        ImGui.SetNextWindowSize(vp.WorkSize);
        ImGui.SetNextWindowViewport(vp.ID);

        const ImGuiWindowFlags rootFlags =
            ImGuiWindowFlags.NoTitleBar           | ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize             | ImGuiWindowFlags.NoMove     |
            ImGuiWindowFlags.NoBringToFrontOnFocus | ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoScrollbar          | ImGuiWindowFlags.NoScrollWithMouse;

        ImGui.Begin("##root", rootFlags);

        ImGui.PushStyleColor(ImGuiCol.Text, new NVec4(0.55f, 0.80f, 1.00f, 1f));
        ImGui.Text("macOS Helper");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        float rightEdge = ImGui.GetContentRegionAvail().X + ImGui.GetCursorPosX();

        string catLabel = T.Catalog;
        float  catW     = ImGui.CalcTextSize(catLabel).X + 18;
        ImGui.SetCursorPosX(rightEdge - catW);
        ImGui.PushStyleColor(ImGuiCol.Button,        new NVec4(0.16f, 0.38f, 0.16f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NVec4(0.22f, 0.52f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new NVec4(0.12f, 0.28f, 0.12f, 1f));
        if (ImGui.Button(catLabel))
        { _app.ShowCatalog = true; ImGui.OpenPopup("##catalog"); }
        ImGui.PopStyleColor(3);

        string detLabel = T.DetectMac;
        float  detW     = ImGui.CalcTextSize(detLabel).X + 18;
        ImGui.SameLine();
        ImGui.SetCursorPosX(rightEdge - catW - detW - 6);
        ImGui.PushStyleColor(ImGuiCol.Button,        new NVec4(0.40f, 0.28f, 0.08f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new NVec4(0.54f, 0.38f, 0.10f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  new NVec4(0.30f, 0.20f, 0.06f, 1f));
        if (ImGui.Button(detLabel))
        { _app.ShowDetect = true; ImGui.OpenPopup("##detect"); }
        ImGui.PopStyleColor(3);

        // Dropdown de idioma — entre o titulo e os botoes
        const float langW = 56f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(rightEdge - catW - detW - 6 - langW - 6);
        ImGui.SetNextItemWidth(langW);
        var langs = new[] { "PT", "EN" };
        int langIdx = T.Current == Lang.En ? 1 : 0;
        ImGui.PushStyleColor(ImGuiCol.FrameBg,        new NVec4(0.18f, 0.18f, 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new NVec4(0.26f, 0.26f, 0.32f, 1f));
        if (ImGui.Combo("##lang", ref langIdx, langs, langs.Length))
            _app.SetLanguage(langIdx == 1 ? Lang.En : Lang.Pt);
        ImGui.PopStyleColor(2);

        ImGui.Separator();
        ImGui.Spacing();

        float contentH = ImGui.GetContentRegionAvail().Y - FooterH - 10f;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new NVec4(0, 0, 0, 0));
        ImGui.BeginChild("##content", new NVec2(0, contentH), ImGuiChildFlags.None);
        _usbPage.Render(_app);
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.Separator();
        ImGui.Spacing();

        string fPrefix = T.DevelopedBy;
        float fPrefixW = ImGui.CalcTextSize(fPrefix).X;
        float fNameW   = ImGui.CalcTextSize("Revoltz").X;
        ImGui.SetCursorPosX((ImGui.GetWindowWidth() - fPrefixW - fNameW) * 0.5f);
        ImGui.PushStyleColor(ImGuiCol.Text, new NVec4(0.35f, 0.35f, 0.35f, 1f));
        ImGui.Text(fPrefix);
        ImGui.PopStyleColor();
        ImGui.SameLine(0, 0);
        RenderLink("Revoltz", "https://revoltz.dev");

        _detectPopup.Render(_app);
        _catalogPopup.Render(_app);

        ImGui.End();
    }

    private static void RenderLink(string label, string url)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new NVec4(0.35f, 0.65f, 1.00f, 1f));
        ImGui.Text(label);
        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
        {
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGui.GetWindowDrawList().AddLine(
                new NVec2(min.X, max.Y),
                new NVec2(max.X, max.Y),
                ImGui.ColorConvertFloat4ToU32(new NVec4(0.35f, 0.65f, 1.00f, 1f)));

            if (ImGui.IsItemClicked())
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
                catch { }
            }
        }
    }

    private void ApplyStyle()
    {
        ImGui.StyleColorsDark();
        var s = ImGui.GetStyle();
        s.WindowRounding = 5f;  s.FrameRounding = 4f;
        s.TabRounding    = 4f;  s.ChildRounding  = 4f;
        s.FramePadding   = new NVec2(7, 3);
        s.ItemSpacing    = new NVec2(7, 5);

        var c = s.Colors;
        c[(int)ImGuiCol.WindowBg]         = new NVec4(0.10f, 0.10f, 0.10f, 1f);
        c[(int)ImGuiCol.ChildBg]          = new NVec4(0.13f, 0.13f, 0.13f, 1f);
        c[(int)ImGuiCol.FrameBg]          = new NVec4(0.18f, 0.18f, 0.18f, 1f);
        c[(int)ImGuiCol.FrameBgHovered]   = new NVec4(0.24f, 0.24f, 0.24f, 1f);
        c[(int)ImGuiCol.Button]           = new NVec4(0.20f, 0.43f, 0.72f, 1f);
        c[(int)ImGuiCol.ButtonHovered]    = new NVec4(0.26f, 0.53f, 0.86f, 1f);
        c[(int)ImGuiCol.ButtonActive]     = new NVec4(0.16f, 0.36f, 0.62f, 1f);
        c[(int)ImGuiCol.Header]           = new NVec4(0.20f, 0.43f, 0.72f, 0.55f);
        c[(int)ImGuiCol.HeaderHovered]    = new NVec4(0.20f, 0.43f, 0.72f, 0.75f);
        c[(int)ImGuiCol.Tab]              = new NVec4(0.16f, 0.16f, 0.16f, 1f);
        c[(int)ImGuiCol.TabHovered]       = new NVec4(0.28f, 0.28f, 0.28f, 1f);
        c[(int)ImGuiCol.TabSelected]      = new NVec4(0.20f, 0.43f, 0.72f, 1f);
        c[(int)ImGuiCol.TitleBgActive]    = new NVec4(0.13f, 0.13f, 0.13f, 1f);
        c[(int)ImGuiCol.PopupBg]          = new NVec4(0.11f, 0.11f, 0.11f, 0.97f);
        c[(int)ImGuiCol.ModalWindowDimBg] = new NVec4(0f, 0f, 0f, 0.55f);
        c[(int)ImGuiCol.Separator]        = new NVec4(0.28f, 0.28f, 0.28f, 1f);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == GLFWKeys.V &&
            (KeyboardState.IsKeyDown(GLFWKeys.LeftControl) ||
             KeyboardState.IsKeyDown(GLFWKeys.RightControl)))
        {
            var text = GetClipboardText();
            if (!string.IsNullOrEmpty(text))
                ImGui.GetIO().AddInputCharactersUTF8(text);
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _controller.PressChar((char)e.Unicode);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _controller.MouseScroll(e.Offset);
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        GL.Viewport(0, 0, e.Width, e.Height);
        _controller.WindowResized(e.Width, e.Height);
    }

    protected override void OnUnload()
    {
        base.OnUnload();
        _controller.DestroyDeviceObjects();
        _controller.Dispose();
    }
}
