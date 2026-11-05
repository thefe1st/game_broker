using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed class BrokerTheme : IHudText
    {
        public readonly Color Canvas = Hex("#0B1220"), Surface = Hex("#172437"), Border = Hex("#384B63");
        public readonly Color Ink = Hex("#F3F7FB"), Muted = Hex("#BAC6D7"), Gold = Hex("#E9B56D"), Green = Hex("#79D7BA"), Red = Hex("#FFABAD"), Accent = Hex("#83E4D1");
        public GUIStyle Text, Small, Title, Heading, Button, Primary, Field, Card, Tag, Display, Metric, PanelStyle;
        // Exploration HUD (Core/HudPlan): text and pictograms straight over the world (no cards) in a warm white with a dark
        // drop shadow and gold accents, over the bright voxel city. HudShadow tints the two shadow passes of the same text or
        // pictogram; HudTrack is the meter's track.
        public readonly Color HudInk = Hex(HudPlan.ToneHex(HudTone.Ink)), HudShadow = new Color(0.04f, 0.025f, 0.02f, 0.8f), HudTrack = new Color(0.08f, 0.05f, 0.04f, 0.45f);
        // The pictograms' textures (white, tinted by GUI.color): this many pixels a side, mipmapped for small screens.
        public const int IconPixels = 96;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly Dictionary<int, GUIStyle> hudStyles = new Dictionary<int, GUIStyle>();
        private readonly Dictionary<string, float>[] hudWidths = new Dictionary<string, float>[6];
        private readonly Dictionary<int, Texture2D> icons = new Dictionary<int, Texture2D>();
        private readonly Color[] hudTones = new Color[5];
        private Font font;
        private bool ownsFont;
        public BrokerTheme()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Liberation Sans", "DejaVu Sans" }, 20);
            ownsFont = font != null;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text = Label(20, Ink); Small = Label(16, Muted); Title = Label(32, Ink, FontStyle.Bold);
            Heading = Label(22, Ink, FontStyle.Bold); Tag = Label(16, Accent, FontStyle.Bold);
            Display = Label(52, Ink, FontStyle.Bold); Metric = Label(36, Ink, FontStyle.Bold);
            for (int i = 0; i < hudTones.Length; i++) hudTones[i] = Hex(HudPlan.ToneHex((HudTone)i));
            PanelStyle = new GUIStyle { border = new RectOffset(10, 10, 10, 10) };
            PanelStyle.normal.background = Rounded(Surface);
            Button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18, wordWrap = true,
                border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(16, 16, 8, 8) };
            Button.normal.background = Rounded(Hex("#293B52")); Button.normal.textColor = Ink;
            Button.hover.background = Rounded(Hex("#3B526C")); Button.hover.textColor = Ink;
            Button.active.background = Rounded(Hex("#466581")); Button.active.textColor = Ink;
            Button.focused.background = Button.hover.background; Button.focused.textColor = Ink;
            Button.onNormal.background = Rounded(Hex("#355A66")); Button.onNormal.textColor = Ink;
            Button.onHover.background = Button.hover.background; Button.onHover.textColor = Ink;
            Button.onActive.background = Button.active.background; Button.onActive.textColor = Ink;
            Button.onFocused.background = Button.onNormal.background; Button.onFocused.textColor = Ink;
            Primary = new GUIStyle(Button);
            Primary.normal.background = Rounded(Accent); Primary.normal.textColor = Canvas;
            Primary.hover.background = Rounded(Hex("#B0F3E4")); Primary.hover.textColor = Canvas;
            Primary.active.background = Rounded(Hex("#61BCAE")); Primary.active.textColor = Canvas;
            Primary.focused.background = Primary.hover.background; Primary.focused.textColor = Canvas;
            Card = new GUIStyle(Button) { alignment = TextAnchor.UpperLeft };
            Field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 20, wordWrap = true,
                border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(16, 16, 12, 8) };
            Field.normal.background = Rounded(Hex("#0E192B")); Field.normal.textColor = Ink;
            Field.focused.background = Rounded(Hex("#263F55")); Field.focused.textColor = Ink;
        }
        private GUIStyle Label(int size, Color color, FontStyle weight = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, wordWrap = true, fontStyle = weight, richText = false,
                alignment = alignment, padding = new RectOffset(0, 0, 0, 0) };
            style.normal.textColor = color; return style;
        }
        // A HUD type size in white (the item's tone tints it through GUI.contentColor), made on first use.
        public GUIStyle HudStyle(HudFont font, HudAlign align)
        {
            int key = (int)font * 4 + (int)align;
            if (hudStyles.TryGetValue(key, out var style)) return style;
            var anchor = align == HudAlign.Middle ? TextAnchor.MiddleCenter : align == HudAlign.Center ? TextAnchor.UpperCenter
                : align == HudAlign.Right ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            style = Label(HudPlan.FontSize(font), Color.white, HudPlan.Bold(font) ? FontStyle.Bold : FontStyle.Normal, anchor);
            hudStyles[key] = style; return style;
        }
        public Color HudColor(HudTone tone) => hudTones[(int)tone];
        // IHudText for the HUD's layout: one line's width in the HUD font, remembered per string.
        public float Width(string text, HudFont font)
        {
            var cache = hudWidths[(int)font] ?? (hudWidths[(int)font] = new Dictionary<string, float>());
            text = text ?? "";
            if (cache.TryGetValue(text, out float width)) return width;
            if (cache.Count > 256) cache.Clear();
            width = HudStyle(font, HudAlign.Left).CalcSize(new GUIContent(text)).x; cache[text] = width; return width;
        }
        // A pictogram of Core/HudIcons as a white texture, turned by `sector` steps of HudIcons.Sectors (the arrow), made on
        // first use and destroyed with the theme.
        public Texture2D Icon(HudIcon icon, int sector = 0)
        {
            int key = (int)icon * HudIcons.Sectors + sector;
            if (icons.TryGetValue(key, out var texture)) return texture;
            var mask = HudIcons.Mask(icon, IconPixels, sector * 360f / HudIcons.Sectors);
            var pixels = new Color32[mask.Length];
            for (int y = 0; y < IconPixels; y++)
                for (int x = 0; x < IconPixels; x++)
                    pixels[(IconPixels - 1 - y) * IconPixels + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(mask[y * IconPixels + x] * 255));
            texture = new Texture2D(IconPixels, IconPixels, TextureFormat.RGBA32, true) { name = "HUD / " + icon, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels); texture.Apply(true, true);
            icons[key] = texture; textures.Add(texture); return texture;
        }
        private Texture2D Rounded(Color color)
        {
            const int size = 32; const float radius = 10;
            var texture = new Texture2D(size, size); texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 15.5f) - 5.5f, 0), dy = Mathf.Max(Mathf.Abs(y - 15.5f) - 5.5f, 0);
                var pixel = color; pixel.a *= Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy)); texture.SetPixel(x, y, pixel);
            }
            texture.Apply(); textures.Add(texture); return texture;
        }
        public void Dispose() { foreach (var texture in textures) Object.Destroy(texture); if (ownsFont && font != null) Object.Destroy(font); }
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
    }
}
