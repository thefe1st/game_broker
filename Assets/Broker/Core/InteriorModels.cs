using System;
using System.Collections.Generic;

namespace Broker.Core
{
    public enum DeskStyle { Executive, Staff }

    // The office and the café as smooth low-poly meshes in the manner of Schedule I, in place of the voxel furniture and of
    // the primitive boxes that now only keep their colliders: desks and office chairs here; monitors, the keyboard, the
    // mouse, the telephones, papers, the espresso machine and the cup in InteriorModels.Equipment.cs; the coffee cabinet,
    // the potted plant, the display pedestal, the door, the window frames, the ceiling lights, the plaque, the elevator and
    // the skirting in InteriorModels.Room.cs; the café's counter, shelf, tables, chairs, menu and lamps in
    // InteriorModels.Cafe.cs. Palette trim, polished metal (brass, chrome) and glowing lamps and screens on the building
    // parts; fronts face −Z and pivots sit at the bottom centre unless noted; bounds stay inside the models they replace.
    public static partial class InteriorModels
    {
        public const float DeskHeight = 0.85f, ChairSeat = 0.54f, LeatherBorder = 0.12f;
        private const int WallPart = (int)BuildingPart.Wall, TrimPart = (int)BuildingPart.Trim, GlassPart = (int)BuildingPart.Glass,
            GlowPart = (int)BuildingPart.Glow, MetalPart = (int)BuildingPart.Metal;
        private const float SmoothAngle = 50;

        // Furniture sits in a little contact shade over `contact` metres from its foot (the floor or the desk it stands on);
        // hanging fittings keep full light.
        private static PolyBuilder NewBuilder(float contact = 0.45f)
        {
            var b = new PolyBuilder(BuildingModels.PartCount, SmoothAngle, WallPart, VoxelSurfaces.MetresPerTile);
            if (contact > 0) b.Ambient = p => 0.84f + 0.16f * SmoothStep(0, contact, p.Y);
            return b;
        }
        private static float SmoothStep(float a, float b, float x) { float t = Math.Max(0, Math.Min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); }
        private static float Rand(int seed, int salt) => VoxelNoise.Value(salt, 23, 11, seed);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // A rigid placement for PolyBuilder.Place: a part built round the origin, turned by roll about Z, then by pitch about X
        // (positive tips +Y towards +Z), then by yaw about Y (positive turns +Z towards +X, as in Unity), moved to `at`.
        private static Func<V3, V3> Pose(V3 at, float yaw, float pitch = 0, float roll = 0)
        {
            double y = yaw * Math.PI / 180, p = pitch * Math.PI / 180, r = roll * Math.PI / 180;
            float cy = (float)Math.Cos(y), sy = (float)Math.Sin(y), cp = (float)Math.Cos(p), sp = (float)Math.Sin(p), cr = (float)Math.Cos(r), sr = (float)Math.Sin(r);
            return v =>
            {
                float x1 = v.X * cr - v.Y * sr, y1 = v.X * sr + v.Y * cr;
                float y2 = y1 * cp - v.Z * sp, z2 = y1 * sp + v.Z * cp;
                return new V3(at.X + x1 * cy + z2 * sy, at.Y + y2, at.Z - x1 * sy + z2 * cy);
            };
        }

