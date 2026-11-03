using System;

namespace Broker.Core
{
    // Pixel-art ground and wall textures built from the voxel palette: 32 × 32 texels, about 16 per metre.
    // Unity uploads them with point filtering, so floors and walls read as the same chunky world as the props.
    public static class VoxelSurfaces
    {
        public const int Size = 32;
        public const float MetresPerTile = 2f;
        private static readonly string[] keys = { "wood", "plaster", "concrete", "pavers", "asphalt", "brick", "grass", "carpet", "roof", "windows" };
        // Distant facades tile two 3 m floors with two windows each; everything else is about 16 texels per metre.
        public static float TileMetres(string key) => key == "windows" ? 6f : MetresPerTile;
        public static bool Has(string key) => Array.IndexOf(keys, key) >= 0;
        public static string[] Keys => (string[])keys.Clone();

        // Palette index for one texel; x right, y up (texture rows from the bottom).
        public static byte Texel(string key, int x, int y)
        {
            x = ((x % Size) + Size) % Size; y = ((y % Size) + Size) % Size;
            float n = VoxelNoise.Value(x, y, 0, Seed(key));
            switch (key)
            {
                case "wood":
                {
                    int row = y / 4, offset = row % 2 == 0 ? 0 : 16, plankEnd = (x + offset + row * 5) % 32;
                    if (y % 4 == 3 || plankEnd == 0) return VoxelPalette.WoodDark;
                    float plank = VoxelNoise.Value((x + offset + row * 5) / 32, row, 1, 91);
                    if (n > 0.9f) return VoxelPalette.WoodDark;
                    return plank < 0.4f ? VoxelPalette.Wood : plank < 0.8f ? VoxelPalette.WoodLight : VoxelPalette.Wood;
                }
                case "plaster":
                    return n > 0.9f ? VoxelPalette.PlasterWarm : n < 0.08f ? VoxelPalette.Trim : VoxelPalette.Plaster;
                case "concrete":
                    if (x == 0 || y == 0) return VoxelPalette.Curb;
                    return n > 0.92f ? VoxelPalette.StoneLight : n < 0.1f ? VoxelPalette.Stone : VoxelPalette.Concrete;
                case "pavers":
                {
                    if (x % 8 == 0 || y % 8 == 0) return VoxelPalette.Stone;
                    float tile = VoxelNoise.Value(x / 8, y / 8, 2, 17);
                    if (n > 0.94f) return VoxelPalette.StoneLight;
                    return tile < 0.35f ? VoxelPalette.StoneLight : tile < 0.8f ? VoxelPalette.Concrete : VoxelPalette.Curb;
                }
                case "asphalt":
                    return n > 0.93f ? VoxelPalette.Metal : n < 0.06f ? VoxelPalette.StoneDark : VoxelPalette.Asphalt;
                case "brick":
                {
                    int row = y / 4, shift = row % 2 == 0 ? 0 : 4;
                    if (y % 4 == 3 || (x + shift) % 8 == 7) return VoxelPalette.StoneLight;
                    float brick = VoxelNoise.Value((x + shift) / 8, row, 3, 23);
                    return brick < 0.2f ? VoxelPalette.BrickDark : n > 0.93f ? VoxelPalette.BrickDark : VoxelPalette.BrickRed;
                }
                case "grass":
                    if (n > 0.995f) return VoxelPalette.FlowerYellow;
                    if (n > 0.99f) return VoxelPalette.FlowerWhite;
                    return n > 0.72f ? VoxelPalette.GrassLight : n < 0.2f ? VoxelPalette.GrassDark : VoxelPalette.GrassMid;
                case "carpet":
                    return (x / 4 + y / 4) % 2 == 0 ? VoxelPalette.Navy : n > 0.5f ? VoxelPalette.Navy : VoxelPalette.Ink;
                case "windows":
                {
                    int wx = x % 16, wy = y % 16;
                    if (wy < 3) return VoxelPalette.Stone;
                    if (wx < 4 || wx > 11 || wy < 5 || wy > 12) return VoxelPalette.Concrete;
                    float pane = VoxelNoise.Value(x / 16, y / 16, 4, 29);
                    return pane < 0.18f ? VoxelPalette.WindowLit : pane < 0.7f ? VoxelPalette.GlassDark : VoxelPalette.Glass;
                }
                case "roof":
                    if (y % 6 == 5) return VoxelPalette.Ink;
                    return n > 0.85f ? VoxelPalette.MetalDark : VoxelPalette.RoofDark;
                default: throw new ArgumentException("Unknown voxel surface: " + key, nameof(key));
            }
        }
        // RGBA32 rows bottom to top, ready for Texture2D.SetPixels32.
        public static byte[] Rgba(string key)
        {
            if (!Has(key)) throw new ArgumentException("Unknown voxel surface: " + key, nameof(key));
            var data = new byte[Size * Size * 4];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                VoxelPalette.Rgb(Texel(key, x, y), out byte r, out byte g, out byte b);
                int i = (y * Size + x) * 4; data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = 255;
            }
            return data;
        }
        private static int Seed(string key) { int h = 17; foreach (char c in key) h = h * 31 + c; return h; }
    }
}
