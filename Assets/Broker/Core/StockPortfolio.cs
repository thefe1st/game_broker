using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // v5: one quoted purchase in a client portfolio. Shares are derived: StockMarket.Shares(client, priceCents).
    [Serializable] public sealed class StockPosition
    {
        public string stockId = "";
        public int day, priceCents;
    }

    public enum PortfolioMood { None, Calm, Growth, Drop, DeepDrop }

    // v5 client portfolios of work days and the founder office: up to three latest purchases with a quote, oldest first.
    // A fourth purchase pushes out the oldest: the client sold it on their own, outside the game; player money is untouched.
    // The morning mood compares today's quotes with the purchase prices of the whole portfolio, in integer tenths of a percent.
    // Test balance, not an investment model and not a promise of any price.
    public static class StockPortfolio
    {
        public const int MaxPositions = 3;
        // +5% and better: growth; −5% and worse: drop; −15% and worse: deep drop.
        public const int GrowthTenths = 50, DropTenths = -50, DeepDropTenths = -150;

        public static long CostCents(string clientId, StockPosition[] positions)
        {
            long total = 0;
            if (positions != null) foreach (var p in positions) total += (long)StockMarket.Shares(clientId, p.priceCents) * p.priceCents;
            return total;
        }
        public static long ValueCents(string clientId, StockPosition[] positions, long day)
        {
            long total = 0;
            if (positions != null) foreach (var p in positions) total += (long)StockMarket.Shares(clientId, p.priceCents) * StockMarket.PriceCents(p.stockId, day);
            return total;
        }
        // Change of the whole portfolio in tenths of a percent, rounded toward zero; 0 when empty.
        public static int ChangeTenths(string clientId, StockPosition[] positions, long day)
        {
            long cost = CostCents(clientId, positions);
            return cost <= 0 ? 0 : (int)((ValueCents(clientId, positions, day) - cost) * 1000 / cost);
        }
        public static PortfolioMood Mood(string clientId, StockPosition[] positions, long day)
        {
            if (positions == null || positions.Length == 0) return PortfolioMood.None;
            int change = ChangeTenths(clientId, positions, day);
            return change <= DeepDropTenths ? PortfolioMood.DeepDrop : change <= DropTenths ? PortfolioMood.Drop
                : change >= GrowthTenths ? PortfolioMood.Growth : PortfolioMood.Calm;
        }
        public static bool IsConcern(PortfolioMood mood) => mood == PortfolioMood.Drop || mood == PortfolioMood.DeepDrop;
        // Position with the lowest change since purchase; the oldest wins a tie. -1 when empty.
        public static int WorstIndex(StockPosition[] positions, long day)
        {
            int worst = -1; long worstTenths = long.MaxValue;
            for (int i = 0; positions != null && i < positions.Length; i++)
            {
                long tenths = ((long)StockMarket.PriceCents(positions[i].stockId, day) - positions[i].priceCents) * 1000 / positions[i].priceCents;
                if (tenths < worstTenths) { worst = i; worstTenths = tenths; }
            }
            return worst;
        }

        // "RLOG, день 8: 304 акции по $24,60 → сегодня $23,10 (−6,1%)"
        public static string Line(string clientId, StockPosition p, long today)
        {
            var stock = StockCatalog.Find(p.stockId); int now = StockMarket.PriceCents(p.stockId, today);
            return stock.Ticker + ", день " + p.day + ": " + StockMarket.SharesText(StockMarket.Shares(clientId, p.priceCents)) + " по "
                + StockMarket.Money(p.priceCents) + " → сегодня " + StockMarket.Money(now) + " (" + StockMarket.Change(p.priceCents, now) + ")";
        }
        public static string MoodNote(PortfolioMood mood)
        {
            switch (mood)
            {
                case PortfolioMood.Growth: return "рост — клиент в духе, +1 доверия на звонок";
                case PortfolioMood.Drop: return "падение — звонок начнётся с вопроса о цене";
                case PortfolioMood.DeepDrop: return "сильное падение — вопрос о цене, −1 доверия и в этот день только умеренные бумаги";
                case PortfolioMood.Calm: return "спокойно — в пределах ±5%";
                default: return "пусто";
            }
        }
        // Client base: every position with today's quote, the total and the reaction. founderOpeningDay > 0 marks employee purchases.
        public static string Describe(WorkClientState client, long today, int founderOpeningDay = 0)
        {
            var positions = client.portfolio;
            if (positions == null || positions.Length == 0)
                return client.deals > 0 || !string.IsNullOrEmpty(client.lastStockId)
                    ? "Портфель: покупок с котировкой нет; бумаги до котировок в портфель не входят, цена не реагирует."
                    : "Портфель пуст: после первой покупки цена будет влиять на настроение клиента.";
            var lines = new List<string>();
            foreach (var p in positions) lines.Add(Line(client.id, p, today) + (founderOpeningDay > 0 && p.day < founderOpeningDay ? " (ещё в найме)" : ""));
            int change = ChangeTenths(client.id, positions, today);
            return "Портфель по ценам дня " + today + " (" + positions.Length + " из " + MaxPositions + "): " + string.Join("; ", lines) + ". Всего "
                + StockMarket.Money(CostCents(client.id, positions)) + " → " + StockMarket.Money(ValueCents(client.id, positions, today))
                + " (" + StockMarket.Percent(change) + "): " + MoodNote(Mood(client.id, positions, today)) + ".";
        }

        internal static StockPosition[] FromLastPurchase(WorkClientState c) => c.lastStockPrice > 0
            ? new[] { new StockPosition { stockId = c.lastStockId ?? "", day = c.lastStockDay, priceCents = c.lastStockPrice } } : new StockPosition[0];
        internal static StockPosition[] Add(StockPosition[] positions, string stockId, int day, int priceCents)
        {
            var list = new List<StockPosition>(Copy(positions)) { new StockPosition { stockId = stockId, day = day, priceCents = priceCents } };
            while (list.Count > MaxPositions) list.RemoveAt(0);
            return list.ToArray();
        }
        // Null reads as empty: Unity writes a null array as []. Null elements stay null so validation rejects them.
        internal static StockPosition[] Copy(StockPosition[] positions)
        {
            if (positions == null) return new StockPosition[0];
            var copy = new StockPosition[positions.Length];
            for (int i = 0; i < copy.Length; i++) copy[i] = positions[i] == null ? null
                : new StockPosition { stockId = positions[i].stockId ?? "", day = positions[i].day, priceCents = positions[i].priceCents };
            return copy;
        }
        internal static bool Same(StockPosition[] a, StockPosition[] b)
        {
            a = Copy(a); b = Copy(b); if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] == null || b[i] == null ? a[i] != b[i] : a[i].stockId != b[i].stockId || a[i].day != b[i].day || a[i].priceCents != b[i].priceCents) return false;
            return true;
        }
        // Shared v5 rule for work and founder checkpoints. Every position is a canonical recorded purchase at the day's quote,
        // strictly by day, at most three, and the last one is the client's last purchase; without a quoted last purchase it is empty.
        // Positions from ownFrom are this career's purchases: quoted days only, counted among its deals and 3%/5% rates.
        // Earlier positions (founder only) were bought while still employed and keep their own quote.
        internal static bool Valid(WorkClientState c, long ownFrom, long marketFrom)
        {
            var positions = c.portfolio ?? new StockPosition[0];
            if (positions.Length > MaxPositions) return false;
            long previous = long.MinValue; int own = 0, at3 = 0, at5 = 0;
            foreach (var p in positions)
            {
                if (p == null || p.day <= previous || !WorkCareerState.ValidStockRecord(c.id, p.stockId, p.day)
                    || p.priceCents != StockMarket.PriceCents(p.stockId, p.day) || !StockMarket.ValidPurchasePrice(c.id, p.priceCents)) return false;
                previous = p.day;
                if (p.day < ownFrom) continue;
                if (p.day < marketFrom) return false;
                own++; int fee = StockMarket.FeePercent(p.stockId); if (fee == 3) at3++; else if (fee == 5) at5++;
            }
            if (own > c.deals || at3 > c.dealsAt3 || at5 > c.dealsAt5) return false;
            if (positions.Length == 0) return c.lastStockPrice == 0;
            var last = positions[positions.Length - 1];
            return last.stockId == c.lastStockId && last.day == c.lastStockDay && last.priceCents == c.lastStockPrice;
        }
        // Shared v5 report rule: a concern exists only on a morning drop of 5% or more; it is settled honestly in a closed call
        // without a false guarantee; a sale needs it settled; after a deep drop only a moderate stock may be sold that day.
        internal static bool ValidReaction(PortfolioMood mood, bool sold, bool closed, bool guaranteed, bool concernResolved, string soldStockId)
        {
            bool concern = IsConcern(mood);
            if (concernResolved && (!concern || !closed || guaranteed)) return false;
            if (sold && concern && !concernResolved) return false;
            var stock = StockCatalog.Find(soldStockId);
            return !sold || mood != PortfolioMood.DeepDrop || (stock != null && stock.Risk == StockRisk.Moderate);
        }
        // Shared relationship change of a day slot. Without a concern this is the v4 rule.
        // A settled concern spares the −1 of a call without a sale; an ignored concern (no call at all) costs 1.
        internal static int RelationshipChange(bool sold, bool closed, bool guaranteed, bool complaintResolved, bool concern, bool concernResolved)
        {
            int change = sold ? 1 : complaintResolved || concernResolved ? 0 : closed ? -1 : 0;
            return change + (complaintResolved ? 1 : 0) - (guaranteed ? 1 : 0) - (concern && !closed ? 1 : 0);
        }
    }
}
