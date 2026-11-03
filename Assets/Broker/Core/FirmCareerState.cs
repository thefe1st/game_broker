using System;

namespace Broker.Core
{
    [Serializable] public sealed class FirmCheckpoint
    {
        public int completedDays, balance, reputation;
        public WorkClientState[] clients;
    }
    [Serializable] public sealed class FirmDayReport
    {
        public int day, openingBalance, revenue, rent, salary, balance, reputation;
        public string[] clientIds;
        public bool[] sold, closed, reportReviewed, guaranteed, complaintResolved;
    }
    [Serializable] public sealed class FirmCareerSave
    {
        public int schemaVersion;
        public string mode;
        public int sourceDay, sourceSavings, personalReserve, assistantHireDay;
        public FirmCheckpoint origin, checkpoint;
        public FirmDayReport[] reports;
    }

    // Independent founder ledger. Personal reserve is not an automatic credit line.
    public sealed class FirmCareerState
    {
        public const int RequiredSavings = 600, OpeningCost = 200, InitialCapital = 300, DailyRent = 60, HistoryDays = 7;
        public const int HiringCost = 100, HiringCashRequired = 200, AssistantSalary = 40;
        public const float DossierSeconds = 25f;
        private FirmCareerSave save;
        private FirmCheckpoint current;
        public int CompletedDays => current.completedDays;
        public int OpeningDayNumber => save.sourceDay;
        public int NextDayNumber => checked(OpeningDayNumber + CompletedDays);
        public bool HasAssistant => save.assistantHireDay != 0;
        public int AssistantHireDay => save.assistantHireDay;
        private int PendingHiringCost => HasAssistant && NextDayNumber == AssistantHireDay ? HiringCost : 0;
        public int Balance => current.balance - PendingHiringCost;
        public bool CanHireAssistant => !HasAssistant && Balance >= HiringCashRequired;
        public int PersonalReserve => save.personalReserve;
        public int Reputation => current.reputation;
        public WorkClientState[] Clients => CopyCheckpoint(current).clients;
        public FirmDayReport[] RecentReports
        {
            get { var reports = new FirmDayReport[save.reports.Length];
                for (int i = 0; i < reports.Length; i++) reports[i] = CopyReport(save.reports[i]); return reports; }
        }
        public FirmDayReport LastReport => save.reports.Length == 0 ? null : CopyReport(save.reports[save.reports.Length - 1]);
        public static bool CanOpen(WorkCareerState work) => work != null && work.PersonalSavings >= RequiredSavings
            && work.NextDayNumber <= int.MaxValue - 5;
        private FirmCareerState(FirmCareerSave data, FirmCheckpoint result) { save = Copy(data); current = CopyCheckpoint(result); }
        public static FirmCareerState FromWork(WorkCareerState work)
        {
            if (!CanOpen(work)) throw new ArgumentException("Not enough settled savings to open a firm.");
            var clients = work.Clients;
            foreach (var c in clients) { c.deals = 0; c.lastSeenWorkDay = 0; }
            var origin = new FirmCheckpoint { balance = InitialCapital, reputation = work.Reputation, clients = clients };
            var data = new FirmCareerSave { schemaVersion = 2, mode = "founder", sourceDay = work.NextDayNumber,
                sourceSavings = work.PersonalSavings, personalReserve = work.PersonalSavings - OpeningCost - InitialCapital,
                origin = origin, checkpoint = CopyCheckpoint(origin), reports = new FirmDayReport[0] };
            if (!TryRestore(data, out var state)) throw new InvalidOperationException("Invalid founder seed.");
            return state;
        }
        public BrokerDay CreateDay()
        {
            var ids = ClientCatalog.Appointments(NextDayNumber); var relationships = new int[3]; var returns = new bool[3]; var complaints = new bool[3];
            for (int i = 0; i < 3; i++) { var c = current.clients[ClientCatalog.Index(ids[i])]; relationships[i] = c.relationship; returns[i] = c.awaitingReport; complaints[i] = c.complaintPending; }
            return new BrokerDay(NextDayNumber, Math.Max(0, Balance), Reputation, relationshipBonuses: relationships,
                returning: returns, carryDebt: Math.Max(0, -Balance), mode: CareerMode.Founder, clientIds: ids, complaints: complaints, founderAssistant: HasAssistant);
        }
        // Candidate is activated by runtime only after a successful between-day write.
        public bool TryHireAssistant(out FirmCareerState hired)
        {
            hired = null; if (!CanHireAssistant) return false;
            var candidate = Capture(); candidate.assistantHireDay = NextDayNumber;
            return TryRestore(candidate, out hired);
        }
        public bool CompleteDay(BrokerDay day)
        {
            if (day == null || day.Mode != CareerMode.Founder || !day.DayEnded || day.DayNumber != NextDayNumber
                || day.OpeningCash != Math.Max(0, Balance) || day.CarryDebt != Math.Max(0, -Balance)
                || day.HasFounderAssistant != HasAssistant || day.DailyPayroll != (HasAssistant ? AssistantSalary : 0)
                || day.Spent != 0 || day.TotalExpenses != (long)DailyRent + day.DailyPayroll + day.CarryDebt || day.Earned != day.FirmRevenue) return false;
            var r = new FirmDayReport { day = day.DayNumber, openingBalance = Balance, revenue = day.FirmRevenue,
                rent = DailyRent, salary = day.DailyPayroll, balance = day.FinalBalance, reputation = day.Reputation, clientIds = new string[3],
                sold = new bool[3], closed = new bool[3], reportReviewed = new bool[3], guaranteed = new bool[3], complaintResolved = new bool[3] };
            for (int i = 0; i < 3; i++) { var c = day.Clients[i]; r.clientIds[i] = c.Id; r.sold[i] = c.DealClosed;
                r.closed[i] = c.Call == CallStatus.Closed; r.reportReviewed[i] = c.ReportReviewed; r.guaranteed[i] = c.FalseGuaranteeMade; r.complaintResolved[i] = c.ComplaintResolved; }
            var candidate = Capture(); int length = candidate.reports.Length;
            if (length == HistoryDays)
            {
                if (!Apply(candidate, candidate.checkpoint, candidate.reports[0])) return false;
                Array.Copy(candidate.reports, 1, candidate.reports, 0, length - 1); candidate.reports[length - 1] = r;
            }
            else { Array.Resize(ref candidate.reports, length + 1); candidate.reports[length] = r; }
            if (!TryRestore(candidate, out var validated)) return false;
            save = validated.save; current = validated.current; return true;
        }
        public FirmCareerState RestartFromOrigin()
        {
            var data = Capture(); data.assistantHireDay = 0; data.checkpoint = CopyCheckpoint(data.origin); data.reports = new FirmDayReport[0];
            if (!TryRestore(data, out var state)) throw new InvalidOperationException("Invalid founder origin.");
            return state;
        }
        public FirmCareerSave Capture() => Copy(save);
        internal bool SameSnapshot(FirmCareerState other)
        {
            if (other == null) return false; var a = save; var b = other.save;
            if (a.schemaVersion != b.schemaVersion || a.mode != b.mode || a.sourceDay != b.sourceDay
                || a.sourceSavings != b.sourceSavings || a.personalReserve != b.personalReserve || a.assistantHireDay != b.assistantHireDay
                || !SameCheckpoint(a.origin, b.origin) || !SameCheckpoint(a.checkpoint, b.checkpoint) || a.reports.Length != b.reports.Length) return false;
            for (int n = 0; n < a.reports.Length; n++)
            {
                var x = a.reports[n]; var y = b.reports[n];
                if (x.day != y.day || x.openingBalance != y.openingBalance || x.revenue != y.revenue || x.rent != y.rent
                    || x.salary != y.salary || x.balance != y.balance || x.reputation != y.reputation) return false;
                for (int i = 0; i < 3; i++) if (x.clientIds[i] != y.clientIds[i] || x.sold[i] != y.sold[i] || x.closed[i] != y.closed[i]
                    || x.reportReviewed[i] != y.reportReviewed[i] || x.guaranteed[i] != y.guaranteed[i] || x.complaintResolved[i] != y.complaintResolved[i]) return false;
            }
            return true;
        }
        public static bool TryRestore(FirmCareerSave s, out FirmCareerState state)
        {
            state = null;
            try
            {
                if (s == null || (s.schemaVersion != 1 && s.schemaVersion != 2) || s.mode != "founder" || s.sourceDay < 4 || s.sourceDay > int.MaxValue - 5
                    || s.sourceSavings < RequiredSavings || s.sourceSavings > int.MaxValue - 1000
                    || s.personalReserve != s.sourceSavings - OpeningCost - InitialCapital
                    || s.assistantHireDay < 0 || (s.assistantHireDay != 0 && s.assistantHireDay < s.sourceDay)
                    || (s.schemaVersion == 1 && s.assistantHireDay != 0)
                    || !ValidCheckpoint(s, s.origin) || !ValidCheckpoint(s, s.checkpoint)
                    || s.origin.completedDays != 0 || s.origin.balance != InitialCapital
                    || s.reports == null || s.reports.Length > HistoryDays
                    || (s.checkpoint.completedDays > 0 && s.reports.Length != HistoryDays)) return false;
                if (s.checkpoint.completedDays == 0 && !SameCheckpoint(s.origin, s.checkpoint)) return false;
                var result = CopyCheckpoint(s.checkpoint);
                foreach (var r in s.reports) if (!Apply(s, result, r)) return false;
                long next = (long)s.sourceDay + result.completedDays;
                if (s.assistantHireDay > next || (s.assistantHireDay == next && result.balance < HiringCashRequired)) return false;
                state = new FirmCareerState(s, result); state.save.schemaVersion = 2; return true;
            }
            catch (OverflowException) { return false; }
        }
        private static bool ValidCheckpoint(FirmCareerSave s, FirmCheckpoint c)
        {
            if (c == null || c.completedDays < 0 || (long)s.sourceDay + c.completedDays > int.MaxValue - 1
                || c.balance < -int.MaxValue + 1000 || c.balance > int.MaxValue - 1000
                || c.reputation < 0 || c.reputation > 100 || c.clients == null || c.clients.Length != 6) return false;
            string[] ids = ClientCatalog.Ids; long revenue = 0;
            for (int i = 0; i < 6; i++)
            {
                var client = c.clients[i];
                if (client == null || client.id != ids[i] || client.relationship < -1 || client.relationship > 2
                    || client.deals < 0 || client.deals > c.completedDays || client.lastSeenWorkDay < 0
                    || client.lastSeenWorkDay > c.completedDays || (client.deals > 0 && client.lastSeenWorkDay == 0)) return false;
                revenue += (long)client.deals * ClientCatalog.Create(ids[i]).Commission;
            }
            long paidDays = s.assistantHireDay == 0 ? 0 : Math.Max(0, (long)s.sourceDay + c.completedDays - s.assistantHireDay);
            return c.balance == InitialCapital + revenue - (long)c.completedDays * DailyRent
                - paidDays * AssistantSalary - (paidDays > 0 ? HiringCost : 0);
        }
        private static bool Apply(FirmCareerSave s, FirmCheckpoint c, FirmDayReport r)
        {
            bool hiring = r != null && r.day == s.assistantHireDay;
            int salary = s.assistantHireDay != 0 && r != null && r.day >= s.assistantHireDay ? AssistantSalary : 0;
            if (r == null || r.day != (long)s.sourceDay + c.completedDays || r.openingBalance != (long)c.balance - (hiring ? HiringCost : 0)
                || (hiring && c.balance < HiringCashRequired) || r.salary != salary || (s.schemaVersion == 1 && r.salary != 0)
                || r.rent != DailyRent || r.reputation < 0 || r.reputation > 100
                || r.clientIds == null || r.clientIds.Length != 3 || r.sold == null || r.sold.Length != 3
                || r.closed == null || r.closed.Length != 3 || r.reportReviewed == null || r.reportReviewed.Length != 3
                || r.guaranteed == null || r.guaranteed.Length != 3 || r.complaintResolved == null || r.complaintResolved.Length != 3) return false;
            var ids = ClientCatalog.Appointments(r.day); int revenue = 0;
            for (int i = 0; i < 3; i++)
            {
                var client = c.clients[ClientCatalog.Index(ids[i])];
                if (r.clientIds[i] != ids[i] || (r.sold[i] && (!r.closed[i] || !r.reportReviewed[i]))
                    || (!client.awaitingReport && !r.reportReviewed[i])
                    || (client.awaitingReport && r.reportReviewed[i] && !r.closed[i])
                    || (r.guaranteed[i] && !r.closed[i])
                    || (r.complaintResolved[i] && (!client.complaintPending || !r.closed[i] || r.guaranteed[i]))
                    || (r.sold[i] && client.complaintPending && !r.complaintResolved[i])) return false;
                if (r.sold[i]) { revenue += ClientCatalog.Create(ids[i]).Commission; client.deals = checked(client.deals + 1); }
                int change = r.sold[i] ? 1 : r.complaintResolved[i] ? 0 : r.closed[i] ? -1 : 0;
                change += (r.complaintResolved[i] ? 1 : 0) - (r.guaranteed[i] ? 1 : 0);
                client.relationship = Math.Max(-1, Math.Min(2, client.relationship + change));
                client.complaintPending = r.guaranteed[i] || (client.complaintPending && !r.complaintResolved[i]);
                client.awaitingReport = r.sold[i] || (client.awaitingReport && !r.reportReviewed[i]);
                client.lastSeenWorkDay = checked(c.completedDays + 1);
            }
            if (r.revenue != revenue || r.balance != (long)r.openingBalance + revenue - DailyRent - salary) return false;
            c.balance = r.balance; c.reputation = r.reputation; c.completedDays++;
            return ValidCheckpoint(s, c);
        }
        private static bool SameCheckpoint(FirmCheckpoint a, FirmCheckpoint b)
        {
            if (a.completedDays != b.completedDays || a.balance != b.balance || a.reputation != b.reputation) return false;
            for (int i = 0; i < 6; i++) if (a.clients[i].id != b.clients[i].id || a.clients[i].deals != b.clients[i].deals
                || a.clients[i].lastSeenWorkDay != b.clients[i].lastSeenWorkDay || a.clients[i].relationship != b.clients[i].relationship
                || a.clients[i].awaitingReport != b.clients[i].awaitingReport || a.clients[i].complaintPending != b.clients[i].complaintPending) return false;
            return true;
        }
        private static FirmCheckpoint CopyCheckpoint(FirmCheckpoint c)
        {
            var copy = new FirmCheckpoint { completedDays = c.completedDays, balance = c.balance, reputation = c.reputation, clients = new WorkClientState[6] };
            for (int i = 0; i < 6; i++) copy.clients[i] = WorkCareerState.CopyClient(c.clients[i]); return copy;
        }
        private static FirmDayReport CopyReport(FirmDayReport r) => new FirmDayReport { day = r.day, openingBalance = r.openingBalance,
            revenue = r.revenue, rent = r.rent, salary = r.salary, balance = r.balance, reputation = r.reputation, clientIds = (string[])r.clientIds.Clone(),
            sold = (bool[])r.sold.Clone(), closed = (bool[])r.closed.Clone(), reportReviewed = (bool[])r.reportReviewed.Clone(),
            guaranteed = (bool[])r.guaranteed.Clone(), complaintResolved = (bool[])r.complaintResolved.Clone() };
        private static FirmCareerSave Copy(FirmCareerSave s)
        {
            var copy = new FirmCareerSave { schemaVersion = s.schemaVersion, mode = s.mode, sourceDay = s.sourceDay,
                sourceSavings = s.sourceSavings, personalReserve = s.personalReserve, assistantHireDay = s.assistantHireDay, origin = CopyCheckpoint(s.origin),
                checkpoint = CopyCheckpoint(s.checkpoint), reports = new FirmDayReport[s.reports.Length] };
            for (int i = 0; i < s.reports.Length; i++) copy.reports[i] = CopyReport(s.reports[i]); return copy;
        }
    }
}
