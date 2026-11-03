using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        private void BuildDistrict(Material concrete, Material[] facades)
        {
            float ground = CityBlockPlan.GroundY;
            foreach (float x in new[] { -30f, 30f })
            {
                for (int block = 0; block < 3; block++)
                {
                    float buildingX = x - 6 + block * 6;
                    var collider = Box("District / office building collider", new Vector3(buildingX, ground + 4.5f, -6), new Vector3(6, 9, 8), facades[0]);
                    collider.GetComponent<Renderer>().enabled = false;
                    // A row of narrow street buildings; fronts and awnings face the walkable street (+Z).
                    ShopStyle style = block == 1 ? ShopStyle.Office : (block == 0) == (x < 0) ? ShopStyle.BrickShop : ShopStyle.Cafe;
                    int seed = block + (x < 0 ? 0 : 3);
                    PlaceVoxel("shop/" + seed, () => VoxelProps.Shop(style, seed), "District / voxel street building",
                        transform, new Vector3(buildingX, ground, -6), 180);
                }
                var sign = Text("District / address", x < 0 ? "ЗАПАДНАЯ УЛИЦА / 4" : "ОФИСНАЯ УЛИЦА / 8", new Vector3(x, ground + 3.1f, -1.9f), 48, 0.035f, Color.white);
                sign.transform.localEulerAngles = new Vector3(0, 180, 0);
                // Solid facade: these are not fake clickable doors to unfinished interiors.
                foreach (float z in new[] { 10f, 23f })
                    PlaceVoxel("street lamp", VoxelProps.StreetLamp, "District / voxel street lamp", transform, new Vector3(x + 11, ground, z), -90);
                Box("District / refuse collider", new Vector3(x + 10.8f, ground + 0.5f, -7), new Vector3(1.2f, 1, 1.6f), concrete).GetComponent<Renderer>().enabled = false;
                PlaceVoxel("dumpster", VoxelProps.Dumpster, "District / voxel refuse container", transform, new Vector3(x + 10.8f, ground, -7));
            }
            var grass = Surface("grass", "#FFFFFF", 2f, 0.03f);
            foreach (float x in new[] { -40f, -23f })
            {
                Box("Park / planted bed", new Vector3(x, ground + 0.15f, 19), new Vector3(2.4f, 0.3f, 5), concrete);
                Box("Park / soil", new Vector3(x, ground + 0.305f, 19), new Vector3(2.2f, 0.015f, 4.8f), grass, false);
                int seed = x < -30 ? 21 : 22;
                PlaceVoxel("park tree/" + seed, () => VoxelProps.RoundTree(seed), "Park / voxel tree", transform, new Vector3(x, ground + 0.3f, 19), seed * 40);
                foreach (float z in new[] { 17.1f, 20.9f })
                    PlaceVoxel("bed shrub/" + seed, () => VoxelProps.Shrub(seed, 8, 4, 5, true), "Park / voxel shrub", transform, new Vector3(x, ground + 0.31f, z), z > 19 ? 180 : 0);
            }
            foreach (float x in new[] { -37f, -25f, 27f, 37f })
            {
                Box("District / bench collider", new Vector3(x, ground + 0.4f, 23), new Vector3(2.4f, 0.8f, 0.8f), concrete).GetComponent<Renderer>().enabled = false;
                PlaceVoxel("bench", () => VoxelProps.Bench(24, true), "District / voxel bench", transform, new Vector3(x, ground, 23));
            }
            // Separate interactive information stands; hidden errand stations remain unbuilt.
            BuildCityStand(-3, 19, OfficeStation.CityMap, "Карта района", "Выбрать пеший ориентир", "КАРТА РАЙОНА", StandStyle.Map);
            BuildCityStand(4, 19, OfficeStation.CityCompanies, "Стенд компаний", "Посмотреть компании и риски", "КОМПАНИИ / АКЦИИ", StandStyle.Companies);
            foreach (var destination in CityDistrictPlan.Destinations)
            {
                if (destination.Id == "office" || destination.Id == "square") continue;
                var label = Text("District / area sign", destination.Name.ToUpperInvariant(), new Vector3(destination.X, ground + 2.5f, 12), 48, 0.04f, Color.white);
                label.transform.localEulerAngles = new Vector3(0, 180, 0);
            }
            PlaceVoxel("traffic light", VoxelProps.TrafficLight, "District / voxel traffic signal", transform, new Vector3(40, ground, 31));
            Box("District / service works blocker", new Vector3(41.5f, ground + 0.6f, -10), new Vector3(2.2f, 1.2f, 0.4f), concrete).GetComponent<Renderer>().enabled = false;
            PlaceVoxel("construction barrier", VoxelProps.ConstructionBarrier, "District / voxel service works", transform, new Vector3(41.5f, ground, -10));
            BuildScenery(concrete);
        }
        // Trees, lamps and small street furniture from the shared scenery plan; trunks and posts get thin colliders.
        private void BuildScenery(Material concrete)
        {
            foreach (var item in CityScenery.Props)
            {
                bool far = item.Z > 41;
                float y = far ? (item.Kind == SceneryKind.StreetLamp ? CityBlockPlan.GroundY + 0.12f : CityBlockPlan.TerrainTopY) : CityBlockPlan.GroundY;
                var position = new Vector3(item.X, y, item.Z); int seed = item.Seed;
                switch (item.Kind)
                {
                    case SceneryKind.RoundTree: PlaceVoxel("tree/" + seed % 5, () => VoxelProps.RoundTree(seed % 5), "Scenery / voxel tree", transform, position, item.Yaw); break;
                    case SceneryKind.Conifer: PlaceVoxel("conifer/" + seed % 3, () => VoxelProps.Conifer(seed % 3), "Scenery / voxel conifer", transform, position, item.Yaw); break;
                    case SceneryKind.StreetLamp: PlaceVoxel("street lamp", VoxelProps.StreetLamp, "Scenery / voxel street lamp", transform, position, item.Yaw); break;
                    case SceneryKind.Bin: PlaceVoxel("bin", VoxelProps.Bin, "Scenery / voxel bin", transform, position, item.Yaw); break;
                    case SceneryKind.Cone: PlaceVoxel("cone", VoxelProps.Cone, "Scenery / voxel cone", transform, position, item.Yaw); break;
                    default: PlaceVoxel("bollard", VoxelProps.Bollard, "Scenery / voxel bollard", transform, position, item.Yaw); break;
                }
                if (item.Solid)
                {
                    var trunk = Box("Scenery / trunk collider", position + new Vector3(0, 1.5f, 0), new Vector3(0.5f, 3, 0.5f), concrete);
                    trunk.GetComponent<Renderer>().enabled = false;
                }
            }
        }
        private void BuildCityStand(float x, float z, OfficeStation station, string title, string action, string label, StandStyle style)
        {
            float ground = CityBlockPlan.GroundY;
            // The box stays as the interactable collider; the voxel board carries the look.
            var stand = Box("District / " + title, new Vector3(x, ground + 1.5f, z), new Vector3(2.1f, 1.4f, 0.16f), navy);
            stand.GetComponent<Renderer>().enabled = false;
            Interactable(stand, station, title, action);
            PlaceVoxel("stand/" + style, () => VoxelProps.InfoStand(style), "District / voxel board", transform, new Vector3(x, ground, z));
            Text("District / stand title", label, new Vector3(x, ground + 1.65f, z - 0.15f), 48, 0.026f, Color.white);
        }
    }
}
