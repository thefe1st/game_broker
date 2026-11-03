using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private FirmCareerState firmCareer;
        private FirmCareerStorage firmStorage;
        private FirmCareerLoadStatus firmLoadStatus;
        private string firmLoadNotice = "";
        private bool showCareerPath;
        private string hiringNotice = "";
        private bool CanRequestAssistant => ApplicationActive && IsFounderMode && !selectingMode && saveSucceeded
            && !showNews && (intro || day.DayEnded) && !busy && !voice.Recording && !voice.Working && firmCareer.CanHireAssistant;
        private void RequestAssistant()
        {
            if (!CanRequestAssistant || UtilityOverlayOpen) return;
            hiringNotice = ""; RequestConfirmation(ConfirmationKind.HireAssistant);
        }
        private void HireAssistant()
        {
            if (confirmation != ConfirmationKind.HireAssistant || !CanRequestAssistant) return;
            if (!firmStorage.TryHireAssistant(firmCareer, out var hired, out hiringNotice)) return;
            firmCareer = hired; firmLoadStatus = FirmCareerLoadStatus.Loaded;
            saveNotice = "Помощник нанят за $100 и сохранён. Зарплата $40 с дня " + hired.AssistantHireDay + ".";
            CloseUtilityOverlay(); Restart();
        }
        private void DrawAssistantConfirmation()
        {
            Scrim(); Panel(280, 132, 880, 704, "");
            Label(320, 172, 800, 64, "Нанять помощника?", theme.Title);
            Label(320, 280, 800, 168, "Лена будет готовить досье по поручению: 25 игровых секунд, +1 доверия один раз клиенту за день. Звонки и продажи остаются за вами. Найм $100: касса $" + firmCareer.Balance + " → $" + (firmCareer.Balance - FirmCareerState.HiringCost) + ". Личный резерв не расходуется.", theme.Text);
            Label(320, 468, 800, 88, "С дня " + firmCareer.NextDayNumber + " зарплата $40 каждый день, даже без поручений и при долге; аренда ещё $60. После записи в broker-founder-v3.json откроется утро. Старые v1/v2 и .bak не меняются.", theme.Small);
            Label(320, 564, 800, 60, hiringNotice, theme.Small);
            if (Button(320, 632, 800, 52, "Нет, без помощника / Esc", true, true)) CloseUtilityOverlay();
            if (Button(320, 724, 800, 52, "Да, нанять за $100", CanRequestAssistant)) HireAssistant();
        }
        private void OpenCareerPath()
        {
            if (!ApplicationActive || !IsWorkMode || selectingMode || showNews || UtilityOverlayOpen || busy || voice.Recording || voice.Working) return;
            voice.Cancel(); showCareerPath = true; office.Player.SetControl(false); GUI.FocusControl(null);
        }
        private void RequestFirm()
        {
            if (!ApplicationActive || !saveSucceeded) return;
            if (!selectingMode && (!IsWorkMode || (!intro && !day.DayEnded))) return;
            if (firmStorage.TryLoad(out firmCareer, out firmLoadNotice, out firmLoadStatus))
            { CloseUtilityOverlay(); LoadMode(CareerMode.Founder); return; }
            if (firmLoadStatus == FirmCareerLoadStatus.Unreadable) return;
            // Only settled mornings/results: opening never discards an unfinished work day.
            if (selectingMode || !IsWorkMode || (!intro && !day.DayEnded) || !FirmCareerState.CanOpen(workCareer)) return;
            showCareerPath = false; RequestConfirmation(ConfirmationKind.StartFirm);
        }
        private void StartFirmFromWork()
        {
            if (!ApplicationActive || !saveSucceeded || selectingMode || confirmation != ConfirmationKind.StartFirm
                || !IsWorkMode || (!intro && !day.DayEnded)) return;
            if (firmStorage.TryLoad(out firmCareer, out firmLoadNotice, out firmLoadStatus))
            { CloseUtilityOverlay(); LoadMode(CareerMode.Founder); return; }
            if (firmLoadStatus != FirmCareerLoadStatus.Missing || !FirmCareerState.CanOpen(workCareer)) return;
            if (!firmStorage.TryCreate(FirmCareerState.FromWork(workCareer), out firmLoadNotice)) return;
            CloseUtilityOverlay(); LoadMode(CareerMode.Founder);
        }
        private void DrawCareerPath()
        {
            Scrim(); Panel(200, 76, 1040, 808, "");
            Label(240, 112, 960, 32, "КАРЬЕРА / РЕШЕНИЕ ЗА ВАМИ", theme.Tag);
            Label(240, 168, 960, 64, "Своя вывеска. Свои счета.", theme.Title);
            MetricCard(240, 260, 296, "Сохранённые накопления", "$" + workCareer.PersonalSavings);
            MetricCard(560, 260, 296, "Для старта", "$600", "Открытие $200 + касса $300");
            MetricCard(880, 260, 320, "Аренда в день", "$60", "Без зарплат: работаете сами");
            Label(240, 420, 960, 104, "В свою контору перейдут отношения, отчёты и претензии шести клиентов. Новые сделки приносят фирме всю комиссию, не личные 20%. Личный резерв — накопления минус $500; он не покрывает долги автоматически. После открытия можно отдельно нанять помощника; рынка пока нет.", theme.Text);
            Label(240, 548, 960, 72, "Можно остаться брокером без штрафа. Старые режимы сохраняются отдельными снимками; их меню — не перевод денег между фирмами. Открыть контору можно только утром или после завершённого дня с успешной записью.", theme.Small);
            Label(240, 644, 960, 64, string.IsNullOrEmpty(firmLoadNotice)
                ? !saveSucceeded ? "Сначала повторите сохранение завершённых дней."
                : !intro && !day.DayEnded ? "Завершите текущий день: сегодняшняя комиссия ещё не входит в стартовый капитал."
                : !FirmCareerState.CanOpen(workCareer) ? "До старта осталось $" + (FirmCareerState.RequiredSavings - workCareer.PersonalSavings) + ". Условия — тестовый баланс."
                : "Условия выполнены. После открытия в кассе $300; лично останется $" + (workCareer.PersonalSavings - 500) + ". Это не финал карьеры."
                : firmLoadNotice, theme.Small);
            if (Button(240, 740, 464, 48, "Остаться брокером / Esc", true, true)) CloseUtilityOverlay();
            if (Button(736, 740, 464, 48, firmLoadStatus == FirmCareerLoadStatus.Unreadable ? "Повторить загрузку конторы"
                : firmCareer != null ? "Продолжить свою контору" : "Открыть свою контору",
                saveSucceeded && (intro || day.DayEnded) && (firmLoadStatus != FirmCareerLoadStatus.Missing || FirmCareerState.CanOpen(workCareer)))) RequestFirm();
            if (!saveSucceeded && Button(240, 812, 960, 40, "Повторить сохранение")) SaveCareer();
        }
        private void DrawFirmConfirmation()
        {
            Scrim(); Panel(280, 132, 880, 704, "");
            Label(320, 172, 800, 64, "Открыть свою контору?", theme.Title);
            Label(320, 280, 800, 164, "Из сохранённых $" + workCareer.PersonalSavings + ": $200 — расходы открытия, $300 — касса фирмы, $" + (workCareer.PersonalSavings - 500) + " — личный резерв. Аренда $60 за день. Всю комиссию получает фирма; сотрудников пока нет. Долг переносится, но личный резерв сам не расходуется.", theme.Text);
            Label(320, 468, 800, 120, "Начнётся день " + workCareer.NextDayNumber + " в отдельном broker-founder-v3.json. Вводная глава, рабочие дни и старый прототип владельца не перезаписываются. Это отдельная ветка-снимок, не повторная выплата заработка. " + firmLoadNotice, theme.Small);
            if (Button(320, 632, 800, 52, "Нет, остаться брокером / Esc", true, true)) CloseUtilityOverlay();
            if (Button(320, 724, 800, 52, "Да, открыть свою контору", saveSucceeded && FirmCareerState.CanOpen(workCareer))) StartFirmFromWork();
        }
        private void DrawFirmIntro()
        {
            Scrim(); Panel(200, 96, 1040, 768, "");
            Label(240, 132, 960, 32, "СВОЯ КОНТОРА / ДЕНЬ " + day.DayNumber, theme.Tag);
            Label(240, 196, 960, 64, "Теперь аренда звонит вам.", theme.Title);
            Label(240, 296, 960, 112, "Три контакта из своей базы. Вы ведёте звонки лично; вся комиссия остаётся фирме. Отчёты и претензии нужно разобрать до новой сделки. " + (firmCareer.HasAssistant ? "Лена готовит досье по поручению: за столом в разделе «Контора» или через E у помощника." : "Можно отдельно нанять одного помощника для досье. Без улучшений и рынка."), theme.Text);
            MetricCard(240, 432, 296, "Касса / долг фирмы", "$" + firmCareer.Balance);
            MetricCard(560, 432, 296, "Личный резерв", "$" + firmCareer.PersonalReserve, "Отдельно, без автосписаний");
            MetricCard(880, 432, 320, "Аренда + зарплата", "$" + (day.DailyRent + day.DailyPayroll), "Долг отдельно: $" + day.CarryDebt);
            Label(240, 560, 960, 48, saveNotice + (!firmCareer.HasAssistant && !firmCareer.CanHireAssistant
                ? " Для найма нужна касса от $200; личный резерв не учитывается." : ""), theme.Small);
            if (!firmCareer.HasAssistant && Button(240, 616, 960, 40, "Нанять помощника / $100", CanRequestAssistant)) RequestAssistant();
            else if (firmCareer.HasAssistant) Label(240, 616, 960, 36, "Помощник нанят. Зарплата $40/день, досье 25 с. Обзвон недоступен.", theme.Small);
            if (Button(240, 664, 960, 56, "Начать день своей конторы", true, true)) { intro = false; EnterWorld(); }
            if (Button(240, 736, 464, 48, "Проверить микрофон без API")) OpenVoiceSetup();
            if (Button(736, 736, 464, 48, saveSucceeded ? "Выбрать режим" : "Повторить сохранение"))
            { if (saveSucceeded) selectingMode = true; else SaveCareer(); }
        }
        private void DrawFirmResults()
        {
            var report = firmCareer.LastReport; if (report == null) return;
            Scrim(); Panel(200, 76, 1040, 808, "");
            Label(240, 112, 960, 32, "СВОЯ КОНТОРА / ДЕНЬ " + report.day + " ЗАВЕРШЁН", theme.Tag);
            Label(240, 176, 960, 64, firmCareer.Balance >= 0 ? "Вывеска остаётся.": "Долг — не финал.", theme.Title);
            MetricCard(240, 280, 296, "Касса / долг", "$" + firmCareer.Balance);
            MetricCard(560, 280, 296, "Комиссия фирмы", "+$" + report.revenue);
            MetricCard(880, 280, 320, "Личный резерв", "$" + firmCareer.PersonalReserve);
            Label(240, 448, 960, 64, "$" + report.openingBalance + " + $" + report.revenue + " − $60 аренды − $" + report.salary + " зарплаты = $" + report.balance + ". Расходы начислены один раз; резерв не потрачен.", theme.Heading);
            Label(240, 520, 960, 92, StockHistory.SoldToday(report.clientIds, report.sold, report.soldStockIds) + " Долг переносится в следующий день, продажи позволяют восстановить кассу. " + (firmCareer.HasAssistant ? "Помощник остаётся в штате, зарплата начисляется и без поручений. Незавершённое досье на следующий день не переносится." : "Найм только между сохранёнными днями: $100 при кассе от $200, затем $40 за день."), theme.Small);
            if (!firmCareer.HasAssistant && Button(240, 616, 960, 40, "Нанять помощника / $100", CanRequestAssistant)) RequestAssistant();
            Label(240, 664, 960, 56, saveNotice, theme.Small);
            if (Button(240, 740, 464, 48, saveSucceeded ? "Следующий день конторы" : "Продолжить без записи", true, true)) Restart();
            if (Button(736, 740, 464, 48, saveSucceeded ? "Сбросить свою контору" : "Повторить сохранение"))
            { if (saveSucceeded) RequestConfirmation(ConfirmationKind.NewCareer); else SaveCareer(); }
            if (Button(240, 812, 960, 40, "Выбрать режим", saveSucceeded)) selectingMode = true;
        }
        private void DrawFirmOffice()
        {
            Panel(344, 184, 1056, 664, "");
            Label(376, 216, 992, 48, firmCareer.HasAssistant ? "Небольшая контора / первый помощник" : "Небольшая контора / пока без команды", theme.Title);
            MetricCard(376, 308, 304, "Комиссия сегодня", "$" + day.Earned);
            MetricCard(704, 308, 304, "После расходов / долг", "$" + day.FinalBalance);
            MetricCard(1032, 308, 336, "Личный резерв", "$" + firmCareer.PersonalReserve);
            Label(376, 448, 992, 72, "Аренда $60 и зарплата $" + day.DailyPayroll + " — в итогах дня, один раз. Личный резерв не покрывает долг. Продажи ведёте сами; закупок, расширения и рынка нет.", theme.Text);
            if (firmCareer.HasAssistant)
            {
                Label(376, 532, 992, 40, AssistantWorkStatus(), theme.Heading);
                for (int i = 0; i < 3; i++) if (Button(376 + i * 336, 588, 320, 44, day.Clients[i].Name, true, i == staffTargetIndex)) staffTargetIndex = i;
                if (Button(376, 648, 992, 52, "Подготовить досье / 25 с", CanAssignDossier(staffTargetIndex), true))
                    News(day.AssignTask(0, WorkKind.Research, staffTargetIndex));
                Label(376, 712, 992, 52, DossierStatus(staffTargetIndex), theme.Small);
            }
            else Label(376, 564, 992, 112, "Помощника можно нанять утром или в итогах после успешной записи. Найм $100 при кассе от $200; зарплата $40 каждый день. Продолжайте личные звонки — найм необязателен.", theme.Small);
            if (Button(376, 772, 992, 48, "Посмотреть базу клиентов", !busy && !voice.Working && !voice.Recording)) OpenWorkNotebook();
        }
        private bool CanAssignDossier(int target) => ApplicationActive && day.HasFounderAssistant && !intro && !day.DayEnded
            && !busy && !voice.Working && !voice.Recording && target >= 0 && target < day.Clients.Count
            && day.Employees[0].Work == WorkKind.Idle && !day.Clients[target].Researched && day.Clients[target].Call != CallStatus.Closed;
        private string AssistantWorkStatus()
        {
            var worker = day.Employees[0];
            return worker.Work == WorkKind.Idle ? "Лена / готова к поручению"
                : "Готовится досье: " + day.Clients[worker.TargetClient].Name + ", осталось " + Mathf.CeilToInt(worker.Remaining) + " с.";
        }
        private string DossierStatus(int target)
        {
            var client = day.Clients[target]; var worker = day.Employees[0];
            if (client.Researched) return "Досье «" + client.Name + "» уже готово. Повтор сегодня недоступен; условия, отчёт и претензию обсуждаете сами.";
            if (day.DayEnded) return "День завершён: поручения и досье на следующий день не переносятся.";
            if (client.Call == CallStatus.Closed) return "Звонок завершён: досье сегодня недоступно. Выберите другого клиента.";
            if (!day.HasFounderAssistant) return "Помощник не нанят: досье по поручению недоступно.";
            if (worker.Work != WorkKind.Idle) return worker.TargetClient == target ? AssistantWorkStatus()
                : "Сначала дождитесь текущего досье: " + day.Clients[worker.TargetClient].Name + ", осталось " + Mathf.CeilToInt(worker.Remaining) + " с.";
            return "Можно поручить досье: 25 игровых секунд, +1 доверия один раз. Условия, отчёт и претензию обсуждаете сами.";
        }
        private void DrawFirmConnection()
        {
            Panel(344, 184, 1056, 664, "");
            Label(376, 216, 992, 48, "Связь / внешние функции необязательны", theme.Title);
            Label(376, 316, 992, 128, "Можно играть полностью локально. ИИ только перефразирует ответ: деньги, условия сделки и претензии определяет игра. Голос экспериментальный; подключение API не требуется для открытия конторы.", theme.Text);
            bool before = GUI.enabled; GUI.enabled = before && !busy && day.Call != CallStatus.Active;
            useGateway = GUI.Toggle(new Rect(376, 596, 448, 52), useGateway, (useGateway ? "Вкл" : "Выкл") + " · ИИ-формулировки", theme.Button); GUI.enabled = before;
            bool next = GUI.Toggle(new Rect(856, 596, 512, 52), voiceEnabled, (voiceEnabled ? "Вкл" : "Выкл") + " · Голос → внешний API", theme.Button);
            if (next != voiceEnabled) { voiceEnabled = next; if (!next) voice.Cancel(); }
            Label(376, 668, 992, 40, "Запись отправляется внешнему API только по кнопке. Можно играть полностью локально.", theme.Small);
            if (Button(376, 740, 992, 56, "Проверить микрофон и связь", !busy && !voice.Working && !voice.Recording)) OpenVoiceSetup();
        }
    }
}
