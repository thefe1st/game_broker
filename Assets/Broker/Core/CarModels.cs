using System;
using System.Collections.Generic;

namespace Broker.Core
{
    public enum CarStyle { Sedan, Taxi }

    // Submeshes of car bodies and wheels, in this order, one material each. Paint is tinted per car over a grain
    // texture (box-mapped UVs in metres); Detail and Lamp sample VoxelPalette texels (Lamp glows); Glass is dark and
    // glossy; Brake holds the red rear lamps, which Unity switches to a brighter material while the car brakes.
    public enum CarPart { Paint, Detail, Glass, Lamp, Brake }

    // Dimensions shared by the body, the wheels and the traffic model. Metres; the car faces +X, Y is up, +Z is its
    // left; the origin is on the road under the centre of the footprint.
    public sealed class CarShape
    {
        public float Length { get; } public float Width { get; } public float Height { get; }
        public float FrontAxleX { get; } public float RearAxleX { get; } public float Track { get; }
        public float WheelRadius { get; } public float WheelWidth { get; }
        public float Wheelbase => FrontAxleX - RearAxleX;
        internal CarShape(float length, float width, float height, float frontAxleX, float rearAxleX, float track, float wheelRadius, float wheelWidth)
        {
            Length = length; Width = width; Height = height; FrontAxleX = frontAxleX; RearAxleX = rearAxleX; Track = track;
            WheelRadius = wheelRadius; WheelWidth = wheelWidth;
        }
        public const int WheelCount = 4;
        // Wheel centres: 0 front left, 1 front right, 2 rear left, 3 rear right.
        public V3 Wheel(int index)
        {
            if (index < 0 || index >= WheelCount) throw new ArgumentOutOfRangeException(nameof(index));
            return new V3(index < 2 ? FrontAxleX : RearAxleX, WheelRadius, index % 2 == 0 ? Track / 2 : -Track / 2);
        }
        public static bool LeftWheel(int index) => index % 2 == 0;
    }

    public readonly struct CarPaint
    {
        public string Name { get; } public uint Rgb { get; }
        public CarPaint(string name, uint rgb) { Name = name; Rgb = rgb; }
        public byte R => (byte)(Rgb >> 16); public byte G => (byte)(Rgb >> 8); public byte B => (byte)Rgb;
    }

    // Low-poly cars in the manner of Schedule I: boxy bodies with soft chamfers, dark glass, grey steel wheels,
    // simple lamps and bumpers, a light grain on the paint. The first model is a cheap old four-door sedan
    // (a "bucket of bolts"); the taxi is the same body in yellow with a roof sign and a checker band.
    // Built from code: a lofted shell (cross-sections along X with wheel wells and a greenhouse), chamfered boxes and lathed wheels.
    public static class CarModels
    {
        public const float SmoothAngle = 50, PaintTile = 1.6f;
        public static CarShape Sedan { get; } = new CarShape(4.14f, 1.664f, 1.42f, 1.24f, -1.20f, 1.38f, 0.30f, 0.19f);

        private static readonly CarPaint[] paints =
        {
            new CarPaint("вишнёвый", 0x8E2F2A), new CarPaint("красный", 0xB8443A), new CarPaint("белый", 0xDDDBD3),
            new CarPaint("чёрный", 0x2A2C30), new CarPaint("серебристый", 0x9DA1A5), new CarPaint("синий", 0x2F5797),
            new CarPaint("тёмно-синий", 0x26355A), new CarPaint("зелёный", 0x3E6E4B), new CarPaint("бежевый", 0xC9B58E),
            new CarPaint("голубой", 0x73A1C4), new CarPaint("жёлтое такси", 0xE3B43C),
        };
        public static IReadOnlyList<CarPaint> Paints => paints;
        public const int TaxiPaint = 10, StreetPaints = 10;
        public static int PaintFor(CarStyle style, int seed)
            => style == CarStyle.Taxi ? TaxiPaint : (int)(VoxelNoise.Hash(seed, 19, 3, 71) % StreetPaints);

        private const int PaintPart = (int)CarPart.Paint, DetailPart = (int)CarPart.Detail, GlassPart = (int)CarPart.Glass,
            LampPart = (int)CarPart.Lamp, BrakePart = (int)CarPart.Brake, PartCount = 5;

