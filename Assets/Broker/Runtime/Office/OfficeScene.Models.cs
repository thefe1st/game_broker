using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        // Voxel visuals never own gameplay hitboxes or station references: the original boxes keep
        // colliders and interactables, only their renderers are hidden.
        private void HidePrimitiveVisuals(params string[] names)
        {
            // Only direct children are construction primitives; voxel descendants are excluded.
            foreach (Transform child in transform)
                foreach (string name in names)
                    if (child.name == name)
                    {
                        var renderer = child.GetComponent<Renderer>(); if (renderer != null) renderer.enabled = false;
                    }
        }
        private void BuildVoxelFurniture()
        {
            PlaceVoxel("desk/director", () => VoxelProps.Desk(3.6f, 0.85f, 1.25f, VoxelPalette.Wood, VoxelPalette.WoodDark),
                "Voxel director desk", transform, new Vector3(1, 0, -1.2f));
            HidePrimitiveVisuals("Director desk - interact", "Director desk leg", "Desk front panel");
            foreach (float x in new[] { -3.5f, 3.5f })
                PlaceVoxel("desk/employee", () => VoxelProps.Desk(2.1f, 0.85f, 1.1f, VoxelPalette.WoodLight, VoxelPalette.Navy),
                    "Voxel employee desk", transform, new Vector3(x, 0, 1.45f));
            HidePrimitiveVisuals("Employee desktop", "Employee desk base");
            // Chair primitives have no colliders and are replaced as a group.
            foreach (var position in new[] { new Vector3(1, 0, 0.05f), new Vector3(-3.5f, 0, 2.9f), new Vector3(3.5f, 0, 2.9f) })
                PlaceVoxel("chair", () => VoxelProps.OfficeChair(VoxelPalette.Navy), "Voxel office chair", transform, position);
            HidePrimitiveVisuals("Chair seat", "Chair back", "Chair stem", "Chair base");
            PlaceVoxel("keyboard", VoxelProps.Keyboard, "Voxel keyboard", transform, new Vector3(0.7f, 0.85f, -1.55f));
            HidePrimitiveVisuals("Keyboard");
            PlaceVoxel("mouse", VoxelProps.Mouse, "Voxel mouse", transform, new Vector3(1.35f, 0.85f, -1.55f));
            PlaceVoxel("coffee machine", VoxelProps.CoffeeMachine, "Voxel coffee machine", transform, new Vector3(-5.25f, 1, -1.1f));
            HidePrimitiveVisuals("Coffee machine - interact", "Coffee front", "Coffee button");
            PlaceVoxel("potted plant", () => VoxelProps.PottedPlant(4), "Voxel potted plant", transform, new Vector3(5.3f, 0, 3.3f));
            HidePrimitiveVisuals("Plant pot");
        }
    }
}