        // ---------- Shared pieces ----------
        // A flat round face at depth z facing −Z (dials, lamps, buttons).
        private static void FrontDisc(PolyBuilder b, float cx, float cy, float z, float r, int sides, int part, byte color, float shade = 1)
        {
            var centre = new V3(cx, cy, z);
            for (int k = 0; k < sides; k++)
            {
                double a0 = 2 * Math.PI * k / sides, a1 = 2 * Math.PI * (k + 1) / sides;
                b.Triangle(centre, new V3(cx + r * (float)Math.Cos(a0), cy + r * (float)Math.Sin(a0), z), new V3(cx + r * (float)Math.Cos(a1), cy + r * (float)Math.Sin(a1), z),
                    new V3(0, 0, -1), part, color, shade);
            }
        }
        // A flat ring between radii r0 and r1 at depth z facing −Z (bezels).
        private static void FrontRing(PolyBuilder b, float cx, float cy, float z, float r0, float r1, int sides, int part, byte color, float shade = 1)
        {
            for (int k = 0; k < sides; k++)
            {
                double a0 = 2 * Math.PI * k / sides, a1 = 2 * Math.PI * (k + 1) / sides;
                float c0 = (float)Math.Cos(a0), s0 = (float)Math.Sin(a0), c1 = (float)Math.Cos(a1), s1 = (float)Math.Sin(a1);
                b.Quad(new V3(cx + r0 * c0, cy + r0 * s0, z), new V3(cx + r1 * c0, cy + r1 * s0, z), new V3(cx + r1 * c1, cy + r1 * s1, z), new V3(cx + r0 * c1, cy + r0 * s1, z),
                    new V3(0, 0, -1), part, color, shade);
            }
        }
        // Flat quad facing −Z at depth z (chalk strokes, text lines, kick plates).
        private static void FrontQuad(PolyBuilder b, float x0, float y0, float x1, float y1, float z, int part, byte color, float shade = 1)
            => b.Quad(new V3(x0, y0, z), new V3(x1, y0, z), new V3(x1, y1, z), new V3(x0, y1, z), new V3(0, 0, -1), part, color, shade);
        // Upward-facing rectangle at height y (inlays, labels on tops).
        private static void TopQuad(PolyBuilder b, float x0, float z0, float x1, float z1, float y, int part, byte color, float shade = 1)
            => b.Quad(new V3(x0, y, z0), new V3(x1, y, z0), new V3(x1, y, z1), new V3(x0, y, z1), new V3(0, 1, 0), part, color, shade);
        // Rectangular frame in the XY plane round an opening, from zFront to zBack: the front ring with its outer edges
        // chamfered by `bevel`, the outer sides, the opening's reveal and, if asked, a back ring; one smoothing group.
        private static void Frame(PolyBuilder b, float x0, float y0, float x1, float y1, float ix0, float iy0, float ix1, float iy1,
            float zFront, float zBack, float bevel, int part, byte color, bool back = false, float shade = 1)
        {
            int group = b.Group; b.Group = b.NextGroup();
            float c = bevel;
            V3[] Ring(float ax0, float ay0, float ax1, float ay1, float z) => new[] { new V3(ax0, ay0, z), new V3(ax1, ay0, z), new V3(ax1, ay1, z), new V3(ax0, ay1, z) };
            V3[] f = Ring(x0 + c, y0 + c, x1 - c, y1 - c, zFront), s = Ring(x0, y0, x1, y1, zFront + c), e = Ring(x0, y0, x1, y1, zBack);
            V3[] i = Ring(ix0, iy0, ix1, iy1, zFront), ib = Ring(ix0, iy0, ix1, iy1, zBack);
            // Outward directions of the sides: bottom, right, top, left.
            var side = new[] { new V3(0, -1, 0), new V3(1, 0, 0), new V3(0, 1, 0), new V3(-1, 0, 0) };
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4;
                b.Quad(f[k], f[n], i[n], i[k], new V3(0, 0, -1), part, color, shade);
                if (c > 0) b.Quad(f[k], f[n], s[n], s[k], side[k] + new V3(0, 0, -1), part, color, shade);
                b.Quad(s[k], s[n], e[n], e[k], side[k], part, color, shade);
                b.Quad(i[k], i[n], ib[n], ib[k], side[k] * -1, part, color, shade * 0.9f);
                if (back) b.Quad(e[k], e[n], ib[n], ib[k], new V3(0, 0, 1), part, color, shade * 0.9f);
            }
            b.Group = group;
        }
        // Key or button cap: a tapered block from the rectangle (x0..x1, z0..z1) at y0 to one `inset` smaller at y1, open
        // underneath; a smoothing group of its own.
        private static void Cap(PolyBuilder b, float x0, float x1, float z0, float z1, float y0, float y1, float inset, int part, byte color, float shade = 1)
        {
            int group = b.Group; b.Group = b.NextGroup();
            V3[] lo = { new V3(x0, y0, z0), new V3(x1, y0, z0), new V3(x1, y0, z1), new V3(x0, y0, z1) };
            V3[] hi = { new V3(x0 + inset, y1, z0 + inset), new V3(x1 - inset, y1, z0 + inset), new V3(x1 - inset, y1, z1 - inset), new V3(x0 + inset, y1, z1 - inset) };
            float cx = (x0 + x1) / 2, cz = (z0 + z1) / 2;
            b.Quad(hi[0], hi[1], hi[2], hi[3], new V3(0, 1, 0), part, color, shade);
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4; var mid = (lo[k] + lo[n] + hi[n] + hi[k]) * 0.25f;
                b.Quad(lo[k], lo[n], hi[n], hi[k], new V3(mid.X - cx, 0, mid.Z - cz), part, color, shade * 0.9f);
            }
            b.Group = group;
        }
        // Cup on y, `r` round at the rim and `h` high: a tapered wall, the rim, the inside turned in with coffee in it, a
        // handle towards +X and, if asked, a saucer under it.
        private static void Cup(PolyBuilder b, float cx, float cz, float y, float r, float h, int sides, bool saucer, byte color)
        {
            if (saucer)
            {
                PolyShapes.Lathe(b, cx, cz, new[] { (y, r * 1.05f), (y + 0.004f, r * 1.7f), (y + 0.009f, r * 1.8f) }, sides + 2, TrimPart, color, capBottom: true);
                PolyShapes.Disc(b, cx, y + 0.009f, cz, r * 1.8f, sides + 2, true, TrimPart, color, 0.97f);
                y += 0.009f;
            }
            PolyShapes.Lathe(b, cx, cz, new[] { (y, r * 0.7f), (y + h * 0.18f, r * 0.9f), (y + h, r) }, sides, TrimPart, color, capBottom: true);
            PolyShapes.Lathe(b, cx, cz, new[] { (y + h, r), (y + h, r * 0.85f), (y + h * 0.75f, r * 0.76f) }, sides, TrimPart, color, 0.85f);
            PolyShapes.Disc(b, cx, y + h * 0.75f, cz, r * 0.76f, sides, true, TrimPart, VoxelPalette.BarkDark);
            var handle = new List<V3>();
            for (int i = 0; i <= 4; i++)
            {
                double a = Math.PI * (0.5 - i / 4.0);
                handle.Add(new V3(cx + r * 0.88f + r * 0.55f * (float)Math.Cos(a), y + h * 0.5f + h * 0.3f * (float)Math.Sin(a), cz));
            }
            PolyShapes.Sweep(b, handle, r * 0.13f, r * 0.13f, 5, new V3(0, 0, 1), TrimPart, color);
        }

        // ---------- Desks ----------
        // Desk `width` × `depth`, DeskHeight high: a solid top over two panel ends, a modesty panel towards −Z and on the
        // right (+X) a pedestal of three drawers facing the sitter at +Z. The director's desk (Executive) has a thick top with
        // a green leather writing surface edged in brass over a dark apron, raised fields on its ends and modesty panel and
        // brass knobs; the staff desks a thinner top, plain panels and chrome bar handles.
        public static PolyMesh Desk(DeskStyle style, float width, float depth, byte top, byte frame)
        {
            if (!(width >= 1.2f) || width > 4) throw new ArgumentOutOfRangeException(nameof(width));
            if (!(depth >= 0.6f) || depth > 1.6f) throw new ArgumentOutOfRangeException(nameof(depth));
            var b = NewBuilder();
            bool executive = style == DeskStyle.Executive;
            float hw = width / 2, hd = depth / 2, thick = executive ? 0.06f : 0.04f, under = DeskHeight - thick;
            // The director's leather and its brass line lie over the wood, flush with DeskHeight at the top.
            b.Box(new V3(-hw, under, -hd), new V3(hw, executive ? DeskHeight - 0.0012f : DeskHeight, hd), executive ? 0.016f : 0.01f, TrimPart, top);
            if (executive)
            {
                b.Box(new V3(-hw + 0.03f, under - 0.03f, -hd + 0.03f), new V3(hw - 0.03f, under, hd - 0.03f), 0.008f, TrimPart, frame, 0.9f);
                under -= 0.03f;
                float lx = hw - LeatherBorder, lz = hd - LeatherBorder, line = 0.008f;
                TopQuad(b, -lx, -lz, lx, lz, DeskHeight - 0.0004f, TrimPart, VoxelPalette.PineDark);
                const float yl = DeskHeight;
                TopQuad(b, -lx, -lz, lx, -lz + line, yl, MetalPart, VoxelPalette.Brass); TopQuad(b, -lx, lz - line, lx, lz, yl, MetalPart, VoxelPalette.Brass);
                TopQuad(b, -lx, -lz + line, -lx + line, lz - line, yl, MetalPart, VoxelPalette.Brass); TopQuad(b, lx - line, -lz + line, lx, lz - line, yl, MetalPart, VoxelPalette.Brass);
            }
            // Ends 0.08 thick, 0.05 in from the sides and the edges of the top.
            const float inset = 0.05f, end = 0.08f;
            float inner = hw - inset - end;
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new V3(s < 0 ? -hw + inset : inner, 0, -hd + inset), new V3(s < 0 ? -inner : hw - inset, under, hd - inset), 0.012f, TrimPart, frame);
                if (executive)
                {
                    float xo = s * (hw - inset);
                    b.Box(new V3(Math.Min(xo, xo + s * 0.008f), 0.12f, -hd + 0.15f), new V3(Math.Max(xo, xo + s * 0.008f), under - 0.08f, hd - 0.15f), 0.005f, TrimPart, frame, 1.06f);
                }
            }
            // Modesty panel towards −Z.
            float mz = -hd + (executive ? 0.07f : 0.1f), my = executive ? 0.1f : DeskHeight / 3;
            b.Box(new V3(-inner, my, mz), new V3(inner, under, mz + 0.03f), 0.008f, TrimPart, frame, 0.96f);
            if (executive)
            {
                const int fields = 3; const float gap = 0.08f;
                float w = (2 * inner - gap * (fields + 1)) / fields;
                for (int k = 0; k < fields; k++)
                {
                    float x0 = -inner + gap + k * (w + gap);
                    b.Box(new V3(x0, my + 0.08f, mz - 0.008f), new V3(x0 + w, under - 0.07f, mz), 0.005f, TrimPart, frame, 1.05f);
                }
            }
            // Pedestal of three drawers on the right, facing +Z.
            float px1 = inner, px0 = inner - 0.45f, pz0 = hd - 0.5f, pz1 = hd - 0.06f, py0 = 0.04f;
            b.Box(new V3(px0, py0, pz0), new V3(px1, under, pz1), 0.008f, TrimPart, frame);
            float drawer = (under - py0 - 0.04f) / 3;
            for (int k = 0; k < 3; k++)
            {
                float y0 = py0 + 0.02f + k * drawer + 0.008f, y1 = y0 + drawer - 0.016f, ym = (y0 + y1) / 2 + (k == 2 ? 0 : drawer * 0.15f);
                b.Box(new V3(px0 + 0.02f, y0, pz1), new V3(px1 - 0.02f, y1, pz1 + 0.016f), 0.006f, TrimPart, top);
                float xm = (px0 + px1) / 2, zf = pz1 + 0.016f;
                if (executive)
                    PolyShapes.Limb(b, new V3(xm, ym, zf), new V3(xm, ym, zf + 0.026f), 0.008f, 0.014f, 8, MetalPart, VoxelPalette.Brass);
                else
                {
                    foreach (float dx in new[] { -0.07f, 0.07f })
                        PolyShapes.Limb(b, new V3(xm + dx, ym, zf), new V3(xm + dx, ym, zf + 0.022f), 0.005f, 0.005f, 4, MetalPart, VoxelPalette.Chrome, caps: false);
                    PolyShapes.Limb(b, new V3(xm - 0.08f, ym, zf + 0.026f), new V3(xm + 0.08f, ym, zf + 0.026f), 0.007f, 0.007f, 6, MetalPart, VoxelPalette.Chrome);
                }
            }
            return b.Build(BuildingModels.PaletteUv);
        }

        // ---------- Office chair ----------
        // Office chair 0.7 × 1.25 m: five arms on castors, a gas lift in a dark shroud, a padded seat ChairSeat high, armrests
        // and a reclined back towards +Z, in `fabric`; the sitter faces −Z.
        public static PolyMesh OfficeChair(byte fabric)
        {
            var b = NewBuilder();
            byte dark = VoxelPalette.MetalDark;
            const int arms = 5; const float reach = 0.29f;
            PolyShapes.Lathe(b, 0, 0, new[] { (0.06f, 0.05f), (0.11f, 0.05f), (0.125f, 0.036f) }, 8, TrimPart, dark, capBottom: true, capTop: true);
            for (int k = 0; k < arms; k++)
            {
                double a = -Math.PI / 2 + 2 * Math.PI * k / arms;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                V3 At(float r, float w, float y) => new V3(ca * r - sa * w, y, sa * r + ca * w);
                PolyShapes.Block(b, new[] { At(0.03f, -0.026f, 0.065f), At(reach, -0.016f, 0.058f), At(reach, 0.016f, 0.058f), At(0.03f, 0.026f, 0.065f) },
                    new[] { At(0.03f, -0.026f, 0.112f), At(reach, -0.016f, 0.084f), At(reach, 0.016f, 0.084f), At(0.03f, 0.026f, 0.112f) }, TrimPart, dark);
                // Castor: a fork down from the tip, the wheel across the arm.
                var c = At(reach - 0.02f, 0, 0);
                PolyShapes.Limb(b, new V3(c.X, 0.06f, c.Z), new V3(c.X, 0.04f, c.Z), 0.012f, 0.012f, 4, TrimPart, dark, caps: false);
                var axle = new V3(-sa, 0, ca) * 0.013f;
                var hub = new V3(c.X, 0.026f, c.Z);
                PolyShapes.Limb(b, hub - axle, hub + axle, 0.026f, 0.026f, 8, TrimPart, VoxelPalette.Tire);
            }
            PolyShapes.Lathe(b, 0, 0, new[] { (0.125f, 0.033f), (0.27f, 0.03f) }, 8, TrimPart, dark, capTop: true);
            PolyShapes.Lathe(b, 0, 0, new[] { (0.27f, 0.019f), (0.42f, 0.019f) }, 8, MetalPart, VoxelPalette.Chrome);
            b.Box(new V3(-0.1f, 0.42f, -0.13f), new V3(0.1f, 0.465f, 0.13f), 0.01f, TrimPart, dark);
            // Seat cushion.
            b.Box(new V3(-0.25f, 0.46f, -0.26f), new V3(0.25f, ChairSeat, 0.22f), 0.035f, TrimPart, fabric);
            // Spine from under the seat up behind it, then the reclined back.
            PolyShapes.Block(b, new[] { new V3(-0.035f, 0.43f, 0.12f), new V3(0.035f, 0.43f, 0.12f), new V3(0.035f, 0.43f, 0.2f), new V3(-0.035f, 0.43f, 0.2f) },
                new[] { new V3(-0.035f, 0.47f, 0.12f), new V3(0.035f, 0.47f, 0.12f), new V3(0.035f, 0.47f, 0.2f), new V3(-0.035f, 0.47f, 0.2f) }, TrimPart, dark);
            PolyShapes.Block(b, new[] { new V3(-0.035f, 0.43f, 0.2f), new V3(0.035f, 0.43f, 0.2f), new V3(0.035f, 0.43f, 0.25f), new V3(-0.035f, 0.43f, 0.25f) },
                new[] { new V3(-0.03f, 0.78f, 0.25f), new V3(0.03f, 0.78f, 0.25f), new V3(0.03f, 0.78f, 0.285f), new V3(-0.03f, 0.78f, 0.285f) }, TrimPart, dark);
            b.Place = Pose(new V3(0, 0.93f, 0.268f), 0, 8);
            b.Box(new V3(-0.23f, -0.29f, -0.035f), new V3(0.23f, 0.29f, 0.035f), 0.03f, TrimPart, fabric);
            b.Place = null;
            // Armrests: a bracket under the seat, a post and a pad.
            foreach (float s in new[] { -1f, 1f })
            {
                float xi = s * 0.1f, xo = s * 0.285f;
                b.Box(new V3(Math.Min(xi, xo), 0.425f, -0.03f), new V3(Math.Max(xi, xo), 0.45f, 0.05f), 0, TrimPart, dark);
                float x0 = s * 0.265f, x1 = s * 0.3f;
                PolyShapes.Block(b, new[] { new V3(x0, 0.425f, -0.02f), new V3(x1, 0.425f, -0.02f), new V3(x1, 0.425f, 0.05f), new V3(x0, 0.425f, 0.05f) },
                    new[] { new V3(x0, 0.7f, 0.0f), new V3(x1, 0.7f, 0.0f), new V3(x1, 0.7f, 0.06f), new V3(x0, 0.7f, 0.06f) }, TrimPart, dark);
                b.Box(new V3(s < 0 ? -0.315f : 0.25f, 0.7f, -0.13f), new V3(s < 0 ? -0.25f : 0.315f, 0.735f, 0.15f), 0.012f, TrimPart, VoxelPalette.Ink);
            }
            return b.Build(BuildingModels.PaletteUv);
        }
    }
}
