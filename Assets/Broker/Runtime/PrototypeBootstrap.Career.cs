using System.IO;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private CareerState career;
        private CareerStorage storage;
        private string saveNotice = "Прогресс сохраняется после завершения рабочего дня.";
        private bool saveSucceeded = true;

        private void InitializeCareer()
        {
            storage = new CareerStorage(Path.Combine(Application.persistentDataPath, "broker-career-v1.json"),
                value => JsonUtility.ToJson(value, true), text => JsonUtility.FromJson<CareerSave>(text));
            if (!storage.TryLoad(out career, out string notice)) career = new CareerState();
            if (!string.IsNullOrEmpty(notice)) saveNotice = notice;
            else if (career.CompletedDays > 0) saveNotice = "Загружено завершённых дней: " + career.CompletedDays + ". Незавершённый день начинается заново.";
            Restart();
        }
        private void SettleCareerDay()
        {
            if (career.CompleteDay(day)) SaveCareer();
        }
        private void SaveCareer()
        {
            saveSucceeded = storage.TrySave(career, out saveNotice);
            News((saveSucceeded ? "Сохранение: " : "ВНИМАНИЕ: ") + saveNotice);
        }
        private void NewCareer()
        {
            CancelPending(); career = new CareerState(); SaveCareer(); Restart();
        }
        private void DrawCareerFinal()
        {
            Scrim(); Panel(280, 112, 880, 736, "");
            Label(320, 152, 800, 28, "ТРИ ДНЯ СПУСТЯ", theme.Tag);
            Label(320, 208, 800, 72, career.Won ? "КОНТОРА В ИГРЕ." : "НУЖЕН НОВЫЙ ПЛАН.", theme.Display);
            MetricCard(320, 304, 248, "Итог / долг", "$" + career.Balance);
            MetricCard(592, 304, 248, "Сделки", career.TotalDeals + " / 9");
            MetricCard(864, 304, 256, "Репутация", career.Reputation + "/100");
            var reports = career.Capture().reports;
            for (int i = 0; i < reports.Length; i++)
            {
                var r = reports[i];
                Label(320, 456 + i * 44, 800, 36, "0" + r.day + "     $" + r.balance + "     " + r.deals + "/3 сделки     репутация " + r.reputation, theme.Text);
            }
            if (day.CityWorkTaken) Label(320, 568, 800, 24, day.CityWorkTitle + (day.CityStage == CityWorkStage.Completed ? " · сдано" : " · без награды"), theme.Small);
            Label(320, 600, 800, 72, career.Won ? saveNotice : (career.Balance < 0 ? "Остался долг. " : "Репутация ниже 40. ") + saveNotice, theme.Small);
            if (Button(320, 704, 800, 56, "Новая кампания", true, true)) RequestConfirmation(ConfirmationKind.NewCareer);
            if (!saveSucceeded && Button(320, 776, 800, 48, "Повторить сохранение")) SaveCareer();
        }
    }
}
