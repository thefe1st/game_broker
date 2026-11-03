using System.Collections.Generic;

namespace Broker.Core
{
    // Text for the stock history. IDs and v4 purchase prices are stored; investment and commission are derived.
    // A price above zero marks a v4 market day: commission = rate of the stock × investment.
    // Price 0 marks a day before quotes: the fixed 2%. The personal share in employee days is 20%.
    public static class StockHistory
    {
        public static int Investment(string clientId) => ClientCatalog.Create(clientId).Commission * 100 / StockCatalog.FeePercent;
        public static int Commission(string clientId, string stockId, int priceCents)
            => priceCents > 0 ? StockMarket.Commission(clientId, stockId) : ClientCatalog.Create(clientId).Commission;
        // "RLOG $7500"; with a price: "RLOG $7500 (304 акции по $24,60, комиссия 2%)". Unknown ID is never guessed.
        public static string Short(string clientId, string stockId, int priceCents = 0)
        {
            var stock = StockCatalog.Find(stockId);
            if (stock == null) return "бумага до истории акций";
            return stock.Ticker + " $" + Investment(clientId) + (priceCents > 0 ? " (" + Purchase(clientId, priceCents)
                + ", комиссия " + stock.FeePercent + "%)" : "");
        }
        // "«Регион Логистика» (RLOG), вложение $7500, комиссия фирмы $150, лично вам $30" before quotes;
        // "«Регион Логистика» (RLOG): 304 акции по $24,60, вложение $7500, комиссия 2% — фирме $150, лично вам $30" with a price.
        public static string Describe(string clientId, string stockId, bool employee, int priceCents = 0)
        {
            var stock = StockCatalog.Find(stockId);
            if (stock == null) return "до истории акций: бумага не записана";
            int commission = Commission(clientId, stockId, priceCents);
            string personal = employee ? ", лично вам $" + BrokerDay.PersonalPayout(commission) : "";
            return priceCents > 0
                ? "«" + stock.Name + "» (" + stock.Ticker + "): " + Purchase(clientId, priceCents) + ", вложение $" + Investment(clientId)
                    + ", комиссия " + stock.FeePercent + "% — фирме $" + commission + personal
                : "«" + stock.Name + "» (" + stock.Ticker + "), вложение $" + Investment(clientId) + ", комиссия фирмы $" + commission + personal;
        }
        // Results line: "Продано сегодня: Борис — RLOG $7500 (304 акции по $24,60, комиссия 2%)."
        public static string SoldToday(string[] clientIds, bool[] sold, string[] stockIds, int[] prices = null)
        {
            var parts = new List<string>();
            for (int i = 0; i < clientIds.Length; i++)
                if (sold[i]) parts.Add(ClientCatalog.Create(clientIds[i]).Name + " — " + Short(clientIds[i], stockIds != null && i < stockIds.Length ? stockIds[i] : "",
                    prices != null && i < prices.Length ? prices[i] : 0));
            return parts.Count == 0 ? "Сегодня сделок нет." : "Продано сегодня: " + string.Join(", ", parts) + ".";
        }
        // Client base: the last recorded purchase survives the seven-day folding.
        // founderOpeningDay > 0: purchases before it were made while still employed.
        // today > 0: compare the purchase price with today's quote. Information only: no money or trust.
        public static string LastPurchase(WorkClientState client, bool employee, int founderOpeningDay = 0, int today = 0)
        {
            if (!string.IsNullOrEmpty(client.lastStockId) && StockCatalog.Find(client.lastStockId) != null)
            {
                bool carried = founderOpeningDay > 0 && client.lastStockDay < founderOpeningDay;
                var stock = StockCatalog.Find(client.lastStockId);
                string text = carried
                    ? "Последняя покупка ещё в найме, день " + client.lastStockDay + ": «" + stock.Name + "» (" + stock.Ticker + "), вложение $" + Investment(client.id)
                        + (client.lastStockPrice > 0 ? ", " + Purchase(client.id, client.lastStockPrice) : "") + ". Комиссия тогда ушла прежней фирме."
                    : "Последняя покупка, день " + client.lastStockDay + ": " + Describe(client.id, client.lastStockId, employee, client.lastStockPrice) + ".";
                return text + TodayQuote(client.lastStockId, client.lastStockPrice, today);
            }
            if (client.deals > 0) return "Последняя покупка — до истории акций: бумага не записана, задним числом не подставляется.";
            return founderOpeningDay > 0 ? "В своей конторе покупок ещё не было." : "Покупок в рабочих днях ещё не было.";
        }
        // " Сегодня $18,90 (+2,7% к цене покупки). Цена не обещание." or, before quotes, " Куплено до котировок; сегодня $18,90."
        public static string TodayQuote(string stockId, int purchaseCents, int today)
        {
            if (today <= 0 || StockCatalog.Find(stockId) == null) return "";
            int now = StockMarket.PriceCents(stockId, today);
            return purchaseCents > 0
                ? " Сегодня " + StockMarket.Money(now) + " (" + StockMarket.Change(purchaseCents, now) + " к цене покупки). Цена не обещание."
                : " Куплено до котировок; сегодня " + StockMarket.Money(now) + ".";
        }
        private static string Purchase(string clientId, int priceCents)
            => StockMarket.SharesText(StockMarket.Shares(clientId, priceCents)) + " по " + StockMarket.Money(priceCents);
    }
}
