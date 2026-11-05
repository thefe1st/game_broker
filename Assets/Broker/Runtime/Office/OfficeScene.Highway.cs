using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // The new quarter and the highway of CityRoads: the Southern drive down from the south street with its pavements, the
    // junction, both carriageways with the concrete median, the markings, the green belt with trees beyond the highway,
    // the give-way sign, and the cars of CityLife.Highway. The towers and the lamps along the new roads come with the
    // ring (BuildRoads), the tunnels with the boulevard's (BuildTunnels).
    public sealed partial class OfficeScene
    {
        private CarRig[] highwayCars = new CarRig[0];

        private void BuildHighway(Material asphalt, Material concrete, Material pavement, Material grass)
        {
            const float g = CityBlockPlan.GroundY, low = CityBlockPlan.TerrainTopY, kerbFace = 2 * CityRoads.KerbHalf;
            const float north = CityRoads.HighwayNorthZ, south = CityRoads.HighwaySouthZ, median = CityRoads.HighwayZ;
            const float end = CityScenery.TunnelEndX, portal = CityScenery.TunnelPortalX, outer = CityRoads.OuterCut, top = CityRoads.DriveCut;
            var drive = CityRoads.SouthernDrive;
            // Ground of the new quarter from the south street's towers to beyond the highway.
            const float groundNorth = -45f;
            Box("Quarter / ground", new Vector3(0, low - 0.25f, (CityScenery.HighwayBankMinZ + groundNorth) / 2),
                new Vector3(200, 0.5f, groundNorth - CityScenery.HighwayBankMinZ), concrete, false);
            // Lawns in the courtyards behind the towers, both sides of the drive.
            foreach (float side in new[] { -1f, 1f })
                Box("Quarter / courtyard lawn", new Vector3(side * 50, low + 0.01f, -54), new Vector3(56, 0.02f, 20), grass, false);
            // Asphalt into the tubes, kerbs on every edge (the corner cuts come with the ring), pavements on the town side.
            Slab("Quarter / Southern drive", drive.MinX, drive.MinZ, drive.MaxX, drive.MaxZ, asphalt);
            Slab("Highway / asphalt", -end, south, end, north, asphalt);
            Kerb("Quarter / drive kerb", drive.MinX, drive.MinZ + outer, drive.MinX, drive.MaxZ - top, -1, 0, KerbJoin, KerbJoin, false, concrete);
            Kerb("Quarter / drive kerb", drive.MaxX, drive.MinZ + outer, drive.MaxX, drive.MaxZ - top, 1, 0, KerbJoin, KerbJoin, false, concrete);
            Kerb("Highway / north kerb", -end, north, drive.MinX - outer, north, 0, 1, 0, KerbJoin, false, concrete);
            Kerb("Highway / north kerb", drive.MaxX + outer, north, end, north, 0, 1, KerbJoin, 0, false, concrete);
            Kerb("Highway / south kerb", -end, south, end, south, 0, -1, 0, 0, false, concrete);
            const float pave = CityRoads.HighwayPavementZ;
            Paving("Highway / north pavement", -portal, north + kerbFace, drive.MinX - kerbFace, pave, pavement);
            Paving("Highway / north pavement", drive.MaxX + kerbFace, north + kerbFace, portal, pave, pavement);
            Paving("Quarter / drive pavement", -CityRoads.DrivePavementX, pave, drive.MinX - kerbFace, CityRoads.OuterPavementSouthZ, pavement);
            Paving("Quarter / drive pavement", drive.MaxX + kerbFace, pave, CityRoads.DrivePavementX, CityRoads.OuterPavementSouthZ, pavement);
            // The median: a low concrete barrier on a wider foot, open at the junction.
            const float gap = CityRoads.MedianGapHalf, road = g + RoadTop;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (gap + end) / 2, length = end - gap;
                Box("Highway / median foot", new Vector3(x, road + 0.15f, median), new Vector3(length, 0.3f, 0.8f), concrete, false);
                Box("Highway / median barrier", new Vector3(x, road + 0.55f, median), new Vector3(length, 0.5f, 0.34f), concrete, false);
            }
            // Markings: lane dashes, solid edge lines by the kerbs and the median, the drive's centre dashes and the give-way
            // line across its southbound lane.
            var paint = Mat("#E9E4D6");
            const float markY = road + 0.005f, line = 0.12f;
            foreach (float z in new[] { (south + median - CityRoads.MedianHalf) / 2, (north + median + CityRoads.MedianHalf) / 2 })
                for (int dash = 0; dash < 27; dash++)
                    Box("Highway / lane dash", new Vector3(-78 + dash * 6, markY, z), new Vector3(3, 0.01f, line), paint, false);
            Box("Highway / edge line", new Vector3(0, markY, south + 0.35f), new Vector3(2 * portal, 0.01f, line), paint, false);
            foreach (float side in new[] { -1f, 1f })
            {
                float from = drive.MaxX + outer, x = side * (from + portal) / 2;
                Box("Highway / edge line", new Vector3(x, markY, north - 0.35f), new Vector3(portal - from, 0.01f, line), paint, false);
                foreach (float z in new[] { median - CityRoads.MedianHalf - 0.3f, median + CityRoads.MedianHalf + 0.3f })
                    Box("Highway / median line", new Vector3(side * (gap + portal) / 2, markY, z), new Vector3(portal - gap, 0.01f, line), paint, false);
            }
            for (int dash = 0; dash < 9; dash++)
                Box("Quarter / centre dash", new Vector3(0, markY, -29.5f - dash * 6), new Vector3(line, 0.01f, 3), paint, false);
            for (int tooth = 0; tooth < 4; tooth++)
                Box("Quarter / give-way mark", new Vector3(drive.MinX + 0.5f + tooth, markY, north + 0.8f), new Vector3(0.5f, 0.01f, 0.5f), paint, false);
            BuildGiveWaySign();
            // Beyond the highway a green belt with a loose row of trees before the far blocks.
            Box("Highway / south verge", new Vector3(0, low + 0.01f, (CityScenery.HighwayBankMinZ + south - kerbFace) / 2),
                new Vector3(2 * portal, 0.02f, south - kerbFace - CityScenery.HighwayBankMinZ), grass, false);
            for (int t = 0; t < 12; t++)
            {
                float x = -66 + t * 12, z = south - 5.5f - (t % 3) * 1.5f; int seed = 30 + t;
                PlaceTree(t % 3 == 1, seed, new Vector3(x, low, z), seed * 41);
            }
            var cars = Life.Highway.Cars;
            highwayCars = new CarRig[cars.Count];
            for (int i = 0; i < cars.Count; i++) highwayCars[i] = PlaceCar(cars[i].Style, cars[i].Paint, "Highway / car " + i, transform);
        }
        // The give-way triangle on a post at the end of the drive, on the driver's right, facing the cars coming down: a
        // red plate with a white field, a pictogram instead of words.
        private void BuildGiveWaySign()
        {
            const float g = CityBlockPlan.GroundY, x = -CityRoads.DriveHalfWidth - 2 * CityRoads.KerbHalf - 0.6f, z = CityRoads.HighwayNorthZ + 2.6f;
            Box("Quarter / give-way post", new Vector3(x, g + 1.15f, z), new Vector3(0.08f, 2.3f, 0.08f), Mat("#4B5157"), false);
            TrianglePlate("Quarter / give-way sign", new Vector3(x, g + 2.05f, z + 0.06f), 0.9f, 0.04f, Mat("#C8372D"), false);
            TrianglePlate("Quarter / give-way field", new Vector3(x, g + 2.05f, z + 0.085f), 0.58f, 0.01f, Mat("#F2F0EA"), false);
            TrianglePlate("Quarter / give-way back", new Vector3(x, g + 2.05f, z + 0.035f), 0.9f, 0.01f, Mat("#8A9096"), true);
        }
        // An equilateral triangle standing on its point, `side` metres across the top, facing +Z (or −Z for the back of a
        // sign).
        private void TrianglePlate(string name, Vector3 centre, float side, float thickness, Material material, bool back)
        {
            float half = side / 2, height = side * 0.866f;
            var outline = new List<Vector2> { new Vector2(-half, height / 3), new Vector2(half, height / 3), new Vector2(0, -2 * height / 3) };
            var mesh = PrismMesh(outline, -thickness / 2, thickness / 2, 1);
            mesh.name = "Broker sign / triangle"; surfaceMeshes.Add(mesh);
            var obj = new GameObject(name); obj.transform.SetParent(transform, false);
            obj.transform.localPosition = centre; obj.transform.localRotation = Quaternion.Euler(-90, back ? 0 : 180, 0);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
