using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Submeshes of a low-poly building: walls (box-mapped in metres; the material picks the surface and tint), palette
    // details, glass that stays dark, night glass — the same glass by day, the windows that light up after dark — and
    // two more palette parts: glowing details (lamps, flames, the LED strip) and polished metal (brass, bronze).
    public enum BuildingPart { Wall, Trim, Glass, Night, Glow, Metal }

    // A wall surface of VoxelSurfaces and the sRGB tint its material multiplies it by.
    public readonly struct WallLook : IEquatable<WallLook>
    {
        public readonly string Surface; public readonly uint Tint;
        public WallLook(string surface, uint tint) { Surface = surface; Tint = tint; }
        public bool Equals(WallLook other) => Surface == other.Surface && Tint == other.Tint;
        public override bool Equals(object obj) => obj is WallLook other && Equals(other);
        public override int GetHashCode() => (Surface ?? "").GetHashCode() * 31 + (int)Tint;
        public override string ToString() => Surface + " #" + Tint.ToString("X6");
    }

    // Low-poly buildings in the manner of Schedule I, built from code like the cars: flat textured walls with framed inset
    // windows, sills and lintels, floor bands, a cornice and a capped parapet, shop fronts with display windows, doors,
    // sign bands and awnings, flat roofs with plant. Metres; pivot at the centre of the footprint on the ground, front −Z.
    public static partial class BuildingModels
    {
        public const int PartCount = 6;
        public const float FloorHeight = 3f, MinFloorHeight = 2.8f, ShopFloorHeight = 4f, PlinthHeight = 1f;
        public const float ParapetHeight = 0.5f, ParapetThickness = 0.25f, CapHeight = 0.08f, CapOverhang = 0.05f, CorniceProud = 0.28f;
        // Share of tower windows that light up after dark; shop fronts and doors always do.
        public const float NightShare = 0.42f;
        // Cornice zone between the top floor and the roof.
        public const float TopZone = 0.9f;
        private const float ContactHeight = 1.2f, SmoothAngle = 25f;
        private const int WallPart = (int)BuildingPart.Wall, TrimPart = (int)BuildingPart.Trim, GlassPart = (int)BuildingPart.Glass, NightPart = (int)BuildingPart.Night,
            GlowPart = (int)BuildingPart.Glow, MetalPart = (int)BuildingPart.Metal;

        private static PolyBuilder NewBuilder(float smoothAngle = SmoothAngle, float tile = VoxelSurfaces.MetresPerTile)
            => new PolyBuilder(PartCount, smoothAngle, WallPart, tile) { Ambient = Contact };
        // The foot of a wall sits in its own contact shade.
        internal static float Contact(V3 p) { float t = Clamp01(p.Y / ContactHeight); return 0.8f + 0.2f * t * t * (3 - 2 * t); }
        // Trim, glow and metal sample their palette texel; glass needs no texture.
        internal static (float, float) PaletteUv(int part, byte color)
            => part == TrimPart || part == GlowPart || part == MetalPart ? (VoxelPalette.U(color), VoxelPalette.V(color)) : (0.5f, 0.5f);
        private static float Rand(int seed, int salt) => VoxelNoise.Value(salt, 13, 5, seed);
        private static T Pick<T>(int seed, int salt, params T[] options) => options[Math.Min(options.Length - 1, (int)(Rand(seed, salt) * options.Length))];

        // ---------- Wall looks ----------
        private static readonly uint[] PlasterTints = { 0xF0D49C, 0xBFD1AE, 0xF0BFA0, 0xB9CEDD, 0xF3E7CF, 0xE7B8B2 };
        private static readonly uint[] BrickTints = { 0xFFFFFF, 0xEBD3C6, 0xCDB2A4 };
        private static readonly uint[] ModernTints = { 0xCFD4D8, 0xDAD5CC, 0xBFC6CC };
        public static WallLook Wall(TowerStyle style, int seed)
        {
            int k = (int)(Rand(seed, 1) * 6);
            switch (style)
            {
                case TowerStyle.Brick: return new WallLook("brick", BrickTints[k % BrickTints.Length]);
                case TowerStyle.Glass: return new WallLook("stucco", ModernTints[k % ModernTints.Length]);
                default: return new WallLook("stucco", PlasterTints[k % PlasterTints.Length]);
            }
        }
        public static WallLook Wall(ShopStyle style)
            => style == ShopStyle.BrickShop ? new WallLook("brick", 0xFFFFFF) : style == ShopStyle.Cafe ? new WallLook("stucco", 0xF0D49C) : new WallLook("stucco", 0xD2D4D2);
        // The block under the player's office keeps the sage green of the voxel office (VoxelPalette.PlasterSage): the
        // author preferred it to the pastel palette's pink (4 November).
        public static readonly WallLook OfficeWall = new WallLook("stucco", 0xA9BFA4);

        // Floors above the ground floor: as many as fit at about 3 m, stretched to fill the height below the cornice.
        public static int Floors(float height, bool shopFloor, out float floorHeight)
        {
            float avail = height - (shopFloor ? ShopFloorHeight : PlinthHeight) - TopZone;
            int n = Math.Max(1, (int)Math.Floor(avail / FloorHeight + 0.5f));
            if (n > 1 && avail / n < MinFloorHeight) n--;
            floorHeight = avail / n; return n;
        }

        // ---------- Towers ----------
        // City tower: brick with white sashes and stone lintels, pastel stucco with floor bands, or a modern block with
        // ribbon windows. Windows on the front and both sides (the back too when asked; otherwise it keeps the bands), a
        // shop front with a door, a sign band and an awning or canopy on the ground floor, a cornice, a parapet and roof plant.
        public static PolyMesh Tower(float width, float depth, float height, TowerStyle style, int seed, bool roofUnits = true, bool shopFloor = true,
            bool backWindows = false)
        {
            if (width < 4 || depth < 4 || width > 60 || depth > 60) throw new ArgumentOutOfRangeException(width < 4 || width > 60 ? nameof(width) : nameof(depth));
            // At least one upper floor over the ground floor and under the cornice.
            if (height > 80 || height < (shopFloor ? ShopFloorHeight : PlinthHeight) + MinFloorHeight + TopZone) throw new ArgumentOutOfRangeException(nameof(height));
            var b = NewBuilder();
            var faces = Faces(width, depth);
            float ground = shopFloor ? ShopFloorHeight : PlinthHeight;
            int floors = Floors(height, shopFloor, out float floorH);
            var t = Trims(style, seed);
            var doorGaps = new List<(int, float, float)>();
            for (int k = 0; k < 4; k++)
            {
                var f = faces[k]; var holes = new List<Rect>();
                bool windows = k != Back || backWindows;
                if (shopFloor && k == Front) { var (d0, d1) = Storefront(b, f, holes, style, seed, t); doorGaps.Add((Front, d0, d1)); }
                else if (shopFloor && windows) GroundWindows(b, f, holes, style, seed, k, t);
                else if (!shopFloor && k == Back && backWindows) { float u = f.Length * 0.3f; Door(b, f, holes, u, u + 1.1f, 2.2f, VoxelPalette.MetalDark, false, GlassPart); doorGaps.Add((Back, u, u + 1.1f)); }
                if (windows)
                    for (int floor = 0; floor < floors; floor++) FloorWindows(b, f, holes, style, seed, k, floor, ground + floor * floorH, floorH, t);
                WallFace(b, f, 0, Math.Min(ContactHeight, height), holes);
                WallFace(b, f, ContactHeight, height + ParapetHeight, holes);
            }
            // Plinth, the band over the ground floor, floor bands, the cornice.
            Belt(b, faces, 0, shopFloor ? 0.32f : 0.42f, 0.035f, t.Plinth, true, doorGaps);
            Belt(b, faces, ground - 0.2f, ground, 0.06f, t.Band);
            if (style != TowerStyle.Brick)
                for (int floor = 1; floor < floors; floor++)
                {
                    float v = ground + floor * floorH;
                    Belt(b, faces, v - 0.08f, v + 0.08f, 0.035f, t.Band);
                }
            float cornice = height - 0.42f;
            Belt(b, faces, cornice - 0.16f, cornice, 0.12f, t.Cornice);
            Belt(b, faces, cornice, height - 0.06f, CorniceProud, t.Cornice);
            Roof(b, width, depth, height, t);
            if (roofUnits) RoofPlant(b, width, depth, height, style, seed);
            return b.Build(PaletteUv);
        }
        private sealed class TowerTrims
        {
            public byte Frame, Sill, Lintel, Band, Cornice, Cap, Plinth, Shopfront, Sign, Door;
        }
        private static TowerTrims Trims(TowerStyle style, int seed)
        {
            var t = new TowerTrims
            {
                Shopfront = Pick(seed, 20, VoxelPalette.MetalDark, VoxelPalette.WoodDark, VoxelPalette.Navy, VoxelPalette.Ink),
                Sign = Pick(seed, 21, VoxelPalette.Navy, VoxelPalette.Ink, VoxelPalette.Awning, VoxelPalette.CarRed, VoxelPalette.WoodDark, VoxelPalette.CarGreen),
                Door = Pick(seed, 22, VoxelPalette.WoodDark, VoxelPalette.Navy, VoxelPalette.MetalDark),
            };
            switch (style)
            {
                case TowerStyle.Brick:
                    t.Frame = VoxelPalette.Trim; t.Sill = VoxelPalette.StoneLight; t.Lintel = VoxelPalette.StoneLight; t.Band = VoxelPalette.StoneLight;
                    t.Cornice = VoxelPalette.StoneLight; t.Cap = VoxelPalette.Stone; t.Plinth = VoxelPalette.StoneDark; break;
                case TowerStyle.Glass:
                    t.Frame = VoxelPalette.MetalDark; t.Sill = VoxelPalette.MetalLight; t.Lintel = 0; t.Band = VoxelPalette.MetalLight;
                    t.Cornice = VoxelPalette.MetalLight; t.Cap = VoxelPalette.Metal; t.Plinth = VoxelPalette.StoneDark; t.Shopfront = VoxelPalette.MetalDark; break;
                default:
                    t.Frame = VoxelPalette.Trim; t.Sill = VoxelPalette.Trim; t.Lintel = 0; t.Band = VoxelPalette.Plaster;
                    t.Cornice = VoxelPalette.Trim; t.Cap = VoxelPalette.Plaster; t.Plinth = VoxelPalette.Stone; break;
            }
            return t;
        }
        private static Sash UpperSash(TowerStyle style, TowerTrims t)
        {
            switch (style)
            {
                case TowerStyle.Brick: return new Sash { FrameColor = t.Frame, Cross = true, SillColor = t.Sill, Lintel = 0.2f, LintelColor = t.Lintel };
                case TowerStyle.Glass: return new Sash { FrameColor = t.Frame, Frame = 0.07f, Bar = 0.07f, MullionStep = 1.5f, SillColor = t.Sill, SillProud = 0.05f, SillHeight = 0.05f, Depth = 0.1f };
                default: return new Sash { FrameColor = t.Frame, Cross = true, TransomAt = 0.72f, SillColor = t.Sill };
            }
        }
        private static int WindowGlass(int seed, int face, int floor, int column)
            => VoxelNoise.Value(face * 31 + column, floor, 5, seed) < NightShare ? NightPart : GlassPart;
        // About a third of the windows have a blind part way down, in one of a few light colours.
        private static Sash WithBlind(Sash sash, int seed, int face, int floor, int column)
        {
            float n = VoxelNoise.Value(face * 31 + column, floor, 7, seed);
            sash.Blind = n < 0.32f ? 0.18f + n : 0;
            sash.BlindColor = Pick(seed * 7 + face * 31 + column, floor, VoxelPalette.Cream, VoxelPalette.StoneLight, VoxelPalette.AwningStripe, VoxelPalette.Plaster);
            return sash;
        }
        private static void FloorWindows(PolyBuilder b, Face f, List<Rect> holes, TowerStyle style, int seed, int face, int floor, float vb, float floorH, TowerTrims t)
        {
            var sash = UpperSash(style, t);
            if (style == TowerStyle.Glass)
            {
                // Ribbon windows between corner piers, the spandrel below them.
                float top = vb + floorH - 0.3f, bottom = vb + Math.Max(0.6f, floorH - 2.45f);
                var ribbon = Bays(f.Length, 6f, 0.6f);
                float span = (f.Length - 1.2f) / ribbon.Length;
                for (int i = 0; i < ribbon.Length; i++)
                {
                    float u0 = 0.6f + span * i + (i > 0 ? 0.2f : 0), u1 = 0.6f + span * (i + 1) - (i < ribbon.Length - 1 ? 0.2f : 0);
                    Window(b, f, holes, new Rect(u0, u1, bottom, top), WithBlind(sash, seed, face, floor, i), WindowGlass(seed, face, floor, i));
                }
                return;
            }
            float bay = style == TowerStyle.Brick ? 2.3f : 2.5f, width = style == TowerStyle.Brick ? 1.15f : 1.3f;
            float height = Math.Max(1.4f, Math.Min(1.9f, floorH - 1.3f)), sill = vb + 0.9f;
            var centres = Bays(f.Length, bay);
            float w = Math.Min(width, f.Length / centres.Length - 0.7f);
            for (int i = 0; i < centres.Length; i++)
                Window(b, f, holes, new Rect(centres[i] - w / 2, centres[i] + w / 2, sill, sill + height), WithBlind(sash, seed, face, floor, i), WindowGlass(seed, face, floor, i));
        }
        // Ground floor sides: taller windows in the same bays.
        private static void GroundWindows(PolyBuilder b, Face f, List<Rect> holes, TowerStyle style, int seed, int face, TowerTrims t)
        {
            var sash = UpperSash(style, t); sash.Lintel = 0;
            var centres = Bays(f.Length, style == TowerStyle.Glass ? 3f : 2.5f);
            float w = Math.Min(style == TowerStyle.Glass ? 2.2f : 1.3f, f.Length / centres.Length - 0.7f);
            for (int i = 0; i < centres.Length; i++)
                Window(b, f, holes, new Rect(centres[i] - w / 2, centres[i] + w / 2, 1.0f, 3.1f), WithBlind(sash, seed, face, -1, i), WindowGlass(seed, face, -1, i));
        }
        // Display windows either side of a door in the middle of the front, a sign band over them and, by the seed, a
        // striped awning over the windows or a flat canopy over the door. Returns the door's u range for the plinth.
        private static (float, float) Storefront(PolyBuilder b, Face f, List<Rect> holes, TowerStyle style, int seed, TowerTrims t)
        {
            float L = f.Length, door = L >= 10 ? 1.8f : 1.3f, uc = L / 2;
            float d0 = uc - door / 2, d1 = uc + door / 2;
            Door(b, f, holes, d0, d1, 3.0f, t.Door, true, NightPart);
            var display = new Sash { Depth = 0.12f, Frame = 0.08f, Bar = 0.06f, FrameColor = t.Shopfront, MullionStep = 1.4f, SillColor = t.Shopfront, SillProud = 0.04f, SillHeight = 0.05f };
            Window(b, f, holes, new Rect(0.6f, d0 - 0.45f, 0.55f, 3.0f), display, NightPart);
            Window(b, f, holes, new Rect(d1 + 0.45f, L - 0.6f, 0.55f, 3.0f), display, NightPart);
            Ledge(b, f, 0.3f, L - 0.3f, 3.18f, 3.72f, 0.1f, t.Sign);
            float awning = Rand(seed, 30);
            if (style != TowerStyle.Glass && awning < 0.6f)
            {
                byte a = Pick(seed, 31, VoxelPalette.Awning, VoxelPalette.CarRed, VoxelPalette.Navy, VoxelPalette.CarGreen);
                Awning(b, f, 0.45f, d0 - 0.3f, 3.12f, 2.55f, 1.2f, a, VoxelPalette.AwningStripe);
                Awning(b, f, d1 + 0.3f, L - 0.45f, 3.12f, 2.55f, 1.2f, a, VoxelPalette.AwningStripe);
            }
            else FaceBox(b, f, d0 - 0.5f, d1 + 0.5f, 3.02f, 3.14f, 0, 1.1f, TrimPart, style == TowerStyle.Glass ? VoxelPalette.MetalLight : t.Cornice, 0.02f);
            return (d0, d1);
        }
        // The flat roof inside a capped parapet.
        private static void Roof(PolyBuilder b, float w, float d, float h, TowerTrims t)
        {
            float x = w / 2, z = d / 2, i = ParapetThickness, top = h + ParapetHeight;
            b.Quad(new V3(-x + i, h, -z + i), new V3(x - i, h, -z + i), new V3(x - i, h, z - i), new V3(-x + i, h, z - i), Up, TrimPart, VoxelPalette.RoofDark, 1);
            // Inner faces of the parapet (part of the wall).
            b.Quad(new V3(-x + i, h, -z + i), new V3(x - i, h, -z + i), new V3(x - i, top, -z + i), new V3(-x + i, top, -z + i), new V3(0, 0, 1), WallPart, 0, 0.85f);
            b.Quad(new V3(-x + i, h, z - i), new V3(x - i, h, z - i), new V3(x - i, top, z - i), new V3(-x + i, top, z - i), new V3(0, 0, -1), WallPart, 0, 0.85f);
            b.Quad(new V3(-x + i, h, -z + i), new V3(-x + i, h, z - i), new V3(-x + i, top, z - i), new V3(-x + i, top, -z + i), new V3(1, 0, 0), WallPart, 0, 0.85f);
            b.Quad(new V3(x - i, h, -z + i), new V3(x - i, h, z - i), new V3(x - i, top, z - i), new V3(x - i, top, -z + i), new V3(-1, 0, 0), WallPart, 0, 0.85f);
            // Cap: front and back over the corners, the sides between them.
            float o = CapOverhang, c = top + CapHeight;
            b.Box(new V3(-x - o, top, -z - o), new V3(x + o, c, -z + i + o), 0, TrimPart, t.Cap);
            b.Box(new V3(-x - o, top, z - i - o), new V3(x + o, c, z + o), 0, TrimPart, t.Cap);
            b.Box(new V3(-x - o, top, -z + i + o), new V3(-x + i + o, c, z - i - o), 0, TrimPart, t.Cap);
            b.Box(new V3(x - i - o, top, -z + i + o), new V3(x + o, c, z - i - o), 0, TrimPart, t.Cap);
        }
        // Plant units on the roof, a stair housing on taller blocks, a timber water tank on brick ones, a mast on the tallest.
        private static void RoofPlant(PolyBuilder b, float w, float d, float h, TowerStyle style, int seed)
        {
            float x = w / 2 - ParapetThickness - 0.4f, z = d / 2 - ParapetThickness - 0.4f;
            int units = 1 + (int)(Rand(seed, 50) * 3);
            for (int i = 0; i < units; i++)
            {
                float uw = 1.0f + Rand(seed, 60 + i) * 1.2f, ud = 0.9f + Rand(seed, 70 + i) * 1.1f, uh = 0.6f + Rand(seed, 100 + i) * 0.7f;
                float ux = -x + (2 * x - uw) * Rand(seed, 80 + i), uz = -z + (2 * z - ud) * Rand(seed, 90 + i);
                b.Box(new V3(ux, h, uz), new V3(ux + uw, h + uh, uz + ud), 0.04f, TrimPart, i == 0 ? VoxelPalette.MetalDark : VoxelPalette.Metal);
                b.Box(new V3(ux + 0.15f, h + uh, uz + 0.15f), new V3(ux + uw - 0.15f, h + uh + 0.05f, uz + ud - 0.15f), 0, TrimPart, VoxelPalette.Tire);
            }
            if (h >= 18)
            {
                float sx = -x + 0.2f, sz = z - 2.4f;
                b.Box(new V3(sx, h, sz), new V3(sx + 2.2f, h + 2.3f, sz + 2.4f), 0.03f, TrimPart, style == TowerStyle.Brick ? VoxelPalette.BrickDark : VoxelPalette.Concrete);
                b.Box(new V3(sx + 2.2f, h + 0.02f, sz + 0.7f), new V3(sx + 2.24f, h + 2.0f, sz + 1.6f), 0, TrimPart, VoxelPalette.MetalDark);
            }
            if (style == TowerStyle.Brick) WaterTank(b, x - 1.2f, z - 1.2f, h);
            if (h >= 28) b.Box(new V3(0.3f, h, 0.3f), new V3(0.42f, h + 4f, 0.42f), 0, TrimPart, VoxelPalette.MetalLight);
        }
        private static void WaterTank(PolyBuilder b, float cx, float cz, float h)
        {
            const int sides = 8; const float r = 0.9f, legs = 0.7f, body = 1.6f, cone = 0.45f;
            foreach (var (lx, lz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                b.Box(new V3(cx + lx * 0.55f - 0.06f, h, cz + lz * 0.55f - 0.06f), new V3(cx + lx * 0.55f + 0.06f, h + legs, cz + lz * 0.55f + 0.06f), 0, TrimPart, VoxelPalette.MetalDark);
            int group = b.Group; b.Group = b.NextGroup();
            var centre = new V3(cx, h + legs + body / 2, cz);
            for (int i = 0; i < sides; i++)
            {
                double a0 = 2 * Math.PI * i / sides, a1 = 2 * Math.PI * (i + 1) / sides;
                var p0 = new V3(cx + r * (float)Math.Cos(a0), h + legs, cz + r * (float)Math.Sin(a0));
                var p1 = new V3(cx + r * (float)Math.Cos(a1), h + legs, cz + r * (float)Math.Sin(a1));
                var q0 = p0 + Up * body; var q1 = p1 + Up * body; var apex = new V3(cx, h + legs + body + cone, cz);
                b.Quad(p0, p1, q1, q0, (p0 + q1) * 0.5f - centre, TrimPart, VoxelPalette.Wood, 1);
                b.Triangle(q0, q1, apex, (q0 + q1 + apex) * (1f / 3) - centre, TrimPart, VoxelPalette.WoodDark, 1);
                b.Triangle(p0, p1, new V3(cx, h + legs, cz), Down, TrimPart, VoxelPalette.WoodDark, 0.7f);
            }
            b.Group = group;
        }

        // ---------- Street buildings ----------
        public const float ShopWidth = 6, ShopDepth = 8, ShopHeight = 9;
        // Narrow street building, 6 × 9 × 8 m: a display window, a door and a sign band on the ground floor, two upper floors
        // of three windows, a cornice and parapet; brick shops and cafés get a striped awning over the window, offices a
        // flat canopy over the door. With backWindows the back (+Z), seen from the ring road, gets a plinth, a service door
        // and two rows of plain windows. The sides are party walls.
        public static PolyMesh Shop(ShopStyle style, int seed, bool backWindows = false)
        {
            var b = NewBuilder();
            var faces = Faces(ShopWidth, ShopDepth);
            var look = new StreetTrims(style);
            var front = faces[Front]; var holes = new List<Rect>();
            var display = new Sash { Depth = 0.12f, Frame = 0.08f, Bar = 0.07f, FrameColor = style == ShopStyle.Office ? VoxelPalette.MetalDark : VoxelPalette.WoodDark,
                MullionStep = 1.5f, SillColor = look.Band, SillProud = 0.05f, SillHeight = 0.06f };
            Window(b, front, holes, new Rect(0.5f, 3.5f, 0.5f, 2.4f), display, NightPart);
            Door(b, front, holes, 4.0f, 5.05f, 2.3f, VoxelPalette.WoodDark, true, NightPart);
            Ledge(b, front, 0.25f, ShopWidth - 0.25f, 2.8f, 3.26f, 0.08f, look.Sign);
            StreetUpper(b, front, holes, style, seed, look, true);
            WallFace(b, front, 0, ContactHeight, holes); WallFace(b, front, ContactHeight, ShopHeight + 0.3f, holes);
            StreetShell(b, faces, seed, look, backWindows, (4.0f, 5.05f));
            if (style == ShopStyle.Office)
                FaceBox(b, front, 3.75f, 5.3f, 2.45f, 2.57f, 0, 0.8f, TrimPart, VoxelPalette.MetalLight, 0.02f);
            else
                Awning(b, front, 0.25f, 3.75f, 2.74f, 2.24f, 1.5f, style == ShopStyle.Cafe ? VoxelPalette.CarRed : VoxelPalette.Awning, VoxelPalette.AwningStripe);
            return b.Build(PaletteUv);
        }
        private sealed class StreetTrims
        {
            public readonly byte Band, Trim, Sign;
            public StreetTrims(ShopStyle style)
            {
                Band = style == ShopStyle.BrickShop ? VoxelPalette.StoneLight : style == ShopStyle.Cafe ? VoxelPalette.Plaster : VoxelPalette.Stone;
                Trim = style == ShopStyle.Office ? VoxelPalette.MetalLight : VoxelPalette.Trim;
                Sign = style == ShopStyle.Office ? VoxelPalette.Navy : style == ShopStyle.Cafe ? VoxelPalette.WoodDark : VoxelPalette.Ink;
            }
        }
        // Two upper floors of three windows on the front, a band at each floor (the first over the sign band unless the
        // sign itself makes it).
        private static void StreetUpper(PolyBuilder b, Face front, List<Rect> holes, ShopStyle style, int seed, StreetTrims look, bool firstBand)
        {
            var upper = new Sash { FrameColor = look.Trim == VoxelPalette.MetalLight ? VoxelPalette.MetalDark : VoxelPalette.Trim, Cross = style != ShopStyle.Office,
                MullionStep = style == ShopStyle.Office ? 0.6f : 0, SillColor = look.Trim, Lintel = style == ShopStyle.Office ? 0 : 0.18f, LintelColor = look.Band };
            for (int floor = 0; floor < 2; floor++)
            {
                float y0 = 3.0f + floor * 3.0f;
                for (int c = 0; c < 3; c++)
                {
                    float x0 = 0.5f + 2 * c;
                    Window(b, front, holes, new Rect(x0, x0 + 1.0f, y0 + 0.75f, y0 + 2.25f), WithBlind(upper, seed, Front, floor, c), VoxelNoise.Value(c, floor, 2, seed) < 0.5f ? NightPart : GlassPart);
                }
                if (floor > 0 || firstBand) Ledge(b, front, -0.03f, ShopWidth + 0.03f, floor > 0 ? 5.94f : 3.32f, floor > 0 ? 6.06f : 3.44f, 0.03f, look.Band);
            }
        }
        // Back, party walls, plinth, cornice, parapet and roof of a 6 × 8 × 9 m street building; the front wall is the caller's.
        private static void StreetShell(PolyBuilder b, Face[] faces, int seed, StreetTrims look, bool backWindows, (float u0, float u1) frontDoor)
        {
            const float w = ShopWidth, d = ShopDepth, h = ShopHeight;
            var back = faces[Back]; var backHoles = new List<Rect>();
            if (backWindows)
            {
                Door(b, back, backHoles, 1.0f, 1.8f, 2.05f, VoxelPalette.MetalDark, false, GlassPart);
                var plain = new Sash { FrameColor = VoxelPalette.Trim, Cross = false, SillColor = look.Trim, SillProud = 0.04f };
                for (int floor = 0; floor < 2; floor++)
                {
                    float y0 = 3.0f + floor * 3.0f;
                    for (int c = 0; c < 3; c++)
                    {
                        float x0 = 0.75f + 1.75f * c;
                        Window(b, back, backHoles, new Rect(x0, x0 + 1.0f, y0 + 0.75f, y0 + 2.25f), plain, VoxelNoise.Value(c, floor, 9, seed) < 0.45f ? NightPart : GlassPart);
                    }
                    Ledge(b, back, -0.03f, w + 0.03f, y0 - 0.06f, y0 + 0.06f, 0.03f, look.Band);
                }
            }
            WallFace(b, back, 0, ContactHeight, backHoles); WallFace(b, back, ContactHeight, h + 0.3f, backHoles);
            foreach (int k in new[] { Left, Right })
            {
                WallFace(b, faces[k], 0, ContactHeight, new List<Rect>()); WallFace(b, faces[k], ContactHeight, h + 0.3f, new List<Rect>());
            }
            var gaps = new List<(int, float, float)> { (Back, 1.0f, 1.8f), (Front, frontDoor.u0, frontDoor.u1) };
            Belt(b, faces, 0, 0.25f, 0.03f, VoxelPalette.StoneDark, backWindows, gaps);
            var front = faces[Front];
            Ledge(b, front, -0.1f, w + 0.1f, 8.3f, 8.45f, 0.1f, look.Band);
            Ledge(b, front, -0.25f, w + 0.25f, 8.45f, 8.95f, 0.25f, look.Trim);
            // Parapet: a low cap over the walls, the roof inside it, a plant box.
            float x = w / 2, z = d / 2, i = 0.2f, top = h + 0.3f;
            b.Quad(new V3(-x + i, h, -z + i), new V3(x - i, h, -z + i), new V3(x - i, h, z - i), new V3(-x + i, h, z - i), Up, TrimPart, VoxelPalette.RoofDark, 1);
            b.Box(new V3(-x, top, -z), new V3(x, top + 0.06f, -z + i), 0, TrimPart, look.Trim);
            b.Box(new V3(-x, top, z - i), new V3(x, top + 0.06f, z), 0, TrimPart, look.Trim);
            b.Box(new V3(-x, top, -z + i), new V3(-x + i, top + 0.06f, z - i), 0, TrimPart, look.Trim);
            b.Box(new V3(x - i, top, -z + i), new V3(x, top + 0.06f, z - i), 0, TrimPart, look.Trim);
            b.Quad(new V3(-x + i, h, -z + i), new V3(x - i, h, -z + i), new V3(x - i, top, -z + i), new V3(-x + i, top, -z + i), new V3(0, 0, 1), WallPart, 0, 0.85f);
            b.Quad(new V3(-x + i, h, z - i), new V3(x - i, h, z - i), new V3(x - i, top, z - i), new V3(-x + i, top, z - i), new V3(0, 0, -1), WallPart, 0, 0.85f);
            b.Quad(new V3(-x + i, h, -z + i), new V3(-x + i, h, z - i), new V3(-x + i, top, z - i), new V3(-x + i, top, -z + i), new V3(1, 0, 0), WallPart, 0, 0.85f);
            b.Quad(new V3(x - i, h, -z + i), new V3(x - i, h, z - i), new V3(x - i, top, z - i), new V3(x - i, top, -z + i), new V3(-1, 0, 0), WallPart, 0, 0.85f);
            b.Box(new V3(-x + 0.75f, h, z - 2.0f), new V3(-x + 1.75f, h + 0.5f, z - 1.0f), 0.03f, TrimPart, VoxelPalette.MetalDark);
        }

        // ---------- Café ----------
        // Café «Бык и медведь», a street building with an open room on the ground floor: a 1.5 m doorway and a wide open
        // window with a mullion in a 0.5 m front wall, a 5 × 7 m room 3 m high with a dark wooden wainscot, a rail and
        // plaster above, a light ceiling, the name band over a red and cream awning. Front −Z as the others; in the city it
        // turns round to the street. Room, door and window are where the scene's blockers and furniture expect them.
        public const float CafeWall = 0.5f, CafeRoomHeight = 3f;
        public static readonly (float U0, float U1) CafeDoor = (3.5f, 5.0f), CafeWindow = (0.75f, 3.0f);
        public const float CafeDoorTop = 2.5f, CafeWindowSill = 0.75f, CafeWindowTop = 2.5f;
        public static PolyMesh Cafe()
        {
            const float w = ShopWidth, d = ShopDepth, t = CafeWall, room = CafeRoomHeight;
            const int seed = 5;
            var b = NewBuilder();
            var faces = Faces(w, d);
            var look = new StreetTrims(ShopStyle.Cafe);
            var front = faces[Front]; var holes = new List<Rect>();
            var door = new Rect(CafeDoor.U0, CafeDoor.U1, 0, CafeDoorTop);
            var window = new Rect(CafeWindow.U0, CafeWindow.U1, CafeWindowSill, CafeWindowTop);
            holes.Add(door); holes.Add(window);
            // Through the thickness of the wall: plain reveals, a stone threshold, a sill board and the mullion.
            Reveal(b, front, door, t, false);
            Reveal(b, front, window, t);
            b.Quad(front.At(door.U0, 0.01f), front.At(door.U1, 0.01f), front.At(door.U1, 0.01f, -t), front.At(door.U0, 0.01f, -t), Up, TrimPart, VoxelPalette.StoneDark, 0.9f);
            float mid = (window.U0 + window.U1) / 2;
            FaceBox(b, front, mid - 0.06f, mid + 0.06f, window.V0, window.V1, -t + 0.05f, -0.05f, TrimPart, VoxelPalette.Trim, 0.01f);
            Ledge(b, front, window.U0 - 0.08f, window.U1 + 0.08f, window.V0 - 0.08f, window.V0, 0.08f, VoxelPalette.Trim);
            // Casings around both openings.
            foreach (var r in new[] { door, window })
            {
                Ledge(b, front, r.U0 - 0.12f, r.U0, r.V0, r.V1 + 0.12f, 0.04f, VoxelPalette.Trim);
                Ledge(b, front, r.U1, r.U1 + 0.12f, r.V0, r.V1 + 0.12f, 0.04f, VoxelPalette.Trim);
                Ledge(b, front, r.U0, r.U1, r.V1, r.V1 + 0.12f, 0.04f, VoxelPalette.Trim, false);
            }
            Ledge(b, front, 0.25f, w - 0.25f, 2.95f, 3.5f, 0.05f, look.Sign);
            StreetUpper(b, front, holes, ShopStyle.Cafe, seed, look, false);
            WallFace(b, front, 0, ContactHeight, holes); WallFace(b, front, ContactHeight, ShopHeight + 0.3f, holes);
            StreetShell(b, faces, seed, look, true, CafeDoor);
            Awning(b, front, 0.25f, w - 0.25f, 2.85f, 2.3f, 1.5f, VoxelPalette.CarRed, VoxelPalette.AwningStripe);
            // The room: inner walls facing in, wainscot, rail and plaster, the ceiling. Inner faces mirror the front's u.
            float ix = w / 2 - t, iz = d / 2 - t;
            var inner = new[]
            {
                new Face(new V3(ix, 0, -iz), new V3(-1, 0, 0), new V3(0, 0, 1), 2 * ix),
                new Face(new V3(-ix, 0, iz), new V3(1, 0, 0), new V3(0, 0, -1), 2 * ix),
                new Face(new V3(-ix, 0, -iz), new V3(0, 0, 1), new V3(1, 0, 0), 2 * iz),
                new Face(new V3(ix, 0, iz), new V3(0, 0, -1), new V3(-1, 0, 0), 2 * iz),
            };
            float flip = w - t;
            var innerHoles = new List<Rect> { new Rect(flip - door.U1, flip - door.U0, door.V0, door.V1), new Rect(flip - window.U1, flip - window.U0, window.V0, window.V1) };
            for (int k = 0; k < 4; k++)
            {
                var openings = k == 0 ? innerHoles : new List<Rect>();
                WallFace(b, inner[k], 0, 1.0f, openings, TrimPart, VoxelPalette.WoodDark);
                WallFace(b, inner[k], 1.0f, 1.12f, openings, TrimPart, VoxelPalette.Wood);
                WallFace(b, inner[k], 1.12f, room, openings, TrimPart, VoxelPalette.Plaster, 0.95f);
                // The rail stops at the doorway and the window.
                float from = 0;
                foreach (var r in openings) { if (r.U0 > from) Ledge(b, inner[k], from, r.U0, 1.0f, 1.12f, 0.03f, VoxelPalette.Wood, k == 0); from = r.U1; }
                Ledge(b, inner[k], from, inner[k].Length, 1.0f, 1.12f, 0.03f, VoxelPalette.Wood, k == 0);
            }
            b.Quad(new V3(-ix, room, -iz), new V3(ix, room, -iz), new V3(ix, room, iz), new V3(-ix, room, iz), Down, TrimPart, VoxelPalette.Plaster, 0.9f);
            return b.Build(PaletteUv);
        }
    }
}
