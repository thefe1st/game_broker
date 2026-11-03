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
                    // A row of narrow office buildings, not one model stretched across 18 metres.
                    string asset = block == 0 ? "building-a" : block == 1 ? "building-b" : "building-d";
                    var building = PlaceLocalModel("KenneyCityCommercial", asset, "District / textured office facade",
                        new Vector3(buildingX, ground, -6), new Vector3(6, 9, 8), transform, true, 180);
                    if (building != null) collider.GetComponent<Renderer>().enabled = false;
                }
                var sign = Text("District / address", x < 0 ? "ЗАПАДНАЯ УЛИЦА / 4" : "ОФИСНАЯ УЛИЦА / 8", new Vector3(x, ground + 3.1f, -1.9f), 48, 0.035f, Color.white);
                sign.transform.localEulerAngles = new Vector3(0, 180, 0);
                // Solid facade: these are not fake clickable doors to unfinished interiors.
                for (int i = 0; i < 4; i++)
                    Box("District / paving seam", new Vector3(x, ground + 0.008f, 1 + i * 2), new Vector3(26, 0.012f, 0.025f), concrete, false);
                foreach (float z in new[] { 10f, 23f })
                {
                    if (PlaceLocalModel("KenneyCityRoads", "light-curved", "District / street lamp", new Vector3(x + 11, ground, z), new Vector3(0.3f, 4.5f, 1.5f), transform, true) == null)
                        Box("District / fallback lamp", new Vector3(x + 11, ground + 2.25f, z), new Vector3(0.12f, 4.5f, 0.12f), navy, false);
                }
                var refuse = Box("District / refuse collider", new Vector3(x + 10.8f, ground + 0.5f, -7), new Vector3(1.2f, 1, 1.6f), concrete);
                if (PlaceLocalModel("KenneyCityRoads", "dumpster", "District / refuse container", new Vector3(x + 10.8f, ground, -7), new Vector3(1.2f, 1, 1.6f), transform, true) != null)
                    refuse.GetComponent<Renderer>().enabled = false;
            }
            var grass = Surface("grass", "#FFFFFF", 2f, 0.03f, 0.15f);
            foreach (float x in new[] { -40f, -23f })
            {
                Box("Park / planted bed", new Vector3(x, ground + 0.15f, 19), new Vector3(2.4f, 0.3f, 5), concrete);
                Box("Park / soil", new Vector3(x, ground + 0.305f, 19), new Vector3(2.2f, 0.015f, 4.8f), grass, false);
                if (PlaceLocalModel("KenneyCityNature", x < -30 ? "tree_oak" : "tree_detailed", "Park / imported tree",
                    new Vector3(x, ground + 0.3f, 19), new Vector3(3, 5.5f, 3), transform, false) == null)
                {
                    Box("Park / tree trunk", new Vector3(x, ground + 1.6f, 19), new Vector3(0.3f, 2.6f, 0.3f), wood, false);
                    Shape(PrimitiveType.Sphere, "Park / tree crown", transform, new Vector3(x, ground + 3.6f, 19), new Vector3(3, 3, 3), grass, false);
                }
            }
            foreach (float x in new[] { -37f, -25f, 27f, 37f })
            {
                Box("District / bench collider", new Vector3(x, ground + 0.4f, 23), new Vector3(2.4f, 0.8f, 0.8f), concrete).GetComponent<Renderer>().enabled = false;
                // Use the already licensed chair/wood shapes; no furniture pack is downloaded at runtime.
                Box("District / bench seat", new Vector3(x, ground + 0.48f, 23), new Vector3(2.4f, 0.12f, 0.7f), wood, false);
                Box("District / bench back", new Vector3(x, ground + 0.85f, 23.3f), new Vector3(2.4f, 0.55f, 0.1f), wood, false);
                foreach (float dx in new[] { -0.9f, 0.9f }) Box("District / bench leg", new Vector3(x + dx, ground + 0.2f, 23), new Vector3(0.12f, 0.4f, 0.6f), navy, false);
            }
            // Separate interactive information stands; hidden errand stations remain unbuilt.
            BuildCityStand(-3, 19, OfficeStation.CityMap, "Карта района", "Выбрать пеший ориентир", "КАРТА РАЙОНА");
            BuildCityStand(4, 19, OfficeStation.CityCompanies, "Стенд компаний", "Посмотреть компании и риски", "КОМПАНИИ / АКЦИИ");
            foreach (var destination in CityDistrictPlan.Destinations)
            {
                if (destination.Id == "office" || destination.Id == "square") continue;
                var label = Text("District / area sign", destination.Name.ToUpperInvariant(), new Vector3(destination.X, ground + 2.5f, 12), 48, 0.04f, Color.white);
                label.transform.localEulerAngles = new Vector3(0, 180, 0);
            }
            PlaceLocalModel("KenneyCityRoads", "traffic-light", "District / traffic signal scenery", new Vector3(40, ground, 31), new Vector3(0.8f, 4, 0.6f), transform, true);
            var service = Box("District / service works blocker", new Vector3(41.5f, ground + 0.6f, -10), new Vector3(2.2f, 1.2f, 0.4f), concrete);
            if (PlaceLocalModel("KenneyCityRoads", "construction-barrier", "District / service works", new Vector3(41.5f, ground, -10), new Vector3(2.2f, 1.2f, 0.4f), transform, true, 90) != null)
                service.GetComponent<Renderer>().enabled = false;
        }
        private void BuildCityStand(float x, float z, OfficeStation station, string title, string action, string label)
        {
            float ground = CityBlockPlan.GroundY;
            var stand = Box("District / " + title, new Vector3(x, ground + 1.5f, z), new Vector3(2.1f, 1.4f, 0.16f), navy);
            Interactable(stand, station, title, action);
            foreach (float dx in new[] { -0.8f, 0.8f }) Box("District / stand post", new Vector3(x + dx, ground + 0.5f, z), new Vector3(0.1f, 1, 0.1f), navy, false);
            Text("District / stand title", label, new Vector3(x, ground + 1.65f, z - 0.1f), 48, 0.026f, Color.white);
        }
    }
}
