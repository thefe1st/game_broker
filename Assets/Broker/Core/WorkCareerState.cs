using System;

namespace Broker.Core
{
    [Serializable] public sealed class WorkClientState
    {
        public string id;
        public int relationship, deals, lastSeenWorkDay;
        public bool awaitingReport, complaintPending;
        // v3: latest purchase with a recorded stock, absolute game day. Empty/0: none or sold before stock history.
        public string lastStockId = "";
        public int lastStockDay;
        // v4: quote of that purchase in cents; 0 when it was made before quotes (marketFromDay).
        public int lastStockPrice;
        // v4: how many of the deals paid 3% / 5%; the rest paid 2% (moderate stocks or days before quotes).
        // Keeps folded days exact: income is rebuilt from these counts, not from a parallel total.
        public int dealsAt3, dealsAt5;
        // v5: up to three latest purchases with a quote, oldest first; the last one is lastStock*. Shares are derived.
        public StockPosition[] portfolio = new StockPosition[0];
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
        // v3: sold stock ID per slot; empty without a sale or for days before stockHistoryFromDay.
        public string[] soldStockIds;
        // v4: purchase quote per slot in cents; 0 without a sale or for days before marketFromDay.
        public int[] soldPrices;
        // v5: price concern settled honestly in this call; possible only after a morning drop of the portfolio by 5% or more.
        public bool[] concernResolved;
    }
    [Serializable] public sealed class WorkCareerSave
    {
        public int schemaVersion;
        public string mode;
        // v3: first day whose sold stocks are recorded; imported v1/v2 days stay "before stock history".
        public int stockHistoryFromDay;
        // v4: first day with daily quotes and per-risk fees; imported v1-v3 days keep the fixed 2%.
        public int marketFromDay;
        // v5: first day with client portfolio reactions to prices; imported v1-v4 days had none.
        public int portfolioFromDay;
        public WorkCheckpoint origin, checkpoint;
        public WorkDayReport[] reports;
    }

