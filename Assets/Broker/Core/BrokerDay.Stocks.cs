namespace Broker.Core
{
    public sealed partial class BrokerDay
    {
        public StockOffer CurrentStock => StockCatalog.Find(Current.StockId);
        public int CurrentInvestment => Current.Commission * 100 / StockCatalog.FeePercent;
        public string StockTermsText => "Акции «" + CurrentStock.Name + "» (" + CurrentStock.Ticker + "). " + CurrentStock.RiskDisclosure
            + " Горизонт — " + CurrentStock.HorizonMonths + " месяцев, не срок гарантированного возврата. Комиссия 2%: $"
            + Current.Commission + " от вложения $" + CurrentInvestment + ".";
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
            return "Выбраны акции «" + stock.Name + "» (" + stock.Ticker + "). Риск и комиссию нужно раскрыть заново. " + StockCatalog.Suitability(Current.Id, stock);
        }
        // Explicit sales action, not a keyword shortcut: it speaks the exact selected terms.
        public string ExplainStockTerms()
        {
            if (DayEnded || PendingEvent != null || Call != CallStatus.Active || Current.ObjectionRaised)
                return "Раскрытие свойств акции сейчас недоступно.";
            Current.RisksExplained = true; Current.StockRiskExplained = true; Current.FeesExplained = true;
            return Current.Name + ": Условия акций «" + CurrentStock.Name + "» услышал. "
                + StockCatalog.Suitability(Current.Id, CurrentStock);
        }
    }
}
