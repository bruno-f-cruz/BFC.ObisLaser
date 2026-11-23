using System.Numerics;
using Hexa.NET.ImGui;

namespace BFC.ObisLaser.Design;
using ImGui = Hexa.NET.ImGui.ImGui;

/// <summary>
/// The look of the OBIS laser dashboard: a dark, compact palette with Segoe UI and rounded frames,
/// and the small widgets it is built from. Sizes are written for 96 DPI and scaled with <see cref="Px"/>.
/// </summary>
internal static class ObisTheme
{
    const float FontSize = 15f;
    static readonly string FontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeui.ttf");
    static readonly string BoldFontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "segoeuib.ttf");

    public static readonly Vector4 Background = Rgb(0x24, 0x26, 0x2b);
    public static readonly Vector4 Card = Rgb(0x1c, 0x1d, 0x21);
    public static readonly Vector4 Track = Rgb(0x3a, 0x3d, 0x44);
    public static readonly Vector4 Text = Rgb(0xe8, 0xea, 0xed);
    public static readonly Vector4 Muted = Rgb(0x9a, 0xa0, 0xa6);
    public static readonly Vector4 Good = new(0.29f, 0.87f, 0.50f, 1f);
    public static readonly Vector4 Bad = new(1.00f, 0.45f, 0.45f, 1f);
    public static readonly Vector4 Warning = new(0.98f, 0.75f, 0.25f, 1f);
    public static readonly Vector4 Accent = Rgb(0x2f, 0x6f, 0xde);
    public static readonly Vector4 Danger = new(0.76f, 0.25f, 0.29f, 1f);

    static readonly Dictionary<uint, float> animations = new();
    static ImFontPtr boldFont;
    static float dpiScale = 1f;

    /// <summary>The scale of the display, 1 at 96 DPI.</summary>
    public static float Scale => dpiScale;

    /// <summary>The bold font, or the default font if Segoe UI Bold is not installed.</summary>
    public static ImFontPtr BoldFont => boldFont.IsNull ? ImGui.GetFont() : boldFont;

    /// <summary>Converts a size written for 96 DPI to pixels.</summary>
    public static float Px(float logicalPixels) => logicalPixels * dpiScale;

    /// <summary>Sets up the current ImGui context once, then applies the palette and DPI scale. Call at the start of each frame.</summary>
    public static unsafe void Apply(float scale, ref bool initialized)
    {
        var io = ImGui.GetIO();
        io.Handle->ConfigErrorRecoveryEnableAssert = 0;
        if (!initialized)
        {
            initialized = true;
            SetMetrics();
            LoadFonts();
        }

        var style = ImGui.GetStyle();
        var current = Math.Max(0.25f, style.Handle->FontScaleDpi);
        if (Math.Abs(scale - current) >= 0.01f)
        {
            style.ScaleAllSizes(scale / current);
            style.Handle->FontScaleDpi = scale;
        }

        dpiScale = scale;
        ApplyColors();
    }

    static unsafe void SetMetrics()
    {
        var style = ImGui.GetStyle().Handle;
        style->WindowPadding = new Vector2(12f, 12f);
        style->FramePadding = new Vector2(8f, 4f);
        style->ItemSpacing = new Vector2(8f, 6f);
        style->ItemInnerSpacing = new Vector2(6f, 4f);
        style->CellPadding = new Vector2(8f, 4f);
        style->ScrollbarSize = 12f;
        style->GrabMinSize = 14f;
        style->FrameRounding = 4f;
        style->GrabRounding = 4f;
        style->PopupRounding = 4f;
        style->ChildRounding = 6f;
        style->ScrollbarRounding = 6f;
        style->WindowBorderSize = 0f;
        style->ChildBorderSize = 0f;
        style->FrameBorderSize = 0f;
    }

    static unsafe void LoadFonts()
    {
        var fonts = new ImFontAtlasPtr(ImGui.GetIO().Handle->Fonts);
        if (File.Exists(FontPath))
        {
            var font = fonts.AddFontFromFileTTF(FontPath, FontSize);
            if (font != null) ImGui.GetIO().Handle->FontDefault = font;
        }

        if (File.Exists(BoldFontPath))
        {
            var font = fonts.AddFontFromFileTTF(BoldFontPath, FontSize);
            if (font != null) boldFont = new ImFontPtr(font);
        }
    }

    // The visualizer host resets the colors to its own theme, so the palette is applied every frame.
    static unsafe void ApplyColors()
    {
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle().Handle;
        var colors = &style->Colors_0;
        colors[(int)ImGuiCol.WindowBg] = Background;
        colors[(int)ImGuiCol.ChildBg] = new Vector4(0f, 0f, 0f, 0f);
        colors[(int)ImGuiCol.PopupBg] = Rgb(0x1f, 0x20, 0x24);
        colors[(int)ImGuiCol.Border] = Rgb(0x33, 0x35, 0x3b);
        colors[(int)ImGuiCol.FrameBg] = Card;
        colors[(int)ImGuiCol.FrameBgHovered] = Rgb(0x2a, 0x2c, 0x31);
        colors[(int)ImGuiCol.FrameBgActive] = Rgb(0x30, 0x33, 0x39);
        colors[(int)ImGuiCol.Button] = Track;
        colors[(int)ImGuiCol.ButtonHovered] = Rgb(0x48, 0x4c, 0x55);
        colors[(int)ImGuiCol.ButtonActive] = Accent;
        colors[(int)ImGuiCol.CheckMark] = Rgb(0x6c, 0xa0, 0xff);
        colors[(int)ImGuiCol.SliderGrab] = Rgb(0x6c, 0xa0, 0xff);
        colors[(int)ImGuiCol.SliderGrabActive] = Accent;
        colors[(int)ImGuiCol.Header] = Rgb(0x2a, 0x2c, 0x31);
        colors[(int)ImGuiCol.HeaderHovered] = Rgb(0x33, 0x35, 0x3b);
        colors[(int)ImGuiCol.HeaderActive] = Rgb(0x3a, 0x3d, 0x44);
        colors[(int)ImGuiCol.TableHeaderBg] = Card;
        colors[(int)ImGuiCol.TableRowBg] = new Vector4(0f, 0f, 0f, 0f);
        colors[(int)ImGuiCol.TableRowBgAlt] = new Vector4(1f, 1f, 1f, 0.025f);
        colors[(int)ImGuiCol.TableBorderLight] = Rgb(0x33, 0x35, 0x3b);
        colors[(int)ImGuiCol.TableBorderStrong] = Rgb(0x33, 0x35, 0x3b);
        colors[(int)ImGuiCol.Separator] = Rgb(0x33, 0x35, 0x3b);
        colors[(int)ImGuiCol.Text] = Text;
        colors[(int)ImGuiCol.TextDisabled] = Muted;
        style->WindowMenuButtonPosition = ImGuiDir.None;
    }

    /// <summary>Draws text in a color. Unlike ImGui.TextColored, the text is not a format string, so % is shown as is.</summary>
    public static void ColoredText(Vector4 color, string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    public static Vector4 Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f, 1f);

    public static Vector4 WithAlpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, alpha);

    public static uint U32(Vector4 color) => ImGui.ColorConvertFloat4ToU32(color);

    /// <summary>
    /// Approximate color of a wavelength in nanometers, lifted towards white so that deep violet and
    /// red stay legible on a dark background.
    /// </summary>
    public static Vector4 WavelengthColor(double wavelength)
    {
        double r = 0, g = 0, b = 0;
        var w = wavelength;
        if (w < 440) { r = -(w - 440) / (440 - 380); b = 1; }
        else if (w < 490) { g = (w - 440) / (490 - 440); b = 1; }
        else if (w < 510) { g = 1; b = -(w - 510) / (510 - 490); }
        else if (w < 580) { r = (w - 510) / (580 - 510); g = 1; }
        else if (w < 645) { r = 1; g = -(w - 645) / (645 - 580); }
        else { r = 1; }

        static float Lift(double value) => (float)(Math.Max(0, Math.Min(1, value)) * 0.7 + 0.3);
        return new Vector4(Lift(r), Lift(g), Lift(b), 1f);
    }

    /// <summary>A muted, uppercase section title.</summary>
    public static void SectionHeader(string text)
    {
        ImGui.Dummy(new Vector2(0, Px(2)));
        ColoredText(Muted, text.ToUpperInvariant());
    }

    /// <summary>A collapsible section with a muted, uppercase title.</summary>
    public static bool Section(string text, bool defaultOpen)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Muted);
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0f, 0f, 0f, 0f));
        var flags = ImGuiTreeNodeFlags.SpanAvailWidth | (defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
        var open = ImGui.CollapsingHeader(text.ToUpperInvariant(), flags);
        ImGui.PopStyleColor(2);
        return open;
    }

    /// <summary>A labelled value tile, like a dashboard stat.</summary>
    public static void Tile(string id, string label, string value, Vector4 valueColor, Vector2 size, string? detail = null)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Card);
        ImGui.BeginChild(id, size, ImGuiChildFlags.AlwaysUseWindowPadding, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ColoredText(Muted, label);
        ImGui.PushFont(BoldFont, ImGui.GetFontSize() * 1.35f);
        ColoredText(valueColor, value);
        ImGui.PopFont();
        if (detail is not null) ColoredText(Muted, detail);
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    /// <summary>A rounded, outlined button that is filled when selected. Returns true when clicked.</summary>
    public static bool Pill(string label, Vector4 color, bool selected, bool enabled = true)
    {
        ImGui.BeginDisabled(!enabled);
        ImGui.PushStyleColor(ImGuiCol.Button, selected ? color : new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, selected ? color : WithAlpha(color, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, color);
        ImGui.PushStyleColor(ImGuiCol.Text, selected ? new Vector4(0.07f, 0.07f, 0.08f, 1f) : color);
        ImGui.PushStyleColor(ImGuiCol.Border, color);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, Px(12f));
        var clicked = ImGui.Button(label);
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(5);
        ImGui.EndDisabled();
        return clicked;
    }

    /// <summary>A small, filled status badge.</summary>
    public static void Badge(string text, Vector4 color)
    {
        var padding = new Vector2(Px(8), Px(2));
        var size = ImGui.CalcTextSize(text) + padding * 2;
        var position = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(position, position + size, U32(WithAlpha(color, 0.18f)), size.Y * 0.5f);
        draw.AddRect(position, position + size, U32(WithAlpha(color, 0.6f)), size.Y * 0.5f);
        ImGui.SetCursorScreenPos(position + padding);
        ColoredText(color, text);
        ImGui.SetCursorScreenPos(position);
        ImGui.Dummy(size);
    }

    /// <summary>A filled button in a solid color, for primary actions. Returns true when clicked.</summary>
    public static bool ColoredButton(string label, Vector4 color, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, color);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(Math.Min(1f, color.X + 0.08f), Math.Min(1f, color.Y + 0.08f), Math.Min(1f, color.Z + 0.08f), 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(color.X * 0.85f, color.Y * 0.85f, color.Z * 0.85f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 1f, 1f, 1f));
        var clicked = ImGui.Button(label, size);
        ImGui.PopStyleColor(4);
        return clicked;
    }

    /// <summary>
    /// An animated on/off switch. <paramref name="on"/> is the value shown; returns true when clicked,
    /// leaving the change to the caller. A null value is drawn as unknown and cannot be clicked.
    /// </summary>
    public static bool Switch(string id, bool? on, Vector4 onColor, float heightScale = 1f)
    {
        var height = ImGui.GetFrameHeight() * heightScale;
        var width = height * 1.8f;
        var position = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height)) && on.HasValue;
        var hovered = ImGui.IsItemHovered() && on.HasValue;

        var key = ImGui.GetItemID();
        var target = on == true ? 1f : 0f;
        if (!animations.TryGetValue(key, out var t)) t = target;
        var step = ImGui.GetIO().DeltaTime * 8f;
        t = t < target ? Math.Min(target, t + step) : Math.Max(target, t - step);
        animations[key] = t;

        var off = on.HasValue ? Track : WithAlpha(Track, 0.4f);
        var track = Vector4.Lerp(off, onColor, t);
        if (hovered) track = Vector4.Lerp(track, new Vector4(1f, 1f, 1f, 1f), 0.08f);
        var draw = ImGui.GetWindowDrawList();
        var radius = height * 0.5f;
        draw.AddRectFilled(position, position + new Vector2(width, height), U32(track), radius);
        var knob = radius - Px(3);
        var center = new Vector2(position.X + radius + t * (width - 2 * radius), position.Y + radius);
        var knobColor = on.HasValue ? new Vector4(0.97f, 0.97f, 0.98f, 1f) : Muted;
        draw.AddCircleFilled(center, knob, U32(knobColor));
        return clicked;
    }

    /// <summary>A label followed by a switch, aligned with the other rows of a settings list.</summary>
    public static bool SwitchRow(string label, string id, bool? on, string tooltip, float labelWidth)
    {
        var start = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SetItemTooltip(tooltip);
        ImGui.SameLine(start + labelWidth);
        var clicked = Switch(id, on, Accent);
        ImGui.SetItemTooltip(tooltip);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ColoredText(Muted, on switch { true => "On", false => "Off", null => "Unknown" });
        return clicked;
    }
}
