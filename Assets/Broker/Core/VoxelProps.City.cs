using System;

namespace Broker.Core
{
    // Passers-by in five kinds of parts: the legs swing from the hips, the arms from the shoulders, the head turns on
    // the neck. Pivots sit at the joints; offsets are in metres from the feet. Faces look −Z. One arm and one leg model
    // serve both sides.
    public sealed class VoxelWalker
    {
        public VoxelModel Torso { get; } public VoxelModel Head { get; } public VoxelModel Arm { get; } public VoxelModel Leg { get; }
        // Bent legs of a seated figure: thighs forward (−Z), shins down, pivot at the hips.
        public VoxelModel SeatedLegs { get; }
        public float HipX { get; } public float HipY { get; } public float SeatedHipY { get; } public float ShoulderX { get; }
        public float NeckY(bool seated) => (seated ? SeatedHipY : HipY) + Torso.Height;
        internal VoxelWalker(VoxelModel torso, VoxelModel head, VoxelModel arm, VoxelModel leg, VoxelModel seated)
        {
            Torso = torso; Head = head; Arm = arm; Leg = leg; SeatedLegs = seated;
            HipX = 2.5f * VoxelProps.PersonVoxel; ShoulderX = 5 * VoxelProps.PersonVoxel;
            HipY = leg.Height; SeatedHipY = seated.Height;
        }
    }

    // City quarter props: passers-by, the Exchange (podium, hall, colonnade, roof, quote board), the square, the café
    // «Бык и медведь», the crossing signals and the boulevard tunnels. Same rules as VoxelProps: no trigonometry,
    // fronts face −Z unless noted, pivots at the bottom centre unless noted.
    public static partial class VoxelProps
    {
        public const float ExchangeVoxel = 0.1f;

