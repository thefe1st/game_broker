namespace Broker.Core
{
    // The plaza's information stands in the street furniture's trim, in place of the voxel boards (VoxelProps.InfoStand)
    // with their pivot and bounds: a navy panel set back in a dark wooden frame on two posts with stone feet, a brass strip
    // under a small roof. The lower band of the panel hints at the content — the district plan with the glowing «you are
    // here» marker, or a share chart — and the middle band stays plain for the world label. Front faces −Z.
    public static partial class StreetModels
    {
        public const float StandHalfWidth = 1.1f, StandHeight = 2.5f, StandHalfDepth = 0.3f;
        // The panel's face 5 cm behind the front of the frame, its bands from the foot up: the content, the plain band the
        // label is written on and the brass strip. BuildCityStand hangs the label at (0, StandLabelY, StandLabelZ), in front
        // of the frame.
        public const float StandFace = -0.05f, StandFrameFront = -0.1f, StandPanelBottom = 0.8f, StandPanelTop = 2.2f;
        public const float StandContentBottom = 0.9f, StandContentTop = 1.4f, StandLabelBottom = 1.45f, StandLabelTop = 1.95f, StandBrassBottom = 2.03f;
        public const float StandLabelY = 1.65f, StandLabelZ = -0.15f;
        // «You are here» on the map: the square of the plaza in the middle of the upper row of blocks.
        public const float StandMarkerX = -0.12f, StandMarkerY = 1.28f, StandMarkerRadius = 0.03f;
        private const float StandPanelHalf = 1.0f, StandBack = 0.06f, StandPostX = 0.8f, StandRoofY = 2.3f;
        // The share chart: bar heights over its base line; the third and the fifth fall (red), the rest rise (green).
        private static readonly float[] ChartBars = { 0.17f, 0.25f, 0.17f, 0.33f, 0.25f, 0.41f, 0.33f, 0.45f };

        // Information stand 2.2 × 2.5 m: tapered stone feet, dark posts behind the board up to the roof, the frame's battens
        // with rounded edges round the panel, the content of the style at its foot. Pivot at the middle of the bottom.
        public static PolyMesh InfoStand(StandStyle style)
        {
            var b = NewBuilder();
            foreach (float x in new[] { -StandPostX, StandPostX })
            {
                PolyShapes.Block(b, Footprint(x, 0.08f, 0, 0.18f, 0.16f), Footprint(x, 0.08f, 0.1f, 0.13f, 0.11f), TrimPart, VoxelPalette.StoneDark);
                b.Box(new V3(x - 0.045f, 0.1f, StandBack), new V3(x + 0.045f, StandRoofY, StandBack + 0.09f), 0.012f, TrimPart, VoxelPalette.MetalDark);
            }
            const float wood = 0.1f, bevel = 0.015f, hw = StandHalfWidth, ph = StandPanelHalf;
            const float bottom = StandPanelBottom - wood, top = StandPanelTop + wood;
            byte frame = VoxelPalette.WoodDark;
            b.Box(new V3(-hw, bottom, StandFrameFront), new V3(-ph, top, StandBack), bevel, TrimPart, frame);
            b.Box(new V3(ph, bottom, StandFrameFront), new V3(hw, top, StandBack), bevel, TrimPart, frame);
            b.Box(new V3(-ph, bottom, StandFrameFront), new V3(ph, StandPanelBottom, StandBack), bevel, TrimPart, frame);
            b.Box(new V3(-ph, StandPanelTop, StandFrameFront), new V3(ph, top, StandBack), bevel, TrimPart, frame);
            // The panel shows only its face and its back between the battens.
            b.Quad(new V3(-ph, StandPanelBottom, StandFace), new V3(ph, StandPanelBottom, StandFace), new V3(ph, StandPanelTop, StandFace),
                new V3(-ph, StandPanelTop, StandFace), new V3(0, 0, -1), TrimPart, VoxelPalette.Navy);
            b.Quad(new V3(-ph, StandPanelBottom, StandBack), new V3(ph, StandPanelBottom, StandBack), new V3(ph, StandPanelTop, StandBack),
                new V3(-ph, StandPanelTop, StandBack), new V3(0, 0, 1), TrimPart, VoxelPalette.Navy, 0.9f);
            b.Box(new V3(-ph, StandBrassBottom, StandFace - 0.01f), new V3(ph, StandPanelTop, StandFace), 0.003f, TrimPart, VoxelPalette.Brass);
            if (style == StandStyle.Map) MapPlate(b); else ChartPlate(b);
            // The roof: an overhanging slab with a low ridge.
            b.Box(new V3(-hw, StandRoofY, -0.22f), new V3(hw, 2.37f, 0.22f), bevel, TrimPart, VoxelPalette.RoofDark);
            PolyShapes.Block(b, Footprint(0, 0, 2.37f, 1.06f, 0.19f), Footprint(0, 0, 2.48f, 1.02f, 0.02f), TrimPart, VoxelPalette.RoofDark);
            return b.Build(BuildingModels.PaletteUv);
        }

        // The district plan over the content band: pale roads round two rows of blocks — pairs of houses, the green square of
        // the plaza with the marker over the middle of the upper row, the water in the lower right corner.
        private static void MapPlate(PolyBuilder b)
        {
            float plate = StandFace - 0.006f, blocks = StandFace - 0.012f;
            b.Box(new V3(-0.9f, StandContentBottom, plate), new V3(0.9f, StandContentTop, StandFace), 0, TrimPart, VoxelPalette.StoneLight);
            float[] xs = { -0.88f, -0.36f, -0.28f, 0.24f, 0.32f, 0.88f }, ys = { 0.92f, 1.12f, 1.18f, 1.38f };
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 3; col++)
                {
                    float x0 = xs[col * 2], x1 = xs[col * 2 + 1], y0 = ys[row * 2], y1 = ys[row * 2 + 1];
                    if (row == 1 && col == 1) Plate(b, x0, y0, x1, y1, blocks, VoxelPalette.GrassMid);
                    else if (row == 0 && col == 2) Plate(b, x0, y0, x1, y1, blocks, VoxelPalette.GlassSky);
                    else
                    {
                        // Two houses with a lane between them.
                        float mid = (x0 + x1) / 2 + ((row + col) % 2 == 0 ? 0.06f : -0.06f);
                        Plate(b, x0, y0, mid - 0.015f, y1, blocks, VoxelPalette.Stone);
                        Plate(b, mid + 0.015f, y0, x1, y1, blocks, VoxelPalette.Stone, 0.92f);
                    }
                }
            // «You are here»: a glowing red dot in a white ring.
            FrontDisc(b, StandMarkerX, StandMarkerY, blocks - 0.004f, StandMarkerRadius + 0.015f, 12, 0, TrimPart, VoxelPalette.CarWhite);
            FrontDisc(b, StandMarkerX, StandMarkerY, blocks - 0.008f, StandMarkerRadius, 12, 0.008f, GlowPart, VoxelPalette.SignalRed);
        }
        // A share chart over the content band: a pale axis and base line, green bars that rise and two red ones that fall.
        private static void ChartPlate(PolyBuilder b)
        {
            float z = StandFace - 0.008f, line = StandContentBottom + 0.03f;
            b.Box(new V3(-0.9f, StandContentBottom, z), new V3(0.9f, line, StandFace), 0, TrimPart, VoxelPalette.StoneLight);
            b.Box(new V3(-0.9f, line, z), new V3(-0.88f, StandContentTop, StandFace), 0, TrimPart, VoxelPalette.StoneLight);
            for (int i = 0; i < ChartBars.Length; i++)
            {
                float x0 = -0.83f + 0.22f * i;
                b.Box(new V3(x0, line, z - 0.004f), new V3(x0 + 0.12f, line + ChartBars[i], StandFace), 0, TrimPart,
                    i == 2 || i == 4 ? VoxelPalette.CarRed : VoxelPalette.GrassLight);
            }
        }
        // Flat rectangle facing −Z on the plane z.
        private static void Plate(PolyBuilder b, float x0, float y0, float x1, float y1, float z, byte color, float shade = 1)
            => b.Quad(new V3(x0, y0, z), new V3(x1, y0, z), new V3(x1, y1, z), new V3(x0, y1, z), new V3(0, 0, -1), TrimPart, color, shade);
    }
}
