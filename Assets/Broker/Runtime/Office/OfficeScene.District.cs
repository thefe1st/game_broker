using System;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // The old district south of the boulevard: closed street facades, the information stands of the plaza with their
    // pictograms, and the dressing of both quarters from CityScenery with the colliders the plan marks as solid: low-poly greenery,
    // lamps, benches, bins, bollards, cones, works and stands (StreetModels), the café's furniture (InteriorModels).
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
                    PlaceBuilding("shop/" + seed, () => BuildingModels.Shop(style, seed, true), BuildingModels.Wall(style), "District / street building",
                        new Vector3(buildingX, ground, -6), 180);
                }
            }
            // Separate interactive information stands; hidden errand stations remain unbuilt.
            BuildCityStand(-3, 19, OfficeStation.CityMap, "Карта района", "Выбрать пеший ориентир", Pictogram.Pin, StandStyle.Map);
            BuildCityStand(4, 19, OfficeStation.CityCompanies, "Стенд компаний", "Посмотреть компании и риски", Pictogram.Chart, StandStyle.Companies);
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
                    case SceneryKind.StreetLamp: PlaceStreetLamp("Scenery / street lamp", position, yaw); break;
                    case SceneryKind.Bin: PlaceBuilding("bin", StreetModels.Bin, StreetModels.FurnitureLook, "Scenery / bin", position, yaw); break;
                    case SceneryKind.Cone: PlaceBuilding("cone", StreetModels.Cone, StreetModels.FurnitureLook, "Scenery / cone", position, yaw); break;
                    case SceneryKind.Bollard: PlaceBuilding("bollard", StreetModels.Bollard, StreetModels.FurnitureLook, "Scenery / bollard", position, yaw); break;
                    case SceneryKind.Planter: PlaceBuilding("planter/" + seed % 3, () => StreetModels.Planter(seed % 3), StreetModels.ShrubLook, "Scenery / planter", position, yaw); break;
                    case SceneryKind.PlazaBench: PlaceBuilding("plaza bench", () => StreetModels.Bench(3, false), StreetModels.FurnitureLook, "Scenery / plaza bench", position, yaw); break;
                    case SceneryKind.Bench: PlaceBuilding("bench", () => StreetModels.Bench(2.4f, true), StreetModels.FurnitureLook, "Scenery / bench", position, yaw); break;
                    case SceneryKind.Dumpster: PlaceBuilding("dumpster", StreetModels.Dumpster, StreetModels.FurnitureLook, "Scenery / refuse container", position, yaw); break;
                    case SceneryKind.WorksBarrier: PlaceBuilding("works barrier", StreetModels.WorksBarrier, StreetModels.FurnitureLook, "Scenery / service works", position, yaw); break;
                    case SceneryKind.FlowerBed: BuildFlowerBed(item, concrete, grass); continue;
                    case SceneryKind.Hedge:
                        // The plan stores the footprint after the yaw; the hedge model runs along its own X.
                        float length = 2 * (Mathf.Abs(Mathf.Abs(yaw % 180) - 90) < 1 ? item.HalfZ : item.HalfX);
                        PlaceBuilding("hedge/" + Mathf.RoundToInt(length * 4) + "/" + seed % 3, () => StreetModels.Hedge(length, seed % 3), StreetModels.HedgeLook, "Scenery / hedge", position, yaw); break;
                    case SceneryKind.Lawn: PlaceBuilding("lawn/" + seed % 2, () => StreetModels.Lawn(seed % 2), StreetModels.LawnLook, "Scenery / lawn", position, yaw); break;
                    case SceneryKind.RostralColumn: PlaceBuilding("rostral column", BuildingModels.RostralColumn, BuildingModels.ExchangeGranite, "Exchange / rostral column", position, yaw); break;
                    case SceneryKind.Kiosk:
                        PlaceBuilding("kiosk", BuildingModels.Kiosk, BuildingModels.KioskWall, "Exchange / press kiosk", position, yaw);
                        PlaceModel("pictogram/newspaper", () => InteriorModels.PictogramSign(Pictogram.Newspaper, 0.26f, VoxelPalette.Cream, VoxelPalette.Navy),
                            "Exchange / kiosk sign", position + new Vector3(0, 2.25f, -item.HalfZ - 0.005f)); break;
                    case SceneryKind.ExchangeColumn: PlaceBuilding("exchange column", BuildingModels.ExchangeColumn, BuildingModels.ExchangeStone, "Exchange / column", position, yaw); break;
                    case SceneryKind.CafeCounter: PlaceBuilding("cafe counter", InteriorModels.CafeCounter, StreetModels.FurnitureLook, "Cafe / counter", position, yaw); break;
                    case SceneryKind.CafeShelf: PlaceBuilding("cafe shelf/" + seed % 2, () => InteriorModels.CafeShelf(seed % 2), StreetModels.FurnitureLook, "Cafe / shelf", position, yaw); break;
                    case SceneryKind.CafeTable: PlaceBuilding("cafe table", InteriorModels.CafeTable, StreetModels.FurnitureLook, "Cafe / table", position, yaw); break;
                    case SceneryKind.CafeChair: PlaceBuilding("cafe chair", InteriorModels.CafeChair, StreetModels.FurnitureLook, "Cafe / chair", position, yaw); break;
                    // Wall-mounted and hanging props: the menu at 1.5 m on the back wall, the lamps from the 3 m ceiling.
                    case SceneryKind.MenuBoard: PlaceBuilding("menu board", InteriorModels.MenuBoard, StreetModels.FurnitureLook, "Cafe / menu board", position + new Vector3(0, 1.5f, 0), yaw); break;
                    case SceneryKind.PendantLamp: PlaceBuilding("pendant lamp", InteriorModels.PendantLamp, StreetModels.FurnitureLook, "Cafe / pendant lamp", position + new Vector3(0, 3, 0), yaw); break;
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
            if (conifer) PlaceBuilding("conifer/" + seed % StreetModels.ConiferKinds, () => StreetModels.Conifer(seed % StreetModels.ConiferKinds), StreetModels.ConiferLook,
                "Scenery / conifer", position, yaw);
            else PlaceBuilding("tree/" + seed % StreetModels.TreeKinds, () => StreetModels.Tree(seed % StreetModels.TreeKinds), StreetModels.TreeLook(seed % StreetModels.TreeKinds),
                "Scenery / tree", position, yaw);
        }
        // Raised bed of the western square: a concrete kerb that blocks walking, soil, a tree and two flowering shrubs.
        private void BuildFlowerBed(SceneryItem item, Material concrete, Material grass)
        {
            float x = item.X, z = item.Z, y = CityBlockPlan.GroundY + item.BaseY; int seed = 21 + item.Seed % 2;
            Box("Park / planted bed", new Vector3(x, y + 0.15f, z), new Vector3(2 * item.HalfX, 0.3f, 2 * item.HalfZ), concrete);
            Box("Park / soil", new Vector3(x, y + 0.305f, z), new Vector3(2 * item.HalfX - 0.2f, 0.015f, 2 * item.HalfZ - 0.2f), grass, false);
            PlaceBuilding("park tree/" + seed, () => StreetModels.Tree(seed), StreetModels.TreeLook(seed), "Park / tree", new Vector3(x, y + 0.3f, z), seed * 40);
            foreach (float dz in new[] { -1.9f, 1.9f })
                PlaceBuilding("bed shrub/" + seed, () => StreetModels.Shrub(2, 1, 1.25f, seed, true), StreetModels.ShrubLook, "Park / shrub", new Vector3(x, y + 0.31f, z + dz),
                    dz > 0 ? 180 : 0);
        }
        private void BuildCityStand(float x, float z, OfficeStation station, string title, string action, Pictogram sign, StandStyle style)
        {
            float ground = CityBlockPlan.GroundY;
            // The box over the panel stays as the interactable collider; the low-poly stand carries the look and its
            // pictogram lies on the plain band of its panel.
            var stand = Box("District / " + title, new Vector3(x, ground + 1.5f, z), new Vector3(2.1f, 1.4f, 0.16f), navy);
            stand.GetComponent<Renderer>().enabled = false;
            Interactable(stand, station, title, action);
            PlaceBuilding("stand/" + style, () => StreetModels.InfoStand(style), StreetModels.FurnitureLook, "District / information stand", new Vector3(x, ground, z));
            PlaceModel("pictogram/" + sign, () => InteriorModels.PictogramSign(sign, 0.4f, VoxelPalette.CarWhite, sign == Pictogram.Pin ? VoxelPalette.Navy : VoxelPalette.Amber),
                "District / stand sign", new Vector3(x, ground + StreetModels.StandLabelY, z + StreetModels.StandFace - 0.004f));
        }
    }
}
