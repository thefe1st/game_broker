using System.Collections.Generic;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed class BrokerTheme
    {
        public readonly Color Canvas = Hex("#101D24"), Surface = Hex("#1A2C34"), Border = Hex("#3F535A");
        public readonly Color Ink = Hex("#F5EEDF"), Muted = Hex("#BBC6C8"), Gold = Hex("#E8B96B"), Green = Hex("#8AD5B3"), Red = Hex("#FFB09D");
        public GUIStyle Text, Small, Title, Heading, Button, Primary, Field, Card, Tag;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private Font font;
        private bool ownsFont;
        public BrokerTheme()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Liberation Sans", "DejaVu Sans" }, 18);
            ownsFont = font != null;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text = Label(18, Ink); Small = Label(15, Muted); Title = Label(26, Ink, FontStyle.Bold);
            Heading = Label(20, Ink, FontStyle.Bold); Tag = Label(15, Gold, FontStyle.Bold);
            Button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16, wordWrap = true, padding = new RectOffset(12, 12, 6, 6) };
            Button.normal.background = Solid(Hex("#304750")); Button.normal.textColor = Ink;
            Button.hover.background = Solid(Hex("#42616C")); Button.hover.textColor = Ink;
            Button.active.background = Solid(Hex("#567580")); Button.active.textColor = Ink;
            Button.focused.background = Button.hover.background; Button.focused.textColor = Ink;
            Button.onNormal.background = Solid(Hex("#567580")); Button.onNormal.textColor = Ink;
            Button.onHover.background = Button.hover.background; Button.onHover.textColor = Ink;
            Button.onActive.background = Button.active.background; Button.onActive.textColor = Ink;
            Button.onFocused.background = Button.onNormal.background; Button.onFocused.textColor = Ink;
            Primary = new GUIStyle(Button);
            Primary.normal.background = Solid(Gold); Primary.normal.textColor = Canvas;
            Primary.hover.background = Solid(Hex("#F3CF90")); Primary.hover.textColor = Canvas;
            Primary.active.background = Solid(Hex("#C99444")); Primary.active.textColor = Canvas;
            Primary.focused.background = Primary.hover.background; Primary.focused.textColor = Canvas;
            Card = new GUIStyle(Button) { alignment = TextAnchor.UpperLeft };
            Field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 18, wordWrap = true, padding = new RectOffset(12, 12, 10, 6) };
            Field.normal.background = Solid(Hex("#0F2028")); Field.normal.textColor = Ink;
            Field.focused.background = Solid(Hex("#243F49")); Field.focused.textColor = Ink;
        }
        private GUIStyle Label(int size, Color color, FontStyle weight = FontStyle.Normal)
        {
            var style = new GUIStyle(GUI.skin.label) { font = font, fontSize = size, wordWrap = true, fontStyle = weight, richText = false };
            style.normal.textColor = color; return style;
        }
        private Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, color); texture.Apply(); textures.Add(texture); return texture;
        }
        public void Dispose() { foreach (var texture in textures) Object.Destroy(texture); if (ownsFont && font != null) Object.Destroy(font); }
        public static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
    }
}
