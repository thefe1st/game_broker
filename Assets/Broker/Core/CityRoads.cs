using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    public readonly struct RoadPoint
    {
        public float X { get; } public float Z { get; }
        public RoadPoint(float x, float z) { X = x; Z = z; }
    }

    // A 45° cut of a kerb corner: the corner (X, Z) of the off-road quadrant that lies toward (Sx, Sz) and the legs along
    // both kerbs. The triangle between the corner and the two ends of the slanted kerb becomes road.
    public readonly struct KerbCut
    {
        public float X { get; } public float Z { get; } public float Sx { get; } public float Sz { get; } public float Leg { get; }
        public KerbCut(float x, float z, float sx, float sz, float leg)
        {
            if (Math.Abs(sx) != 1 || Math.Abs(sz) != 1 || !(leg > 0)) throw new ArgumentException("A kerb cut needs a quadrant and a leg.");
            X = x; Z = z; Sx = sx; Sz = sz; Leg = leg;
        }
        // The ends of the slanted kerb: on the kerb along X and on the kerb along Z.
        public RoadPoint Start => new RoadPoint(X + Sx * Leg, Z);
        public RoadPoint End => new RoadPoint(X, Z + Sz * Leg);
    }

    // A building on the outer side of the ring: its centre, the facade length along the street, the depth, the height, the
    // TowerStyle and the yaw that turns the model's front (−Z) toward the road.
    public sealed class RoadsideBuilding
    {
        public float X { get; } public float Z { get; } public float Front { get; } public float Depth { get; } public float Height { get; }
        public int Style { get; } public float Yaw { get; }
        internal RoadsideBuilding(float x, float z, float front, float depth, float height, int style, float yaw)
        { X = x; Z = z; Front = front; Depth = depth; Height = height; Style = style; Yaw = yaw; }
        // The ground it covers: the facade runs along X when the front faces north or south, along Z otherwise.
        public CityRect Footprint => Math.Abs(Math.Abs(Yaw) - 90) < 1 ? CityRect.Around(X, Z, Depth / 2, Front / 2) : CityRect.Around(X, Z, Front / 2, Depth / 2);
    }

    // A lamp on an outer pavement: the pole and the yaw that turns its arm (+Z of the model) over the road.
    public readonly struct RoadsideLamp
    {
        public float X { get; } public float Z { get; } public float Yaw { get; }
        public RoadsideLamp(float x, float z, float yaw) { X = x; Z = z; Yaw = yaw; }
    }

    // Asphalt the car may use: a union of rectangles, some of them slanted. Everything outside is a kerb face the car
    // stops against. Kerb corners that point into the carriageway are found once, so the car's flat sides cannot slide
    // through them.
    public sealed class DriveArea
    {
        public ReadOnlyCollection<OrientedRect> Pieces { get; }
        public ReadOnlyCollection<RoadPoint> InnerCorners { get; }
        // A band that loops along X (the highway between its tunnels): a car past one end comes back at the other.
        public CityRect? Loop { get; }
        public DriveArea(params CityRect[] rects) : this(Array.ConvertAll(rects ?? new CityRect[0], OrientedRect.From)) { }
        public DriveArea(params OrientedRect[] pieces) : this(null, pieces) { }
        public DriveArea(CityRect? loop, params OrientedRect[] pieces)
        {
            if (pieces == null || pieces.Length == 0) throw new ArgumentException("A drive area needs at least one piece.");
            if (loop.HasValue && !(loop.Value.Width > 0)) throw new ArgumentException("A loop needs a length.");
            Pieces = Array.AsReadOnly((OrientedRect[])pieces.Clone());
            InnerCorners = Array.AsReadOnly(FindInnerCorners());
            Loop = loop;
        }
        // Brings a point past an end of the loop band round to the other end; false when nothing changed.
        public bool Wrap(ref float x, float z)
        {
            if (!Loop.HasValue) return false;
            var band = Loop.Value;
            if (z < band.MinZ || z > band.MaxZ) return false;
            if (x > band.MaxX) { x -= band.Width; return true; }
            if (x < band.MinX) { x += band.Width; return true; }
            return false;
        }
        public bool Contains(float x, float z)
        {
            foreach (var piece in Pieces) if (piece.Contains(x, z)) return true;
            return false;
        }
        // Distance from a point off the road to the nearest point of the road and the unit direction toward it; 0 on the road.
        public float Outside(float x, float z, out float towardX, out float towardZ)
        {
            towardX = towardZ = 0;
            float best = float.MaxValue;
            foreach (var piece in Pieces)
            {
                float d = piece.Outside(x, z, out float tx, out float tz);
                if (d <= 0) { towardX = towardZ = 0; return 0; }
                if (d < best) { best = d; towardX = tx; towardZ = tz; }
            }
            return best;
        }
        // A piece corner where the kerb turns into the road: more than half of a small circle round it is asphalt.
        private RoadPoint[] FindInnerCorners()
        {
            var found = new List<RoadPoint>();
            const int samples = 16; const float radius = 0.02f;
            foreach (var piece in Pieces)
                for (int c = 0; c < 4; c++)
                {
                    piece.Corner(c, out float x, out float z);
                    int inside = 0;
                    for (int k = 0; k < samples; k++)
                    {
                        double angle = (k + 0.5) * 2 * Math.PI / samples;
                        if (Contains(x + radius * (float)Math.Cos(angle), z + radius * (float)Math.Sin(angle))) inside++;
                    }
                    if (inside > samples / 2 && inside < samples && !found.Exists(p => Math.Abs(p.X - x) < 1e-3f && Math.Abs(p.Z - z) < 1e-3f))
                        found.Add(new RoadPoint(x, z));
                }
            return found.ToArray();
        }
    }

    // The ring of roads around the office quarter that the player's car may use. The boulevard between its kerbs, up to
    // the tunnel portals, is the north side of the ring; the west, south and east streets run behind the old district and
    // the office tower; a lay-by cut into the western square holds the test car. From the middle of the south street the
    // Southern drive runs down through the new quarter to the highway: two carriageways of two lanes each side of a
    // concrete median with a gap at the junction, between two tunnels. Kerb corners at the junctions are cut at 45° and
    // the lay-by has tapered ends, so the car turns in without catching them. Local coordinates match CityDistrictPlan.
    // Kerbs and pavements are walls for the car (it never mounts them). The boulevard tunnels stay with its traffic, so
    // the car stops at their portals; the highway goes on through its tunnels: past HighwayLoopX the car comes out of
    // the other one, like the highway traffic.
    public static class CityRoads
    {
        // Centre lines and half-width of the two-lane streets; the kerb faces of the boulevard as the car meets them.
        public const float WestX = -50, EastX = 50, SouthZ = -20, StreetHalfWidth = 4, KerbHalf = 0.175f;
        public const float BoulevardMinZ = CityDistrictPlan.SouthKerbZ + KerbHalf, BoulevardMaxZ = CityDistrictPlan.NorthKerbZ - KerbHalf;
        public const float BoulevardEndX = CityScenery.TunnelPortalX - 0.8f;
        // Lay-by: a kerb cut into the western square for one car standing along the kerb, nose to the east; its ends taper
        // at 45° over its depth.
        public const float BayMinX = -30, BayMaxX = -19.5f, BayMinZ = 26.1f, BayTaper = BoulevardMinZ - BayMinZ;
        public const float ParkX = -24.75f, ParkZ = 27.35f, ParkHeading = 0;
        // The car counts as parked with every corner in the lay-by (its tapers included) or at most this far into the
        // boulevard, where it still leaves the first lane free. The driver gets out onto the square behind the railing.
        public const float ParkingMaxZ = 29.1f, ExitZ = 25.35f;
        // The railing between the lay-by and the square (pedestrians stay behind it).
        public const float BayRailZ = 25.95f;
        // Legs of the 45° cuts at the junction corners: short beside the district boundary, longer elsewhere; at the top of
        // the Southern drive the towers of the corners stand close.
        public const float InnerCut = 1.4f, OuterCut = 3f, DriveCut = 2f;
        // The Southern drive: a two-lane street on the office's axis from the south street to the highway.
        public const float DriveHalfWidth = StreetHalfWidth;
        // The highway: its median's centre line and half-width, a carriageway of two 3.5 m lanes with half a metre to spare
        // on each side, the gap in the median at the junction, and where the car loops inside the tunnels (the tubes'
        // asphalt reaches a little further).
        public const float HighwayZ = -92, MedianHalf = 0.5f, CarriagewayWidth = 8, LaneWidth = 3.5f, MedianGapHalf = 10;
        public const float HighwayNorthZ = HighwayZ + MedianHalf + CarriagewayWidth, HighwaySouthZ = HighwayZ - MedianHalf - CarriagewayWidth;
        public const float HighwayLoopX = 90, HighwayEndX = HighwayLoopX + 4;
        public static readonly CityRect Boulevard = new CityRect(-BoulevardEndX, BoulevardMinZ, BoulevardEndX, BoulevardMaxZ);
        public static readonly CityRect WestStreet = new CityRect(WestX - StreetHalfWidth, SouthZ - StreetHalfWidth, WestX + StreetHalfWidth, BoulevardMinZ);
        public static readonly CityRect EastStreet = new CityRect(EastX - StreetHalfWidth, SouthZ - StreetHalfWidth, EastX + StreetHalfWidth, BoulevardMinZ);
        public static readonly CityRect SouthStreet = new CityRect(WestX - StreetHalfWidth, SouthZ - StreetHalfWidth, EastX + StreetHalfWidth, SouthZ + StreetHalfWidth);
        public static readonly CityRect Bay = new CityRect(BayMinX, BayMinZ, BayMaxX, BoulevardMinZ);
        public static readonly CityRect SouthernDrive = new CityRect(-DriveHalfWidth, HighwayNorthZ, DriveHalfWidth, SouthZ - StreetHalfWidth);
        // The carriageways: westbound north of the median, eastbound south of it; the gap joins them at the junction.
        public static readonly CityRect HighwayWestbound = new CityRect(-HighwayEndX, HighwayZ + MedianHalf, HighwayEndX, HighwayNorthZ);
        public static readonly CityRect HighwayEastbound = new CityRect(-HighwayEndX, HighwaySouthZ, HighwayEndX, HighwayZ - MedianHalf);
        public static readonly CityRect MedianGap = new CityRect(-MedianGapHalf, HighwayZ - MedianHalf, MedianGapHalf, HighwayZ + MedianHalf);
        // The stretch of the highway the car goes round: out of one tunnel when it drives deep into the other.
        public static readonly CityRect HighwayLoop = new CityRect(-HighwayLoopX, HighwaySouthZ, HighwayLoopX, HighwayNorthZ);
        public static readonly CityRect ParkingZone = new CityRect(BayMinX - 1.5f, BayMinZ, BayMaxX + 1.5f, ParkingMaxZ);
        // The office block behind the plaza, its top floor the player's office.
        public static readonly CityRect OfficeBlock = new CityRect(-15.5f, -12.1f, 15.5f, 7.4f);
        // The straight pieces, west to east and north to south: the boulevard, the three streets and the lay-by, then the
        // Southern drive and the highway.
        public static ReadOnlyCollection<CityRect> Drivable { get; } = Array.AsReadOnly(new[]
            { Boulevard, WestStreet, SouthStreet, EastStreet, Bay, SouthernDrive, HighwayWestbound, MedianGap, HighwayEastbound });
        // Openings in the boulevard's south kerb, west to east: the west street, the lay-by and the east street, each
        // with its corner cuts.
        public static ReadOnlyCollection<CityRect> SouthKerbCuts { get; } = Array.AsReadOnly(new[]
        {
            new CityRect(WestStreet.MinX - OuterCut, WestStreet.MaxZ, WestStreet.MaxX + InnerCut, WestStreet.MaxZ),
            new CityRect(BayMinX - BayTaper, BoulevardMinZ, BayMaxX + BayTaper, BoulevardMinZ),
            new CityRect(EastStreet.MinX - InnerCut, EastStreet.MaxZ, EastStreet.MaxX + OuterCut, EastStreet.MaxZ),
        });
        // The 45° kerb cuts: the four corners where the streets meet the boulevard, the two inner corners of the south
        // street, the tapers of the lay-by, then both ends of the Southern drive.
        private static readonly KerbCut[] kerbCuts =
        {
            new KerbCut(WestStreet.MinX, BoulevardMinZ, -1, -1, OuterCut), new KerbCut(WestStreet.MaxX, BoulevardMinZ, 1, -1, InnerCut),
            new KerbCut(EastStreet.MinX, BoulevardMinZ, -1, -1, InnerCut), new KerbCut(EastStreet.MaxX, BoulevardMinZ, 1, -1, OuterCut),
            new KerbCut(WestStreet.MaxX, SouthStreet.MaxZ, 1, 1, OuterCut), new KerbCut(EastStreet.MinX, SouthStreet.MaxZ, -1, 1, OuterCut),
            new KerbCut(BayMinX, BoulevardMinZ, -1, -1, BayTaper), new KerbCut(BayMaxX, BoulevardMinZ, 1, -1, BayTaper),
            new KerbCut(SouthernDrive.MinX, SouthernDrive.MaxZ, -1, -1, DriveCut), new KerbCut(SouthernDrive.MaxX, SouthernDrive.MaxZ, 1, -1, DriveCut),
            new KerbCut(SouthernDrive.MinX, SouthernDrive.MinZ, -1, 1, OuterCut), new KerbCut(SouthernDrive.MaxX, SouthernDrive.MinZ, 1, 1, OuterCut),
        };
        public static ReadOnlyCollection<KerbCut> KerbCuts { get; } = Array.AsReadOnly(kerbCuts);
        // The same cuts as pieces of road, each standing on its slanted kerb face.
        public static ReadOnlyCollection<OrientedRect> Cuts { get; } = Array.AsReadOnly(Array.ConvertAll(kerbCuts, Cut));
        // Pavements beyond the kerbs: 2.5 m on the outer side of the ring, the buildings 1.5 m behind them. On the inner side
        // a strip of pavers reaches the district hedges and the back walls.
        public const float PavementWidth = 2.5f, FrontageGap = 1.5f;
        public const float OuterPavementWestX = WestX - StreetHalfWidth - 2 * KerbHalf - PavementWidth;
        public const float OuterPavementSouthZ = SouthZ - StreetHalfWidth - 2 * KerbHalf - PavementWidth;
        public const float FrontageWestX = OuterPavementWestX - FrontageGap, FrontageSouthZ = OuterPavementSouthZ - FrontageGap;
        // The pavements of the Southern drive and the north side of the highway, and the building lines behind them.
        public const float DrivePavementX = DriveHalfWidth + 2 * KerbHalf + PavementWidth, HighwayPavementZ = HighwayNorthZ + 2 * KerbHalf + PavementWidth;
        public const float FrontageDriveX = DrivePavementX + FrontageGap, FrontageHighwayZ = HighwayPavementZ + FrontageGap;
        // Towers along the south street (fronts north), then along the west and east streets (fronts toward the road); the
        // corners by the boulevard stay open for the grass verges and their trees. The new quarter follows: a tower on each
        // side of the Southern drive, then the glassy row along the north side of the highway (fronts south).
        public static ReadOnlyCollection<RoadsideBuilding> Frontage { get; } = Array.AsReadOnly(BuildFrontage());
        public static ReadOnlyCollection<RoadsideLamp> Lamps { get; } = Array.AsReadOnly(BuildLamps());
        // What the chase camera stays out of: the towers along the ring, the office block, the closed buildings of the
        // district, the towers north of the boulevard and the tunnel mouths.
        public static ReadOnlyCollection<CityRect> Solids { get; } = Array.AsReadOnly(BuildSolids());
        // The whole ring the car may use, with the highway that loops through its tunnels.
        public static DriveArea Ring { get; } = new DriveArea(HighwayLoop, Pieces());
        public static bool IsDrivable(float x, float z) => Ring.Contains(x, z);
        // The part of the way from (x0, z0) to (x1, z1) that keeps `margin` clear of every solid, as a fraction: 1 when
        // nothing stands in the way. A solid round the start is skipped, so the camera never collapses onto the car.
        public static float ClearFraction(float x0, float z0, float x1, float z1, float margin)
        {
            float sum = x0 + z0 + x1 + z1 + margin;
            if (float.IsNaN(sum) || float.IsInfinity(sum)) throw new ArgumentException("Invalid camera ray.");
            float best = 1, dx = x1 - x0, dz = z1 - z0;
            foreach (var solid in Solids)
            {
                float minX = solid.MinX - margin, minZ = solid.MinZ - margin, maxX = solid.MaxX + margin, maxZ = solid.MaxZ + margin;
                if (x0 >= minX && x0 <= maxX && z0 >= minZ && z0 <= maxZ) continue;
                float enter = 0, leave = 1;
                if (Clip(-dx, x0 - minX, ref enter, ref leave) && Clip(dx, maxX - x0, ref enter, ref leave)
                    && Clip(-dz, z0 - minZ, ref enter, ref leave) && Clip(dz, maxZ - z0, ref enter, ref leave))
                    best = Math.Min(best, enter);
            }
            return best;
        }
        // Liang–Barsky: narrows [enter, leave] to where p·t ≤ q; false when nothing is left.
        private static bool Clip(float p, float q, ref float enter, ref float leave)
        {
            if (Math.Abs(p) < 1e-9f) return q >= 0;
            float t = q / p;
            if (p < 0) { if (t > leave) return false; if (t > enter) enter = t; }
            else { if (t < enter) return false; if (t < leave) leave = t; }
            return true;
        }

        // A slanted piece of road on the hypotenuse of a cut, deep enough that its far side stays on the asphalt around the
        // corner.
        private static OrientedRect Cut(KerbCut cut)
        {
            float px = cut.Start.X, pz = cut.Start.Z, qx = cut.End.X, qz = cut.End.Z;
            // OnEdge reaches to the left of P→Q; the road lies away from the off-road quadrant.
            if (-(qz - pz) * -cut.Sx + (qx - px) * -cut.Sz < 0) { (px, qx) = (qx, px); (pz, qz) = (qz, pz); }
            return OrientedRect.OnEdge(px, pz, qx, qz, 0.85f * cut.Leg);
        }
        private static RoadsideBuilding[] BuildFrontage()
        {
            var list = new List<RoadsideBuilding>();
            // Centre along the street, facade length, depth, height, style.
            // The two in the middle make room for the Southern drive: narrower, they keep clear of its corner cuts.
            float[,] south = { { -46.5f, 17, 13, 14, 0 }, { -28, 16, 12, 22, 2 }, { -15, 9, 14, 12, 1 }, { 15, 9, 12, 18, 0 }, { 28, 16, 13, 26, 1 }, { 46.5f, 17, 12, 13, 2 } };
            for (int i = 0; i < south.GetLength(0); i++)
                list.Add(new RoadsideBuilding(south[i, 0], FrontageSouthZ - south[i, 2] / 2, south[i, 1], south[i, 2], south[i, 3], (int)south[i, 4], 180));
            float[,] side = { { -19.5f, 13, 12, 16, 2 }, { -5.5f, 12, 12, 24, 0 }, { 8, 12, 13, 12, 1 } };
            foreach (int sign in new[] { -1, 1 })
                for (int i = 0; i < side.GetLength(0); i++)
                    list.Add(new RoadsideBuilding(sign * (-FrontageWestX + side[i, 2] / 2), side[i, 0], side[i, 1], side[i, 2], side[i, 3] + (sign > 0 ? 2 : 0),
                        ((int)side[i, 4] + (sign > 0 ? 1 : 0)) % 3, sign < 0 ? -90 : 90));
            // The Southern drive: an office tower on each side, fronts toward the drive.
            list.Add(new RoadsideBuilding(-FrontageDriveX - 6.5f, -54, 17, 13, 30, (int)TowerStyle.Glass, -90));
            list.Add(new RoadsideBuilding(FrontageDriveX + 6, -54, 17, 12, 18, (int)TowerStyle.Brick, 90));
            // The highway row from the junction outward, west then east: centre, facade, depth, height, style.
            float[,] row =
            {
                { -19, 15, 12, 22, 1 }, { -35, 15, 13, 32, 1 }, { -51, 15, 12, 16, 2 }, { -67, 14, 13, 26, 1 },
                { 19, 15, 12, 18, 2 }, { 35, 15, 13, 34, 1 }, { 51, 15, 12, 20, 0 }, { 67, 14, 13, 28, 1 },
            };
            for (int i = 0; i < row.GetLength(0); i++)
                list.Add(new RoadsideBuilding(row[i, 0], FrontageHighwayZ + row[i, 2] / 2, row[i, 1], row[i, 2], row[i, 3], (int)row[i, 4], 0));
            return list.ToArray();
        }
        private static RoadsideLamp[] BuildLamps()
        {
            var list = new List<RoadsideLamp>();
            const float offset = 2 * KerbHalf + 0.6f;
            foreach (float z in new[] { -18f, -2f, 14f })
            {
                list.Add(new RoadsideLamp(WestStreet.MinX - offset, z, 90));
                list.Add(new RoadsideLamp(EastStreet.MaxX + offset, z, -90));
            }
            foreach (float x in new[] { -40f, -20f, 20f, 40f }) list.Add(new RoadsideLamp(x, SouthStreet.MinZ - offset, 0));
            // Down the Southern drive, staggered, and along the north pavement of the highway.
            foreach (float z in new[] { -36f, -68f }) list.Add(new RoadsideLamp(SouthernDrive.MinX - offset, z, 90));
            list.Add(new RoadsideLamp(SouthernDrive.MaxX + offset, -52, -90));
            foreach (float x in new[] { -70f, -52f, -34f, -16f, 16f, 34f, 52f, 70f }) list.Add(new RoadsideLamp(x, HighwayNorthZ + offset, 180));
            return list.ToArray();
        }
        private static CityRect[] BuildSolids()
        {
            var list = new List<CityRect>();
            foreach (var building in Frontage) list.Add(building.Footprint);
            list.Add(OfficeBlock);
            list.AddRange(CityDistrictPlan.Footprints);
            foreach (var tower in new CityBlockPlan().Buildings) list.Add(CityRect.Around(tower.X, tower.Z, tower.Width / 2, tower.Depth / 2));
            foreach (int sign in new[] { -1, 1 })
                list.Add(sign < 0 ? new CityRect(-CityScenery.TunnelEndX, CityScenery.TunnelMinZ, -CityScenery.TunnelPortalX, CityScenery.TunnelMaxZ)
                    : new CityRect(CityScenery.TunnelPortalX, CityScenery.TunnelMinZ, CityScenery.TunnelEndX, CityScenery.TunnelMaxZ));
            // The highway runs on into its tunnels; the embankments beside the mouths stop the camera.
            foreach (int sign in new[] { -1, 1 })
                foreach (var (minZ, maxZ) in new[] { (CityScenery.HighwayBankMinZ, CityScenery.HighwayTunnelMinZ), (CityScenery.HighwayTunnelMaxZ, CityScenery.HighwayBankMaxZ) })
                    list.Add(sign < 0 ? new CityRect(-CityScenery.EmbankmentEndX, minZ, -CityScenery.TunnelPortalX, maxZ)
                        : new CityRect(CityScenery.TunnelPortalX, minZ, CityScenery.EmbankmentEndX, maxZ));
            return list.ToArray();
        }
        private static OrientedRect[] Pieces()
        {
            var pieces = new List<OrientedRect>();
            foreach (var rect in Drivable) pieces.Add(OrientedRect.From(rect));
            pieces.AddRange(Cuts);
            return pieces.ToArray();
        }
    }
}
