using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Smaller low-poly pieces of the city in place of voxel ones, with the same sizes and pivots: the press kiosk of the
    // Exchange square, the boulevard's tunnel portals, and the hazy skyline beyond the playable streets.
    public static partial class BuildingModels
    {
        // ---------- Press kiosk ----------
        // 2.2 × 1.6 × 2.7 m: teal stucco on the wall material, a window of magazine covers over a wooden counter (−Z), a
        // cream panel under it, a side door (+X), the navy band for the scene's «ПРЕССА» (its face 0.8 m in front of the
        // pivot), a cream strip and a striped roof. Pivot at the bottom centre.
        public static readonly WallLook KioskWall = new WallLook("stucco", 0x5E9E9A);
        public const float KioskHalfX = 1.1f, KioskHalfZ = 0.8f, KioskHeight = 2.7f;
        public static PolyMesh Kiosk()
        {
            var b = NewBuilder();
            const float x = KioskHalfX, z0 = -0.7f, z1 = KioskHalfZ, top = 2.1f;
            b.Box(new V3(-x, 0, z0), new V3(x, 0.1f, z1), 0.01f, TrimPart, VoxelPalette.StoneDark);
            var front = new Face(new V3(-x, 0, z0), new V3(1, 0, 0), new V3(0, 0, -1), 2 * x);
            var faces = new[]
            {
                front,
                new Face(new V3(x, 0, z1), new V3(-1, 0, 0), new V3(0, 0, 1), 2 * x),
                new Face(new V3(-x, 0, z1), new V3(0, 0, -1), new V3(-1, 0, 0), z1 - z0),
                new Face(new V3(x, 0, z0), new V3(0, 0, 1), new V3(1, 0, 0), z1 - z0),
            };
            // The display: covers in three rows behind the glass, a light band of glass over them, a cream mullion.
            var display = new Rect(0.2f, 2 * x - 0.2f, 0.9f, 1.9f);
            Reveal(b, front, display, 0.06f);
            Pane(b, front, new Rect(display.U0, display.U1, 1.7f, display.V1), -0.06f, GlassPart);
            Panel(b, front, display.U0, display.U1, display.V0, 1.7f, -0.062f, TrimPart, VoxelPalette.GlassDark, 0.8f);
            byte[] covers = { VoxelPalette.CarRed, VoxelPalette.CarYellow, VoxelPalette.CarBlue, VoxelPalette.CarWhite, VoxelPalette.FlowerPink, VoxelPalette.CarGreen };
            const int columns = 9;
            float cw = (display.U1 - display.U0) / columns;
            for (int row = 0; row < 3; row++)
                for (int c = 0; c < columns; c++)
                {
                    float u0 = display.U0 + c * cw + 0.02f, v0 = display.V0 + row * 0.27f + 0.03f;
                    Panel(b, front, u0, u0 + cw - 0.04f, v0, v0 + 0.22f, -0.058f, TrimPart, covers[(int)(VoxelNoise.Value(c, row, 0, 11) * covers.Length)], 0.95f);
                }
            FaceBox(b, front, x - 0.025f, x + 0.025f, display.V0, display.V1, -0.06f, -0.02f, TrimPart, VoxelPalette.Cream, 0);
            var holes = new List<Rect> { display };
            for (int k = 0; k < 4; k++) WallFace(b, faces[k], 0.1f, top, k == Front ? holes : new List<Rect>());
            b.Box(new V3(-x + 0.1f, 0.8f, -0.86f), new V3(x - 0.1f, 0.9f, z0 + 0.1f), 0.01f, TrimPart, VoxelPalette.Wood);
            Ledge(b, front, 0.2f, 2 * x - 0.2f, 0.2f, 0.7f, 0.02f, VoxelPalette.AwningStripe);
            // Side door with a brass knob.
            var side = faces[Right];
            Ledge(b, side, 0.4f, 1.0f, 0.1f, 1.8f, 0.02f, VoxelPalette.WoodLight, true, false);
            FaceBox(b, side, 0.48f, 0.54f, 0.92f, 0.98f, 0.02f, 0.07f, MetalPart, VoxelPalette.Brass, 0.01f);
            // Sign band, strip and the striped roof.
            b.Box(new V3(-x, top, -KioskHalfZ), new V3(x, 2.4f, z1), 0, TrimPart, VoxelPalette.Navy);
            b.Box(new V3(-x - 0.02f, 2.4f, -KioskHalfZ - 0.02f), new V3(x + 0.02f, 2.5f, z1 + 0.02f), 0.01f, TrimPart, VoxelPalette.AwningStripe);
            for (int k = 0; k < 11; k++)
                b.Box(new V3(-x + 0.2f * k, 2.5f, -0.7f), new V3(-x + 0.2f * (k + 1), 2.6f, 0.7f), 0, TrimPart, k % 2 == 0 ? VoxelPalette.Awning : VoxelPalette.AwningStripe);
            b.Box(new V3(-0.8f, 2.6f, -0.5f), new V3(0.8f, KioskHeight, 0.5f), 0.02f, TrimPart, VoxelPalette.AwningStripe);
            return b.Build(PaletteUv);
        }

        // ---------- Tunnel portal ----------
        // 18 × 9 × 1 m of cast concrete (the wall material) with the 14 × 6.5 m mouth through it over the carriageway, a
        // light stone frame round the mouth with seven small lamps on its lintel, stone joint strips every 2 m over it
        // (down to the base at the ends), a dark base and a coping. Front −Z; pivot at the bottom centre of the slab.
        public static readonly WallLook PortalConcrete = new WallLook("concrete", 0xFFFFFF);
        public const float PortalWidth = 18, PortalHeight = 9, PortalDepth = 1, MouthHalf = 7;
        public static PolyMesh TunnelPortal()
        {
            var b = NewBuilder();
            const float w = PortalWidth, h = PortalHeight, m = MouthHalf, mouthTop = CityScenery.TunnelHeight;
            var faces = Faces(w, PortalDepth);
            var mouth = new Rect(w / 2 - m, w / 2 + m, 0, mouthTop);
            for (int k = 0; k < 4; k++)
            {
                var holes = k == Front || k == Back ? new List<Rect> { mouth } : new List<Rect>();
                WallFace(b, faces[k], 0, ContactHeight, holes); WallFace(b, faces[k], ContactHeight, h, holes);
            }
            Reveal(b, faces[Front], mouth, PortalDepth, false);
            var gaps = new List<(int, float, float)> { (Front, mouth.U0 - 0.5f, mouth.U1 + 0.5f), (Back, mouth.U0, mouth.U1) };
            Belt(b, faces, 0, 0.5f, 0.05f, VoxelPalette.StoneDark, true, gaps);
            Belt(b, faces, h - 0.5f, h, 0.1f, VoxelPalette.StoneLight);
            b.Quad(new V3(-w / 2, h, -PortalDepth / 2), new V3(w / 2, h, -PortalDepth / 2), new V3(w / 2, h, PortalDepth / 2), new V3(-w / 2, h, PortalDepth / 2), Up, TrimPart, VoxelPalette.StoneLight);
            var front = faces[Front];
            Ledge(b, front, mouth.U0 - 0.5f, mouth.U0, 0, mouthTop + 0.5f, 0.08f, VoxelPalette.StoneLight);
            Ledge(b, front, mouth.U1, mouth.U1 + 0.5f, 0, mouthTop + 0.5f, 0.08f, VoxelPalette.StoneLight);
            Ledge(b, front, mouth.U0, mouth.U1, mouthTop, mouthTop + 0.5f, 0.08f, VoxelPalette.StoneLight, false);
            for (int k = -3; k <= 3; k++)
            {
                float u = w / 2 + 2 * k;
                FaceBox(b, front, u - 0.25f, u + 0.25f, mouthTop + 0.1f, mouthTop + 0.4f, 0.08f, 0.16f, GlowPart, VoxelPalette.LampWarm, 0.01f);
            }
            for (int k = -4; k <= 4; k++)
            {
                float u = w / 2 + 2 * k; bool end = Math.Abs(k) == 4;
                Ledge(b, front, u - 0.15f, u + 0.15f, end ? 0.5f : mouthTop + 0.5f, h - 0.5f, 0.03f, VoxelPalette.StoneLight, true, false);
            }
            return b.Build(PaletteUv);
        }

        // ---------- Skyline ----------
        // The hazy blocks beyond the playable streets (CityScenery.Skyline) as one mesh per shade: walls on the distant
        // facade texture (a 6 m tile of two floors), a stone band under the roof line, a flat dark roof and a plant box on
        // the larger roofs. In plan coordinates over the blocks' base; uniformly lit, without contact shade.
        public static readonly WallLook[] SkylineLooks = { new WallLook("windows", 0xD2C6B8), new WallLook("windows", 0xC4C8CC), new WallLook("windows", 0xD4C0AE) };
        public static PolyMesh Skyline(int shade)
        {
            if (shade < 0 || shade >= SkylineLooks.Length) throw new ArgumentOutOfRangeException(nameof(shade));
            var b = NewBuilder(SmoothAngle, VoxelSurfaces.TileMetres("windows")); b.Ambient = p => 1;
            foreach (var block in CityScenery.Skyline)
            {
                if (block.Shade != shade) continue;
                float x0 = block.X - block.Width / 2, x1 = block.X + block.Width / 2, z0 = block.Z - block.Depth / 2, z1 = block.Z + block.Depth / 2, h = block.Height;
                b.Quad(new V3(x0, 0, z0), new V3(x1, 0, z0), new V3(x1, h, z0), new V3(x0, h, z0), new V3(0, 0, -1), WallPart, 0);
                b.Quad(new V3(x0, 0, z1), new V3(x1, 0, z1), new V3(x1, h, z1), new V3(x0, h, z1), new V3(0, 0, 1), WallPart, 0);
                b.Quad(new V3(x0, 0, z0), new V3(x0, 0, z1), new V3(x0, h, z1), new V3(x0, h, z0), new V3(-1, 0, 0), WallPart, 0);
                b.Quad(new V3(x1, 0, z0), new V3(x1, 0, z1), new V3(x1, h, z1), new V3(x1, h, z0), new V3(1, 0, 0), WallPart, 0);
                b.Quad(new V3(x0, h, z0), new V3(x1, h, z0), new V3(x1, h, z1), new V3(x0, h, z1), Up, TrimPart, VoxelPalette.RoofDark);
                b.Box(new V3(x0 - 0.2f, h - 0.5f, z0 - 0.2f), new V3(x1 + 0.2f, h - 0.15f, z1 + 0.2f), 0, TrimPart, VoxelPalette.StoneLight);
                if (block.Width * block.Depth > 150)
                    b.Box(new V3(block.X - 1.5f, h, block.Z - 1.2f), new V3(block.X + 1.5f, h + 1.2f, block.Z + 1.2f), 0, TrimPart, VoxelPalette.MetalDark);
            }
            return b.Build(PaletteUv);
        }
    }
}
