using System.Drawing;
using Hexa.NET.ImGui;

namespace BFC.ObisLaser.Design;
using ImGui = Hexa.NET.ImGui.ImGui;

internal static class ImGuiPanel
{
    // Dear ImGui's default font is tiny (13 px). Scale it by the OS display scale, plus a modest
    // readability boost so it is comfortable even at 100% scaling.
    const float ReadabilityBoost = 1.3f;
    static readonly float DisplayScale = GetDisplayScale();

    static float GetDisplayScale()
    {
        try
        {
            using var graphics = Graphics.FromHwnd(IntPtr.Zero);
            return Math.Max(1f, graphics.DpiX / 96f);
        }
        catch { return 1f; }
    }

    public static void Draw(string title, Action content)
    {
        ImGui.SetNextWindowPos(System.Numerics.Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        ImGui.Begin(title, ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        var style = ImGui.GetStyle();
        style.FontScaleDpi = DisplayScale;
        style.FontScaleMain = ReadabilityBoost;
        try { content(); }
        finally { ImGui.End(); }
    }
}
