using System;

namespace Broker.Core
{
    [Serializable]
    public sealed class CareerDayReport
    {
        public int day, openingCash, earned, spent, expenses, balance, deals, reputation;
        public bool goldenPhone;
        public bool[] sold = new bool[3];
    }

    // Public fields intentionally support Unity JsonUtility without a JSON dependency in Core.
    [Serializable]
    public sealed class CareerSave
    {
        public int schemaVersion = 1;
        public int completedDays;
        public int balance;
        public int reputation = 50;
        public bool goldenPhone;
        public int[] relationships = new int[3];
        public bool[] lastSold = new bool[3];
        public CareerDayReport[] reports = new CareerDayReport[0];
    }

    public sealed class CareerState
    {
        public const int Length = 3;
        private readonly CareerSave save;
        public int CompletedDays => save.completedDays;
        public int NextDay => CompletedDays + 1;
        public int Balance => save.balance;
        public int Reputation => save.reputation;
        public bool GoldenPhone => save.goldenPhone;
        public bool IsFinished => CompletedDays >= Length;
        public bool Won => IsFinished && Balance >= 0 && Reputation >= 40;
        public CareerDayReport LastReport => CompletedDays == 0 ? null : CopyReport(save.reports[CompletedDays - 1]);
        public int TotalDeals { get { int n = 0; foreach (var r in save.reports) n += r.deals; return n; } }

        public CareerState() { save = new CareerSave(); }
        private CareerState(CareerSave source) { save = Copy(source); }
        public BrokerDay CreateDay()
        {
            if (IsFinished) throw new InvalidOperationException("Career is complete");
            return new BrokerDay(NextDay, Math.Max(0, Balance), Reputation, GoldenPhone,
                (int[])save.relationships.Clone(), (bool[])save.lastSold.Clone(), Math.Max(0, -Balance));
        }
        public bool CompleteDay(BrokerDay day)
        {
            if (day == null || !day.DayEnded || IsFinished || day.DayNumber != NextDay) return false;
            int expectedCash = Math.Max(0, Balance), expectedDebt = Math.Max(0, -Balance);
            if (day.OpeningCash != expectedCash || day.CarryDebt != expectedDebt) return false;
            var report = new CareerDayReport {
                day = day.DayNumber, openingCash = day.OpeningCash, earned = day.Earned, spent = day.Spent,
                expenses = day.TotalExpenses, balance = day.FinalBalance, deals = day.DealsCount,
                reputation = day.Reputation, goldenPhone = day.HasGoldenPhone
            };
            for (int i = 0; i < 3; i++)
            {
                var client = day.Clients[i]; report.sold[i] = client.DealClosed;
                int shift = client.DealClosed ? 1 : client.Call == CallStatus.Closed ? -1 : 0;
                save.relationships[i] = Math.Max(-1, Math.Min(2, save.relationships[i] + shift));
                save.lastSold[i] = client.DealClosed;
            }
            Array.Resize(ref save.reports, CompletedDays + 1); save.reports[CompletedDays] = report;
            save.completedDays++; save.balance = report.balance; save.reputation = report.reputation;
            save.goldenPhone = report.goldenPhone;
            return true;
        }
        public CareerSave Capture() { return Copy(save); }
        public static bool TryRestore(CareerSave candidate, out CareerState state)
        {
            state = null;
            if (!Valid(candidate)) return false;
            state = new CareerState(candidate); return true;
        }
        private static bool Valid(CareerSave s)
        {
            if (s == null || s.schemaVersion != 1 || s.completedDays < 0 || s.completedDays > Length
                || s.balance < -100000 || s.balance > 100000 || s.reputation < 0 || s.reputation > 100
                || s.relationships == null || s.relationships.Length != 3 || s.lastSold == null || s.lastSold.Length != 3
                || s.reports == null || s.reports.Length != s.completedDays) return false;
            foreach (int value in s.relationships) if (value < -1 || value > 2) return false;
            if (s.completedDays == 0)
            {
                if (s.balance != 0 || s.reputation != 50 || s.goldenPhone) return false;
                for (int i = 0; i < 3; i++) if (s.relationships[i] != 0 || s.lastSold[i]) return false;
                return true;
            }
            int previousBalance = 0; bool ownedPhone = false;
            for (int i = 0; i < s.reports.Length; i++)
            {
                var r = s.reports[i];
                if (r == null || r.day != i + 1 || r.openingCash != Math.Max(0, previousBalance)
                    || r.earned < 0 || r.earned > 2000 || r.spent < 0 || r.spent > 5000
                    || r.expenses < 340 + i * 40 + Math.Max(0, -previousBalance) || r.expenses > 100000
                    || r.reputation < 0 || r.reputation > 100 || r.deals < 0 || r.deals > 3
                    || r.sold == null || r.sold.Length != 3 || (ownedPhone && !r.goldenPhone)
                    || r.openingCash + r.earned - r.spent - r.expenses != r.balance) return false;
                int sold = 0; foreach (bool value in r.sold) if (value) sold++;
                if (sold != r.deals) return false;
                previousBalance = r.balance; ownedPhone = r.goldenPhone;
            }
            var last = s.reports[s.reports.Length - 1];
            if (s.balance != last.balance || s.reputation != last.reputation || s.goldenPhone != last.goldenPhone) return false;
            for (int i = 0; i < 3; i++) if (s.lastSold[i] != last.sold[i]) return false;
            return true;
        }
        private static CareerSave Copy(CareerSave s)
        {
            var result = new CareerSave { schemaVersion = s.schemaVersion, completedDays = s.completedDays,
                balance = s.balance, reputation = s.reputation, goldenPhone = s.goldenPhone,
                relationships = (int[])s.relationships.Clone(), lastSold = (bool[])s.lastSold.Clone(),
                reports = new CareerDayReport[s.reports.Length] };
            for (int i = 0; i < s.reports.Length; i++) result.reports[i] = CopyReport(s.reports[i]);
            return result;
        }
        private static CareerDayReport CopyReport(CareerDayReport r)
        {
            return new CareerDayReport { day = r.day, openingCash = r.openingCash, earned = r.earned, spent = r.spent,
                expenses = r.expenses, balance = r.balance, deals = r.deals, reputation = r.reputation,
                goldenPhone = r.goldenPhone, sold = (bool[])r.sold.Clone() };
        }
    }
}
