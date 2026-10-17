using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // Editable greybox set built from Unity primitives; no third-party art assets.
    public sealed class OfficeScene : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly Transform[] people = new Transform[2];
        private readonly Renderer[] monitorScreens = new Renderer[2];
        private Material green, amber, phoneMaterial, navy, wood, brass;
        private Camera officeCamera;
        private Light sunlight;
        private Font font;
        private TextMesh officeSign;
        private readonly TextMesh[] workerLabels = new TextMesh[2];

        private void Awake() { Build(); }
        private Material Mat(string hex)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null || shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var material = new Material(shader);
            ColorUtility.TryParseHtmlString(hex, out var color); material.color = color;
            materials.Add(material); return material;
        }
        private GameObject Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.SetParent(transform, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var collider = obj.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            return obj;
        }
        private TextMesh Text(string name, string text, Vector3 position, int size, float scale, Color color)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false); obj.transform.localPosition = position;
            obj.transform.localEulerAngles = new Vector3(0, 0, 0);
            var label = obj.AddComponent<TextMesh>(); label.text = text; label.font = font; label.fontSize = size;
            label.characterSize = scale; label.anchor = TextAnchor.MiddleCenter; label.color = color;
            label.GetComponent<Renderer>().sharedMaterial = font.material;
            return label;
        }
        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            navy = Mat("#183039"); wood = Mat("#A66D45"); brass = Mat("#D6AB60");
            var wall = Mat("#DAD5C6"); var floor = Mat("#43565A"); var white = Mat("#E8E3D6");
            var ink = Mat("#172329"); green = Mat("#60BCA3"); amber = Mat("#DC9C51");
            phoneMaterial = Mat("#263F48");
            Box("Floor", new Vector3(0, -0.2f, 0), new Vector3(12, 0.35f, 7), floor);
            Box("Rear wall", new Vector3(0, 1.6f, 3.45f), new Vector3(12, 3.5f, 0.2f), wall);
            Box("Left wall", new Vector3(-5.9f, 1.3f, 0.8f), new Vector3(0.2f, 2.9f, 5.3f), navy);
            for (int i = 0; i < 3; i++)
            {
                float x = -3.7f + i * 3.0f;
                Box("Window frame", new Vector3(x, 2, 3.28f), new Vector3(2.5f, 1.8f, 0.12f), navy);
                Box("Window glass", new Vector3(x, 2, 3.18f), new Vector3(2.25f, 1.55f, 0.08f), Mat(i % 2 == 0 ? "#83A9B4" : "#A9C2C5"));
                Box("Window mullion", new Vector3(x, 2, 3.08f), new Vector3(0.06f, 1.6f, 0.08f), white);
            }
            Box("Company board", new Vector3(0.5f, 3.12f, 3.09f), new Vector3(5.3f, 0.55f, 0.1f), navy);
            officeSign = Text("Company name", "МОЙКА КАПИТАЛ  /  BROKERAGE", new Vector3(0.5f, 3.1f, 2.99f), 48, 0.055f, new Color(0.92f, 0.82f, 0.58f));
            Box("Boss desk", new Vector3(0.5f, 0.8f, -1.65f), new Vector3(4.2f, 0.18f, 1.25f), wood);
            foreach (float x in new[] { -1.1f, 2.1f }) Box("Desk leg", new Vector3(x, 0.35f, -1.65f), new Vector3(0.14f, 0.8f, 1), navy);
            Box("Boss monitor", new Vector3(0.1f, 1.35f, -1.3f), new Vector3(1.0f, 0.7f, 0.12f), ink);
            Box("Boss screen", new Vector3(0.1f, 1.35f, -1.39f), new Vector3(0.88f, 0.57f, 0.04f), green);
            Box("Telephone", new Vector3(1.7f, 0.98f, -1.8f), new Vector3(0.7f, 0.16f, 0.45f), phoneMaterial);
            Box("Handset", new Vector3(1.7f, 1.12f, -1.8f), new Vector3(0.8f, 0.12f, 0.2f), phoneMaterial);
            Box("Paper stack", new Vector3(-1.0f, 0.96f, -1.8f), new Vector3(0.55f, 0.12f, 0.4f), white);
            for (int index = 0; index < 2; index++)
            {
                float x = index == 0 ? -3.2f : 3.5f;
                Box("Employee desk", new Vector3(x, 0.75f, 1.0f), new Vector3(2.0f, 0.16f, 1.0f), wood);
                Box("Desk base", new Vector3(x, 0.35f, 1.0f), new Vector3(1.5f, 0.7f, 0.65f), navy);
                Box("Employee monitor", new Vector3(x, 1.15f, 0.95f), new Vector3(0.8f, 0.55f, 0.1f), ink);
                monitorScreens[index] = Box("Employee screen", new Vector3(x, 1.15f, 0.86f), new Vector3(0.68f, 0.43f, 0.04f), green).GetComponent<Renderer>();
                var person = GameObject.CreatePrimitive(PrimitiveType.Capsule); person.name = index == 0 ? "Lena greybox" : "Max greybox";
                person.transform.SetParent(transform, false); person.transform.localPosition = new Vector3(x, 0.95f, 1.95f);
                person.transform.localScale = new Vector3(0.55f, 0.75f, 0.55f); person.GetComponent<Renderer>().sharedMaterial = index == 0 ? navy : amber;
                Destroy(person.GetComponent<Collider>()); people[index] = person.transform;
                workerLabels[index] = Text("Employee status", "", new Vector3(x, 2.25f, 1.4f), 40, 0.065f, new Color(0.12f, 0.18f, 0.2f));
            }
            Box("Coffee cabinet", new Vector3(-5.2f, 0.55f, -0.3f), new Vector3(1, 1.1f, 0.8f), wood);
            Box("Coffee machine", new Vector3(-5.2f, 1.4f, -0.3f), new Vector3(0.7f, 0.7f, 0.55f), ink);
            Box("Plant pot", new Vector3(5.0f, 0.3f, -1.8f), new Vector3(0.6f, 0.6f, 0.6f), brass);
            Box("Plant foliage", new Vector3(5.0f, 1.0f, -1.8f), new Vector3(0.9f, 1.1f, 0.75f), green);
            var cameraObject = new GameObject("Office camera"); cameraObject.transform.SetParent(transform, false);
            officeCamera = cameraObject.AddComponent<Camera>(); officeCamera.clearFlags = CameraClearFlags.SolidColor;
            officeCamera.backgroundColor = new Color(0.10f, 0.17f, 0.2f); officeCamera.orthographic = true;
            officeCamera.orthographicSize = 4.4f; officeCamera.nearClipPlane = 0.1f; officeCamera.farClipPlane = 100;
            cameraObject.transform.localPosition = new Vector3(9, 9, -13); cameraObject.transform.LookAt(transform.TransformPoint(new Vector3(0, 1.1f, 0.3f)));
            var lightObject = new GameObject("Warm office light"); lightObject.transform.SetParent(transform, false);
            sunlight = lightObject.AddComponent<Light>(); sunlight.type = LightType.Directional;
            sunlight.intensity = 1.2f; sunlight.color = new Color(1f, 0.91f, 0.78f); lightObject.transform.localEulerAngles = new Vector3(45, -35, 0);
            // Keep the generated set away from any unrelated scene content.
            transform.position = new Vector3(500, 0, 0);
            // LookAt above was computed before relocation; world rotation remains valid.
        }
        public void Layout(float scale, Vector2 offset)
        {
            if (officeCamera == null) return;
            officeCamera.pixelRect = new Rect(offset.x + 340 * scale, Screen.height - offset.y - 410 * scale, 720 * scale, 300 * scale);
        }
        public void Refresh(BrokerDay day)
        {
            if (day == null) return;
            phoneMaterial.color = day.HasGoldenPhone ? brass.color : navy.color;
            for (int i = 0; i < 2; i++)
            {
                var worker = day.Employees[i];
                monitorScreens[i].sharedMaterial = worker.Work == WorkKind.Idle || worker.Work == WorkKind.Break ? amber : green;
                workerLabels[i].transform.rotation = officeCamera.transform.rotation;
                workerLabels[i].text = worker.Name + " / " + (worker.Work == WorkKind.Idle ? "СВОБОДЕН" : worker.Work == WorkKind.Break ? "ПЕРЕРЫВ" : "В РАБОТЕ");
                float x = i == 0 ? -3.2f : 3.5f;
                people[i].localPosition = new Vector3(x, 0.95f + (worker.Work == WorkKind.Idle ? 0f : Mathf.Sin(Time.unscaledTime * 2f + i) * 0.025f), worker.Work == WorkKind.Break ? 2.8f : 1.95f);
            }
        }
        private void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
