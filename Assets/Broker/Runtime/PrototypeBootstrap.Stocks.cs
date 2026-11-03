using Broker.Core;
using Broker.Runtime.Dialogue;
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
            Label(180, 92, 1080, 36, "АКЦИИ / ВЫМЫШЛЕННЫЕ КОМПАНИИ", theme.Tag);
            Label(180, 144, 1080, 44, "Предложение для «" + day.Current.Name + "»", theme.Title);
            var offers = StockCatalog.Offers;
            for (int i = 0; i < offers.Length; i++)
            {
                var offer = offers[i]; float y = 220 + i * 88;
                if (Button(180, y, 420, 76, offer.Ticker + " / " + offer.Name + "\n" + offer.RiskLabel + " · " + offer.HorizonMonths + " мес.", true, i == stockPreviewIndex)) stockPreviewIndex = i;
            }
            var stock = offers[stockPreviewIndex];
            Label(640, 220, 600, 40, stock.Name + " / " + stock.Ticker, theme.Heading);
            Label(640, 272, 600, 28, stock.Sector + " · обычные акции", theme.Small);
            Label(640, 320, 600, 100, stock.Description, theme.Text);
            Label(640, 428, 600, 88, stock.RiskDisclosure, theme.Small);
            Label(640, 524, 600, 56, "Горизонт " + stock.HorizonMonths + " мес. Это не обещание выкупа. Комиссия 2%; ставки одинаковые, цены пока не моделируются.", theme.Small);
            Label(640, 592, 600, 48, "Вложение клиента: $" + day.CurrentInvestment + "; фирме $" + day.Current.Commission
                + (IsEmployeeMode ? ", лично вам $" + BrokerDay.PersonalPayout(day.Current.Commission) + " (20%)." : ". Вся комиссия в кассу."), theme.Small);
            Label(640, 648, 600, 60, StockCatalog.Suitability(day.Current.Id, stock), theme.Small);
            if (Button(640, 724, 600, 48, "Выбрать / " + stock.Ticker, ApplicationActive && day.CanSelectStock && !busy && !voice.Recording && !voice.Working, true))
            { stockNotice = day.SelectStock(stock.Id); News(stockNotice); }
            Label(180, 776, 720, 96, string.IsNullOrEmpty(stockNotice) ? day.StockSelectionNotice : stockNotice, theme.Small);
            if (Button(932, 808, 308, 48, "Вернуться к звонку / Esc")) CloseUtilityOverlay();
            Label(180, 872, 1080, 40, "Портфель/котировки не моделируются. " + (IsWorkMode || IsFounderMode
                ? "Проданная бумага попадёт в историю после завершения дня; вложение и комиссия выводятся из сделки."
                : "В этом режиме сохраняются сумма сделки и обязательства, не тикер."), theme.Small);
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
