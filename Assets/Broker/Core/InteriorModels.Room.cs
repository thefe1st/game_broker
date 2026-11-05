using System;

namespace Broker.Core
{
    // Fittings of the office room: the coffee cabinet, the potted plant, the pedestal of the golden telephone, the exit door,
    // the window frames of the back wall, the ceiling lights, the company plaque, the street elevator and the skirting.
    // Wall pieces stand against the wall at z = 0 and come forward towards −Z; the scene turns them to face the room.
    public static partial class InteriorModels
    {
        public const float CabinetTop = 1.0f, PotTop = 0.6f, PotRadius = 0.29f, PedestalTop = 1.1f, PedestalHalf = 0.425f;
        public const float DoorHalfWidth = 0.825f, DoorHeight = 2.6f, DoorLeafHalf = 0.7f, DoorLeafTop = 2.45f, DoorDepth = 0.26f, DoorSignZ = -0.33f,
            DoorHandleX = 0.48f, DoorHandleY = 1.1f;
        public const float WindowHalf = 1.455f, WindowInnerHalf = 1.385f, WindowFrameHeight = 2.03f, WindowRail = 0.07f, WindowSillFront = -0.445f;
        public const float CeilingLightDrop = 0.1f, PlaqueFront = -0.062f;
        public const float ElevatorHalf = 0.65f, ElevatorJamb = 0.76f, ElevatorHeight = 2.75f, ElevatorDepth = 0.23f, ElevatorLabelZ = -0.28f;
        public const float SkirtingHeight = 0.1f, SkirtingDepth = 0.018f;

        // Cabinet of the coffee corner, 1.1 × 1.0 × 0.8 m: a dark carcass on a plinth, two panelled doors with brass knobs
        // towards −Z and a dark top CabinetTop high for the machine and the cup.
        public static PolyMesh CoffeeCabinet()
        {
            var b = NewBuilder();
            b.Box(new V3(-0.52f, 0, -0.33f), new V3(0.52f, 0.07f, 0.37f), 0, TrimPart, VoxelPalette.Ink);
            b.Box(new V3(-0.55f, 0.07f, -0.36f), new V3(0.55f, 0.96f, 0.4f), 0.01f, TrimPart, VoxelPalette.WoodDark);
            b.Box(new V3(-0.55f, 0.96f, -0.4f), new V3(0.55f, CabinetTop, 0.4f), 0.012f, TrimPart, VoxelPalette.WoodDark, 0.94f);
            foreach (float s in new[] { -1f, 1f })
            {
                float x0 = s < 0 ? -0.53f : 0.006f, x1 = s < 0 ? -0.006f : 0.53f;
                b.Box(new V3(x0, 0.1f, -0.377f), new V3(x1, 0.93f, -0.36f), 0.006f, TrimPart, VoxelPalette.Wood);
                b.Box(new V3(x0 + 0.065f, 0.17f, -0.384f), new V3(x1 - 0.065f, 0.86f, -0.377f), 0.005f, TrimPart, VoxelPalette.Wood, 1.05f);
                PolyShapes.Limb(b, new V3(s * 0.04f, 0.78f, -0.377f), new V3(s * 0.04f, 0.78f, -0.397f), 0.007f, 0.013f, 8, MetalPart, VoxelPalette.Brass);
            }
            return b.Build(BuildingModels.PaletteUv);
        }

