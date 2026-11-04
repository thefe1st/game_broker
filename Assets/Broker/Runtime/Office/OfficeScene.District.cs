using System;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // The old district south of the boulevard: closed street facades, the information stands of the plaza, area signs,
    // and the voxel dressing of both quarters from CityScenery with the colliders the plan marks as solid.
    public sealed partial class OfficeScene
    {
        private void BuildDistrict(Material[] facades)
        {
            const float ground = CityBlockPlan.GroundY;
            foreach (float x in new[] { -30f, 30f })
            {
                for (int block = 0; block < 3; block++)
                {
                    // The eastern corner house is the open café «Бык и медведь»; BuildCafe puts up its walls.
                    if (x > 0 && block == 0) continue;
                    float buildingX = x - 6 + block * 6;
                    Blocker("District / street building", new Vector3(buildingX, ground + 4.5f, -6), new Vector3(6, 9, 8));
                    // Narrow closed buildings; fronts face the walkable street (+Z), windowed backs the ring road. No second café look-alike.
                    ShopStyle style = block == 1 ? ShopStyle.Office : ShopStyle.BrickShop;
                    int seed = block + (x < 0 ? 0 : 3);
                    PlaceVoxel("shop/" + seed, () => VoxelProps.Shop(style, seed, true), "District / voxel street building",
                        transform, new Vector3(buildingX, ground, -6), 180);
                }
                var sign = Text("District / address", x < 0 ? "ЗАПАДНАЯ УЛИЦА / 4" : "ОФИСНАЯ УЛИЦА / 8", new Vector3(x, ground + 3.1f, -1.9f), 48, 0.035f, Color.white);
                sign.transform.localEulerAngles = new Vector3(0, 180, 0);
            }
            // Separate interactive information stands; hidden errand stations remain unbuilt.
            BuildCityStand(-3, 19, OfficeStation.CityMap, "Карта района", "Выбрать пеший ориентир", "КАРТА РАЙОНА", StandStyle.Map);
            BuildCityStand(4, 19, OfficeStation.CityCompanies, "Стенд компаний", "Посмотреть компании и риски", "КОМПАНИИ / АКЦИИ", StandStyle.Companies);
            foreach (string id in new[] { "park", "business" })
            {
                var destination = CityDistrictPlan.Find(id);
                var label = Text("District / area sign", destination.Name.ToUpperInvariant(), new Vector3(destination.X, ground + 2.5f, 12), 48, 0.04f, Color.white);
                label.transform.localEulerAngles = new Vector3(0, 180, 0);
            }
        }
        // Every prop of the plan; solid ones get an invisible collider over their footprint, the café counter is the
        // café's interactable.
        private void BuildScenery(Material concrete, Material grass)
        {
            foreach (var item in CityScenery.Props)
            {
                var position = new Vector3(item.X, CityBlockPlan.GroundY + item.BaseY, item.Z); int seed = item.Seed; float yaw = item.Yaw;
                switch (item.Kind)
                {
                    case SceneryKind.RoundTree: PlaceTree(false, seed, position, yaw); break;
                    case SceneryKind.Conifer: PlaceTree(true, seed, position, yaw); break;
                    case SceneryKind.StreetLamp: PlaceVoxel("street lamp", VoxelProps.StreetLamp, "Scenery / voxel street lamp", transform, position, yaw); break;
                    case SceneryKind.Bin: PlaceVoxel("bin", VoxelProps.Bin, "Scenery / voxel bin", transform, position, yaw); break;
                    case SceneryKind.Cone: PlaceVoxel("cone", VoxelProps.Cone, "Scenery / voxel cone", transform, position, yaw); break;
                    case SceneryKind.Bollard: PlaceVoxel("bollard", VoxelProps.Bollard, "Scenery / voxel bollard", transform, position, yaw); break;
                    case SceneryKind.Planter: PlaceVoxel("planter/" + seed % 3, () => VoxelProps.Planter(seed % 3), "Scenery / voxel planter", transform, position, yaw); break;
                    case SceneryKind.PlazaBench: PlaceVoxel("plaza bench", () => VoxelProps.Bench(30, false), "Scenery / voxel plaza bench", transform, position, yaw); break;
                    case SceneryKind.Bench: PlaceVoxel("bench", () => VoxelProps.Bench(24, true), "Scenery / voxel bench", transform, position, yaw); break;
                    case SceneryKind.Dumpster: PlaceVoxel("dumpster", VoxelProps.Dumpster, "Scenery / voxel refuse container", transform, position, yaw); break;
                    case SceneryKind.WorksBarrier: PlaceVoxel("construction barrier", VoxelProps.ConstructionBarrier, "Scenery / voxel service works", transform, position, yaw); break;
                    case SceneryKind.FlowerBed: BuildFlowerBed(item, concrete, grass); continue;
                    case SceneryKind.Hedge:
                        // The plan stores the footprint after the yaw; the hedge model runs along its own X.
                        float length = 2 * (Mathf.Abs(Mathf.Abs(yaw % 180) - 90) < 1 ? item.HalfZ : item.HalfX);
                        PlaceVoxel("hedge/" + Mathf.RoundToInt(length * 4) + "/" + seed % 3, () => VoxelProps.Hedge(length, seed % 3), "Scenery / voxel hedge", transform, position, yaw); break;
                    case SceneryKind.Lawn: PlaceVoxel("lawn/" + seed % 2, () => VoxelProps.Lawn(seed % 2), "Scenery / voxel lawn", transform, position, yaw); break;
                    case SceneryKind.RostralColumn: PlaceVoxel("rostral column", VoxelProps.RostralColumn, "Exchange / rostral column", transform, position, yaw); break;
                    case SceneryKind.Kiosk:
                        PlaceVoxel("kiosk", VoxelProps.Kiosk, "Exchange / press kiosk", transform, position, yaw);
                        Text("Exchange / kiosk sign", "ПРЕССА", position + new Vector3(0, 2.25f, -item.HalfZ - 0.02f), 48, 0.05f, new Color(0.98f, 0.93f, 0.82f)); break;
                    case SceneryKind.ExchangeColumn: PlaceVoxel("exchange column", VoxelProps.ExchangeColumn, "Exchange / column", transform, position, yaw); break;
                    case SceneryKind.CafeCounter: PlaceVoxel("cafe counter", VoxelProps.CafeCounter, "Cafe / counter", transform, position, yaw); break;
                    case SceneryKind.CafeShelf: PlaceVoxel("cafe shelf/" + seed % 2, () => VoxelProps.CafeShelf(seed % 2), "Cafe / shelf", transform, position, yaw); break;
                    case SceneryKind.CafeTable: PlaceVoxel("cafe table", VoxelProps.CafeTable, "Cafe / table", transform, position, yaw); break;
                    case SceneryKind.CafeChair: PlaceVoxel("cafe chair", VoxelProps.CafeChair, "Cafe / chair", transform, position, yaw); break;
                    // Wall-mounted and hanging props: the menu at 1.5 m on the back wall, the lamps from the 3 m ceiling.
                    case SceneryKind.MenuBoard: PlaceVoxel("menu board", VoxelProps.MenuBoard, "Cafe / menu board", transform, position + new Vector3(0, 1.5f, 0), yaw); break;
                    case SceneryKind.PendantLamp: PlaceVoxel("pendant lamp", VoxelProps.PendantLamp, "Cafe / pendant lamp", transform, position + new Vector3(0, 3, 0), yaw); break;
                    default: throw new ArgumentOutOfRangeException(nameof(item), item.Kind, "Scenery kind without a model.");
                }
                if (!item.Solid) continue;
                float height = SolidHeight(item.Kind);
                var block = Blocker("Scenery / " + item.Kind + " collider", position + new Vector3(0, height / 2, 0), new Vector3(2 * item.HalfX, height, 2 * item.HalfZ));
                if (item.Kind == SceneryKind.CafeCounter)
                    Interactable(block, OfficeStation.CityCafe, "Стойка кофейни «Бык и медведь»", "Послушать, о чём говорят");
            }
        }
        private static float SolidHeight(SceneryKind kind)
        {
            switch (kind)
            {
                case SceneryKind.Planter: return 0.56f;
                case SceneryKind.CafeTable: return 0.75f;
                case SceneryKind.PlazaBench: case SceneryKind.Bench: case SceneryKind.CafeChair: return 0.8f;
                case SceneryKind.Bin: case SceneryKind.Bollard: case SceneryKind.Cone: case SceneryKind.Dumpster: case SceneryKind.Lawn: return 1;
                case SceneryKind.CafeCounter: return 1.05f;
                case SceneryKind.WorksBarrier: return 1.2f;
                case SceneryKind.Kiosk: return 2.7f;
                case SceneryKind.RostralColumn: return 4;
                case SceneryKind.ExchangeColumn: return 7;
                default: return 3;   // tree trunks and lamp posts
            }
        }
        private void PlaceTree(bool conifer, int seed, Vector3 position, float yaw)
        {
            if (conifer) PlaceVoxel("conifer/" + seed % 3, () => VoxelProps.Conifer(seed % 3), "Scenery / voxel conifer", transform, position, yaw);
            else PlaceVoxel("tree/" + seed % 5, () => VoxelProps.RoundTree(seed % 5), "Scenery / voxel tree", transform, position, yaw);
        }
        // Raised bed of the western square: a concrete kerb that blocks walking, soil, a tree and two flowering shrubs.
        private void BuildFlowerBed(SceneryItem item, Material concrete, Material grass)
        {
            float x = item.X, z = item.Z, y = CityBlockPlan.GroundY + item.BaseY; int seed = 21 + item.Seed % 2;
            Box("Park / planted bed", new Vector3(x, y + 0.15f, z), new Vector3(2 * item.HalfX, 0.3f, 2 * item.HalfZ), concrete);
            Box("Park / soil", new Vector3(x, y + 0.305f, z), new Vector3(2 * item.HalfX - 0.2f, 0.015f, 2 * item.HalfZ - 0.2f), grass, false);
            PlaceVoxel("park tree/" + seed, () => VoxelProps.RoundTree(seed), "Park / voxel tree", transform, new Vector3(x, y + 0.3f, z), seed * 40);
            foreach (float dz in new[] { -1.9f, 1.9f })
                PlaceVoxel("bed shrub/" + seed, () => VoxelProps.Shrub(seed, 8, 4, 5, true), "Park / voxel shrub", transform, new Vector3(x, y + 0.31f, z + dz), dz > 0 ? 180 : 0);
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
