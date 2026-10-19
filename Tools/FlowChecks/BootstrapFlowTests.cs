using System;
using System.IO;
using System.Reflection;
using Broker.Core;
using Broker.Runtime;
using Broker.Runtime.Office;
using Broker.Runtime.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace Broker.FlowChecks
{
    // Runs real bootstrap methods with explicit scene/GUI/serialization doubles.
    // Does not instantiate OfficeScene geometry, Unity physics, fonts or JsonUtility.
    public sealed class BootstrapFlowTests
    {
        private PrototypeBootstrap game;
        private string folder;
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private T Get<T>(string name) => (T)typeof(PrototypeBootstrap).GetField(name, Flags).GetValue(game);
        private void Set(string name, object value) => typeof(PrototypeBootstrap).GetField(name, Flags).SetValue(game, value);
        private object Call(string name, params object[] args) => typeof(PrototypeBootstrap).GetMethod(name, Flags).Invoke(game, args);
        private BrokerDay Day => Get<BrokerDay>("day");
        private void Frame() { Call("Update"); Call("OnGUI"); }
        private void Click(string label) { GUI.Click = label; Call("OnGUI"); Assert.That(GUI.Click, Is.Null, "Expected enabled button: " + label); }
        private void Begin() { Click("Начать / продолжить вводную главу"); Click("Начать рабочий день"); }
        private void Interact(OfficeStation station)
        {
            var item = new OfficeInteractable(); item.Configure(station, "test", "test"); Call("OnWorldInteraction", item);
        }
        private void Confirm(string kind)
        {
            var t = typeof(PrototypeBootstrap).GetNestedType("ConfirmationKind", BindingFlags.NonPublic);
            Call("RequestConfirmation", Enum.Parse(t, kind));
        }
        private static void Sell(BrokerDay d, int i)
        {
            d.SelectClient(i); d.StartCall();
            d.Say("Обсудим отчёт по прошлому разговору. Какая цель? Есть риск убытка. Комиссия 2%. Срок год. Персональный отчёт.");
            d.OfferDeal();
            d.Say(i == 0 ? "Оставьте резерв на оборудование." : i == 1 ? "Условия в письменном договоре." : "У вас будет личный менеджер, буду на связи.");
            d.OfferDeal(); Assert.That(d.DealClosed, Is.True);
        }
        [SetUp] public void Setup()
        {
            folder = Path.Combine(Path.GetTempPath(), "broker-flow-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
            Application.persistentDataPath = folder; GUI.Reset(); OfficeInput.Escape = false; OfficeInput.Available = true;
            Time.unscaledDeltaTime = 1; Time.realtimeSinceStartupAsDouble = 0; UnityWebRequest.Sent = 0;
            game = new PrototypeBootstrap(); Call("Awake");
        }
        [TearDown] public void Cleanup() { Call("OnDestroy"); Directory.Delete(folder, true); }
        [Test] public void MenuPausesAndDoesNotWriteAnyProgressOrSendNetworkRequests()
        {
            for (int i = 0; i < 10; i++) Frame();
            Assert.That(Day.RemainingSeconds, Is.EqualTo(600)); Assert.That(Directory.GetFiles(folder), Is.Empty);
            Assert.That(Get<OfficeScene>("office").Player.ControlEnabled, Is.False); Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void EmploymentStartsWithCorrectSceneModeMoneyAndTabs()
        {
            Begin(); Interact(OfficeStation.Desk); Frame();
            Assert.That(Get<OfficeScene>("office").Mode, Is.EqualTo(CareerMode.Employee));
            Assert.That(Day.Cash + Day.TotalExpenses, Is.Zero);
            Assert.That(GUI.Labels, Does.Contain("Начальник")); Assert.That(GUI.Labels, Does.Contain("Связь"));
            Assert.That(GUI.Labels, Does.Not.Contain("Подготовить досье"));
        }
        [Test] public void IntroPausesThenExplorationAndDeskRunClock()
        {
            Click("Начать / продолжить вводную главу"); Frame(); Assert.That(Day.RemainingSeconds, Is.EqualTo(600));
            Click("Начать рабочий день"); Frame(); Assert.That(Day.RemainingSeconds, Is.EqualTo(599));
            Interact(OfficeStation.Desk); Frame(); Assert.That(Day.RemainingSeconds, Is.EqualTo(598));
        }
        [TestCase(OfficeStation.Supervisor)] [TestCase(OfficeStation.Employee)] [TestCase(OfficeStation.Coffee)] [TestCase(OfficeStation.Exit)]
        public void StationPanelsPauseWithoutEndingDayOrChargingPlayer(OfficeStation station)
        {
            Begin(); Interact(station); Frame();
            Assert.That(Day.RemainingSeconds, Is.EqualTo(600)); Assert.That(Day.DayEnded, Is.False); Assert.That(Day.Cash, Is.Zero);
        }
        [Test] public void VoiceDiagnosticsAndConfirmationDoNotRunClock()
        {
            Begin(); Call("OpenVoiceSetup"); Frame(); Assert.That(Day.RemainingSeconds, Is.EqualTo(600));
            Call("CloseUtilityOverlay"); Confirm("RestartDay"); Frame(); Assert.That(Day.RemainingSeconds, Is.EqualTo(600));
            Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void ColleaguePanelNeverOffersDelegation()
        {
            Begin(); Interact(OfficeStation.Employee); GUI.Labels.Clear(); Call("OnGUI");
            Assert.That(GUI.Labels, Does.Contain("Лена / коллега")); Assert.That(GUI.Labels, Does.Not.Contain("Обзвон"));
            Assert.That(GUI.Labels, Does.Not.Contain("Досье")); Assert.That(GUI.Labels, Does.Not.Contain("Перерыв"));
        }
        [Test] public void AgendaAndNextClientButtonFollowClosedCallWithoutMakingOne()
        {
            Begin(); Day.StartCall(); Day.EndCall(); Interact(OfficeStation.Desk); Click("Следующий клиент");
            Assert.That(Day.SelectedClientIndex, Is.EqualTo(1)); Assert.That(Day.Call, Is.EqualTo(CallStatus.NotStarted));
            Assert.That(Day.Cash, Is.Zero);
        }
        [Test] public void NextClientButtonCannotInterruptActiveCall()
        {
            Begin(); Day.StartCall(); Interact(OfficeStation.Desk); GUI.Click = "Следующий клиент"; Call("OnGUI");
            Assert.That(Day.SelectedClientIndex, Is.Zero); Assert.That(Day.Call, Is.EqualTo(CallStatus.Active));
            Assert.That(GUI.Click, Is.EqualTo("Следующий клиент")); GUI.Click = null;
        }
        [Test] public void DoorSettlementHappensExactlyOnceThenSavedMoneyCarries()
        {
            Begin(); Sell(Day, 0); Interact(OfficeStation.Exit); Click("Закончить день");
            for (int i = 0; i < 3; i++) Frame();
            Assert.That(Get<EmploymentState>("employment").PersonalSavings, Is.EqualTo(30));
            Assert.That(Get<EmploymentState>("employment").CompletedDays, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(folder, "broker-employee-intro-v1.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(folder, "broker-career-v1.json")), Is.False);
            Click("Следующий день"); Assert.That(Day.DayNumber, Is.EqualTo(2)); Assert.That(Day.OpeningCash, Is.EqualTo(30));
        }
        [Test] public void CancelModeConfirmationDoesNotLoseUnfinishedDay()
        {
            Begin(); Sell(Day, 0); var same = Day; Confirm("SelectMode"); Click("Остаться / Esc");
            Assert.That(Day, Is.SameAs(same)); Assert.That(Day.Cash, Is.EqualTo(30));
            Assert.That(Get<bool>("selectingMode"), Is.False);
        }
        [Test] public void ConfirmedSwitchDiscardsOnlyUnfinishedDayAndKeepsCompletedDay()
        {
            Begin(); Sell(Day, 0); Day.EndDay(); Frame(); Click("Следующий день"); Click("Начать рабочий день"); Sell(Day, 1);
            Confirm("SelectMode"); Click("Выбрать режим"); Click("Открыть кампанию владельца");
            Assert.That(Day.IsEmployee, Is.False); Assert.That(Day.Cash, Is.Zero); Assert.That(Day.TotalExpenses, Is.EqualTo(340));
            Call("LoadMode", CareerMode.Employee); Assert.That(Day.DayNumber, Is.EqualTo(2)); Assert.That(Day.OpeningCash, Is.EqualTo(30));
            Assert.That(Day.Earned, Is.Zero);
        }
        [Test] public void EmployeeResetDoesNotTouchOwnerSaveOrBackup()
        {
            Call("LoadMode", CareerMode.Owner); Day.EndDay(); Frame(); Call("NewCareer");
            string path = Path.Combine(folder, "broker-career-v1.json"); string main = File.ReadAllText(path), backup = File.ReadAllText(path + ".bak");
            Call("LoadMode", CareerMode.Employee); Call("NewCareer");
            Assert.That(File.ReadAllText(path), Is.EqualTo(main)); Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
        }
        [Test] public void FinishedIntroLoadsSummaryRatherThanDayFourOrDuplicatePayouts()
        {
            Begin(); for (int n = 0; n < 3; n++)
            {
                for (int i = 0; i < 3; i++) Sell(Day, i);
                Day.EndDay(); Frame(); if (n < 2) { Click("Следующий день"); Click("Начать рабочий день"); }
            }
            Assert.That(Get<EmploymentState>("employment").PersonalSavings, Is.EqualTo(330));
            Call("LoadMode", CareerMode.Employee); Frame();
            Assert.That(Day.DayEnded, Is.True); Assert.That(Day.DayNumber, Is.EqualTo(3));
            Assert.That(Get<EmploymentState>("employment").PersonalSavings, Is.EqualTo(330));
            Assert.That(GUI.Labels, Does.Contain("ВВОДНАЯ ГЛАВА ПРОЙДЕНА / НЕ ФИНАЛ КАРЬЕРЫ"));
        }
        [Test] public void HiddenCityStationsCannotOpenAnyWindow()
        {
            Begin(); Get<OfficeScene>("office").VisitCity(true);
            Interact(OfficeStation.CityContact); Interact(OfficeStation.CityPickup);
            Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True); Assert.That(Day.CityWorkTaken, Is.False);
        }
        [Test] public void FailedWriteBlocksModeSwitchAndCanBeRetriedWithoutDuplicatePayout()
        {
            Begin(); Sell(Day, 0);
            // A directory at the file path makes the atomic save fail without platform permissions tricks.
            string path = Path.Combine(folder, "broker-employee-intro-v1.json"); Directory.CreateDirectory(path);
            Day.EndDay(); Frame(); Assert.That(Get<bool>("saveSucceeded"), Is.False);
            Confirm("SelectMode"); GUI.Click = "Выбрать режим"; Call("OnGUI"); Assert.That(Get<bool>("selectingMode"), Is.False);
            Call("CloseUtilityOverlay"); GUI.Click = null; Directory.Delete(path);
            Click("Повторить сохранение"); Assert.That(Get<bool>("saveSucceeded"), Is.True);
            Assert.That(Get<EmploymentState>("employment").PersonalSavings, Is.EqualTo(30));
        }
    }
}
