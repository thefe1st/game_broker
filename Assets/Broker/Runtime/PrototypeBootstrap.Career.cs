using System.IO;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private WorkCareerState workCareer;
        private WorkCareerStorage workStorage;
        private string workLoadNotice = "";
        private CareerState career;
        private CareerStorage storage;
        private EmploymentState employment;
        private EmploymentStorage employmentStorage;
        private CareerMode mode = CareerMode.Employee;
        private bool selectingMode = true;
        private bool IsWorkMode => mode == CareerMode.EmployeeWork;
        private bool IsEmployeeMode => mode != CareerMode.Owner;
        private bool CurrentCareerFinished => !IsWorkMode && (IsEmployeeMode ? employment.IsIntroComplete : career.IsFinished);
        private string saveNotice = "Прогресс сохраняется после завершения рабочего дня.";
        private bool saveSucceeded = true;

        private void InitializeCareer()
        {
            storage = new CareerStorage(Path.Combine(Application.persistentDataPath, "broker-career-v1.json"),
                value => JsonUtility.ToJson(value, true), text => JsonUtility.FromJson<CareerSave>(text));
            employmentStorage = new EmploymentStorage(Path.Combine(Application.persistentDataPath, "broker-employee-intro-v1.json"),
                value => JsonUtility.ToJson(value, true), text => JsonUtility.FromJson<EmploymentSave>(text));
            workStorage = new WorkCareerStorage(Path.Combine(Application.persistentDataPath, "broker-employee-work-v1.json"),
                value => JsonUtility.ToJson(value, true), text => JsonUtility.FromJson<WorkCareerSave>(text));
            workStorage.TryLoad(out workCareer, out workLoadNotice);
            LoadMode(CareerMode.Employee);
            selectingMode = true;
        }
        private void LoadMode(CareerMode selected)
        {
            if (selected == CareerMode.EmployeeWork && !workStorage.TryLoad(out workCareer, out workLoadNotice))
            { selectingMode = true; return; }
            mode = selected; saveSucceeded = true; saveNotice = "Прогресс сохраняется только между днями. Текущий день при выходе начнётся заново.";
            string notice;
            if (IsWorkMode) notice = workLoadNotice;
            else if (IsEmployeeMode)
            {
                if (!employmentStorage.TryLoad(out employment, out notice)) employment = new EmploymentState();
                if (career == null) career = new CareerState();
            }
            else
            {
                if (!storage.TryLoad(out career, out notice)) career = new CareerState();
            }
            if (!string.IsNullOrEmpty(notice)) saveNotice = notice;
            selectingMode = false; Restart();
        }
        private void SettleCareerDay()
        {
            bool settled = IsWorkMode ? workCareer.CompleteDay(day) : IsEmployeeMode ? employment.CompleteDay(day) : career.CompleteDay(day);
            if (settled) SaveCareer();
        }
        private void SaveCareer()
        {
            saveSucceeded = IsWorkMode ? workStorage.TrySave(workCareer, out saveNotice) : IsEmployeeMode ? employmentStorage.TrySave(employment, out saveNotice) : storage.TrySave(career, out saveNotice);
            News((saveSucceeded ? "Сохранение: " : "ВНИМАНИЕ: ") + saveNotice);
        }
        private void NewCareer()
        {
            CancelPending();
            if (IsWorkMode) workCareer = workCareer.RestartFromOrigin();
            else if (IsEmployeeMode) employment = new EmploymentState(); else career = new CareerState();
            SaveCareer(); Restart();
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
            if (saveSucceeded && Button(320, 776, 800, 48, "Выбрать режим")) selectingMode = true;
            Label(320, 600, 800, 72, career.Won ? saveNotice : (career.Balance < 0 ? "Остался долг. " : "Репутация ниже 40. ") + saveNotice, theme.Small);
            if (Button(320, 704, 800, 56, "Новая кампания", true, true)) RequestConfirmation(ConfirmationKind.NewCareer);
            if (!saveSucceeded && Button(320, 776, 800, 48, "Повторить сохранение")) SaveCareer();
        }
    }
}
