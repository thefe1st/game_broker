using System;

namespace Broker.Core
{
    [Serializable] public sealed class WorkClientState
    {
        public string id;
        public int relationship, deals, lastSeenWorkDay;
        public bool awaitingReport, complaintPending;
    }
    [Serializable] public sealed class WorkCheckpoint
    {
        public int completedWorkDays, startingSavings, savings, reputation;
        public WorkClientState[] clients;
    }
    [Serializable] public sealed class WorkDayReport
    {
        public int day, openingSavings, firmRevenue, personalEarned, savings, deals, reputation;
        public string[] clientIds;
        public bool[] sold, closed, reportReviewed;
        public bool[] guaranteed, complaintResolved;
    }
    [Serializable] public sealed class WorkCareerSave
    {
        public int schemaVersion;
        public string mode;
        public WorkCheckpoint origin, checkpoint;
        public WorkDayReport[] reports;
    }

    // Checkpoint + a bounded seven-day ledger. No final day or duplicate settlement.
    // All state after the checkpoint is reconstructed, never trusted from parallel summary fields.
    public sealed class WorkCareerState
    {
        public const int HistoryDays = 7;
        private WorkCareerSave save;
        private WorkCheckpoint current;
        public int CompletedDays => current.completedWorkDays;
        public int NextDayNumber => checked(CompletedDays + 4);
        public int PersonalSavings => current.savings;
        public int Reputation => current.reputation;
        public int TotalDeals { get { int n = 0; foreach (var c in current.clients) n += c.deals; return n; } }
        public WorkDayReport LastReport => save.reports.Length == 0 ? null : CopyReport(save.reports[save.reports.Length - 1]);
        public WorkClientState[] Clients => CopyCheckpoint(current).clients;
        public WorkDayReport[] RecentReports
        {
            get { var reports = new WorkDayReport[save.reports.Length];
                for (int i = 0; i < reports.Length; i++) reports[i] = CopyReport(save.reports[i]); return reports; }
        }
        private WorkCareerState(WorkCareerSave data, WorkCheckpoint result) { save = Copy(data); current = CopyCheckpoint(result); }

