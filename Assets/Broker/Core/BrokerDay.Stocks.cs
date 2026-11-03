namespace Broker.Core
{
    public sealed partial class BrokerDay
    {
        public StockOffer CurrentStock => StockCatalog.Find(Current.StockId);
        public int CurrentInvestment => Current.Commission * 100 / StockCatalog.FeePercent;
        // v4: work days and the founder office use daily quotes and the fee of the selected stock.
        public bool HasQuotes => IsRepeatableCareer;
        public int FeePercentFor(string stockId) => HasQuotes ? StockMarket.FeePercent(stockId) : StockCatalog.FeePercent;
        public int CurrentFeePercent => FeePercentFor(Current.StockId);
        public int CommissionFor(ClientRecord client) => HasQuotes ? StockMarket.Commission(client.Id, client.StockId) : client.Commission;
        public int CommissionFor(string stockId) => HasQuotes ? StockMarket.Commission(Current.Id, stockId) : Current.Commission;
        public int CurrentCommission => CommissionFor(Current);
        public int QuoteCents(string stockId) => StockMarket.PriceCents(stockId, DayNumber);
        public int CurrentQuoteCents => QuoteCents(Current.StockId);
        public string StockTermsText => "Акции «" + CurrentStock.Name + "» (" + CurrentStock.Ticker + "). " + CurrentStock.RiskDisclosure
            + " Горизонт — " + CurrentStock.HorizonMonths + " месяцев, не срок гарантированного возврата. Комиссия " + CurrentFeePercent + "%: $"
            + CurrentCommission + " от вложения $" + CurrentInvestment + "."
            + (HasQuotes ? " Цена сегодня " + StockMarket.Money(CurrentQuoteCents) + " за акцию: на вложение — "
                + StockMarket.SharesText(StockMarket.Shares(Current.Id, CurrentQuoteCents)) + ". Цена может упасть." : "");
        // Report of a returning client: purchase price vs today's quote. Information only, no trust or money.
        public string ReportQuoteNote
        {
            get
            {
                if (!HasQuotes || string.IsNullOrEmpty(Current.LastStockId) || StockCatalog.Find(Current.LastStockId) == null) return "";
                var stock = StockCatalog.Find(Current.LastStockId); int today = QuoteCents(stock.Id);
                return Current.LastStockPrice > 0
                    ? " " + stock.Ticker + ": куплены в день " + Current.LastStockDay + " по " + StockMarket.Money(Current.LastStockPrice) + ", сегодня "
                        + StockMarket.Money(today) + " (" + StockMarket.Change(Current.LastStockPrice, today) + "). Цена не обещание."
                    : " " + stock.Ticker + ": куплены в день " + Current.LastStockDay + " до котировок, сегодня " + StockMarket.Money(today) + ".";
            }
        }
        internal void AttachLastPurchase(int slot, string stockId, int day, int priceCents)
        {
            var client = clients[slot]; client.LastStockId = stockId ?? ""; client.LastStockDay = day; client.LastStockPrice = priceCents;
        }
        public bool CanSelectStock => !DayEnded && PendingEvent == null && Call != CallStatus.Closed && !Current.ObjectionRaised;
        public string StockSelectionNotice => DayEnded ? "День завершён: предложение изменить нельзя."
            : Call == CallStatus.Closed ? "Звонок завершён: предложение изменить нельзя."
            : Current.ObjectionRaised ? "Предложение уже обсуждается. Акция зафиксирована до конца звонка."
            : "Можно выбрать акцию до первого принятого предложения. Смена сбрасывает раскрытие риска и комиссии, но сохраняет цель, отчёт и претензию.";
        public string SelectStock(string stockId)
        {
            var stock = StockCatalog.Find(stockId);
            if (stock == null || !CanSelectStock) return "Выбор акции недоступен. " + StockSelectionNotice;
            if (Current.StockId == stockId) return "Акция уже выбрана: " + stock.Name + ".";
            Current.StockId = stockId; Current.RisksExplained = false; Current.FeesExplained = false;
            Current.StockRiskExplained = false;
            return "Выбраны акции «" + stock.Name + "» (" + stock.Ticker + "). Риск и комиссию нужно раскрыть заново. " + StockSuitability(stock);
        }
        // Explicit sales action, not a keyword shortcut: it speaks the exact selected terms.
        public string ExplainStockTerms()
        {
            if (DayEnded || PendingEvent != null || Call != CallStatus.Active || Current.ObjectionRaised)
                return "Раскрытие свойств акции сейчас недоступно.";
            Current.RisksExplained = true; Current.StockRiskExplained = true; Current.FeesExplained = true;
            return Current.Name + ": Условия акций «" + CurrentStock.Name + "» услышал. "
                + StockSuitability(CurrentStock);
        }
    }
}
