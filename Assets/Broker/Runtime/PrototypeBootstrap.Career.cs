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
            Scrim(); Panel(340, 100, 760, 760, "КАМПАНИЯ / ТРИ ДНЯ СПУСТЯ");
            string title = career.Won ? "ВАС ПОКА НЕ ЗАКРЫЛИ." : career.Balance < 0 ? "В КАССЕ НЕ СОШЛОСЬ." : "ДЕНЬГИ ЕСТЬ. ДОВЕРИЯ НЕТ.";
            Label(372, 170, 696, 76, title, theme.Title);
            Label(372, 262, 696, 42, "Основных сделок за кампанию: " + career.TotalDeals + "/9", theme.Heading);
            var reports = career.Capture().reports;
            for (int i = 0; i < reports.Length; i++)
            {
                var r = reports[i];
                Label(372, 326 + i * 64, 696, 48,
                    "День " + r.day + "  /  остаток или долг $" + r.balance + "  /  сделок " + r.deals + "/3  /  репутация " + r.reputation, theme.Text);
            }
            Label(372, 540, 696, 66, "Итоговый баланс: $" + career.Balance + ". Репутация: " + career.Reputation + "/100.\nЦель: выйти без долга и сохранить не меньше 40 репутации.", theme.Text);
            Label(372, 624, 696, 60, (saveSucceeded ? "" : "ВНИМАНИЕ: ") + saveNotice, theme.Small);
            if (Button(372, 704, 696, 52, "Новая кампания (сбросить прогресс)", true, true)) NewCareer();
            if (!saveSucceeded && Button(372, 778, 696, 44, "Повторить сохранение")) SaveCareer();
        }
    }
}
