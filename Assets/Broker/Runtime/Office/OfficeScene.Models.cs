using System;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        private GameObject deskPhone, goldenDeskPhone;
        private const float ExitSignY = 2.9f;

        // Low-poly visuals never own gameplay hitboxes or station references: the original boxes keep colliders and
        // interactables, only their renderers (and those of attached parts such as a handset) are hidden.
        private void HidePrimitiveVisuals(params string[] names)
        {
            // Only direct children are construction primitives; model objects carry other names.
            foreach (Transform child in transform)
                foreach (string name in names)
                    if (child.name == name)
                        foreach (var renderer in child.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }
        private GameObject PlaceModel(string key, Func<PolyMesh> build, string name, Vector3 position, float yaw = 0, Transform parent = null)
        { return PlaceBuilding(key, build, StreetModels.FurnitureLook, name, position, yaw, true, null, parent); }
        // Office interior: every model stands where its primitive stood, on the same footprint; see InteriorModels.
        private void BuildFurniture()
        {
            PlaceModel("desk/director", () => InteriorModels.Desk(DeskStyle.Executive, 3.6f, 1.25f, VoxelPalette.Wood, VoxelPalette.WoodDark),
                "Office / director desk", new Vector3(1, 0, -1.2f));
            HidePrimitiveVisuals("Director desk - interact", "Director desk leg", "Desk front panel");
            foreach (float x in new[] { -3.5f, 3.5f })
            {
                PlaceModel("desk/employee", () => InteriorModels.Desk(DeskStyle.Staff, 2.1f, 1.1f, VoxelPalette.WoodLight, VoxelPalette.Navy),
                    "Office / employee desk", new Vector3(x, 0, 1.45f));
                // The employee screen stays a primitive: Refresh recolours it by the worker's task. Monitor, keyboard and mouse
                // turn to the worker at +Z, the mouse by the right hand (−X).
                PlaceModel("monitor/employee", () => InteriorModels.Monitor(0.58f, 0.39f, 0.27f), "Office / employee monitor",
                    new Vector3(x, InteriorModels.DeskHeight, EmployeeScreenZ), 180);
                PlaceModel("keyboard", InteriorModels.Keyboard, "Office / employee keyboard", new Vector3(x, InteriorModels.DeskHeight, EmployeeKeysZ), 180);
                PlaceModel("mouse", InteriorModels.Mouse, "Office / employee mouse", new Vector3(x - 0.58f, InteriorModels.DeskHeight, EmployeeKeysZ));
                PlaceModel("papers/2", () => InteriorModels.Papers(0.4f, 0.3f, 2), "Office / employee papers", new Vector3(x + EmployeePapersDX, InteriorModels.DeskHeight, EmployeePapersZ), 180);
            }
            HidePrimitiveVisuals("Employee desktop", "Employee desk base", "Employee monitor", "Employee papers");
            // Chair primitives have no colliders and are replaced as a group.
            foreach (var position in new[] { new Vector3(1, 0, 0.05f), new Vector3(-3.5f, 0, 2.9f), new Vector3(3.5f, 0, 2.9f) })
                PlaceModel("chair", () => InteriorModels.OfficeChair(VoxelPalette.Navy), "Office / chair", position);
            HidePrimitiveVisuals("Chair seat", "Chair back", "Chair stem", "Chair base");
            // The director's workstation turned to the chair (+Z): the models' fronts face −Z, so the screen, the keyboard and
            // the telephone turn round; the mouse's wheel (−Z) points at the screen as it is. A banker's lamp in the far corner.
            PlaceModel("monitor/director", () => InteriorModels.Monitor(0.86f, 0.5f, 0.4f), "Office / director monitor",
                new Vector3(DirectorX, InteriorModels.DeskHeight, DirectorScreenZ), 180);
            HidePrimitiveVisuals("Director monitor", "Monitor stand");
            PlaceModel("keyboard", InteriorModels.Keyboard, "Office / keyboard", new Vector3(DirectorX, InteriorModels.DeskHeight, DirectorKeysZ), 180);
            HidePrimitiveVisuals("Keyboard");
            PlaceModel("mouse", InteriorModels.Mouse, "Office / mouse", new Vector3(DirectorX - 0.62f, InteriorModels.DeskHeight, DirectorKeysZ));
            PlaceModel("papers/1", () => InteriorModels.Papers(0.48f, 0.35f, 1), "Office / director papers", new Vector3(DirectorPapersX, InteriorModels.DeskHeight, DirectorPapersZ), 180);
            HidePrimitiveVisuals("Director papers");
            PlaceModel("bankers lamp", InteriorModels.BankersLamp, "Office / banker's lamp", new Vector3(2.4f, InteriorModels.DeskHeight, -1.58f), 180);
            // The desk phone: navy until the golden upgrade is bought (Refresh swaps the two).
            var phone = new Vector3(DirectorPhoneX, InteriorModels.DeskHeight, DirectorPhoneZ);
            deskPhone = PlaceModel("phone/black", () => InteriorModels.Phone(false), "Office / desk phone", phone, 180);
            goldenDeskPhone = PlaceModel("phone/golden", () => InteriorModels.Phone(true), "Office / golden desk phone", phone, 180);
            goldenDeskPhone.SetActive(false);
            HidePrimitiveVisuals("Telephone - interact");
            ownerDisplay.Add(PlaceModel("pedestal", InteriorModels.DisplayPedestal, "Office / display pedestal", new Vector3(5, 0, -2.6f)));
            ownerDisplay.Add(PlaceModel("phone/golden", () => InteriorModels.Phone(true), "Office / displayed golden phone", new Vector3(5, InteriorModels.PedestalTop, -2.6f)));
            HidePrimitiveVisuals("Premium display pedestal", "Golden phone display - interact");
            PlaceModel("coffee cabinet", InteriorModels.CoffeeCabinet, "Office / coffee cabinet", new Vector3(-5.25f, 0, -1.1f));
            PlaceModel("coffee machine", InteriorModels.CoffeeMachine, "Office / coffee machine", new Vector3(-5.25f, InteriorModels.CabinetTop, -1.1f));
            // The cup stands beside the drip tray, clear of the machine.
            PlaceModel("coffee cup", InteriorModels.CoffeeCup, "Office / coffee cup", new Vector3(-5.62f, InteriorModels.CabinetTop, -1.41f));
            HidePrimitiveVisuals("Coffee cabinet", "Coffee machine - interact", "Coffee front", "Coffee button", "Coffee cup");
            PlaceBuilding("potted plant", () => InteriorModels.PottedPlant(4), StreetModels.ShrubLook, "Office / potted plant", new Vector3(5.3f, 0, 3.3f));
            HidePrimitiveVisuals("Plant pot");
            // The door model's front (−Z) faces into the room; the green exit sign hangs on the wall over it.
            PlaceModel("door", InteriorModels.Door, "Office / exit door", new Vector3(0, 0, -4.5f), 180);
            PlaceModel("exit sign", InteriorModels.ExitSign, "Office / exit sign", new Vector3(0, ExitSignY, -4.5f), 180);
            HidePrimitiveVisuals("Door frame", "Exit door - interact", "Door handle");
            for (int i = 0; i < 3; i++)
                PlaceModel("window", InteriorModels.WindowFrame, "Office / window frame", new Vector3(-3.6f + i * 3.6f, 1.285f, 4.47f));
            HidePrimitiveVisuals("Window / frame side", "Window / frame rail", "Window mullion", "Window sill");
            for (int i = 0; i < 3; i++)
                PlaceModel("ceiling light", InteriorModels.CeilingLight, "Office / ceiling light", new Vector3(-3.5f + i * 3.5f, 3.8f, 0));
            HidePrimitiveVisuals("Ceiling light fixture");
            // The company's mark on the plaque instead of its name, on the board's face inside the brass frame.
            PlaceModel("plaque/" + PlaqueWidth, () => InteriorModels.Plaque(PlaqueWidth, 0.45f), "Office / company plaque", new Vector3(0, 3.42f, 4.35f));
            PlaceModel("logo/0.3", () => InteriorModels.CompanyLogo(0.3f), "Office / company logo", new Vector3(0, 3.42f, 4.35f - 0.0505f));
            HidePrimitiveVisuals("Company plaque");
            PlaceModel("elevator", InteriorModels.ElevatorDoor, "Office / street elevator door", new Vector3(6, 0, -0.2f), 90);
            HidePrimitiveVisuals("Office / street elevator", "Office / elevator light");
            // Skirting along the walls, broken at the door and the elevator.
            Skirting(new Vector3(0, 0, 4.5f), 12, 0);
            foreach (float x in new[] { -3.4125f, 3.4125f }) Skirting(new Vector3(x, 0, -4.5f), 5.175f, 180);
            Skirting(new Vector3(6, 0, -2.73f), 3.54f, 90);
            Skirting(new Vector3(6, 0, 2.53f), 3.94f, 90);
        }
        private void Skirting(Vector3 position, float length, float yaw)
        {
            PlaceModel("skirting/" + length, () => InteriorModels.Skirting(length), "Office / skirting", position, yaw);
        }
    }
}
