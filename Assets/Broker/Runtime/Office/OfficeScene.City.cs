using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        private readonly Transform[] cityCars = new Transform[4];
        private double cityElapsed;
        private bool previousFog;
        private Color previousFogColor;
        private FogMode previousFogMode;
        private float previousFogStart, previousFogEnd;
        public bool IsInCity { get; private set; }
        public bool WorldMotionEnabled { get; set; } = true;
        public float MapX => Player == null ? CityBlockPlan.SpawnX : Player.transform.localPosition.x;
        public float MapZ => Player == null ? CityBlockPlan.SpawnZ : Player.transform.localPosition.z;

        private void BuildCity()
        {
            previousFog = RenderSettings.fog; previousFogColor = RenderSettings.fogColor;
            previousFogMode = RenderSettings.fogMode; previousFogStart = RenderSettings.fogStartDistance; previousFogEnd = RenderSettings.fogEndDistance;
            var pavement = Surface("pavers", "#FFFFFF", 2f, 0.1f, 0);
            var asphalt = Surface("asphalt", "#FFFFFF", 2f, 0.04f, 0);
            var concrete = Surface("concrete", "#FFFFFF", 2f, 0.08f, 0);
            var grass = Surface("grass", "#FFFFFF", 2f, 0.03f, 0);
            var metal = Mat("#4B5157");
            var cityLight = Mat("#F3D9A6", true);
            var facades = new[] { Surface("brick", "#FFFFFF", 2f, 0.12f, 0), Surface("concrete", "#D9D2C6", 2f, 0.12f, 0), Surface("plaster", "#FFFFFF", 2f, 0.12f, 0) };
            const float ground = CityBlockPlan.GroundY;
            Box("City / outer meadow", new Vector3(0, ground - 0.45f, CityScenery.TerrainCenterZ), new Vector3(CityScenery.TerrainSize, 0.5f, CityScenery.TerrainSize), grass, false);
            Box("City / terrain", new Vector3(0, CityBlockPlan.TerrainTopY - 0.25f, 59), new Vector3(120, 0.5f, 112), concrete, false);
            Box("City / walkable plaza", new Vector3(0, ground - 0.12f, 18), new Vector3(32, 0.24f, 22), pavement);
            foreach (float x in new[] { -30f, 30f })
            {
                Box("District / promenade wing", new Vector3(x, ground - 0.12f, 18), new Vector3(28, 0.24f, 22), pavement);
                Box("District / side street sidewalk", new Vector3(x, ground - 0.12f, -2.5f), new Vector3(28, 0.24f, 19), pavement);
            }
            Box("City / boulevard", new Vector3(0, ground + 0.015f, 35), new Vector3(112, 0.03f, 12), asphalt, false);
            Box("City / far sidewalk", new Vector3(0, ground + 0.06f, 42.6f), new Vector3(112, 0.12f, 3.2f), pavement, false);
            for (int lane = 0; lane < 3; lane++) for (int mark = 0; mark < 14; mark++)
                Box("Road / lane marking", new Vector3(-47 + mark * 7, ground + 0.04f, 32 + lane * 3), new Vector3(2.4f, 0.02f, 0.09f), cream, false);
            // Pedestrian wings share a floor; moving traffic remains behind a visible barrier.
            foreach (float x in new[] { -44.5f, 44.5f })
                Box("District / outer boundary", new Vector3(x, ground + 0.65f, 8), new Vector3(0.35f, 1.3f, 41), concrete);
            foreach (float x in new[] { -16.5f, 16.5f })
                Box("District / inner boundary", new Vector3(x, ground + 1.5f, -2), new Vector3(0.35f, 3, 19), facades[0]);
            foreach (float x in new[] { -30f, 30f })
                Box("District / south boundary", new Vector3(x, ground + 0.65f, -12), new Vector3(28, 1.3f, 0.35f), concrete);
            var trafficBarrier = Box("Plaza / safety barrier", new Vector3(0, ground + 0.65f, 28.5f), new Vector3(89, 1.3f, 0.35f), concrete);
            trafficBarrier.GetComponent<Renderer>().enabled = false;
            Box("Plaza / boulevard curb", new Vector3(0, ground + 0.17f, 28.5f), new Vector3(89, 0.34f, 0.35f), concrete);
            for (int post = 0; post < 25; post++)
                Box("Plaza / railing post", new Vector3(-44 + post * 3.67f, ground + 0.8f, 28.5f), new Vector3(0.1f, 1.1f, 0.1f), metal, false);
            Box("Plaza / railing", new Vector3(0, ground + 1.33f, 28.5f), new Vector3(89, 0.08f, 0.14f), metal, false);
            Box("Plaza / rear boundary", new Vector3(0, ground + 1.5f, 7.5f), new Vector3(31, 3, 0.4f), facades[0]);
            // The office reads as the top floor of a building instead of floating above the plaza.
            PlaceVoxel("office block", () => VoxelProps.Tower(31, 19.5f, 7.5f, TowerStyle.Plaster, 5, false, false),
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
            for (int i = 0; i < 6; i++)
            {
                float x = -12.5f + i * 5; int seed = i % 3;
                Box("Plaza / planter", new Vector3(x, ground + 0.28f, 25.5f), new Vector3(2.2f, 0.56f, 1.25f), concrete).GetComponent<Renderer>().enabled = false;
                PlaceVoxel("planter/" + seed, () => VoxelProps.Planter(seed), "Plaza / voxel planter", transform, new Vector3(x, ground, 25.5f));
                PlaceVoxel("street lamp", VoxelProps.StreetLamp, "Street / voxel lamp", transform, new Vector3(x, ground, 27.9f));
            }
            foreach (float x in new[] { -9f, 9f })
            {
                Box("Plaza / bench seat", new Vector3(x, ground + 0.48f, 17), new Vector3(3, 0.16f, 0.75f), wood).GetComponent<Renderer>().enabled = false;
                Box("Plaza / bench base", new Vector3(x, ground + 0.2f, 17), new Vector3(2.4f, 0.4f, 0.5f), concrete).GetComponent<Renderer>().enabled = false;
                PlaceVoxel("plaza bench", () => VoxelProps.Bench(30, false), "Plaza / voxel bench", transform, new Vector3(x, ground, 17));
            }
            BuildDistrict(concrete, facades);
            BuildSkyline();
            // City errands are temporarily hidden by the approved career plan. Builder retained.
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
            for (int lane = 0; lane < cityCars.Length; lane++)
            {
                var car = new GameObject("City / visual traffic " + lane); car.transform.SetParent(transform, false); cityCars[lane] = car.transform;
                CarStyle style = lane == 1 ? CarStyle.Suv : lane == 2 ? CarStyle.Taxi : CarStyle.Sedan; int seed = lane * 3 + 1;
                // The car root rides 0.48 m above the road; the voxel body's wheels touch the asphalt.
                PlaceVoxel("car/" + lane, () => VoxelProps.Car(style, seed), "Car / voxel body", car.transform, new Vector3(0, -0.45f, 0));
                car.transform.localRotation = Quaternion.Euler(0, lane % 2 == 0 ? 0 : 180, 0);
            }
        }
        // Hazy boxy skyline beyond the playable block: cheap textured boxes that the fog dissolves.
        private void BuildSkyline()
        {
            var shades = new[] { Surface("windows", "#E8DED2", VoxelSurfaces.TileMetres("windows"), 0.05f, 0),
                Surface("windows", "#C9CCD0", VoxelSurfaces.TileMetres("windows"), 0.05f, 0), Surface("windows", "#E0C9B2", VoxelSurfaces.TileMetres("windows"), 0.05f, 0) };
            foreach (var block in CityScenery.Skyline)
            {
                var box = Box("City / distant block", new Vector3(block.X, CityBlockPlan.GroundY - 0.2f + block.Height / 2, block.Z),
                    new Vector3(block.Width, block.Height, block.Depth), shades[block.Shade], false);
                box.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        public void VisitCity(bool outside)
        {
            IsInCity = outside;
            Player.ResetAt(transform.TransformPoint(outside
                ? new Vector3(CityBlockPlan.SpawnX, CityBlockPlan.SpawnY, CityBlockPlan.SpawnZ)
                : new Vector3(4.5f, 0.05f, -0.2f)));
        }
        private void Update()
        {
            if (WorldMotionEnabled) cityElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            for (int lane = 0; lane < cityCars.Length; lane++) if (cityCars[lane] != null)
                cityCars[lane].localPosition = new Vector3(CityBlockPlan.TrafficX(cityElapsed, lane), CityBlockPlan.GroundY + 0.48f, 30.5f + lane * 2.9f);
        }
        private void RestoreCityEnvironment()
        {
            RenderSettings.fog = previousFog; RenderSettings.fogColor = previousFogColor; RenderSettings.fogMode = previousFogMode;
            RenderSettings.fogStartDistance = previousFogStart; RenderSettings.fogEndDistance = previousFogEnd;
        }
    }
}
