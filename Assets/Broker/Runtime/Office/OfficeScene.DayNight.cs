using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // Time of day (stage 4, Core/DayCycle). Refresh follows the work day: 9:00 at its start, midnight when its time is up.
    // SetHour applies the light of an hour: the sun or the moon with its colour, intensity, shadow strength and
    // direction, the ambient, the haze and the sky's palette and disk; after dusk the buildings' night glass glows, the street
    // lamps light their glass and a point light under it, a light comes on over the office door, car lamps and the rooms'
    // lamps burn brighter. The environment (reflections in glass)
    // is re-rendered every few minutes of the city clock. HourOverride shows any hour in Play mode for checking (Inspector of
    // the office scene object); −1 follows the day. Without the Broker/Sky shader the fallback sky stays a daytime one.
    public sealed partial class OfficeScene
    {
        public float HourOverride = -1;
        public float Hour { get; private set; } = -1;
        public DayLight CurrentLight { get; private set; }
        // Lights that come on after dusk with their full intensity: the street lamps and the office door.
        private struct NightLamp { public Light Light; public float Intensity; }
        private readonly List<NightLamp> nightLamps = new List<NightLamp>();
        // The rooms' lamps (office, café, exchange hall) with their daytime intensity.
        private readonly List<NightLamp> roomLights = new List<NightLamp>();
        private Material lampGlass;
        private float environmentHour = -100;
        // The environment is re-rendered when the clock has moved this far (hours): three minutes, 2 s of the work day.
        public const float EnvironmentStep = 0.05f;

        public void FollowDay(BrokerDay day)
        {
            if (day == null) return;
            SetHour(HourOverride >= 0 ? HourOverride : DayCycle.HourOf(day));
        }
        // Any hour; Hour keeps it on the 24-hour circle (midnight, the end of the work day, is 0).
        public void SetHour(float hour)
        {
            float h = hour % 24; if (h < 0) h += 24;
            if (CurrentLight != null && Mathf.Abs(h - Hour) < 1e-4f) return;
            ApplyDayLight(DayCycle.At(h));
        }
        private void ApplyDayLight(DayLight light)
        {
            CurrentLight = light; Hour = light.Hour;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(new Vector3(light.LightX, light.LightY, light.LightZ), Vector3.up);
                sun.color = Rgb(light.Sun); sun.intensity = light.Intensity; sun.shadowStrength = light.Shadow;
            }
            RenderSettings.ambientSkyColor = Rgb(light.AmbientSky);
            RenderSettings.ambientEquatorColor = Rgb(light.AmbientEquator);
            RenderSettings.ambientGroundColor = Rgb(light.AmbientGround);
            var haze = Rgb(light.Haze);
            RenderSettings.fogColor = haze;
            if (Player != null && Player.Eye != null) Player.Eye.backgroundColor = haze;
            if (skyMaterial != null) PaintSky(skyMaterial, light);
            if (Mathf.Abs(light.Hour - environmentHour) >= EnvironmentStep) { environmentHour = light.Hour; DynamicGI.UpdateEnvironment(); }
            ApplyNightLights(light.Lights);
        }
        // Night glass, lamp glass and lamp lights, car lamps, the rooms' lamps: as by day until the evening, full after dusk.
        private void ApplyNightLights(float lights)
        {
            bool on = lights > 0.001f;
            if (buildingNight != null) Glow(buildingNight, Rgb(DayCycle.WindowGlow) * (DayCycle.WindowStrength * lights));
            if (lampGlass != null) Glow(lampGlass, Color.white * Mathf.Lerp(DayCycle.LampGlassDay, DayCycle.LampGlassEmission, lights));
            foreach (var lamp in nightLamps)
            {
                if (lamp.Light == null) continue;
                lamp.Light.enabled = on; lamp.Light.intensity = lamp.Intensity * lights;
            }
            foreach (var room in roomLights) if (room.Light != null) room.Light.intensity = room.Intensity * (1 + DayCycle.RoomBoost * lights);
            if (carLamp != null) Glow(carLamp, Color.white * (CarLampEmission * (1 + DayCycle.HeadlightBoost * lights)));
            if (carBrakeOff != null) Glow(carBrakeOff, Color.white * (BrakeOffEmission * (1 + DayCycle.TailLightBoost * lights)));
        }
        private static void Glow(Material material, Color emission)
        {
            emission.a = 1;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
        // Street lamp glass: the voxel glow's palette, pale by day and lit after dusk (ApplyNightLights).
        private Material LampGlass()
        {
            if (lampGlass != null) return lampGlass;
            EnsureVoxelMaterials();
            lampGlass = new Material(voxelGlow) { name = "Broker / street lamp glass" }; materials.Add(lampGlass);
            Glow(lampGlass, Color.white * DayCycle.LampGlassDay);
            return lampGlass;
        }
        // A street lamp with its own glass material and a point light under the glass, out by day. The light renders per
        // pixel (Important): the lamps light the pavement and the walls around them, not only the nearest few.
        private void PlaceStreetLamp(string name, Vector3 position, float yaw)
        {
            var lamp = PlaceBuilding("street lamp", StreetModels.StreetLamp, StreetModels.FurnitureLook, name, position, yaw, glow: LampGlass());
            NightLight(name + " / light", lamp.transform, new Vector3(0, StreetModels.LampGlassY - DayCycle.LampDrop, StreetModels.LampGlassZ),
                DayCycle.LampRange, DayCycle.LampIntensity);
        }
        // A room's lamp, brighter after dusk (ApplyNightLights).
        private void RoomLight(Light point) => roomLights.Add(new NightLamp { Light = point, Intensity = point.intensity });
        // A warm point light out by day, lit after dusk (the street lamps, the office door on the plaza).
        private void NightLight(string name, Transform parent, Vector3 position, float range, float intensity)
        {
            var bulb = new GameObject(name); bulb.transform.SetParent(parent, false); bulb.transform.localPosition = position;
            var point = bulb.AddComponent<Light>(); point.type = LightType.Point; point.range = range;
            point.color = Rgb(DayCycle.LampLight); point.intensity = 0; point.shadows = LightShadows.None;
            point.renderMode = LightRenderMode.ForcePixel; point.enabled = false;
            nightLamps.Add(new NightLamp { Light = point, Intensity = intensity });
        }
    }
}
