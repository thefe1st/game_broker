using System;
using System.IO;
using Broker.Core;
using Broker.Runtime.Office;
using Broker.Runtime.Voice;
using NUnit.Framework;
using UnityEngine;

namespace Broker.FlowChecks
{
    public sealed partial class BootstrapFlowTests
    {
        [TestCase(OfficeStation.CityMap)] [TestCase(OfficeStation.CityCompanies)]
        public void NewCityStandsRequireStreetAndNeverGrantMoneyOrDiscloseStock(OfficeStation station)
        {
            Begin(); Interact(station); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Interact(OfficeStation.Street); Interact(station); float seconds = Day.RemainingSeconds; Frame();
            Assert.That(Day.RemainingSeconds, Is.EqualTo(seconds)); Assert.That(Day.Cash, Is.Zero); Assert.That(Day.RisksExplained || Day.FeesExplained, Is.False);
            Assert.That(UnityEngine.Networking.UnityWebRequest.Sent, Is.Zero);
            if (station == OfficeStation.CityCompanies) foreach (var stock in StockCatalog.Offers) Assert.That(NotebookText(), Does.Contain(stock.Name));
            Click("Назад · Esc"); Frame(); Assert.That(Day.RemainingSeconds, Is.LessThan(seconds));
            Interact(OfficeStation.ReturnOffice); Assert.That(Get<OfficeScene>("office").IsInCity, Is.False); Assert.That(Day.DayEnded, Is.False);
        }
        [Test] public void MapWaypointIsOnlyNavigationNotFastTravelSaveOrTaskReward()
        {
            OpenFirm(false); HireFirstAssistant(); Click("Начать день своей конторы"); Day.AssignTask(0, WorkKind.Research, 0);
            Interact(OfficeStation.Street); var office = Get<OfficeScene>("office"); float x = office.MapX, z = office.MapZ;
            var files = Directory.GetFiles(folder); var bytes = Array.ConvertAll(files, File.ReadAllText);
            Interact(OfficeStation.CityMap); Click("Ориентир / Западный сквер"); Frame();
            Assert.That(Get<string>("cityDestinationId"), Is.EqualTo("park")); Assert.That(office.MapX, Is.EqualTo(x)); Assert.That(office.MapZ, Is.EqualTo(z));
            Assert.That(Day.Employees[0].Remaining, Is.EqualTo(25)); Assert.That(Day.Current.Researched, Is.False);
            Click("Назад · Esc"); Assert.That(NotebookText(), Does.Contain("Западный сквер"));
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
        }
        [Test] public void MapFromPausePreservesDeskCallAndDoesNotTeleportOutOfOffice()
        {
            Begin(); Interact(OfficeStation.Desk); Click("Позвонить"); Set("message", "Городской черновик");
            Call("PauseGameplay"); Click("Карта района"); Frame();
            Assert.That(Get<OfficeScene>("office").IsInCity, Is.False); Assert.That(NotebookText(), Does.Contain("Карта не переносит"));
            Click("Ориентир / Офисная улица"); OfficeInput.Escape = true; Call("Update");
            Assert.That(Get<OfficeNavigation>("navigation").View, Is.EqualTo(OfficeView.Pause)); Click("Продолжить · Esc");
            Assert.That(Get<OfficeNavigation>("navigation").View, Is.EqualTo(OfficeView.Desk));
            Assert.That(Day.Call, Is.EqualTo(CallStatus.Active)); Assert.That(Get<string>("message"), Is.EqualTo("Городской черновик"));
        }
        [Test] public void FocusOrRecordingCannotOpenStreetInformationStands()
        {
            Begin(); Interact(OfficeStation.Street); Call("OnApplicationFocus", false); Interact(OfficeStation.CityMap);
            Assert.That(Get<OfficeNavigation>("navigation").View, Is.EqualTo(OfficeView.Pause));
            Call("OnApplicationFocus", true); Click("Продолжить · Esc"); Get<VoiceSession>("voice").BeginRecording(true);
            Interact(OfficeStation.CityCompanies); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Get<VoiceSession>("voice").Cancel(); Interact(OfficeStation.CityMap); Call("OnApplicationFocus", false);
            GUI.Click = "Ориентир / Западный сквер"; Call("OnGUI"); Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null;
        }
    }
}
