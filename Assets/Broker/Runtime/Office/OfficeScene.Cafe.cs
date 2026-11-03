using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // Café «Бык и медведь» on the corner of the business street: the open voxel shell, the walls of its room around the
    // door, a wooden floor, a warm lamp, the coffee machine on the counter and the name over the awning. Furniture comes
    // from CityScenery; the counter collider opens the café panel.
    public sealed partial class OfficeScene
    {
        private void BuildCafe()
        {
            const float g = CityBlockPlan.GroundY;
            PlaceVoxel("cafe shell", VoxelProps.CafeShell, "Cafe / voxel shell", transform, new Vector3(24, g, -10));
            Blocker("Cafe / west wall", new Vector3(21.25f, g + 1.5f, -6), new Vector3(0.5f, 3, 8));
            Blocker("Cafe / east wall", new Vector3(26.75f, g + 1.5f, -6), new Vector3(0.5f, 3, 8));
            Blocker("Cafe / back wall", new Vector3(24, g + 1.5f, -9.75f), new Vector3(6, 3, 0.5f));
            // The front wall leaves the 1.5 m door open; the wide window is for looking, not for climbing through.
            Blocker("Cafe / front wall by the door", new Vector3(21.5f, g + 1.5f, -2.25f), new Vector3(1, 3, 0.5f));
            Blocker("Cafe / front wall with the window", new Vector3(25.25f, g + 1.5f, -2.25f), new Vector3(3.5f, 3, 0.5f));
            Box("Cafe / wooden floor", new Vector3(24, g + 0.006f, -6), new Vector3(5, 0.012f, 7), wood, false);
            // Turned to the room, so the guests see the brass front and the lights rather than a black back.
            PlaceVoxel("coffee machine", VoxelProps.CoffeeMachine, "Cafe / coffee machine", transform, new Vector3(24.2f, g + 1.05f, -7.45f), 180);
            PointLight("Cafe / warm light", new Vector3(24, g + 2.55f, -6), 7, 1.1f, new Color(1, 0.84f, 0.62f));
            var sign = Text("Cafe / name", "БЫК И МЕДВЕДЬ", new Vector3(24, g + 3.25f, -1.97f), 48, 0.065f, new Color(0.98f, 0.86f, 0.6f));
            sign.transform.localEulerAngles = new Vector3(0, 180, 0);
        }
    }
}
