using System.Collections.Generic;
using Broker.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Broker.Runtime.Office
{
    // Procedural low-poly set. All objects are visible/editable in Play Mode.
    public sealed class OfficeScene : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private readonly Transform[] people = new Transform[2], heads = new Transform[2], arms = new Transform[4];
        private readonly Renderer[] screens = new Renderer[2];
        private readonly TextMesh[] workerLabels = new TextMesh[2];
        private Material navy, wood, brass, green, amber, ink, cream, skin, phoneMaterial;
        private Camera overview;
        private Renderer ceilingRenderer;
        private Font font;
        private AmbientMode previousAmbientMode;
        private Color previousAmbient;
        public FirstPersonOfficeController Player { get; private set; }

        private void Awake() { Build(); }
        private Material Mat(string hex, bool glowing = false)
        {
            var shader = GraphicsSettings.currentRenderPipeline == null ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var material = new Material(shader);
            ColorUtility.TryParseHtmlString(hex, out var color); material.color = color;
            if (glowing) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 0.45f); }
            materials.Add(material); return material;
        }
        private GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            var collider = obj.GetComponent<Collider>(); if (!solid && collider != null) Destroy(collider);
            return obj;
        }
        private GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool solid = true)
        { return Shape(PrimitiveType.Cube, name, transform, position, scale, material, solid); }
        private TextMesh Text(string name, string text, Vector3 position, int size, float scale, Color color)
        {
            var obj = new GameObject(name); obj.transform.SetParent(transform, false); obj.transform.localPosition = position;
            var label = obj.AddComponent<TextMesh>(); label.text = text; label.font = font; label.fontSize = size;
            label.characterSize = scale; label.anchor = TextAnchor.MiddleCenter; label.color = color;
            label.GetComponent<Renderer>().sharedMaterial = font.material; return label;
        }
        private void Interactable(GameObject obj, OfficeStation kind, string title, string action, int worker = 0)
        { obj.AddComponent<OfficeInteractable>().Configure(kind, title, action, worker); }
        private void Build()
        {
            transform.position = new Vector3(500, 0, 0);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            navy = Mat("#19323C"); wood = Mat("#9C6542"); brass = Mat("#D2A352"); green = Mat("#65BCA3", true);
            amber = Mat("#D99149"); ink = Mat("#14232A"); cream = Mat("#E9E1CE"); skin = Mat("#DCB18B");
            phoneMaterial = Mat("#294650");
            var wall = Mat("#D8CDB9"); var floor = Mat("#785A45"); var plank = Mat("#8C6950");
            var sky = Mat("#92B5C0", true); var trim = Mat("#4B666C"); var lights = Mat("#FFF0C6", true);
            previousAmbientMode = RenderSettings.ambientMode; previousAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.43f, 0.46f, 0.47f);
            Box("Floor collider", new Vector3(0, -0.15f, 0), new Vector3(12.4f, 0.3f, 9.4f), floor);
            for (int i = 0; i < 12; i++) Box("Wood floor strip", new Vector3(-5.5f + i, 0.005f, 0), new Vector3(0.985f, 0.01f, 9), i % 2 == 0 ? floor : plank, false);
            Box("Rear wall", new Vector3(0, 1.9f, 4.6f), new Vector3(12.4f, 3.8f, 0.2f), wall);
            Box("Front wall", new Vector3(0, 1.9f, -4.6f), new Vector3(12.4f, 3.8f, 0.2f), wall);
            Box("Left wall", new Vector3(-6.1f, 1.9f, 0), new Vector3(0.2f, 3.8f, 9), navy);
            Box("Right wall", new Vector3(6.1f, 1.9f, 0), new Vector3(0.2f, 3.8f, 9), wall);
            ceilingRenderer = Box("Ceiling", new Vector3(0, 3.9f, 0), new Vector3(12.4f, 0.2f, 9.4f), cream).GetComponent<Renderer>();
            Box("Office carpet", new Vector3(0.4f, 0.022f, -0.7f), new Vector3(5.0f, 0.015f, 4.2f), navy, false);
            for (int i = 0; i < 3; i++)
            {
                float x = -3.6f + i * 3.6f;
                Box("Window frame", new Vector3(x, 2.35f, 4.42f), new Vector3(2.9f, 1.85f, 0.12f), navy);
                Box("Window sky", new Vector3(x, 2.35f, 4.32f), new Vector3(2.66f, 1.62f, 0.08f), sky, false);
                for (int b = 0; b < 5; b++)
                {
                    float h = 0.22f + ((b * 3 + i) % 4) * 0.12f;
                    Box("Distant city silhouette", new Vector3(x - 1.0f + b * 0.5f, 1.56f + h * 0.5f, 4.25f), new Vector3(0.34f, h, 0.04f), trim, false);
                }
                Box("Window mullion", new Vector3(x, 2.35f, 4.16f), new Vector3(0.06f, 1.66f, 0.08f), cream, false);
                Box("Window sill", new Vector3(x, 1.43f, 4.2f), new Vector3(3.0f, 0.12f, 0.35f), cream);
            }
            Box("Company plaque", new Vector3(0, 3.42f, 4.35f), new Vector3(6.0f, 0.45f, 0.1f), navy, false);
            Text("Company name", "МОЙКА КАПИТАЛ", new Vector3(0, 3.42f, 4.27f), 64, 0.055f, new Color(0.96f, 0.82f, 0.56f));
            for (int i = 0; i < 3; i++)
            {
                float x = -3.5f + i * 3.5f;
                Box("Ceiling light fixture", new Vector3(x, 3.72f, 0), new Vector3(1.8f, 0.08f, 0.8f), lights, false);
                var lamp = new GameObject("Warm point light"); lamp.transform.SetParent(transform, false); lamp.transform.localPosition = new Vector3(x, 3.3f, 0);
                var point = lamp.AddComponent<Light>(); point.type = LightType.Point; point.range = 8; point.intensity = 1.1f;
                point.color = new Color(1, 0.88f, 0.69f);
            }
            BuildBossDesk(); BuildWorker(0, -3.5f); BuildWorker(1, 3.5f); BuildCoffee(); BuildUpgrade(); BuildDoor();
            Box("Plant pot", new Vector3(5.3f, 0.32f, 3.3f), new Vector3(0.6f, 0.64f, 0.6f), brass);
            Box("Plant stem", new Vector3(5.3f, 0.96f, 3.3f), new Vector3(0.08f, 0.8f, 0.08f), wood, false);
            for (int i = 0; i < 4; i++)
            {
                var leaf = Box("Plant leaf", new Vector3(5.3f + (i % 2 == 0 ? -0.2f : 0.2f), 1.0f + i * 0.16f, 3.3f), new Vector3(0.65f, 0.1f, 0.35f), green, false);
                leaf.transform.localEulerAngles = new Vector3(0, i * 65, i % 2 == 0 ? -25 : 25);
            }
            var player = new GameObject("Player - WASD and mouse"); player.transform.SetParent(transform, false);
            player.transform.localPosition = new Vector3(0, 0.05f, -3.4f); player.AddComponent<CharacterController>();
            Player = player.AddComponent<FirstPersonOfficeController>();
            var camera = new GameObject("Desk overview camera"); camera.transform.SetParent(transform, false);
            overview = camera.AddComponent<Camera>(); overview.depth = 10; overview.clearFlags = CameraClearFlags.SolidColor;
            overview.backgroundColor = new Color(0.10f, 0.17f, 0.2f); overview.orthographic = true;
            overview.orthographicSize = 5.5f; overview.nearClipPlane = 0.1f; overview.farClipPlane = 100;
            camera.transform.localPosition = new Vector3(10, 12, -14); camera.transform.LookAt(transform.TransformPoint(new Vector3(0, 0.9f, 0.4f)));
            overview.enabled = false;
        }
        private void BuildBossDesk()
        {
            var desktop = Box("Director desk - interact", new Vector3(1, 0.78f, -1.2f), new Vector3(3.6f, 0.16f, 1.25f), wood);
            Interactable(desktop, OfficeStation.Desk, "Рабочее место директора", "Сесть за стол и открыть клиентскую базу");
            foreach (float x in new[] { -0.5f, 2.5f }) Box("Director desk leg", new Vector3(x, 0.35f, -1.2f), new Vector3(0.16f, 0.7f, 1.1f), navy);
            Box("Desk front panel", new Vector3(1, 0.45f, -0.7f), new Vector3(3.3f, 0.55f, 0.08f), wood);
            var monitor = Box("Director monitor", new Vector3(0.7f, 1.25f, -1.0f), new Vector3(1, 0.64f, 0.1f), ink);
            Interactable(monitor, OfficeStation.Desk, "Клиентская база", "Открыть рабочий экран");
            Box("Director screen", new Vector3(0.7f, 1.25f, -1.07f), new Vector3(0.86f, 0.5f, 0.025f), green, false);
            Box("Monitor stand", new Vector3(0.7f, 0.97f, -1.0f), new Vector3(0.12f, 0.3f, 0.12f), ink, false);
            Box("Keyboard", new Vector3(0.7f, 0.89f, -1.55f), new Vector3(0.85f, 0.05f, 0.25f), cream, false);
            var phone = Box("Telephone - interact", new Vector3(2.2f, 0.94f, -1.45f), new Vector3(0.64f, 0.15f, 0.4f), phoneMaterial);
            Interactable(phone, OfficeStation.Desk, "Брокерский телефон", "Выбрать клиента и позвонить");
            var handset = Box("Handset", new Vector3(2.2f, 1.07f, -1.45f), new Vector3(0.72f, 0.12f, 0.19f), phoneMaterial);
            handset.transform.SetParent(phone.transform, true);
            Box("Director papers", new Vector3(-0.1f, 0.9f, -1.5f), new Vector3(0.48f, 0.07f, 0.35f), cream, false);
            Chair(1, 0.05f); Text("Director title", "ГЕНЕРАЛЬНЫЙ ДИРЕКТОР", new Vector3(1, 0.5f, -1.86f), 48, 0.025f, new Color(0.94f, 0.82f, 0.58f));
        }
        private void Chair(float x, float z)
        {
            Box("Chair seat", new Vector3(x, 0.48f, z), new Vector3(0.7f, 0.13f, 0.65f), navy, false);
            Box("Chair back", new Vector3(x, 0.9f, z + 0.28f), new Vector3(0.7f, 0.75f, 0.1f), navy, false);
            Box("Chair stem", new Vector3(x, 0.24f, z), new Vector3(0.12f, 0.4f, 0.12f), ink, false);
            Box("Chair base", new Vector3(x, 0.06f, z), new Vector3(0.8f, 0.08f, 0.6f), ink, false);
        }
        private void BuildWorker(int index, float x)
        {
            Box("Employee desktop", new Vector3(x, 0.76f, 1.45f), new Vector3(2.1f, 0.14f, 1.1f), wood);
            Box("Employee desk base", new Vector3(x, 0.34f, 1.6f), new Vector3(1.6f, 0.68f, 0.7f), navy);
            Box("Employee monitor", new Vector3(x + 0.4f, 1.12f, 1.15f), new Vector3(0.7f, 0.5f, 0.1f), ink);
            screens[index] = Box("Employee screen", new Vector3(x + 0.4f, 1.12f, 1.08f), new Vector3(0.58f, 0.39f, 0.025f), green, false).GetComponent<Renderer>();
            Box("Employee papers", new Vector3(x - 0.55f, 0.87f, 1.2f), new Vector3(0.4f, 0.06f, 0.3f), cream, false);
            Chair(x, 2.9f);
            var person = new GameObject(index == 0 ? "Lena - low poly" : "Max - low poly"); person.transform.SetParent(transform, false);
            person.transform.localPosition = new Vector3(x, 0, 2.45f); people[index] = person.transform;
            var hitbox = person.AddComponent<CapsuleCollider>(); hitbox.height = 1.85f; hitbox.radius = 0.3f; hitbox.center = new Vector3(0, 0.94f, 0);
            Interactable(person, OfficeStation.Employee, index == 0 ? "Лена / порядок в хаосе" : "Макс / отдел громких обещаний", "Поговорить и дать поручение", index);
            var suit = index == 0 ? navy : amber;
            Shape(PrimitiveType.Cube, "Suit torso", person.transform, new Vector3(0, 1.1f, 0), new Vector3(0.52f, 0.62f, 0.3f), suit, false);
            Shape(PrimitiveType.Cube, "Shirt", person.transform, new Vector3(0, 1.2f, -0.16f), new Vector3(0.15f, 0.38f, 0.03f), cream, false);
            Shape(PrimitiveType.Cube, "Tie", person.transform, new Vector3(0, 1.14f, -0.185f), new Vector3(0.06f, 0.31f, 0.02f), index == 0 ? brass : navy, false);
            foreach (float side in new[] { -1f, 1f })
            {
                Shape(PrimitiveType.Cube, "Trouser leg", person.transform, new Vector3(side * 0.14f, 0.46f, 0), new Vector3(0.21f, 0.75f, 0.24f), navy, false);
                Shape(PrimitiveType.Cube, "Shoe", person.transform, new Vector3(side * 0.14f, 0.08f, -0.07f), new Vector3(0.24f, 0.14f, 0.4f), ink, false);
                var arm = Shape(PrimitiveType.Cube, "Sleeve", person.transform, new Vector3(side * 0.34f, 1.05f, -0.04f), new Vector3(0.14f, 0.55f, 0.17f), suit, false);
                arms[index * 2 + (side < 0 ? 0 : 1)] = arm.transform;
                Shape(PrimitiveType.Cube, "Hand", arm.transform, new Vector3(0, -0.56f, 0), new Vector3(0.9f, 0.2f, 0.9f), skin, false);
            }
            var head = Shape(PrimitiveType.Sphere, "Head", person.transform, new Vector3(0, 1.65f, 0), new Vector3(0.38f, 0.44f, 0.35f), skin, false); heads[index] = head.transform;
            Shape(PrimitiveType.Cube, "Hair", person.transform, new Vector3(0, 1.85f, 0.04f), new Vector3(0.4f, 0.14f, 0.36f), index == 0 ? ink : wood, false);
            foreach (float side in new[] { -1f, 1f }) Shape(PrimitiveType.Cube, "Eye", person.transform, new Vector3(side * 0.08f, 1.68f, -0.17f), new Vector3(0.035f, 0.035f, 0.025f), ink, false);
            workerLabels[index] = Text("Employee label", index == 0 ? "ЛЕНА" : "МАКС", new Vector3(x, 2.2f, 2.45f), 48, 0.035f, new Color(0.95f, 0.88f, 0.7f));
        }
        private void BuildCoffee()
        {
            Box("Coffee cabinet", new Vector3(-5.25f, 0.5f, -1.1f), new Vector3(1.1f, 1, 0.8f), wood);
            var coffee = Box("Coffee machine - interact", new Vector3(-5.25f, 1.3f, -1.1f), new Vector3(0.7f, 0.6f, 0.55f), ink);
            Interactable(coffee, OfficeStation.Coffee, "Кофемашина / корпоративная мотивация", "Купить кофе для команды");
            Box("Coffee front", new Vector3(-5.25f, 1.3f, -1.4f), new Vector3(0.5f, 0.4f, 0.05f), brass, false);
            Box("Coffee button", new Vector3(-5.05f, 1.45f, -1.44f), new Vector3(0.08f, 0.08f, 0.03f), green, false);
            Shape(PrimitiveType.Cylinder, "Coffee cup", transform, new Vector3(-5.55f, 1.08f, -1.38f), new Vector3(0.18f, 0.12f, 0.18f), cream, false);
        }
        private void BuildUpgrade()
        {
            Box("Premium display pedestal", new Vector3(5.0f, 0.55f, -2.6f), new Vector3(0.85f, 1.1f, 0.85f), navy);
            var sample = Box("Golden phone display - interact", new Vector3(5.0f, 1.18f, -2.6f), new Vector3(0.65f, 0.16f, 0.42f), brass);
            Interactable(sample, OfficeStation.Upgrade, "Телефон для серьёзного человека", "Посмотреть улучшение за $90");
            var receiver = Box("Display handset", new Vector3(5.0f, 1.32f, -2.6f), new Vector3(0.74f, 0.12f, 0.2f), brass); receiver.transform.SetParent(sample.transform, true);
            Text("Upgrade price", "$90 / СОЛИДНОСТЬ", new Vector3(5, 0.75f, -3.04f), 48, 0.023f, new Color(0.96f, 0.82f, 0.56f));
        }
        private void BuildDoor()
        {
            Box("Door frame", new Vector3(0, 1.3f, -4.4f), new Vector3(1.65f, 2.6f, 0.15f), brass);
            var door = Box("Exit door - interact", new Vector3(0, 1.25f, -4.28f), new Vector3(1.4f, 2.45f, 0.12f), navy);
            Interactable(door, OfficeStation.Exit, "Входная дверь", "Завершить рабочий день");
            Box("Door handle", new Vector3(-0.48f, 1.1f, -4.17f), new Vector3(0.16f, 0.08f, 0.14f), brass, false);
            var sign = Text("Door sign", "ДОМОЙ / ПОСЛЕ ПЛАНА", new Vector3(0, 1.85f, -4.17f), 48, 0.024f, new Color(0.95f, 0.85f, 0.65f));
            sign.transform.localEulerAngles = new Vector3(0, 180, 0);
        }
        public void Present(bool atDesk, float scale, Vector2 offset)
        {
            if (overview == null || Player == null) return;
            overview.enabled = atDesk; Player.Eye.enabled = !atDesk;
            ceilingRenderer.enabled = !atDesk; // The desk overview must see inside the room, not the roof.
            if (atDesk) overview.pixelRect = new Rect(offset.x + 340 * scale, Screen.height - offset.y - 410 * scale, 720 * scale, 300 * scale);
            else Player.Eye.rect = new Rect(0, 0, 1, 1);
        }
        public void ResetPlayer() { Player.ResetAt(transform.TransformPoint(new Vector3(0, 0.05f, -3.4f))); }
        public void Refresh(BrokerDay day)
        {
            if (day == null) return;
            phoneMaterial.color = day.HasGoldenPhone ? brass.color : navy.color;
            Camera active = overview.enabled ? overview : Player.Eye;
            for (int i = 0; i < 2; i++)
            {
                var worker = day.Employees[i]; bool working = worker.Work == WorkKind.Leads || worker.Work == WorkKind.Research;
                screens[i].sharedMaterial = working ? green : amber;
                workerLabels[i].transform.rotation = active.transform.rotation;
                workerLabels[i].text = worker.Name + " / " + (worker.Work == WorkKind.Idle ? (i == 0 ? "СВОБОДНА" : "СВОБОДЕН") : worker.Work == WorkKind.Break ? "ПЕРЕРЫВ" : "В РАБОТЕ");
                heads[i].localRotation = Quaternion.Euler(0, working ? Mathf.Sin(Time.unscaledTime * 1.7f + i) * 8 : 0, 0);
                for (int a = 0; a < 2; a++) arms[i * 2 + a].localRotation = Quaternion.Euler(working ? -22 + Mathf.Sin(Time.unscaledTime * 3 + a) * 5 : 0, 0, 0);
            }
        }
        private void OnDestroy()
        {
            RenderSettings.ambientMode = previousAmbientMode; RenderSettings.ambientLight = previousAmbient;
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}
