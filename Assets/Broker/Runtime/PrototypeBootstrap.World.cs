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
        // Driving HUD: the keys for the first seconds behind the wheel, the gold notice after E away from the parking.
        private double carHintUntil, carNoticeUntil;
        private const string LeaveCarTitle = "Выйти из машины", ParkingOnly = "Выйти можно только на парковке";
        private const string DrivingKeys = "W/S · газ и тормоз     A/D · руль     Пробел · ручник     Мышь · обзор     Esc · пауза";
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
        private void DrawWorldHUD()
        {
            // Plain text with a drop shadow straight over the world, no cards: name and place top left, the clock in the
            // middle, money with its gold meter top right, the landmark and the crossing below the name, the target at the bottom.
            HudLabel(40, 30, 360, 30, "МОЙКА КАПИТАЛ", theme.HudHeading);
            HudLabel(40, 60, 360, 24, office.IsInCity ? "Деловой квартал" : (IsFounderMode ? "Своя контора · день " : IsEmployeeMode ? "Наёмный брокер · день " : "Офис · день ") + day.DayNumber, theme.HudText);
            HudLabel(620, 26, 200, 44, ClockText(), theme.HudClock);
            HudLabel(1040, 30, 360, 30, IsEmployeeMode ? "$" + day.Cash + " · личные" : "$" + day.Cash + " / " + day.TotalExpenses, theme.HudRight);
            HudMeter(1144, 68, 256, IsEmployeeMode ? day.DealsCount / (float)day.SalesTarget : day.Cash / (float)day.TotalExpenses, theme.Gold);
            if (!navigation.IsExploring || day.PendingEvent != null) return;
            var player = office.Player;
            bool driving = office.Driving;
            if (!driving) Crosshair(player.Target != null);
            if (!OfficeInput.Available) HudLabel(420, 812, 600, 32, "Ввод недоступен · открой рабочий экран", theme.HudCenter);
            else if (!player.Captured) HudLabel(420, 812, 600, 32, "Нажми в окне Game", theme.HudCenter);
            else if (driving) DrawLeaveCarLine();
            else if (player.Target != null)
            {
                // The key and the title as one centred line; the title and its width are cached per interactable title.
                string title = TargetTitle(player.Target.Title);
                float left = 720 - (40 + 12 + targetTitleWidth) / 2;
                KeyCap(left, 808, 40, "E"); HudLabel(left + 52, 812, targetTitleWidth + 8, 32, title, theme.HudHeading);
            }
            else HudLabel(420, 812, 600, 32, office.IsInCity ? "Прогулка · лифт ведёт в офис" : "Телефон — звонки", theme.HudCenter);
            if (IsEmployeeMode || IsFounderMode)
            {
                string guidance = office.IsInCity ? "Вернитесь в офис через лифт. " + day.EmploymentNextAction : day.EmploymentNextAction;
                float guidanceHeight = Mathf.Max(24, theme.HudText.CalcHeight(new GUIContent(guidance), 560));
                HudLabel(40, 104, 560, 24, IsFounderMode ? "ДАЛЬШЕ / СВОЯ КОНТОРА" : "ДАЛЬШЕ / ОРИЕНТИР " + day.DealsCount + "/" + day.SalesTarget, theme.HudTag);
                HudLabel(40, 130, 560, guidanceHeight, guidance, theme.HudText);
            }
            if (driving) DrawSpeedometer();
            if (driving && Time.realtimeSinceStartupAsDouble < carHintUntil) HudLabel(220, 900, 1000, 28, DrivingKeys, theme.HudHint);
            else if (!driving && day.RemainingSeconds > BrokerDay.DurationSeconds - 15)
                HudLabel(320, 900, 800, 28, "WASD · ходить     Мышь · обзор     E · действие     Esc · пауза", theme.HudHint);
            else if (officeNews.Count > 0 && Time.realtimeSinceStartupAsDouble < toastUntil)
            {
                string news = officeNews[officeNews.Count - 1]; if (news.Length > 105) news = news.Substring(0, 102) + "…";
                HudLabel(240, 892, 960, 48, news, theme.HudHint);
            }
            if (office.IsInCity && navigation.IsExploring)
            {
                var destination = CityDistrictPlan.Find(cityDestinationId);
                HudLabel(40, 288, 360, 30, destination.Name, theme.HudHeading);
                HudLabel(40, 318, 360, 48, Math.Ceiling(CityDistrictPlan.Distance(office.MapX, office.MapZ, destination)) + " м · "
                    + CityDistrictPlan.Direction(office.MapX, office.MapZ, destination) + "\nКарта — у стенда / в паузе", theme.HudText);
                string crossing = office.CrossingHint;
                if (crossing != null)
                {
                    HudLabel(40, 384, 360, 24, "ПЕРЕХОД / СВЕТОФОР", theme.HudTag);
                    HudLabel(40, 410, 360, 48, crossing, theme.HudText);
                }
            }
        }
        // Bottom line in the car: E to get out in the lay-by; elsewhere where to get out, in gold for a moment after E.
        private void DrawLeaveCarLine()
        {
            if (office.CanLeaveCar)
            {
                string title = TargetTitle(LeaveCarTitle);
                float left = 720 - (40 + 12 + targetTitleWidth) / 2;
                KeyCap(left, 808, 40, "E"); HudLabel(left + 52, 812, targetTitleWidth + 8, 32, title, theme.HudHeading);
                return;
            }
            var tint = GUI.contentColor;
            if (Time.realtimeSinceStartupAsDouble < carNoticeUntil) GUI.contentColor = theme.Gold;
            HudLabel(420, 812, 600, 32, ParkingOnly, theme.HudCenter);
            GUI.contentColor = tint;
        }
        // Speed in big figures bottom right, the gear under it.
        private void DrawSpeedometer()
        {
            HudLabel(1160, 764, 240, 52, ((int)Math.Round(office.CarSpeedKmh)).ToString(), theme.HudSpeed);
            HudLabel(1040, 818, 360, 30, office.CarGear == "R" ? "км/ч · задний ход" : "км/ч · передача " + office.CarGear, theme.HudRight);
        }
        // The HUD title of an interactable ("Лифт в офис / …" → "Лифт в офис") and its width in the HUD font,
        // recomputed only when the title string changes, not on every OnGUI call.
        private string targetTitleSource, targetTitle;
        private float targetTitleWidth;
        private string TargetTitle(string source)
        {
            if (ReferenceEquals(source, targetTitleSource)) return targetTitle;
            targetTitleSource = source; targetTitle = (source ?? "").Split('/')[0].Trim();
            targetTitleWidth = Mathf.Min(560, theme.HudHeading.CalcSize(new GUIContent(targetTitle)).x);
            return targetTitle;
        }
        // HUD text on the world: the same text in the shadow colour two pixels down and right, then the text itself.
        private void HudLabel(float x, float y, float w, float h, string text, GUIStyle style)
        {
            var tint = GUI.contentColor; GUI.contentColor = theme.HudShadow;
            GUI.Label(new Rect(x + 2, y + 2, w, h), text, style);
            GUI.contentColor = tint; GUI.Label(new Rect(x, y, w, h), text, style);
        }
        // Thin gold bar on a dark translucent track, with the same drop shadow as the text.
        private void HudMeter(float x, float y, float w, float value, Color color)
        {
            Paint(new Rect(x + 2, y + 2, w, 4), theme.HudShadow); Paint(new Rect(x, y, w, 4), theme.HudTrack);
            Paint(new Rect(x, y, w * Mathf.Clamp01(value), 4), color);
        }
        // The interaction key: a thin warm-white outline with its shadow and the letter centred inside.
        private void KeyCap(float x, float y, float size, string key)
        {
            KeyOutline(x + 2, y + 2, size, theme.HudShadow); KeyOutline(x, y, size, theme.HudInk);
            HudLabel(x, y, size, size, key, theme.HudKey);
        }
        private void KeyOutline(float x, float y, float size, Color color)
        {
            const float t = 2;
            Paint(new Rect(x, y, size, t), color); Paint(new Rect(x, y + size - t, size, t), color);
            Paint(new Rect(x, y + t, t, size - 2 * t), color); Paint(new Rect(x + size - t, y + t, t, size - 2 * t), color);
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
            else Label(400, 700, 640, 64, office.Driving ? "W/S · газ и тормоз     A/D · руль     Пробел · ручник\nE · выйти на парковке у сквера     Мышь · обзор"
                : "WASD · ходить     Мышь · обзор\nE · действие     Shift · быстрее", theme.Small);
        }
    }
}
