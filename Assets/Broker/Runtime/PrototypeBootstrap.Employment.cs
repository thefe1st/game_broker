using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private void DrawModeSelection()
        {
            Scrim(); Panel(200, 96, 1040, 768, "");
            Label(240, 132, 960, 28, "АЛЛО, ЭТО ДЕНЬГИ / РЕЖИМЫ ПРОТОТИПА", theme.Tag);
            Label(240, 184, 960, 64, "С чего начнём?", theme.Display);
            Label(240, 276, 960, 40, "Наёмный брокер — новая вводная глава", theme.Title);
            Label(240, 336, 960, 76, "Три вводных дня: личные звонки, начальник и 20% комиссии. Лена и Макс — коллеги. Дальнейшая карьера и песочница пока не реализованы.", theme.Text);
            if (Button(240, 436, 960, 56, "Начать / продолжить вводную главу", true, true)) LoadMode(CareerMode.Employee);
            Label(240, 540, 960, 40, "Владелец — прежний прототип", theme.Title);
            Label(240, 600, 960, 64, "Три дня управления: сотрудники, счета и покупки. Прежнее сохранение загружается без миграции. Это отдельный режим, не карьерная развилка.", theme.Text);
            if (Button(240, 688, 960, 56, "Открыть кампанию владельца")) LoadMode(CareerMode.Owner);
            Label(240, 788, 960, 40, "Режимы хранят прогресс отдельно. Переключение ничего не сбрасывает.", theme.Small);
        }
        private void DrawEmploymentIntro()
        {
            Scrim(); Panel(200, 96, 1040, 768, "");
            Label(240, 132, 960, 28, "НАЁМНЫЙ БРОКЕР / ВВОДНЫЙ ДЕНЬ " + day.DayNumber + " ИЗ 3", theme.Tag);
            Label(240, 184, 960, 64, day.EmploymentTitle, theme.Title);
            Label(240, 280, 960, 120, day.SupervisorBrief, theme.Text);
            MetricCard(240, 432, 296, "Личные накопления", "$" + day.OpeningCash);
            MetricCard(560, 432, 296, "Ваша ставка", "20%", "От комиссии фирмы");
            MetricCard(880, 432, 320, "Ориентир", day.SalesTarget + " сделки", "Без личных штрафов");
            Label(240, 580, 960, 64, saveNotice, theme.Small);
            if (Button(240, 664, 960, 56, "Начать рабочий день", true, true)) { intro = false; EnterWorld(); }
            if (Button(240, 736, 464, 48, "Проверить микрофон без API")) OpenVoiceSetup();
            if (Button(736, 736, 464, 48, "Выбрать режим", saveSucceeded)) selectingMode = true;
            Label(240, 812, 960, 28, "Телефон — звонки · Орлов слева у входа — план · Дверь — закончить день", theme.Small);
        }
        private void DrawSupervisorWorkspace()
        {
            Panel(344, 184, 1056, 664, "");
            Label(376, 216, 992, 48, "Орлов / руководитель отдела", theme.Title);
            Label(376, 296, 992, 128, day.SupervisorBrief, theme.Text);
            MetricCard(376, 448, 304, "Ориентир", day.DealsCount + " / " + day.SalesTarget);
            MetricCard(704, 448, 304, "Лично вам сегодня", "$" + day.Earned);
            MetricCard(1032, 448, 336, "Осталось фирме", "$" + day.FirmRetained);
            Label(376, 596, 992, 112, day.SupervisorFeedback, theme.Text);
            Label(376, 748, 992, 64, "Цель обучения — личные продажи с раскрытием условий. Управление командой, повышение и открытие фирмы будут отдельными этапами.", theme.Small);
        }
        private void DrawSupervisorPanel()
        {
            Panel(200, 128, 1040, 704, "");
            Label(240, 168, 960, 48, "Орлов / руководитель отдела", theme.Title);
            Label(240, 256, 960, 144, day.SupervisorBrief, theme.Text);
            Label(240, 432, 960, 112, day.SupervisorFeedback, theme.Heading);
            Label(240, 580, 960, 52, "Сейчас " + day.DealsCount + "/" + day.SalesTarget + " сделки. Личная выплата за день: $" + day.Earned + ". Во время разговора с начальником часы стоят.", theme.Small);
            if (Button(240, 712, 960, 56, "К работе · Esc", true, true)) EnterWorld();
        }
        private void DrawColleaguePanel()
        {
            int index = navigation.EmployeeIndex;
            Panel(280, 220, 880, 520, "");
            Label(320, 260, 800, 48, day.Employees[index].Name + " / коллега", theme.Title);
            Label(320, 352, 800, 172, day.ColleagueAdvice(index), theme.Text);
            if (Button(320, 624, 800, 56, "Спасибо · Esc", true, true)) EnterWorld();
        }
        private void DrawEmployerPropertyPanel()
        {
            Panel(280, 220, 880, 520, "");
            Label(320, 260, 800, 48, "Офис работодателя", theme.Title);
            Label(320, 352, 800, 120, "Закупки, аренда и зарплаты — ответственность фирмы. Ваши личные накопления на них не тратятся. Сейчас ваша работа — разговоры с клиентами.", theme.Text);
            if (Button(320, 624, 800, 56, "К работе · Esc", true, true)) EnterWorld();
        }
        private void DrawEmploymentConnection()
        {
            MetricCard(344, 184, 336, "Комиссия фирмы", "$" + day.FirmRevenue);
            MetricCard(704, 184, 336, "Ваша выплата (20%)", "$" + day.Earned);
            MetricCard(1064, 184, 336, "Осталось фирме", "$" + day.FirmRetained);
            Panel(344, 324, 1056, 524, "");
            Label(376, 360, 992, 44, "Связь / внешние функции необязательны", theme.Heading);
            Label(376, 432, 992, 80, "Каждая сделка оплачивается один раз: $150 / $220 / $180 фирме, из них $30 / $44 / $36 лично вам. Можно играть полностью локально.", theme.Text);
            bool before = GUI.enabled; GUI.enabled = before && !busy && day.Call != CallStatus.Active;
            useGateway = GUI.Toggle(new Rect(376, 544, 448, 52), useGateway, (useGateway ? "Вкл" : "Выкл") + " · ИИ-формулировки", theme.Button); GUI.enabled = before;
            bool next = GUI.Toggle(new Rect(856, 544, 512, 52), voiceEnabled, (voiceEnabled ? "Вкл" : "Выкл") + " · Голос → внешний API", theme.Button);
            if (next != voiceEnabled) { voiceEnabled = next; if (!next) voice.Cancel(); }
            Label(376, 628, 992, 64, "Голос: запись отправляется внешнему API только по кнопке. API не проверен; локальный тест микрофона не отправляет звук наружу.", theme.Small);
            if (Button(376, 740, 992, 56, "Проверить микрофон и связь", !busy && !voice.Working && !voice.Recording)) OpenVoiceSetup();
        }
        private void DrawEmploymentExit()
        {
            Panel(240, 188, 960, 584, "");
            Label(280, 228, 880, 48, "Закончить рабочий день?", theme.Title);
            MetricCard(280, 324, 272, "Комиссия фирмы", "$" + day.FirmRevenue);
            MetricCard(584, 324, 272, "Лично вам за день", "$" + day.Earned);
            MetricCard(888, 324, 272, "Ваши накопления", "$" + day.Cash);
            Label(280, 488, 880, 68, "Незакрытые звонки завершатся без комиссии. Итоги и отношения сохранятся. Счета работодателя из личных денег не вычитаются.", theme.Text);
            if (Button(280, 596, 880, 56, "Закончить день", true, true)) { CancelPending(); day.EndDay(); }
            if (Button(280, 680, 880, 48, "Ещё поработать · Esc")) EnterWorld();
        }
        private void DrawEmploymentResults()
        {
            var report = employment.LastReport;
            if (report == null) return;
            bool complete = employment.IsIntroComplete;
            Scrim(); Panel(200, 76, 1040, 808, "");
            Label(240, 112, 960, 28, complete ? "ВВОДНАЯ ГЛАВА ПРОЙДЕНА / НЕ ФИНАЛ КАРЬЕРЫ" : "РАБОЧИЙ ДЕНЬ " + report.day + " ЗАВЕРШЁН", theme.Tag);
            Label(240, 168, 960, 64, complete ? "Первые результаты." : "Первый опыт — уже актив.", theme.Title);
            MetricCard(240, 264, 296, "Ваши накопления", "$" + employment.PersonalSavings);
            MetricCard(560, 264, 296, complete ? "Сделки за главу" : "Сделки за день", complete ? employment.TotalDeals + " / 9" : report.deals + " / 3");
            MetricCard(880, 264, 320, "Доверие клиентов", report.reputation + "/100");
            if (complete)
            {
                var saved = employment.Capture();
                for (int i = 0; i < saved.reports.Length; i++)
                {
                    var r = saved.reports[i];
                    Label(240, 408 + i * 38, 960, 32, "День " + r.day + " · фирме $" + r.firmRevenue + " · лично +$" + r.personalEarned + " · " + r.deals + "/3 сделки", theme.Small);
                }
                Label(240, 532, 960, 48, "Отношения: Борис " + Signed(saved.relationships[0]) + " · Инна " + Signed(saved.relationships[1]) + " · Артём " + Signed(saved.relationships[2]), theme.Small);
                Label(240, 588, 960, 72, "Дальнейшая работа, карьерный выбор и песочница ещё не реализованы. Накопления сохранены отдельно. Автоматического перехода в свою фирму нет.", theme.Text);
            }
            else
            {
                Label(240, 416, 960, 48, "Фирме начислено $" + report.firmRevenue + " − ваша выплата $" + report.personalEarned + " = фирме осталось $" + (report.firmRevenue - report.personalEarned), theme.Heading);
                Label(240, 492, 960, 72, day.SupervisorFeedback, theme.Text);
                Label(240, 596, 960, 48, "Лично: $" + report.openingSavings + " утром + $" + report.personalEarned + " = $" + report.savings + ". Штрафов за ориентир нет.", theme.Small);
            }
            Label(240, 672, 960, 52, saveNotice, theme.Small);
            if (Button(240, 740, 464, 48, complete ? "Выбрать режим" : saveSucceeded ? "Следующий день" : "Продолжить без записи", !complete || saveSucceeded, true))
            { if (complete) { if (saveSucceeded) selectingMode = true; } else Restart(); }
            if (Button(736, 740, 464, 48, saveSucceeded ? "Новая вводная глава" : "Повторить сохранение"))
            { if (saveSucceeded) RequestConfirmation(ConfirmationKind.NewCareer); else SaveCareer(); }
            if (!complete && Button(240, 812, 960, 40, "Выбрать режим", saveSucceeded)) selectingMode = true;
        }
        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
    }
}
