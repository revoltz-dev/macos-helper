using System.Reflection;

namespace MacOSHelper.Core;

public static class BootResources
{
    public static byte[] Boot     { get; } = Load("tools.boot");
    public static byte[] Boot0    { get; } = Load("tools.boot0");
    public static byte[] Boot1f32 { get; } = Load("tools.boot1f32");

    private static byte[] Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new Exception($"Recurso embutido '{name}' não encontrado.");
        using var ms = new MemoryStream((int)stream.Length);
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
