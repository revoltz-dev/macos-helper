using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using GLFWKeys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

namespace MacOSHelper.UI;

public sealed class ImGuiController : IDisposable
{
    private int _vertexArray;
    private int _vertexBuffer;
    private int _vertexBufferSize;
    private int _indexBuffer;
    private int _indexBufferSize;
    private int _fontTexture;
    private int _shader;
    private int _shaderFontTexLocation;
    private int _shaderProjectionMatrixLocation;

    private int _windowWidth;
    private int _windowHeight;
    private readonly System.Numerics.Vector2 _scaleFactor = System.Numerics.Vector2.One;

    private readonly List<char> _pressedChars = new();

    private const string VertexShaderSource = @"
#version 330 core

uniform mat4 projection_matrix;

layout(location = 0) in vec2 in_position;
layout(location = 1) in vec2 in_texCoord;
layout(location = 2) in vec4 in_color;

out vec4 color;
out vec2 texCoord;

void main()
{
    gl_Position = projection_matrix * vec4(in_position, 0, 1);
    color = in_color;
    texCoord = in_texCoord;
}";

    private const string FragmentShaderSource = @"
#version 330 core

uniform sampler2D in_fontTexture;

in vec4 color;
in vec2 texCoord;

out vec4 outputColor;

void main()
{
    outputColor = color * texture(in_fontTexture, texCoord);
}";

    public ImGuiController(int width, int height)
    {
        _windowWidth  = width;
        _windowHeight = height;

        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();

        unsafe { io.NativePtr->IniFilename = null; }

        var fontPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");

        if (File.Exists(fontPath))
        {
            unsafe
            {
                ushort[] ranges = { 0x0020, 0x00FF, 0x2013, 0x2015, 0 };
                fixed (ushort* pRanges = ranges)
                    io.Fonts.AddFontFromFileTTF(fontPath, 15f, new ImFontConfigPtr(IntPtr.Zero), (IntPtr)pRanges);
            }
        }
        else
        {
            io.Fonts.AddFontDefault();
        }

        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        CreateDeviceObjects();
        SetPerFrameImGuiData(1f / 60f);
    }

    public void Update(GameWindow wnd, float deltaSeconds)
    {
        var io = ImGui.GetIO();

        foreach (var c in _pressedChars)
            io.AddInputCharacter(c);
        _pressedChars.Clear();

        SetPerFrameImGuiData(deltaSeconds);
        UpdateImGuiInput(wnd);

        ImGui.NewFrame();
    }

    public void Render()
    {
        ImGui.Render();
        RenderImDrawData(ImGui.GetDrawData());
    }

    public void WindowResized(int width, int height)
    {
        _windowWidth  = width;
        _windowHeight = height;
    }

    public void MouseScroll(OpenTK.Mathematics.Vector2 offset)
    {
        var io = ImGui.GetIO();
        io.MouseWheelH += offset.X;
        io.MouseWheel  += offset.Y;
    }

    public void PressChar(char keyChar)
    {
        _pressedChars.Add(keyChar);
    }

    public void DestroyDeviceObjects()
    {
        if (_vertexBuffer  != 0) { GL.DeleteBuffer(_vertexBuffer);    _vertexBuffer  = 0; }
        if (_vertexArray   != 0) { GL.DeleteVertexArray(_vertexArray); _vertexArray   = 0; }
        if (_indexBuffer   != 0) { GL.DeleteBuffer(_indexBuffer);     _indexBuffer   = 0; }
        if (_fontTexture   != 0)
        {
            GL.DeleteTexture(_fontTexture);
            _fontTexture = 0;
            ImGui.GetIO().Fonts.SetTexID(IntPtr.Zero);
        }
        if (_shader != 0) { GL.DeleteProgram(_shader); _shader = 0; }
    }

    public void Dispose() => DestroyDeviceObjects();

    private void CreateDeviceObjects()
    {
        _shader = CreateProgram("ImGui", VertexShaderSource, FragmentShaderSource);
        _shaderProjectionMatrixLocation = GL.GetUniformLocation(_shader, "projection_matrix");
        _shaderFontTexLocation          = GL.GetUniformLocation(_shader, "in_fontTexture");

        _vertexArray = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArray);

