using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Facade geometry of the low-poly buildings: a flat wall with rectangular openings in a few greedy quads, window and
    // door openings with reveals, glass and frames, ledges (sills, lintels, bands, cornices) and striped awnings.
    public static partial class BuildingModels
    {
        private static readonly V3 Up = new V3(0, 1, 0), Down = new V3(0, -1, 0);

        // One outer wall seen from outside: Origin is its bottom left corner on the ground, U runs along it to the right and
        // N points out of the building. Facade coordinates: u along the wall, v up, o outwards (negative is inside the wall).
        private readonly struct Face
        {
            public readonly V3 Origin, U, N; public readonly float Length;
            public Face(V3 origin, V3 u, V3 n, float length) { Origin = origin; U = u; N = n; Length = length; }
            public V3 At(float u, float v, float o = 0)
                => new V3(Origin.X + U.X * u + N.X * o, Origin.Y + v, Origin.Z + U.Z * u + N.Z * o);
        }
        // Front (−Z), back (+Z), left (−X) and right (+X) walls of a w × d footprint centred on the origin.
        private const int Front = 0, Back = 1, Left = 2, Right = 3;
        private static Face[] Faces(float w, float d)
        {
            float x = w / 2, z = d / 2;
            return new[]
            {
                new Face(new V3(-x, 0, -z), new V3(1, 0, 0), new V3(0, 0, -1), w),
                new Face(new V3(x, 0, z), new V3(-1, 0, 0), new V3(0, 0, 1), w),
                new Face(new V3(-x, 0, z), new V3(0, 0, -1), new V3(-1, 0, 0), d),
                new Face(new V3(x, 0, -z), new V3(0, 0, 1), new V3(1, 0, 0), d),
            };
        }
        private readonly struct Rect
        {
            public readonly float U0, U1, V0, V1;
            public Rect(float u0, float u1, float v0, float v1) { U0 = u0; U1 = u1; V0 = v0; V1 = v1; }
            public bool Contains(float u, float v) => u > U0 && u < U1 && v > V0 && v < V1;
        }
        // How an opening is glazed and dressed.
        private sealed class Sash
        {
            public float Depth = 0.14f, Frame = 0.06f, Bar = 0.05f;
            public byte FrameColor = VoxelPalette.Trim;
            // A cross (mullion and transom at TransomAt of the height), or vertical mullions about every MullionStep metres.
            public bool Cross; public float TransomAt = 0.68f, MullionStep;
            public byte SillColor = VoxelPalette.Trim; public float SillProud = 0.07f, SillHeight = 0.07f;
            public byte LintelColor = VoxelPalette.StoneLight; public float Lintel;
            // A blind drawn down from the top over this share of the pane (0: none).
            public float Blind; public byte BlindColor = VoxelPalette.Cream;
        }
        // Glass darkens towards the bottom of each pane, as if the sky reflected in its upper part.
        private const float RevealShade = 0.8f, FrameProud = 0.03f, GlassLow = 0.6f;

        // The wall between heights v0 and v1 minus the holes. Cells of the grid that the hole edges cut are merged into
        // rectangles, rows first: a regular grid of windows becomes spandrels and piers.
        private static void WallFace(PolyBuilder b, Face f, float v0, float v1, List<Rect> holes,
            int part = (int)BuildingPart.Wall, byte color = 0, float shade = 1)
        {
            var us = new List<float> { 0, f.Length }; var vs = new List<float> { v0, v1 };
            foreach (var h in holes)
            {
                if (h.V1 <= v0 || h.V0 >= v1) continue;
                us.Add(h.U0); us.Add(h.U1); vs.Add(Math.Max(v0, h.V0)); vs.Add(Math.Min(v1, h.V1));
            }
            us = Distinct(us); vs = Distinct(vs);
            int nu = us.Count - 1, nv = vs.Count - 1;
            var solid = new bool[nu, nv];
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    float uc = (us[i] + us[i + 1]) / 2, vc = (vs[j] + vs[j + 1]) / 2; bool open = false;
                    foreach (var h in holes) if (h.Contains(uc, vc)) { open = true; break; }
                    solid[i, j] = !open;
                }
            var used = new bool[nu, nv];
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    if (!solid[i, j] || used[i, j]) continue;
                    int i2 = i; while (i2 + 1 < nu && solid[i2 + 1, j] && !used[i2 + 1, j]) i2++;
                    int j2 = j;
                    while (j2 + 1 < nv)
                    {
                        bool row = true;
                        for (int k = i; k <= i2; k++) if (!solid[k, j2 + 1] || used[k, j2 + 1]) { row = false; break; }
                        // A whole row only: the run must not continue sideways in the next row, or the merge would leave slivers.
                        if (row && ((i > 0 && solid[i - 1, j2 + 1] && !used[i - 1, j2 + 1]) || (i2 + 1 < nu && solid[i2 + 1, j2 + 1] && !used[i2 + 1, j2 + 1]))) row = false;
                        if (!row) break;
                        j2++;
                    }
                    for (int a = i; a <= i2; a++) for (int c = j; c <= j2; c++) used[a, c] = true;
                    Panel(b, f, us[i], us[i2 + 1], vs[j], vs[j2 + 1], 0, part, color, shade);
                }
        }
        private static List<float> Distinct(List<float> values)
        {
            values.Sort(); var list = new List<float>();
            foreach (float v in values) if (list.Count == 0 || v - list[list.Count - 1] > 1e-4f) list.Add(v);
            return list;
        }
        // A flat rectangle on the face plane pushed out by o.
        private static void Panel(PolyBuilder b, Face f, float u0, float u1, float v0, float v1, float o, int part, byte color, float shade)
            => b.Quad(f.At(u0, v0, o), f.At(u1, v0, o), f.At(u1, v1, o), f.At(u0, v1, o), f.N, part, color, shade);

        // A block standing proud of the wall: its front, top, bottom and (unless they meet a neighbour) both ends.
        private static void Ledge(PolyBuilder b, Face f, float u0, float u1, float v0, float v1, float proud, byte color,
            bool ends = true, bool bottom = true, float shade = 1)
        {
            const int part = (int)BuildingPart.Trim;
            Panel(b, f, u0, u1, v0, v1, proud, part, color, shade);
            b.Quad(f.At(u0, v1, 0), f.At(u1, v1, 0), f.At(u1, v1, proud), f.At(u0, v1, proud), Up, part, color, shade);
            if (bottom) b.Quad(f.At(u0, v0, 0), f.At(u1, v0, 0), f.At(u1, v0, proud), f.At(u0, v0, proud), Down, part, color, shade * 0.85f);
            if (!ends) return;
            b.Quad(f.At(u0, v0, 0), f.At(u0, v1, 0), f.At(u0, v1, proud), f.At(u0, v0, proud), f.U * -1, part, color, shade * 0.92f);
            b.Quad(f.At(u1, v0, 0), f.At(u1, v1, 0), f.At(u1, v1, proud), f.At(u1, v0, proud), f.U, part, color, shade * 0.92f);
        }
        // A ledge around the whole building between heights v0 and v1. Front and back run over the corners, the sides stop
        // against them, so nothing overlaps. Doors (face, u range) interrupt it.
        private static void Belt(PolyBuilder b, Face[] faces, float v0, float v1, float proud, byte color, bool back = true,
            IList<(int face, float u0, float u1)> gaps = null)
        {
            for (int k = 0; k < 4; k++)
            {
                if (k == Back && !back) continue;
                var f = faces[k]; bool across = k == Front || k == Back;
                float a = across ? -proud : 0, z = across ? f.Length + proud : f.Length;
                float start = a; bool cut = false;
                if (gaps != null)
                    foreach (var (face, g0, g1) in gaps)
                    {
                        if (face != k) continue;
                        if (g0 > start) Ledge(b, f, start, g0, v0, v1, proud, color, across || cut);
                        start = Math.Max(start, g1); cut = true;
                    }
                if (z > start) Ledge(b, f, start, z, v0, v1, proud, color, across || cut);
            }
        }
        // The reveals of an opening: the wall turns in to the glass, slightly in shade.
        private static void Reveal(PolyBuilder b, Face f, Rect r, float depth, bool bottom = true)
        {
            const int wall = (int)BuildingPart.Wall;
            b.Quad(f.At(r.U0, r.V0), f.At(r.U0, r.V1), f.At(r.U0, r.V1, -depth), f.At(r.U0, r.V0, -depth), f.U, wall, 0, RevealShade);
            b.Quad(f.At(r.U1, r.V0), f.At(r.U1, r.V1), f.At(r.U1, r.V1, -depth), f.At(r.U1, r.V0, -depth), f.U * -1, wall, 0, RevealShade);
            b.Quad(f.At(r.U0, r.V1), f.At(r.U1, r.V1), f.At(r.U1, r.V1, -depth), f.At(r.U0, r.V1, -depth), Down, wall, 0, RevealShade * 0.9f);
            if (bottom) b.Quad(f.At(r.U0, r.V0), f.At(r.U1, r.V0), f.At(r.U1, r.V0, -depth), f.At(r.U0, r.V0, -depth), Up, wall, 0, RevealShade);
        }
        // A glazed opening: reveals, the pane, a frame with its bars standing just proud of the glass, a sill and a lintel.
        // Through a wall `through` metres thick the reveals run on to its inner face and the glass shows into the room too.
        private static void Window(PolyBuilder b, Face f, List<Rect> holes, Rect r, Sash s, int glass, float through = 0)
        {
            const int trim = (int)BuildingPart.Trim;
            holes.Add(r);
            Reveal(b, f, r, Math.Max(s.Depth, through));
            Pane(b, f, r, -s.Depth, glass);
            if (through > s.Depth)
            {
                float inner = -s.Depth - 0.003f;
                b.Quad(f.At(r.U0, r.V0, inner), f.At(r.U1, r.V0, inner), f.At(r.U1, r.V1, inner), f.At(r.U0, r.V1, inner), f.N * -1, glass, 0, 0.85f);
            }
            float o = -s.Depth + FrameProud, fw = s.Frame, bar = s.Bar;
            if (s.Blind > 0) Panel(b, f, r.U0 + fw, r.U1 - fw, r.V1 - fw - (r.V1 - r.V0 - 2 * fw) * s.Blind, r.V1 - fw, -s.Depth + 0.01f, trim, s.BlindColor, 0.9f);
            Panel(b, f, r.U0, r.U1, r.V0, r.V0 + fw, o, trim, s.FrameColor, 1);
            Panel(b, f, r.U0, r.U1, r.V1 - fw, r.V1, o, trim, s.FrameColor, 1);
            Panel(b, f, r.U0, r.U0 + fw, r.V0 + fw, r.V1 - fw, o, trim, s.FrameColor, 1);
            Panel(b, f, r.U1 - fw, r.U1, r.V0 + fw, r.V1 - fw, o, trim, s.FrameColor, 1);
            if (s.Cross)
            {
                float uc = (r.U0 + r.U1) / 2, vt = r.V0 + (r.V1 - r.V0) * s.TransomAt;
                Panel(b, f, uc - bar / 2, uc + bar / 2, r.V0 + fw, r.V1 - fw, o, trim, s.FrameColor, 1);
                Panel(b, f, r.U0 + fw, uc - bar / 2, vt - bar / 2, vt + bar / 2, o, trim, s.FrameColor, 1);
                Panel(b, f, uc + bar / 2, r.U1 - fw, vt - bar / 2, vt + bar / 2, o, trim, s.FrameColor, 1);
            }
            else if (s.MullionStep > 0)
            {
                int panes = Math.Max(1, (int)Math.Round((r.U1 - r.U0) / s.MullionStep));
                for (int k = 1; k < panes; k++)
                {
                    float u = r.U0 + (r.U1 - r.U0) * k / panes;
                    Panel(b, f, u - bar / 2, u + bar / 2, r.V0 + fw, r.V1 - fw, o, trim, s.FrameColor, 1);
                }
            }
            if (s.SillProud > 0) Ledge(b, f, r.U0 - 0.06f, r.U1 + 0.06f, r.V0 - s.SillHeight, r.V0, s.SillProud, s.SillColor);
            if (s.Lintel > 0) Ledge(b, f, r.U0 - 0.08f, r.U1 + 0.08f, r.V1, r.V1 + s.Lintel, 0.035f, s.LintelColor);
        }
        // The glass of an opening at depth o with its sky gradient.
        private static void Pane(PolyBuilder b, Face f, Rect r, float o, int glass)
        {
            var ambient = b.Ambient; float v0 = r.V0, v1 = r.V1;
            b.Ambient = p => ambient(p) * (GlassLow + (1 - GlassLow) * Clamp01((p.Y - v0) / (v1 - v0)));
            Panel(b, f, r.U0, r.U1, r.V0, r.V1, o, glass, 0, 1);
            b.Ambient = ambient;
        }
        // A door from the ground: a leaf (or two) with a glazed upper part, a solid kick panel, a frame, handles, a stone
        // threshold. Without glass the leaf is solid (service doors).
        private static void Door(PolyBuilder b, Face f, List<Rect> holes, float u0, float u1, float top, byte leaf, bool glazed, int glass)
        {
            const int trim = (int)BuildingPart.Trim;
            const float depth = 0.16f, fw = 0.08f;
            var r = new Rect(u0, u1, 0, top);
            holes.Add(r);
            Reveal(b, f, r, depth, false);
            b.Quad(f.At(u0, 0.01f), f.At(u1, 0.01f), f.At(u1, 0.01f, -depth), f.At(u0, 0.01f, -depth), Up, trim, VoxelPalette.StoneDark, 0.85f);
            float o = -depth + FrameProud;
            if (glazed)
            {
                Pane(b, f, new Rect(u0, u1, 0.01f, top), -depth, glass);
                Panel(b, f, u0 + fw, u1 - fw, 0.01f, 0.95f, o, trim, leaf, 1);
            }
            else Panel(b, f, u0 + fw, u1 - fw, 0.01f, top - fw, o, trim, leaf, 0.95f);
            Panel(b, f, u0, u1, top - fw, top, o, trim, leaf, 1);
            Panel(b, f, u0, u0 + fw, 0.01f, top - fw, o, trim, leaf, 1);
            Panel(b, f, u1 - fw, u1, 0.01f, top - fw, o, trim, leaf, 1);
            bool pair = u1 - u0 > 1.5f; float uc = (u0 + u1) / 2;
            if (pair) Panel(b, f, uc - fw / 2, uc + fw / 2, 0.01f, top - fw, o, trim, leaf, 1);
            if (glazed && top > 2.6f) Panel(b, f, u0 + fw, u1 - fw, 2.25f, 2.25f + fw, o, trim, leaf, 1);
            float handle = pair ? uc - 0.16f : u1 - fw - 0.12f;
            HandleBar(b, f, handle, 1.05f, o);
            if (pair) HandleBar(b, f, uc + 0.12f, 1.05f, o);
        }
        private static void HandleBar(PolyBuilder b, Face f, float u, float v, float o)
            => FaceBox(b, f, u, u + 0.04f, v - 0.15f, v + 0.15f, o, o + 0.05f, (int)BuildingPart.Trim, VoxelPalette.Brass, 0);
        // An axis-aligned box given in facade coordinates (the faces are axis-aligned, so its corners span it).
        private static void FaceBox(PolyBuilder b, Face f, float u0, float u1, float v0, float v1, float o0, float o1, int part, byte color, float bevel, float shade = 1)
        {
            V3 p = f.At(u0, v0, o0), q = f.At(u1, v1, o1);
            b.Box(new V3(Math.Min(p.X, q.X), Math.Min(p.Y, q.Y), Math.Min(p.Z, q.Z)), new V3(Math.Max(p.X, q.X), Math.Max(p.Y, q.Y), Math.Max(p.Z, q.Z)),
                bevel, part, color, shade);
        }
        // A sloped awning from the wall at height top down to low, `reach` metres out, in stripes about `stripe` metres wide;
        // a striped valance hangs from its front edge, triangular cheeks close the ends, the underside is in shade.
        private static void Awning(PolyBuilder b, Face f, float u0, float u1, float top, float low, float reach, byte a, byte c, float stripe = 0.5f)
        {
            const int trim = (int)BuildingPart.Trim;
            const float valance = 0.25f, lift = 0.005f;
            var slope = (f.N * (top - low) + Up * reach).Normalized;
            int stripes = Math.Max(1, (int)Math.Round((u1 - u0) / stripe));
            for (int k = 0; k < stripes; k++)
            {
                float s0 = u0 + (u1 - u0) * k / stripes, s1 = u0 + (u1 - u0) * (k + 1) / stripes; byte color = k % 2 == 0 ? a : c;
                b.Quad(f.At(s0, top, lift), f.At(s1, top, lift), f.At(s1, low, reach), f.At(s0, low, reach), slope, trim, color, 1);
                b.Quad(f.At(s0, low, reach), f.At(s1, low, reach), f.At(s1, low - valance, reach), f.At(s0, low - valance, reach), f.N, trim, color, 1);
            }
            b.Quad(f.At(u0, top, lift), f.At(u1, top, lift), f.At(u1, low, reach), f.At(u0, low, reach), slope * -1, trim, a, 0.6f);
            b.Quad(f.At(u0, low, reach), f.At(u1, low, reach), f.At(u1, low - valance, reach), f.At(u0, low - valance, reach), f.N * -1, trim, a, 0.6f);
            foreach (float u in new[] { u0, u1 })
            {
                var side = u == u0 ? f.U * -1 : f.U;
                b.Triangle(f.At(u, top, lift), f.At(u, low, reach), f.At(u, low, lift), side, trim, a, 0.9f);
                b.Triangle(f.At(u, top, lift), f.At(u, low, reach), f.At(u, low, lift), side * -1, trim, a, 0.6f);
                b.Quad(f.At(u, low, reach), f.At(u, low - valance, reach), f.At(u, low - valance, reach - 0.02f), f.At(u, low, reach - 0.02f), side, trim, a, 0.9f);
            }
        }
        // Even bays along a wall, each about `target` metres wide (at least one): the window centres.
        private static float[] Bays(float length, float target, float margin = 0)
        {
            float usable = length - 2 * margin;
            int n = Math.Max(1, (int)Math.Floor(usable / target + 0.5f));
            var centres = new float[n];
            for (int i = 0; i < n; i++) centres[i] = margin + usable * (i + 0.5f) / n;
            return centres;
        }
        private static float Clamp01(float v) => Math.Max(0, Math.Min(1, v));
    }
}