        // ---------- Passers-by: 1.94 m tall like the office staff ----------
        public static VoxelWalker Walker(WalkerStyle style)
        {
            byte top, sleeve, cuff, trousers, shoes = VoxelPalette.Ink, skin = VoxelPalette.Skin, hair = VoxelPalette.HairDark;
            int bareArm = 2, bareLeg = 0, coat = 0;
            switch (style)
            {
                case WalkerStyle.Clerk: top = sleeve = cuff = VoxelPalette.Cream; trousers = VoxelPalette.MetalDark; hair = VoxelPalette.HairBrown; break;
                case WalkerStyle.Banker: top = sleeve = trousers = VoxelPalette.Navy; cuff = VoxelPalette.Cream; break;
                case WalkerStyle.Casual: top = sleeve = cuff = VoxelPalette.CarBlue; trousers = VoxelPalette.GlassDark; shoes = VoxelPalette.CarWhite; skin = VoxelPalette.SkinShade; break;
                case WalkerStyle.Coat: top = sleeve = VoxelPalette.Wood; cuff = VoxelPalette.WoodDark; trousers = VoxelPalette.Ink; hair = VoxelPalette.HairBrown; coat = 5; break;
                case WalkerStyle.Courier: top = sleeve = VoxelPalette.Amber; cuff = VoxelPalette.Ink; trousers = VoxelPalette.Navy; shoes = VoxelPalette.CarBlack; break;
                case WalkerStyle.Student: top = sleeve = VoxelPalette.CarGreen; cuff = VoxelPalette.PineMid; trousers = VoxelPalette.GlassDark; shoes = VoxelPalette.CarRed; hair = VoxelPalette.HairBrown; break;
                case WalkerStyle.Elder: top = sleeve = cuff = VoxelPalette.WoodLight; trousers = VoxelPalette.Stone; shoes = VoxelPalette.WoodDark; hair = VoxelPalette.StoneLight; break;
                case WalkerStyle.Tourist: top = sleeve = cuff = VoxelPalette.Awning; trousers = VoxelPalette.PlasterWarm; shoes = VoxelPalette.CarWhite; bareArm = 7; bareLeg = 5; break;
                case WalkerStyle.Analyst: top = sleeve = cuff = VoxelPalette.GlassSky; trousers = VoxelPalette.StoneDark; shoes = VoxelPalette.WoodDark; bareArm = 4; break;
                case WalkerStyle.Barista: top = sleeve = cuff = VoxelPalette.Cream; trousers = VoxelPalette.Ink; hair = VoxelPalette.HairBrown; bareArm = 5; break;
                case WalkerStyle.Guard: top = sleeve = cuff = trousers = VoxelPalette.Ink; shoes = VoxelPalette.CarBlack; break;
                case WalkerStyle.Trader: top = sleeve = VoxelPalette.CarRed; cuff = VoxelPalette.Cream; trousers = VoxelPalette.Navy; break;
                default: throw new ArgumentOutOfRangeException(nameof(style));
            }
            byte shade = skin == VoxelPalette.Skin ? VoxelPalette.SkinShade : VoxelPalette.Bark;

            // Torso, hips to shoulders; couriers and students carry a backpack behind (+Z).
            bool pack = style == WalkerStyle.Courier || style == WalkerStyle.Student;
            var torso = new VoxelModel(8, 11, pack ? 8 : 5, PersonVoxel) { PivotX = 4, PivotZ = 2.5f };
            torso.Box(0, 0, 0, 8, 11, 5, top);
            switch (style)
            {
                case WalkerStyle.Clerk:
                    torso.Box(0, 0, 0, 8, 1, 5, VoxelPalette.Ink); torso.Box(3, 2, 0, 2, 8, 1, VoxelPalette.Navy); break;
                case WalkerStyle.Banker: case WalkerStyle.Trader:
                    torso.Box(2, 6, 0, 4, 5, 1, VoxelPalette.Cream);
                    torso.Box(3, 2, 0, 2, 7, 1, style == WalkerStyle.Banker ? VoxelPalette.Brass : VoxelPalette.Navy);
                    torso.Set(2, 10, 0, top); torso.Set(5, 10, 0, top);
                    if (style == WalkerStyle.Trader) torso.Box(5, 3, 0, 2, 2, 1, VoxelPalette.Cream);
                    break;
                case WalkerStyle.Casual:
                    torso.Box(0, 6, 0, 8, 1, 5, VoxelPalette.CarWhite); torso.Box(3, 10, 0, 2, 1, 1, VoxelPalette.CarWhite); break;
                case WalkerStyle.Coat:
                    torso.Box(0, 1, 0, 8, 1, 5, VoxelPalette.WoodDark);
                    torso.Box(1, 9, 0, 6, 2, 5, VoxelPalette.CarRed); torso.Box(5, 5, 0, 2, 4, 1, VoxelPalette.CarRed);
                    torso.Set(3, 4, 0, VoxelPalette.WoodDark); torso.Set(3, 7, 0, VoxelPalette.WoodDark);
                    break;
                case WalkerStyle.Courier:
                    torso.Box(0, 5, 0, 8, 1, 5, VoxelPalette.AwningStripe);
                    torso.Box(1, 3, 0, 1, 8, 1, VoxelPalette.Ink); torso.Box(6, 3, 0, 1, 8, 1, VoxelPalette.Ink);
                    torso.Box(1, 1, 5, 6, 9, 3, VoxelPalette.CarYellow); torso.Box(1, 9, 5, 6, 1, 3, VoxelPalette.Amber);
                    torso.Box(2, 4, 7, 4, 1, 1, VoxelPalette.AwningStripe);
                    break;
                case WalkerStyle.Student:
                    torso.Box(2, 1, 0, 4, 3, 1, VoxelPalette.PineMid); torso.Box(1, 10, 0, 6, 1, 5, VoxelPalette.PineMid);
                    torso.Box(2, 7, 0, 1, 2, 1, VoxelPalette.CarWhite); torso.Box(5, 7, 0, 1, 2, 1, VoxelPalette.CarWhite);
                    torso.Box(1, 4, 0, 1, 6, 1, VoxelPalette.PineDark); torso.Box(6, 4, 0, 1, 6, 1, VoxelPalette.PineDark);
                    torso.Box(1, 2, 5, 6, 8, 3, VoxelPalette.BrickRed); torso.Box(2, 4, 7, 4, 2, 1, VoxelPalette.BrickDark);
                    break;
                case WalkerStyle.Elder:
                    torso.Box(2, 8, 0, 4, 3, 1, VoxelPalette.Cream); torso.Set(2, 10, 0, top); torso.Set(5, 10, 0, top);
                    foreach (int y in new[] { 2, 4, 6 }) torso.Set(3, y, 0, VoxelPalette.Brass);
                    break;
                case WalkerStyle.Tourist:
                    torso.Box(1, 6, 0, 1, 5, 1, VoxelPalette.Ink); torso.Box(5, 6, 0, 1, 5, 1, VoxelPalette.Ink);
                    torso.Box(1, 4, 0, 5, 2, 1, VoxelPalette.Ink); torso.Set(3, 4, 0, VoxelPalette.Chrome);
                    break;
                case WalkerStyle.Analyst:
                    torso.Box(0, 0, 0, 8, 1, 5, VoxelPalette.Ink);
                    torso.Box(2, 7, 0, 1, 4, 1, VoxelPalette.Navy); torso.Box(5, 7, 0, 1, 4, 1, VoxelPalette.Navy);
                    torso.Box(3, 6, 0, 2, 1, 1, VoxelPalette.Navy); torso.Box(3, 4, 0, 2, 2, 1, VoxelPalette.Cream);
                    break;
                case WalkerStyle.Barista:
                    torso.Box(0, 4, 1, 8, 1, 4, VoxelPalette.WoodDark);
                    torso.Box(1, 0, 0, 6, 8, 1, VoxelPalette.WoodDark);
                    torso.Box(2, 8, 0, 1, 3, 1, VoxelPalette.WoodDark); torso.Box(5, 8, 0, 1, 3, 1, VoxelPalette.WoodDark);
                    break;
                case WalkerStyle.Guard:
                    torso.Box(0, 0, 0, 8, 1, 5, VoxelPalette.MetalDark); torso.Box(3, 0, 0, 2, 1, 1, VoxelPalette.Brass);
                    torso.Box(3, 10, 0, 2, 1, 1, VoxelPalette.Cream); torso.Box(3, 9, 0, 2, 1, 1, VoxelPalette.Navy);
                    torso.Set(3, 3, 0, VoxelPalette.Brass); torso.Set(3, 6, 0, VoxelPalette.Brass);
                    torso.Box(5, 7, 0, 2, 2, 1, VoxelPalette.Brass);
                    torso.Box(0, 10, 0, 2, 1, 5, VoxelPalette.Navy); torso.Box(6, 10, 0, 2, 1, 5, VoxelPalette.Navy);
                    break;
            }

            // Head on the neck: the face is the z = 1 layer, z = 0 holds cap brims and glasses.
            var head = new VoxelModel(7, 10, 8, PersonVoxel) { PivotX = 3.5f, PivotZ = 4.5f };
            head.Box(2, 0, 3, 3, 1, 3, shade);
            head.Box(0, 1, 1, 7, 6, 7, skin);
            head.Set(2, 4, 1, VoxelPalette.Ink); head.Set(4, 4, 1, VoxelPalette.Ink);
            head.Set(3, 3, 1, shade); head.Box(2, 2, 1, 3, 1, 1, shade);
            head.Box(0, 7, 1, 7, 1, 7, hair); head.Box(0, 5, 5, 7, 2, 3, hair);
            head.Box(0, 5, 2, 1, 2, 6, hair); head.Box(6, 5, 2, 1, 2, 6, hair);
            head.Box(0, 6, 1, 7, 1, 1, hair); head.Set(3, 6, 1, skin);
            switch (style)
            {
                case WalkerStyle.Casual:
                    head.Box(0, 1, 5, 7, 7, 3, hair); head.Box(0, 2, 4, 1, 5, 1, hair); head.Box(6, 2, 4, 1, 5, 1, hair); break;
                case WalkerStyle.Student:
                    for (int z = 1; z < 8; z++) for (int x = 0; x < 7; x++) if (VoxelNoise.Value(x, 8, z, 13) > 0.55f) head.Set(x, 8, z, hair);
                    break;
                case WalkerStyle.Barista: head.Box(2, 8, 4, 3, 2, 3, hair); break;
                case WalkerStyle.Courier: case WalkerStyle.Tourist:
                {
                    byte cap = style == WalkerStyle.Courier ? VoxelPalette.Amber : VoxelPalette.CarRed;
                    head.Box(0, 7, 1, 7, 2, 7, cap); head.Box(1, 7, 0, 5, 1, 1, cap); head.Set(3, 9, 4, cap);
                    break;
                }
                case WalkerStyle.Elder:
                    head.Box(1, 6, 1, 5, 1, 1, skin);
                    head.Box(0, 7, 1, 7, 1, 7, VoxelPalette.StoneDark); head.Box(1, 8, 2, 5, 1, 5, VoxelPalette.StoneDark);
                    head.Box(1, 7, 0, 5, 1, 1, VoxelPalette.StoneDark);
                    break;
                case WalkerStyle.Analyst:
                    head.Box(1, 5, 0, 5, 1, 1, VoxelPalette.Ink);
                    head.Set(1, 4, 0, VoxelPalette.Ink); head.Set(3, 4, 0, VoxelPalette.Ink); head.Set(5, 4, 0, VoxelPalette.Ink);
                    break;
                case WalkerStyle.Guard:
                    head.Box(0, 7, 1, 7, 1, 7, VoxelPalette.CarBlack); head.Box(0, 8, 1, 7, 2, 7, VoxelPalette.Navy);
                    head.Box(1, 7, 0, 5, 1, 1, VoxelPalette.Ink); head.Set(3, 8, 1, VoxelPalette.Brass);
                    break;
            }

            // Arm from the shoulder: hand, cuff, sleeve; rolled or short sleeves show more skin.
            var arm = new VoxelModel(2, 10, 3, PersonVoxel) { PivotX = 1, PivotY = 10, PivotZ = 1.5f };
            arm.Box(0, 0, 0, 2, 10, 3, sleeve);
            arm.Box(0, 0, 0, 2, bareArm, 3, skin);
            arm.Box(0, bareArm, 0, 2, 1, 3, cuff);
            if (style == WalkerStyle.Casual) arm.Box(0, 5, 0, 2, 1, 3, VoxelPalette.CarWhite);
            if (style == WalkerStyle.Courier) arm.Box(0, 4, 0, 2, 1, 3, VoxelPalette.AwningStripe);

            // Leg from the hip: trousers (or shorts over bare shins and socks), a coat hem or an apron, the shoe forward.
            var leg = new VoxelModel(3, 11, 4, PersonVoxel) { PivotX = 1.5f, PivotY = 11, PivotZ = 2.5f };
            leg.Box(0, 1, 1, 3, 10, 3, trousers);
            if (bareLeg > 0) { leg.Box(0, 1, 1, 3, 1, 3, VoxelPalette.CarWhite); leg.Box(0, 2, 1, 3, bareLeg - 1, 3, skin); }
            if (coat > 0) leg.Box(0, 11 - coat, 1, 3, coat, 3, top);
            if (style == WalkerStyle.Barista) leg.Box(0, 5, 1, 3, 6, 1, VoxelPalette.WoodDark);
            leg.Box(0, 0, 0, 3, 1, 4, shoes);

            // Seated: thighs on the seat (0.375–0.56 m), shins down to the shoes.
            var seated = new VoxelModel(8, 9, 9, PersonVoxel) { PivotX = 4, PivotY = 9, PivotZ = 6.5f };
            foreach (int x in new[] { 0, 5 })
            {
                seated.Box(x, 6, 1, 3, 3, 8, coat > 0 ? top : trousers);
                seated.Box(x, 1, 1, 3, 5, 3, trousers);
                if (bareLeg > 0) { seated.Box(x, 1, 1, 3, 1, 3, VoxelPalette.CarWhite); seated.Box(x, 2, 1, 3, 4, 3, skin); }
                seated.Box(x, 0, 0, 3, 1, 4, shoes);
            }
            return new VoxelWalker(torso, head, arm, leg, seated);
        }

