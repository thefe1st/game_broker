using Broker.Core;
using Broker.Runtime.Dialogue;
using Broker.Runtime.Office;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private bool showStocks;
        private int stockPreviewIndex;
        private string stockNotice = "";
        private void OpenStocks()
        {
            if (!ApplicationActive || selectingMode || intro || day.DayEnded || day.PendingEvent != null || UtilityOverlayOpen
                || busy || voice.Working || voice.Recording) return;
            var offers = StockCatalog.Offers; stockPreviewIndex = 0;
            for (int i = 0; i < offers.Length; i++) if (offers[i].Id == day.Current.StockId) stockPreviewIndex = i;
            showStocks = true; stockNotice = ""; voice.Cancel(); office.Player.SetControl(false); GUI.FocusControl(null);
        }
        private void DrawStocks()
        {
            Scrim(); Panel(140, 64, 1160, 856, "");
            bool quotes = day.HasQuotes;
            Label(180, 92, 1080, 36, quotes ? "АКЦИИ / ВЫМЫШЛЕННЫЕ КОМПАНИИ / КОТИРОВКИ ДНЯ " + day.DayNumber : "АКЦИИ / ВЫМЫШЛЕННЫЕ КОМПАНИИ", theme.Tag);
            Label(180, 144, 1080, 44, "Предложение для «" + day.Current.Name + "»", theme.Title);
            var offers = StockCatalog.Offers;
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i]; float y = 220 + i * 88;
                string caption = offer.Ticker + " / " + offer.Name + "\n" + offer.RiskLabel + " · " + offer.HorizonMonths + " мес.";
                if (quotes) caption += "\n" + StockMarket.Money(day.QuoteCents(offer.Id)) + " · " + DayChange(offer.Id) + " · комиссия " + day.FeePercentFor(offer.Id) + "%";
                if (Button(180, y, 420, quotes ? 84 : 76, caption, true, i == stockPreviewIndex)) stockPreviewIndex = i;
            }
            var stock = offers[stockPreviewIndex];
            Label(640, 220, 600, 40, stock.Name + " / " + stock.Ticker, theme.Heading);
            if (quotes)
            {
                int price = day.QuoteCents(stock.Id), commission = day.CommissionFor(stock.Id);
                Label(640, 264, 600, 28, stock.Sector + " · цена дня " + StockMarket.Money(price) + " (" + DayChange(stock.Id) + " ко вчера)", theme.Small);
                DrawQuoteChart(640, 298, 600, 84, StockMarket.Chart(stock.Id, day.DayNumber));
                Label(640, 392, 600, 76, stock.Description, theme.Text);
                Label(640, 472, 600, 48, stock.RiskDisclosure, theme.Small);
                Label(640, 524, 600, 56, "Горизонт " + stock.HorizonMonths + " мес. Это не обещание выкупа. Комиссия " + day.FeePercentFor(stock.Id) + "% — ставка "
                    + (stock.Risk == StockRisk.Moderate ? "умеренных бумаг" : stock.Risk == StockRisk.Growth ? "бумаг роста" : "спекулятивных бумаг") + "; цена дня её не меняет.", theme.Small);
                Label(640, 592, 600, 48, "Вложение клиента: $" + day.CurrentInvestment + " → " + StockMarket.SharesText(StockMarket.Shares(day.Current.Id, price)) + " по " + StockMarket.Money(price)
                    + "; фирме $" + commission + (IsEmployeeMode ? ", лично вам $" + BrokerDay.PersonalPayout(commission) + " (20%)." : ". Вся комиссия в кассу."), theme.Small);
            }
            else
            {
                Label(640, 272, 600, 28, stock.Sector + " · обычные акции", theme.Small);
                Label(640, 320, 600, 100, stock.Description, theme.Text);
                Label(640, 428, 600, 88, stock.RiskDisclosure, theme.Small);
                Label(640, 524, 600, 56, "Горизонт " + stock.HorizonMonths + " мес. Это не обещание выкупа. Комиссия 2%: в этом режиме ставка одна, цены не моделируются.", theme.Small);
                Label(640, 592, 600, 48, "Вложение клиента: $" + day.CurrentInvestment + "; фирме $" + day.Current.Commission
                    + (IsEmployeeMode ? ", лично вам $" + BrokerDay.PersonalPayout(day.Current.Commission) + " (20%)." : ". Вся комиссия в кассу."), theme.Small);
            }
            Label(640, 648, 600, 60, StockCatalog.Suitability(day.Current.Id, stock), theme.Small);
            if (Button(640, 724, 600, 48, "Выбрать / " + stock.Ticker, ApplicationActive && day.CanSelectStock && !busy && !voice.Recording && !voice.Working, true))
            { stockNotice = day.SelectStock(stock.Id); News(stockNotice); }
            Label(180, 776, 720, 96, string.IsNullOrEmpty(stockNotice) ? day.StockSelectionNotice : stockNotice, theme.Small);
            if (Button(932, 808, 308, 48, "Вернуться к звонку / Esc")) CloseUtilityOverlay();
            Label(180, 872, 1080, 40, quotes
                ? "Котировки вымышленные: одна цена на день, без реальных данных; портфель не моделируется. Проданная бумага попадёт в историю после завершения дня вместе с ценой покупки."
                : "Котировки есть только в рабочих днях и своей конторе. В этом режиме сохраняются сумма сделки и обязательства, не тикер.", theme.Small);
        }
        private string DayChange(string stockId) => StockMarket.Change(StockMarket.PriceCents(stockId, day.DayNumber - 1L), day.QuoteCents(stockId));
        // Seven daily bars, oldest first; today is highlighted. Display only: the chart never changes money or trust.
        private void DrawQuoteChart(float x, float y, float w, float h, int[] prices)
        {
            Paint(new Rect(x, y, w, h), BrokerTheme.Hex("#0F1B2D"));
            int min = int.MaxValue, max = 0;
            foreach (int p in prices) { min = System.Math.Min(min, p); max = System.Math.Max(max, p); }
            float slot = (w - 16) / prices.Length, plot = h - 40, top = y + 8;
            for (int i = 0; i < prices.Length; i++)
            {
                float bar = 6 + (max == min ? plot / 2 : (prices[i] - min) * (plot - 6) / (max - min));
                var color = i == prices.Length - 1 ? theme.Accent : i > 0 && prices[i] < prices[i - 1] ? theme.Red : theme.Green;
                Paint(new Rect(x + 8 + i * slot + 6, top + plot - bar, slot - 12, bar), color);
            }
            Label(x + 12, y + h - 28, w - 24, 22, "7 игровых дней по день " + day.DayNumber + ": мин " + StockMarket.Money(min) + " · макс " + StockMarket.Money(max), theme.Small);
        }
        private void ExplainCurrentStock()
        {
            if (!ApplicationActive || busy || UtilityOverlayOpen || intro || day.DayEnded || day.PendingEvent != null
                || day.Call != CallStatus.Active || day.Current.ObjectionRaised || voice.Recording || voice.Working) return;
            string spoken = day.StockTermsText; string reply = day.ExplainStockTerms();
            Add("Вы: " + spoken); History.Add(new DialogueTurn("player", spoken)); PresentClientReply(reply);
        }
    }
}
