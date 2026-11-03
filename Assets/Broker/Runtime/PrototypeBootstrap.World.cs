using System;
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
            if (!ApplicationActive || selectingMode || intro || busy || voice.Working || voice.Recording || UtilityOverlayOpen || day.DayEnded || day.PendingEvent != null || !navigation.IsExploring) return;
            if (IsStreetStation(target.Station) && !office.IsInCity) return;
            if (IsFounderMode && ((target.Station == OfficeStation.Employee && (!day.HasFounderAssistant || target.EmployeeIndex != 0))
                || target.Station == OfficeStation.Supervisor || target.Station == OfficeStation.Upgrade || target.Station == OfficeStation.Coffee)) return;
            // The crossing button only asks the signal for the pedestrians' green: no panel, the clock keeps running.
            if (target.Station == OfficeStation.CrosswalkButton) { office.RequestCrossing(); return; }
            if (target.Station == OfficeStation.Street || target.Station == OfficeStation.ReturnOffice)
            {
                CancelPending(); office.VisitCity(target.Station == OfficeStation.Street); EnterWorld(); return;
            }
            if (target.Station == OfficeStation.CityContact || target.Station == OfficeStation.CityPickup) return; // Temporarily hidden, not deleted.
            if (navigation.Open(target.Station, target.EmployeeIndex))
            {
                cityMapFromPause = false;
                staffTargetIndex = day.SelectedClientIndex;
                office.Player.SetControl(false);
            }
        }
        private static bool IsStreetStation(OfficeStation station) => station == OfficeStation.CityMap || station == OfficeStation.CityCompanies
            || station == OfficeStation.CityExchange || station == OfficeStation.CityCafe || station == OfficeStation.CrosswalkButton;
        private static bool IsCityInformation(OfficeView view) => view == OfficeView.CityMap || view == OfficeView.CityCompanies
            || view == OfficeView.CityExchange || view == OfficeView.CityCafe;
        private void EnterWorld()
        {
            if (!ApplicationActive || busy || UtilityOverlayOpen || voice.Recording || voice.Working) return;
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
            HudPanel(32, 32, 320, 80); Label(56, 44, 272, 32, "МОЙКА КАПИТАЛ", theme.Heading);
            Label(56, 80, 272, 24, office.IsInCity ? "Деловой квартал" : (IsFounderMode ? "Своя контора · день " : IsEmployeeMode ? "Наёмный брокер · день " : "Офис · день ") + day.DayNumber, theme.Small);
            HudPanel(616, 32, 208, 80); Label(648, 48, 160, 44, ClockText(), theme.Title);
            HudPanel(1104, 32, 304, 80); Label(1128, 44, 256, 36, IsEmployeeMode ? "$" + day.Cash + " · личные" : "$" + day.Cash + " / " + day.TotalExpenses, theme.Heading);
            Meter(1128, 96, 256, IsEmployeeMode ? day.DealsCount / (float)day.SalesTarget : day.Cash / (float)day.TotalExpenses, theme.Accent);
            if (!navigation.IsExploring || day.PendingEvent != null) return;
            var player = office.Player;
            Crosshair(player.Target != null);
            string target;
            if (!OfficeInput.Available) target = "Ввод недоступен · открой рабочий экран";
            else if (!player.Captured) target = "Нажми в окне Game";
            else if (player.Target != null) target = player.Target.Title.Split('/')[0].Trim();
            else target = office.IsInCity ? "Прогулка · лифт ведёт в офис" : "Телефон — звонки";
            HudPanel(420, 784, 600, 88);
            if (player.Target != null) { HudFrame(444, 804, 48, 48, 2); Label(459, 811, 32, 36, "E", theme.Heading); }
            Label(player.Target != null ? 516 : 452, 808, player.Target != null ? 476 : 536, 44, target, theme.Heading);
            if (IsEmployeeMode || IsFounderMode)
            {
                string guidance = office.IsInCity ? "Вернитесь в офис через лифт. " + day.EmploymentNextAction : day.EmploymentNextAction;
                float guidanceHeight = Mathf.Max(56, theme.Small.CalcHeight(new GUIContent(guidance), 624));
                HudPanel(384, 136, 672, guidanceHeight + 68);
                Label(408, 152, 624, 24, IsFounderMode ? "ДАЛЬШЕ / СВОЯ КОНТОРА" : "ДАЛЬШЕ / ОРИЕНТИР " + day.DealsCount + "/" + day.SalesTarget, theme.Tag);
                Label(408, 192, 624, guidanceHeight, guidance, theme.Small);
            }
            if (day.RemainingSeconds > BrokerDay.DurationSeconds - 15)
                Label(420, 896, 600, 28, "WASD · ходить     Мышь · обзор     E · действие     Esc · пауза", theme.Small);
            else if (officeNews.Count > 0 && Time.realtimeSinceStartupAsDouble < toastUntil)
            {
                string news = officeNews[officeNews.Count - 1]; if (news.Length > 105) news = news.Substring(0, 102) + "…";
                Label(240, 896, 960, 44, news, theme.Small);
            }
            if (office.IsInCity && navigation.IsExploring)
            {
                var destination = CityDistrictPlan.Find(cityDestinationId);
                HudPanel(32, 288, 320, 144);
                Label(56, 308, 272, 28, destination.Name, theme.Heading);
                Label(56, 348, 272, 64, Math.Ceiling(CityDistrictPlan.Distance(office.MapX, office.MapZ, destination)) + " м · "
                    + CityDistrictPlan.Direction(office.MapX, office.MapZ, destination) + "\nКарта — у стенда / в паузе", theme.Small);
                string crossing = office.CrossingHint;
                if (crossing != null)
                {
                    HudPanel(32, 448, 320, 96);
                    Label(56, 462, 272, 24, "ПЕРЕХОД / СВЕТОФОР", theme.Tag);
                    Label(56, 490, 272, 48, crossing, theme.Small);
                }
            }
        }
        // Exploration HUD: a light translucent wash with thin white rules instead of solid cards,
        // so the voxel city stays visible around the text.
        private void HudPanel(float x, float y, float w, float h)
        {
            Paint(new Rect(x, y, w, h), new Color(0.04f, 0.05f, 0.07f, 0.34f));
            HudFrame(x, y, w, h, 12);
        }
        private void HudFrame(float x, float y, float w, float h, float corner)
        {
            var line = new Color(1, 1, 1, 0.82f); const float t = 1.5f;
            Paint(new Rect(x, y, w, t), line); Paint(new Rect(x, y + h - t, w, t), line);
            Paint(new Rect(x, y, t, corner), line); Paint(new Rect(x + w - t, y, t, corner), line);
            Paint(new Rect(x, y + h - corner, t, corner), line); Paint(new Rect(x + w - t, y + h - corner, t, corner), line);
        }
        // Thin white cross with a soft shadow; it widens and takes the accent colour over an interactable.
        private void Crosshair(bool target)
        {
            float arm = target ? 10 : 7; var color = target ? theme.Accent : new Color(1, 1, 1, 0.9f); var shadow = new Color(0, 0, 0, 0.35f);
            Paint(new Rect(720 - arm + 1, 480, arm * 2, 2), shadow); Paint(new Rect(720, 480 - arm + 1, 2, arm * 2), shadow);
            Paint(new Rect(720 - arm, 479, arm * 2, 2), color); Paint(new Rect(719, 480 - arm, 2, arm * 2), color);
        }
        private void DrawStationPanel()
        {
            Scrim();
            switch (navigation.View)
            {
                case OfficeView.Employee: if (IsEmployeeMode) DrawColleaguePanel(); else DrawEmployeePanel(); break;
                case OfficeView.Supervisor: DrawSupervisorPanel(); break;
                case OfficeView.Coffee: if (IsEmployeeMode) DrawEmployerPropertyPanel(); else DrawShopPanel(false); break;
                case OfficeView.Upgrade: if (IsEmployeeMode) DrawEmployerPropertyPanel(); else DrawShopPanel(true); break;
                case OfficeView.Exit: DrawExitPanel(); break;
                case OfficeView.Pause: DrawPausePanel(); break;
                case OfficeView.CityMap: DrawCityMap(); break;
                case OfficeView.CityCompanies: DrawCityCompanies(); break;
                case OfficeView.CityExchange: DrawCityExchange(); break;
                case OfficeView.CityCafe: DrawCityCafe(); break;
            }
        }
        private void DrawEmployeePanel()
        {
            if (IsFounderMode) { DrawAssistantPanel(); return; }
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
        private void DrawAssistantPanel()
        {
            Panel(280, 188, 880, 584, ""); Label(320, 224, 800, 48, "Лена / помощник по досье", theme.Title);
            Label(320, 296, 800, 72, "Зарплата $40 за день, даже без поручений. Досье 25 игровых секунд: +1 доверия один раз. В этом окне время на паузе; вернитесь к работе, чтобы поручение выполнялось.", theme.Small);
            for (int i = 0; i < 3; i++) if (Button(320 + i * 272, 392, 256, 56, day.Clients[i].Name, true, i == staffTargetIndex)) staffTargetIndex = i;
            if (Button(320, 488, 800, 64, "Подготовить досье / 25 с", CanAssignDossier(staffTargetIndex), true))
            { News(day.AssignTask(0, WorkKind.Research, staffTargetIndex)); EnterWorld(); }
            Label(320, 584, 800, 80, DossierStatus(staffTargetIndex), theme.Small);
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
            if (IsEmployeeMode) { DrawEmploymentExit(); return; }
            Panel(280, 220, 880, 520, ""); Label(320, 260, 800, 48, "Закончить день?", theme.Title);
            MetricCard(320, 344, 248, "Касса", "$" + day.Cash);
            MetricCard(592, 344, 248, "Счета", "$" + day.TotalExpenses);
            MetricCard(864, 344, 256, "Остаток / долг", "$" + day.FinalBalance);
            Label(320, 488, 800, 48, IsFounderMode ? "Звонки и досье остановятся. Аренда $60 и зарплата $" + day.DailyPayroll + " начислятся один раз; резерв не расходуется." : "Звонки и поручения остановятся. Итоги сохранятся.", theme.Small);
            if (Button(320, 552, 800, 56, IsFounderMode ? "Закончить день конторы" : "Закрыть контору", true, true)) { CancelPending(); day.EndDay(); }
            if (Button(320, 640, 800, 56, "Ещё поработать · Esc")) EnterWorld();
        }
        private void DrawPausePanel()
        {
            Panel(360, 156, 720, 648, ""); Label(400, 196, 640, 56, "Рынок подождёт.", theme.Title);
            if (Button(400, 296, 640, 64, "Продолжить · Esc", ApplicationActive, true)) ResumeGameplay();
            if (Button(400, 384, IsWorkMode || IsFounderMode ? 312 : 640, 56, "Журнал")) showNews = true;
            if ((IsWorkMode || IsFounderMode) && Button(728, 384, 312, 56, "База клиентов", !busy && !voice.Working && !voice.Recording)) OpenWorkNotebook();
            if (Button(400, 464, IsWorkMode ? 312 : 640, 56, "Микрофон и связь")) OpenVoiceSetup();
            if (IsWorkMode && Button(728, 464, 312, 56, "Карьера", !busy && !voice.Working && !voice.Recording)) OpenCareerPath();
            if (Button(400, 544, 312, 56, "Начать день заново")) RequestConfirmation(ConfirmationKind.RestartDay);
            if (Button(728, 544, 312, 56, "Карта района", !busy && !voice.Working && !voice.Recording)) OpenCityMapFromPause();
            if (Button(400, 624, 640, 48, "Выбрать режим")) RequestConfirmation(ConfirmationKind.SelectMode);
            if (!saveSucceeded) { if (Button(400, 724, 640, 48, "Повторить сохранение")) SaveCareer(); }
            else Label(400, 700, 640, 64, "WASD · ходить     Мышь · обзор\nE · действие     Shift · быстрее", theme.Small);
        }
    }
}