        // ---------- Sedan shell, metres (the first car) ----------
        private const float FrontX = 2.0f, RearX = -2.0f, EndRound = 0.10f;
        private const float FloorY = 0.22f, InnerZ = 0.57f, SillZ = 0.80f, SillY = 0.29f, CreaseZ = 0.832f, CreaseY = 0.70f;
        private const float SideTopZ = 0.815f, ShoulderZ = 0.79f, BeltZ = 0.755f, ArchRadius = 0.375f;
        private const float CowlX = 0.62f, RoofFrontX = 0.12f, RoofRearX = -0.92f, TrunkX = -1.30f;
        private const float BPillarFront = -0.22f, BPillarRear = -0.30f, RearWindowEnd = -0.86f, SeamHalf = 0.006f;
        private static readonly float[] seams = { 0.70f, -0.26f, -0.96f };
        private static readonly float[,] roof = { { 0.645f, 1.365f }, { 0.585f, 1.405f }, { 0.32f, 1.417f }, { 0, 1.42f } };
        public const int SectionPoints = 12;

        // Hood falls 5 cm towards the nose, the trunk 1.5 cm towards the tail; the waist is level under the greenhouse.
        private static float DeckY(float x)
        {
            if (x >= CowlX) return 0.935f - 0.05f * Math.Min(1, (x - CowlX) / (1.90f - CowlX));
            if (x <= TrunkX) return 0.94f - 0.015f * Math.Min(1, (TrunkX - x) / (TrunkX + 1.90f));
            return 0.94f;
        }
        // Top of the wheel arch over a station, or −1 outside both arches. Arches are half circles on the sill line.
        private static float ArchTop(float x)
        {
            foreach (float axle in new[] { Sedan.FrontAxleX, Sedan.RearAxleX })
            {
                float d = x - axle;
                if (Math.Abs(d) < ArchRadius) return SillY + (float)Math.Sqrt(ArchRadius * ArchRadius - d * d);
            }
            return -1;
        }
        private static void Deck(float deck, float[] z, float[] y)
        {
            z[8] = 0.745f; y[8] = deck + 0.002f; z[9] = 0.70f; y[9] = deck + 0.005f;
            z[10] = 0.36f; y[10] = deck + 0.011f; z[11] = 0; y[11] = deck + 0.014f;
        }
        // Half cross-section at station x, (z ≥ 0, y) from the floor centre out and up the left side to the roof centre:
        // 0 floor centre, 1 floor edge, 2–3 wheel-well wall and ceiling (folded flat outside the arches), 3 sill,
        // 4 crease, 5–6 shoulder, 7 waist (window base), 8–9 pillar or roof edge, 10 crown, 11 centre line.
        internal static void Section(float x, float[] z, float[] y)
        {
            float deck = DeckY(x), arch = ArchTop(x);
            float d = Math.Min(FrontX - x, x - RearX), f = d < EndRound ? 1 - Math.Max(0, d) / EndRound : 0;
            float width = 1 - 0.05f * f * f, drop = 0.035f * f * f, floor = FloorY + 0.03f * f * f;
            deck -= drop;
            z[0] = 0; y[0] = floor; z[1] = InnerZ; y[1] = floor;
            z[2] = InnerZ; y[2] = arch >= 0 ? arch : floor;
            z[3] = SillZ; y[3] = arch >= 0 ? arch : SillY;
            z[4] = CreaseZ; y[4] = CreaseY; z[5] = SideTopZ; y[5] = deck - 0.05f; z[6] = ShoulderZ; y[6] = deck - 0.012f;
            z[7] = BeltZ; y[7] = deck;
            if (x >= CowlX || x <= TrunkX) Deck(deck, z, y);
            else
            {
                float t; bool front = x > RoofFrontX, rear = x < RoofRearX;
                var baseZ = new float[SectionPoints]; var baseY = new float[SectionPoints];
                if (front) { t = (CowlX - x) / (CowlX - RoofFrontX); Deck(DeckY(CowlX), baseZ, baseY); }
                else if (rear) { t = (RoofRearX - x) / (RoofRearX - TrunkX); Deck(DeckY(TrunkX), baseZ, baseY); t = 1 - t; }
                else t = 1;
                for (int k = 0; k < 4; k++)
                {
                    float rz = roof[k, 0], ry = roof[k, 1];
                    z[8 + k] = front || rear ? baseZ[8 + k] + (rz - baseZ[8 + k]) * t : rz;
                    y[8 + k] = front || rear ? baseY[8 + k] + (ry - baseY[8 + k]) * t : ry;
                }
            }
            for (int k = 0; k < SectionPoints; k++) z[k] *= width;
        }
        internal static float[] Stations()
        {
            var xs = new List<float> { FrontX, RearX, CowlX, RoofFrontX, RoofRearX, TrunkX, BPillarFront, BPillarRear, RearWindowEnd };
            foreach (float d in new[] { 0.05f, 0.02f, EndRound }) { xs.Add(FrontX - d); xs.Add(RearX + d); }
            foreach (float s in seams) { xs.Add(s - SeamHalf); xs.Add(s + SeamHalf); }
            foreach (float axle in new[] { Sedan.FrontAxleX, Sedan.RearAxleX })
                for (int k = 0; k <= 12; k++) xs.Add(axle + ArchRadius * (float)Math.Cos(Math.PI * k / 12));
            xs.Sort();
            var result = new List<float>();
            foreach (float x in xs) if (result.Count == 0 || x - result[result.Count - 1] > 1e-4f) result.Add(x);
            return result.ToArray();
        }
        private static bool InSeam(float x)
        {
            foreach (float s in seams) if (Math.Abs(x - s) < SeamHalf) return true;
            return false;
        }
        // Surface of section segment j (points j → j + 1) between two stations centred on xm.
        private static (int part, byte color, float shade) Segment(int j, float xm)
        {
            bool greenhouse = xm > TrunkX && xm < CowlX;
            switch (j)
            {
                case 0: case 1: case 2: return (DetailPart, VoxelPalette.Ink, 0.45f);
                case 3: case 4: case 5: return InSeam(xm) ? (DetailPart, VoxelPalette.Ink, 0.8f) : (PaintPart, (byte)0, 1f);
                case 6: return greenhouse ? (DetailPart, VoxelPalette.Ink, 1f) : (PaintPart, (byte)0, 1f);
                case 7:
                    bool window = (xm > BPillarFront && xm < CowlX) || (xm > RearWindowEnd && xm < BPillarRear);
                    return window ? (GlassPart, (byte)0, 1f) : (PaintPart, (byte)0, 1f);
                case 8: return (PaintPart, (byte)0, 1f);
                default:
                    bool screen = (xm > RoofFrontX && xm < CowlX) || (xm > TrunkX && xm < RoofRearX);
                    return screen ? (GlassPart, (byte)0, 1f) : (PaintPart, (byte)0, 1f);
            }
        }
        // Light falls off towards the road: the sills and wheels sit in their own contact shade.
        private static float Ground(V3 p)
        {
            float t = Math.Max(0, Math.Min(1, (p.Y - 0.15f) / 0.6f));
            return 0.68f + 0.32f * t * t * (3 - 2 * t);
        }
        private static PolyBuilder NewBuilder() => new PolyBuilder(PartCount, SmoothAngle, PaintPart, PaintTile);
        private static (float, float) PaletteUv(int part, byte color)
            => part == PaintPart || part == GlassPart ? (0.5f, 0.5f) : (VoxelPalette.U(color), VoxelPalette.V(color));