        // Potted plant 0.9 × 1.65 m: a terracotta pot with a rolled rim and soil, a trunk with two branches and five clumps
        // of leaves on the "leaves" wall material, darker underneath. The seed moves the clumps and their bumps.
        public static PolyMesh PottedPlant(int seed)
        {
            var b = NewBuilder();
            PolyShapes.Lathe(b, 0, 0, new[] { (0f, 0.19f), (0.03f, 0.205f), (0.46f, 0.255f), (0.48f, 0.272f), (0.5f, PotRadius), (PotTop, PotRadius), (PotTop, 0.262f), (0.56f, 0.255f) },
                14, TrimPart, VoxelPalette.BrickRed, capBottom: true);
            PolyShapes.Disc(b, 0, 0.56f, 0, 0.255f, 14, true, TrimPart, VoxelPalette.Soil);
            PolyShapes.Limb(b, new V3(0, 0.55f, 0), new V3(0.02f, 1.12f, -0.01f), 0.032f, 0.02f, 6, TrimPart, VoxelPalette.Bark);
            PolyShapes.Limb(b, new V3(0.015f, 0.9f, 0), new V3(0.14f, 1.12f, 0.05f), 0.017f, 0.01f, 6, TrimPart, VoxelPalette.Bark);
            PolyShapes.Limb(b, new V3(0.01f, 0.82f, 0), new V3(-0.13f, 1.06f, -0.07f), 0.017f, 0.01f, 6, TrimPart, VoxelPalette.Bark);
            b.Ambient = p => 0.7f + 0.3f * SmoothStep(0.85f, 1.6f, p.Y);
            var clumps = new[] { (0.02f, 1.38f, 0.0f, 0.22f), (0.16f, 1.14f, 0.06f, 0.19f), (-0.15f, 1.1f, -0.08f, 0.19f), (-0.02f, 1.04f, 0.16f, 0.17f), (0.06f, 1.0f, -0.15f, 0.16f) };
            for (int k = 0; k < clumps.Length; k++)
            {
                var (x, y, z, r) = clumps[k];
                float jx = (Rand(seed, k) - 0.5f) * 0.04f, jz = (Rand(seed, k + 7) - 0.5f) * 0.04f;
                PolyShapes.Lump(b, new V3(x + jx, y, z + jz), r, r * 0.92f, r, 8, 6, 0.12f, seed * 13 + k, WallPart, 0);
            }
            return b.Build(BuildingModels.PaletteUv);
        }

