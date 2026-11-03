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
        // v3: sold stock ID per slot; empty without a sale or for imported days before stockHistoryFromDay.
        public string[] soldStockIds;
        // v4: purchase quote per slot in cents; 0 without a sale or for imported days before marketFromDay.
        public int[] soldPrices;
        // v5: price concern settled honestly in this call; possible only after a morning drop of the portfolio by 5% or more.
        public bool[] concernResolved;
    }
    [Serializable] public sealed class FirmCareerSave
    {
        public int schemaVersion;
        public string mode;
        public int sourceDay, sourceSavings, personalReserve, assistantHireDay;
        // v3: first founder day whose sold stocks are recorded. Purchases before sourceDay are carried from employee days.
        public int stockHistoryFromDay;
        // v4: first founder day with daily quotes and per-risk fees; imported v1-v3 days keep the fixed 2%.
        public int marketFromDay;
        // v5: first founder day with client portfolio reactions to prices; imported v1-v4 days had none.
        public int portfolioFromDay;
        public FirmCheckpoint origin, checkpoint;
        public FirmDayReport[] reports;
    }

    // Independent founder ledger. Personal reserve is not an automatic credit line.
    public sealed class FirmCareerState
    {
        public const int RequiredSavings = 600, OpeningCost = 200, InitialCapital = 300, DailyRent = 60, HistoryDays = 7;
        public const int HiringCost = 100, HiringCashRequired = 200, AssistantSalary = 40, SchemaVersion = 5;
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
        public int StockHistoryFromDay => save.stockHistoryFromDay;
        public int MarketFromDay => save.marketFromDay;
        public int PortfolioFromDay => save.portfolioFromDay;
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
            // Last recorded purchases and portfolios stay as "still employed" facts; firm deal counters start from zero.
            foreach (var c in clients) { c.deals = 0; c.lastSeenWorkDay = 0; c.dealsAt3 = 0; c.dealsAt5 = 0; }
            var origin = new FirmCheckpoint { balance = InitialCapital, reputation = work.Reputation, clients = clients };
            var data = new FirmCareerSave { schemaVersion = SchemaVersion, mode = "founder", sourceDay = work.NextDayNumber, stockHistoryFromDay = work.NextDayNumber,
                marketFromDay = work.NextDayNumber, portfolioFromDay = work.NextDayNumber,
                sourceSavings = work.PersonalSavings, personalReserve = work.PersonalSavings - OpeningCost - InitialCapital,
                origin = origin, checkpoint = CopyCheckpoint(origin), reports = new FirmDayReport[0] };
            if (!TryRestore(data, out var state)) throw new InvalidOperationException("Invalid founder seed.");
            return state;
        }
        public BrokerDay CreateDay()
        {
            var ids = ClientCatalog.Appointments(NextDayNumber); var relationships = new int[3]; var returns = new bool[3]; var complaints = new bool[3];
            for (int i = 0; i < 3; i++) { var c = current.clients[ClientCatalog.Index(ids[i])]; relationships[i] = c.relationship; returns[i] = c.awaitingReport; complaints[i] = c.complaintPending; }
            var day = new BrokerDay(NextDayNumber, Math.Max(0, Balance), Reputation, relationshipBonuses: relationships,
                returning: returns, carryDebt: Math.Max(0, -Balance), mode: CareerMode.Founder, clientIds: ids, complaints: complaints, founderAssistant: HasAssistant);
            for (int i = 0; i < 3; i++) { var c = current.clients[ClientCatalog.Index(ids[i])]; day.AttachLastPurchase(i, c.lastStockId, c.lastStockDay, c.lastStockPrice); day.AttachPortfolio(i, c.portfolio); }
            return day;
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
                sold = new bool[3], closed = new bool[3], reportReviewed = new bool[3], guaranteed = new bool[3], complaintResolved = new bool[3],
                soldStockIds = new string[3], soldPrices = new int[3], concernResolved = new bool[3] };
            for (int i = 0; i < 3; i++) { var c = day.Clients[i]; r.clientIds[i] = c.Id; r.sold[i] = c.DealClosed;
                r.closed[i] = c.Call == CallStatus.Closed; r.reportReviewed[i] = c.ReportReviewed; r.guaranteed[i] = c.FalseGuaranteeMade; r.complaintResolved[i] = c.ComplaintResolved;
                r.soldStockIds[i] = c.DealClosed ? c.StockId : ""; r.soldPrices[i] = c.DealClosed ? c.SoldPriceCents : 0; r.concernResolved[i] = c.PriceConcernResolved; }
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
            var data = Capture(); data.assistantHireDay = 0; data.stockHistoryFromDay = data.sourceDay; data.marketFromDay = data.sourceDay; data.portfolioFromDay = data.sourceDay;
            data.checkpoint = CopyCheckpoint(data.origin); data.reports = new FirmDayReport[0];
            if (!TryRestore(data, out var state)) throw new InvalidOperationException("Invalid founder origin.");
            return state;
        }
        public FirmCareerSave Capture() => Copy(save);
        internal bool SameSnapshot(FirmCareerState other)
        {
            if (other == null) return false; var a = save; var b = other.save;
            if (a.schemaVersion != b.schemaVersion || a.mode != b.mode || a.sourceDay != b.sourceDay || a.stockHistoryFromDay != b.stockHistoryFromDay
                || a.marketFromDay != b.marketFromDay || a.portfolioFromDay != b.portfolioFromDay
                || a.sourceSavings != b.sourceSavings || a.personalReserve != b.personalReserve || a.assistantHireDay != b.assistantHireDay
                || !SameCheckpoint(a.origin, b.origin) || !SameCheckpoint(a.checkpoint, b.checkpoint) || a.reports.Length != b.reports.Length) return false;
            for (int n = 0; n < a.reports.Length; n++)
            {
                var x = a.reports[n]; var y = b.reports[n];
                if (x.day != y.day || x.openingBalance != y.openingBalance || x.revenue != y.revenue || x.rent != y.rent
                    || x.salary != y.salary || x.balance != y.balance || x.reputation != y.reputation) return false;
                for (int i = 0; i < 3; i++) if (x.clientIds[i] != y.clientIds[i] || x.sold[i] != y.sold[i] || x.closed[i] != y.closed[i]
                    || x.reportReviewed[i] != y.reportReviewed[i] || x.guaranteed[i] != y.guaranteed[i] || x.complaintResolved[i] != y.complaintResolved[i]
                    || (x.soldStockIds[i] ?? "") != (y.soldStockIds[i] ?? "") || x.soldPrices[i] != y.soldPrices[i]
                    || x.concernResolved[i] != y.concernResolved[i]) return false;
            }
            return true;
        }
        public static bool TryRestore(FirmCareerSave s, out FirmCareerState state)
        {
            state = null;
            try
            {
                // v1 predates the assistant: no hire, so every imported day has zero salary.
                if (s != null && s.schemaVersion == 1 && s.assistantHireDay != 0) return false;
                // v1/v2 have no stock records: imported days stay "before stock history", tickers are never guessed.
                if (s != null && (s.schemaVersion == 1 || s.schemaVersion == 2))
                {
                    if (!LegacyStockFieldsEmpty(s)) return false;
                    s = Copy(s); s.schemaVersion = 3;
                    s.stockHistoryFromDay = checked(s.sourceDay + s.checkpoint.completedDays + s.reports.Length);
                    foreach (var r in s.reports) r.soldStockIds = new[] { "", "", "" };
                }
                // v1-v3 have no quotes: imported days keep the fixed 2% without prices; the market starts on the next day.
                if (s != null && s.schemaVersion == 3)
                {
                    if (!LegacyMarketFieldsEmpty(s)) return false;
                    s = Copy(s); s.schemaVersion = 4;
                    s.marketFromDay = checked(s.sourceDay + s.checkpoint.completedDays + s.reports.Length);
                    foreach (var r in s.reports) r.soldPrices = new int[3];
                }
                // v4 has no portfolios: the last quoted purchase (also one carried from employee days) becomes the only position;
                // the ledger days are replayed and add their purchases. Reactions to prices start on the next day.
                if (s != null && s.schemaVersion == 4)
                {
                    if (!LegacyPortfolioFieldsEmpty(s)) return false;
                    s = Copy(s); s.schemaVersion = SchemaVersion;
                    s.portfolioFromDay = checked(s.sourceDay + s.checkpoint.completedDays + s.reports.Length);
                    foreach (var checkpoint in new[] { s.origin, s.checkpoint }) foreach (var c in checkpoint.clients) c.portfolio = StockPortfolio.FromLastPurchase(c);
                    foreach (var r in s.reports) r.concernResolved = new bool[3];
                }
                if (s == null || s.schemaVersion != SchemaVersion || s.mode != "founder" || s.sourceDay < 4 || s.sourceDay > int.MaxValue - 5
                    || s.sourceSavings < RequiredSavings || s.sourceSavings > int.MaxValue - 1000
                    || s.personalReserve != s.sourceSavings - OpeningCost - InitialCapital
                    || s.assistantHireDay < 0 || (s.assistantHireDay != 0 && s.assistantHireDay < s.sourceDay)
                    || s.stockHistoryFromDay < s.sourceDay || s.marketFromDay < s.stockHistoryFromDay || s.portfolioFromDay < s.marketFromDay
                    || !ValidCheckpoint(s, s.origin) || !ValidCheckpoint(s, s.checkpoint)
                    || s.origin.completedDays != 0 || s.origin.balance != InitialCapital
                    || s.reports == null || s.reports.Length > HistoryDays
                    || (s.checkpoint.completedDays > 0 && s.reports.Length != HistoryDays)) return false;
                if (s.checkpoint.completedDays == 0 && !SameCheckpoint(s.origin, s.checkpoint)) return false;
                var result = CopyCheckpoint(s.checkpoint);
                foreach (var r in s.reports) if (!Apply(s, result, r)) return false;
                long next = (long)s.sourceDay + result.completedDays;
                if (s.assistantHireDay > next || (s.assistantHireDay == next && result.balance < HiringCashRequired) || s.portfolioFromDay > next) return false;
                state = new FirmCareerState(s, result); return true;
            }
            catch (OverflowException) { return false; }
            catch (ArgumentException) { return false; }
        }
        // Structural checks happen before copying: Copy must not hide missing legacy arrays.
        private static bool LegacyStockFieldsEmpty(FirmCareerSave s)
        {
            if (s.stockHistoryFromDay != 0 || s.origin?.clients == null || s.checkpoint?.clients == null || s.reports == null
                || s.origin.clients.Length != 6 || s.checkpoint.clients.Length != 6) return false;
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
        // Structural checks before copying, as for v3: Copy must not hide missing v4 arrays as defaults.
        private static bool LegacyPortfolioFieldsEmpty(FirmCareerSave s)
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
        // Structural checks before copying, as for v2: Copy must not hide missing v3 arrays as defaults.
        private static bool LegacyMarketFieldsEmpty(FirmCareerSave s)
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
        private static bool ValidCheckpoint(FirmCareerSave s, FirmCheckpoint c)
        {
            if (c == null || c.completedDays < 0 || (long)s.sourceDay + c.completedDays > int.MaxValue - 1
                || c.balance < -int.MaxValue + 1000 || c.balance > int.MaxValue - 1000
                || c.reputation < 0 || c.reputation > 100 || c.clients == null || c.clients.Length != 6) return false;
            string[] ids = ClientCatalog.Ids; long revenue = 0;
            // Deals at 3%/5% are possible only on folded days from marketFromDay, at most one per client and day.
            long marketDays = Math.Max(0, (long)s.sourceDay + c.completedDays - s.marketFromDay);
            for (int i = 0; i < 6; i++)
            {
                var client = c.clients[i];
                if (client == null || client.id != ids[i] || client.relationship < -1 || client.relationship > 2
                    || client.deals < 0 || client.deals > c.completedDays || client.lastSeenWorkDay < 0
                    || client.lastSeenWorkDay > c.completedDays || (client.deals > 0 && client.lastSeenWorkDay == 0)) return false;
                // Before sourceDay: carried from employee days, so no firm deal may have replaced it.
                // From sourceDay: a recorded firm purchase on a day this client was last seen or earlier.
                // With history from sourceDay every firm purchase is recorded, so a buyer must have one.
                // A carried employee purchase keeps its own price: 0 before quotes of that career or the day's quote.
                if (string.IsNullOrEmpty(client.lastStockId) ? client.lastStockDay != 0 || client.lastStockPrice != 0 || (client.deals > 0 && s.stockHistoryFromDay == s.sourceDay)
                    : client.lastStockDay > (long)s.sourceDay + c.completedDays - 1
                        || !WorkCareerState.ValidStockRecord(client.id, client.lastStockId, client.lastStockDay)
                        || (client.lastStockDay < s.sourceDay ? client.deals != 0
                                || (client.lastStockPrice != 0 && client.lastStockPrice != StockMarket.PriceCents(client.lastStockId, client.lastStockDay))
                            : client.deals == 0 || client.lastStockDay < s.stockHistoryFromDay
                                || (long)client.lastStockDay - s.sourceDay + 1 > client.lastSeenWorkDay
                                || client.lastStockPrice != StockMarket.RecordedPrice(client.lastStockId, client.lastStockDay, s.marketFromDay))) return false;
                if (!WorkCareerState.ValidRateCounts(client, marketDays) || !StockPortfolio.Valid(client, s.sourceDay, s.marketFromDay)) return false;
                revenue += WorkCareerState.Income(client, false);
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
                || (hiring && c.balance < HiringCashRequired) || r.salary != salary
                || r.soldStockIds == null || r.soldStockIds.Length != 3 || r.soldPrices == null || r.soldPrices.Length != 3
                || r.concernResolved == null || r.concernResolved.Length != 3
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
                bool hasStock = !string.IsNullOrEmpty(r.soldStockIds[i]);
                if (hasStock ? !r.sold[i] || r.day < s.stockHistoryFromDay || !WorkCareerState.ValidStockRecord(ids[i], r.soldStockIds[i], r.day)
                    : r.sold[i] && r.day >= s.stockHistoryFromDay) return false;
                int price = hasStock ? StockMarket.RecordedPrice(r.soldStockIds[i], r.day, s.marketFromDay) : 0;
                if (r.soldPrices[i] != price) return false;
                // Morning mood from positions bought before this day, including ones carried from employee days.
                var mood = r.day >= s.portfolioFromDay ? StockPortfolio.Mood(ids[i], client.portfolio, r.day) : PortfolioMood.None;
                if (!StockPortfolio.ValidReaction(mood, r.sold[i], r.closed[i], r.guaranteed[i], r.concernResolved[i], r.soldStockIds[i])) return false;
                if (r.sold[i]) { int rate = StockMarket.SaleRate(hasStock ? r.soldStockIds[i] : "", r.day, s.marketFromDay);
                    revenue += StockMarket.CommissionAt(ids[i], rate); client.deals = checked(client.deals + 1); WorkCareerState.CountRate(client, rate);
                    client.lastStockId = hasStock ? r.soldStockIds[i] : ""; client.lastStockDay = hasStock ? r.day : 0; client.lastStockPrice = price;
                    client.portfolio = price > 0 ? StockPortfolio.Add(client.portfolio, r.soldStockIds[i], r.day, price) : new StockPosition[0]; }
                int change = StockPortfolio.RelationshipChange(r.sold[i], r.closed[i], r.guaranteed[i], r.complaintResolved[i], StockPortfolio.IsConcern(mood), r.concernResolved[i]);
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
                || a.clients[i].awaitingReport != b.clients[i].awaitingReport || a.clients[i].complaintPending != b.clients[i].complaintPending
                || a.clients[i].lastStockDay != b.clients[i].lastStockDay || (a.clients[i].lastStockId ?? "") != (b.clients[i].lastStockId ?? "")
                || a.clients[i].lastStockPrice != b.clients[i].lastStockPrice
                || a.clients[i].dealsAt3 != b.clients[i].dealsAt3 || a.clients[i].dealsAt5 != b.clients[i].dealsAt5
                || !StockPortfolio.Same(a.clients[i].portfolio, b.clients[i].portfolio)) return false;
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
            guaranteed = (bool[])r.guaranteed.Clone(), complaintResolved = (bool[])r.complaintResolved.Clone(), soldStockIds = WorkCareerState.CopyStockIds(r.soldStockIds),
            soldPrices = WorkCareerState.CopyPrices(r.soldPrices), concernResolved = WorkCareerState.CopyFlags(r.concernResolved) };
        private static FirmCareerSave Copy(FirmCareerSave s)
        {
            var copy = new FirmCareerSave { schemaVersion = s.schemaVersion, mode = s.mode, sourceDay = s.sourceDay, stockHistoryFromDay = s.stockHistoryFromDay, marketFromDay = s.marketFromDay,
                portfolioFromDay = s.portfolioFromDay,
                sourceSavings = s.sourceSavings, personalReserve = s.personalReserve, assistantHireDay = s.assistantHireDay, origin = CopyCheckpoint(s.origin),
                checkpoint = CopyCheckpoint(s.checkpoint), reports = new FirmDayReport[s.reports.Length] };
            for (int i = 0; i < s.reports.Length; i++) copy.reports[i] = CopyReport(s.reports[i]); return copy;
        }
    }
}