        public static PolyMesh Body(CarStyle style)
        {
            var b = NewBuilder();
            b.Ambient = Ground;
            Shell(b);
            Front(b); Rear(b); Sides(b);
            if (style == CarStyle.Taxi) Taxi(b);
            return b.Build(PaletteUv);
        }

        private static void Shell(PolyBuilder b)
        {
            var xs = Stations();
            int n = xs.Length;
            var z = new float[n][]; var y = new float[n][];
            for (int i = 0; i < n; i++) { z[i] = new float[SectionPoints]; y[i] = new float[SectionPoints]; Section(xs[i], z[i], y[i]); }
            for (int i = 0; i + 1 < n; i++)
            {
                float xm = (xs[i] + xs[i + 1]) / 2;
                for (int j = 0; j + 1 < SectionPoints; j++)
                {
                    var (part, color, shade) = Segment(j, xm);
                    float dz = (z[i][j + 1] - z[i][j] + z[i + 1][j + 1] - z[i + 1][j]) / 2, dy = (y[i][j + 1] - y[i][j] + y[i + 1][j + 1] - y[i + 1][j]) / 2;
                    for (int side = 1; side >= -1; side -= 2)
                    {
                        var a = new V3(xs[i], y[i][j], side * z[i][j]); var bb = new V3(xs[i], y[i][j + 1], side * z[i][j + 1]);
                        var c = new V3(xs[i + 1], y[i + 1][j + 1], side * z[i + 1][j + 1]); var d = new V3(xs[i + 1], y[i + 1][j], side * z[i + 1][j]);
                        b.Quad(a, bb, c, d, new V3(0, -dz, side * dy), part, color, shade);
                    }
                }
            }
            // End caps close the loft: the nose and the tail panels behind the grille and the lamps.
            foreach (int i in new[] { 0, n - 1 })
            {
                var loop = new List<V3>();
                for (int k = 0; k < SectionPoints; k++) loop.Add(new V3(xs[i], y[i][k], z[i][k]));
                for (int k = SectionPoints - 2; k >= 1; k--) loop.Add(new V3(xs[i], y[i][k], -z[i][k]));
                b.Polygon(loop, new V3(i == 0 ? -1 : 1, 0, 0), PaintPart, 0);
            }
        }
        // A box mirrored to the car's side (side = 1 left, −1 right): z0 < z1 are distances from the centre line.
        private static void SideBox(PolyBuilder b, int side, float x0, float x1, float y0, float y1, float z0, float z1, float bevel, int part, byte color, float shade = 1)
            => b.Box(new V3(x0, y0, side > 0 ? z0 : -z1), new V3(x1, y1, side > 0 ? z1 : -z0), bevel, part, color, shade);
        private static void Plate(PolyBuilder b, float faceX, float y0, float y1, int facing)
        {
            float back = faceX - facing * 0.016f;
            b.Box(new V3(Math.Min(faceX, back), y0, -0.25f), new V3(Math.Max(faceX, back), y1, 0.25f), 0.004f, DetailPart, VoxelPalette.Cream);
            float ym = (y0 + y1) / 2;
            for (int k = 0; k < 6; k++)
            {
                float zc = -0.185f + k * 0.066f, h = k == 0 || k >= 4 ? 0.022f : 0.03f;
                float out1 = faceX + facing * 0.003f;
                b.Box(new V3(Math.Min(faceX - facing * 0.002f, out1), ym - h, zc - 0.021f), new V3(Math.Max(faceX - facing * 0.002f, out1), ym + h, zc + 0.021f), 0, DetailPart, VoxelPalette.Ink);
            }
            b.Box(new V3(Math.Min(faceX - facing * 0.002f, faceX + facing * 0.003f), ym - 0.03f, 0.205f), new V3(Math.Max(faceX - facing * 0.002f, faceX + facing * 0.003f), ym + 0.03f, 0.235f), 0, DetailPart, VoxelPalette.Ink);
        }
        private static void Front(PolyBuilder b)
        {
            // Bumper with a bright strip and short side wraps; plate with characters; amber indicators.
            b.Box(new V3(1.965f, 0.30f, -0.80f), new V3(2.065f, 0.47f, 0.80f), 0.03f, DetailPart, VoxelPalette.MetalDark);
            b.Box(new V3(2.0f, 0.452f, -0.775f), new V3(2.07f, 0.474f, 0.775f), 0.008f, DetailPart, VoxelPalette.Chrome);
            Plate(b, 2.07f, 0.335f, 0.445f, 1);
            // Grille between the headlights: dark recess, grey slats, bright frame and a small badge.
            b.Box(new V3(1.99f, 0.565f, -0.405f), new V3(2.012f, 0.79f, 0.405f), 0.006f, DetailPart, VoxelPalette.Ink);
            for (int k = 0; k < 4; k++)
            {
                float y = 0.597f + k * 0.05f;
                b.Box(new V3(2.006f, y, -0.38f), new V3(2.017f, y + 0.016f, 0.38f), 0.004f, DetailPart, VoxelPalette.Metal);
            }
            foreach (float y in new[] { 0.557f, 0.782f })
                b.Box(new V3(2.004f, y, -0.41f), new V3(2.02f, y + 0.016f, 0.41f), 0.005f, DetailPart, VoxelPalette.Chrome);
            b.Box(new V3(2.016f, 0.66f, -0.035f), new V3(2.026f, 0.73f, 0.035f), 0.006f, DetailPart, VoxelPalette.Chrome);
            // Wipers resting on the cowl.
            b.Box(new V3(0.628f, 0.946f, -0.60f), new V3(0.642f, 0.958f, -0.08f), 0.004f, DetailPart, VoxelPalette.Ink);
            b.Box(new V3(0.628f, 0.946f, 0.0f), new V3(0.642f, 0.958f, 0.52f), 0.004f, DetailPart, VoxelPalette.Ink);
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, 1.70f, 1.99f, 0.31f, 0.46f, 0.78f, 0.845f, 0.025f, DetailPart, VoxelPalette.MetalDark);
                SideBox(b, side, 2.055f, 2.071f, 0.37f, 0.43f, 0.58f, 0.74f, 0.005f, DetailPart, VoxelPalette.Amber);
                // Square twin headlights in a bright surround.
                SideBox(b, side, 1.99f, 2.012f, 0.575f, 0.80f, 0.42f, 0.765f, 0.008f, DetailPart, VoxelPalette.Chrome);
                SideBox(b, side, 2.006f, 2.02f, 0.59f, 0.785f, 0.435f, 0.592f, 0.005f, LampPart, VoxelPalette.Headlight);
                SideBox(b, side, 2.006f, 2.02f, 0.59f, 0.785f, 0.606f, 0.75f, 0.005f, LampPart, VoxelPalette.Headlight);
            }
        }
        private static void Rear(PolyBuilder b)
        {
            b.Box(new V3(-2.065f, 0.30f, -0.80f), new V3(-1.965f, 0.47f, 0.80f), 0.03f, DetailPart, VoxelPalette.MetalDark);
            b.Box(new V3(-2.07f, 0.452f, -0.775f), new V3(-2.0f, 0.474f, 0.775f), 0.008f, DetailPart, VoxelPalette.Chrome);
            Plate(b, -2.011f, 0.615f, 0.725f, -1);
            b.Box(new V3(-2.008f, 0.745f, -0.03f), new V3(-1.995f, 0.775f, 0.03f), 0.004f, DetailPart, VoxelPalette.Chrome);
            b.Box(new V3(-2.07f, 0.235f, -0.56f), new V3(-1.85f, 0.275f, -0.50f), 0.01f, DetailPart, VoxelPalette.MetalDark);
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, -1.99f, -1.66f, 0.31f, 0.46f, 0.78f, 0.845f, 0.025f, DetailPart, VoxelPalette.MetalDark);
                // Wide rear lamps: amber indicator outside, red brake lamp, white reverse lamp, red tail lamp inside; both reds
                // are Brake, so the whole red of the cluster lights up when the car brakes.
                SideBox(b, side, -2.012f, -1.99f, 0.60f, 0.82f, 0.35f, 0.765f, 0.008f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, -2.022f, -2.006f, 0.612f, 0.808f, 0.645f, 0.752f, 0.004f, DetailPart, VoxelPalette.Amber);
                SideBox(b, side, -2.022f, -2.006f, 0.612f, 0.808f, 0.495f, 0.638f, 0.004f, BrakePart, VoxelPalette.TailLight);
                SideBox(b, side, -2.022f, -2.006f, 0.612f, 0.808f, 0.435f, 0.488f, 0.004f, DetailPart, VoxelPalette.Cream);
                SideBox(b, side, -2.022f, -2.006f, 0.612f, 0.808f, 0.365f, 0.428f, 0.004f, BrakePart, VoxelPalette.TailLight);
            }
        }
        private static void Sides(PolyBuilder b)
        {
            for (int side = 1; side >= -1; side -= 2)
            {
                // Mirrors on the door corners, glass facing back; door handles near the rear edge of each door.
                SideBox(b, side, 0.50f, 0.545f, 0.945f, 0.975f, 0.77f, 0.865f, 0.006f, DetailPart, VoxelPalette.MetalDark);
                SideBox(b, side, 0.455f, 0.565f, 0.935f, 1.035f, 0.85f, 0.94f, 0.018f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, 0.449f, 0.46f, 0.947f, 1.023f, 0.862f, 0.928f, 0.003f, DetailPart, VoxelPalette.Chrome);
                foreach (float x in new[] { -0.20f, -0.92f })
                    SideBox(b, side, x, x + 0.11f, 0.846f, 0.863f, 0.81f, 0.829f, 0.004f, DetailPart, VoxelPalette.Chrome);
            }
        }
        // Body surface z (left side) at station x and height y, between the crease and the shoulder.
        internal static float SideZ(float x, float y)
        {
            var z = new float[SectionPoints]; var h = new float[SectionPoints]; Section(x, z, h);
            for (int k = 3; k < 7; k++)
                if (y >= h[k] && y <= h[k + 1]) return z[k] + (z[k + 1] - z[k]) * (y - h[k]) / Math.Max(1e-6f, h[k + 1] - h[k]);
            return z[4];
        }
        private static void Taxi(PolyBuilder b)
        {
            b.Box(new V3(-0.40f, 1.41f, -0.22f), new V3(-0.10f, 1.44f, 0.22f), 0.01f, DetailPart, VoxelPalette.Ink);
            b.Box(new V3(-0.43f, 1.435f, -0.27f), new V3(-0.07f, 1.57f, 0.27f), 0.025f, LampPart, VoxelPalette.SignalAmber);
            // Checker band on the doors and wings: two rows of 5.5 cm squares just off the paint.
            const float cell = 0.055f, y0 = 0.73f, x0 = -1.65f;
            int columns = (int)Math.Round(-2 * x0 / cell);
            for (int side = 1; side >= -1; side -= 2)
                for (int c = 0; c < columns; c++)
                    for (int r = 0; r < 2; r++)
                    {
                        float xa = x0 + c * cell, xb = xa + cell, ya = y0 + r * cell, yb = ya + cell;
                        V3 P(float x, float y) => new V3(x, y, side * (SideZ(x, y) + 0.003f));
                        b.Quad(P(xa, ya), P(xb, ya), P(xb, yb), P(xa, yb), new V3(0, 0, side), DetailPart, (c + r) % 2 == 0 ? VoxelPalette.Ink : VoxelPalette.Cream);
                    }
        }

        // ---------- Wheel: axle along Z, outer face +Z, centre at the origin ----------
        public const int WheelSegments = 16;
        // Profile (radius, z) from the inner hub round the tyre to the outer hubcap, with each segment's colour and shade.
        private static readonly float[,] wheelProfile =
        {
            { 0, -0.07f }, { 0.165f, -0.07f }, { 0.20f, -0.088f }, { 0.262f, -0.095f }, { 0.292f, -0.083f }, { 0.30f, -0.052f },
            { 0.30f, 0.052f }, { 0.292f, 0.083f }, { 0.262f, 0.095f }, { 0.20f, 0.088f }, { 0.19f, 0.08f }, { 0.172f, 0.058f },
            { 0.14f, 0.058f }, { 0.098f, 0.064f }, { 0.072f, 0.074f }, { 0.045f, 0.084f }, { 0, 0.088f },
        };
        private static readonly byte[] wheelColor =
        {
            VoxelPalette.Ink, VoxelPalette.Tire, VoxelPalette.Tire, VoxelPalette.Tire, VoxelPalette.Tire, VoxelPalette.Tire, VoxelPalette.Tire,
            VoxelPalette.Tire, VoxelPalette.Tire, VoxelPalette.MetalLight, VoxelPalette.MetalLight, VoxelPalette.Metal, VoxelPalette.MetalLight,
            VoxelPalette.MetalLight, VoxelPalette.Chrome, VoxelPalette.Chrome,
        };
        private static readonly float[] wheelShade = { 0.45f, 0.7f, 0.8f, 0.85f, 0.85f, 0.85f, 0.9f, 0.95f, 0.95f, 1, 0.8f, 0.85f, 1, 1, 1, 1 };
        // Segment of the disc that alternates with dark vent holes.
        public const int VentSegment = 12, Vents = WheelSegments / 2;
        public static PolyMesh Wheel()
        {
            var b = NewBuilder();
            float scale = Sedan.WheelRadius / 0.30f;
            int points = wheelProfile.GetLength(0);
            for (int j = 0; j + 1 < points; j++)
            {
                float r0 = wheelProfile[j, 0] * scale, z0 = wheelProfile[j, 1] * scale, r1 = wheelProfile[j + 1, 0] * scale, z1 = wheelProfile[j + 1, 1] * scale;
                for (int k = 0; k < WheelSegments; k++)
                {
                    double a0 = 2 * Math.PI * k / WheelSegments, a1 = 2 * Math.PI * (k + 1) / WheelSegments, am = (a0 + a1) / 2;
                    float c0 = (float)Math.Cos(a0), s0 = (float)Math.Sin(a0), c1 = (float)Math.Cos(a1), s1 = (float)Math.Sin(a1);
                    var radial = new V3((float)Math.Cos(am), (float)Math.Sin(am), 0);
                    var outward = radial * (z1 - z0) + new V3(0, 0, -(r1 - r0));
                    byte color = j == VentSegment && k % 2 == 0 ? VoxelPalette.Ink : wheelColor[j];
                    float shade = j == VentSegment && k % 2 == 0 ? 0.5f : wheelShade[j];
                    b.Quad(new V3(r0 * c0, r0 * s0, z0), new V3(r1 * c0, r1 * s0, z1), new V3(r1 * c1, r1 * s1, z1), new V3(r0 * c1, r0 * s1, z0),
                        outward, DetailPart, color, shade);
                }
            }
            return b.Build(PaletteUv);
        }

        // ---------- Paint grain: tileable, light mottling, specks and fine scratches ----------
        public const int GrainSize = 128;
        public static byte[] GrainRgba()
        {
            const int n = GrainSize;
            var value = new float[n * n];
            float Lattice(int x, int y, int cells, int seed) => VoxelNoise.Value(((x % cells) + cells) % cells, ((y % cells) + cells) % cells, 0, seed);
            float Smooth(float px, float py, int cells, int seed)
            {
                float fx = px * cells / n, fy = py * cells / n; int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
                float tx = fx - ix, ty = fy - iy; tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                float a = Lattice(ix, iy, cells, seed), b = Lattice(ix + 1, iy, cells, seed), c = Lattice(ix, iy + 1, cells, seed), d = Lattice(ix + 1, iy + 1, cells, seed);
                return a + (b - a) * tx + (c - a) * ty + (a - b - c + d) * tx * ty;
            }
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = 0.965f + 0.05f * (Smooth(x, y, 8, 301) - 0.5f) + 0.024f * (Smooth(x, y, 32, 302) - 0.5f);
                    uint h = VoxelNoise.Hash(x, y, 1, 303) % 1000;
                    if (h < 7) v -= 0.07f + 0.05f * VoxelNoise.Value(x, y, 2, 304);
                    value[y * n + x] = v;
                }
            for (int s = 0; s < 14; s++)
            {
                float sx = VoxelNoise.Value(s, 1, 0, 305) * n, sy = VoxelNoise.Value(s, 2, 0, 305) * n;
                float angle = (VoxelNoise.Value(s, 3, 0, 305) - 0.5f) * 1.2f, length = 6 + 16 * VoxelNoise.Value(s, 4, 0, 305);
                for (int t = 0; t < length; t++)
                {
                    int x = (((int)(sx + t * Math.Cos(angle))) % n + n) % n, y = (((int)(sy + t * Math.Sin(angle))) % n + n) % n;
                    value[y * n + x] += 0.035f;
                }
            }
            var rgba = new byte[n * n * 4];
            for (int i = 0; i < n * n; i++)
            {
                byte g = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(value[i] * 255)));
                rgba[i * 4] = g; rgba[i * 4 + 1] = g; rgba[i * 4 + 2] = g; rgba[i * 4 + 3] = 255;
            }
            return rgba;
        }
    }
}