        // ---------- Exchange ----------
        // Granite podium, 25.2 × 20.4 m in 10 cm voxels: four steps between two cheeks at the front (−Z), a checkered
        // top with a light border, parquet inside the hall, a red runner from the steps to the board, a low parapet along
        // the sides and the back. Pivot at the middle of the front edge on the terrain, 0.1 m under the square, so the top
        // lies at CityDistrictPlan.PodiumFloorY and the step treads at 0.3, 0.5, 0.7 and 0.9 m.
        public static VoxelModel ExchangePodium()
        {
            const int w = 252, d = 204, rows = 12, stairDepth = 20, stairHalf = 90, runnerHalf = 8, edge = 3, cx = w / 2;
            // Hall floor inside the walls (−8.5..8.5 m, 60.5..71.5 m) and the end of the runner at the board (69 m).
            const int hallMinX = 41, hallMaxX = 211, hallMinZ = 57, hallMaxZ = 167, runnerEndZ = 142;
            var m = new VoxelModel(w, rows + 5, d, ExchangeVoxel) { PivotX = w / 2f, PivotZ = 0 };
            for (int z = 0; z < d; z++)
                for (int x = 0; x < w; x++)
                {
                    float off = Math.Abs(x + 0.5f - cx);
                    bool stair = z < stairDepth && off < stairHalf, runner = off < runnerHalf && z < runnerEndZ;
                    bool runnerEdge = !runner && off < runnerHalf + 1 && z < runnerEndZ;
                    bool hall = x >= hallMinX && x < hallMaxX && z >= hallMinZ && z < hallMaxZ;
                    int height = stair ? 4 + 2 * (z / 5) : rows;
                    bool boundary = x == 0 || x == w - 1 || z == d - 1 || (z == 0 && !stair);
                    byte floor;
                    if (runner) floor = VoxelPalette.CarRed;
                    else if (runnerEdge) floor = VoxelPalette.Brass;
                    else if (stair) floor = z % 5 == 0 ? VoxelPalette.StoneLight : VoxelPalette.Concrete;
                    else if (x < edge || x >= w - edge || z >= d - edge || z < edge) floor = VoxelPalette.StoneLight;
                    else if (hall) floor = ((x - hallMinX) / 5 + (z - hallMinZ) / 5) % 2 == 0 ? VoxelPalette.Wood : VoxelPalette.WoodLight;
                    else floor = (x / 12 + z / 12) % 2 == 0 ? VoxelPalette.StoneLight : VoxelPalette.Concrete;
                    for (int y = 0; y < height; y++)
                    {
                        byte c = VoxelPalette.Stone;
                        if (boundary)
                        {
                            int along = x == 0 || x == w - 1 ? z : x;
                            if (y < 2) c = VoxelPalette.StoneDark;
                            else if (y == rows - 1) c = VoxelPalette.StoneLight;
                            else if ((along + (y - 2) / 3 % 2 * 6) % 12 == 0) c = VoxelPalette.StoneDark;
                        }
                        // The runner drapes over the risers of the steps.
                        if (y == height - 1 || (stair && runner && y >= height - 2)) c = y == height - 1 ? floor : VoxelPalette.CarRed;
                        m.Set(x, y, z, c);
                    }
                    bool parapet = (x < 2 || x >= w - 2) && z >= stairDepth || z >= d - 2;
                    if (parapet) { m.Box(x, rows, z, 1, 4, 1, VoxelPalette.Stone); m.Set(x, rows + 4, z, VoxelPalette.StoneLight); }
                }
            return m;
        }

