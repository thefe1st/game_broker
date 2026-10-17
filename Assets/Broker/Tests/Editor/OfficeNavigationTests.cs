using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class OfficeNavigationTests
    {
        [Test] public void StartsInExplorationWithRunningClock()
        { var n = new OfficeNavigation(); Assert.That(n.IsExploring, Is.True); Assert.That(n.RunsClock, Is.True); }
        [Test] public void DeskUsesExistingManagementView()
        { var n = new OfficeNavigation(); Assert.That(n.Open(OfficeStation.Desk), Is.True); Assert.That(n.View, Is.EqualTo(OfficeView.Desk)); Assert.That(n.RunsClock, Is.True); }
        [Test] public void StaffPanelPreservesEmployeeIndexAndPausesClock()
        { var n = new OfficeNavigation(); n.Open(OfficeStation.Employee, 1); Assert.That(n.EmployeeIndex, Is.EqualTo(1)); Assert.That(n.View, Is.EqualTo(OfficeView.Employee)); Assert.That(n.RunsClock, Is.False); }
        [Test] public void InvalidEmployeeDoesNotChangeView()
        { var n = new OfficeNavigation(); Assert.That(n.Open(OfficeStation.Employee, -1), Is.False); Assert.That(n.Open(OfficeStation.Employee, 2), Is.False); Assert.That(n.IsExploring, Is.True); }
        [Test] public void InvalidStationDoesNotChangeView()
        { var n = new OfficeNavigation(); Assert.That(n.Open((OfficeStation)999), Is.False); Assert.That(n.IsExploring, Is.True); }
        [Test] public void CoffeeAndUpgradePauseWithoutSpendingMoney()
        {
            var n = new OfficeNavigation(); var day = new BrokerDay();
            n.Open(OfficeStation.Coffee); Assert.That(n.RunsClock, Is.False);
            n.Open(OfficeStation.Upgrade); Assert.That(n.View, Is.EqualTo(OfficeView.Upgrade)); Assert.That(day.Cash, Is.Zero);
        }
        [Test] public void DoorOnlyOpensConfirmationAndDoesNotEndDay()
        { var n = new OfficeNavigation(); var day = new BrokerDay(); n.Open(OfficeStation.Exit); Assert.That(n.RunsClock, Is.False); Assert.That(day.DayEnded, Is.False); }
        [Test] public void PauseAndCloseReturnToExploration()
        { var n = new OfficeNavigation(); n.Pause(); Assert.That(n.View, Is.EqualTo(OfficeView.Pause)); Assert.That(n.RunsClock, Is.False); n.Close(); Assert.That(n.IsExploring, Is.True); }
        [Test] public void ResetClearsEmployeeSelection()
        { var n = new OfficeNavigation(); n.Open(OfficeStation.Employee, 1); n.Reset(); Assert.That(n.EmployeeIndex, Is.Zero); Assert.That(n.IsExploring, Is.True); }
        [Test] public void OpeningPanelsDoesNotResetExistingCall()
        {
            var day = new BrokerDay(); day.StartCall(); var n = new OfficeNavigation(); n.Open(OfficeStation.Desk); n.Close(); n.Open(OfficeStation.Employee, 1);
            Assert.That(day.Call, Is.EqualTo(CallStatus.Active)); Assert.That(day.Trust, Is.EqualTo(1));
        }
    }
}
