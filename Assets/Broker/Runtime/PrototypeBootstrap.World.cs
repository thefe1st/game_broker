using Broker.Core;
using Broker.Runtime.Office;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private readonly OfficeNavigation navigation = new OfficeNavigation();
        private int staffTargetIndex;
        private void OnWorldInteraction(OfficeInteractable target)
        {
            if (intro || busy || UtilityOverlayOpen || day.DayEnded || day.PendingEvent != null || !navigation.IsExploring) return;
            if (target.Station == OfficeStation.Street || target.Station == OfficeStation.ReturnOffice)
            {
                CancelPending(); office.VisitCity(target.Station == OfficeStation.Street); EnterWorld(); return;
            }
            if ((target.Station == OfficeStation.CityContact || target.Station == OfficeStation.CityPickup) && !office.IsInCity) return;
            if (navigation.Open(target.Station, target.EmployeeIndex))
            {
                staffTargetIndex = day.SelectedClientIndex;
                office.Player.SetControl(false);
            }
        }
        private void EnterWorld()
        {
            if (busy || UtilityOverlayOpen || voice.Recording || voice.Working) return;
            navigation.Close(); voice.Cancel();
            office.Player.SetControl(!intro && !day.DayEnded && day.PendingEvent == null && OfficeInput.Available);
            office.Player.Capture();
        }
        private void ResolveOfficeEvent(int choice)
        {
            News(day.ResolveEvent(choice));
            if (day.PendingEvent == null && navigation.IsExploring) EnterWorld();
        }
        private void DrawExplorationScreen()
        {
            GUI.enabled = true;
            if (!intro && !day.DayEnded) DrawWorldHUD();
            if (intro) DrawIntro();
            else if (day.DayEnded) DrawResults();
            else if (day.PendingEvent != null) DrawEvent();
            else if (showNews) DrawNewsJournal();
            else if (!navigation.IsExploring) DrawStationPanel();
            if (!intro && !day.DayEnded && navigation.IsExploring && !OfficeInput.Available
                && Button(420, 640, 600, 52, "Открыть рабочий экран без ходьбы", true, true))
                navigation.Open(OfficeStation.Desk);
        }
        private void DrawWorldHUD()
        {
            Panel(32, 32, 320, 80, ""); Label(56, 44, 272, 32, "МОЙКА КАПИТАЛ", theme.Heading);
            Label(56, 80, 272, 24, office.IsInCity ? "Деловой квартал" : "Офис · день " + day.DayNumber, theme.Small);
            Panel(616, 32, 208, 80, ""); Label(648, 48, 160, 44, ClockText(), theme.Title);
            Panel(1104, 32, 304, 80, ""); Label(1128, 44, 256, 36, "$" + day.Cash + " / " + day.TotalExpenses, theme.Heading);
            Meter(1128, 96, 256, day.Cash / (float)day.TotalExpenses, theme.Accent);
            if (!navigation.IsExploring || day.PendingEvent != null) return;
            var player = office.Player;
            Paint(new Rect(718, 478, 4, 4), player.Target != null ? theme.Accent : theme.Ink);
            string target;
            if (!OfficeInput.Available) target = "Ввод недоступен · открой рабочий экран";
            else if (!player.Captured) target = "Нажми в окне Game";
            else if (player.Target != null) target = player.Target.Title.Split('/')[0].Trim();
            else target = office.IsInCity ? "Бюро — у лифта · Павильон — слева" : "Телефон — звонки";
            Panel(420, 784, 600, 88, "");
            if (player.Target != null) { Paint(new Rect(444, 804, 48, 48), theme.Border); Label(459, 811, 32, 36, "E", theme.Heading); }
            Label(player.Target != null ? 516 : 452, 808, player.Target != null ? 476 : 536, 44, target, theme.Heading);
            if (office.IsInCity && day.CityWorkTaken)
            {
                Panel(352, 136, 736, 88, "");
                Label(376, 152, 688, 60, day.CityWorkStatus, theme.Small);
            }
            if (day.RemainingSeconds > BrokerDay.DurationSeconds - 15)
                Label(420, 896, 600, 28, "WASD · ходить     Мышь · обзор     E · действие     Esc · пауза", theme.Small);
            else if (officeNews.Count > 0 && Time.realtimeSinceStartupAsDouble < toastUntil)
            {
                string news = officeNews[officeNews.Count - 1]; if (news.Length > 105) news = news.Substring(0, 102) + "…";
                Label(240, 896, 960, 44, news, theme.Small);
            }
        }
        private void DrawStationPanel()
        {
            Scrim();
            switch (navigation.View)
            {
                case OfficeView.Employee: DrawEmployeePanel(); break;
                case OfficeView.Coffee: DrawShopPanel(false); break;
                case OfficeView.Upgrade: DrawShopPanel(true); break;
                case OfficeView.Exit: DrawExitPanel(); break;
                case OfficeView.Pause: DrawPausePanel(); break;
                case OfficeView.CityContact: DrawCityPanel(false); break;
                case OfficeView.CityPickup: DrawCityPanel(true); break;
            }
        }
        private void DrawEmployeePanel()
        {
            int index = navigation.EmployeeIndex; var worker = day.Employees[index];
            Panel(280, 188, 880, 584, ""); Label(320, 224, 800, 48, worker.Name, theme.Title);
            Label(320, 288, 800, 28, BrokerPresentation.TaskLabel(worker.Work) + " · Настроение " + worker.Morale + "%", theme.Small);
            Meter(320, 344, 800, worker.Morale / 100f, theme.Accent);
            for (int i = 0; i < day.Clients.Count; i++)
                if (Button(320 + i * 272, 392, 256, 56, day.Clients[i].Name, true, i == staffTargetIndex)) staffTargetIndex = i;
            bool idle = worker.Work == WorkKind.Idle;
            if (Button(320, 488, 256, 80, "Досье", idle && !day.Clients[staffTargetIndex].Researched, true))
            { News(day.AssignTask(index, WorkKind.Research, staffTargetIndex)); EnterWorld(); }
            if (Button(592, 488, 256, 80, "Обзвон", idle && !worker.SideDealDone))
            { News(day.AssignTask(index, WorkKind.Leads, staffTargetIndex)); EnterWorld(); }
            if (Button(864, 488, 256, 80, "Перерыв", idle))
            { News(day.AssignTask(index, WorkKind.Break, staffTargetIndex)); EnterWorld(); }
            if (!idle) Label(320, 600, 800, 32, "Осталось " + Mathf.CeilToInt(worker.Remaining) + " с игрового времени", theme.Small);
            if (Button(320, 680, 800, 56, "Назад · Esc")) EnterWorld();
        }
        private void DrawShopPanel(bool upgrade)
        {
            Panel(280, 220, 880, 520, "");
            Label(320, 260, 800, 48, upgrade ? "Золотой телефон" : "Кофе команде", theme.Title);
            Label(320, 344, 800, 72, upgrade ? "$90" : "$40", theme.Display);
            Label(320, 432, 800, 36, upgrade ? "+1 доверия · один раз за кампанию" : "+20 настроения · один раз за день", theme.Heading);
            int cost = upgrade ? 90 : 40; bool bought = upgrade ? day.HasGoldenPhone : day.CoffeeBought;
            if (Button(320, 528, 800, 64, bought ? "Уже куплено" : "Купить", !bought && day.Cash >= cost, true))
            { News(upgrade ? day.BuyGoldenPhone() : day.BuyCoffee()); EnterWorld(); }
            if (Button(320, 632, 800, 56, "Назад · Esc")) EnterWorld();
        }
        private void DrawExitPanel()
        {
            Panel(280, 220, 880, 520, ""); Label(320, 260, 800, 48, "Закончить день?", theme.Title);
            MetricCard(320, 344, 248, "Касса", "$" + day.Cash);
            MetricCard(592, 344, 248, "Счета", "$" + day.TotalExpenses);
            MetricCard(864, 344, 256, "Остаток / долг", "$" + day.FinalBalance);
            Label(320, 488, 800, 32, "Звонки и поручения остановятся. Итоги сохранятся.", theme.Small);
            if (day.CityWorkTaken && day.CityStage != CityWorkStage.Completed)
                Label(320, 600, 800, 32, "Дело квартала не сдано: награды не будет, оплата не вернётся.", theme.Small);
            if (Button(320, 552, 800, 56, "Закрыть контору", true, true)) { CancelPending(); day.EndDay(); }
            if (Button(320, 640, 800, 56, "Ещё поработать · Esc")) EnterWorld();
        }
        private void DrawPausePanel()
        {
            Panel(360, 156, 720, 648, ""); Label(400, 196, 640, 56, "Рынок подождёт.", theme.Title);
            if (Button(400, 296, 640, 64, "Продолжить · Esc", true, true)) EnterWorld();
            if (Button(400, 384, 640, 56, "Журнал")) showNews = true;
            if (Button(400, 464, 640, 56, "Микрофон и связь")) OpenVoiceSetup();
            if (Button(400, 544, 640, 56, "Начать день заново")) RequestConfirmation(ConfirmationKind.RestartDay);
            Label(400, 664, 640, 64, "WASD · ходить     Мышь · обзор\nE · действие     Shift · быстрее", theme.Small);
        }
    }
}