        // Hall on the podium, 18 × 12 × 7 m in 25 cm voxels: plaster walls with stone pilasters, a 3 × 4 m door with a
        // transom in the middle of the front (−Z), tall windows between the columns, a wooden wainscot and a cornice
        // inside. Pivot at the middle of the front wall's outer face, on the podium top.
        private static readonly int[] FrontPilasters = { 10, 11, 25, 26, 45, 46, 60, 61 }, SidePilasters = { 5, 6, 19, 20, 32, 33 };
        public static VoxelModel ExchangeHall()
        {
            const int w = 72, h = 28, d = 48;
            var m = new VoxelModel(w, h, d, ShopVoxel) { PivotX = w / 2f, PivotZ = 0 };
            for (int y = 0; y < h; y++) for (int z = 0; z < d; z++) for (int x = 0; x < w; x++)
            {
                bool outer = x == 0 || x == w - 1 || z == 0 || z == d - 1, inner = x == 1 || x == w - 2 || z == 1 || z == d - 2;
                if (!outer && !inner) continue;
                byte c;
                if (outer)
                {
                    int along = x == 0 || x == w - 1 ? z : x;
                    bool pilaster = Array.IndexOf(z == 0 ? FrontPilasters : SidePilasters, along) >= 0;
                    c = y == 0 ? VoxelPalette.Stone : y >= h - 2 || pilaster || along < 2 || along >= (z == 0 || z == d - 1 ? w : d) - 2 ? VoxelPalette.StoneLight : VoxelPalette.Plaster;
                }
                else c = y < 4 ? VoxelPalette.WoodDark : y == 4 ? VoxelPalette.Wood : y >= h - 2 ? VoxelPalette.StoneLight : VoxelPalette.PlasterWarm;
                m.Set(x, y, z, c);
            }
            // Door, its stone frame with a brass keystone, and the transom window above.
            Window(m, 30, 18, 0, 12, 4, true);
            m.Box(30, 0, 0, 12, 16, 2, E);
            m.Box(28, 0, 0, 2, 18, 1, VoxelPalette.StoneLight); m.Box(42, 0, 0, 2, 18, 1, VoxelPalette.StoneLight);
            m.Box(30, 16, 0, 12, 2, 1, VoxelPalette.StoneLight); m.Box(35, 17, 0, 2, 1, 1, VoxelPalette.Brass);
            // Front windows between the middle columns, three windows in each side wall.
            foreach (int x0 in new[] { 14, 50 }) Window(m, x0, 8, 0, 8, 14, true);
            foreach (int z0 in new[] { 11, 25, 38 }) { Window(m, 0, 8, z0, 4, 14, false); Window(m, w - 2, 8, z0, 4, 14, false); }
            return m;
        }
        // Recessed window in a two-voxel wall: the outer layer opens, the inner layer holds the glass, a sill below.
        private static void Window(VoxelModel m, int a0, int y0, int b0, int width, int height, bool alongX)
        {
            for (int y = y0; y < y0 + height; y++) for (int i = 0; i < width; i++)
            {
                byte glass = y >= y0 + height - 3 ? VoxelPalette.GlassSky : y == y0 + height / 2 ? VoxelPalette.Trim : VoxelPalette.GlassDark;
                if (alongX) { m.Set(a0 + i, y, b0, E); m.Set(a0 + i, y, b0 + 1, glass); }
                else
                {
                    int outer = a0 == 0 ? 0 : a0 + 1, inner = a0 == 0 ? 1 : a0;
                    m.Set(outer, y, b0 + i, E); m.Set(inner, y, b0 + i, glass);
                }
            }
            for (int i = -1; i <= width; i++)
                if (alongX) { m.Set(a0 + i, y0 - 1, b0, VoxelPalette.StoneLight); m.Set(a0 + i, y0 + height, b0, VoxelPalette.StoneLight); }
                else { int outer = a0 == 0 ? 0 : a0 + 1; m.Set(outer, y0 - 1, b0 + i, VoxelPalette.StoneLight); m.Set(outer, y0 + height, b0 + i, VoxelPalette.StoneLight); }
        }

        // Entablature and pediment over the hall and the colonnade, 25 × 15.75 m: architrave, a frieze with triglyphs and
        // a plain middle for the name, a projecting cornice and a low gable roof with a recessed tympanum and a brass
        // medallion. Pivot at the middle of the front edge, on the column tops (7 m above the podium).
        public static VoxelModel ExchangeRoof()
        {
            const int w = 100, d = 63, cx = w / 2;
            var m = new VoxelModel(w, 17, d, ShopVoxel) { PivotX = w / 2f, PivotZ = 0 };
            m.Box(1, 0, 1, w - 2, 2, d - 2, VoxelPalette.StoneLight);
            m.Box(1, 2, 1, w - 2, 2, d - 2, VoxelPalette.Plaster);
            for (int y = 2; y < 4; y++)
            {
                for (int x = 1; x < w - 1; x++) if (x % 8 < 2 && (x < 30 || x >= 70)) m.Set(x, y, 1, VoxelPalette.Stone);
                for (int z = 1; z < d - 1; z++) if (z % 8 < 2) { m.Set(1, y, z, VoxelPalette.Stone); m.Set(w - 2, y, z, VoxelPalette.Stone); }
            }
            m.Box(0, 4, 0, w, 1, d, VoxelPalette.StoneLight);
            for (int y = 5; y < 17; y++)
            {
                int half = cx - (y - 5) * 4;
                for (int z = 0; z < d; z++) for (int x = cx - half; x < cx + half; x++)
                {
                    bool frame = y == 5 || x < cx - half + 3 || x >= cx + half - 3 || y == 16;
                    bool face = z < 2 || z >= d - 2;
                    byte c = !face ? VoxelPalette.PlasterSage : frame ? VoxelPalette.StoneLight : z == 0 ? E : z == d - 1 ? VoxelPalette.Plaster : VoxelPalette.Stone;
                    m.Set(x, y, z, c);
                }
            }
            m.Ellipsoid(cx, 10, 0.5f, 3.2f, 3.2f, 0.6f, (x, y, z, dd) => dd < 0.35f ? VoxelPalette.Amber : VoxelPalette.Brass);
            return m;
        }

        // Fluted column of the colonnade, 7 m in 10 cm voxels: square plinth, round base, tapering shaft, capital.
        public static VoxelModel ExchangeColumn()
        {
            var m = new VoxelModel(12, 70, 12, ExchangeVoxel);
            m.Box(0, 0, 0, 12, 2, 12, VoxelPalette.StoneLight);
            m.Cylinder(6, 6, 5.6f, 2, 2, VoxelPalette.StoneLight);
            m.Cylinder(6, 6, 5f, 4, 1, VoxelPalette.Stone);
            for (int y = 5; y < 63; y++)
            {
                float r = 4.6f - 0.6f * (y - 5) / 58f;
                for (int z = 0; z < 12; z++) for (int x = 0; x < 12; x++)
                {
                    float dx = x + 0.5f - 6, dz = z + 0.5f - 6;
                    if (dx * dx + dz * dz <= r * r) m.Set(x, y, z, (x + z) % 2 == 0 ? VoxelPalette.StoneLight : VoxelPalette.Concrete);
                }
            }
            m.Cylinder(6, 6, 4.4f, 63, 2, VoxelPalette.Stone);
            m.Cylinder(6, 6, 5.4f, 65, 3, VoxelPalette.StoneLight);
            m.Box(0, 68, 0, 12, 2, 12, VoxelPalette.StoneLight);
            return m;
        }

