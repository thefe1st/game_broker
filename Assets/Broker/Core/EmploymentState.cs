using System;

namespace Broker.Core
{
    [Serializable]
    public sealed class EmploymentDayReport
    {
        public int day, openingSavings, firmRevenue, personalEarned, savings, deals, reputation;
        public bool[] sold = new bool[3];
        public bool[] closed = new bool[3];
    }

    [Serializable]
    public sealed class EmploymentSave
    {
        public int schemaVersion;
        public string mode;
        public int completedIntroDays;
        public int personalSavings;
        public int reputation = 50;
        public int[] relationships = new int[3];
        public bool[] lastSold = new bool[3];
        public EmploymentDayReport[] reports = new EmploymentDayReport[0];
    }

    public sealed class EmploymentState
    {
        public const int IntroLength = 3;
        private readonly EmploymentSave save;
        public int CompletedDays => save.completedIntroDays;
        public bool IsIntroComplete => CompletedDays == IntroLength;
        public int PersonalSavings => save.personalSavings;
        public int Reputation => save.reputation;
        public int TotalDeals { get { int n = 0; foreach (var r in save.reports) n += r.deals; return n; } }
        public int TotalFirmRevenue { get { int n = 0; foreach (var r in save.reports) n += r.firmRevenue; return n; } }
        public EmploymentDayReport LastReport => CompletedDays == 0 ? null : CopyReport(save.reports[CompletedDays - 1]);

        public EmploymentState() { save = new EmploymentSave { schemaVersion = 1, mode = "employee-intro" }; }
        private EmploymentState(EmploymentSave source) { save = Copy(source); }
        public BrokerDay CreateDay()
        {
            if (IsIntroComplete) throw new InvalidOperationException("Only the introductory chapter is available in this build.");
            return new BrokerDay(CompletedDays + 1, PersonalSavings, Reputation, false,
                (int[])save.relationships.Clone(), (bool[])save.lastSold.Clone(), 0, CareerMode.Employee);
        }
        public bool CompleteDay(BrokerDay day)
        {
            if (day == null || !day.IsEmployee || !day.DayEnded || IsIntroComplete || day.DayNumber != CompletedDays + 1
                || day.OpeningCash != PersonalSavings || day.CarryDebt != 0 || day.Spent != 0 || day.TotalExpenses != 0) return false;
            var r = new EmploymentDayReport { day = day.DayNumber, openingSavings = day.OpeningCash,
                firmRevenue = day.FirmRevenue, personalEarned = day.Earned, savings = day.Cash,
                deals = day.DealsCount, reputation = day.Reputation };
            for (int i = 0; i < 3; i++)
            {
                var client = day.Clients[i]; r.sold[i] = client.DealClosed; r.closed[i] = client.Call == CallStatus.Closed;
            }
            var candidate = Capture();
            Array.Resize(ref candidate.reports, CompletedDays + 1); candidate.reports[CompletedDays] = r;
            candidate.completedIntroDays++; candidate.personalSavings = r.savings; candidate.reputation = r.reputation;
            for (int i = 0; i < 3; i++)
            {
                candidate.relationships[i] = Shift(candidate.relationships[i], r.sold[i], r.closed[i]);
                candidate.lastSold[i] = r.sold[i];
            }
            if (!Valid(candidate)) return false;
            // Mutate only after the entire ledger has passed validation.
            save.reports = candidate.reports; save.completedIntroDays = candidate.completedIntroDays;
            save.personalSavings = candidate.personalSavings; save.reputation = candidate.reputation;
            save.relationships = candidate.relationships; save.lastSold = candidate.lastSold;
            return true;
        }
        public EmploymentSave Capture() => Copy(save);
        public static bool TryRestore(EmploymentSave candidate, out EmploymentState state)
        {
            state = null;
            if (!Valid(candidate)) return false;
            state = new EmploymentState(candidate); return true;
        }
        private static int Shift(int value, bool sold, bool closed) => Math.Max(-1, Math.Min(2, value + (sold ? 1 : closed ? -1 : 0)));
        private static bool Valid(EmploymentSave s)
        {
            if (s == null || s.schemaVersion != 1 || s.mode != "employee-intro"
                || s.completedIntroDays < 0 || s.completedIntroDays > IntroLength || s.personalSavings < 0 || s.personalSavings > 330
                || s.reputation < 0 || s.reputation > 100 || s.relationships == null || s.relationships.Length != 3
                || s.lastSold == null || s.lastSold.Length != 3 || s.reports == null || s.reports.Length != s.completedIntroDays) return false;
            int previous = 0, reputation = 50;
            int[] relationships = new int[3]; bool[] lastSold = new bool[3];
            int[] commissions = { 150, 220, 180 };
            for (int n = 0; n < s.reports.Length; n++)
            {
                var r = s.reports[n];
                if (r == null || r.day != n + 1 || r.openingSavings != previous || r.sold == null || r.sold.Length != 3
                    || r.closed == null || r.closed.Length != 3 || r.reputation < 0 || r.reputation > 100) return false;
                int gross = 0, earned = 0, deals = 0;
                for (int i = 0; i < 3; i++)
                {
                    if (r.sold[i] && !r.closed[i]) return false;
                    if (r.sold[i]) { deals++; gross += commissions[i]; earned += BrokerDay.PersonalPayout(commissions[i]); }
                    relationships[i] = Shift(relationships[i], r.sold[i], r.closed[i]); lastSold[i] = r.sold[i];
                }
                if (r.deals != deals || r.firmRevenue != gross || r.personalEarned != earned || r.savings != previous + earned) return false;
                previous = r.savings; reputation = r.reputation;
            }
            if (s.personalSavings != previous || s.reputation != reputation) return false;
            for (int i = 0; i < 3; i++) if (s.relationships[i] != relationships[i] || s.lastSold[i] != lastSold[i]) return false;
            return true;
        }
        private static EmploymentDayReport CopyReport(EmploymentDayReport r) => new EmploymentDayReport {
            day = r.day, openingSavings = r.openingSavings, firmRevenue = r.firmRevenue, personalEarned = r.personalEarned,
            savings = r.savings, deals = r.deals, reputation = r.reputation, sold = (bool[])r.sold.Clone(), closed = (bool[])r.closed.Clone() };
        private static EmploymentSave Copy(EmploymentSave s)
        {
            var result = new EmploymentSave { schemaVersion = s.schemaVersion, mode = s.mode, completedIntroDays = s.completedIntroDays,
                personalSavings = s.personalSavings, reputation = s.reputation, relationships = (int[])s.relationships.Clone(),
                lastSold = (bool[])s.lastSold.Clone(), reports = new EmploymentDayReport[s.reports.Length] };
            for (int i = 0; i < s.reports.Length; i++) result.reports[i] = CopyReport(s.reports[i]);
            return result;
        }
    }
}