    // Checkpoint + a bounded seven-day ledger. No final day or duplicate settlement.
    // All state after the checkpoint is reconstructed, never trusted from parallel summary fields.
    public sealed class WorkCareerState
    {
        public const int HistoryDays = 7, SchemaVersion = 5, FirstWorkDay = 4;
        // Legacy checks: no day has stock records, so any stored stock is invalid.
        private const int NoStockHistory = int.MaxValue;
        private WorkCareerSave save;
        private WorkCheckpoint current;
        public int CompletedDays => current.completedWorkDays;
        public int NextDayNumber => checked(CompletedDays + 4);
        public int PersonalSavings => current.savings;
        public int Reputation => current.reputation;
        public int TotalDeals { get { int n = 0; foreach (var c in current.clients) n += c.deals; return n; } }
        public int StockHistoryFromDay => save.stockHistoryFromDay;
        public int MarketFromDay => save.marketFromDay;
        public int PortfolioFromDay => save.portfolioFromDay;
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
            var data = new WorkCareerSave { schemaVersion = SchemaVersion, mode = "employee-work", stockHistoryFromDay = FirstWorkDay, marketFromDay = FirstWorkDay,
                portfolioFromDay = FirstWorkDay, origin = CopyCheckpoint(checkpoint), checkpoint = checkpoint, reports = new WorkDayReport[0] };
            if (!TryRestore(data, out var state)) throw new InvalidOperationException("Invalid intro seed.");
            return state;
        }
        public WorkCareerState RestartFromOrigin()
        {
            var data = new WorkCareerSave { schemaVersion = SchemaVersion, mode = "employee-work", stockHistoryFromDay = FirstWorkDay, marketFromDay = FirstWorkDay,
                portfolioFromDay = FirstWorkDay, origin = CopyCheckpoint(save.origin), checkpoint = CopyCheckpoint(save.origin), reports = new WorkDayReport[0] };
            if (!TryRestore(data, out var state)) throw new InvalidOperationException("Invalid career origin.");
            return state;
        }
        public BrokerDay CreateDay()
        {
            var ids = ClientCatalog.Appointments(NextDayNumber); var bonuses = new int[3]; var returns = new bool[3]; var complaints = new bool[3];
            for (int i = 0; i < 3; i++) { var c = current.clients[ClientCatalog.Index(ids[i])]; bonuses[i] = c.relationship; returns[i] = c.awaitingReport; complaints[i] = c.complaintPending; }
            var day = new BrokerDay(NextDayNumber, PersonalSavings, Reputation, relationshipBonuses: bonuses,
                returning: returns, mode: CareerMode.EmployeeWork, clientIds: ids, complaints: complaints);
            for (int i = 0; i < 3; i++) { var c = current.clients[ClientCatalog.Index(ids[i])]; day.AttachLastPurchase(i, c.lastStockId, c.lastStockDay, c.lastStockPrice); day.AttachPortfolio(i, c.portfolio); }
            return day;
        }
        public bool CompleteDay(BrokerDay day)
        {
            if (day == null || day.Mode != CareerMode.EmployeeWork || !day.DayEnded || day.DayNumber != NextDayNumber
                || day.OpeningCash != PersonalSavings || day.CarryDebt != 0 || day.Spent != 0 || day.TotalExpenses != 0) return false;
            var r = new WorkDayReport { day = day.DayNumber, openingSavings = day.OpeningCash, firmRevenue = day.FirmRevenue,
                personalEarned = day.Earned, savings = day.Cash, deals = day.DealsCount, reputation = day.Reputation,
                clientIds = new string[3], sold = new bool[3], closed = new bool[3], reportReviewed = new bool[3], guaranteed = new bool[3], complaintResolved = new bool[3],
                soldStockIds = new string[3], soldPrices = new int[3], concernResolved = new bool[3] };
            for (int i = 0; i < 3; i++) { var c = day.Clients[i]; r.clientIds[i] = c.Id; r.sold[i] = c.DealClosed;
                r.closed[i] = c.Call == CallStatus.Closed; r.reportReviewed[i] = c.ReportReviewed; r.guaranteed[i] = c.FalseGuaranteeMade; r.complaintResolved[i] = c.ComplaintResolved;
                r.soldStockIds[i] = c.DealClosed ? c.StockId : ""; r.soldPrices[i] = c.DealClosed ? c.SoldPriceCents : 0; r.concernResolved[i] = c.PriceConcernResolved; }
            var candidate = Capture(); int length = candidate.reports.Length;
            if (length == HistoryDays)
            {
                if (!Apply(candidate.checkpoint, candidate.reports[0], candidate)) return false;
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
                // v2 has no stock records: import keeps old deals "before stock history", never guesses tickers.
                if (data != null && data.schemaVersion == 2)
                {
                    if (!LegacyStockFieldsEmpty(data)) return false;
                    data = UpgradeToV3(data);
                }
                // v3 has no quotes: imported days keep the fixed 2% without prices; the market starts on the next day.
                if (data != null && data.schemaVersion == 3)
                {
                    if (!LegacyMarketFieldsEmpty(data)) return false;
                    data = UpgradeToV4(data);
                }
                // v4 has no portfolios: the last quoted purchase becomes the only position of the folded checkpoint; the ledger days
                // are replayed and add their purchases. Reactions to prices start on the next day; earlier purchases are not guessed.
                if (data != null && data.schemaVersion == 4)
                {
                    if (!LegacyPortfolioFieldsEmpty(data)) return false;
                    data = UpgradeToCurrent(data);
                }
                if (data == null || data.schemaVersion != SchemaVersion || data.mode != "employee-work" || data.marketFromDay < data.stockHistoryFromDay
                    || data.portfolioFromDay < data.marketFromDay
                    || !ValidCheckpoint(data.checkpoint, data.stockHistoryFromDay, data.marketFromDay)
                    || !ValidCheckpoint(data.origin, data.stockHistoryFromDay, data.marketFromDay) || data.origin.completedWorkDays != 0
                    || data.origin.startingSavings != data.checkpoint.startingSavings
                    || (data.checkpoint.completedWorkDays == 0 && !SameCheckpoint(data.origin, data.checkpoint))
                    || data.reports == null || data.reports.Length > HistoryDays
                    || (data.checkpoint.completedWorkDays > 0 && data.reports.Length != HistoryDays)) return false;
                foreach (var client in data.origin.clients) if (client.complaintPending) return false;
                var result = CopyCheckpoint(data.checkpoint);
                foreach (var r in data.reports) if (!Apply(result, r, data)) return false;
                if (data.stockHistoryFromDay < FirstWorkDay || data.portfolioFromDay > (long)result.completedWorkDays + FirstWorkDay) return false;
                state = new WorkCareerState(data, result); return true;
            }
            catch (OverflowException) { return false; }
            catch (ArgumentException) { return false; }
        }
        private static bool LegacyComplaintFieldsEmpty(WorkCareerSave s)
        {
            if (!ValidCheckpoint(s.origin, NoStockHistory, NoStockHistory) || !ValidCheckpoint(s.checkpoint, NoStockHistory, NoStockHistory)
                || s.reports == null || s.reports.Length > HistoryDays) return false;
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
        // Structural checks happen before copying: Copy must not hide missing v2 arrays as defaults.
        private static bool LegacyStockFieldsEmpty(WorkCareerSave s)
        {
            if (s.stockHistoryFromDay != 0 || s.origin?.clients == null || s.checkpoint?.clients == null || s.reports == null) return false;
            foreach (var clients in new[] { s.origin.clients, s.checkpoint.clients })
                foreach (var c in clients) if (c == null || !string.IsNullOrEmpty(c.lastStockId) || c.lastStockDay != 0) return false;
            foreach (var r in s.reports)
            {
                if (r == null || r.clientIds == null || r.sold == null || r.closed == null || r.reportReviewed == null
                    || r.guaranteed == null || r.complaintResolved == null) return false;
                if (r.soldStockIds != null) foreach (string id in r.soldStockIds) if (!string.IsNullOrEmpty(id)) return false;
            }
            return true;
        }
        private static WorkCareerSave UpgradeToV3(WorkCareerSave legacy)
        {
            var s = Copy(legacy); s.schemaVersion = 3;
            s.stockHistoryFromDay = checked(s.checkpoint.completedWorkDays + s.reports.Length + FirstWorkDay);
            foreach (var r in s.reports) r.soldStockIds = new[] { "", "", "" };
            return s;
        }
        // Structural checks before copying, as for v2: Copy must not hide missing v3 arrays as defaults.
        private static bool LegacyMarketFieldsEmpty(WorkCareerSave s)
        {
            if (s.marketFromDay != 0 || s.origin?.clients == null || s.checkpoint?.clients == null || s.reports == null) return false;
            foreach (var clients in new[] { s.origin.clients, s.checkpoint.clients })
                foreach (var c in clients) if (c == null || c.lastStockPrice != 0 || c.dealsAt3 != 0 || c.dealsAt5 != 0) return false;
            foreach (var r in s.reports)
            {
                if (r == null || r.clientIds == null || r.sold == null || r.closed == null || r.reportReviewed == null
                    || r.guaranteed == null || r.complaintResolved == null || r.soldStockIds == null) return false;
                if (r.soldPrices != null) foreach (int price in r.soldPrices) if (price != 0) return false;
            }
            return true;
        }
        private static WorkCareerSave UpgradeToV4(WorkCareerSave legacy)
        {
            var s = Copy(legacy); s.schemaVersion = 4;
            s.marketFromDay = checked(s.checkpoint.completedWorkDays + s.reports.Length + FirstWorkDay);
            foreach (var r in s.reports) r.soldPrices = new int[3];
            return s;
        }
        // Structural checks before copying, as for v3: Copy must not hide missing v4 arrays as defaults.
        private static bool LegacyPortfolioFieldsEmpty(WorkCareerSave s)
        {
            if (s.portfolioFromDay != 0 || s.origin?.clients == null || s.checkpoint?.clients == null || s.reports == null) return false;
            foreach (var clients in new[] { s.origin.clients, s.checkpoint.clients })
                foreach (var c in clients) if (c == null || (c.portfolio != null && c.portfolio.Length != 0)) return false;
            foreach (var r in s.reports)
            {
                if (r == null || r.clientIds == null || r.sold == null || r.closed == null || r.reportReviewed == null
                    || r.guaranteed == null || r.complaintResolved == null || r.soldStockIds == null || r.soldPrices == null) return false;
                if (r.concernResolved != null) foreach (bool flag in r.concernResolved) if (flag) return false;
            }
            return true;
        }
        private static WorkCareerSave UpgradeToCurrent(WorkCareerSave legacy)
        {
            var s = Copy(legacy); s.schemaVersion = SchemaVersion;
            s.portfolioFromDay = checked(s.checkpoint.completedWorkDays + s.reports.Length + FirstWorkDay);
            foreach (var checkpoint in new[] { s.origin, s.checkpoint }) foreach (var c in checkpoint.clients) c.portfolio = StockPortfolio.FromLastPurchase(c);
            foreach (var r in s.reports) r.concernResolved = new bool[3];
            return s;
        }
        // Shared v3 rule for work and founder: a recorded purchase is a canonical appointment of a suitable stock.
        internal static bool ValidStockRecord(string clientId, string stockId, long day)
            => day >= FirstWorkDay && day <= int.MaxValue && Array.IndexOf(ClientCatalog.Appointments((int)day), clientId) >= 0
                && StockCatalog.Fits(clientId, StockCatalog.Find(stockId));
        private static bool SameCheckpoint(WorkCheckpoint a, WorkCheckpoint b)
        {
            if (a.savings != b.savings || a.reputation != b.reputation) return false;
            for (int i = 0; i < 6; i++) if (a.clients[i].relationship != b.clients[i].relationship
                || a.clients[i].awaitingReport != b.clients[i].awaitingReport
                || a.clients[i].complaintPending != b.clients[i].complaintPending
                || a.clients[i].lastStockDay != b.clients[i].lastStockDay || (a.clients[i].lastStockId ?? "") != (b.clients[i].lastStockId ?? "")
                || a.clients[i].lastStockPrice != b.clients[i].lastStockPrice
                || a.clients[i].dealsAt3 != b.clients[i].dealsAt3 || a.clients[i].dealsAt5 != b.clients[i].dealsAt5
                || !StockPortfolio.Same(a.clients[i].portfolio, b.clients[i].portfolio)) return false;
            return true;
        }
        private static bool ValidCheckpoint(WorkCheckpoint c, int historyFrom, int marketFrom)
        {
            if (c == null || c.completedWorkDays < 0 || c.completedWorkDays > int.MaxValue - 5
                || c.startingSavings < 0 || c.startingSavings > 330 || c.savings < c.startingSavings || c.savings > int.MaxValue - 1000
                || c.reputation < 0 || c.reputation > 100 || c.clients == null || c.clients.Length != 6) return false;
            string[] ids = ClientCatalog.Ids; long earned = 0;
            // Deals at 3%/5% are possible only on folded days from marketFrom, at most one per client and day.
            long marketDays = Math.Max(0, (long)c.completedWorkDays + FirstWorkDay - marketFrom);
            for (int i = 0; i < ids.Length; i++)
            {
                var client = c.clients[i];
                if (client == null || client.id != ids[i] || client.relationship < -1 || client.relationship > 2
                    || client.deals < 0 || client.deals > c.completedWorkDays || client.lastSeenWorkDay < 0
                    || client.lastSeenWorkDay > c.completedWorkDays || (client.deals > 0 && client.lastSeenWorkDay == 0)) return false;
                // With history from day 4 every purchase is recorded, so a buyer must have a last stock.
                if (string.IsNullOrEmpty(client.lastStockId) ? client.lastStockDay != 0 || client.lastStockPrice != 0 || (client.deals > 0 && historyFrom == FirstWorkDay)
                    : client.deals == 0 || client.lastStockDay < historyFrom || client.lastStockDay > (long)c.completedWorkDays + 3
                    || client.lastStockDay - 3L > client.lastSeenWorkDay || !ValidStockRecord(client.id, client.lastStockId, client.lastStockDay)
                    || client.lastStockPrice != StockMarket.RecordedPrice(client.lastStockId, client.lastStockDay, marketFrom)) return false;
                if (!ValidRateCounts(client, marketDays) || !StockPortfolio.Valid(client, FirstWorkDay, marketFrom)) return false;
                earned += Income(client, true);
            }
            return (long)c.startingSavings + earned == c.savings;
        }
        internal static bool ValidRateCounts(WorkClientState c, long marketDays)
            => c.dealsAt3 >= 0 && c.dealsAt5 >= 0 && (long)c.dealsAt3 + c.dealsAt5 <= Math.Min(c.deals, marketDays)
                && (c.dealsAt3 == 0 || StockMarket.CanPay(c.id, 3)) && (c.dealsAt5 == 0 || StockMarket.CanPay(c.id, 5));
        // Income of all settled deals of a client: personal 20% payouts (employee) or firm commission.
        internal static long Income(WorkClientState c, bool personal)
            => ((long)c.deals - c.dealsAt3 - c.dealsAt5) * Pay(c.id, StockCatalog.FeePercent, personal)
                + (long)c.dealsAt3 * Pay(c.id, 3, personal) + (long)c.dealsAt5 * Pay(c.id, 5, personal);
        private static int Pay(string clientId, int feePercent, bool personal)
        { int commission = StockMarket.CommissionAt(clientId, feePercent); return personal ? BrokerDay.PersonalPayout(commission) : commission; }
        internal static void CountRate(WorkClientState c, int feePercent)
        { if (feePercent == 3) c.dealsAt3 = checked(c.dealsAt3 + 1); else if (feePercent == 5) c.dealsAt5 = checked(c.dealsAt5 + 1); }
        private static bool Apply(WorkCheckpoint c, WorkDayReport r, WorkCareerSave s)
        {
            int historyFrom = s.stockHistoryFromDay, marketFrom = s.marketFromDay;
            if (r == null || r.day != c.completedWorkDays + 4 || r.openingSavings != c.savings
                || r.soldStockIds == null || r.soldStockIds.Length != 3 || r.soldPrices == null || r.soldPrices.Length != 3
                || r.concernResolved == null || r.concernResolved.Length != 3
                || r.clientIds == null || r.clientIds.Length != 3 || r.sold == null || r.sold.Length != 3
                || r.closed == null || r.closed.Length != 3 || r.reportReviewed == null || r.reportReviewed.Length != 3
                || r.guaranteed == null || r.guaranteed.Length != 3 || r.complaintResolved == null || r.complaintResolved.Length != 3
                || r.reputation < 0 || r.reputation > 100) return false;
            string[] ids = ClientCatalog.Appointments(r.day); int earned = 0, gross = 0, deals = 0;
            for (int i = 0; i < 3; i++)
            {
                if (r.clientIds[i] != ids[i] || (r.sold[i] && (!r.closed[i] || !r.reportReviewed[i]))) return false;
                bool hasStock = !string.IsNullOrEmpty(r.soldStockIds[i]);
                if (hasStock ? !r.sold[i] || r.day < historyFrom || !ValidStockRecord(ids[i], r.soldStockIds[i], r.day)
                    : r.sold[i] && r.day >= historyFrom) return false;
                int price = hasStock ? StockMarket.RecordedPrice(r.soldStockIds[i], r.day, marketFrom) : 0;
                if (r.soldPrices[i] != price) return false;
                var client = c.clients[ClientCatalog.Index(ids[i])];
                if (!client.awaitingReport && !r.reportReviewed[i]) return false;
                if (r.complaintResolved[i] && (!client.complaintPending || !r.closed[i] || r.guaranteed[i])) return false;
                if (r.guaranteed[i] && !r.closed[i]) return false;
                if (r.sold[i] && client.complaintPending && !r.complaintResolved[i]) return false;
                // The morning mood comes from positions bought before this day, exactly as BrokerDay saw them.
                var mood = r.day >= s.portfolioFromDay ? StockPortfolio.Mood(ids[i], client.portfolio, r.day) : PortfolioMood.None;
                if (!StockPortfolio.ValidReaction(mood, r.sold[i], r.closed[i], r.guaranteed[i], r.concernResolved[i], r.soldStockIds[i])) return false;
                if (r.sold[i]) { int rate = StockMarket.SaleRate(hasStock ? r.soldStockIds[i] : "", r.day, marketFrom), commission = StockMarket.CommissionAt(ids[i], rate);
                    deals++; gross += commission; earned += BrokerDay.PersonalPayout(commission); client.deals = checked(client.deals + 1); CountRate(client, rate);
                    client.lastStockId = hasStock ? r.soldStockIds[i] : ""; client.lastStockDay = hasStock ? r.day : 0; client.lastStockPrice = price;
                    client.portfolio = price > 0 ? StockPortfolio.Add(client.portfolio, r.soldStockIds[i], r.day, price) : new StockPosition[0]; }
                int change = StockPortfolio.RelationshipChange(r.sold[i], r.closed[i], r.guaranteed[i], r.complaintResolved[i], StockPortfolio.IsConcern(mood), r.concernResolved[i]);
                client.relationship = Math.Max(-1, Math.Min(2, client.relationship + change));
                client.complaintPending = r.guaranteed[i] || (client.complaintPending && !r.complaintResolved[i]);
                client.awaitingReport = r.sold[i] || (client.awaitingReport && !r.reportReviewed[i]);
                client.lastSeenWorkDay = checked(c.completedWorkDays + 1);
            }
            if (r.deals != deals || r.firmRevenue != gross || r.personalEarned != earned || r.savings != (long)c.savings + earned) return false;
            c.savings = r.savings; c.reputation = r.reputation; c.completedWorkDays++;
            return ValidCheckpoint(c, historyFrom, marketFrom);
        }
        internal static WorkClientState CopyClient(WorkClientState c) => new WorkClientState {
            id = c.id, relationship = c.relationship, deals = c.deals, lastSeenWorkDay = c.lastSeenWorkDay, awaitingReport = c.awaitingReport, complaintPending = c.complaintPending,
            lastStockId = c.lastStockId ?? "", lastStockDay = c.lastStockDay, lastStockPrice = c.lastStockPrice, dealsAt3 = c.dealsAt3, dealsAt5 = c.dealsAt5,
            portfolio = StockPortfolio.Copy(c.portfolio) };
        // Unity writes null strings as "": normalize so a JsonUtility round-trip compares equal.
        internal static string[] CopyStockIds(string[] ids)
        {
            if (ids == null) return new[] { "", "", "" };
            var copy = new string[ids.Length]; for (int i = 0; i < ids.Length; i++) copy[i] = ids[i] ?? ""; return copy;
        }
        internal static int[] CopyPrices(int[] prices) => prices == null ? new int[3] : (int[])prices.Clone();
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
            complaintResolved = r.complaintResolved == null ? new bool[3] : (bool[])r.complaintResolved.Clone(), soldStockIds = CopyStockIds(r.soldStockIds),
            soldPrices = CopyPrices(r.soldPrices), concernResolved = CopyFlags(r.concernResolved) };
        internal static bool[] CopyFlags(bool[] flags) => flags == null ? new bool[3] : (bool[])flags.Clone();
        private static WorkCareerSave Copy(WorkCareerSave s)
        {
            var copy = new WorkCareerSave { schemaVersion = s.schemaVersion, mode = s.mode, stockHistoryFromDay = s.stockHistoryFromDay, marketFromDay = s.marketFromDay,
                portfolioFromDay = s.portfolioFromDay,
                origin = CopyCheckpoint(s.origin), checkpoint = CopyCheckpoint(s.checkpoint), reports = new WorkDayReport[s.reports.Length] };
            for (int i = 0; i < s.reports.Length; i++) copy.reports[i] = CopyReport(s.reports[i]); return copy;
        }
    }
}
