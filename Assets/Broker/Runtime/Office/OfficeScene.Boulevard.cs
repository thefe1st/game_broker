using System;
using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // The boulevard between the quarters: asphalt and markings, kerbs with railings and the gaps of the crossing, the
    // signals with the push button, the tunnels at both ends and the cars of CityTraffic. Invisible gates in the kerb
    // gaps follow CityLife.GatesOpen; walls along the zebra keep the player off the carriageway.
    public sealed partial class OfficeScene
    {
        private CarRig[] cityCars = new CarRig[0];
        private Collider[] crossingGates = new Collider[0];
        private GameObject[][] carLamps = new GameObject[0][], walkLamps = new GameObject[0][], buttonLamps = new GameObject[0][];
        private TextMesh[] walkCountdowns = new TextMesh[0];
        private int shownSignal = -1;
        private static readonly Color WalkGreen = new Color(0.45f, 0.95f, 0.6f), WaitAmber = new Color(1f, 0.72f, 0.3f);

        private void BuildBoulevard(Material asphalt, Material concrete, Material grass, Material metal)
        {
            const float g = CityBlockPlan.GroundY, south = CityDistrictPlan.SouthKerbZ, north = CityDistrictPlan.NorthKerbZ;
            const float centre = (south + north) / 2, portalX = CityScenery.TunnelPortalX;
            Box("Boulevard / asphalt", new Vector3(0, g + 0.015f, centre), new Vector3(192, 0.03f, north - south + 0.4f), asphalt);
            // Markings: lane dashes, a double centre line, stop lines before the zebra and its stripes.
            var paint = Mat("#E9E4D6");
            const float markY = g + 0.035f;
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { 32f, 38f })
                    for (int dash = 0; dash < 15; dash++)
                        Box("Boulevard / lane dash", new Vector3(side * (7.5f + dash * 6), markY, z), new Vector3(3, 0.01f, 0.12f), paint, false);
                foreach (float dz in new[] { -0.12f, 0.12f })
                    Box("Boulevard / centre line", new Vector3(side * 50.2f, markY, centre + dz), new Vector3(91.6f, 0.01f, 0.1f), paint, false);
            }
            Box("Crossing / eastbound stop line", new Vector3(CityCrosswalk.EastboundStopX - 0.2f, markY, (south + centre) / 2), new Vector3(0.4f, 0.01f, centre - south - 0.3f), paint, false);
            Box("Crossing / westbound stop line", new Vector3(CityCrosswalk.WestboundStopX + 0.2f, markY, (north + centre) / 2), new Vector3(0.4f, 0.01f, north - centre - 0.3f), paint, false);
            for (int stripe = 0; stripe < 12; stripe++)
                Box("Crossing / zebra stripe", new Vector3(0, markY, south + 0.9f + stripe), new Vector3(2 * CityCrosswalk.HalfWidth, 0.01f, 0.5f), paint, false);
            // Kerbs from the crossing to the tunnels; inside the district a railing and an invisible barrier above them. The
            // south kerb opens for the ring streets and the lay-by (CityRoads.SouthKerbCuts), its pieces reaching into the
            // openings under the slanted kerbs of OfficeScene.Roads; west of the crossing its railing and barrier follow
            // the lay-by (BuildBayRailing).
            const float gap = CityCrosswalk.GateHalfWidth, districtEdge = 44.5f;
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Boulevard / kerb", new Vector3(side * (gap + portalX) / 2, g + 0.17f, north), new Vector3(portalX - gap, 0.34f, 0.35f), concrete);
                foreach (float z in side < 0 ? new[] { north } : new[] { south, north })
                {
                    Blocker("Boulevard / kerb barrier", new Vector3(side * (gap + districtEdge) / 2, g + 0.65f, z), new Vector3(districtEdge - gap, 1.3f, 0.35f));
                    for (int post = 0; post <= 12; post++)
                        Box("Boulevard / railing post", new Vector3(side * (3.3f + post * RailPostSpacing), g + 0.8f, z), new Vector3(0.1f, 1.1f, 0.1f), metal, false);
                    Box("Boulevard / railing", new Vector3(side * 23.7f, g + RailHeight, z), new Vector3(40.8f, 0.08f, 0.14f), metal, false);
                }
            }
            var openings = new List<(float min, float max, float join)> { (-gap, gap, 0) };
            foreach (var cut in CityRoads.SouthKerbCuts) openings.Add((cut.MinX, cut.MaxX, KerbJoin));
            openings.Sort((a, b) => a.min.CompareTo(b.min));
            float from = -portalX;
            foreach (var (min, max, join) in openings)
            {
                Box("Boulevard / kerb", new Vector3((from + min + join) / 2, g + 0.17f, south), new Vector3(min + join - from, 0.34f, 0.35f), concrete);
                from = max - join;
            }
            Box("Boulevard / kerb", new Vector3((from + portalX) / 2, g + 0.17f, south), new Vector3(portalX - from, 0.34f, 0.35f), concrete);
            // Gates in the kerb gaps and walls along the zebra.
            crossingGates = new Collider[2];
            for (int i = 0; i < 2; i++)
                crossingGates[i] = Blocker(i == 0 ? "Crossing / south gate" : "Crossing / north gate", new Vector3(0, g + 0.65f, i == 0 ? south : north),
                    new Vector3(2 * CityCrosswalk.GateHalfWidth, 1.3f, 0.3f)).GetComponent<Collider>();
            foreach (float side in new[] { -1f, 1f })
                Blocker("Crossing / corridor wall", new Vector3(side * CityCrosswalk.CorridorX, g + 0.65f, centre), new Vector3(0.3f, 1.3f, north - south));
            BuildSignals();
            BuildTunnels(grass);
            // Grass verges and a row of trees between the district and the tunnels; south of the boulevard they begin past the
            // outer pavements of the ring streets.
            float[] treeX = { 50, 59, 68, 76 };
            const float verge = -CityRoads.OuterPavementWestX, vergeEnd = 80.3f;
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Boulevard / south verge", new Vector3(side * (verge + vergeEnd) / 2, CityBlockPlan.TerrainTopY + 0.01f, 22.2f), new Vector3(vergeEnd - verge, 0.02f, 12.2f), grass, false);
                Box("Boulevard / north verge", new Vector3(side * 62.4f, CityBlockPlan.TerrainTopY + 0.01f, 47.8f), new Vector3(35.8f, 0.02f, 12.6f), grass, false);
                for (int t = 0; t < treeX.Length; t++)
                    foreach (float z in new[] { 25.5f, 45f })
                    {
                        if (z < centre && treeX[t] < verge) continue;
                        int seed = t * 2 + (z > centre ? 1 : 0) + (side > 0 ? 3 : 0);
                        PlaceTree(t % 2 == 1, seed, new Vector3(side * treeX[t], CityBlockPlan.TerrainTopY, z), seed * 37);
                    }
            }
            // Cars: low-poly rigs (OfficeScene.Cars) standing on the asphalt, posed by ShowTraffic.
            var cars = Life.Traffic.Cars;
            cityCars = new CarRig[cars.Count];
            for (int i = 0; i < cars.Count; i++) cityCars[i] = PlaceCar(cars[i].Style, cars[i].Paint, "Boulevard / car " + i, transform);
        }
        // South-west and north-east posts: a car head facing the oncoming lanes, a walk head facing the far kerb, the
        // button on the side of the waiting walkers and a countdown under the walk head.
        private void BuildSignals()
        {
            const float g = CityBlockPlan.GroundY;
            carLamps = new GameObject[2][]; walkLamps = new GameObject[2][]; buttonLamps = new GameObject[2][]; walkCountdowns = new TextMesh[2];
            for (int i = 0; i < 2; i++)
            {
                bool south = i == 0;
                float x = south ? -2.7f : 2.7f, z = south ? CityDistrictPlan.SouthKerbZ : CityDistrictPlan.NorthKerbZ;
                float y = g + (south ? 0 : CityDistrictPlan.NorthFloorY), across = south ? 1 : -1;
                PlaceVoxel("signal post", VoxelProps.SignalPost, "Crossing / signal post", transform, new Vector3(x, y, z));
                carLamps[i] = SignalVariants("car signal", 4, VoxelProps.CarSignalHead, new Vector3(x - across * 0.3f, y + 2.4f, z), south ? 90 : -90);
                walkLamps[i] = SignalVariants("walk signal", 3, VoxelProps.PedSignalHead, new Vector3(x, y + 1.3f, z + across * 0.25f), south ? 180 : 0);
                var button = new Vector3(x, y + 0.85f, z - across * 0.175f);
                buttonLamps[i] = new[]
                {
                    PlaceVoxel("crossing button/idle", () => VoxelProps.CrosswalkButton(false), "Crossing / button", transform, button, south ? 0 : 180),
                    PlaceVoxel("crossing button/waiting", () => VoxelProps.CrosswalkButton(true), "Crossing / button lit", transform, button, south ? 0 : 180),
                };
                var press = Blocker("Crossing / push button", new Vector3(x, y + 1.05f, z - across * 0.25f), new Vector3(0.4f, 0.5f, 0.3f));
                Interactable(press, OfficeStation.CrosswalkButton, "Кнопка перехода", "Попросить зелёный для пешеходов");
                walkCountdowns[i] = Text("Crossing / countdown", "", new Vector3(x, y + 1.15f, z + across * 0.42f), 48, 0.042f, WalkGreen);
                if (south) walkCountdowns[i].transform.localEulerAngles = new Vector3(0, 180, 0);
            }
        }
        // One model per lit state (−1 dark, then the lamps in order); the scene shows one of them.
        private GameObject[] SignalVariants(string key, int count, Func<int, VoxelModel> build, Vector3 position, float yaw)
        {
            var variants = new GameObject[count];
            for (int n = 0; n < count; n++)
            {
                int lit = n - 1;
                variants[n] = PlaceVoxel(key + "/" + lit, () => build(lit), "Crossing / " + key, transform, position, yaw);
            }
            return variants;
        }
        // Portals at both ends, embankments around them and a dark tube with amber lamps where the cars turn round.
        private void BuildTunnels(Material grass)
        {
            const float g = CityBlockPlan.GroundY, portalX = CityScenery.TunnelPortalX, endX = CityScenery.TunnelEndX;
            const float minZ = CityScenery.TunnelMinZ, maxZ = CityScenery.TunnelMaxZ, mouth = CityScenery.TunnelHeight;
            const float bankLength = CityScenery.EmbankmentEndX - portalX, bankTop = CityScenery.EmbankmentHeight - 0.1f;
            var dark = Mat("#17191D"); var lamp = Mat("#F0B45A", true);
            foreach (float side in new[] { -1f, 1f })
            {
                float bank = side * (portalX + CityScenery.EmbankmentEndX) / 2, tube = side * (portalX + endX) / 2;
                PlaceVoxel("tunnel portal", VoxelProps.TunnelPortal, "Boulevard / tunnel portal", transform,
                    new Vector3(side * portalX, g, (CityDistrictPlan.SouthKerbZ + CityDistrictPlan.NorthKerbZ) / 2), side > 0 ? 90 : -90);
                Box("Boulevard / embankment south", new Vector3(bank, g - 0.1f + CityScenery.EmbankmentHeight / 2, (CityScenery.EmbankmentMinZ + minZ) / 2),
                    new Vector3(bankLength, CityScenery.EmbankmentHeight, minZ - CityScenery.EmbankmentMinZ), grass, false);
                Box("Boulevard / embankment north", new Vector3(bank, g - 0.1f + CityScenery.EmbankmentHeight / 2, (maxZ + CityScenery.EmbankmentMaxZ) / 2),
                    new Vector3(bankLength, CityScenery.EmbankmentHeight, CityScenery.EmbankmentMaxZ - maxZ), grass, false);
                Box("Boulevard / embankment over tunnel", new Vector3(bank, g + (mouth + bankTop) / 2, (minZ + maxZ) / 2),
                    new Vector3(bankLength, bankTop - mouth, maxZ - minZ), grass, false);
                Box("Tunnel / south wall", new Vector3(tube, g + mouth / 2, minZ + 0.05f), new Vector3(endX - portalX, mouth, 0.1f), dark, false);
                Box("Tunnel / north wall", new Vector3(tube, g + mouth / 2, maxZ - 0.05f), new Vector3(endX - portalX, mouth, 0.1f), dark, false);
                Box("Tunnel / ceiling", new Vector3(tube, g + mouth - 0.05f, (minZ + maxZ) / 2), new Vector3(endX - portalX, 0.1f, maxZ - minZ), dark, false);
                Box("Tunnel / far end", new Vector3(side * (endX + 0.5f), g + mouth / 2, (minZ + maxZ) / 2), new Vector3(1, mouth, maxZ - minZ), dark, false);
                for (int n = 0; n < 4; n++)
                    Box("Tunnel / lamp", new Vector3(side * (portalX + 2.5f + n * 4), g + mouth - 0.14f, (minZ + maxZ) / 2), new Vector3(1.2f, 0.08f, 0.3f), lamp, false);
            }
        }
        private void ShowTraffic()
        {
            var cars = Life.Traffic.Cars;
            for (int i = 0; i < cityCars.Length && i < cars.Count; i++)
            {
                var car = cars[i]; var rig = cityCars[i];
                rig.Root.localPosition = new Vector3(car.X, CityBlockPlan.GroundY + RoadTop, car.Z);
                rig.Root.localRotation = Quaternion.Euler(0, car.Yaw, 0);
                PoseCar(rig, car.S, car.Braking);
            }
        }
        // Lamps, the button's «waiting» light, the countdown and the gates; models and text change only with the state.
        private void ShowSignals()
        {
            var signal = Life.Signal;
            bool open = Life.GatesOpen;
            foreach (var gate in crossingGates) gate.enabled = !open;
            int car = signal.CarsGreen ? 2 : signal.CarsAmber ? 1 : 0;
            bool blink = signal.PedestrianFlashing && (int)(signal.PhaseElapsed * 4) % 2 == 1;
            int walk = signal.PedestrianGreen ? (blink ? -1 : 1) : 0;
            int seconds = signal.PedestrianGreen ? Mathf.CeilToInt((float)signal.WalkSecondsLeft) : 0;
            int state = ((car * 3 + walk + 1) * 2 + (signal.WalkRequested ? 1 : 0)) * 100 + seconds;
            if (state == shownSignal) return;
            shownSignal = state;
            string count = signal.PedestrianGreen ? seconds.ToString() : signal.WalkRequested ? "ЖДИТЕ" : "";
            for (int i = 0; i < carLamps.Length; i++)
            {
                for (int n = 0; n < carLamps[i].Length; n++) carLamps[i][n].SetActive(n == car + 1);
                for (int n = 0; n < walkLamps[i].Length; n++) walkLamps[i][n].SetActive(n == walk + 1);
                buttonLamps[i][0].SetActive(!signal.WalkRequested); buttonLamps[i][1].SetActive(signal.WalkRequested);
                walkCountdowns[i].text = count; walkCountdowns[i].color = signal.PedestrianGreen ? WalkGreen : WaitAmber;
            }
        }
    }
}
