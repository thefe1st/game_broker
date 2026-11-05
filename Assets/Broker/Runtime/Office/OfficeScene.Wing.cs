using System;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // The executive wing west of the office (stage 6): the navy wall opens on a meeting room (north) and a lounge (south)
    // under one ceiling, x ∈ [-12.4, -6.2]. Walls, floor and ceiling are boxes with colliders like the office's, the west
    // wall and the partition panelled in wood; the furniture is low-poly (InteriorModels.Wing) over invisible colliders.
    // Decoration: nothing here touches the day.
    // Also the badges that float over the people instead of their names.
    public sealed partial class OfficeScene
    {
        // Doorways in the office's west wall: (middle z, width), WingDoorHeight high.
        private static readonly (float z, float width)[] WingDoors = { (2.7f, 1.4f), (-3.0f, 1.4f) };
        private const float WingDoorHeight = 2.4f, WingWest = -12.4f, WingEast = -6.2f, WingMidX = -9.3f;
        private Renderer wingCeiling;

        // The office's west wall with the two doorways: posts between them and lintels over them.
        private void BuildWestWall()
        {
            float z0 = -4.5f;
            foreach (var (z, width) in new[] { WingDoors[1], WingDoors[0] })
            {
                float a = z - width / 2, c = z + width / 2;
                Box("Left wall", new Vector3(-6.1f, 1.9f, (z0 + a) / 2), new Vector3(0.2f, 3.8f, a - z0), navy);
                Box("Left wall / lintel", new Vector3(-6.1f, (WingDoorHeight + 3.8f) / 2, z), new Vector3(0.2f, 3.8f - WingDoorHeight, width), navy);
                z0 = c;
            }
            Box("Left wall", new Vector3(-6.1f, 1.9f, (z0 + 4.5f) / 2), new Vector3(0.2f, 3.8f, 4.5f - z0), navy);
        }
        private void BuildWing(Material wall, Material floor, Material panel)
        {
            float w = WingEast - WingWest, mid = (WingEast + WingWest) / 2;
            Box("Wing / floor", new Vector3(mid, -0.15f, 0), new Vector3(w, 0.3f, 9.4f), floor);
            wingCeiling = Box("Wing / ceiling", new Vector3(mid, 3.9f, 0), new Vector3(w, 0.2f, 9.4f), cream).GetComponent<Renderer>();
            // The back wall with one window over the meeting table, the front and the west wall, the partition between the rooms.
            Box("Wing / back wall base", new Vector3(mid, 0.65f, 4.6f), new Vector3(w, 1.3f, 0.2f), wall);
            Box("Wing / back wall lintel", new Vector3(mid, 3.6f, 4.6f), new Vector3(w, 0.6f, 0.2f), wall);
            foreach (float x in new[] { -11.55f, -7.05f }) Box("Wing / back wall pier", new Vector3(x, 2.3f, 4.6f), new Vector3(1.7f, 2, 0.2f), wall);
            Box("Wing / invisible safety pane", new Vector3(WingMidX, 2.3f, 4.53f), new Vector3(2.8f, 1.96f, 0.05f), navy).GetComponent<Renderer>().enabled = false;
            Box("Wing / front wall", new Vector3(mid, 1.9f, -4.6f), new Vector3(w, 3.8f, 0.2f), wall);
            Box("Wing / west wall", new Vector3(WingWest + 0.1f, 1.9f, 0), new Vector3(0.2f, 3.8f, 9), panel);
            Box("Wing / partition", new Vector3(mid, 1.9f, 0), new Vector3(w, 3.8f, 0.2f), panel);
            // The panelled walls: the west wall of each room and both faces of the partition.
            const float inner = WingWest + 0.2f, length = WingEast - inner;
            foreach (float z in new[] { 2.3f, -2.3f })
                PlaceModel("panels/4.4", () => InteriorModels.WallPanels(4.4f, 3.8f), "Wing / wall panels", new Vector3(inner, 0, z), -90);
            PlaceModel("panels/" + length, () => InteriorModels.WallPanels(length, 3.8f), "Wing / wall panels", new Vector3((inner + WingEast) / 2, 0, 0.1f), 180);
            PlaceModel("panels/" + length, () => InteriorModels.WallPanels(length, 3.8f), "Wing / wall panels", new Vector3((inner + WingEast) / 2, 0, -0.1f));
            PlaceModel("window", InteriorModels.WindowFrame, "Wing / window frame", new Vector3(WingMidX, 1.285f, 4.47f));
            foreach (var (z, width) in WingDoors)
            {
                PlaceModel("casing/" + width, () => InteriorModels.DoorCasing(width, WingDoorHeight), "Wing / doorway casing", new Vector3(-6, 0, z), -90);
                PlaceModel("casing/" + width, () => InteriorModels.DoorCasing(width, WingDoorHeight), "Wing / doorway casing", new Vector3(-6.2f, 0, z), 90);
            }
            foreach (float z in new[] { 2.3f, -2.3f })
            {
                PlaceModel("ceiling light", InteriorModels.CeilingLight, "Wing / ceiling light", new Vector3(WingMidX, 3.8f, z));
                PointLight("Wing / warm light", new Vector3(WingMidX, 3.3f, z), 6.5f, 0.95f, new Color(1, 0.86f, 0.66f));
            }
            BuildMeetingRoom(); BuildLounge(); WingSkirting();
        }
        // The meeting room: the boardroom table under the window with five oxblood chairs, the head towards the trading
        // screen on the west wall, the credenza with the whisky and the bull on the partition, a plant in the corner.
        private void BuildMeetingRoom()
        {
            const float tableX = -9.4f, tableZ = 2.35f;
            PlaceModel("conference table", () => InteriorModels.ConferenceTable(3, 1.2f), "Wing / conference table", new Vector3(tableX, 0, tableZ));
            Blocker("Wing / conference table collider", new Vector3(tableX, 0.45f, tableZ), new Vector3(3, 0.9f, 1.2f));
            foreach (float x in new[] { -10.2f, -8.6f })
            {
                PlaceModel("chair/leather", () => InteriorModels.OfficeChair(VoxelPalette.BrickDark), "Wing / meeting chair", new Vector3(x, 0, tableZ - 1.1f), 180);
                PlaceModel("chair/leather", () => InteriorModels.OfficeChair(VoxelPalette.BrickDark), "Wing / meeting chair", new Vector3(x, 0, tableZ + 1.1f));
            }
            PlaceModel("chair/leather", () => InteriorModels.OfficeChair(VoxelPalette.BrickDark), "Wing / meeting chair", new Vector3(-7.35f, 0, tableZ), 90);
            PlaceModel("wall screen", () => InteriorModels.WallScreen(1.7f, 1), "Wing / trading screen", new Vector3(WingWest + 0.2f, 1.65f, tableZ), -90);
            const float credenzaZ = 0.1f + InteriorModels.CredenzaDepth / 2 + 0.01f;
            PlaceModel("credenza", () => InteriorModels.Credenza(1.8f), "Wing / credenza", new Vector3(tableX, 0, credenzaZ), 180);
            Blocker("Wing / credenza collider", new Vector3(tableX, InteriorModels.CredenzaHeight / 2, credenzaZ), new Vector3(1.8f, InteriorModels.CredenzaHeight, InteriorModels.CredenzaDepth));
            PlaceModel("decanter", InteriorModels.Decanter, "Wing / whisky", new Vector3(tableX - 0.45f, InteriorModels.CredenzaHeight, credenzaZ), 180);
            PlaceModel("bull figure", InteriorModels.BullFigure, "Wing / brass bull", new Vector3(tableX + 0.45f, InteriorModels.CredenzaHeight, credenzaZ), -90);
            PlaceBuilding("potted plant", () => InteriorModels.PottedPlant(4), StreetModels.ShrubLook, "Wing / potted plant", new Vector3(-11.8f, 0, 4.1f), 70);
            Blocker("Wing / plant collider", new Vector3(-11.8f, 0.5f, 4.1f), new Vector3(0.6f, 1, 0.6f));
        }
        // The lounge: the chesterfield on the west wall under the yacht, the coffee table on a rug between two armchairs,
        // the aquarium on the far wall, a floor lamp in the corner.
        private void BuildLounge()
        {
            const float z = -2.3f, sofaX = WingWest + 0.2f + InteriorModels.SofaDepth / 2 + 0.03f, tableX = -10.25f;
            PlaceModel("rug", () => InteriorModels.Rug(3.2f, 2.4f), "Wing / rug", new Vector3(-10.1f, 0, z), 90);
            PlaceModel("sofa/2.2", () => InteriorModels.Sofa(2.2f), "Wing / chesterfield", new Vector3(sofaX, 0, z), -90);
            Blocker("Wing / chesterfield collider", new Vector3(sofaX, 0.45f, z), new Vector3(InteriorModels.SofaDepth, 0.9f, 2.2f));
            PlaceModel("coffee table", () => InteriorModels.CoffeeTable(1.2f, 0.65f), "Wing / coffee table", new Vector3(tableX, 0, z), 90);
            Blocker("Wing / coffee table collider", new Vector3(tableX, InteriorModels.CoffeeTableHeight / 2, z), new Vector3(0.65f, InteriorModels.CoffeeTableHeight, 1.2f));
            foreach (float side in new[] { -1f, 1f })
            {
                float az = z + side * 1.35f;
                PlaceModel("sofa/1.05", () => InteriorModels.Sofa(1.05f), "Wing / armchair", new Vector3(tableX, 0, az), side > 0 ? 0 : 180);
                Blocker("Wing / armchair collider", new Vector3(tableX, 0.45f, az), new Vector3(1.05f, 0.9f, InteriorModels.SofaDepth));
            }
            PlaceModel("painting", () => InteriorModels.Painting(1.5f, 1), "Wing / yacht painting", new Vector3(WingWest + 0.2f, 1.85f, z), -90);
            const float aquariumZ = -1.15f, aquariumX = WingEast - InteriorModels.AquariumDepth / 2 - 0.02f;
            PlaceModel("aquarium", () => InteriorModels.Aquarium(1.6f), "Wing / aquarium", new Vector3(aquariumX, 0, aquariumZ), 90);
            Blocker("Wing / aquarium collider", new Vector3(aquariumX, 0.72f, aquariumZ), new Vector3(InteriorModels.AquariumDepth, 1.44f, 1.6f));
            PlaceModel("floor lamp", InteriorModels.FloorLamp, "Wing / floor lamp", new Vector3(-11.85f, 0, -3.95f));
            Blocker("Wing / floor lamp collider", new Vector3(-11.85f, 0.8f, -3.95f), new Vector3(0.36f, 1.6f, 0.36f));
            PointLight("Wing / lamp light", new Vector3(-11.85f, InteriorModels.FloorLampHeight - 0.2f, -3.95f), 3.5f, 0.55f, new Color(1, 0.8f, 0.55f));
        }
        // Skirting round both rooms, broken at the doorways and their casings.
        private void WingSkirting()
        {
            const float c = InteriorModels.CasingWidth, inner = WingWest + 0.2f, length = WingEast - inner;
            float mid = (inner + WingEast) / 2;
            Skirting(new Vector3(mid, 0, 4.5f), length, 0);
            Skirting(new Vector3(mid, 0, -4.5f), length, 180);
            Skirting(new Vector3(mid, 0, 0.1f), length, 180);
            Skirting(new Vector3(mid, 0, -0.1f), length, 0);
            foreach (var (z0, z1) in new[] { (0.1f, 4.5f), (-4.5f, -0.1f) }) Skirting(new Vector3(inner, 0, (z0 + z1) / 2), z1 - z0, -90);
            // Both faces of the office's west wall: the office side runs the whole wall, the wing side room by room.
            var office = new System.Collections.Generic.List<(float, float)>(); var wing = new System.Collections.Generic.List<(float, float)>();
            float from = -4.5f;
            foreach (var (z, width) in new[] { WingDoors[1], WingDoors[0] }) { office.Add((from, z - width / 2 - c)); from = z + width / 2 + c; }
            office.Add((from, 4.5f));
            foreach (var (z0, z1) in office)
                foreach (var (r0, r1) in new[] { (-4.5f, -0.1f), (0.1f, 4.5f) })
                {
                    float a = Mathf.Max(z0, r0), b = Mathf.Min(z1, r1);
                    if (b - a > 0.2f) wing.Add((a, b));
                }
            foreach (var (z0, z1) in office) Skirting(new Vector3(-6, 0, (z0 + z1) / 2), z1 - z0, -90);
            foreach (var (z0, z1) in wing) Skirting(new Vector3(-6.2f, 0, (z0 + z1) / 2), z1 - z0, 90);
        }

        // ---------- Badges ----------
        // A badge over a person instead of a name: one model of each kind under a root that Refresh turns to the camera;
        // Show keeps the one for what the person is doing.
        private sealed class PersonBadge
        {
            public Transform Root; public GameObject[] Kinds;
            public void Show(BadgeKind kind) { for (int k = 0; k < Kinds.Length; k++) if (Kinds[k].activeSelf != (k == (int)kind)) Kinds[k].SetActive(k == (int)kind); }
        }
        private PersonBadge Badge(Transform parent, Vector3 position)
        {
            var root = new GameObject("Office / badge"); root.transform.SetParent(parent, false); root.transform.localPosition = position;
            var kinds = new GameObject[4];
            foreach (BadgeKind kind in Enum.GetValues(typeof(BadgeKind)))
                kinds[(int)kind] = PlaceBuilding("badge/" + kind, () => InteriorModels.Badge(kind), StreetModels.FurnitureLook, "Office / badge " + kind, Vector3.zero, 0, false, null, root.transform);
            var badge = new PersonBadge { Root = root.transform, Kinds = kinds };
            badge.Show(BadgeKind.Talk);
            return badge;
        }
        // What a worker's badge shows: free to talk or take work, on the telephone for leads, on a dossier, on a break.
        private static BadgeKind WorkerBadge(BrokerDay day, EmployeeRecord worker, bool employeeMode)
        {
            if (employeeMode) return BadgeKind.Talk;
            if (day.IsFounder) return worker.Work == WorkKind.Leads || worker.Work == WorkKind.Research ? BadgeKind.Dossier : BadgeKind.Talk;
            switch (worker.Work)
            {
                case WorkKind.Leads: return BadgeKind.Calls;
                case WorkKind.Research: return BadgeKind.Dossier;
                case WorkKind.Break: return BadgeKind.Break;
                default: return BadgeKind.Talk;
            }
        }
    }
}
