using System.Collections.Generic;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        private readonly Dictionary<string, Material> modelMaterials = new Dictionary<string, Material>();

        // Imported visuals never own gameplay hitboxes or station references.
        // Keep the procedural rendering if an imported model is unavailable.
        private bool PlaceFurniture(string asset, string name, Vector3 floorCenter, Vector3 size)
            => PlaceLocalModel("KenneyFurniture", asset, name, floorCenter, size, transform, false) != null;
        private GameObject PlaceLocalModel(string pack, string asset, string name, Vector3 floorCenter, Vector3 size, Transform parent, bool textured, float yaw = 0)
        {
            var source = Resources.Load<GameObject>("BrokerModels/" + pack + "/" + asset);
            if (source == null) return null;
            var atlas = textured ? Resources.Load<Texture2D>("BrokerModels/" + pack + "/Textures/colormap") : null;
            if (textured && atlas == null) return null;
            var obj = Instantiate(source, parent, false); obj.name = name;
            obj.transform.localPosition = Vector3.zero; obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            var renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { Destroy(obj); return null; }
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.x <= 0 || bounds.size.y <= 0 || bounds.size.z <= 0) { Destroy(obj); return null; }
            // Fit before rotation: local X and Z exchange world axes at a quarter turn.
            bool quarterTurn = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) > 0.5f;
            obj.transform.localScale = new Vector3((quarterTurn ? size.z : size.x) / bounds.size.x, size.y / bounds.size.y,
                (quarterTurn ? size.x : size.z) / bounds.size.z);
            obj.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var anchor = parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            obj.transform.localPosition = floorCenter - anchor;
            foreach (var collider in obj.GetComponentsInChildren<Collider>()) { collider.enabled = false; Destroy(collider); }
            foreach (var renderer in renderers)
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string slotName = slots[i] == null ? "default" : slots[i].name;
                    string key = pack + "/" + slotName + (textured ? "/atlas" : "/flat");
                    if (!modelMaterials.TryGetValue(key, out var material))
                    {
                        // Model colors are remapped to the game's restrained office palette.
                        string lower = slotName.ToLowerInvariant();
                        string color = lower.Contains("wooddark") ? "#62513F" : lower.Contains("wood") ? "#917454"
                            : lower.Contains("plant") || lower.Contains("leaf") || lower.Contains("green") ? "#608776"
                            : lower.Contains("screen") ? "#294650" : lower.Contains("white") ? "#D5D8D3"
                            : lower.Contains("carpet") || lower.Contains("fabric") ? "#294053"
                            : lower.Contains("dark") || lower.Contains("black") ? "#202D36"
                            : lower.Contains("metal") ? "#62757C" : "#D5D8D3";
                        material = Mat(textured ? "#FFFFFF" : color);
                        if (textured)
                        {
                            if (atlas != null)
                            {
                                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", atlas);
                                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", atlas);
                            }
                            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.15f);
                            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.15f);
                        }
                        modelMaterials.Add(key, material);
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
            }
            return obj;
        }
        private void HidePrimitiveVisuals(params string[] names)
        {
            // Only direct children are construction primitives; imported descendants are excluded.
            foreach (Transform child in transform)
                foreach (string name in names)
                    if (child.name == name)
                    {
                        var renderer = child.GetComponent<Renderer>(); if (renderer != null) renderer.enabled = false;
                    }
        }
        private void BuildImportedFurniture()
        {
            if (PlaceFurniture("desk", "Imported director desk", new Vector3(1, 0, -1.2f), new Vector3(3.6f, 0.86f, 1.25f)))
                HidePrimitiveVisuals("Director desk - interact", "Director desk leg", "Desk front panel");
            bool bothDesks = true;
            foreach (float x in new[] { -3.5f, 3.5f })
                bothDesks &= PlaceFurniture("desk", "Imported employee desk", new Vector3(x, 0, 1.45f), new Vector3(2.1f, 0.83f, 1.1f));
            if (bothDesks) HidePrimitiveVisuals("Employee desktop", "Employee desk base");
            // Existing chair primitives have no colliders and may be replaced as a group.
            bool allChairs = true;
            foreach (var position in new[] { new Vector3(1, 0, 0.05f), new Vector3(-3.5f, 0, 2.9f), new Vector3(3.5f, 0, 2.9f) })
                allChairs &= PlaceFurniture("chairDesk", "Imported office chair", position, new Vector3(0.8f, 1.25f, 0.8f));
            if (allChairs) HidePrimitiveVisuals("Chair seat", "Chair back", "Chair stem", "Chair base");
            if (PlaceFurniture("computerKeyboard", "Imported keyboard", new Vector3(0.7f, 0.865f, -1.55f), new Vector3(0.85f, 0.05f, 0.25f)))
                HidePrimitiveVisuals("Keyboard");
            PlaceFurniture("computerMouse", "Imported mouse", new Vector3(1.35f, 0.865f, -1.55f), new Vector3(0.08f, 0.035f, 0.12f));
            if (PlaceFurniture("kitchenCoffeeMachine", "Imported coffee machine", new Vector3(-5.25f, 1, -1.1f), new Vector3(0.7f, 0.6f, 0.55f)))
                HidePrimitiveVisuals("Coffee machine - interact", "Coffee front", "Coffee button");
            if (PlaceFurniture("pottedPlant", "Imported potted plant", new Vector3(5.3f, 0, 3.3f), new Vector3(0.9f, 1.65f, 0.9f)))
                HidePrimitiveVisuals("Plant pot", "Plant stem", "Plant leaf");
        }
    }
}
