using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // Both quarters around the boulevard. This partial lays the floors and the invisible boundaries, the office tower
    // with its elevator, the towers north of the boulevard and the hazy skyline, and drives the boulevard clock. The old
    // district, the boulevard with its crossing, the Exchange, the café and the passers-by have their own partials.
    public sealed partial class OfficeScene
    {
        private bool previousFog;
        private Color previousFogColor;
        private FogMode previousFogMode;
        private float previousFogStart, previousFogEnd;
        public bool IsInCity { get; private set; }
        public bool WorldMotionEnabled { get; set; } = true;
        // Where the player is on the plan: the car while driving, the walker otherwise.
        public float MapX => Driving ? Life.Car.X : Player == null ? CityBlockPlan.SpawnX : Player.transform.localPosition.x;
        public float MapZ => Driving ? Life.Car.Z : Player == null ? CityBlockPlan.SpawnZ : Player.transform.localPosition.z;
        // Signal, cars and passers-by of the boulevard. Decoration: nothing here touches the day, money or clients.
        public CityLife Life { get; private set; } = new CityLife();
        // Signal state for the HUD while the player walks near the crossing; null elsewhere, in the car and in the office.
        public string CrossingHint => IsInCity && !Driving && CityCrosswalk.Near(MapX, MapZ) ? Life.Hint : null;
        // The button on the signal post: true when the press shortens the cars' green.
        public bool RequestCrossing() { bool changed = Life.Signal.RequestWalk(); ShowCity(0); return changed; }
        // A fresh boulevard clock: the cars' green just started, cars and passers-by at their starting places, the test
        // car parked (a driver steps out beside it).
        public void ResetCity()
        {
            bool seated = Player != null && Player.Seated;
            Life = new CityLife(); chaseSet = false;
            if (seated) Player.Unseat(transform.TransformPoint(ExitPoint()), transform.rotation);
            ShowCity(1);
        }
        public void AdvanceCity(double seconds)
        {
            Life.Input = Driving && Player != null && Player.Seated ? Player.Drive : DriveInput.None;
            Life.Advance(seconds, IsInCity, MapX, MapZ);
            ShowCity((float)seconds);
        }

        private void BuildCity()
        {
            previousFog = RenderSettings.fog; previousFogColor = RenderSettings.fogColor;
            previousFogMode = RenderSettings.fogMode; previousFogStart = RenderSettings.fogStartDistance; previousFogEnd = RenderSettings.fogEndDistance;
            var pavement = Surface("pavers", "#C8C4C0", 2f, 0.1f);
            var asphalt = Surface("asphalt", "#FFFFFF", 2f, 0.04f);
            var concrete = Surface("concrete", "#FFFFFF", 2f, 0.08f);
            var grass = Surface("grass", "#FFFFFF", 2f, 0.03f);
            var metal = Mat("#4B5157");
            var cityLight = Mat("#F3D9A6", true);
            var facades = new[] { Surface("brick", "#FFFFFF", 2f, 0.12f), Surface("concrete", "#D9D2C6", 2f, 0.12f), Surface("plaster", "#FFFFFF", 2f, 0.12f) };
            const float ground = CityBlockPlan.GroundY;
            Box("City / outer meadow", new Vector3(0, ground - 0.45f, CityScenery.TerrainCenterZ), new Vector3(CityScenery.TerrainSize, 0.5f, CityScenery.TerrainSize), grass, false);
            Box("City / terrain", new Vector3(0, CityBlockPlan.TerrainTopY - 0.25f, 55), new Vector3(200, 0.5f, 140), concrete, false);
            // Walkable floors: the plaza and the side streets at ground level, the north pavement and the square 12 cm up.
            Box("City / walkable plaza", new Vector3(0, ground - 0.12f, 18), new Vector3(32, 0.24f, 22), pavement);
            foreach (float x in new[] { -30f, 30f })
            {
                Box("District / promenade wing", new Vector3(x, ground - 0.12f, 18), new Vector3(28, 0.24f, 22), pavement);
                Box("District / side street sidewalk", new Vector3(x, ground - 0.12f, -2.5f), new Vector3(28, 0.24f, 19), pavement);
            }
            const float northTop = ground + CityDistrictPlan.NorthFloorY;
            Box("North / pavement", new Vector3(0, northTop - 0.06f, 42.9f), new Vector3(89, 0.12f, 3.2f), pavement);
            Box("North / Exchange square", new Vector3(0, northTop - 0.06f, 49.65f), new Vector3(32, 0.12f, 10.3f), pavement);
            // Old district: outer walls, the facades of the side streets and the office behind the plaza.
            foreach (float x in new[] { -44.5f, 44.5f }) BoundaryHedge(x, 8, 41, 1, concrete);
            foreach (float x in new[] { -16.5f, 16.5f })
                Box("District / inner boundary", new Vector3(x, ground + 1.5f, -2), new Vector3(0.35f, 3, 19), facades[0]);
            foreach (float x in new[] { -30f, 30f })
                Box("District / south boundary", new Vector3(x, ground + 0.65f, -12), new Vector3(28, 1.3f, 0.35f), concrete);
            Box("Plaza / rear boundary", new Vector3(0, ground + 1.5f, 7.5f), new Vector3(31, 3, 0.4f), facades[0]);
            // North side: walls at the ends of the pavement, invisible lines along the tower fronts and around the square.
            foreach (float x in new[] { -44.5f, 44.5f }) BoundaryHedge(x, 42.9f, 3.2f, 2, concrete);
            foreach (float x in new[] { -30.25f, 30.25f })
                Blocker("North / tower line", new Vector3(x, ground + 0.75f, 44.65f), new Vector3(28.5f, 1.5f, 0.3f));
            foreach (float x in new[] { -16.15f, 16.15f })
                Blocker("North / square side", new Vector3(x, ground + 0.75f, 49.65f), new Vector3(0.3f, 1.5f, 10.3f));
            foreach (float x in new[] { -14.3f, 14.3f })
                Blocker("North / square back", new Vector3(x, ground + 0.75f, 54.95f), new Vector3(3.4f, 1.5f, 0.3f));
            // The office reads as the top floor of a building instead of floating above the plaza; its back has windows
            // too, for the south street of the ring.
            PlaceVoxel("office block", () => VoxelProps.Tower(31, 19.5f, 7.5f, TowerStyle.Plaster, 5, false, false, true),
                "City / office building", transform, new Vector3(0, ground, -2.35f), 180);
            var plan = new CityBlockPlan();
            int number = 0;
            foreach (var b in plan.Buildings)
            {
                var building = b; int seed = number;
                PlaceVoxel("tower/" + number, () => VoxelProps.Tower(building.Width, building.Depth, building.Height, (TowerStyle)building.Style, seed),
                    "City / voxel tower " + number, transform, new Vector3(b.X, CityBlockPlan.TerrainTopY, b.Z));
                number++;
            }
            BuildDistrict(facades);
            BuildBoulevard(asphalt, concrete, grass, metal);
            BuildRoads(asphalt, concrete, pavement, grass, metal);
            BuildPlayerCar();
            BuildExchange();
            BuildCafe();
            BuildScenery(concrete, grass);
            BuildCrowd();
            BuildSkyline();
            // Elevator portals are explicit prototype transitions; the end-day door stays separate.
            var entry = Box("City / office elevator", new Vector3(0, ground + 1.35f, 8.2f), new Vector3(2.5f, 2.7f, 0.35f), navy);
            Interactable(entry, OfficeStation.ReturnOffice, "Лифт в офис", "Вернуться в Мойка Капитал");
            Box("Elevator / city indicator", new Vector3(0, ground + 2.95f, 8.12f), new Vector3(2.5f, 0.12f, 0.2f), cityLight, false);
            var sign = Text("City / elevator sign", "ОФИС", new Vector3(0, ground + 2.05f, 8.43f), 48, 0.048f, new Color(0.98f, 0.93f, 0.82f));
            sign.transform.localEulerAngles = new Vector3(0, 180, 0);
            var portal = Box("Office / street elevator", new Vector3(5.85f, 1.3f, -0.2f), new Vector3(0.14f, 2.6f, 1.3f), navy);
            Interactable(portal, OfficeStation.Street, "Лифт в квартал", "Выйти на площадь");
            Box("Office / elevator light", new Vector3(5.74f, 2.5f, -0.2f), new Vector3(0.05f, 0.06f, 1.15f), cityLight, false);
            var label = Text("Office / street sign", "КВАРТАЛ", new Vector3(5.72f, 1.8f, -0.2f), 48, 0.03f, new Color(0.98f, 0.93f, 0.82f));
            label.transform.localEulerAngles = new Vector3(0, 90, 0);
            ShowCity(1);
        }
        // The east and west ends of the walkable streets: a clipped hedge on a stone kerb instead of a bare wall.
        private void BoundaryHedge(float x, float z, float length, int seed, Material concrete)
        {
            const float ground = CityBlockPlan.GroundY;
            Blocker("District / boundary", new Vector3(x, ground + 0.7f, z), new Vector3(0.7f, 1.4f, length));
            Box("District / boundary kerb", new Vector3(x, ground + 0.06f, z), new Vector3(0.7f, 0.12f, length), concrete, false);
            PlaceVoxel("hedge/" + Mathf.RoundToInt(length * 4) + "/" + seed, () => VoxelProps.Hedge(length, seed), "District / boundary hedge",
                transform, new Vector3(x, ground + 0.12f, z), 90);
        }
        // Invisible collider for walls the voxel models already show, or for limits that need no wall.
        private GameObject Blocker(string name, Vector3 position, Vector3 size)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false);
            obj.transform.localPosition = position; obj.transform.localScale = size;
            obj.AddComponent<BoxCollider>(); return obj;
        }
        private void PointLight(string name, Vector3 position, float range, float intensity, Color color)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false); obj.transform.localPosition = position;
            var point = obj.AddComponent<Light>(); point.type = LightType.Point; point.range = range; point.intensity = intensity; point.color = color;
        }
        // Hazy boxy skyline beyond the playable block: cheap textured boxes that the fog dissolves.
        private void BuildSkyline()
        {
            var shades = new[] { Surface("windows", "#8C8076", VoxelSurfaces.TileMetres("windows"), 0.05f),
                Surface("windows", "#7E8084", VoxelSurfaces.TileMetres("windows"), 0.05f), Surface("windows", "#927A66", VoxelSurfaces.TileMetres("windows"), 0.05f) };
            foreach (var block in CityScenery.Skyline)
            {
                var box = Box("City / distant block", new Vector3(block.X, CityBlockPlan.GroundY - 0.2f + block.Height / 2, block.Z),
                    new Vector3(block.Width, block.Height, block.Depth), shades[block.Shade], false);
                box.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        public void VisitCity(bool outside)
        {
            IsInCity = outside; ParkPlayerCar();
            Player.ResetAt(transform.TransformPoint(outside
                ? new Vector3(CityBlockPlan.SpawnX, CityBlockPlan.SpawnY, CityBlockPlan.SpawnZ)
                : new Vector3(4.5f, 0.05f, -0.2f)));
        }
        private void Update()
        {
            if (WorldMotionEnabled) AdvanceCity(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }
        // Moves the cars, the passers-by and the test car to the state of CityLife and lights the signals; dt only smooths
        // turning and the chase camera.
        private void ShowCity(float dt)
        {
            ShowTraffic(); ShowSignals(); ShowCrowd(dt); ShowPlayerCar(dt);
        }
        private void RestoreCityEnvironment()
        {
            RenderSettings.fog = previousFog; RenderSettings.fogColor = previousFogColor; RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogStartDistance = previousFogStart; RenderSettings.fogEndDistance = previousFogEnd;
        }
    }
}