        public static WorkCareerState FromIntro(EmploymentState intro)
        {
            if (intro == null || !intro.IsIntroComplete) throw new ArgumentException("Complete the intro before starting work days.");
            var seed = intro.Capture(); string[] ids = ClientCatalog.Ids;
            var checkpoint = new WorkCheckpoint { startingSavings = seed.personalSavings, savings = seed.personalSavings,
                reputation = seed.reputation, clients = new WorkClientState[ids.Length] };
            for (int i = 0; i < ids.Length; i++) checkpoint.clients[i] = new WorkClientState { id = ids[i],
                relationship = i < 3 ? seed.relationships[i] : 0, awaitingReport = i < 3 && seed.lastSold[i] };
            var data = new WorkCareerSave { schemaVersion = 2, mode = "employee-work", origin = CopyCheckpoint(checkpoint), checkpoint = checkpoint, reports = new WorkDayReport[0] };
            if (!TryRestore(data, out var state)) throw new InvalidOperationException("Invalid intro seed.");
            return state;
        }
        public WorkCareerState RestartFromOrigin()
        {
            var data = new WorkCareerSave { schemaVersion = 2, mode = "employee-work", origin = CopyCheckpoint(save.origin),
                checkpoint = CopyCheckpoint(save.origin), reports = new WorkDayReport[0] };
            if (!TryRestore(data, out var state)) throw new InvalidOperationException("Invalid career origin.");
            return state;
        }
        public BrokerDay CreateDay()
        {
            var ids = ClientCatalog.Appointments(NextDayNumber); var bonuses = new int[3]; var returns = new bool[3]; var complaints = new bool[3];
            for (int i = 0; i < 3; i++) { var c = current.clients[ClientCatalog.Index(ids[i])]; bonuses[i] = c.relationship; returns[i] = c.awaitingReport; complaints[i] = c.complaintPending; }
            return new BrokerDay(NextDayNumber, PersonalSavings, Reputation, relationshipBonuses: bonuses,
                returning: returns, mode: CareerMode.EmployeeWork, clientIds: ids, complaints: complaints);
        }
        public bool CompleteDay(BrokerDay day)
        {
            if (day == null || day.Mode != CareerMode.EmployeeWork || !day.DayEnded || day.DayNumber != NextDayNumber
                || day.OpeningCash != PersonalSavings || day.CarryDebt != 0 || day.Spent != 0 || day.TotalExpenses != 0) return false;
            var r = new WorkDayReport { day = day.DayNumber, openingSavings = day.OpeningCash, firmRevenue = day.FirmRevenue,
                personalEarned = day.Earned, savings = day.Cash, deals = day.DealsCount, reputation = day.Reputation,
                clientIds = new string[3], sold = new bool[3], closed = new bool[3], reportReviewed = new bool[3], guaranteed = new bool[3], complaintResolved = new bool[3] };
            for (int i = 0; i < 3; i++) { var c = day.Clients[i]; r.clientIds[i] = c.Id; r.sold[i] = c.DealClosed;
                r.closed[i] = c.Call == CallStatus.Closed; r.reportReviewed[i] = c.ReportReviewed; r.guaranteed[i] = c.FalseGuaranteeMade; r.complaintResolved[i] = c.ComplaintResolved; }
            var candidate = Capture(); int length = candidate.reports.Length;
            if (length == HistoryDays)
            {
                if (!Apply(candidate.checkpoint, candidate.reports[0])) return false;
                Array.Copy(candidate.reports, 1, candidate.reports, 0, length - 1); candidate.reports[length - 1] = r;
            }
            else { Array.Resize(ref candidate.reports, length + 1); candidate.reports[length] = r; }
            if (!TryRestore(candidate, out var validated)) return false;
            save = validated.save; current = validated.current; return true;
        }
        public WorkCareerSave Capture() => Copy(save);
        public static bool TryRestore(WorkCareerSave data, out WorkCareerState state)
        {
            state = null;
            try
            {
                if (data != null && data.schemaVersion == 1)
                {
                    if (!LegacyComplaintFieldsEmpty(data)) return false;
                    data = Copy(data); data.schemaVersion = 2;
                }
                if (data == null || data.schemaVersion != 2 || data.mode != "employee-work" || !ValidCheckpoint(data.checkpoint)
                    || !ValidCheckpoint(data.origin) || data.origin.completedWorkDays != 0
                    || data.origin.startingSavings != data.checkpoint.startingSavings
                    || (data.checkpoint.completedWorkDays == 0 && !SameCheckpoint(data.origin, data.checkpoint))
                    || data.reports == null || data.reports.Length > HistoryDays
                    || (data.checkpoint.completedWorkDays > 0 && data.reports.Length != HistoryDays)) return false;
                foreach (var client in data.origin.clients) if (client.complaintPending) return false;
                var result = CopyCheckpoint(data.checkpoint);
                foreach (var r in data.reports) if (!Apply(result, r)) return false;
                state = new WorkCareerState(data, result); return true;
            }
            catch (OverflowException) { return false; }
        }
        private static bool LegacyComplaintFieldsEmpty(WorkCareerSave s)
        {
            if (!ValidCheckpoint(s.origin) || !ValidCheckpoint(s.checkpoint) || s.reports == null || s.reports.Length > HistoryDays) return false;
            foreach (var c in s.origin.clients) if (c.complaintPending) return false;
            foreach (var c in s.checkpoint.clients) if (c.complaintPending) return false;
            foreach (var r in s.reports)
            {
                if (r == null || r.clientIds == null || r.sold == null || r.closed == null || r.reportReviewed == null) return false;
                foreach (var flags in new[] { r.guaranteed, r.complaintResolved })
                {
                    if (flags == null) continue;
                    if (flags.Length != 3) return false;
                    foreach (bool flag in flags) if (flag) return false;
                }
            }
            return true;
        }
        private static bool SameCheckpoint(WorkCheckpoint a, WorkCheckpoint b)
        {
            if (a.savings != b.savings || a.reputation != b.reputation) return false;
            for (int i = 0; i < 6; i++) if (a.clients[i].relationship != b.clients[i].relationship
                || a.clients[i].awaitingReport != b.clients[i].awaitingReport
                || a.clients[i].complaintPending != b.clients[i].complaintPending) return false;
            return true;
        }
        private static bool ValidCheckpoint(WorkCheckpoint c)
        {
            if (c == null || c.completedWorkDays < 0 || c.completedWorkDays > int.MaxValue - 5
                || c.startingSavings < 0 || c.startingSavings > 330 || c.savings < c.startingSavings || c.savings > int.MaxValue - 1000
                || c.reputation < 0 || c.reputation > 100 || c.clients == null || c.clients.Length != 6) return false;
            string[] ids = ClientCatalog.Ids; long earned = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                var client = c.clients[i];
                if (client == null || client.id != ids[i] || client.relationship < -1 || client.relationship > 2
                    || client.deals < 0 || client.deals > c.completedWorkDays || client.lastSeenWorkDay < 0
                    || client.lastSeenWorkDay > c.completedWorkDays || (client.deals > 0 && client.lastSeenWorkDay == 0)) return false;
                earned += (long)client.deals * BrokerDay.PersonalPayout(ClientCatalog.Create(client.id).Commission);
            }
            return (long)c.startingSavings + earned == c.savings;
        }
        private static bool Apply(WorkCheckpoint c, WorkDayReport r)
        {
            if (r == null || r.day != c.completedWorkDays + 4 || r.openingSavings != c.savings
                || r.clientIds == null || r.clientIds.Length != 3 || r.sold == null || r.sold.Length != 3
                || r.closed == null || r.closed.Length != 3 || r.reportReviewed == null || r.reportReviewed.Length != 3
                || r.guaranteed == null || r.guaranteed.Length != 3 || r.complaintResolved == null || r.complaintResolved.Length != 3
                || r.reputation < 0 || r.reputation > 100) return false;
            string[] ids = ClientCatalog.Appointments(r.day); int earned = 0, gross = 0, deals = 0;
            for (int i = 0; i < 3; i++)
            {
                if (r.clientIds[i] != ids[i] || (r.sold[i] && (!r.closed[i] || !r.reportReviewed[i]))) return false;
                var client = c.clients[ClientCatalog.Index(ids[i])];
                if (!client.awaitingReport && !r.reportReviewed[i]) return false;
                if (r.complaintResolved[i] && (!client.complaintPending || !r.closed[i] || r.guaranteed[i])) return false;
                if (r.guaranteed[i] && !r.closed[i]) return false;
                if (r.sold[i] && client.complaintPending && !r.complaintResolved[i]) return false;
                if (r.sold[i]) { int commission = ClientCatalog.Create(ids[i]).Commission;
                    deals++; gross += commission; earned += BrokerDay.PersonalPayout(commission); client.deals = checked(client.deals + 1); }
                int change = r.sold[i] ? 1 : r.complaintResolved[i] ? 0 : r.closed[i] ? -1 : 0;
                change += (r.complaintResolved[i] ? 1 : 0) - (r.guaranteed[i] ? 1 : 0);
                client.relationship = Math.Max(-1, Math.Min(2, client.relationship + change));
                client.complaintPending = r.guaranteed[i] || (client.complaintPending && !r.complaintResolved[i]);
                client.awaitingReport = r.sold[i] || (client.awaitingReport && !r.reportReviewed[i]);
                client.lastSeenWorkDay = checked(c.completedWorkDays + 1);
            }
            if (r.deals != deals || r.firmRevenue != gross || r.personalEarned != earned || r.savings != (long)c.savings + earned) return false;
            c.savings = r.savings; c.reputation = r.reputation; c.completedWorkDays++;
            return ValidCheckpoint(c);
        }
        private static WorkClientState CopyClient(WorkClientState c) => new WorkClientState {
            id = c.id, relationship = c.relationship, deals = c.deals, lastSeenWorkDay = c.lastSeenWorkDay, awaitingReport = c.awaitingReport, complaintPending = c.complaintPending };
        private static WorkCheckpoint CopyCheckpoint(WorkCheckpoint c)
        {
            var result = new WorkCheckpoint { completedWorkDays = c.completedWorkDays, startingSavings = c.startingSavings,
                savings = c.savings, reputation = c.reputation, clients = new WorkClientState[c.clients.Length] };
            for (int i = 0; i < c.clients.Length; i++) result.clients[i] = CopyClient(c.clients[i]); return result;
        }
        private static WorkDayReport CopyReport(WorkDayReport r) => new WorkDayReport { day = r.day, openingSavings = r.openingSavings,
            firmRevenue = r.firmRevenue, personalEarned = r.personalEarned, savings = r.savings, deals = r.deals, reputation = r.reputation,
            clientIds = (string[])r.clientIds.Clone(), sold = (bool[])r.sold.Clone(), closed = (bool[])r.closed.Clone(), reportReviewed = (bool[])r.reportReviewed.Clone(),
            guaranteed = r.guaranteed == null ? new bool[3] : (bool[])r.guaranteed.Clone(),
            complaintResolved = r.complaintResolved == null ? new bool[3] : (bool[])r.complaintResolved.Clone() };
        private static WorkCareerSave Copy(WorkCareerSave s)
        {
            var copy = new WorkCareerSave { schemaVersion = s.schemaVersion, mode = s.mode, origin = CopyCheckpoint(s.origin), checkpoint = CopyCheckpoint(s.checkpoint), reports = new WorkDayReport[s.reports.Length] };
            for (int i = 0; i < s.reports.Length; i++) copy.reports[i] = CopyReport(s.reports[i]); return copy;
        }
    }
}
