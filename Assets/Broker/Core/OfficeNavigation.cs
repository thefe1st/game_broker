namespace Broker.Core
{
    public enum OfficeStation { Desk, Employee, Coffee, Upgrade, Exit, Street, ReturnOffice, CityContact, CityPickup, Supervisor, CityMap, CityCompanies, CityExchange, CityCafe, CrosswalkButton }
    public enum OfficeView { Explore, Desk, Employee, Coffee, Upgrade, Exit, Pause, CityContact, CityPickup, Supervisor, CityMap, CityCompanies, CityExchange, CityCafe }

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
                case OfficeStation.Supervisor: View = OfficeView.Supervisor; break;
                case OfficeStation.Desk: View = OfficeView.Desk; break;
                case OfficeStation.Employee:
                    if (employeeIndex < 0 || employeeIndex > 1) return false;
                    EmployeeIndex = employeeIndex; View = OfficeView.Employee; break;
                case OfficeStation.Coffee: View = OfficeView.Coffee; break;
                case OfficeStation.Upgrade: View = OfficeView.Upgrade; break;
                case OfficeStation.Exit: View = OfficeView.Exit; break;
                case OfficeStation.CityContact: View = OfficeView.CityContact; break;
                case OfficeStation.CityPickup: View = OfficeView.CityPickup; break;
                case OfficeStation.CityMap: View = OfficeView.CityMap; break;
                case OfficeStation.CityCompanies: View = OfficeView.CityCompanies; break;
                case OfficeStation.CityExchange: View = OfficeView.CityExchange; break;
                case OfficeStation.CityCafe: View = OfficeView.CityCafe; break;
                // The crosswalk button acts in the world and opens no panel.
                default: return false;
            }
            return true;
        }
        public void Pause() { View = OfficeView.Pause; }
        public void Close() { View = OfficeView.Explore; }
        public void Reset() { EmployeeIndex = 0; Close(); }
    }
}