        // Quote board on the back wall of the hall, 8.4 × 3.4 m: brass frame, navy header, six dark rows for the scene's
        // text, an LED ticker strip at the bottom. Front −Z; pivot at the bottom centre of the 0.2 m slab.
        public static VoxelModel QuoteBoard()
        {
            const int w = 84, h = 34;
            var m = new VoxelModel(w, h, 2, ExchangeVoxel) { PivotX = w / 2f, PivotZ = 1 };
            m.Box(0, 0, 0, w, h, 2, VoxelPalette.Brass);
            m.Box(2, 4, 0, w - 4, 24, 1, VoxelPalette.Ink);
            for (int row = 0; row < 6; row++) if (row % 2 == 1) m.Box(2, 4 + row * 4, 0, w - 4, 4, 1, VoxelPalette.CarBlack);
            m.Box(2, 28, 0, w - 4, 4, 1, VoxelPalette.Navy);
            for (int x = 2; x < w - 2; x++) { m.Set(x, 1, 0, x % 3 == 0 ? VoxelPalette.Ink : VoxelPalette.ScreenGreen); m.Set(x, 2, 0, x % 3 == 1 ? VoxelPalette.Ink : VoxelPalette.ScreenGreen); }
            m.Box(2, 3, 0, w - 4, 1, 1, VoxelPalette.Ink);
            return m;
        }

        // ---------- Exchange square ----------
        // Rostral column, 12.2 m in 10 cm voxels: stone pedestal with brass plaques, red shaft with brass ship prows on four
        // levels (turning a quarter each level), a stone capital and a brass brazier with a glowing flame.
        public static VoxelModel RostralColumn()
        {
            const int s = 26, c = 13;
            var m = new VoxelModel(s, 122, s, ExchangeVoxel);
            m.Box(0, 0, 0, s, 4, s, VoxelPalette.StoneDark);
            m.Box(2, 4, 2, s - 4, 18, s - 4, VoxelPalette.Stone);
            m.Box(8, 9, 1, 10, 6, 1, VoxelPalette.Brass); m.Box(8, 9, s - 2, 10, 6, 1, VoxelPalette.Brass);
            m.Box(1, 9, 8, 1, 6, 10, VoxelPalette.Brass); m.Box(s - 2, 9, 8, 1, 6, 10, VoxelPalette.Brass);
            m.Box(1, 22, 1, s - 2, 3, s - 2, VoxelPalette.StoneLight);
            m.Cylinder(c, c, 5.6f, 25, 2, VoxelPalette.StoneLight);
            m.Cylinder(c, c, 4.6f, 27, 76, VoxelPalette.BrickRed);
            int[] reach = { 2, 4, 6, 7, 3 };
            for (int level = 0; level < 4; level++)
            {
                int y0 = 36 + level * 17;
                bool alongX = level % 2 == 0;
                m.Cylinder(c, c, 5f, y0 + 6, 1, VoxelPalette.Brass);
                for (int k = 0; k < reach.Length; k++)
                    for (int r = 0; r < reach[k]; r++)
                        for (int t = c - 1; t <= c + 1; t++)
                        {
                            if (r >= reach[k] - 2 && t != c) continue;
                            int o = 8 - r;
                            if (alongX) { m.Set(o, y0 + k, t, VoxelPalette.Brass); m.Set(s - 1 - o, y0 + k, t, VoxelPalette.Brass); }
                            else { m.Set(t, y0 + k, o, VoxelPalette.Brass); m.Set(t, y0 + k, s - 1 - o, VoxelPalette.Brass); }
                        }
            }
            m.Box(7, 103, 7, 12, 2, 12, VoxelPalette.StoneLight);
            m.Box(6, 105, 6, 14, 2, 14, VoxelPalette.StoneLight);
            m.Cylinder(c, c, 2f, 107, 3, VoxelPalette.Brass);
            m.Cylinder(c, c, 4.5f, 110, 2, VoxelPalette.Brass);
            m.Cylinder(c, c, 5.5f, 112, 2, VoxelPalette.Brass);
            for (int y = 114; y < 122; y++)
            {
                float r = 4.2f * (1 - (y - 114) / 8.5f);
                for (int z = 0; z < s; z++) for (int x = 0; x < s; x++)
                {
                    float dx = x + 0.5f - c, dz = z + 0.5f - c, dd = dx * dx + dz * dz;
                    if (dd > r * r || (y > 117 && VoxelNoise.Value(x, y, z, 31) > 0.7f)) continue;
                    m.Set(x, y, z, dd < r * r * 0.3f ? VoxelPalette.LampWarm : VoxelPalette.SignalAmber);
                }
            }
            return m;
        }

        // Bull (brass) or bear (bronze) on a dark stone pedestal for the cheeks of the Exchange steps, 2.6 m in all.
        // Heads −Z; brass plaques on both long sides of the pedestal.
        public static VoxelModel ExchangeStatue(bool bull)
        {
            var m = new VoxelModel(14, 26, 26, ExchangeVoxel);
            m.Box(0, 0, 0, 14, 9, 26, VoxelPalette.StoneDark);
            m.Box(0, 9, 0, 14, 1, 26, VoxelPalette.Stone);
            m.Box(0, 3, 7, 1, 3, 12, VoxelPalette.Brass); m.Box(13, 3, 7, 1, 3, 12, VoxelPalette.Brass);
            byte metal = bull ? VoxelPalette.Brass : VoxelPalette.Wood, dark = VoxelPalette.WoodDark;
            if (bull)
            {
                foreach (int x in new[] { 2, 9 }) foreach (int z in new[] { 6, 18 }) { m.Box(x, 10, z, 3, 6, 3, metal); m.Box(x, 10, z, 3, 1, 3, dark); }
                m.Ellipsoid(7, 18.5f, 14, 5.5f, 4, 9, metal);
                m.Ellipsoid(7, 20.5f, 9, 5.2f, 3.6f, 4.5f, metal);
                m.Ellipsoid(7, 16.5f, 4, 3.2f, 3.2f, 3.6f, metal);
                foreach (int side in new[] { -1, 1 })
                {
                    int x0 = side < 0 ? 4 : 9;
                    m.Set(x0, 19, 4, VoxelPalette.Cream); m.Set(x0 + side, 20, 3, VoxelPalette.Cream);
                    m.Set(x0 + 2 * side, 20, 2, VoxelPalette.Cream); m.Set(x0 + 2 * side, 21, 1, VoxelPalette.Cream);
                }
                m.Set(6, 14, 1, dark); m.Set(7, 14, 1, dark);
                m.Box(7, 15, 23, 1, 6, 1, metal); m.Box(7, 13, 23, 1, 2, 1, dark);
            }
            else
            {
                foreach (int x in new[] { 1, 9 }) foreach (int z in new[] { 6, 17 }) { m.Box(x, 10, z, 4, 5, 4, metal); m.Box(x, 10, z, 4, 1, 4, dark); }
                m.Ellipsoid(7, 18, 14, 6, 4.6f, 9, metal);
                m.Ellipsoid(7, 20.5f, 9.5f, 5.6f, 4, 4.5f, metal);
                m.Ellipsoid(7, 16, 4.2f, 3.6f, 3.2f, 3.6f, metal);
                m.Box(5, 14, 0, 4, 3, 2, metal); m.Box(6, 16, 0, 2, 1, 1, VoxelPalette.Ink);
                m.Box(4, 18, 3, 2, 2, 2, metal); m.Box(8, 18, 3, 2, 2, 2, metal);
                m.Box(6, 18, 23, 2, 2, 1, metal);
            }
            m.Set(4, 17, 2, VoxelPalette.Ink); m.Set(9, 17, 2, VoxelPalette.Ink);
            return m;
        }

