using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    public sealed class OfficeInteractable : MonoBehaviour
    {
        public OfficeStation Station { get; private set; }
        public int EmployeeIndex { get; private set; }
        public string Title { get; private set; }
        public string Action { get; private set; }
        public void Configure(OfficeStation station, string title, string action, int employeeIndex = 0)
        { Station = station; Title = title; Action = action; EmployeeIndex = employeeIndex; }
    }
}
