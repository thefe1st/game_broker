using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // Body styles: the old four-door sedan and its taxi, a small five-door hatchback, a pickup with an open bed and a
    // cargo van. New styles go at the end, so stored values keep their meaning.
    public enum CarStyle { Sedan, Taxi, Hatchback, Pickup, Van }

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
    // simple lamps and bumpers, a light grain on the paint. Cheap old cars of four bodies: the four-door sedan (a
    // "bucket of bolts"; the taxi is the same body in yellow with a roof sign and a checker band), a small five-door
    // hatchback, a pickup with an open bed and a tall cargo van. Built from code: a lofted shell (cross-sections along X
    // with wheel wells and a greenhouse, one BodyPlan per body), chamfered boxes and lathed wheels.
    public static partial class CarModels
    {
        public const float SmoothAngle = 50, PaintTile = 1.6f;
        public static CarShape Sedan { get; } = new CarShape(4.14f, 1.664f, 1.42f, 1.24f, -1.20f, 1.38f, 0.30f, 0.19f);
        public static CarShape Hatchback { get; } = new CarShape(3.70f, 1.62f, 1.44f, 1.13f, -1.14f, 1.35f, 0.285f, 0.175f);
        public static CarShape Pickup { get; } = new CarShape(4.90f, 1.78f, 1.74f, 1.58f, -1.42f, 1.50f, 0.34f, 0.22f);
        public static CarShape Van { get; } = new CarShape(4.80f, 1.86f, 2.04f, 1.48f, -1.52f, 1.56f, 0.33f, 0.21f);
        // The taxi is a sedan.
        public static CarShape Shape(CarStyle style)
        {
            switch (style)
            {
                case CarStyle.Sedan: case CarStyle.Taxi: return Sedan;
                case CarStyle.Hatchback: return Hatchback;
                case CarStyle.Pickup: return Pickup;
                case CarStyle.Van: return Van;
                default: throw new ArgumentOutOfRangeException(nameof(style));
            }
        }

        private static readonly CarPaint[] paints =
        {
            new CarPaint("вишнёвый", 0x8E2F2A), new CarPaint("красный", 0xB8443A), new CarPaint("белый", 0xDDDBD3),
            new CarPaint("чёрный", 0x2A2C30), new CarPaint("серебристый", 0x9DA1A5), new CarPaint("синий", 0x2F5797),
            new CarPaint("тёмно-синий", 0x26355A), new CarPaint("зелёный", 0x3E6E4B), new CarPaint("бежевый", 0xC9B58E),
            new CarPaint("голубой", 0x73A1C4), new CarPaint("жёлтое такси", 0xE3B43C),
        };
        public static IReadOnlyList<CarPaint> Paints => paints;
        public const int TaxiPaint = 10, StreetPaints = 10, WhitePaint = 2, SilverPaint = 4;
        // Taxis are yellow; work vans white, every third one silver; the rest take any street colour.
        public static int PaintFor(CarStyle style, int seed)
        {
            uint roll = VoxelNoise.Hash(seed, 19, 3, 71);
            if (style == CarStyle.Taxi) return TaxiPaint;
            if (style == CarStyle.Van) return roll % 3 == 0 ? SilverPaint : WhitePaint;
            return (int)(roll % StreetPaints);
        }

        private const int PaintPart = (int)CarPart.Paint, DetailPart = (int)CarPart.Detail, GlassPart = (int)CarPart.Glass,
            LampPart = (int)CarPart.Lamp, BrakePart = (int)CarPart.Brake, PartCount = 5;
        public const int SectionPoints = 12;

        // ---------- Body plans, metres ----------
        // The sedan, the first car.
        private static readonly BodyPlan sedanPlan = new BodyPlan
        {
            Shape = Sedan, FrontX = 2.0f, RearX = -2.0f,
            FloorY = 0.22f, InnerZ = 0.57f, SillZ = 0.80f, SillY = 0.29f, CreaseZ = 0.832f, CreaseY = 0.70f,
            SideTopZ = 0.815f, ShoulderZ = 0.79f, BeltZ = 0.755f, ArchRadius = 0.375f,
            HoodY = 0.935f, HoodDrop = 0.05f, BeltY = 0.94f, TailY = 0.94f, TailDrop = 0.015f,
            CowlX = 0.62f, RoofFrontX = 0.12f, RoofRearX = -0.92f, TrunkX = -1.30f,
            Windows = new[] { -0.22f, 0.62f, -0.86f, -0.30f }, Seams = new[] { 0.70f, -0.26f, -0.96f },
            Roof = new[,] { { 0.645f, 1.365f }, { 0.585f, 1.405f }, { 0.32f, 1.417f }, { 0, 1.42f } },
            Crown = new[,] { { 0.745f, 0.002f }, { 0.70f, 0.005f }, { 0.36f, 0.011f }, { 0, 0.014f } },
        };
        // A short nose, a tall glasshouse with a thick rear pillar and a steep hatch over a stub of a tail.
        private static readonly BodyPlan hatchPlan = new BodyPlan
        {
            Shape = Hatchback, FrontX = 1.78f, RearX = -1.78f,
            FloorY = 0.22f, InnerZ = 0.56f, SillZ = 0.78f, SillY = 0.29f, CreaseZ = 0.81f, CreaseY = 0.70f,
            SideTopZ = 0.795f, ShoulderZ = 0.77f, BeltZ = 0.735f, ArchRadius = 0.36f,
            HoodY = 0.92f, HoodDrop = 0.06f, BeltY = 0.93f, TailY = 0.93f, TailDrop = 0.01f,
            CowlX = 0.80f, RoofFrontX = 0.24f, RoofRearX = -1.30f, TrunkX = -1.64f,
            Windows = new[] { -0.12f, 0.80f, -0.96f, -0.20f }, Seams = new[] { 0.88f, -0.16f, -1.00f },
            Roof = new[,] { { 0.625f, 1.385f }, { 0.565f, 1.425f }, { 0.31f, 1.437f }, { 0, 1.44f } },
            Crown = BodyPlan.CrownOf(0.735f),
        };
        // A long high hood, a two-door cab with small rear quarter windows and an open bed behind its upright back.
        private static readonly BodyPlan pickupPlan = new BodyPlan
        {
            Shape = Pickup, FrontX = 2.38f, RearX = -2.38f,
            FloorY = 0.32f, InnerZ = 0.60f, SillZ = 0.86f, SillY = 0.36f, CreaseZ = 0.89f, CreaseY = 0.74f,
            SideTopZ = 0.875f, ShoulderZ = 0.855f, BeltZ = 0.81f, ArchRadius = 0.41f,
            HoodY = 1.12f, HoodDrop = 0.045f, BeltY = 1.14f, TailY = PickupBedFloor, TailDrop = 0,
            CowlX = 0.86f, RoofFrontX = 0.34f, RoofRearX = -0.80f, TrunkX = PickupCabBack,
            Windows = new[] { -0.30f, 0.86f, -0.72f, -0.38f }, Seams = new[] { 0.94f, -0.34f },
            Roof = new[,] { { 0.735f, 1.69f }, { 0.68f, 1.728f }, { 0.37f, 1.738f }, { 0, 1.74f } },
            Crown = BodyPlan.CrownOf(0.81f), RearScreen = false, Bed = true,
        };
        // A short sloping nose, a long box with a flat roof and an upright back; windows only in the front doors.
        private static readonly BodyPlan vanPlan = new BodyPlan
        {
            Shape = Van, FrontX = 2.33f, RearX = -2.33f,
            FloorY = 0.26f, InnerZ = 0.62f, SillZ = 0.90f, SillY = 0.33f, CreaseZ = 0.93f, CreaseY = 0.78f,
            SideTopZ = 0.925f, ShoulderZ = 0.915f, BeltZ = 0.895f, ArchRadius = 0.40f,
            HoodY = 1.16f, HoodDrop = 0.12f, BeltY = 1.18f, TailY = 1.18f, TailDrop = 0,
            CowlX = 1.56f, RoofFrontX = 0.98f, RoofRearX = -2.329f, TrunkX = -2.3295f,
            Windows = new[] { 0.30f, 1.56f }, Seams = new[] { 1.50f, 0.26f, 0.16f, -1.04f }, TallSeams = true,
            Roof = new[,] { { 0.875f, 1.975f }, { 0.83f, 2.025f }, { 0.46f, 2.038f }, { 0, 2.04f } },
            Crown = BodyPlan.CrownOf(0.895f), RearScreen = false,
        };
        // The pickup's bed: its floor, the top of its walls and the back of the cab.
        public const float PickupBedFloor = 0.88f, PickupBedRail = 1.16f, PickupCabBack = -0.84f;
        private static BodyPlan PlanFor(CarStyle style)
        {
            switch (style)
            {
                case CarStyle.Hatchback: return hatchPlan;
                case CarStyle.Pickup: return pickupPlan;
                case CarStyle.Van: return vanPlan;
                default: Shape(style); return sedanPlan;
            }
        }

        // Side silhouette and cross-section of a lofted body: the car faces +X, the origin is on the road under the
        // middle of the footprint. Bumpers, lamps and mirrors are added around it by each body's dress.
        private sealed class BodyPlan
        {
            public CarShape Shape;
            // Ends of the shell (the bumpers stand 7 cm further out) and the length over which they round off.
            public float FrontX, RearX, EndRound = 0.10f;
            // Cross-section: floor, wheel-well wall, sill, crease (the widest line), side top, shoulder, waist; the arches.
            public float FloorY, InnerZ, SillZ, SillY, CreaseZ, CreaseY, SideTopZ, ShoulderZ, BeltZ, ArchRadius;
            // Deck line: the hood falls from HoodY at the cowl by HoodDrop towards the nose, the waist is level at BeltY
            // under the greenhouse, the tail deck falls from TailY at TrunkX by TailDrop towards the tail.
            public float HoodY, HoodDrop, BeltY, TailY, TailDrop;
            // Greenhouse: windscreen from the cowl up to the roof, roof, rear screen down to the trunk.
            public float CowlX, RoofFrontX, RoofRearX, TrunkX;
            // Side window openings as (from, to) pairs along X; door seams; seams also over the paint above the waist.
            public float[] Windows, Seams; public bool TallSeams;
            // Half cross-sections (z, y) of the roof from its edge to the centre line, and (z, rise) of the decks.
            public float[,] Roof, Crown;
            // The slope between the trunk and the roof is glass; the deck behind the cab is the floor of an open bed.
            public bool RearScreen = true, Bed;
            public const float SeamHalf = 0.006f;
            public static float[,] CrownOf(float beltZ)
                => new[,] { { beltZ - 0.01f, 0.002f }, { beltZ - 0.055f, 0.005f }, { beltZ * 0.4768f, 0.011f }, { 0, 0.014f } };

            public float DeckY(float x)
            {
                if (x >= CowlX) return HoodY - HoodDrop * Math.Min(1, (x - CowlX) / (FrontX - EndRound - CowlX));
                if (x <= TrunkX) return TailY - TailDrop * Math.Min(1, (TrunkX - x) / Math.Max(0.05f, TrunkX - RearX - EndRound));
                return BeltY;
            }
            // Top of the wheel arch over a station, or −1 outside both arches. Arches are half circles on the sill line.
            public float ArchTop(float x)
            {
                foreach (float axle in new[] { Shape.FrontAxleX, Shape.RearAxleX })
                {
                    float d = x - axle;
                    if (Math.Abs(d) < ArchRadius) return SillY + (float)Math.Sqrt(ArchRadius * ArchRadius - d * d);
                }
                return -1;
            }
            private void Deck(float deck, float[] z, float[] y)
            {
                for (int k = 0; k < 4; k++) { z[8 + k] = Crown[k, 0]; y[8 + k] = deck + Crown[k, 1]; }
            }
            // Half cross-section at station x, (z ≥ 0, y) from the floor centre out and up the left side to the roof centre:
            // 0 floor centre, 1 floor edge, 2–3 wheel-well wall and ceiling (folded flat outside the arches), 3 sill,
            // 4 crease, 5–6 shoulder, 7 waist (window base), 8–9 pillar or roof edge, 10 crown, 11 centre line.
            public void Section(float x, float[] z, float[] y)
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
                    // Windscreen and rear screen blend the deck at the waist into the roof; behind a pickup's cab the
                    // bed floor is lower, so the back of the cab rises straight from it.
                    float t; bool front = x > RoofFrontX, rear = x < RoofRearX;
                    var baseZ = new float[SectionPoints]; var baseY = new float[SectionPoints];
                    if (front) { t = (CowlX - x) / (CowlX - RoofFrontX); Deck(HoodY, baseZ, baseY); }
                    else if (rear) { t = (RoofRearX - x) / (RoofRearX - TrunkX); Deck(BeltY, baseZ, baseY); t = 1 - t; }
                    else t = 1;
                    for (int k = 0; k < 4; k++)
                    {
                        float rz = Roof[k, 0], ry = Roof[k, 1];
                        z[8 + k] = front || rear ? baseZ[8 + k] + (rz - baseZ[8 + k]) * t : rz;
                        y[8 + k] = front || rear ? baseY[8 + k] + (ry - baseY[8 + k]) * t : ry;
                    }
                }
                for (int k = 0; k < SectionPoints; k++) z[k] *= width;
            }
            public float[] Stations()
            {
                var xs = new List<float> { FrontX, RearX, CowlX, RoofFrontX, RoofRearX, TrunkX };
                xs.AddRange(Windows);
                foreach (float d in new[] { 0.05f, 0.02f, EndRound }) { xs.Add(FrontX - d); xs.Add(RearX + d); }
                foreach (float s in Seams) { xs.Add(s - SeamHalf); xs.Add(s + SeamHalf); }
                foreach (float axle in new[] { Shape.FrontAxleX, Shape.RearAxleX })
                    for (int k = 0; k <= 12; k++) xs.Add(axle + ArchRadius * (float)Math.Cos(Math.PI * k / 12));
                xs.Sort();
                var result = new List<float>();
                foreach (float x in xs) if (result.Count == 0 || x - result[result.Count - 1] > 1e-4f) result.Add(x);
                return result.ToArray();
            }
            private bool InSeam(float x)
            {
                foreach (float s in Seams) if (Math.Abs(x - s) < SeamHalf) return true;
                return false;
            }
            private bool InWindow(float x)
            {
                for (int k = 0; k + 1 < Windows.Length; k += 2) if (x > Windows[k] && x < Windows[k + 1]) return true;
                return false;
            }
            // Surface of section segment j (points j → j + 1) between two stations centred on xm.
            public (int part, byte color, float shade) Segment(int j, float xm)
            {
                var paint = (PaintPart, (byte)0, 1f); var seam = (DetailPart, VoxelPalette.Ink, 0.8f);
                bool greenhouse = xm > TrunkX && xm < CowlX, bed = Bed && xm < TrunkX, cabBack = Bed && greenhouse && xm < RoofRearX;
                bool tallSeam = TallSeams && InSeam(xm);
                switch (j)
                {
                    case 0: case 1: case 2: return (DetailPart, VoxelPalette.Ink, 0.45f);
                    case 3: case 4: case 5: return InSeam(xm) ? seam : paint;
                    case 6: return greenhouse && !cabBack ? (DetailPart, VoxelPalette.Ink, 1f) : tallSeam ? seam : paint;
                    case 7: return greenhouse && InWindow(xm) ? (GlassPart, (byte)0, 1f) : bed ? Liner : tallSeam ? seam : paint;
                    case 8: return bed ? Liner : paint;
                    default:
                        bool screen = (xm > RoofFrontX && xm < CowlX) || (RearScreen && xm > TrunkX && xm < RoofRearX);
                        return screen ? (GlassPart, (byte)0, 1f) : bed ? Liner : paint;
                }
            }
            private static readonly (int, byte, float) Liner = (DetailPart, VoxelPalette.Tire, 0.9f);
            // Body surface z (left side) at station x and height y, between the crease and the shoulder.
            public float SideZ(float x, float y)
            {
                var z = new float[SectionPoints]; var h = new float[SectionPoints]; Section(x, z, h);
                for (int k = 3; k < 7; k++)
                    if (y >= h[k] && y <= h[k + 1]) return z[k] + (z[k + 1] - z[k]) * (y - h[k]) / Math.Max(1e-6f, h[k + 1] - h[k]);
                return z[4];
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
            var plan = PlanFor(style);
            var b = NewBuilder();
            b.Ambient = Ground;
            Shell(b, plan);
            switch (style)
            {
                case CarStyle.Hatchback: HatchbackDress(b, plan); break;
                case CarStyle.Pickup: PickupDress(b, plan); break;
                case CarStyle.Van: VanDress(b, plan); break;
                default:
                    SedanFront(b); SedanRear(b); SedanSides(b);
                    if (style == CarStyle.Taxi) Taxi(b, plan);
                    break;
            }
            return b.Build(PaletteUv);
        }

        private static void Shell(PolyBuilder b, BodyPlan plan)
        {
            var xs = plan.Stations();
            int n = xs.Length;
            var z = new float[n][]; var y = new float[n][];
            for (int i = 0; i < n; i++) { z[i] = new float[SectionPoints]; y[i] = new float[SectionPoints]; plan.Section(xs[i], z[i], y[i]); }
            for (int i = 0; i + 1 < n; i++)
            {
                float xm = (xs[i] + xs[i + 1]) / 2, dx = xs[i + 1] - xs[i];
                for (int j = 0; j + 1 < SectionPoints; j++)
                {
                    var (part, color, shade) = plan.Segment(j, xm);
                    // Along the section (dz, dy) and along the body (dyx, dzx): their cross product is the outward normal
                    // of the left side, mirrored for the right; it also turns the upright back of a cab the right way.
                    float dz = (z[i][j + 1] - z[i][j] + z[i + 1][j + 1] - z[i + 1][j]) / 2, dy = (y[i][j + 1] - y[i][j] + y[i + 1][j + 1] - y[i + 1][j]) / 2;
                    float dzx = (z[i + 1][j] - z[i][j] + z[i + 1][j + 1] - z[i][j + 1]) / 2, dyx = (y[i + 1][j] - y[i][j] + y[i + 1][j + 1] - y[i][j + 1]) / 2;
                    for (int side = 1; side >= -1; side -= 2)
                    {
                        var a = new V3(xs[i], y[i][j], side * z[i][j]); var bb = new V3(xs[i], y[i][j + 1], side * z[i][j + 1]);
                        var c = new V3(xs[i + 1], y[i + 1][j + 1], side * z[i + 1][j + 1]); var d = new V3(xs[i + 1], y[i + 1][j], side * z[i + 1][j]);
                        b.Quad(a, bb, c, d, new V3(dz * dyx - dy * dzx, -dz * dx, side * dy * dx), part, color, shade);
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
        // The wheel of a body: the same tyre and hubcap scaled to its radius and width.
        public static PolyMesh Wheel(CarStyle style)
        {
            var shape = Shape(style);
            var b = NewBuilder();
            float scale = shape.WheelRadius / 0.30f, across = shape.WheelWidth / 0.19f;
            int points = wheelProfile.GetLength(0);
            for (int j = 0; j + 1 < points; j++)
            {
                float r0 = wheelProfile[j, 0] * scale, z0 = wheelProfile[j, 1] * across, r1 = wheelProfile[j + 1, 0] * scale, z1 = wheelProfile[j + 1, 1] * across;
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
