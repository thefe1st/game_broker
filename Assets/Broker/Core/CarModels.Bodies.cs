using System;

namespace Broker.Core
{
    // Dress of each body: bumpers, plates, grilles, lamps, mirrors, handles and the pickup's bed around the lofted shell.
    public static partial class CarModels
    {
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
        // ---------- Sedan (and taxi): chrome strips, twin square lamps, wide rear clusters ----------
        private static void SedanFront(PolyBuilder b)
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
        private static void SedanRear(PolyBuilder b)
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
        private static void SedanSides(PolyBuilder b)
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
        private static void Taxi(PolyBuilder b, BodyPlan plan)
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
                        V3 P(float x, float y) => new V3(x, y, side * (plan.SideZ(x, y) + 0.003f));
                        b.Quad(P(xa, ya), P(xb, ya), P(xb, yb), P(xa, yb), new V3(0, 0, side), DetailPart, (c + r) % 2 == 0 ? VoxelPalette.Ink : VoxelPalette.Cream);
                    }
        }

        // ---------- Shared details ----------
        // Wiper arms resting on the cowl, reaching `reach` to each side of the centre line.
        private static void Wipers(PolyBuilder b, BodyPlan p, float reach)
        {
            float x = p.CowlX + 0.008f, y = p.HoodY + 0.011f;
            b.Box(new V3(x, y, -reach - 0.02f), new V3(x + 0.014f, y + 0.012f, -0.08f), 0.004f, DetailPart, VoxelPalette.Ink);
            b.Box(new V3(x, y, 0), new V3(x + 0.014f, y + 0.012f, reach), 0.004f, DetailPart, VoxelPalette.Ink);
        }
        // Door mirrors on arms off the window base, the glass facing back: (x, y) is the lower front corner of the head.
        private static void Mirrors(PolyBuilder b, BodyPlan p, float x, float y, float height, float reach)
        {
            float z0 = p.CreaseZ + 0.018f, z1 = z0 + reach;
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, x + 0.045f, x + 0.09f, y + 0.01f, y + 0.04f, p.BeltZ + 0.015f, p.CreaseZ + 0.033f, 0.006f, DetailPart, VoxelPalette.MetalDark);
                SideBox(b, side, x, x + 0.11f, y, y + height, z0, z1, 0.018f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, x - 0.006f, x + 0.005f, y + 0.012f, y + height - 0.012f, z0 + 0.012f, z1 - 0.012f, 0.003f, DetailPart, VoxelPalette.Chrome);
            }
        }
        // Bright door handles standing a centimetre off the side, each starting at one of xs.
        private static void Handles(PolyBuilder b, BodyPlan p, float y, params float[] xs)
        {
            for (int side = 1; side >= -1; side -= 2)
                foreach (float x in xs)
                {
                    float z = p.SideZ(x + 0.055f, y + 0.008f);
                    SideBox(b, side, x, x + 0.11f, y, y + 0.017f, z - 0.009f, z + 0.01f, 0.004f, DetailPart, VoxelPalette.Chrome);
                }
        }
        // A closed band bent round a wheel arch from the sill behind it over the top to the sill in front: radii r0–r1
        // about (cx, cy), z0 < z1 from the centre line, in its own smoothing group so the arc reads round.
        private static void Flare(PolyBuilder b, int side, float cx, float cy, float r0, float r1, float z0, float z1, int segments, byte color)
        {
            int group = b.Group; b.Group = b.NextGroup();
            V3 P(double a, float r, float z) => new V3(cx + r * (float)Math.Cos(a), cy + r * (float)Math.Sin(a), side * z);
            for (int k = 0; k < segments; k++)
            {
                double a0 = Math.PI * k / segments, a1 = Math.PI * (k + 1) / segments, am = (a0 + a1) / 2;
                var radial = new V3((float)Math.Cos(am), (float)Math.Sin(am), 0);
                b.Quad(P(a0, r1, z0), P(a1, r1, z0), P(a1, r1, z1), P(a0, r1, z1), radial, DetailPart, color);
                b.Quad(P(a0, r0, z0), P(a1, r0, z0), P(a1, r0, z1), P(a0, r0, z1), radial * -1, DetailPart, color);
                b.Quad(P(a0, r0, z1), P(a1, r0, z1), P(a1, r1, z1), P(a0, r1, z1), new V3(0, 0, side), DetailPart, color);
                b.Quad(P(a0, r0, z0), P(a1, r0, z0), P(a1, r1, z0), P(a0, r1, z0), new V3(0, 0, -side), DetailPart, color);
            }
            foreach (double a in new[] { 0, Math.PI })
                b.Quad(P(a, r0, z0), P(a, r1, z0), P(a, r1, z1), P(a, r0, z1), new V3(0, -1, 0), DetailPart, color);
            b.Group = group;
        }

        // ---------- Hatchback: plastic bumpers, single wide lamps, a slot grille, tall corner lamps on the hatch ----------
        private static void HatchbackDress(PolyBuilder b, BodyPlan p)
        {
            b.Box(new V3(1.745f, 0.29f, -0.765f), new V3(1.845f, 0.455f, 0.765f), 0.03f, DetailPart, VoxelPalette.MetalDark);
            Plate(b, 1.85f, 0.315f, 0.425f, 1);
            b.Box(new V3(1.77f, 0.60f, -0.30f), new V3(1.792f, 0.715f, 0.30f), 0.006f, DetailPart, VoxelPalette.Ink);
            for (int k = 0; k < 2; k++)
            {
                float y = 0.63f + k * 0.045f;
                b.Box(new V3(1.786f, y, -0.28f), new V3(1.797f, y + 0.014f, 0.28f), 0.004f, DetailPart, VoxelPalette.Metal);
            }
            b.Box(new V3(1.794f, 0.636f, -0.03f), new V3(1.804f, 0.69f, 0.03f), 0.005f, DetailPart, VoxelPalette.Chrome);
            Wipers(b, p, 0.54f);
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, 1.50f, 1.765f, 0.30f, 0.445f, 0.745f, 0.823f, 0.025f, DetailPart, VoxelPalette.MetalDark);
                SideBox(b, side, 1.835f, 1.851f, 0.35f, 0.41f, 0.56f, 0.70f, 0.005f, DetailPart, VoxelPalette.Amber);
                SideBox(b, side, 1.77f, 1.792f, 0.585f, 0.735f, 0.31f, 0.735f, 0.008f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, 1.786f, 1.80f, 0.597f, 0.723f, 0.322f, 0.723f, 0.005f, LampPart, VoxelPalette.Headlight);
            }
            // Tail: bumper, the plate between the lamps with a badge over it, a wiper at the foot of the hatch glass.
            b.Box(new V3(-1.845f, 0.29f, -0.765f), new V3(-1.745f, 0.455f, 0.765f), 0.03f, DetailPart, VoxelPalette.MetalDark);
            Plate(b, -1.791f, 0.575f, 0.685f, -1);
            b.Box(new V3(-1.79f, 0.71f, -0.03f), new V3(-1.776f, 0.74f, 0.03f), 0.004f, DetailPart, VoxelPalette.Chrome);
            b.Box(new V3(-1.632f, 0.952f, -0.02f), new V3(-1.612f, 0.982f, 0.36f), 0.005f, DetailPart, VoxelPalette.Ink);
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, -1.792f, -1.77f, 0.52f, 0.84f, 0.49f, 0.745f, 0.008f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, -1.802f, -1.786f, 0.68f, 0.828f, 0.505f, 0.73f, 0.004f, BrakePart, VoxelPalette.TailLight);
                SideBox(b, side, -1.802f, -1.786f, 0.60f, 0.672f, 0.505f, 0.73f, 0.004f, DetailPart, VoxelPalette.Amber);
                SideBox(b, side, -1.802f, -1.786f, 0.532f, 0.592f, 0.505f, 0.73f, 0.004f, DetailPart, VoxelPalette.Cream);
            }
            Mirrors(b, p, p.CowlX - 0.165f, p.BeltY - 0.005f, 0.10f, 0.09f);
            Handles(b, p, 0.835f, -0.10f, -0.90f);
        }

        // ---------- Pickup: heavy bumpers, a tall grille between stacked lamps, black flares, an open bed ----------
        private static void PickupDress(PolyBuilder b, BodyPlan p)
        {
            b.Box(new V3(2.345f, 0.37f, -0.84f), new V3(2.445f, 0.60f, 0.84f), 0.035f, DetailPart, VoxelPalette.MetalDark);
            b.Box(new V3(2.38f, 0.578f, -0.81f), new V3(2.45f, 0.598f, 0.81f), 0.008f, DetailPart, VoxelPalette.Metal);
            Plate(b, 2.45f, 0.42f, 0.53f, 1);
            b.Box(new V3(2.365f, 0.64f, -0.48f), new V3(2.392f, 0.98f, 0.48f), 0.006f, DetailPart, VoxelPalette.Ink);
            for (int k = 0; k < 5; k++)
            {
                float y = 0.675f + k * 0.058f;
                b.Box(new V3(2.386f, y, -0.46f), new V3(2.398f, y + 0.022f, 0.46f), 0.004f, DetailPart, VoxelPalette.MetalDark);
            }
            foreach (float y in new[] { 0.628f, 0.972f })
                b.Box(new V3(2.384f, y, -0.49f), new V3(2.40f, y + 0.018f, 0.49f), 0.005f, DetailPart, VoxelPalette.Metal);
            b.Box(new V3(2.396f, 0.79f, -0.05f), new V3(2.406f, 0.84f, 0.05f), 0.005f, DetailPart, VoxelPalette.Chrome);
            Wipers(b, p, 0.62f);
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, 2.03f, 2.36f, 0.38f, 0.59f, 0.82f, 0.905f, 0.03f, DetailPart, VoxelPalette.MetalDark);
                SideBox(b, side, 2.435f, 2.451f, 0.44f, 0.52f, 0.62f, 0.80f, 0.005f, DetailPart, VoxelPalette.Amber);
                SideBox(b, side, 2.365f, 2.392f, 0.64f, 0.98f, 0.495f, 0.80f, 0.008f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, 2.386f, 2.40f, 0.825f, 0.965f, 0.515f, 0.78f, 0.005f, LampPart, VoxelPalette.Headlight);
                SideBox(b, side, 2.386f, 2.40f, 0.655f, 0.795f, 0.515f, 0.78f, 0.005f, LampPart, VoxelPalette.Headlight);
                foreach (float axle in new[] { p.Shape.FrontAxleX, p.Shape.RearAxleX })
                    Flare(b, side, axle, p.SillY, p.ArchRadius + 0.004f, p.ArchRadius + 0.075f, 0.855f, 0.915f, 9, VoxelPalette.Tire);
            }
            // The bed: walls from the tail to the cab with dark rail caps, the tailgate between them, a rubber floor
            // (the shell's deck); upright tail lamps on the ends of the walls; a window in the back of the cab.
            const float floor = PickupBedFloor, rail = PickupBedRail, wallIn = 0.765f, wallOut = 0.875f;
            float tail = p.RearX;
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, tail, PickupCabBack + 0.01f, floor - 0.03f, rail, wallIn, wallOut, 0.012f, PaintPart, 0);
                SideBox(b, side, tail - 0.004f, PickupCabBack + 0.012f, rail - 0.004f, rail + 0.016f, wallIn - 0.01f, wallOut + 0.008f, 0.006f, DetailPart, VoxelPalette.MetalDark);
                SideBox(b, side, tail - 0.012f, tail + 0.004f, 0.86f, 1.14f, 0.772f, 0.87f, 0.006f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, tail - 0.02f, tail - 0.006f, 0.985f, 1.128f, 0.782f, 0.86f, 0.004f, BrakePart, VoxelPalette.TailLight);
                SideBox(b, side, tail - 0.02f, tail - 0.006f, 0.925f, 0.977f, 0.782f, 0.86f, 0.004f, DetailPart, VoxelPalette.Amber);
                SideBox(b, side, tail - 0.02f, tail - 0.006f, 0.872f, 0.917f, 0.782f, 0.86f, 0.004f, DetailPart, VoxelPalette.Cream);
            }
            b.Box(new V3(tail - 0.004f, floor - 0.05f, -wallIn), new V3(tail + 0.045f, rail - 0.01f, wallIn), 0.012f, PaintPart, 0);
            b.Box(new V3(tail - 0.012f, rail - 0.11f, -0.11f), new V3(tail - 0.002f, rail - 0.075f, 0.11f), 0.005f, DetailPart, VoxelPalette.Ink);
            b.Box(new V3(-2.445f, 0.38f, -0.84f), new V3(-2.345f, 0.58f, 0.84f), 0.03f, DetailPart, VoxelPalette.MetalDark);
            b.Box(new V3(-2.45f, 0.56f, -0.80f), new V3(-2.36f, 0.585f, 0.80f), 0.008f, DetailPart, VoxelPalette.Metal);
            Plate(b, -2.451f, 0.41f, 0.52f, -1);
            b.Box(new V3(PickupCabBack - 0.008f, 1.22f, -0.56f), new V3(PickupCabBack + 0.025f, 1.60f, 0.56f), 0.012f, GlassPart, 0);
            Mirrors(b, p, p.CowlX - 0.17f, p.BeltY - 0.005f, 0.15f, 0.10f);
            Handles(b, p, 1.03f, -0.30f);
        }

        // ---------- Van: big bumpers and lamps, a black grille, two rear doors with windows, tall tail lamps ----------
        private static void VanDress(PolyBuilder b, BodyPlan p)
        {
            b.Box(new V3(2.295f, 0.31f, -0.89f), new V3(2.395f, 0.54f, 0.89f), 0.035f, DetailPart, VoxelPalette.MetalDark);
            Plate(b, 2.40f, 0.37f, 0.48f, 1);
            b.Box(new V3(2.315f, 0.60f, -0.46f), new V3(2.342f, 0.93f, 0.46f), 0.006f, DetailPart, VoxelPalette.Ink);
            for (int k = 0; k < 5; k++)
            {
                float y = 0.632f + k * 0.062f;
                b.Box(new V3(2.336f, y, -0.44f), new V3(2.348f, y + 0.022f, 0.44f), 0.004f, DetailPart, VoxelPalette.MetalDark);
            }
            b.Box(new V3(2.344f, 0.75f, -0.05f), new V3(2.354f, 0.80f, 0.05f), 0.005f, DetailPart, VoxelPalette.Chrome);
            Wipers(b, p, 0.75f);
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, 1.99f, 2.30f, 0.32f, 0.53f, 0.875f, 0.95f, 0.03f, DetailPart, VoxelPalette.MetalDark);
                SideBox(b, side, 2.315f, 2.342f, 0.60f, 0.93f, 0.47f, 0.85f, 0.008f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, 2.336f, 2.35f, 0.75f, 0.915f, 0.485f, 0.835f, 0.005f, LampPart, VoxelPalette.Headlight);
                SideBox(b, side, 2.336f, 2.35f, 0.62f, 0.735f, 0.485f, 0.835f, 0.005f, DetailPart, VoxelPalette.Amber);
            }
            // Rear doors: the seam between them, their windows and a handle, the plate low on the left one; tall tail
            // lamps on the corners and a third brake lamp over the doors.
            b.Box(new V3(-2.395f, 0.31f, -0.89f), new V3(-2.295f, 0.52f, 0.89f), 0.03f, DetailPart, VoxelPalette.MetalDark);
            b.Box(new V3(-2.338f, 0.33f, -0.006f), new V3(-2.322f, 1.965f, 0.006f), 0, DetailPart, VoxelPalette.Ink);
            b.Box(new V3(-2.348f, 1.06f, -0.20f), new V3(-2.328f, 1.09f, -0.05f), 0.005f, DetailPart, VoxelPalette.Chrome);
            Plate(b, -2.341f, 0.60f, 0.71f, -1);
            b.Box(new V3(-2.345f, 1.935f, -0.15f), new V3(-2.326f, 1.97f, 0.15f), 0.006f, BrakePart, VoxelPalette.TailLight);
            for (int side = 1; side >= -1; side -= 2)
            {
                SideBox(b, side, -2.342f, -2.322f, 1.34f, 1.86f, 0.06f, 0.70f, 0.01f, GlassPart, 0);
                SideBox(b, side, -2.342f, -2.322f, 0.60f, 1.12f, 0.74f, 0.862f, 0.006f, DetailPart, VoxelPalette.Ink);
                SideBox(b, side, -2.35f, -2.336f, 0.86f, 1.105f, 0.752f, 0.85f, 0.004f, BrakePart, VoxelPalette.TailLight);
                SideBox(b, side, -2.35f, -2.336f, 0.74f, 0.85f, 0.752f, 0.85f, 0.004f, DetailPart, VoxelPalette.Amber);
                SideBox(b, side, -2.35f, -2.336f, 0.615f, 0.73f, 0.752f, 0.85f, 0.004f, DetailPart, VoxelPalette.Cream);
            }
            Mirrors(b, p, p.CowlX - 0.20f, p.BeltY - 0.005f, 0.22f, 0.11f);
            Handles(b, p, 1.06f, 0.36f, 0.03f);
        }
    }
}
