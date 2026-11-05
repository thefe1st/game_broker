using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        private readonly List<GameObject> ownerDisplay = new List<GameObject>();
        private GameObject supervisorRoot;
        private PersonBadge supervisorBadge;
        private OfficeInteractable playerDesk;
        private bool employeeMode;
        private OfficeInteractable coffeeStation;

        private void BuildSupervisor()
        {
            supervisorRoot = new GameObject("Orlov - employer supervisor");
            supervisorRoot.transform.SetParent(transform, false);
            var desk = Shape(PrimitiveType.Cube, "Supervisor desk", supervisorRoot.transform,
                new Vector3(-3.4f, 0.76f, -3.0f), new Vector3(2, 0.15f, 0.8f), wood, true);
            Interactable(desk, OfficeStation.Supervisor, "Орлов / руководитель отдела", "Обсудить план и результаты");
            var supervisorBase = Shape(PrimitiveType.Cube, "Supervisor desk base", supervisorRoot.transform,
                new Vector3(-3.4f, 0.36f, -3.1f), new Vector3(1.6f, 0.72f, 0.6f), navy, true);
            var torso = Shape(PrimitiveType.Cube, "Orlov / suit", supervisorRoot.transform,
                new Vector3(-3.4f, 1.12f, -3.8f), new Vector3(0.56f, 0.72f, 0.34f), navy, true);
            Interactable(torso, OfficeStation.Supervisor, "Орлов / руководитель отдела", "Обсудить план и результаты");
            // Gameplay boxes stay as hitboxes; the low-poly desk and figure carry the look. Orlov faces his desk (+Z).
            foreach (var box in new[] { desk, torso, supervisorBase }) box.GetComponent<Renderer>().enabled = false;
            PlaceModel("desk/supervisor", () => InteriorModels.Desk(DeskStyle.Staff, 2f, 0.8f, VoxelPalette.Wood, VoxelPalette.Navy),
                "Office / supervisor desk", new Vector3(-3.4f, 0, -3.0f), 180, supervisorRoot.transform);
            var orlov = new GameObject("Orlov / figure"); orlov.transform.SetParent(supervisorRoot.transform, false);
            orlov.transform.localPosition = new Vector3(-3.4f, 0, -3.8f); orlov.transform.localRotation = Quaternion.Euler(0, 180, 0);
            PlacePerson("staff/" + PersonStyle.Orlov, StaffModel(PersonStyle.Orlov), orlov.transform, false);
            supervisorBadge = Badge(supervisorRoot.transform, new Vector3(-3.4f, BadgeY + 0.05f, -3.8f));
            supervisorRoot.SetActive(false);
        }
        public void ConfigureCareerMode(CareerMode mode, bool founderAssistant = false)
        {
            employeeMode = mode == CareerMode.Employee || mode == CareerMode.EmployeeWork;
            supervisorRoot.SetActive(employeeMode);
            foreach (var obj in ownerDisplay) obj.SetActive(mode == CareerMode.Owner);
            for (int i = 0; i < people.Length; i++)
            { bool visible = mode != CareerMode.Founder || (founderAssistant && i == 0);
                people[i].gameObject.SetActive(visible); badges[i].Root.gameObject.SetActive(visible); }
            people[0].GetComponent<OfficeInteractable>().Configure(OfficeStation.Employee,
                mode == CareerMode.Founder ? "Лена / помощник по досье" : "Лена / порядок в хаосе",
                mode == CareerMode.Founder ? "Поручить досье / 25 с" : "Поговорить и дать поручение", 0);
            coffeeStation.gameObject.layer = mode == CareerMode.Founder ? 2 : 0; // Hide inactive shop from the interaction ray.
            playerDesk.Configure(OfficeStation.Desk, mode == CareerMode.Founder ? "Ваш стол в своей конторе" : employeeMode ? "Ваше рабочее место брокера" : "Рабочее место директора", "Открыть клиентскую базу");
        }
    }
}
