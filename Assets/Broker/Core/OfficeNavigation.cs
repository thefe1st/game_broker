namespace Broker.Core
{
    public enum OfficeStation { Desk, Employee, Coffee, Upgrade, Exit, Street, ReturnOffice }
    public enum OfficeView { Explore, Desk, Employee, Coffee, Upgrade, Exit, Pause }

    // UI routing only. Never grants money, ends calls or completes staff tasks.
    public sealed class OfficeNavigation
    {
        public OfficeView View { get; private set; } = OfficeView.Explore;
        public int EmployeeIndex { get; private set; }
        public bool IsExploring => View == OfficeView.Explore;
        public bool RunsClock => View == OfficeView.Explore || View == OfficeView.Desk;
        public bool Open(OfficeStation station, int employeeIndex = 0)
        {
            switch (station)
            {
                case OfficeStation.Desk: View = OfficeView.Desk; break;
                case OfficeStation.Employee:
                    if (employeeIndex < 0 || employeeIndex > 1) return false;
                    EmployeeIndex = employeeIndex; View = OfficeView.Employee; break;
                case OfficeStation.Coffee: View = OfficeView.Coffee; break;
                case OfficeStation.Upgrade: View = OfficeView.Upgrade; break;
                case OfficeStation.Exit: View = OfficeView.Exit; break;
                default: return false;
            }
            return true;
        }
        public void Pause() { View = OfficeView.Pause; }
        public void Close() { View = OfficeView.Explore; }
        public void Reset() { EmployeeIndex = 0; Close(); }
    }
}