        _vertexBufferSize = 10_000 * Unsafe.SizeOf<ImDrawVert>();
        _vertexBuffer = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

        _indexBufferSize = 10_000 * sizeof(ushort);
        _indexBuffer = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);

        int stride = Unsafe.SizeOf<ImDrawVert>();
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

        RecreateFontDeviceTexture();
    }

    private void RecreateFontDeviceTexture()
    {
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int _);

        _fontTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, _fontTexture);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                      width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

        io.Fonts.SetTexID((IntPtr)_fontTexture);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        io.Fonts.ClearTexData();
    }

    private void SetPerFrameImGuiData(float deltaSeconds)
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new System.Numerics.Vector2(
            _windowWidth  / _scaleFactor.X,
            _windowHeight / _scaleFactor.Y);
        io.DisplayFramebufferScale = _scaleFactor;
        io.DeltaTime = deltaSeconds > 0 ? deltaSeconds : 1f / 60f;
    }

    private static void UpdateImGuiInput(GameWindow wnd)
    {
        var io    = ImGui.GetIO();
        var mouse = wnd.MouseState;
        var kbd   = wnd.KeyboardState;

        io.MouseDown[0] = mouse[MouseButton.Left];
        io.MouseDown[1] = mouse[MouseButton.Right];
        io.MouseDown[2] = mouse[MouseButton.Middle];
        io.MousePos = new System.Numerics.Vector2(mouse.X, mouse.Y);

        io.KeyCtrl  = kbd.IsKeyDown(GLFWKeys.LeftControl)  || kbd.IsKeyDown(GLFWKeys.RightControl);
        io.KeyAlt   = kbd.IsKeyDown(GLFWKeys.LeftAlt)      || kbd.IsKeyDown(GLFWKeys.RightAlt);
        io.KeyShift = kbd.IsKeyDown(GLFWKeys.LeftShift)    || kbd.IsKeyDown(GLFWKeys.RightShift);
        io.KeySuper = kbd.IsKeyDown(GLFWKeys.LeftSuper)    || kbd.IsKeyDown(GLFWKeys.RightSuper);

        TranslateKey(io, kbd, GLFWKeys.Tab,         ImGuiKey.Tab);
        TranslateKey(io, kbd, GLFWKeys.Left,        ImGuiKey.LeftArrow);
        TranslateKey(io, kbd, GLFWKeys.Right,       ImGuiKey.RightArrow);
        TranslateKey(io, kbd, GLFWKeys.Up,          ImGuiKey.UpArrow);
        TranslateKey(io, kbd, GLFWKeys.Down,        ImGuiKey.DownArrow);
        TranslateKey(io, kbd, GLFWKeys.PageUp,      ImGuiKey.PageUp);
        TranslateKey(io, kbd, GLFWKeys.PageDown,    ImGuiKey.PageDown);
        TranslateKey(io, kbd, GLFWKeys.Home,        ImGuiKey.Home);
        TranslateKey(io, kbd, GLFWKeys.End,         ImGuiKey.End);
        TranslateKey(io, kbd, GLFWKeys.Insert,      ImGuiKey.Insert);
        TranslateKey(io, kbd, GLFWKeys.Delete,      ImGuiKey.Delete);
        TranslateKey(io, kbd, GLFWKeys.Backspace,   ImGuiKey.Backspace);
        TranslateKey(io, kbd, GLFWKeys.Space,       ImGuiKey.Space);
        TranslateKey(io, kbd, GLFWKeys.Enter,       ImGuiKey.Enter);
        TranslateKey(io, kbd, GLFWKeys.Escape,      ImGuiKey.Escape);
        TranslateKey(io, kbd, GLFWKeys.KeyPadEnter, ImGuiKey.KeypadEnter);
        TranslateKey(io, kbd, GLFWKeys.A,           ImGuiKey.A);
        TranslateKey(io, kbd, GLFWKeys.C,           ImGuiKey.C);
        TranslateKey(io, kbd, GLFWKeys.V,           ImGuiKey.V);
        TranslateKey(io, kbd, GLFWKeys.X,           ImGuiKey.X);
        TranslateKey(io, kbd, GLFWKeys.Y,           ImGuiKey.Y);
        TranslateKey(io, kbd, GLFWKeys.Z,           ImGuiKey.Z);
    }

    private static void TranslateKey(ImGuiIOPtr io, KeyboardState kbd, GLFWKeys glKey, ImGuiKey imKey)
    {
        io.AddKeyEvent(imKey, kbd.IsKeyDown(glKey));
    }

    private void RenderImDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0) return;

        int totalVtxSize = drawData.TotalVtxCount * Unsafe.SizeOf<ImDrawVert>();
        if (totalVtxSize > _vertexBufferSize)
        {
            _vertexBufferSize = (int)(totalVtxSize * 1.5f);
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, _vertexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }

        int totalIdxSize = drawData.TotalIdxCount * sizeof(ushort);
        if (totalIdxSize > _indexBufferSize)
        {
            _indexBufferSize = (int)(totalIdxSize * 1.5f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, _indexBufferSize, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }

        GL.GetInteger(GetPName.TextureBinding2D,          out int prevTexture2D);
        GL.GetInteger(GetPName.ActiveTexture,             out int prevActiveTexture);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.GetInteger(GetPName.CurrentProgram,            out int prevProgram);
        GL.GetInteger(GetPName.ArrayBufferBinding,        out int prevArrayBuffer);
        GL.GetInteger(GetPName.ElementArrayBufferBinding, out int prevElementBuffer);
        GL.GetInteger(GetPName.VertexArrayBinding,        out int prevVao);
        GL.GetInteger(GetPName.BlendSrcRgb,               out int prevBlendSrcRgb);
        GL.GetInteger(GetPName.BlendDstRgb,               out int prevBlendDstRgb);
        GL.GetInteger(GetPName.BlendSrcAlpha,             out int prevBlendSrcAlpha);
        GL.GetInteger(GetPName.BlendDstAlpha,             out int prevBlendDstAlpha);
        GL.GetInteger(GetPName.BlendEquationRgb,          out int prevBlendEqRgb);
        GL.GetInteger(GetPName.BlendEquationAlpha,        out int prevBlendEqAlpha);
        bool prevBlendEnabled    = GL.IsEnabled(EnableCap.Blend);
        bool prevCullEnabled     = GL.IsEnabled(EnableCap.CullFace);
        bool prevDepthEnabled    = GL.IsEnabled(EnableCap.DepthTest);
        bool prevScissorEnabled  = GL.IsEnabled(EnableCap.ScissorTest);

        GL.Enable(EnableCap.Blend);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFuncSeparate(BlendingFactorSrc.SrcAlpha,  BlendingFactorDest.OneMinusSrcAlpha,
                             BlendingFactorSrc.One,       BlendingFactorDest.OneMinusSrcAlpha);
        GL.Disable(EnableCap.CullFace);
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.ScissorTest);

        GL.UseProgram(_shader);
        GL.Uniform1(_shaderFontTexLocation, 0);

        float L = drawData.DisplayPos.X;
        float R = drawData.DisplayPos.X + drawData.DisplaySize.X;
        float T = drawData.DisplayPos.Y;
        float B = drawData.DisplayPos.Y + drawData.DisplaySize.Y;

        var proj = new Matrix4(
             2.0f / (R - L),     0.0f,             0.0f, 0.0f,
             0.0f,               2.0f / (T - B),   0.0f, 0.0f,
             0.0f,               0.0f,             -1.0f, 0.0f,
            (R + L) / (L - R), (T + B) / (B - T),  0.0f, 1.0f);

        GL.UniformMatrix4(_shaderProjectionMatrixLocation, false, ref proj);

        GL.BindVertexArray(_vertexArray);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBuffer);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _indexBuffer);

        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            var cmdList = drawData.CmdLists[n];

            GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
                cmdList.VtxBuffer.Size * Unsafe.SizeOf<ImDrawVert>(),
                cmdList.VtxBuffer.Data);

            GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero,
                cmdList.IdxBuffer.Size * sizeof(ushort),
                cmdList.IdxBuffer.Data);

            for (int cmdIdx = 0; cmdIdx < cmdList.CmdBuffer.Size; cmdIdx++)
            {
                var pcmd = cmdList.CmdBuffer[cmdIdx];

                if (pcmd.UserCallback != IntPtr.Zero)
                    throw new NotImplementedException("ImGui UserCallback is not supported.");

                GL.BindTexture(TextureTarget.Texture2D, (int)pcmd.TextureId);

                var clip = pcmd.ClipRect;
                GL.Scissor((int)clip.X,
                           _windowHeight - (int)clip.W,
                           (int)(clip.Z - clip.X),
                           (int)(clip.W - clip.Y));

                if ((ImGui.GetIO().BackendFlags & ImGuiBackendFlags.RendererHasVtxOffset) != 0)
                {
                    GL.DrawElementsBaseVertex(PrimitiveType.Triangles, (int)pcmd.ElemCount,
                        DrawElementsType.UnsignedShort,
                        (IntPtr)(pcmd.IdxOffset * sizeof(ushort)),
                        (int)pcmd.VtxOffset);
                }
                else
                {
                    GL.DrawElements(BeginMode.Triangles, (int)pcmd.ElemCount,
                        DrawElementsType.UnsignedShort,
                        (int)(pcmd.IdxOffset * sizeof(ushort)));
                }
            }
        }

        GL.UseProgram(prevProgram);
        GL.BindTexture(TextureTarget.Texture2D, prevTexture2D);
        GL.ActiveTexture((TextureUnit)prevActiveTexture);
        GL.BindVertexArray(prevVao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, prevArrayBuffer);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, prevElementBuffer);
        GL.BlendEquationSeparate((BlendEquationMode)prevBlendEqRgb, (BlendEquationMode)prevBlendEqAlpha);
        GL.BlendFuncSeparate((BlendingFactorSrc)prevBlendSrcRgb,   (BlendingFactorDest)prevBlendDstRgb,
                             (BlendingFactorSrc)prevBlendSrcAlpha, (BlendingFactorDest)prevBlendDstAlpha);

        if (prevBlendEnabled)   GL.Enable(EnableCap.Blend);       else GL.Disable(EnableCap.Blend);
        if (prevCullEnabled)    GL.Enable(EnableCap.CullFace);    else GL.Disable(EnableCap.CullFace);
        if (prevDepthEnabled)   GL.Enable(EnableCap.DepthTest);   else GL.Disable(EnableCap.DepthTest);
        if (prevScissorEnabled) GL.Enable(EnableCap.ScissorTest); else GL.Disable(EnableCap.ScissorTest);
    }

    private static int CreateProgram(string name, string vertSrc, string fragSrc)
    {
        int program = GL.CreateProgram();
        int vert    = CompileShader(name, ShaderType.VertexShader,   vertSrc);
        int frag    = CompileShader(name, ShaderType.FragmentShader, fragSrc);

        GL.AttachShader(program, vert);
        GL.AttachShader(program, frag);
        GL.LinkProgram(program);

        GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            GL.GetProgramInfoLog(program, out string info);
            throw new Exception($"[ImGui] Program '{name}' link error: {info}");
        }

        GL.DetachShader(program, vert);
        GL.DetachShader(program, frag);
        GL.DeleteShader(vert);
        GL.DeleteShader(frag);
        return program;
    }

    private static int CompileShader(string name, ShaderType type, string src)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, src);
        GL.CompileShader(shader);

        GL.GetShader(shader, ShaderParameter.CompileStatus, out int status);
        if (status == 0)
        {
            GL.GetShaderInfoLog(shader, out string info);
            throw new Exception($"[ImGui] Shader '{name}' ({type}) compile error: {info}");
        }
        return shader;
    }
}
