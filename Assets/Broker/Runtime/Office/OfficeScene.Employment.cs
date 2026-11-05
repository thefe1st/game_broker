using System.Collections.Generic;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed partial class OfficeScene
    {
        private readonly List<GameObject> ownerDisplay = new List<GameObject>();
        private GameObject supervisorRoot;
        private TextMesh roleLabel, supervisorLabel;
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
            // Gameplay boxes stay as hitboxes; the voxel desk and the low-poly figure carry the look. Orlov faces his desk (+Z).
            foreach (var box in new[] { desk, torso, supervisorBase }) box.GetComponent<Renderer>().enabled = false;
            PlaceVoxel("desk/supervisor", () => VoxelProps.Desk(2f, 0.85f, 0.8f, VoxelPalette.Wood, VoxelPalette.Navy),
                "Voxel supervisor desk", supervisorRoot.transform, new Vector3(-3.4f, 0, -3.0f), 180);
            var orlov = new GameObject("Orlov / figure"); orlov.transform.SetParent(supervisorRoot.transform, false);
            orlov.transform.localPosition = new Vector3(-3.4f, 0, -3.8f); orlov.transform.localRotation = Quaternion.Euler(0, 180, 0);
            PlacePerson("staff/" + PersonStyle.Orlov, StaffModel(PersonStyle.Orlov), orlov.transform, false);
            supervisorLabel = Text("Supervisor label", "ОРЛОВ / РУКОВОДИТЕЛЬ", new Vector3(-3.4f, 2.3f, -3.8f), 48, 0.028f, new Color(0.98f, 0.93f, 0.82f));
            supervisorLabel.transform.SetParent(supervisorRoot.transform, true);
            supervisorRoot.SetActive(false);
        }
        public void ConfigureCareerMode(CareerMode mode, bool founderAssistant = false)
        {
            employeeMode = mode == CareerMode.Employee || mode == CareerMode.EmployeeWork;
            supervisorRoot.SetActive(employeeMode);
            foreach (var obj in ownerDisplay) obj.SetActive(mode == CareerMode.Owner);
            for (int i = 0; i < people.Length; i++)
            { bool visible = mode != CareerMode.Founder || (founderAssistant && i == 0);
                people[i].gameObject.SetActive(visible); workerLabels[i].gameObject.SetActive(visible); }
            people[0].GetComponent<OfficeInteractable>().Configure(OfficeStation.Employee,
                mode == CareerMode.Founder ? "Лена / помощник по досье" : "Лена / порядок в хаосе",
                mode == CareerMode.Founder ? "Поручить досье / 25 с" : "Поговорить и дать поручение", 0);
            roleLabel.text = mode == CareerMode.Founder ? (founderAssistant ? "ОСНОВАТЕЛЬ / ПЕРВЫЙ ПОМОЩНИК" : "ОСНОВАТЕЛЬ / ПОКА ОДИН") : employeeMode ? "БРОКЕР / ЛИЧНЫЕ ПРОДАЖИ" : "ГЕНЕРАЛЬНЫЙ ДИРЕКТОР";
            coffeeStation.gameObject.layer = mode == CareerMode.Founder ? 2 : 0; // Hide inactive shop from the interaction ray.
            playerDesk.Configure(OfficeStation.Desk, mode == CareerMode.Founder ? "Ваш стол в своей конторе" : employeeMode ? "Ваше рабочее место брокера" : "Рабочее место директора", "Открыть клиентскую базу");
        }
    }
}
