using System;

namespace Broker.Core
{
    // Ground and wall textures of the city and the office, painted from the voxel palette in the manner of Schedule I:
    // smooth, low-contrast surfaces with soft seams instead of pixel art. 128 × 128 texels, one tile 2 m (64 texels a
    // metre), distant facades 6 m. Unity filters them bilinearly with mipmaps; every pattern tiles seamlessly.
    public static class VoxelSurfaces
    {
        public const int Size = 128;
        public const float MetresPerTile = 2f;
        private static readonly string[] keys = { "wood", "plaster", "concrete", "pavers", "asphalt", "brick", "grass", "carpet", "roof", "windows", "stucco" };
        // Distant facades tile two 3 m floors with two windows each; everything else is 2 m.
        public static float TileMetres(string key) => key == "windows" ? 6f : MetresPerTile;
        public static bool Has(string key) => Array.IndexOf(keys, key) >= 0;
        public static string[] Keys => (string[])keys.Clone();
        // The palette colour a surface is painted around (its texels stay close to it, seams and windows aside).
        public static byte BaseColor(string key)
        {
            switch (key)
            {
                case "wood": return VoxelPalette.Wood;
                case "plaster": return VoxelPalette.Plaster;
                case "concrete": return VoxelPalette.Concrete;
                case "pavers": return VoxelPalette.Concrete;
                case "asphalt": return VoxelPalette.Asphalt;
                case "brick": return VoxelPalette.BrickRed;
                case "grass": return VoxelPalette.GrassMid;
                case "carpet": return VoxelPalette.Navy;
                case "roof": return VoxelPalette.RoofDark;
                case "windows": return VoxelPalette.Concrete;
                case "stucco": return VoxelPalette.Trim;
                default: throw new ArgumentException("Unknown surface: " + key, nameof(key));
            }
        }

        // sRGB colour (0..1) of one texel; x right, y up (texture rows from the bottom); wraps every Size texels.
        public static void Color(string key, int x, int y, out float r, out float g, out float b)
        {
            x = Wrap(x); y = Wrap(y);
            int seed = Seed(key);
            float soft = Fbm(x, y, seed), fine = Smooth(x, y, 32, seed + 7);
            Rgb c;
            switch (key)
            {
                case "asphalt":
                {
                    // Even dark grey, broad worn patches and a faint grain.
                    float worn = SmoothStep(0.58f, 0.78f, Fbm(x, y, seed + 3));
                    c = Mix(P(VoxelPalette.Asphalt), P(VoxelPalette.StoneDark), 0.18f * worn) * (0.96f + 0.06f * soft + 0.03f * fine);
                    break;
                }
                case "concrete":
                    // Cast slabs of a tile each, joined by a thin soft seam.
                    c = Mix(P(VoxelPalette.Concrete), P(VoxelPalette.StoneLight), 0.25f * soft) * (0.97f + 0.04f * fine);
                    c = Mix(c, P(VoxelPalette.Curb) * 0.92f, 0.55f * Seam(x, Size, 1.2f) + 0.55f * Seam(y, Size, 1.2f));
                    break;
                case "pavers":
                {
                    // Pavement slabs of 1 m, each a shade of its own, grout lines between them.
                    float slab = VoxelNoise.Value(x / 64, y / 64, 2, 17);
                    c = Mix(P(VoxelPalette.Concrete), P(VoxelPalette.StoneLight), 0.15f + 0.45f * slab) * (0.96f + 0.05f * soft + 0.02f * fine);
                    float grout = Math.Max(Seam(x, 64, 1.4f), Seam(y, 64, 1.4f));
                    c = Mix(c, P(VoxelPalette.Stone), 0.75f * grout);
                    break;
                }
                case "grass":
                    // Mown lawn: broad lighter and darker drifts, no single blades.
                    c = Mix(P(VoxelPalette.GrassMid), soft > 0.5f ? P(VoxelPalette.GrassLight) : P(VoxelPalette.GrassDark), Math.Abs(soft - 0.5f) * 0.9f) * (0.97f + 0.05f * fine);
                    break;
                case "plaster":
                    c = Mix(P(VoxelPalette.Plaster), P(VoxelPalette.PlasterWarm), 0.3f * soft) * (0.98f + 0.03f * fine);
                    break;
                case "stucco":
                    // Near-white render for tinted facades: the building's colour comes from the material, the texture only
                    // adds soft trowel clouds.
                    c = Mix(P(VoxelPalette.Trim), P(VoxelPalette.Cream), 0.5f * soft) * (0.96f + 0.04f * Fbm(x, y, seed + 9) + 0.02f * fine);
                    break;
                case "brick":
                {
                    // Rows of 0.25 m, bricks 0.5 m, every other row shifted by half a brick; light mortar.
                    int row = y / 16, shift = row % 2 == 0 ? 0 : 16;
                    float brick = VoxelNoise.Value(Wrap(x + shift) / 32, row, 3, 23);
                    c = Mix(P(VoxelPalette.BrickRed), P(VoxelPalette.BrickDark), 0.45f * brick) * (0.95f + 0.06f * soft + 0.03f * fine);
                    float mortar = Math.Max(Seam(y, 16, 1.3f), Seam(x + shift, 32, 1.3f));
                    c = Mix(c, P(VoxelPalette.StoneLight) * 0.9f, 0.85f * mortar);
                    break;
                }
                case "wood":
                {
                    // Floor boards 0.25 m wide and 2 m long in a staggered pattern, with grain along them.
                    int row = y / 16, offset = (row % 2 == 0 ? 0 : 64) + row * 20;
                    float plank = VoxelNoise.Value(Wrap(x + offset) / 128, row, 1, 91);
                    float grain = Smooth(x, y * 8, 16, seed + 11);
                    c = Mix(P(VoxelPalette.Wood), P(VoxelPalette.WoodLight), 0.2f + 0.5f * plank) * (0.92f + 0.1f * grain + 0.03f * fine);
                    float joint = Math.Max(Seam(y, 16, 1.1f), Seam(x + offset, 128, 1.1f));
                    c = Mix(c, P(VoxelPalette.WoodDark), 0.8f * joint);
                    break;
                }
                case "carpet":
                {
                    // Office carpet tiles of 0.5 m laid in alternating directions: a hint of a checker.
                    bool turned = (x / 32 + y / 32) % 2 == 0;
                    float pile = turned ? Smooth(x * 4, y, 32, seed + 5) : Smooth(x, y * 4, 32, seed + 5);
                    c = Mix(P(VoxelPalette.Navy), P(VoxelPalette.Ink), 0.25f + 0.25f * pile) * (turned ? 1.03f : 0.97f);
                    break;
                }
                case "roof":
                    // Roofing sheets 0.5 m apart with a soft dark joint.
                    c = Mix(P(VoxelPalette.RoofDark), P(VoxelPalette.MetalDark), 0.3f * soft) * (0.97f + 0.04f * fine);
                    c = Mix(c, P(VoxelPalette.Ink), 0.6f * Seam(y, 32, 1.2f));
                    break;
                case "windows":
                {
                    // Two 3 m floors of two 3 m bays: a floor band, a framed window in each bay; one of the four is lit.
                    int bx = x % 64, by = y % 64, window = x / 64 + 2 * (y / 64);
                    bool lit = window == (int)(VoxelNoise.Value(0, 0, 4, 31) * 4);
                    float pane = VoxelNoise.Value(x / 64, y / 64, 4, 29);
                    if (by < 10) { c = P(VoxelPalette.Stone) * (0.97f + 0.04f * fine); break; }
                    c = Mix(P(VoxelPalette.Concrete), P(VoxelPalette.StoneLight), 0.2f * soft);
                    if (bx >= 16 && bx < 48 && by >= 20 && by < 52)
                    {
                        bool frame = bx < 18 || bx >= 46 || by < 22 || by >= 50 || (bx >= 31 && bx < 33);
                        if (frame) { c = P(VoxelPalette.Trim) * 0.85f; break; }
                        // A soft sky reflection, lighter toward the top of each pane.
                        float up = (by - 22) / 28f;
                        c = lit ? P(VoxelPalette.WindowLit) * (0.92f + 0.08f * up)
                            : Mix(P(pane < 0.6f ? VoxelPalette.GlassDark : VoxelPalette.Glass), P(VoxelPalette.GlassSky), 0.12f + 0.28f * up);
                    }
                    break;
                }
                default: throw new ArgumentException("Unknown surface: " + key, nameof(key));
            }
            r = Clamp(c.R); g = Clamp(c.G); b = Clamp(c.B);
        }
        // RGBA32 rows bottom to top, ready for Texture2D.SetPixels32.
        public static byte[] Rgba(string key)
        {
            if (!Has(key)) throw new ArgumentException("Unknown surface: " + key, nameof(key));
            var data = new byte[Size * Size * 4];
            for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++)
            {
                Color(key, x, y, out float r, out float g, out float b);
                int i = (y * Size + x) * 4;
                data[i] = (byte)Math.Round(r * 255); data[i + 1] = (byte)Math.Round(g * 255); data[i + 2] = (byte)Math.Round(b * 255); data[i + 3] = 255;
            }
            return data;
        }

