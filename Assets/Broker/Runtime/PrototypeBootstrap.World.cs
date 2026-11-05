using System.Collections.Generic;
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
            // Behind the wheel nothing else is in reach (the seated walker does not aim).
            if (office.Driving) return;
            if (IsFounderMode && ((target.Station == OfficeStation.Employee && (!day.HasFounderAssistant || target.EmployeeIndex != 0))
                || target.Station == OfficeStation.Supervisor || target.Station == OfficeStation.Upgrade || target.Station == OfficeStation.Coffee)) return;
            // The crossing button only asks the signal for the pedestrians' green: no panel, the clock keeps running.
            if (target.Station == OfficeStation.CrosswalkButton) { office.RequestCrossing(); return; }
            // The test car: behind the wheel in the world, the clock keeps running; a drive costs and earns nothing.
            if (target.Station == OfficeStation.Car) { if (office.EnterCar()) carHintUntil = Time.realtimeSinceStartupAsDouble + 10; return; }
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
            || station == OfficeStation.CityExchange || station == OfficeStation.CityCafe || station == OfficeStation.CrosswalkButton
            || station == OfficeStation.Car;
        // Driving HUD: the controls row for the first seconds behind the wheel, the parking guide in gold after E away from the lay-by.
        private double carHintUntil, carNoticeUntil;
        // E in the driver's seat: out in the lay-by, elsewhere only the notice.
        private void OnLeaveCarRequest()
        {
            if (!ApplicationActive || selectingMode || intro || UtilityOverlayOpen || day.DayEnded || day.PendingEvent != null || !navigation.IsExploring || !office.Driving) return;
            if (!office.TryLeaveCar()) carNoticeUntil = Time.realtimeSinceStartupAsDouble + 2.5;
        }
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
        // Stage 8: the HUD in pictograms. Core/HudPlan lays it out (the scene preview draws the same plan); this gathers what
        // it shows from the day and the scene and paints its items.
        private readonly HudFrame hudFrame = new HudFrame();
        private HudPlan hud;
        private HudPlan Hud => hud ?? (hud = new HudPlan(theme));
        private void DrawWorldHUD()
        {
            var f = hudFrame; var player = office.Player; double now = Time.realtimeSinceStartupAsDouble;
            f.Day = day.DayNumber; f.Hour = DayCycle.HourOf(day); f.Employee = IsEmployeeMode;
            f.Cash = day.Cash; f.Expenses = day.TotalExpenses; f.Deals = day.DealsCount; f.SalesTarget = day.SalesTarget;
            f.Guidance = IsEmployeeMode || IsFounderMode ? day.EmploymentNextAction : null;
            f.Exploring = navigation.IsExploring && day.PendingEvent == null;
            f.InputAvailable = OfficeInput.Available; f.Captured = player.Captured;
            f.InCity = office.IsInCity; f.Driving = office.Driving;
            var target = f.Driving ? null : player.Target;
            f.Target = target != null ? target.Title : null; f.TargetStation = target != null ? target.Station : OfficeStation.Desk;
            f.CanLeaveCar = office.CanLeaveCar; f.CarNotice = now < carNoticeUntil;
            f.SpeedKmh = office.CarSpeedKmh; f.Gear = office.CarGear;
            f.X = office.MapX; f.Z = office.MapZ; f.Heading = office.MapHeading; f.DestinationId = cityDestinationId;
            f.Crossing = office.CrossingSignal; f.Time = now;
            f.WalkKeys = day.RemainingSeconds > BrokerDay.DurationSeconds - 15; f.DriveKeys = now < carHintUntil;
            f.Toast = officeNews.Count > 0 && now < toastUntil ? officeNews[officeNews.Count - 1] : null;
            DrawHud(Hud.Build(f));
        }
        // Paints HudPlan items over the world (or over a panel: the controls rows of the pause and the start of the day).
        private void DrawHud(IReadOnlyList<HudItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i]; var color = theme.HudColor(item.Tone);
                switch (item.Kind)
                {
                    case HudKind.Text: HudLabel(item.X, item.Y, item.W, item.H, item.Text, theme.HudStyle(item.Font, item.Align), color); break;
                    case HudKind.Icon:
                        HudPicture(item.X, item.Y, item.W, item.H, theme.Icon(item.Icon, item.Icon == HudIcon.Arrow ? HudIcons.Sector(item.Angle) : 0), color);
                        break;
                    case HudKind.Key:
                        KeyOutline(item.X + 2, item.Y + 2, item.W, item.H, theme.HudShadow); KeyOutline(item.X, item.Y, item.W, item.H, color);
                        if (!string.IsNullOrEmpty(item.Text)) HudLabel(item.X, item.Y, item.W, item.H, item.Text, theme.HudStyle(item.Font, HudAlign.Middle), color);
                        break;
                    case HudKind.Meter: HudMeter(item.X, item.Y, item.W, item.H, item.Value, color); break;
                    case HudKind.Crosshair: Crosshair(item.Active); break;
                }
            }
        }
        // HUD text on the world: the same text in the shadow colour one and two pixels down and right (a solid edge that reads
        // on light paving and sky), then the text itself in its tone (the HUD styles are white, GUI.contentColor tints them).
        private void HudLabel(float x, float y, float w, float h, string text, GUIStyle style, Color color)
        {
            var tint = GUI.contentColor; GUI.contentColor = theme.HudShadow;
            GUI.Label(new Rect(x + 1, y + 1, w, h), text, style); GUI.Label(new Rect(x + 2, y + 2, w, h), text, style);
            GUI.contentColor = color; GUI.Label(new Rect(x, y, w, h), text, style); GUI.contentColor = tint;
        }
        // A white pictogram tinted by GUI.color, with the same two shadow passes.
        private void HudPicture(float x, float y, float w, float h, Texture2D texture, Color color)
        {
            var tint = GUI.color; GUI.color = theme.HudShadow;
            GUI.DrawTexture(new Rect(x + 1, y + 1, w, h), texture); GUI.DrawTexture(new Rect(x + 2, y + 2, w, h), texture);
            GUI.color = color; GUI.DrawTexture(new Rect(x, y, w, h), texture); GUI.color = tint;
        }
        // Thin bar on a dark translucent track, with the same drop shadow.
        private void HudMeter(float x, float y, float w, float h, float value, Color color)
        {
            Paint(new Rect(x + 2, y + 2, w, h), theme.HudShadow); Paint(new Rect(x, y, w, h), theme.HudTrack);
            Paint(new Rect(x, y, w * Mathf.Clamp01(value), h), color);
        }
        // A key: a 2-pixel outline.
        private void KeyOutline(float x, float y, float w, float h, Color color)
        {
            const float t = 2;
            Paint(new Rect(x, y, w, t), color); Paint(new Rect(x, y + h - t, w, t), color);
            Paint(new Rect(x, y + t, t, h - 2 * t), color); Paint(new Rect(x + w - t, y + t, t, h - 2 * t), color);
        }
        // Thin white cross with a soft shadow; it widens and turns gold over an interactable.
        private void Crosshair(bool target)
        {
            float arm = target ? 10 : 7; var color = target ? theme.Gold : new Color(1, 1, 1, 0.9f); var shadow = new Color(0, 0, 0, 0.35f);
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
            else DrawHud(Hud.Controls(office.Driving, true, 716));
        }
    }
}
