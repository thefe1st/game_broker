using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // v5: client portfolios react to the day's quotes in work days and the founder office.
    public sealed partial class BrokerDay
    {
        // Positions bought before today; the mood is fixed once, before any call. Growth: +1 trust; deep drop: −1 trust.
        internal void AttachPortfolio(int slot, StockPosition[] positions)
        {
            var client = clients[slot];
            if (!HasQuotes || client.PortfolioAttached || client.Call != CallStatus.NotStarted) return;
            client.PortfolioAttached = true; client.Positions = StockPortfolio.Copy(positions);
            client.Mood = StockPortfolio.Mood(client.Id, client.Positions, DayNumber);
            client.PortfolioChangeTenths = StockPortfolio.ChangeTenths(client.Id, client.Positions, DayNumber);
            if (client.Mood == PortfolioMood.Growth) client.Trust = Math.Min(4, client.Trust + 1);
            else if (client.Mood == PortfolioMood.DeepDrop) client.Trust = Math.Max(0, client.Trust - 1);
        }
        // After a deep drop the client accepts only moderate stocks that day; the catalog limits still apply.
        public bool FitsToday(StockOffer stock) => StockCatalog.Fits(Current.Id, stock)
            && (Current.Mood != PortfolioMood.DeepDrop || stock.Risk == StockRisk.Moderate);
        public string StockSuitability(StockOffer stock) => StockCatalog.Fits(Current.Id, stock) && !FitsToday(stock)
            ? "После сильного падения портфеля клиент сегодня согласен только на умеренный риск: SVET или RLOG. Давление и досье этого не меняют."
            : StockCatalog.Suitability(Current.Id, stock);
        // Rail and call card: "Портфель −7,4%"; empty without positions or outside quoted careers.
        public static string PortfolioBadge(ClientRecord client)
            => client.Positions.Length == 0 ? "" : "Портфель " + StockMarket.Percent(client.PortfolioChangeTenths);
        private string WorstPositionText
        {
            get
            {
                var positions = Current.Positions; int worst = StockPortfolio.WorstIndex(positions, DayNumber);
                var p = positions[worst]; int now = QuoteCents(p.stockId);
                long tenths = ((long)now - p.priceCents) * 1000 / p.priceCents;
                return StockCatalog.Find(p.stockId).Ticker + (tenths < 0 ? " упали на " + StockMarket.Percent(-tenths).TrimStart('+') : " " + StockMarket.Percent(tenths))
                    + " с покупки в день " + p.day + " (" + StockMarket.Money(p.priceCents) + " → " + StockMarket.Money(now) + ")";
            }
        }
        private string PortfolioChangeText => "весь портфель " + StockMarket.Percent(Current.PortfolioChangeTenths) + " к цене покупки";
        private string PriceConcernOpening => Current.Mood == PortfolioMood.DeepDrop
            ? "Мои акции " + WorstPositionText + ", " + PortfolioChangeText + ". Я теряю деньги. Объясните честно, что происходит, — и сегодня никакого повышенного риска."
            : "Мои акции " + WorstPositionText + ", " + PortfolioChangeText + ". Что происходит? Только без сказок, что цена «точно вернётся».";
        private string PriceConcernAside => "И ещё: " + PortfolioChangeText + ", акции " + WorstPositionText + ". Об этом поговорим сразу после претензии.";
        private string GrowthAside => "Кстати, " + PortfolioChangeText + ". Приятно, но это не обещание.";
        private string PriceConcernHint => Current.FalseGuaranteeMade
            ? "Прозвучала ложная гарантия. Падение цены сегодня не урегулировать, новой сделки не будет; на следующем звонке — претензия."
            : "Портфель клиента " + StockMarket.Percent(Current.PortfolioChangeTenths) + " к цене покупки. Объясни падение честно, без обещаний роста: например, «Цена упала — это риск акций. Гарантировать рост не могу»."
                + (Current.Mood == PortfolioMood.DeepDrop ? " После сильного падения сегодня только умеренные бумаги: SVET или RLOG." : "");
        private string PriceConcernSettled => Current.Mood == PortfolioMood.DeepDrop
            ? "Спасибо за честность. Убыток это не отменяет; сегодня обсуждаю только умеренный риск."
            : "Честно — уже лучше. Цена может упасть и дальше, это я понимаю.";
        // Results: reactions of today's clients. Information about the finished day, no new effects.
        public string PriceReactionsSummary
        {
            get
            {
                var parts = new List<string>();
                foreach (var c in clients)
                {
                    if (c.Mood == PortfolioMood.Growth) { parts.Add(c.Name + " — рост " + StockMarket.Percent(c.PortfolioChangeTenths) + ", +1 доверия"); continue; }
                    if (!c.PriceConcern) continue;
                    string outcome = c.PriceConcernResolved ? "объяснили честно" : c.FalseGuaranteeMade ? "ложная гарантия — претензия на следующий звонок"
                        : c.Call == CallStatus.Closed ? "не объяснили" : "не позвонили: отношения −1";
                    parts.Add(c.Name + " — " + (c.Mood == PortfolioMood.DeepDrop ? "сильное падение " : "падение ") + StockMarket.Percent(c.PortfolioChangeTenths) + ", " + outcome);
                }
                return parts.Count == 0 ? "" : "Портфели: " + string.Join("; ", parts) + ".";
            }
        }
    }
}