        private readonly struct Rgb
        {
            public readonly float R, G, B;
            public Rgb(float r, float g, float b) { R = r; G = g; B = b; }
            public static Rgb operator *(Rgb c, float k) => new Rgb(c.R * k, c.G * k, c.B * k);
        }
        private static Rgb P(byte index)
        {
            VoxelPalette.Rgb(index, out byte r, out byte g, out byte b);
            return new Rgb(r / 255f, g / 255f, b / 255f);
        }
        private static Rgb Mix(Rgb a, Rgb b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
        }
        private static int Wrap(int v) => ((v % Size) + Size) % Size;
        private static float Clamp(float v) => Math.Max(0, Math.Min(1, v));
        private static float SmoothStep(float a, float b, float x) { float t = Clamp((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        // Coverage (0..1) of a line every `period` texels at texel v, `width` texels wide with a one-texel soft edge.
        private static float Seam(int v, int period, float width)
        {
            float p = ((v % period) + period) % period + 0.5f, d = Math.Min(p, period - p);
            return Clamp(width / 2 + 0.5f - d);
        }
        // Tileable value noise over `cells` lattice cells per texture side, smoothly interpolated; 0..1.
        private static float Smooth(int x, int y, int cells, int seed)
        {
            float fx = (float)x * cells / Size, fy = (float)y * cells / Size;
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            float tx = fx - ix, ty = fy - iy; tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
            float Lattice(int i, int j) => VoxelNoise.Value(((i % cells) + cells) % cells, ((j % cells) + cells) % cells, 0, seed);
            float a = Lattice(ix, iy), b = Lattice(ix + 1, iy), c = Lattice(ix, iy + 1), d = Lattice(ix + 1, iy + 1);
            return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
        }
        private static float Fbm(int x, int y, int seed) => 0.5f * Smooth(x, y, 4, seed) + 0.3f * Smooth(x, y, 8, seed + 1) + 0.2f * Smooth(x, y, 16, seed + 2);
        private static int Seed(string key) { int h = 17; foreach (char c in key) h = h * 31 + c; return h; }
    }
}
