using System;
using System.Globalization;

namespace Broker.Core
{
    // v4 market for repeatable careers: one fictional quote per stock and game day, fee by risk.
    // Integer arithmetic only, so every platform computes the same price. Bounded waves plus
    // hash noise: no real data, no endless drift, not an investment model.
    public static class StockMarket
    {
        public const int ChartDays = 7;
        // Base price in cents; slow wave amplitude (basis points)/period; fast wave amplitude/period; noise amplitude.
        private static readonly int[][] profiles = {
            new[] { 1840, 500, 29, 250, 11, 120 },   // utility   SVET
            new[] { 2460, 700, 23, 350, 9, 200 },    // logistics RLOG
            new[] { 4120, 1400, 19, 700, 7, 400 },   // medical   MDAT
            new[] { 5780, 1600, 17, 800, 8, 450 },   // cloud     OBLK
            new[] { 135, 3500, 13, 1500, 5, 1200 },  // ore       NEDR
            new[] { 390, 3800, 11, 1700, 6, 1300 },  // biotech   BIMP
        };
        public static int FeePercent(string stockId) { var stock = StockCatalog.Find(stockId); return stock == null ? 0 : stock.FeePercent; }
        public static int Investment(string clientId) => ClientCatalog.Create(clientId).Commission * 100 / StockCatalog.FeePercent;
        // Whole dollars: rate of the stock times the client's fixed investment.
        public static int Commission(string clientId, string stockId) => Investment(clientId) * FeePercent(stockId) / 100;
        // Highest commission a client can pay: the top rate among stocks within the client's limits.
        // The lowest is always the fixed 2%: every client fits the moderate stocks.
        public static int MaxCommission(string clientId)
        {
            int max = 0;
            foreach (var stock in StockCatalog.Offers) if (StockCatalog.Fits(clientId, stock)) max = Math.Max(max, Commission(clientId, stock.Id));
            return max;
        }
        public static int CommissionAt(string clientId, int feePercent) => Investment(clientId) * feePercent / 100;
        // A rate the client can pay: some stock within the client's limits has it.
        public static bool CanPay(string clientId, int feePercent)
        {
            foreach (var stock in StockCatalog.Offers) if (stock.FeePercent == feePercent && StockCatalog.Fits(clientId, stock)) return true;
            return false;
        }
        // Saved careers: a sale from marketFromDay pays the rate of its stock, earlier imported days the fixed 2%.
        internal static int SaleRate(string stockId, long day, long marketFrom)
            => day >= marketFrom && StockCatalog.Find(stockId) != null ? StockCatalog.Find(stockId).FeePercent : StockCatalog.FeePercent;
        // Stored price of a recorded purchase: the day's quote from marketFromDay, 0 before quotes.
        internal static int RecordedPrice(string stockId, long day, long marketFrom) => day >= marketFrom ? PriceCents(stockId, day) : 0;
        public static int PriceCents(string stockId, long day)
        {
            int index = Index(stockId); var p = profiles[index];
            long bp = 10000 + Wave(day + Phase(index, 1, p[2]), p[2], p[1]) + Wave(day + Phase(index, 2, p[4]), p[4], p[3]) + Noise(index, day, p[5]);
            return (int)Math.Max(1, p[0] * bp / 10000);
        }
        public static int MinPriceCents(string stockId) { var p = profiles[Index(stockId)]; return (int)Math.Max(1, p[0] * (10000L - p[1] - p[3] - p[5]) / 10000); }
        public static int MaxPriceCents(string stockId) { var p = profiles[Index(stockId)]; return (int)(p[0] * (10000L + p[1] + p[3] + p[5]) / 10000); }
        // Whole shares; the remainder stays with the client and is not game money.
        public static int Shares(string clientId, int priceCents) => priceCents <= 0 ? 0 : (int)((long)Investment(clientId) * 100 / priceCents);
        // A recorded purchase price: positive and at least one share for the client's investment.
        public static bool ValidPurchasePrice(string clientId, int priceCents) => priceCents >= 1 && Shares(clientId, priceCents) >= 1;
        // Oldest first, ending with the given day.
        public static int[] Chart(string stockId, long day)
        {
            var prices = new int[ChartDays];
            for (int i = 0; i < ChartDays; i++) prices[i] = PriceCents(stockId, day - ChartDays + 1 + i);
            return prices;
        }
        public static string Money(long cents) => (cents < 0 ? "−$" : "$") + (Math.Abs(cents) / 100).ToString(CultureInfo.InvariantCulture)
            + "," + (Math.Abs(cents) % 100).ToString("00", CultureInfo.InvariantCulture);
        // "+2,0%", "−3,4%", "0,0%": change in tenths of a percent, rounded toward zero.
        public static string Change(int fromCents, int toCents)
        {
            if (fromCents <= 0) return "0,0%";
            long tenths = ((long)toCents - fromCents) * 1000 / fromCents;
            return (tenths > 0 ? "+" : tenths < 0 ? "−" : "") + (Math.Abs(tenths) / 10).ToString(CultureInfo.InvariantCulture)
                + "," + (Math.Abs(tenths) % 10).ToString(CultureInfo.InvariantCulture) + "%";
        }
        public static string SharesText(int shares)
        {
            int last = shares % 10, lastTwo = shares % 100;
            string word = last == 1 && lastTwo != 11 ? "акция" : last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14) ? "акции" : "акций";
            return shares.ToString(CultureInfo.InvariantCulture) + " " + word;
        }
        private static int Index(string stockId)
        {
            var offers = StockCatalog.Offers;
            for (int i = 0; i < offers.Length; i++) if (offers[i].Id == stockId) return i;
            throw new ArgumentException("Unknown stock", nameof(stockId));
        }
        // Triangle wave from -amplitude to +amplitude over one period.
        private static long Wave(long x, int period, int amplitude)
        {
            long m = x % period; if (m < 0) m += period;
            return (long)amplitude * (4 * Math.Min(m, period - m) - period) / period;
        }
        private static long Phase(int index, int salt, int period) => (long)(Mix(((ulong)index << 8) ^ (ulong)salt) % (ulong)period);
        private static long Noise(int index, long day, int amplitude)
            => (long)(Mix(((ulong)(index + 1) << 48) ^ unchecked((ulong)day)) % (ulong)(2 * amplitude + 1)) - amplitude;
        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL; z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL; return z ^ (z >> 31);
            }
        }
    }
}