        // Newspaper kiosk, 2.2 × 1.6 × 2.7 m: teal walls, a window of magazines over a small counter (−Z), a side door,
        // a navy band for the scene's «ПРЕССА» label and a striped roof.
        public static VoxelModel Kiosk()
        {
            const int w = 22, d = 16;
            var m = new VoxelModel(w, 27, d, ExchangeVoxel);
            m.Box(0, 0, 1, w, 1, d - 1, VoxelPalette.StoneDark);
            m.Box(0, 1, 1, w, 20, d - 1, VoxelPalette.Awning);
            byte[] covers = { VoxelPalette.CarRed, VoxelPalette.CarYellow, VoxelPalette.CarBlue, VoxelPalette.CarWhite, VoxelPalette.FlowerPink, VoxelPalette.CarGreen };
            for (int y = 9; y < 19; y++) for (int x = 2; x < w - 2; x++)
            {
                byte c = y >= 17 ? VoxelPalette.GlassSky : x == w / 2 ? VoxelPalette.Trim : (y - 9) % 3 == 2 ? VoxelPalette.GlassDark
                    : covers[RandInt(x, (y - 9) / 3, 0, covers.Length)];
                m.Set(x, y, 1, c);
            }
            m.Box(1, 8, 0, w - 2, 1, 2, VoxelPalette.Wood);
            m.Box(2, 2, 1, w - 4, 5, 1, VoxelPalette.AwningStripe);
            m.Box(w - 1, 1, 5, 1, 17, 6, VoxelPalette.WoodLight); m.Set(w - 1, 9, 6, VoxelPalette.Brass);
            m.Box(0, 21, 0, w, 3, d, VoxelPalette.Navy);
            m.Box(0, 24, 0, w, 1, d, VoxelPalette.AwningStripe);
            for (int x = 0; x < w; x++) m.Box(x, 25, 1, 1, 1, d - 2, x / 2 % 2 == 0 ? VoxelPalette.Awning : VoxelPalette.AwningStripe);
            m.Box(3, 26, 3, w - 6, 1, d - 6, VoxelPalette.AwningStripe);
            return m;
        }

        // Lawn of the Exchange square, 6 × 5 m in 10 cm voxels: a stone kerb 0.3 m high around grass with scattered flowers.
        public static VoxelModel Lawn(int seed)
        {
            const int w = 60, d = 50;
            var m = new VoxelModel(w, 3, d, ExchangeVoxel);
            for (int z = 0; z < d; z++) for (int x = 0; x < w; x++)
            {
                if (x < 1 || x >= w - 1 || z < 1 || z >= d - 1) { m.Box(x, 0, z, 1, 3, 1, VoxelPalette.StoneLight); continue; }
                float n = VoxelNoise.Value(x / 3, 1, z / 3, seed);
                m.Box(x, 0, z, 1, 2, 1, n < 0.33f ? VoxelPalette.GrassLight : n < 0.8f ? VoxelPalette.GrassMid : VoxelPalette.GrassDark);
                float f = VoxelNoise.Value(x, 2, z, seed + 1);
                if (x > 2 && x < w - 3 && z > 2 && z < d - 3 && f > 0.95f)
                    m.Set(x, 2, z, f > 0.983f ? VoxelPalette.FlowerPink : f > 0.966f ? VoxelPalette.FlowerYellow : VoxelPalette.FlowerWhite);
            }
            return m;
        }

        // Clipped hedge along X, 1.25 m high and 0.5 m deep with a ragged top; the length is rounded to the 25 cm grid.
        public static VoxelModel Hedge(float length, int seed)
        {
            if (!(length > 0) || float.IsInfinity(length)) throw new ArgumentOutOfRangeException(nameof(length));
            int n = Math.Max(2, Round(length / NatureVoxel));
            var m = new VoxelModel(n, 5, 2, NatureVoxel);
            for (int y = 0; y < 5; y++) for (int z = 0; z < 2; z++) for (int x = 0; x < n; x++)
            {
                float v = VoxelNoise.Value(x, y, z, seed);
                if (y == 4 && v > 0.7f) continue;
                m.Set(x, y, z, y == 0 ? VoxelPalette.LeafDark : v < 0.3f ? VoxelPalette.LeafLight : v < 0.75f ? VoxelPalette.LeafMid : VoxelPalette.LeafDark);
            }
            return m;
        }

