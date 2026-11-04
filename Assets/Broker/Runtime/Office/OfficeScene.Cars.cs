using System.Collections.Generic;
using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // Low-poly cars from CarModels on the Broker/Voxel shader (Standard without it): one body submesh per CarPart — paint
    // tinted per car over the grain texture, details and lamps on the voxel palette, dark glossy glass and red brake lamps
    // that brighten while the car brakes — and four wheels that roll with the distance travelled. Meshes are built once
    // per style, paint materials once per colour; the cars have no colliders (the kerb barriers keep the player off the road).
    public sealed partial class OfficeScene
    {
        private sealed class CarRig
        {
            public Transform Root; public Transform[] Wheels; public MeshRenderer Body;
            // Brake lamps as shown: −1 before the first pose, then 0 or 1 (reading Renderer.sharedMaterials would allocate).
            public Material[] Cruising, Braking; public int ShownBrake = -1;
        }
        private static readonly CarPart[] BodyParts = { CarPart.Paint, CarPart.Detail, CarPart.Glass, CarPart.Lamp, CarPart.Brake };
        private static readonly string[] WheelNames = { "Car / wheel front left", "Car / wheel front right", "Car / wheel rear left", "Car / wheel rear right" };
        private readonly Dictionary<CarStyle, Mesh> carBodies = new Dictionary<CarStyle, Mesh>();
        private Mesh carWheel;
        private Texture2D carGrain;
        private Material[] carPaints = new Material[0];
        private Material carDetail, carGlass, carLamp, carBrakeOff, carBrakeOn;
        // The road surface (top of the asphalt slab) above CityBlockPlan.GroundY, where the tyres stand.
        public const float RoadTop = 0.03f;
        public const float CarPaintSmoothness = 0.55f, CarDetailSmoothness = 0.3f, CarGlassSmoothness = 0.92f;
        public const float CarLampEmission = 1.1f, BrakeOffEmission = 0.22f, BrakeOnEmission = 1.5f;

        private void EnsureCarMaterials()
        {
            if (carDetail != null) return;
            EnsureVoxelMaterials();
            var shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find(VoxelShader) : null;
            if (shader != null && !shader.isSupported) shader = null;
            byte[] rgba = CarModels.GrainRgba();
            var pixels = new Color32[CarModels.GrainSize * CarModels.GrainSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], 255);
            carGrain = new Texture2D(CarModels.GrainSize, CarModels.GrainSize, TextureFormat.RGBA32, true);
            carGrain.name = "Broker / car paint grain";
            carGrain.filterMode = FilterMode.Bilinear; carGrain.wrapMode = TextureWrapMode.Repeat; carGrain.anisoLevel = 4;
            carGrain.SetPixels32(pixels); carGrain.Apply(true, true);
            var paints = CarModels.Paints;
            carPaints = new Material[paints.Count];
            for (int i = 0; i < paints.Count; i++)
            {
                var paint = VoxelMat(shader, "#" + paints[i].Rgb.ToString("X6")); paint.name = "Broker / car paint / " + paints[i].Name;
                UseTexture(paint, carGrain, CarPaintSmoothness);
                carPaints[i] = paint;
            }
            carDetail = VoxelMat(shader, "#FFFFFF"); carDetail.name = "Broker / car detail";
            UseTexture(carDetail, voxelPalette, CarDetailSmoothness);
            // Opaque dark glass, as in Schedule I: the sky and the street reflect in it, nothing is seen inside.
            carGlass = VoxelMat(shader, "#1C2229"); carGlass.name = "Broker / car glass";
            UseTexture(carGlass, null, CarGlassSmoothness);
            carLamp = CarGlow(shader, "Broker / car lamp", "#7A7A7A", CarLampEmission);
            carBrakeOff = CarGlow(shader, "Broker / car brake lamp", "#9A9A9A", BrakeOffEmission);
            carBrakeOn = CarGlow(shader, "Broker / car brake lamp on", "#7A7A7A", BrakeOnEmission);
        }
        private Material CarGlow(Shader shader, string name, string albedo, float emission)
        {
            var material = VoxelMat(shader, albedo); material.name = name;
            UseTexture(material, voxelPalette, 0.6f);
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", voxelPalette);
            material.SetColor("_EmissionColor", new Color(emission, emission, emission));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return material;
        }
        // Unity mesh with the given parts of a PolyMesh as its submeshes; the vertex colour carries the corner shade.
        private static Mesh CarMesh(PolyMesh data, string name, params CarPart[] parts)
        {
            int count = data.VertexCount;
            var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = new Vector2[count]; var shades = new Color32[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]);
                normals[i] = new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]);
                uv[i] = new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]);
                byte shade = data.Shades[i]; shades[i] = new Color32(shade, shade, shade, 255);
            }
            var mesh = new Mesh { name = name };
            if (count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.colors32 = shades;
            mesh.subMeshCount = parts.Length;
            for (int k = 0; k < parts.Length; k++) mesh.SetTriangles(data.Triangles[(int)parts[k]], k);
            mesh.RecalculateBounds();
            return mesh;
        }
        // A car rig: the root on the road under the centre of the car (front +X), the body and four wheel joints.
        private CarRig PlaceCar(CarStyle style, int paint, string name, Transform parent)
        {
            EnsureCarMaterials();
            if (!carBodies.TryGetValue(style, out var body)) carBodies.Add(style, body = CarMesh(CarModels.Body(style), "Broker car / " + style, BodyParts));
            if (carWheel == null) carWheel = CarMesh(CarModels.Wheel(), "Broker car / wheel", CarPart.Detail);
            var root = new GameObject(name); root.transform.SetParent(parent, false);
            var shell = new GameObject("Car / body"); shell.transform.SetParent(root.transform, false);
            shell.AddComponent<MeshFilter>().sharedMesh = body;
            var rig = new CarRig { Root = root.transform, Body = shell.AddComponent<MeshRenderer>(), Wheels = new Transform[CarShape.WheelCount] };
            var colour = carPaints[Mathf.Clamp(paint, 0, carPaints.Length - 1)];
            rig.Cruising = new[] { colour, carDetail, carGlass, carLamp, carBrakeOff };
            rig.Braking = new[] { colour, carDetail, carGlass, carLamp, carBrakeOn };
            var wheelMaterials = new[] { carDetail };
            for (int w = 0; w < CarShape.WheelCount; w++)
            {
                var wheel = new GameObject(WheelNames[w]); wheel.transform.SetParent(root.transform, false);
                var centre = CarModels.Sedan.Wheel(w);
                wheel.transform.localPosition = new Vector3(centre.X, centre.Y, centre.Z);
                wheel.AddComponent<MeshFilter>().sharedMesh = carWheel;
                wheel.AddComponent<MeshRenderer>().sharedMaterials = wheelMaterials;
                rig.Wheels[w] = wheel.transform;
            }
            PoseCar(rig, 0, false);
            return rig;
        }
        // Wheels turn by the distance rolled (front +X: a forward roll is a negative turn about +Z); the right wheels face
        // out on −Z, so they are the same wheel turned round the vertical. Brake lamps swap one material array.
        private static void PoseCar(CarRig rig, float rolled, bool braking)
        {
            float turn = rolled / CarModels.Sedan.WheelRadius * Mathf.Rad2Deg % 360f;
            for (int w = 0; w < rig.Wheels.Length; w++)
                rig.Wheels[w].localRotation = CarShape.LeftWheel(w) ? Quaternion.Euler(0, 0, -turn) : Quaternion.Euler(0, 180, turn);
            int brake = braking ? 1 : 0;
            if (brake == rig.ShownBrake) return;
            rig.ShownBrake = brake; rig.Body.sharedMaterials = braking ? rig.Braking : rig.Cruising;
        }
        private void ReleaseCars()
        {
            foreach (var mesh in carBodies.Values) if (mesh != null) Destroy(mesh);
            carBodies.Clear();
            if (carWheel != null) Destroy(carWheel);
            if (carGrain != null) Destroy(carGrain);
        }
    }
}
