using System;
using System.IO;
using Broker.Core;
using Broker.Runtime;
using Broker.Runtime.Office;
using Broker.Runtime.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace Broker.FlowChecks
{
    public sealed partial class BootstrapFlowTests
    {
        private string FirmPath => Path.Combine(folder, "broker-founder-v3.json");
        // Real v1/v2 founder JSON has no stock fields; explicit empty values are accepted too.
        private static FirmCareerSave LegacyFirm(FirmCareerSave s, int version)
        {
            s.schemaVersion = version; s.stockHistoryFromDay = 0;
            foreach (var clients in new[] { s.origin.clients, s.checkpoint.clients }) foreach (var c in clients) { c.lastStockId = ""; c.lastStockDay = 0; }
            foreach (var r in s.reports) r.soldStockIds = null;
            return s;
        }
        private static void SellAny(BrokerDay d, int i)
        {
            Assert.That(d.SelectClient(i), Is.True); d.StartCall();
            d.Say("Обсудим отчёт по прошлой сделке. Какая цель? Есть риск убытка. Комиссия 2%. Срок год. Персональный отчёт.");
            d.OfferDeal(); d.Say(ClientConversation.AnswerIntent(d.Current.Id) == DialogueIntent.Reserve ? "Оставьте резерв на оборудование."
                : ClientConversation.AnswerIntent(d.Current.Id) == DialogueIntent.Written ? "Все условия в письменном договоре." : "Личный менеджер, буду на связи.");
            d.OfferDeal(); Assert.That(d.DealClosed, Is.True, d.Current.Id + ": " + d.NextConversationStep);
        }
        private void ReadyForFirm()
        {
            StartWork(true);
            while (!FirmCareerState.CanOpen(Get<WorkCareerState>("workCareer")))
            {
                for (int i = 0; i < 3; i++) SellAny(Day, i); Day.EndDay(); Frame();
                if (!FirmCareerState.CanOpen(Get<WorkCareerState>("workCareer"))) { Click("Следующий рабочий день"); Click("Начать рабочий день"); }
            }
        }
        private void OpenFirm(bool begin = true)
        {
            ReadyForFirm(); Click("Карьера"); Click("Открыть свою контору"); Click("Да, открыть свою контору");
            if (begin) Click("Начать день своей конторы");
        }
        private void FreshGame() { Call("OnDestroy"); game = new PrototypeBootstrap(); Call("Awake"); }
        private void HireFirstAssistant()
        { Click("Нанять помощника / $100"); Click("Да, нанять за $100"); }
        [TestCase(false)] [TestCase(true)]
        public void HiringCancellationDoesNotSpendOrChangeSavedDay(bool escape)
        {
            OpenFirm(false); var d = Day; var files = Directory.GetFiles(folder); var bytes = Array.ConvertAll(files, File.ReadAllText);
            Click("Нанять помощника / $100"); float seconds = Day.RemainingSeconds; Frame();
            Assert.That(NotebookText(), Does.Contain("Нанять помощника?")); Assert.That(Day.RemainingSeconds, Is.EqualTo(seconds));
            if (escape) { OfficeInput.Escape = true; Call("Update"); } else Click("Нет, без помощника / Esc");
            Assert.That(OverlayOpen, Is.False); Assert.That(Day, Is.SameAs(d)); Assert.That(Day.Cash, Is.EqualTo(300));
            Assert.That(Get<FirmCareerState>("firmCareer").HasAssistant, Is.False);
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
        }
        [TestCase(false)] [TestCase(true)]
        public void HireFromMorningOrResultsPersistsBeforeStartingNextDay(bool results)
        {
            OpenFirm(results); if (results) { Day.EndDay(); Frame(); }
            int opening = Get<FirmCareerState>("firmCareer").Balance, next = Get<FirmCareerState>("firmCareer").NextDayNumber;
            var files = Directory.GetFiles(folder, "broker-employee-*"); var bytes = Array.ConvertAll(files, File.ReadAllText);
            HireFirstAssistant(); Assert.That(Get<bool>("intro"), Is.True); Assert.That(OverlayOpen, Is.False);
            Assert.That(Day.DayNumber, Is.EqualTo(next)); Assert.That(Day.Cash, Is.EqualTo(opening - 100)); Assert.That(Day.DailyPayroll, Is.EqualTo(40));
            Assert.That(Get<OfficeScene>("office").FounderAssistant, Is.True);
            FreshGame(); Click("Продолжить свою контору"); Assert.That(Day.HasFounderAssistant, Is.True);
            Assert.That(Day.Cash, Is.EqualTo(opening - 100));
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
            Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void FailedHiringWriteStaysOnConfirmationAndRetryDoesNotDoubleSpend()
        {
            OpenFirm(false); Click("Нанять помощника / $100"); string bytes = File.ReadAllText(FirmPath);
            Set("firmStorage", new FirmCareerStorage(FirmPath, _ => throw new Exception(), JsonUtility.FromJson<FirmCareerSave>));
            Click("Да, нанять за $100"); Assert.That(OverlayOpen, Is.True); Assert.That(NotebookText(), Does.Contain("Не удалось записать"));
            Assert.That(Day.Cash, Is.EqualTo(300)); Assert.That(Get<FirmCareerState>("firmCareer").HasAssistant, Is.False);
            Assert.That(File.ReadAllText(FirmPath), Is.EqualTo(bytes)); Assert.That(Get<OfficeScene>("office").FounderAssistant, Is.False);
            Set("firmStorage", new FirmCareerStorage(FirmPath, JsonUtility.ToJson, JsonUtility.FromJson<FirmCareerSave>));
            Click("Да, нанять за $100"); Call("HireAssistant"); Assert.That(Day.Cash, Is.EqualTo(200));
            Assert.That(Get<FirmCareerState>("firmCareer").HasAssistant, Is.True);
        }
        [TestCase("changed")] [TestCase("broken")] [TestCase("hired")]
        public void HireConfirmationCannotOverwriteChangedOrUnreadableProgress(string kind)
        {
            OpenFirm(false); Click("Нанять помощника / $100"); var store = Get<FirmCareerStorage>("firmStorage");
            var firm = Get<FirmCareerState>("firmCareer");
            if (kind == "broken") File.WriteAllText(FirmPath, "unknown founder");
            else if (kind == "hired") Assert.That(store.TryHireAssistant(firm, out _, out _), Is.True);
            else { var changed = firm.RestartFromOrigin(); var d = changed.CreateDay(); d.EndDay(); changed.CompleteDay(d); store.TrySave(changed, out _); }
            string bytes = File.ReadAllText(FirmPath); Click("Да, нанять за $100");
            Assert.That(OverlayOpen, Is.True); Assert.That(NotebookText(), Does.Contain("Найм не записан"));
            Assert.That(firm.HasAssistant, Is.False); Assert.That(Day.Cash, Is.EqualTo(300)); Assert.That(File.ReadAllText(FirmPath), Is.EqualTo(bytes));
        }
        [Test] public void HireIsBlockedMidDayEvenWithEarnedCashAndDuringFailedSettlement()
        {
            OpenFirm(); Interact(OfficeStation.Desk); SellAny(Day, 0); var d = Day; string bytes = File.ReadAllText(FirmPath);
            Call("RequestAssistant"); Assert.That(OverlayOpen, Is.False); Assert.That(Day, Is.SameAs(d));
            Assert.That(File.ReadAllText(FirmPath), Is.EqualTo(bytes));
            Day.EndDay(); Frame(); Set("saveSucceeded", false); Call("RequestAssistant"); Assert.That(OverlayOpen, Is.False);
            GUI.Click = "Нанять помощника / $100"; Call("OnGUI"); Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null;
            Click("Повторить сохранение"); HireFirstAssistant(); Assert.That(Day.HasFounderAssistant, Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void HireConfirmationDoesNotOverwriteDamagedOrMissingPrimaryWithMatchingBackup(bool missing)
        {
            OpenFirm(false); Get<FirmCareerStorage>("firmStorage").TrySave(Get<FirmCareerState>("firmCareer"), out _);
            Click("Нанять помощника / $100");
            if (missing) File.Delete(FirmPath); else File.WriteAllText(FirmPath, "unknown newer founder");
            var files = Directory.GetFiles(folder); var bytes = Array.ConvertAll(files, File.ReadAllText);
            Click("Да, нанять за $100"); Assert.That(OverlayOpen, Is.True);
            Assert.That(NotebookText(), Does.Contain("Найм не записан")); Assert.That(Day.HasFounderAssistant, Is.False);
            Assert.That(Day.Cash, Is.EqualTo(300)); CollectionAssert.AreEquivalent(files, Directory.GetFiles(folder));
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
            Click("Нет, без помощника / Esc"); Click("Выбрать режим"); Click("Продолжить свою контору");
            Click("Начать день своей конторы"); Day.EndDay(); Frame();
            HireFirstAssistant(); Assert.That(Day.Cash, Is.EqualTo(140)); Assert.That(Day.HasFounderAssistant, Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void DossierPanelsExplainReadyBusyCompletedAndClosedTargets(bool physical)
        {
            OpenFirm(false); HireFirstAssistant(); Click("Начать день своей конторы");
            Interact(physical ? OfficeStation.Employee : OfficeStation.Desk); if (!physical) Click("Контора");
            Assert.That(NotebookText(), Does.Contain("Можно поручить досье"));
            Click("Подготовить досье / 25 с");
            if (physical) Interact(OfficeStation.Employee);
            Assert.That(NotebookText(), Does.Contain("Готовится досье:"));
            Click(Day.Clients[1].Name); Assert.That(NotebookText(), Does.Contain("Сначала дождитесь текущего досье"));
            Day.AdvanceTime(25); Click(Day.Clients[0].Name); Assert.That(NotebookText(), Does.Contain("уже готово"));
            GUI.Click = "Подготовить досье / 25 с"; Call("OnGUI"); Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null;
            Click(Day.Clients[1].Name); Day.SelectClient(1); Day.StartCall(); Day.EndCall();
            Assert.That(NotebookText(), Does.Contain("Звонок завершён: досье сегодня недоступно"));
        }
        [Test] public void EndingCallReleasesAssistantAndShowsCancelledDossierInProfile()
        {
            OpenFirm(false); HireFirstAssistant(); Click("Начать день своей конторы"); Interact(OfficeStation.Desk);
            Click("Позвонить"); Click("Контора"); Click("Подготовить досье / 25 с"); Click("Звонки");
            Click("Завершить звонок"); Click("Да, завершить без сделки"); Frame();
            Assert.That(Day.Employees[0].Work, Is.EqualTo(WorkKind.Idle)); Assert.That(Day.Current.Researched, Is.False);
            Assert.That(Get<System.Collections.Generic.List<string>>("officeNews"), Has.Some.Contains("отменено без бонуса"));
            Call("OpenCallLog", true); Assert.That(NotebookText(), Does.Contain("Звонок завершён: досье сегодня недоступно"));
        }
        [Test] public void LowCashOrDebtCannotHireAndDoesNotUseReserve()
        {
            OpenFirm(); for (int n = 0; n < 3; n++) { Day.EndDay(); Frame(); if (n < 2) { Click("Следующий день конторы"); Click("Начать день своей конторы"); } }
            int reserve = Get<FirmCareerState>("firmCareer").PersonalReserve;
            Call("RequestAssistant"); Assert.That(OverlayOpen, Is.False);
            GUI.Click = "Нанять помощника / $100"; Call("OnGUI"); Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null;
            Assert.That(Get<FirmCareerState>("firmCareer").Balance, Is.EqualTo(120)); Assert.That(Get<FirmCareerState>("firmCareer").PersonalReserve, Is.EqualTo(reserve));
        }
        [Test] public void AssistantDeskDossierRunsOnlyInGameplayAndPreservesCallDraft()
        {
            OpenFirm(false); HireFirstAssistant(); Click("Начать день своей конторы"); Interact(OfficeStation.Desk);
            Day.StartCall(); Set("message", "Черновик"); Click("Контора"); string text = NotebookText();
            Assert.That(text, Does.Contain("первый помощник")); Assert.That(text, Does.Not.Contain("Обзвон")); Assert.That(text, Does.Not.Contain("Настроение"));
            int trust = Day.Trust; Click("Подготовить досье / 25 с"); Assert.That(Day.Employees[0].Remaining, Is.EqualTo(25));
            Call("OpenWorkNotebook", false); Frame(); Assert.That(Day.Employees[0].Remaining, Is.EqualTo(25)); Click("Вернуться к работе / Esc");
            Time.unscaledDeltaTime = 24; Frame(); Assert.That(Day.Current.Researched, Is.False);
            Time.unscaledDeltaTime = 1; Frame(); Assert.That(Day.Current.Researched, Is.True); Assert.That(Day.Trust, Is.EqualTo(Math.Min(4, trust + 1)));
            Click("Звонки"); Assert.That(Get<string>("message"), Is.EqualTo("Черновик")); Assert.That(Day.Call, Is.EqualTo(CallStatus.Active));
            Assert.That(Day.Earned, Is.Zero); Assert.That(Day.Cash, Is.EqualTo(200)); Assert.That(Day.FeesExplained || Day.RisksExplained, Is.False);
        }
        [Test] public void PhysicalAssistantStationAllowsOnlyDossierAndPausesTaskUntilReturn()
        {
            OpenFirm(false); HireFirstAssistant(); Click("Начать день своей конторы"); Interact(OfficeStation.Employee);
            Assert.That(Get<OfficeNavigation>("navigation").View, Is.EqualTo(OfficeView.Employee));
            string text = NotebookText(); Assert.That(text, Does.Contain("помощник по досье"));
            Assert.That(GUI.Labels, Does.Not.Contain("Обзвон")); Assert.That(GUI.Labels, Does.Not.Contain("Перерыв"));
            Click("Подготовить досье / 25 с"); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Frame(); Assert.That(Day.Employees[0].Remaining, Is.EqualTo(24));
            Interact(OfficeStation.Employee); Frame(); Assert.That(Day.Employees[0].Remaining, Is.EqualTo(24)); Click("Назад · Esc");
            var max = new OfficeInteractable(); max.Configure(OfficeStation.Employee, "Макс", "test", 1); Call("OnWorldInteraction", max);
            Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            foreach (var station in new[] { OfficeStation.Supervisor, OfficeStation.Upgrade, OfficeStation.Coffee }) Interact(station);
            Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True); Assert.That(Day.Spent, Is.Zero);
        }
        [Test] public void SalarySettlesOnceEvenOnEarlyExitFailedWriteAndRetry()
        {
            OpenFirm(false); HireFirstAssistant(); Click("Начать день своей конторы");
            File.Delete(FirmPath); Directory.CreateDirectory(FirmPath); Interact(OfficeStation.Exit); Click("Закончить день конторы");
            for (int n = 0; n < 3; n++) Frame(); var firm = Get<FirmCareerState>("firmCareer");
            Assert.That(firm.Balance, Is.EqualTo(100)); Assert.That(firm.LastReport.salary, Is.EqualTo(40)); Assert.That(Get<bool>("saveSucceeded"), Is.False);
            Assert.That(NotebookText(), Does.Contain("− $40 зарплаты")); Directory.Delete(FirmPath); Click("Повторить сохранение"); Frame();
            Assert.That(firm.Balance, Is.EqualTo(100)); Assert.That(firm.CompletedDays, Is.EqualTo(1));
            Click("Следующий день конторы"); Click("Начать день своей конторы"); Interact(OfficeStation.Desk); Click("История дней");
            Assert.That(NotebookText(), Does.Contain("− $40 зарплаты"));
        }
        [Test] public void RestartKeepsHireButDropsUnfinishedDossierAndResetRemovesHire()
        {
            OpenFirm(false); HireFirstAssistant(); Click("Начать день своей конторы"); Day.AssignTask(0, WorkKind.Research, 0); Day.AdvanceTime(20);
            string bytes = File.ReadAllText(FirmPath); Confirm("RestartDay"); Click("Да, начать день заново");
            Assert.That(Day.HasFounderAssistant, Is.True); Assert.That(Day.Cash, Is.EqualTo(200)); Assert.That(Day.Employees[0].Work, Is.EqualTo(WorkKind.Idle));
            Assert.That(File.ReadAllText(FirmPath), Is.EqualTo(bytes)); Click("Начать день своей конторы"); Day.EndDay(); Frame();
            Click("Сбросить свою контору"); Click("Да, сбросить кампанию");
            Assert.That(Day.Cash, Is.EqualTo(300)); Assert.That(Day.HasFounderAssistant, Is.False); Assert.That(Get<OfficeScene>("office").FounderAssistant, Is.False);
            Assert.That(Get<FirmCareerState>("firmCareer").CompletedDays, Is.Zero); HireFirstAssistant(); Assert.That(Day.Cash, Is.EqualTo(200));
        }
        [TestCase(false)] [TestCase(true)]
        public void LegacyFounderMenuHireAndResetNeverChangeV1OrBackup(bool backup)
        {
            OpenFirm(false); var s = LegacyFirm(Get<FirmCareerState>("firmCareer").Capture(), 1);
            File.Delete(FirmPath); string legacy = Path.Combine(folder, "broker-founder-v1.json");
            string p = legacy + (backup ? ".bak" : ""); string bytes = JsonUtility.ToJson(s); File.WriteAllText(p, bytes);
            if (backup) File.WriteAllText(legacy, "broken v1");
            FreshGame(); Click("Продолжить свою контору"); Assert.That(NotebookText(), Does.Contain("копия конторы v1"));
            Assert.That(File.Exists(FirmPath), Is.False); HireFirstAssistant(); Assert.That(File.Exists(FirmPath), Is.True);
            Click("Начать день своей конторы"); Day.EndDay(); Frame(); Click("Сбросить свою контору"); Click("Да, сбросить кампанию");
            Assert.That(File.ReadAllText(p), Is.EqualTo(bytes)); if (backup) Assert.That(File.ReadAllText(legacy), Is.EqualTo("broken v1"));
        }
        [Test] public void LostFocusCannotCommitHireAndRecordingCannotRequestIt()
        {
            OpenFirm(false); Get<VoiceSession>("voice").BeginRecording(true); Call("RequestAssistant"); Assert.That(OverlayOpen, Is.False);
            Get<VoiceSession>("voice").Cancel(); Click("Нанять помощника / $100"); Call("OnApplicationFocus", false); Call("HireAssistant");
            Assert.That(Get<FirmCareerState>("firmCareer").HasAssistant, Is.False);
            Call("OnApplicationFocus", true); Click("Да, нанять за $100"); Assert.That(Day.HasFounderAssistant, Is.True);
        }
        [Test] public void CareerScreenShowsInsufficientSavingsAndDoesNotCreateFirm()
        {
            StartWork(); Day.EndDay(); Frame(); Click("Карьера"); string text = NotebookText();
            Assert.That(text, Does.Contain("До старта осталось $600")); Assert.That(text, Does.Contain("Открытие $200 + касса $300"));
            GUI.Click = "Открыть свою контору"; Call("OnGUI"); Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null;
            Call("RequestFirm"); Assert.That(Get<bool>("showCareerPath"), Is.True); Assert.That(File.Exists(FirmPath), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void OpeningCancellationKeepsSavedWorkAndCreatesNoFirm(bool escape)
        {
            ReadyForFirm(); var same = Day; var paths = Directory.GetFiles(folder); var bytes = Array.ConvertAll(paths, File.ReadAllText);
            Click("Карьера"); Click("Открыть свою контору"); Frame(); Assert.That(NotebookText(), Does.Contain("Открыть свою контору?"));
            if (escape) { OfficeInput.Escape = true; Call("Update"); } else Click("Нет, остаться брокером / Esc");
            Assert.That(OverlayOpen, Is.False); Assert.That(Day, Is.SameAs(same)); Assert.That(File.Exists(FirmPath), Is.False);
            for (int i = 0; i < paths.Length; i++) Assert.That(File.ReadAllText(paths[i]), Is.EqualTo(bytes[i]));
        }
        [Test] public void CareerDuringWorkPausesKeepsDraftAndCannotOpenEvenExistingFirm()
        {
            ReadyForFirm(); var firm = FirmCareerState.FromWork(Get<WorkCareerState>("workCareer"));
            Assert.That(Get<FirmCareerStorage>("firmStorage").TryCreate(firm, out _), Is.True);
            Click("Следующий рабочий день"); Click("Начать рабочий день"); Interact(OfficeStation.Desk); Day.StartCall(); Set("message", "Черновик");
            OfficeInput.Escape = true; Call("Update"); OfficeInput.Escape = true; Call("Update"); // Desk -> world -> pause.
            Click("Карьера"); var same = Day; float seconds = Day.RemainingSeconds; Frame();
            Assert.That(Day.RemainingSeconds, Is.EqualTo(seconds)); Assert.That(NotebookText(), Does.Contain("Завершите текущий день"));
            Call("RequestFirm"); Assert.That(Day, Is.SameAs(same)); Assert.That(Day.Mode, Is.EqualTo(CareerMode.EmployeeWork));
            Click("Остаться брокером / Esc"); Assert.That(Get<string>("message"), Is.EqualTo("Черновик")); Assert.That(Day.Call, Is.EqualTo(CallStatus.Active));
            Assert.That(Get<OfficeNavigation>("navigation").View, Is.EqualTo(OfficeView.Pause)); Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [TestCase(false)] [TestCase(true)]
        public void ExplicitOpeningFromResultsOrMorningHasSeparateMoneyFileAndNoOldWrites(bool morning)
        {
            ReadyForFirm(); if (morning) Click("Следующий рабочий день");
            var work = Get<WorkCareerState>("workCareer"); int savings = work.PersonalSavings, next = work.NextDayNumber;
            var paths = Directory.GetFiles(folder); var bytes = Array.ConvertAll(paths, File.ReadAllText);
            Click("Карьера"); Click("Открыть свою контору"); Click("Да, открыть свою контору");
            Assert.That(Day.Mode, Is.EqualTo(CareerMode.Founder)); Assert.That(Day.DayNumber, Is.EqualTo(next)); Assert.That(Day.Cash, Is.EqualTo(300));
            Assert.That(Get<FirmCareerState>("firmCareer").PersonalReserve, Is.EqualTo(savings - 500)); Assert.That(Get<bool>("intro"), Is.True);
            Assert.That(Get<OfficeScene>("office").Mode, Is.EqualTo(CareerMode.Founder)); Assert.That(File.Exists(FirmPath), Is.True);
            Frame(); Assert.That(Day.RemainingSeconds, Is.EqualTo(600));
            for (int i = 0; i < paths.Length; i++) Assert.That(File.ReadAllText(paths[i]), Is.EqualTo(bytes[i]));
            Assert.That(work.PersonalSavings, Is.EqualTo(savings)); Assert.That(UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void InitialFirmWriteFailureStaysOnConfirmationAndCanRetry()
        {
            ReadyForFirm(); Click("Карьера"); Click("Открыть свою контору");
            Set("firmStorage", new FirmCareerStorage(FirmPath, _ => throw new Exception(), JsonUtility.FromJson<FirmCareerSave>));
            Click("Да, открыть свою контору"); Assert.That(OverlayOpen, Is.True); Assert.That(Day.Mode, Is.EqualTo(CareerMode.EmployeeWork));
            Assert.That(File.Exists(FirmPath), Is.False); Assert.That(NotebookText(), Does.Contain("Не удалось записать"));
            Set("firmStorage", new FirmCareerStorage(FirmPath, JsonUtility.ToJson, JsonUtility.FromJson<FirmCareerSave>));
            Click("Да, открыть свою контору"); Assert.That(Day.IsFounder, Is.True); Assert.That(Day.Cash, Is.EqualTo(300));
        }
        [TestCase(false)] [TestCase(true)]
        public void ExistingFirmAppearingDuringConfirmationLoadsWithoutReopeningOrWriting(bool backup)
        {
            ReadyForFirm(); Click("Карьера"); Click("Открыть свою контору");
            var firm = FirmCareerState.FromWork(Get<WorkCareerState>("workCareer")); var d = firm.CreateDay(); SellAny(d, 0); d.EndDay(); Assert.That(firm.CompleteDay(d), Is.True);
            string path = FirmPath + (backup ? ".bak" : ""), bytes = JsonUtility.ToJson(firm.Capture()); File.WriteAllText(path, bytes);
            var files = Directory.GetFiles(folder); Click("Да, открыть свою контору");
            Assert.That(Day.DayNumber, Is.EqualTo(firm.NextDayNumber)); Assert.That(Day.Cash, Is.EqualTo(firm.Balance));
            Assert.That(Get<FirmCareerState>("firmCareer").PersonalReserve, Is.EqualTo(firm.PersonalReserve));
            CollectionAssert.AreEquivalent(files, Directory.GetFiles(folder)); Assert.That(File.ReadAllText(path), Is.EqualTo(bytes));
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void UnreadableFirmAppearingDuringConfirmationIsPreservedAndRetryCanLoad(bool backup, bool directory)
        {
            ReadyForFirm(); Click("Карьера"); Click("Открыть свою контору"); string path = FirmPath + (backup ? ".bak" : "");
            if (directory) Directory.CreateDirectory(path); else File.WriteAllText(path, "future firm");
            Click("Да, открыть свою контору"); Assert.That(OverlayOpen, Is.True); Assert.That(Day.IsRepeatingWork, Is.True);
            Assert.That(NotebookText(), Does.Contain("Новое открытие заблокировано"));
            if (directory) Directory.Delete(path); else { Assert.That(File.ReadAllText(path), Is.EqualTo("future firm")); File.Delete(path); }
            Click("Да, открыть свою контору"); Assert.That(Day.IsFounder, Is.True);
        }
        [Test] public void FreshMenuCanRetryUnreadableFirmEvenWithNoCompletedIntro()
        {
            File.WriteAllText(FirmPath, "future firm"); FreshGame(); Click("Повторить загрузку своей конторы");
            Assert.That(Get<bool>("selectingMode"), Is.True); Assert.That(NotebookText(), Does.Contain("Новое открытие заблокировано"));
            Assert.That(File.ReadAllText(FirmPath), Is.EqualTo("future firm"));
        }
        [Test] public void FailedWorkSaveBlocksOpeningUntilSavedAndDoesNotDuplicateEarnedMoney()
        {
            ReadyForFirm(); int savings = Get<WorkCareerState>("workCareer").PersonalSavings; Set("saveSucceeded", false); Click("Карьера");
            Call("RequestFirm"); Assert.That(Day.IsRepeatingWork, Is.True); Assert.That(File.Exists(FirmPath), Is.False);
            Click("Повторить сохранение"); Click("Открыть свою контору"); Click("Да, открыть свою контору");
            Assert.That(Get<FirmCareerState>("firmCareer").PersonalReserve, Is.EqualTo(savings - 500));
        }
        [Test] public void FounderDeskHasCorrectTabsNoTeamMetricAndAllCommissionShown()
        {
            OpenFirm(); Interact(OfficeStation.Desk); string text = NotebookText();
            Assert.That(text, Does.Contain(" · контора")); Assert.That(text, Does.Contain("Контора")); Assert.That(text, Does.Not.Contain("Начальник"));
            Assert.That(text, Does.Not.Contain("Первый контакт")); Click("Контора"); text = NotebookText();
            Assert.That(text, Does.Contain("Комиссия в кассу")); Assert.That(text, Does.Not.Contain("Команда")); Assert.That(text, Does.Not.Contain("Подготовить досье"));
            Click("Связь"); Assert.That(NotebookText(), Does.Contain("внешние функции необязательны"));
        }
        [TestCase(OfficeStation.Employee)] [TestCase(OfficeStation.Supervisor)] [TestCase(OfficeStation.Coffee)] [TestCase(OfficeStation.Upgrade)]
        public void FounderCannotOpenOldStaffAndShops(OfficeStation station)
        { OpenFirm(); Interact(station); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True); Assert.That(Day.Spent, Is.Zero); }
        [Test] public void FounderDoorSettlesExactlyOnceAndFreshMenuContinuesWithUnchangedReserve()
        {
            OpenFirm(); SellAny(Day, 0); int revenue = Day.Earned, source = Day.DayNumber; int reserve = Get<FirmCareerState>("firmCareer").PersonalReserve;
            Interact(OfficeStation.Exit); Click("Закончить день конторы"); for (int n = 0; n < 4; n++) Frame();
            Assert.That(Get<FirmCareerState>("firmCareer").Balance, Is.EqualTo(300 + revenue - 60));
            Assert.That(Get<FirmCareerState>("firmCareer").CompletedDays, Is.EqualTo(1));
            FreshGame(); Click("Продолжить свою контору"); Assert.That(Day.DayNumber, Is.EqualTo(source + 1));
            Assert.That(Day.Cash, Is.EqualTo(300 + revenue - 60)); Assert.That(Get<FirmCareerState>("firmCareer").PersonalReserve, Is.EqualTo(reserve));
        }
        [Test] public void FounderDebtPersistsAndNeverOpensOldThreeDayFinal()
        {
            OpenFirm(); int source = Day.DayNumber;
            for (int n = 0; n < 10; n++) { Day.EndDay(); Frame(); Click("Следующий день конторы"); Click("Начать день своей конторы"); }
            Assert.That(Get<FirmCareerState>("firmCareer").Balance, Is.EqualTo(-300)); Assert.That(Day.DayNumber, Is.EqualTo(source + 10));
            Assert.That(Day.Cash, Is.Zero); Assert.That(Day.CarryDebt, Is.EqualTo(300)); Assert.That(Day.TotalExpenses, Is.EqualTo(360));
            Assert.That(Get<FirmCareerState>("firmCareer").RecentReports.Length, Is.EqualTo(7));
        }
        [Test] public void FounderFailedSaveRetryDoesNotChargeSecondRentAndBlocksModeSwitch()
        {
            OpenFirm(); File.Delete(FirmPath); Directory.CreateDirectory(FirmPath); Day.EndDay(); Frame();
            Assert.That(Get<bool>("saveSucceeded"), Is.False); Assert.That(Get<FirmCareerState>("firmCareer").Balance, Is.EqualTo(240));
            GUI.Click = "Выбрать режим"; Call("OnGUI"); Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null;
            Directory.Delete(FirmPath); Click("Повторить сохранение"); Frame();
            Assert.That(Get<bool>("saveSucceeded"), Is.True); Assert.That(Get<FirmCareerState>("firmCareer").Balance, Is.EqualTo(240));
            Assert.That(Get<FirmCareerState>("firmCareer").CompletedDays, Is.EqualTo(1));
        }
        [Test] public void FounderNotebookPausesPreservesDraftAndDoesNotPretendToPayPersonalCommission()
        {
            OpenFirm(); Interact(OfficeStation.Desk); Day.StartCall(); Set("message", "Черновик своей конторы"); float seconds = Day.RemainingSeconds;
            Click("База клиентов"); Frame(); string text = NotebookText();
            Assert.That(text, Does.Contain("вся сумма в кассу фирмы")); Assert.That(text, Does.Not.Contain("лично вам"));
            foreach (string id in ClientCatalog.Ids) Assert.That(text, Does.Contain(ClientCatalog.Create(id).Name));
            Assert.That(Day.RemainingSeconds, Is.EqualTo(seconds)); Click("История своей конторы"); Assert.That(NotebookText(), Does.Contain("Показано 0 из 0"));
            Click("Вернуться к работе / Esc"); Assert.That(Get<string>("message"), Is.EqualTo("Черновик своей конторы")); Assert.That(Day.Call, Is.EqualTo(CallStatus.Active));
        }
        [Test] public void FounderNotebookHistoryShowsFirmArithmeticNotEmployeeHistory()
        {
            OpenFirm(); SellAny(Day, 0); int revenue = Day.Earned; Day.EndDay(); Frame(); Click("Следующий день конторы"); Click("Начать день своей конторы");
            Interact(OfficeStation.Desk); Click("История дней"); string text = NotebookText();
            Assert.That(text, Does.Contain("Фирма: $300 + $" + revenue + " − $60 аренды")); Assert.That(text, Does.Contain("Личной выплаты нет"));
            Assert.That(text, Does.Contain("Старые рабочие дни не включены"));
        }
        [Test] public void FounderConfirmedResetUsesOpeningAndLeavesOtherSavesAndBackupsIdentical()
        {
            OpenFirm(); int source = Day.DayNumber, reserve = Get<FirmCareerState>("firmCareer").PersonalReserve;
            var paths = Directory.GetFiles(folder, "broker-employee-*"); var bytes = Array.ConvertAll(paths, File.ReadAllText);
            SellAny(Day, 0); Day.EndDay(); Frame(); Click("Сбросить свою контору"); Click("Да, сбросить кампанию");
            Assert.That(Day.DayNumber, Is.EqualTo(source)); Assert.That(Day.Cash, Is.EqualTo(300)); Assert.That(Get<FirmCareerState>("firmCareer").PersonalReserve, Is.EqualTo(reserve));
            Assert.That(Get<FirmCareerState>("firmCareer").CompletedDays, Is.Zero);
            for (int i = 0; i < paths.Length; i++) Assert.That(File.ReadAllText(paths[i]), Is.EqualTo(bytes[i]));
        }
        [Test] public void FounderDayRestartDropsOnlyUnsettledSaleAndDoesNotWriteOrChargeRent()
        {
            OpenFirm(); string bytes = File.ReadAllText(FirmPath); SellAny(Day, 0); Confirm("RestartDay"); Click("Да, начать день заново");
            Assert.That(Day.Cash, Is.EqualTo(300)); Assert.That(Day.Earned, Is.Zero); Assert.That(Get<FirmCareerState>("firmCareer").CompletedDays, Is.Zero);
            Assert.That(File.ReadAllText(FirmPath), Is.EqualTo(bytes));
        }
        [Test] public void ExistingFirmIsIndependentOfLaterEmployeeResetAndMenuSwitchDoesNotGrantMoney()
        {
            OpenFirm(false); string bytes = File.ReadAllText(FirmPath); Call("LoadMode", CareerMode.Employee); Call("NewCareer");
            Call("LoadMode", CareerMode.EmployeeWork); Call("NewCareer"); FreshGame(); Click("Продолжить свою контору");
            Assert.That(Day.Cash, Is.EqualTo(300)); Assert.That(File.ReadAllText(FirmPath), Is.EqualTo(bytes));
            Assert.That(Get<EmploymentState>("employment").CompletedDays, Is.Zero);
        }
        [Test] public void FounderCarriesWorkComplaintAndGuiRepairRequiresReportBeforeFullCommission()
        {
            ReadyForFirm(); Click("Следующий рабочий день"); Click("Начать рабочий день");
            Day.StartCall(); Day.Say("Какая цель?"); Day.Say("Гарантирую прибыль"); string id = Day.Current.Id;
            Day.EndDay(); Frame(); Click("Карьера"); Click("Открыть свою контору"); Click("Да, открыть свою контору"); Click("Начать день своей конторы");
            while (Array.IndexOf(ClientCatalog.Appointments(Day.DayNumber), id) < 0)
            { Day.EndDay(); Frame(); Click("Следующий день конторы"); Click("Начать день своей конторы"); }
            Day.SelectClient(Array.IndexOf(ClientCatalog.Appointments(Day.DayNumber), id)); Interact(OfficeStation.Desk); Click("Позвонить");
            Assert.That(Day.ConversationStep, Is.EqualTo(ConversationStage.Complaint)); Click("Предложить сделку"); Assert.That(Day.DealClosed, Is.False);
            Set("message", "Признаю ошибку. Есть риск убытка."); Click("Сказать"); Assert.That(Day.ConversationStep, Is.EqualTo(ConversationStage.Report));
            Set("message", "Обсудим отчёт по прошлой сделке. Какая цель? Есть риск убытка. Комиссия 2%. Срок год. Персональный отчёт."); Click("Сказать"); Click("Предложить сделку");
            Set("message", ClientConversation.AnswerIntent(id) == DialogueIntent.Reserve ? "Оставьте резерв на оборудование."
                : ClientConversation.AnswerIntent(id) == DialogueIntent.Written ? "Все условия в письменном договоре." : "Личный менеджер, буду на связи.");
            Click("Сказать"); Click("Закрыть сделку"); Assert.That(Day.Earned, Is.EqualTo(Day.Current.Commission));
            Day.EndDay(); Frame(); FreshGame(); Click("Продолжить свою контору");
            Assert.That(Get<FirmCareerState>("firmCareer").Clients[ClientCatalog.Index(id)].complaintPending, Is.False);
            Assert.That(Get<FirmCareerState>("firmCareer").Clients[ClientCatalog.Index(id)].awaitingReport, Is.True);
        }
        [Test] public void CareerScreenWillNotOpenDuringVoiceRecordingOrFocusLoss()
        {
            StartWork(); Get<VoiceSession>("voice").BeginRecording(true); Call("OpenCareerPath"); Assert.That(Get<bool>("showCareerPath"), Is.False);
            Get<VoiceSession>("voice").Cancel(); Call("OnApplicationFocus", false); Call("OpenCareerPath"); Assert.That(Get<bool>("showCareerPath"), Is.False);
        }
    }
}
