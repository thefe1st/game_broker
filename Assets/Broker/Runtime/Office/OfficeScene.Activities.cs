using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        private TextMesh cityBureauLabel, cityPickupLabel;

        private void BuildCityActivities(Material concrete, Material light)
        {
            float g = CityBlockPlan.GroundY;
            // Both fronts face the walkable plaza; no booth is inside the lift landing or a planter.
            var bureau = Box("City / neighbours bureau", new Vector3(4, g + 0.5f, 10.5f), new Vector3(2.6f, 1f, 0.65f), navy);
            Interactable(bureau, OfficeStation.CityContact, "Бюро квартала / Вера", "Выбрать дело или сдать заказ");
            Box("Bureau / accent", new Vector3(4, g + 1.06f, 10.5f), new Vector3(2.8f, 0.12f, 0.8f), light, false);
            Box("Bureau / sign backing", new Vector3(4, g + 2.45f, 9.45f), new Vector3(2.8f, 0.75f, 0.12f), navy, false);
            cityBureauLabel = Text("Bureau / sign", "БЮРО КВАРТАЛА", new Vector3(4, g + 2.45f, 9.53f), 48, 0.026f, new Color(0.83f, 0.96f, 0.96f));
            cityBureauLabel.transform.localEulerAngles = new Vector3(0, 180, 0);
            // A small low-poly coordinator behind the counter.
            Shape(PrimitiveType.Cube, "Vera / suit", transform, new Vector3(4, g + 1.2f, 9.8f), new Vector3(0.5f, 0.72f, 0.32f), green, false);
            Shape(PrimitiveType.Sphere, "Vera / head", transform, new Vector3(4, g + 1.78f, 9.8f), new Vector3(0.34f, 0.4f, 0.34f), skin, false);
            Shape(PrimitiveType.Cube, "Vera / hair", transform, new Vector3(4, g + 1.97f, 9.8f), new Vector3(0.36f, 0.16f, 0.36f), ink, false);
            foreach (float side in new[] { -1f, 1f })
            {
                Shape(PrimitiveType.Cube, "Vera / leg", transform, new Vector3(4 + side * 0.13f, g + 0.42f, 9.8f), new Vector3(0.18f, 0.84f, 0.24f), navy, false);
                Shape(PrimitiveType.Cube, "Vera / sleeve", transform, new Vector3(4 + side * 0.34f, g + 1.2f, 9.8f), new Vector3(0.15f, 0.55f, 0.22f), green, false);
            }
            var kiosk = Box("City / print and lunch pavilion", new Vector3(-11, g + 1.15f, 22), new Vector3(3.2f, 2.3f, 1.5f), concrete);
            Interactable(kiosk, OfficeStation.CityPickup, "Павильон / Печать и обеды", "Забрать подготовленный заказ");
            Box("Kiosk / canopy", new Vector3(-11, g + 2.4f, 21.85f), new Vector3(3.6f, 0.2f, 2), green, false);
            cityPickupLabel = Text("Kiosk / sign", "ПЕЧАТЬ / ОБЕДЫ", new Vector3(-11, g + 1.9f, 21.22f), 48, 0.024f, new Color(0.83f, 0.96f, 0.96f));
            Box("Kiosk / service window", new Vector3(-11, g + 1.1f, 21.22f), new Vector3(2.4f, 0.7f, 0.04f), navy, false);
        }
        private void RefreshCityActivities(BrokerDay day)
        {
            if (cityBureauLabel != null) cityBureauLabel.text = day.CityStage == CityWorkStage.Carrying
                ? "БЮРО / СДАТЬ ЗАКАЗ" : day.CityStage == CityWorkStage.Completed ? "БЮРО / ДО ЗАВТРА" : "БЮРО КВАРТАЛА";
            if (cityPickupLabel != null) cityPickupLabel.text = day.CityStage == CityWorkStage.ReadyForPickup
                ? "ЗАКАЗ ГОТОВ / E" : "ПЕЧАТЬ / ОБЕДЫ";
        }
    }
}
