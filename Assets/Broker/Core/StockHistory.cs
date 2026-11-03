using System.Collections.Generic;

namespace Broker.Core
{
    // Text for the v3 stock history. Only stock IDs are stored; investment and commission are derived:
    // commission is 2% of the client's investment, the personal share in employee days is 20%.
    public static class StockHistory
    {
        public static int Investment(string clientId) => ClientCatalog.Create(clientId).Commission * 100 / StockCatalog.FeePercent;
        // "RLOG $7500"; unknown ID (sale before stock history) is never guessed.
        public static string Short(string clientId, string stockId)
        {
            var stock = StockCatalog.Find(stockId);
            return stock == null ? "бумага до истории акций" : stock.Ticker + " $" + Investment(clientId);
        }
        // "«Регион Логистика» (RLOG), вложение $7500, комиссия фирмы $150, лично вам $30"
        public static string Describe(string clientId, string stockId, bool employee)
        {
            var stock = StockCatalog.Find(stockId);
            if (stock == null) return "до истории акций: бумага не записана";
            int commission = ClientCatalog.Create(clientId).Commission;
            return "«" + stock.Name + "» (" + stock.Ticker + "), вложение $" + Investment(clientId) + ", комиссия фирмы $" + commission
                + (employee ? ", лично вам $" + BrokerDay.PersonalPayout(commission) : "");
        }
        // Results line: "Продано сегодня: Борис — RLOG $7500, Вера — SVET $10000."
        public static string SoldToday(string[] clientIds, bool[] sold, string[] stockIds)
        {
            var parts = new List<string>();
            for (int i = 0; i < clientIds.Length; i++)
                if (sold[i]) parts.Add(ClientCatalog.Create(clientIds[i]).Name + " — " + Short(clientIds[i], stockIds != null && i < stockIds.Length ? stockIds[i] : ""));
            return parts.Count == 0 ? "Сегодня сделок нет." : "Продано сегодня: " + string.Join(", ", parts) + ".";
        }
        // Client base: the last recorded purchase survives the seven-day folding.
        // founderOpeningDay > 0: purchases before it were made while still employed.
        public static string LastPurchase(WorkClientState client, bool employee, int founderOpeningDay = 0)
        {
            if (!string.IsNullOrEmpty(client.lastStockId) && StockCatalog.Find(client.lastStockId) != null)
            {
                bool carried = founderOpeningDay > 0 && client.lastStockDay < founderOpeningDay;
                var stock = StockCatalog.Find(client.lastStockId);
                return carried
                    ? "Последняя покупка ещё в найме, день " + client.lastStockDay + ": «" + stock.Name + "» (" + stock.Ticker + "), вложение $" + Investment(client.id)
                        + ". Комиссия тогда ушла прежней фирме."
                    : "Последняя покупка, день " + client.lastStockDay + ": " + Describe(client.id, client.lastStockId, employee) + ".";
            }
            if (client.deals > 0) return "Последняя покупка — до истории акций: бумага не записана, задним числом не подставляется.";
            return founderOpeningDay > 0 ? "В своей конторе покупок ещё не было." : "Покупок в рабочих днях ещё не было.";
        }
    }
}