        // Pedestal of the golden telephone, 0.85 m square and PedestalTop high: navy with brass bands over the plinth and
        // under the cap and raised fields on the front and the sides (the price label hangs on the front).
        public static PolyMesh DisplayPedestal()
        {
            var b = NewBuilder();
            byte navy = VoxelPalette.Navy;
            const float h = PedestalHalf;
            b.Box(new V3(-h, 0, -h), new V3(h, 0.08f, h), 0.012f, TrimPart, navy, 0.85f);
            b.Box(new V3(-0.41f, 0.08f, -0.41f), new V3(0.41f, 0.1f, 0.41f), 0, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-0.4f, 0.1f, -0.4f), new V3(0.4f, 1.02f, 0.4f), 0.008f, TrimPart, navy);
            b.Box(new V3(-0.41f, 1.02f, -0.41f), new V3(0.41f, 1.04f, 0.41f), 0, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-h, 1.04f, -h), new V3(h, PedestalTop, h), 0.012f, TrimPart, navy);
            b.Box(new V3(-0.32f, 0.2f, -0.406f), new V3(0.32f, 0.92f, -0.4f), 0.004f, TrimPart, navy, 1.06f);
            foreach (float s in new[] { -1f, 1f })
                b.Box(new V3(s < 0 ? -0.406f : 0.4f, 0.2f, -0.32f), new V3(s < 0 ? -0.4f : 0.406f, 0.92f, 0.32f), 0.004f, TrimPart, navy, 1.06f);
            return b.Build(BuildingModels.PaletteUv);
        }

        // The exit door, 1.65 × 2.6 m: a brass casing DoorDepth deep round a navy leaf with a wide upper panel (the sign hangs
        // on it) and two lower ones, a brass kick plate, a lever handle on a backplate at DoorHandle and a keyhole. Nothing
        // comes forward of DoorSignZ.
        public static PolyMesh Door()
        {
            var b = NewBuilder();
            const float leafFront = -0.24f, panel = -0.25f;
            foreach (float s in new[] { -1f, 1f })
                b.Box(new V3(s < 0 ? -DoorHalfWidth : DoorLeafHalf, 0, -DoorDepth), new V3(s < 0 ? -DoorLeafHalf : DoorHalfWidth, DoorHeight, 0), 0.015f, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-DoorHalfWidth, DoorLeafTop, -DoorDepth), new V3(DoorHalfWidth, DoorHeight, 0), 0.015f, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-DoorLeafHalf, 0.005f, leafFront), new V3(DoorLeafHalf, DoorLeafTop, -0.2f), 0.008f, TrimPart, VoxelPalette.Navy);
            b.Box(new V3(-0.45f, 1.62f, panel), new V3(0.45f, 2.28f, leafFront), 0.006f, TrimPart, VoxelPalette.Navy, 1.08f);
            foreach (float s in new[] { -1f, 1f })
                b.Box(new V3(s < 0 ? -0.45f : 0.05f, 0.32f, panel), new V3(s < 0 ? -0.05f : 0.45f, 1.45f, leafFront), 0.006f, TrimPart, VoxelPalette.Navy, 1.08f);
            b.Box(new V3(-0.64f, 0.03f, leafFront - 0.003f), new V3(0.64f, 0.22f, leafFront), 0, MetalPart, VoxelPalette.Brass);
            // Handle: backplate, neck and a lever towards the middle of the door; the keyhole under it.
            b.Box(new V3(DoorHandleX - 0.025f, 0.97f, panel), new V3(DoorHandleX + 0.025f, 1.25f, leafFront), 0.004f, MetalPart, VoxelPalette.Brass);
            PolyShapes.Limb(b, new V3(DoorHandleX, DoorHandleY, panel), new V3(DoorHandleX, DoorHandleY, -0.297f), 0.013f, 0.013f, 8, MetalPart, VoxelPalette.Brass, caps: false);
            PolyShapes.Limb(b, new V3(DoorHandleX + 0.012f, DoorHandleY, -0.297f), new V3(DoorHandleX - 0.125f, DoorHandleY - 0.006f, -0.309f), 0.012f, 0.01f, 8, MetalPart, VoxelPalette.Brass);
            FrontDisc(b, DoorHandleX, 1.01f, panel - 0.0005f, 0.007f, 6, TrimPart, VoxelPalette.Ink);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Window frame of the office's back wall round its opening, 2.91 × 2.03 m: a navy frame 0.15 deep, a cream mullion
        // on the room side and a cream sill with a rounded nose. Pivot at the middle of the frame's foot.
        public static PolyMesh WindowFrame()
        {
            var b = NewBuilder(0);
            Frame(b, -WindowHalf, 0, WindowHalf, WindowFrameHeight, -WindowInnerHalf, WindowRail, WindowInnerHalf, WindowFrameHeight - WindowRail, -0.075f, 0.075f, 0.012f,
                TrimPart, VoxelPalette.Navy, back: true);
            b.Box(new V3(-0.025f, WindowRail, -0.08f), new V3(0.025f, WindowFrameHeight - WindowRail, -0.02f), 0.008f, TrimPart, VoxelPalette.Cream);
            b.Box(new V3(-1.5f, 0.085f, WindowSillFront), new V3(1.5f, 0.205f, -0.075f), 0.02f, TrimPart, VoxelPalette.Cream);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Ceiling light 1.8 × 0.8 m: a white housing, a glowing diffuser underneath crossed by three bars. Pivot at the top
        // (the ceiling); it hangs CeilingLightDrop.
        public static PolyMesh CeilingLight()
        {
            var b = NewBuilder(0);
            b.Box(new V3(-0.9f, -0.08f, -0.4f), new V3(0.9f, 0, 0.4f), 0.012f, TrimPart, VoxelPalette.CarWhite);
            const float glow = -0.082f;
            b.Quad(new V3(-0.85f, glow, -0.35f), new V3(0.85f, glow, -0.35f), new V3(0.85f, glow, 0.35f), new V3(-0.85f, glow, 0.35f), new V3(0, -1, 0), GlowPart, VoxelPalette.Headlight);
            foreach (float x in new[] { -0.425f, 0, 0.425f })
                b.Box(new V3(x - 0.012f, -CeilingLightDrop, -0.36f), new V3(x + 0.012f, -0.08f, 0.36f), 0, TrimPart, VoxelPalette.CarWhite);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Company plaque `width` × `height`, 0.1 m deep: a navy board with a thin brass frame on its face, which comes to
        // PlaqueFront (the name hangs just in front). Pivot at its centre.
        public static PolyMesh Plaque(float width, float height)
        {
            if (!(width >= 0.3f) || width > 12 || !(height >= 0.15f) || height > 2) throw new ArgumentOutOfRangeException(nameof(width));
            var b = NewBuilder(0);
            float hw = width / 2, hh = height / 2;
            b.Box(new V3(-hw, -hh, -0.05f), new V3(hw, hh, 0.05f), 0.01f, TrimPart, VoxelPalette.Navy);
            Frame(b, -hw + 0.02f, -hh + 0.02f, hw - 0.02f, hh - 0.02f, -hw + 0.045f, -hh + 0.045f, hw - 0.045f, hh - 0.045f, PlaqueFront, -0.05f, 0.004f,
                MetalPart, VoxelPalette.Brass);
            return b.Build(BuildingModels.PaletteUv);
        }

        // The street elevator, 1.52 × 2.75 m: brass jambs and head ElevatorDepth deep, two navy leaves meeting at a chrome
        // seam, a dark transom with the glowing indicator bar and a threshold; a brass call plate with a lit and an unlit
        // button on the wall to the right. The label hangs at ElevatorLabelZ.
        public static PolyMesh ElevatorDoor()
        {
            var b = NewBuilder();
            foreach (float s in new[] { -1f, 1f })
                b.Box(new V3(s < 0 ? -ElevatorJamb : ElevatorHalf, 0, -ElevatorDepth), new V3(s < 0 ? -ElevatorHalf : ElevatorJamb, ElevatorHeight, 0), 0.012f, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-ElevatorJamb, 2.6f, -ElevatorDepth), new V3(ElevatorJamb, ElevatorHeight, 0), 0.012f, MetalPart, VoxelPalette.Brass);
            b.Box(new V3(-ElevatorHalf, 2.42f, -0.2f), new V3(ElevatorHalf, 2.6f, -0.1f), 0, TrimPart, VoxelPalette.Ink);
            b.Box(new V3(-0.575f, 2.47f, -0.245f), new V3(0.575f, 2.53f, -0.2f), 0.006f, GlowPart, VoxelPalette.WindowLit);
            foreach (float s in new[] { -1f, 1f })
                b.Box(new V3(s < 0 ? -ElevatorHalf : 0.005f, 0.01f, -0.2f), new V3(s < 0 ? -0.005f : ElevatorHalf, 2.42f, -0.17f), 0.005f, TrimPart, VoxelPalette.Navy);
            b.Box(new V3(-0.005f, 0.01f, -0.192f), new V3(0.005f, 2.42f, -0.17f), 0, MetalPart, VoxelPalette.Chrome);
            b.Box(new V3(-ElevatorHalf, 0, -ElevatorDepth), new V3(ElevatorHalf, 0.01f, 0), 0, TrimPart, VoxelPalette.MetalLight);
            b.Box(new V3(0.8f, 1.0f, -0.012f), new V3(0.88f, 1.28f, 0), 0.004f, MetalPart, VoxelPalette.Brass);
            FrontDisc(b, 0.84f, 1.185f, -0.0125f, 0.014f, 8, GlowPart, VoxelPalette.SignalAmber);
            FrontDisc(b, 0.84f, 1.095f, -0.0125f, 0.014f, 8, TrimPart, VoxelPalette.Cream);
            return b.Build(BuildingModels.PaletteUv);
        }

        // Skirting board `length` long, SkirtingHeight high and SkirtingDepth out from the wall, in dark wood.
        public static PolyMesh Skirting(float length)
        {
            if (!(length >= 0.2f) || length > 40) throw new ArgumentOutOfRangeException(nameof(length));
            var b = NewBuilder(0.2f);
            b.Box(new V3(-length / 2, 0, -SkirtingDepth), new V3(length / 2, SkirtingHeight, 0), 0.006f, TrimPart, VoxelPalette.WoodDark);
            return b.Build(BuildingModels.PaletteUv);
        }
    }
}
