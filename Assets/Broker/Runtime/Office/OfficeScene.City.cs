using Broker.Core;
using UnityEngine;

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

        private void BuildCity()
        {
            var pavement = Surface("concrete", "#B9C7D0", 2f, 0.12f, 0.3f);
            var asphalt = Surface("asphalt", "#8998A8", 3f, 0.04f, 0.3f);
            var concrete = Surface("concrete", "#8D9DAF", 2f, 0.08f, 0.25f);
            var cityLight = Mat("#A5DAD5", true); var warmLight = Mat("#EBCB99", true);
            var facades = new[] { Mat("#2C4054"), Mat("#455061"), Mat("#344C58") };
            const float ground = CityBlockPlan.GroundY;
            Box("City / terrain", new Vector3(0, CityBlockPlan.TerrainTopY - 0.25f, 59), new Vector3(120, 0.5f, 112), concrete, false);
            Box("City / walkable plaza", new Vector3(0, ground - 0.12f, 18), new Vector3(32, 0.24f, 22), pavement);
            for (int i = 0; i < 9; i++)
                Box("Plaza / paving joint", new Vector3(-14 + i * 3.5f, ground + 0.008f, 18), new Vector3(0.025f, 0.012f, 20), concrete, false);
            Box("City / boulevard", new Vector3(0, ground + 0.015f, 35), new Vector3(112, 0.03f, 12), asphalt, false);
            for (int lane = 0; lane < 3; lane++) for (int mark = 0; mark < 14; mark++)
                Box("Road / lane marking", new Vector3(-47 + mark * 7, ground + 0.04f, 32 + lane * 3), new Vector3(2.4f, 0.02f, 0.09f), cream, false);
            // The plaza is explorable; traffic and buildings are scenery beyond a visible railing.
            foreach (float x in new[] { -15.5f, 15.5f })
                Box("Plaza / side barrier", new Vector3(x, ground + 0.65f, 18), new Vector3(0.35f, 1.3f, 22), concrete);
            var trafficBarrier = Box("Plaza / safety barrier", new Vector3(0, ground + 0.65f, 28.5f), new Vector3(31, 1.3f, 0.35f), concrete);
            trafficBarrier.GetComponent<Renderer>().enabled = false;
            Box("Plaza / boulevard curb", new Vector3(0, ground + 0.17f, 28.5f), new Vector3(31, 0.34f, 0.35f), concrete);
            for (int post = 0; post < 9; post++)
                Box("Plaza / railing post", new Vector3(-15 + post * 3.75f, ground + 0.8f, 28.5f), new Vector3(0.09f, 1.1f, 0.12f), concrete, false);
            Box("Plaza / rear boundary", new Vector3(0, ground + 1.5f, 7.5f), new Vector3(31, 3, 0.4f), facades[0]);
            Box("Plaza / light rail", new Vector3(0, ground + 1.33f, 28.5f), new Vector3(31, 0.06f, 0.4f), cityLight, false);
            var plan = new CityBlockPlan();
            int number = 0;
            foreach (var b in plan.Buildings)
            {
                Material facade = facades[b.Style];
                Box("City tower " + number, new Vector3(b.X, ground + b.Height / 2, b.Z), new Vector3(b.Width, b.Height, b.Depth), facade, false);
                Box("Tower / crown", new Vector3(b.X, ground + b.Height + 0.2f, b.Z), new Vector3(b.Width + 0.25f, 0.4f, b.Depth + 0.25f), concrete, false);
                for (int floor = 0; floor < 6; floor++)
                {
                    float y = ground + 2.2f + floor * (b.Height - 3) / 6;
                    Box("Tower / lit floor", new Vector3(b.X, y, b.Z - b.Depth / 2 - 0.02f), new Vector3(b.Width * 0.76f, 0.16f, 0.03f), (floor + number) % 3 == 0 ? warmLight : cityLight, false);
                }
                Box("Tower / vertical reveal", new Vector3(b.X - b.Width * 0.34f, ground + b.Height / 2, b.Z - b.Depth / 2 - 0.04f), new Vector3(0.12f, b.Height * 0.88f, 0.04f), concrete, false);
                if (b.Style == 1) Box("Tower / rooftop plant", new Vector3(b.X, ground + b.Height + 1, b.Z), new Vector3(b.Width * 0.5f, 1.6f, b.Depth * 0.4f), facades[0], false);
                number++;
            }
            for (int i = 0; i < 6; i++)
            {
                float x = -12.5f + i * 5;
                Box("Plaza / planter", new Vector3(x, ground + 0.28f, 25.5f), new Vector3(2.2f, 0.56f, 1.25f), concrete);
                Shape(PrimitiveType.Sphere, "Plaza / clipped shrub", transform, new Vector3(x, ground + 0.78f, 25.5f), new Vector3(1.8f, 0.95f, 0.9f), green, false);
                Box("Street / lamp pole", new Vector3(x, ground + 2.5f, 29), new Vector3(0.12f, 5, 0.12f), navy, false);
                Box("Street / lamp head", new Vector3(x, ground + 5, 29), new Vector3(0.9f, 0.08f, 0.36f), cityLight, false);
            }
            foreach (float x in new[] { -9f, 9f })
            {
                Box("Plaza / bench seat", new Vector3(x, ground + 0.48f, 17), new Vector3(3, 0.16f, 0.75f), wood);
                Box("Plaza / bench base", new Vector3(x, ground + 0.2f, 17), new Vector3(2.4f, 0.4f, 0.5f), concrete);
            }
            // City errands are temporarily hidden by the approved career plan. Builder retained.
            // Elevator portals are explicit prototype transitions; the end-day door stays separate.
            var entry = Box("City / office elevator", new Vector3(0, ground + 1.35f, 8.2f), new Vector3(2.5f, 2.7f, 0.35f), navy);
            Interactable(entry, OfficeStation.ReturnOffice, "Лифт в офис", "Вернуться в Мойка Капитал");
            Box("Elevator / city indicator", new Vector3(0, ground + 2.95f, 8.12f), new Vector3(2.5f, 0.12f, 0.2f), cityLight, false);
            var sign = Text("City / elevator sign", "ОФИС", new Vector3(0, ground + 2.05f, 8.43f), 48, 0.048f, new Color(0.83f, 0.96f, 0.96f));
            sign.transform.localEulerAngles = new Vector3(0, 180, 0);
            var portal = Box("Office / street elevator", new Vector3(5.85f, 1.3f, -0.2f), new Vector3(0.14f, 2.6f, 1.3f), navy);
            Interactable(portal, OfficeStation.Street, "Лифт в квартал", "Выйти на площадь");
            Box("Office / elevator light", new Vector3(5.74f, 2.5f, -0.2f), new Vector3(0.05f, 0.06f, 1.15f), cityLight, false);
            var label = Text("Office / street sign", "КВАРТАЛ", new Vector3(5.72f, 1.8f, -0.2f), 48, 0.03f, new Color(0.83f, 0.96f, 0.96f));
            label.transform.localEulerAngles = new Vector3(0, 90, 0);
            for (int lane = 0; lane < cityCars.Length; lane++)
            {
                var car = new GameObject("City / visual traffic " + lane); car.transform.SetParent(transform, false); cityCars[lane] = car.transform;
                Shape(PrimitiveType.Cube, "Car / body", car.transform, Vector3.zero, new Vector3(3.6f, 0.65f, 1.65f), lane % 2 == 0 ? facades[1] : cream, false);
                Shape(PrimitiveType.Cube, "Car / cabin", car.transform, new Vector3(-0.1f, 0.48f, 0), new Vector3(1.8f, 0.5f, 1.45f), navy, false);
                Shape(PrimitiveType.Cube, "Car / headlight", car.transform, new Vector3(1.81f, 0.08f, 0), new Vector3(0.04f, 0.12f, 1.25f), warmLight, false);
                Shape(PrimitiveType.Cube, "Car / tail light", car.transform, new Vector3(-1.81f, 0.08f, 0), new Vector3(0.04f, 0.1f, 1.25f), amber, false);
                car.transform.localRotation = Quaternion.Euler(0, lane % 2 == 0 ? 0 : 180, 0);
            }
            previousFog = RenderSettings.fog; previousFogColor = RenderSettings.fogColor;
            previousFogMode = RenderSettings.fogMode; previousFogStart = RenderSettings.fogStartDistance; previousFogEnd = RenderSettings.fogEndDistance;
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.09f, 0.14f, 0.22f); RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 45; RenderSettings.fogEndDistance = 145;
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