        // ---------- Café «Бык и медведь» ----------
        // The open shell among the closed street facades: 6 × 9 × 8 m like Shop but with the front +Z (towards the street),
        // a 5 × 7 × 3 m room behind an open door and a wide window, a dark name band above a red awning. Pivot at the
        // middle of the back wall's outer face.
        public static VoxelModel CafeShell()
        {
            const int w = 24, h = 36, d = 32, porch = 8, room = 12, f = d - 1;
            var m = new VoxelModel(w, h + 3, d + porch, ShopVoxel) { PivotX = w / 2f, PivotZ = 0 };
            m.Box(0, 0, 0, w, h, d, VoxelPalette.PlasterWarm);
            m.Box(0, h - 1, 0, w, 1, d, VoxelPalette.RoofDark);
            m.Box(0, 0, f, w, 1, 1, VoxelPalette.StoneDark);
            // Room: wooden wainscot and plaster on the inner layer of the walls, a light ceiling.
            for (int y = 0; y <= room; y++) for (int z = 1; z <= d - 2; z++) for (int x = 1; x <= w - 2; x++)
            {
                if (y < room && x >= 2 && x <= w - 3 && z >= 2 && z <= d - 3) { m.Set(x, y, z, E); continue; }
                m.Set(x, y, z, y == room ? VoxelPalette.Plaster : y < 4 ? VoxelPalette.WoodDark : y == 4 ? VoxelPalette.Wood : VoxelPalette.Plaster);
            }
            // Open door (x 4–9) and a wide open window with a mullion (x 12–20) in the trimmed front wall.
            m.Box(4, 0, d - 2, 6, 10, 2, E);
            m.Box(3, 0, f, 1, 10, 1, VoxelPalette.Trim); m.Box(10, 0, f, 1, 10, 1, VoxelPalette.Trim);
            m.Box(12, 3, d - 2, 9, 7, 2, E);
            m.Box(11, 2, f, 11, 1, 1, VoxelPalette.Trim); m.Box(11, 3, f, 1, 7, 1, VoxelPalette.Trim);
            m.Box(21, 3, f, 1, 7, 1, VoxelPalette.Trim); m.Box(16, 3, f, 1, 7, 1, VoxelPalette.Trim);
            m.Box(11, 10, f, 11, 1, 1, VoxelPalette.Trim);
            // Name band for the scene's label, then the upper floors as on the neighbours.
            m.Box(1, 12, f, w - 2, 2, 1, VoxelPalette.WoodDark);
            for (int floor = 0; floor < 2; floor++)
            {
                int y0 = 12 + floor * 12;
                if (floor > 0) m.Box(0, y0, f, w, 1, 1, VoxelPalette.Plaster);
                foreach (int x0 in new[] { 2, 10, 18 })
                {
                    float n = VoxelNoise.Value(x0, floor, 2, 5);
                    for (int y = y0 + 3; y <= y0 + 8; y++) for (int x = x0; x < x0 + 4; x++)
                    {
                        m.Set(x, y, f, E);
                        m.Set(x, y, f - 1, n < 0.3f ? VoxelPalette.WindowLit : y == y0 + 8 ? VoxelPalette.GlassSky : VoxelPalette.GlassDark);
                    }
                    m.Box(x0 - 1, y0 + 2, f + 1, 6, 1, 1, VoxelPalette.Trim);
                    m.Box(x0 - 1, y0 + 9, f, 6, 1, 1, VoxelPalette.Plaster);
                }
            }
            m.Box(0, h - 2, f + 1, w, 1, 1, VoxelPalette.Trim);
            m.Box(0, h - 1, f + 1, w, 1, 1, VoxelPalette.Plaster);
            for (int x = 0; x < w; x++) for (int z = 0; z < d; z++)
                if (x == 0 || x == w - 1 || z == 0 || z == d - 1) m.Set(x, h, z, VoxelPalette.Trim);
            m.Box(3, h, 4, 4, 2, 4, VoxelPalette.MetalDark);
            // The back faces the ring road like the neighbours' backs; the room's inner wall stays behind its windows.
            BackFacade(m, 0, 1, 5, VoxelPalette.Plaster, VoxelPalette.Trim);
            // Red and cream awning over the door and the window, stepping out 1.5 m.
            for (int step = 0; step < 3; step++)
                for (int x = 1; x < w - 1; x++) for (int dz = 0; dz < 2; dz++)
                    m.Set(x, 11 - step, d + step * 2 + dz, x / 2 % 2 == 0 ? VoxelPalette.CarRed : VoxelPalette.AwningStripe);
            for (int x = 1; x < w - 1; x += 2) m.Set(x, 8, d + 5, x / 2 % 2 == 0 ? VoxelPalette.CarRed : VoxelPalette.AwningStripe);
            return m;
        }

        // Café counter, 3.2 × 0.7 m and 1.05 m high in 5 cm voxels: panelled front (−Z, the customers' side), a light stone
        // top, a pastry case and a till.
        public static VoxelModel CafeCounter()
        {
            const int w = 64, d = 14;
            var m = new VoxelModel(w, 25, d, FurnitureVoxel);
            m.Box(0, 0, 1, w, 2, d - 1, VoxelPalette.Ink);
            m.Box(0, 2, 1, w, 17, d - 1, VoxelPalette.WoodDark);
            for (int y = 2; y < 19; y++) for (int x = 0; x < w; x++)
                m.Set(x, y, 0, x % 8 == 0 || y == 2 || y == 18 ? VoxelPalette.Wood : VoxelPalette.WoodDark);
            m.Box(0, 19, 0, w, 2, d, VoxelPalette.StoneLight);
            m.Box(4, 21, 2, 14, 3, 8, VoxelPalette.GlassSky); m.Box(4, 24, 2, 14, 1, 8, VoxelPalette.MetalLight);
            byte[] pastry = { VoxelPalette.Cream, VoxelPalette.FlowerPink, VoxelPalette.WoodLight };
            for (int x = 5; x < 17; x += 2) m.Box(x, 22, 2, 1, 1, 1, pastry[x / 2 % 3]);
            m.Box(48, 21, 5, 6, 3, 5, VoxelPalette.Ink); m.Box(49, 23, 5, 4, 1, 1, VoxelPalette.ScreenGreen);
            return m;
        }

        // Wall shelf behind the counter, 3 × 2.2 m: four boards with tins, bags and cups; the back panel lies against the
        // wall (+Z).
        public static VoxelModel CafeShelf(int seed)
        {
            const int w = 60, h = 44, d = 8;
            var m = new VoxelModel(w, h, d, FurnitureVoxel);
            m.Box(0, 0, d - 1, w, h, 1, VoxelPalette.Wood);
            m.Box(0, 0, 0, 2, h, d, VoxelPalette.WoodDark); m.Box(w - 2, 0, 0, 2, h, d, VoxelPalette.WoodDark);
            byte[] goods = { VoxelPalette.Cream, VoxelPalette.Brass, VoxelPalette.CarRed, VoxelPalette.Amber, VoxelPalette.WoodLight, VoxelPalette.CarWhite, VoxelPalette.Navy };
            foreach (int y0 in new[] { 0, 14, 28 })
            {
                m.Box(0, y0, 0, w, 2, d, VoxelPalette.WoodDark);
                for (int x = 3; x < w - 3;)
                {
                    int salt = x + y0 * 7, width = RandInt(seed, salt, 2, 5), height = RandInt(seed, salt + 1, 3, 9);
                    if (x + width > w - 3) break;
                    m.Box(x, y0 + 2, 2, width, height, 4, goods[RandInt(seed, salt + 2, 0, goods.Length)]);
                    x += width + RandInt(seed, salt + 3, 1, 3);
                }
            }
            m.Box(0, h - 2, 0, w, 2, d, VoxelPalette.WoodDark);
            return m;
        }

        // Round café table, 0.8 m across and 0.75 m high, with a cup.
        public static VoxelModel CafeTable()
        {
            var m = new VoxelModel(16, 17, 16, FurnitureVoxel);
            m.Cylinder(8, 8, 3.6f, 0, 1, VoxelPalette.MetalDark);
            m.Box(7, 1, 7, 2, 13, 2, VoxelPalette.MetalDark);
            m.Cylinder(8, 8, 8, 14, 1, VoxelPalette.Wood);
            m.Cylinder(8, 8, 7, 14, 1, VoxelPalette.WoodLight);
            m.Box(5, 15, 5, 2, 2, 2, VoxelPalette.CarWhite); m.Set(7, 16, 5, VoxelPalette.CarWhite);
            return m;
        }

        // Café chair: metal legs, wooden seat 0.4 m high, back towards +Z.
        public static VoxelModel CafeChair()
        {
            var m = new VoxelModel(9, 18, 9, FurnitureVoxel);
            foreach (int x in new[] { 0, 7 }) foreach (int z in new[] { 0, 7 }) m.Box(x, 0, z, 2, 7, 2, VoxelPalette.MetalDark);
            m.Box(0, 7, 0, 9, 1, 9, VoxelPalette.Wood);
            m.Box(0, 8, 7, 2, 10, 2, VoxelPalette.MetalDark); m.Box(7, 8, 7, 2, 10, 2, VoxelPalette.MetalDark);
            m.Box(0, 12, 8, 9, 5, 1, VoxelPalette.Wood);
            return m;
        }

