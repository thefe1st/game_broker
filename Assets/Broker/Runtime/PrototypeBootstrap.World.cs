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
            if (intro || busy || day.DayEnded || day.PendingEvent != null || !navigation.IsExploring) return;
            if (navigation.Open(target.Station, target.EmployeeIndex))
            {
                staffTargetIndex = day.SelectedClientIndex;
                office.Player.SetControl(false);
            }
        }
        private void EnterWorld()
        {
            if (busy || voice.Recording || voice.Working) return;
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
            Paint(new Rect(24, 24, 1392, 86), new Color(0.05f, 0.11f, 0.14f, 0.91f));
            Label(44, 40, 430, 34, "МОЙКА КАПИТАЛ / ДЕНЬ " + day.DayNumber + "/3", theme.Heading);
            int seconds = Mathf.CeilToInt(day.RemainingSeconds);
            Label(510, 40, 180, 34, $"{seconds / 60:00}:{seconds % 60:00}", theme.Title);
            Label(720, 40, 210, 34, "$" + day.Cash + " / " + day.TotalExpenses, theme.Title);
            Label(960, 40, 200, 28, "Репутация " + day.Reputation, theme.Text);
            Label(1170, 40, 220, 28, "Команда " + day.TeamMorale + "%", theme.Text);
            Label(44, 78, 1300, 24, "WASD / стрелки — ходить     Мышь — осмотр     E — действие     Shift — быстрее     Esc — пауза", theme.Small);
            if (!navigation.IsExploring || day.PendingEvent != null) return;
            var player = office.Player;
            Paint(new Rect(718, 478, 4, 4), player.Target != null ? theme.Gold : theme.Ink);
            string title, action;
            if (!OfficeInput.Available)
            {
                title = "Ввод пока не активен";
                action = "В Unity: Player → Active Input Handling → Input System Package или Both. Затем перезапустите редактор.";
            }
            else if (!player.Captured)
            {
                title = "Щёлкните по окну Game, чтобы управлять персонажем";
                action = "Если курсор не захватился — откройте паузу Esc и нажмите «Продолжить».";
            }
            else if (player.Target != null) { title = "[ E ] " + player.Target.Title; action = player.Target.Action; }
            else
            {
                title = day.Call == CallStatus.Active ? "Звонок остаётся на линии: " + day.Current.Name : "Подойдите к рабочему столу и наведите взгляд на телефон";
                action = "Лена и Макс — у дальних столов. Кофемашина — слева. Выход — за спиной у входа.";
            }
            Paint(new Rect(300, 760, 840, 112), new Color(0.05f, 0.11f, 0.14f, 0.92f));
            Label(324, 774, 792, 40, title, theme.Heading); Label(324, 818, 792, 44, action, theme.Small);
            if (officeNews.Count > 0)
            {
                string news = officeNews[officeNews.Count - 1]; if (news.Length > 150) news = news.Substring(0, 147) + "…";
                Label(40, 902, 1360, 42, news, theme.Small);
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
            }
        }
        private void DrawEmployeePanel()
        {
            int index = navigation.EmployeeIndex; var worker = day.Employees[index];
            Panel(340, 172, 760, 616, "РАЗГОВОР С СОТРУДНИКОМ / ВРЕМЯ НА ПАУЗЕ");
            Label(372, 230, 696, 42, worker.Name + " / настроение " + worker.Morale + "%", theme.Title);
            Label(372, 286, 696, 60, worker.Trait, theme.Text);
            Label(372, 356, 696, 30, "Для кого подготовить досье:", theme.Small);
            for (int i = 0; i < day.Clients.Count; i++)
            {
                if (Button(372 + i * 236, 398, 224, 48, day.Clients[i].Name + (i == staffTargetIndex ? " / выбран" : ""), true, i == staffTargetIndex)) staffTargetIndex = i;
            }
            bool idle = worker.Work == WorkKind.Idle;
            Label(372, 466, 696, 50, idle ? "Свободен. Досье повышает доверие, обзвон приносит небольшую комиссию." : "Сейчас: " + (worker.Work == WorkKind.Research ? "подготовка досье" : worker.Work == WorkKind.Leads ? "обзвон" : "перерыв") + ". Осталось " + Mathf.CeilToInt(worker.Remaining) + " секунд игрового времени.", theme.Text);
            if (Button(372, 542, 224, 52, "Подготовить досье", idle && !day.Clients[staffTargetIndex].Researched))
            { News(day.AssignTask(index, WorkKind.Research, staffTargetIndex)); EnterWorld(); }
            if (Button(608, 542, 224, 52, "Побочный обзвон", idle && !worker.SideDealDone))
            { News(day.AssignTask(index, WorkKind.Leads, staffTargetIndex)); EnterWorld(); }
            if (Button(844, 542, 224, 52, "Отправить на перерыв", idle))
            { News(day.AssignTask(index, WorkKind.Break, staffTargetIndex)); EnterWorld(); }
            if (Button(372, 686, 696, 52, "Закончить разговор / Esc", true, true)) EnterWorld();
        }
        private void DrawShopPanel(bool upgrade)
        {
            Panel(340, 220, 760, 510, upgrade ? "ШОУРУМ СОЛИДНОСТИ" : "КОФЕ / МОТИВАЦИЯ В СТАКАНЧИКЕ");
            Label(372, 282, 696, 60, upgrade ? "Золотистый телефон — $90" : "Кофе для команды — $40", theme.Title);
            Label(372, 368, 696, 100, upgrade ? "+1 доверия незавершённых клиентов. Цвет телефона на вашем столе изменится.\n\nКлиенты услышат солидность. Бухгалтерия — расход." : "+20 настроения обоим сотрудникам. Покупка один раз за день.\n\nКофеин не заменяет зарплату, но делает ожидание эмоциональнее.", theme.Text);
            int cost = upgrade ? 90 : 40;
            bool bought = upgrade ? day.HasGoldenPhone : day.CoffeeBought;
            Label(372, 490, 696, 36, "В кассе $" + day.Cash + (bought ? " / уже куплено" : day.Cash < cost ? " / пока не хватает" : ""), theme.Tag);
            if (Button(372, 552, 696, 52, bought ? "Уже куплено" : "Купить за $" + cost, !bought && day.Cash >= cost, true))
            { News(upgrade ? day.BuyGoldenPhone() : day.BuyCoffee()); EnterWorld(); }
            if (Button(372, 628, 696, 52, "Отойти / Esc")) EnterWorld();
        }
        private void DrawExitPanel()
        {
            Panel(340, 220, 760, 510, "ВХОДНАЯ ДВЕРЬ / ПОРА ДОМОЙ?");
            Label(372, 288, 696, 64, "Закончить рабочий день?", theme.Title);
            Label(372, 380, 696, 128, "В кассе $" + day.Cash + ". Обязательства: $" + day.TotalExpenses + ".\nПосле расчёта останется / будет долг: $" + day.FinalBalance + ".\n\nНезавершённые звонки и поручения остановятся. После расчёта прогресс сохранится.", theme.Text);
            if (Button(372, 552, 696, 52, "Закрыть контору на сегодня", true, true)) { CancelPending(); day.EndDay(); }
            if (Button(372, 628, 696, 52, "Нет, вернуться к работе / Esc")) EnterWorld();
        }
        private void DrawPausePanel()
        {
            Panel(340, 220, 760, 510, "ПАУЗА / РЫНОК ПОДОЖДЁТ");
            Label(372, 288, 696, 60, "День " + day.DayNumber + "/3 в собственной конторе.", theme.Title);
            Label(372, 376, 696, 100, "Подойдите к столу, сотруднику, кофемашине или двери и нажмите E.\n\nКурсор освобождается в меню. Время на паузе, пока открыто это окно.", theme.Text);
            if (Button(372, 520, 696, 52, "Продолжить / Esc", true, true)) EnterWorld();
            if (Button(372, 594, 696, 52, "Журнал офиса")) showNews = true;
            if (Button(372, 668, 696, 44, "Начать текущий день заново")) Restart();
        }
    }
}
