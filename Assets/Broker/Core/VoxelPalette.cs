using System;

namespace Broker.Core
{
    // One shared 8×8 palette for every voxel model: a face samples the centre of its colour texel,
    // so Unity needs a single point-filtered texture and two materials (plain and glowing).
    // Index 0 is empty space. Indices 56–63 glow (lamps, lit windows, car and signal lights, screens).
    public static class VoxelPalette
    {
        public const int TextureSize = 8, Count = TextureSize * TextureSize, FirstGlow = 56;
        public const byte Empty = 0,
            GrassLight = 1, GrassMid = 2, GrassDark = 3, LeafLight = 4, LeafMid = 5, LeafDark = 6, PineMid = 7, PineDark = 8,
            Bark = 9, BarkDark = 10, Soil = 11, FlowerPink = 12, FlowerYellow = 13, FlowerWhite = 14, StoneLight = 15, Stone = 16,
            StoneDark = 17, Concrete = 18, Asphalt = 19, Curb = 20, MetalLight = 21, Metal = 22, MetalDark = 23, Tire = 24,
            Chrome = 25, Glass = 26, GlassDark = 27, GlassSky = 28, BrickRed = 29, BrickDark = 30, Plaster = 31, PlasterWarm = 32,
            PlasterSage = 33, Trim = 34, RoofDark = 35, Awning = 36, AwningStripe = 37, WoodLight = 38, Wood = 39, WoodDark = 40,
            CarRed = 41, CarBlue = 42, CarYellow = 43, CarWhite = 44, CarGreen = 45, CarBlack = 46, Navy = 47, Amber = 48,
            Ink = 49, Cream = 50, Brass = 51, Skin = 52, SkinShade = 53, HairDark = 54, HairBrown = 55,
            SignalRed = 56, SignalAmber = 57, SignalGreen = 58, LampWarm = 59, WindowLit = 60, Headlight = 61, TailLight = 62, ScreenGreen = 63;

        // sRGB, warm "golden hour" set: no pure black or white, so lighting and haze keep their colour.
        private static readonly uint[] colors = {
            0x000000, 0x8AAA4C, 0x6C8E3C, 0x4E722F, 0x95B455, 0x6D923F, 0x4A7233, 0x3F7046,
            0x2C5536, 0x7A5638, 0x5A3E29, 0x5E4632, 0xE58FA8, 0xF0CF55, 0xEDE7D6, 0xC9C4B8,
            0xA39E94, 0x77736D, 0xB8B2A6, 0x55575C, 0x9A958C, 0xB9BEC2, 0x868C92, 0x4B5157,
            0x2B2C30, 0xD8DCDD, 0x8FB4C8, 0x41566A, 0xA9CBE0, 0xA4553C, 0x7E3F2E, 0xE3D8C3,
            0xE8C99A, 0xA9BFA4, 0xF1ECE0, 0x5B5A60, 0x2F6E73, 0xEFE6D2, 0xC79A66, 0x9C6B42,
            0x6E4A2F, 0xC2413A, 0x3D6FB0, 0xE6B739, 0xE7E5DD, 0x4E8A5A, 0x2E3238, 0x243A55,
            0xD08A3E, 0x1E2A33, 0xEDE3CC, 0xD6A853, 0xE3B48F, 0xC48E6C, 0x3A2B24, 0x7B5233,
            0xFF4A3D, 0xFFB43C, 0x5DEB8A, 0xFFD58A, 0xFFC978, 0xFFF1C4, 0xFF3B30, 0x7CF0D2,
        };
        public static bool Glows(int index) => index >= FirstGlow && index < Count;
        public static void Rgb(int index, out byte r, out byte g, out byte b)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            uint c = colors[index]; r = (byte)(c >> 16); g = (byte)(c >> 8); b = (byte)c;
        }
        // Texel centre: column = index % 8, row = index / 8 from the bottom (Unity texture origin).
        public static float U(int index) => (index % TextureSize + 0.5f) / TextureSize;
        public static float V(int index) => (index / TextureSize + 0.5f) / TextureSize;
        // RGBA32, rows bottom to top, as Texture2D.SetPixels32/LoadRawTextureData expect.
        public static byte[] TextureRgba()
        {
            var data = new byte[Count * 4];
            for (int i = 0; i < Count; i++)
            {
                Rgb(i, out byte r, out byte g, out byte b);
                data[i * 4] = r; data[i * 4 + 1] = g; data[i * 4 + 2] = b; data[i * 4 + 3] = 255;
            }
            return data;
        }
    }
}