        // Chalk menu on the café's back wall, 1.2 × 0.9 m, front −Z: wooden frame, dark board, lines of dishes and prices.
        // Pivot in the middle of the slab's depth.
        public static VoxelModel MenuBoard()
        {
            const int w = 24, h = 18;
            var m = new VoxelModel(w, h, 2, FurnitureVoxel) { PivotZ = 1 };
            m.Box(0, 0, 0, w, h, 2, VoxelPalette.Wood);
            m.Box(1, 1, 0, w - 2, h - 2, 1, VoxelPalette.Ink);
            m.Box(5, 14, 0, 14, 2, 1, VoxelPalette.Cream);
            for (int line = 0; line < 4; line++)
            {
                int y = 11 - line * 3;
                m.Box(3, y, 0, 7 + RandInt(3, line, 0, 6), 1, 1, VoxelPalette.CarWhite);
                m.Box(w - 6, y, 0, 3, 1, 1, VoxelPalette.Amber);
            }
            return m;
        }

        // Pendant lamp over the counter: cord, brass shade, warm bulb. Pivot at the top of the cord (the ceiling).
        public static VoxelModel PendantLamp()
        {
            var m = new VoxelModel(6, 14, 6, FurnitureVoxel) { PivotY = 14 };
            m.Box(2, 6, 2, 2, 8, 2, VoxelPalette.Ink);
            m.Box(1, 4, 1, 4, 2, 4, VoxelPalette.Brass);
            m.Box(0, 2, 0, 6, 2, 6, VoxelPalette.Brass);
            m.Box(2, 1, 2, 2, 1, 2, VoxelPalette.LampWarm);
            return m;
        }

        // ---------- Crossing signals ----------
        // Pole of the crossing signals, 3.6 m: base, yellow band, cap. The heads and the button are separate models so the
        // scene can swap lit variants.
        public static VoxelModel SignalPost()
        {
            var m = new VoxelModel(6, 36, 6, ExchangeVoxel);
            m.Box(1, 0, 1, 4, 1, 4, VoxelPalette.MetalDark);
            m.Box(2, 1, 2, 2, 34, 2, VoxelPalette.MetalDark);
            m.Box(2, 12, 2, 2, 3, 2, VoxelPalette.Amber);
            m.Box(1, 35, 1, 4, 1, 4, VoxelPalette.MetalDark);
            return m;
        }
        // Car signal head, 0.4 × 1.2 m, lenses −Z: red over amber over green under small visors.
        // lit: −1 none, 0 red, 1 amber, 2 green.
        public static VoxelModel CarSignalHead(int lit)
        {
            if (lit < -1 || lit > 2) throw new ArgumentOutOfRangeException(nameof(lit));
            var m = new VoxelModel(4, 12, 4, ExchangeVoxel);
            m.Box(0, 0, 1, 4, 12, 3, VoxelPalette.Ink);
            byte[] on = { VoxelPalette.SignalRed, VoxelPalette.SignalAmber, VoxelPalette.SignalGreen };
            byte[] off = { VoxelPalette.BrickDark, VoxelPalette.WoodDark, VoxelPalette.PineDark };
            for (int i = 0; i < 3; i++)
            {
                int y = 9 - i * 4;
                m.Box(1, y, 1, 2, 2, 1, lit == i ? on[i] : off[i]);
                m.Box(0, y + 2, 0, 4, 1, 1, VoxelPalette.Ink);
            }
            return m;
        }
        // Pedestrian signal head, 0.5 × 1.1 m, front −Z: a standing red figure over a walking green one.
        // lit: −1 none, 0 red, 1 green.
        public static VoxelModel PedSignalHead(int lit)
        {
            if (lit < -1 || lit > 1) throw new ArgumentOutOfRangeException(nameof(lit));
            var m = new VoxelModel(5, 11, 3, ExchangeVoxel);
            m.Box(0, 0, 0, 5, 11, 3, VoxelPalette.Ink);
            m.Box(1, 6, 0, 3, 4, 1, VoxelPalette.CarBlack); m.Box(1, 1, 0, 3, 4, 1, VoxelPalette.CarBlack);
            byte red = lit == 0 ? VoxelPalette.SignalRed : VoxelPalette.BrickDark, green = lit == 1 ? VoxelPalette.SignalGreen : VoxelPalette.PineDark;
            m.Set(2, 9, 0, red); m.Box(1, 8, 0, 3, 1, 1, red); m.Set(2, 7, 0, red); m.Set(1, 6, 0, red); m.Set(3, 6, 0, red);
            m.Set(2, 4, 0, green); m.Box(1, 3, 0, 2, 1, 1, green); m.Box(2, 2, 0, 2, 1, 1, green); m.Set(1, 1, 0, green); m.Set(3, 1, 0, green);
            return m;
        }
        // Push-button box, 0.25 × 0.4 m, front −Z: a round button under the «waiting» lamp.
        public static VoxelModel CrosswalkButton(bool waiting)
        {
            var m = new VoxelModel(5, 8, 3, FurnitureVoxel);
            m.Box(0, 0, 0, 5, 8, 3, VoxelPalette.CarYellow);
            m.Box(1, 1, 0, 3, 3, 1, VoxelPalette.CarBlack); m.Set(2, 2, 0, VoxelPalette.MetalLight);
            m.Box(1, 5, 0, 3, 2, 1, waiting ? VoxelPalette.SignalAmber : VoxelPalette.WoodDark);
            return m;
        }

        // ---------- Boulevard tunnels ----------
        // Portal at each end of the boulevard, 18 × 9 m in 50 cm voxels: jointed concrete with a stone frame around the
        // 14 × 6.5 m mouth over the carriageway, small lamps on the lintel and a coping. Front −Z; pivot at the bottom
        // centre of the 1 m slab.
        public static VoxelModel TunnelPortal()
        {
            const int w = 36, h = 18;
            var m = new VoxelModel(w, h, 2, TowerVoxel) { PivotX = w / 2f, PivotZ = 1 };
            m.Box(0, 0, 0, w, h, 2, VoxelPalette.Concrete);
            for (int x = 0; x < w; x += 4) m.Box(x, 1, 0, 1, h - 2, 1, VoxelPalette.Stone);
            m.Box(0, 0, 0, w, 1, 2, VoxelPalette.StoneDark);
            m.Box(3, 0, 0, 30, 14, 1, VoxelPalette.StoneLight);
            m.Box(4, 0, 0, 28, 13, 2, E);
            for (int x = 6; x < 31; x += 4) m.Set(x, 13, 0, VoxelPalette.LampWarm);
            m.Box(0, h - 1, 0, w, 1, 2, VoxelPalette.StoneLight);
            return m;
        }
    }
}
