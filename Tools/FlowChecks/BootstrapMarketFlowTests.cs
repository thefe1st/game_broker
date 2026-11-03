using System;
using System.IO;
using Broker.Core;
using Broker.Runtime.Office;
using NUnit.Framework;
using UnityEngine;

namespace Broker.FlowChecks
{
    // v4 market: daily quotes, rate by risk and shares are visible, but only canonical numbers move money.
    public sealed partial class BootstrapFlowTests
    {
        private string Caption(string stockId)
        {
            var s = StockCatalog.Find(stockId); int today = Day.QuoteCents(stockId);
            return s.Ticker + " / " + s.Name + "\n" + s.RiskLabel + " · " + s.HorizonMonths + " мес.\n" + StockMarket.Money(today) + " · "
                + StockMarket.Change(StockMarket.PriceCents(stockId, Day.DayNumber - 1L), today) + " · комиссия " + s.FeePercent + "%";
        }
        private static void CloseAfterTerms(BrokerDay d)
        {
            d.OfferDeal(); d.Say(ClientConversation.AnswerIntent(d.Current.Id) == DialogueIntent.Reserve ? "Оставьте резерв на оборудование."
                : ClientConversation.AnswerIntent(d.Current.Id) == DialogueIntent.Written ? "Все условия в письменном договоре." : "Личный менеджер, буду на связи.");
            d.OfferDeal(); Assert.That(d.DealClosed, Is.True, d.Current.Id + ": " + d.NextConversationStep);
        }
        private void NextWorkDay() { Day.EndDay(); Frame(); Click("Следующий рабочий день"); Click("Начать рабочий день"); }
        [Test] public void WorkCatalogShowsQuotesChartRateAndSharesWithoutChangingMoneyOrClock()
        {
            StartWork(); Interact(OfficeStation.Desk); Click("Акции / RLOG"); float seconds = Day.RemainingSeconds; Frame();
            Assert.That(Day.RemainingSeconds, Is.EqualTo(seconds)); var files = Directory.GetFiles(folder); var bytes = Array.ConvertAll(files, File.ReadAllText);
            string text = NotebookText();
            Assert.That(text, Does.Contain("АКЦИИ / ВЫМЫШЛЕННЫЕ КОМПАНИИ / КОТИРОВКИ ДНЯ 4"));
            foreach (var s in StockCatalog.Offers) Assert.That(text, Does.Contain(Caption(s.Id)));
            Assert.That(text, Does.Contain("RLOG / Регион Логистика\\nУмеренный · 12 мес.\\n$25,56 · +2,4% · комиссия 2%".Replace("\\n", "\n")));
            Assert.That(text, Does.Contain("Склады и перевозки · цена дня $25,56 (+2,4% ко вчера)"));
            Assert.That(text, Does.Contain("Комиссия 2% — ставка умеренных бумаг; цена дня её не меняет."));
            Assert.That(text, Does.Contain("Вложение клиента: $7500 → 293 акции по $25,56; фирме $150, лично вам $30 (20%)."));
            int[] chart = StockMarket.Chart("logistics", 4); Assert.That(chart.Length, Is.EqualTo(7)); Assert.That(chart[6], Is.EqualTo(2556));
            Assert.That(text, Does.Contain("7 игровых дней по день 4: мин " + StockMarket.Money(Math.Min(chart[0], Min(chart))) + " · макс " + StockMarket.Money(Max(chart))));
            Click(Caption("medical")); text = NotebookText();
            Assert.That(text, Does.Contain("Комиссия 3% — ставка бумаг роста; цена дня её не меняет."));
            Assert.That(text, Does.Contain("Вложение клиента: $7500 → 214 акций по $34,89; фирме $225, лично вам $45 (20%)."));
            // Choosing is allowed, but the terms switch to 3% and the suitability warning stays visible.
            Click("Выбрать / MDAT"); Assert.That(Day.Current.StockId, Is.EqualTo("medical"));
            Assert.That(Day.CurrentFeePercent, Is.EqualTo(3)); Assert.That(Day.CurrentCommission, Is.EqualTo(225));
            Assert.That(NotebookText(), Does.Contain("Клиент не готов к такому риску"));
            Click("Вернуться к звонку / Esc"); Assert.That(Day.Cash, Is.Zero); Assert.That(Day.Earned, Is.Zero);
            CollectionAssert.AreEquivalent(files, Directory.GetFiles(folder));
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
        }
        private static int Min(int[] a) { int m = int.MaxValue; foreach (int v in a) m = Math.Min(m, v); return m; }
        private static int Max(int[] a) { int m = 0; foreach (int v in a) m = Math.Max(m, v); return m; }
        [Test] public void GrowthStockPaysThreePercentOnlyAfterTheRightRateIsNamed()
        {
            StartWork(); NextWorkDay(); Assert.That(Day.DayNumber, Is.EqualTo(5)); Assert.That(Day.Clients[0].Id, Is.EqualTo("inna"));
            Interact(OfficeStation.Desk); Click("Акции / MDAT");
            Assert.That(NotebookText(), Does.Contain("Вложение клиента: $11000 → 327 акций по $33,58; фирме $330, лично вам $66 (20%)."));
            Click("Вернуться к звонку / Esc"); Assert.That(Day.SelectClient(0), Is.True); Day.StartCall();
            Day.Say("Какая цель? Есть риск убытка. Комиссия 2%. Срок год.");
            Assert.That(Day.FeesExplained, Is.False); Assert.That(Day.NextConversationStep, Does.Contain("3%"));
            Assert.That(Day.StockTermsText, Does.Contain("Комиссия 3%: $330 от вложения $11000. Цена сегодня $33,58 за акцию: на вложение — 327 акций."));
            Day.Say("Комиссия 3%."); Assert.That(Day.FeesExplained, Is.True); CloseAfterTerms(Day);
            Day.EndDay(); Frame();
            Assert.That(NotebookText(), Does.Contain("Продано сегодня: Инна — MDAT $11000 (327 акций по $33,58, комиссия 3%)."));
            var work = Get<WorkCareerState>("workCareer"); Assert.That(work.PersonalSavings, Is.EqualTo(66));
            Assert.That(work.LastReport.soldPrices, Is.EqualTo(new[] { 3358, 0, 0 })); Assert.That(work.Clients[ClientCatalog.Index("inna")].dealsAt3, Is.EqualTo(1));
            FreshGame(); Click("Продолжить рабочие дни"); Assert.That(Get<WorkCareerState>("workCareer").PersonalSavings, Is.EqualTo(66));
        }
        [Test] public void ReturningClientReportComparesPurchasePriceWithTodayWithoutMoney()
        {
            StartWork(); Sell(Day, 0); NextWorkDay(); NextWorkDay(); NextWorkDay();
            Assert.That(Day.DayNumber, Is.EqualTo(7)); Assert.That(Day.Clients[0].Id, Is.EqualTo("boris")); int cash = Day.Cash;
            Assert.That(Day.SelectClient(0), Is.True); Day.StartCall(); string answer = Day.Say("Обсудим отчёт по прошлому разговору.");
            // RLOG: $25,56 on day 4, $27,33 on day 7. Information only: no payout or trust change from the quote.
            Assert.That(answer, Does.Contain("RLOG: куплены в день 4 по $25,56, сегодня $27,33 (+6,9%). Цена не обещание."));
            Assert.That(Day.Cash, Is.EqualTo(cash)); Assert.That(Day.Earned, Is.Zero);
        }
        [Test] public void FounderSaleUsesStockRateAndWholeCommissionGoesToCash()
        {
            OpenFirm(); Func<string, bool> growth = id => StockCatalog.Fits(id, StockCatalog.Find("medical"));
            while (Array.FindIndex(ClientCatalog.Appointments(Day.DayNumber), id => growth(id)) < 0)
            { Day.EndDay(); Frame(); Click("Следующий день конторы"); Click("Начать день своей конторы"); }
            int i = Array.FindIndex(ClientCatalog.Appointments(Day.DayNumber), id => growth(id)); string client = ClientCatalog.Appointments(Day.DayNumber)[i];
            Interact(OfficeStation.Desk); Assert.That(Day.SelectClient(i), Is.True); Click("Акции / " + Day.CurrentStock.Ticker); Click(Caption("medical"));
            int price = Day.QuoteCents("medical"), commission = StockMarket.Commission(client, "medical");
            Assert.That(NotebookText(), Does.Contain("фирме $" + commission + ". Вся комиссия в кассу.")); Assert.That(NotebookText(), Does.Not.Contain("лично вам"));
            Click("Выбрать / MDAT"); Click("Вернуться к звонку / Esc"); Assert.That(Day.Current.StockId, Is.EqualTo("medical"));
            int cash = Day.Cash; SellAny(Day, i); Assert.That(Day.Earned, Is.EqualTo(commission)); Assert.That(Day.Cash, Is.EqualTo(cash + commission));
            Assert.That(commission, Is.EqualTo(StockHistory.Investment(client) * 3 / 100));
            Day.EndDay(); Frame();
            Assert.That(NotebookText(), Does.Contain(ClientCatalog.Create(client).Name + " — " + StockHistory.Short(client, "medical", price)));
            Assert.That(Get<FirmCareerState>("firmCareer").LastReport.soldPrices[i], Is.EqualTo(price));
        }
        [Test] public void IntroCatalogKeepsFixedTwoPercentWithoutPrices()
        {
            Begin(); Interact(OfficeStation.Desk); Click("Акции / " + Day.CurrentStock.Ticker); string text = NotebookText();
            Assert.That(text, Does.Contain("АКЦИИ / ВЫМЫШЛЕННЫЕ КОМПАНИИ")); Assert.That(text, Does.Not.Contain("КОТИРОВКИ ДНЯ"));
            Assert.That(text, Does.Contain("Комиссия 2%: в этом режиме ставка одна, цены не моделируются."));
            Assert.That(text, Does.Contain("Котировки есть только в рабочих днях и своей конторе.")); Assert.That(text, Does.Not.Contain("7 игровых дней"));
            Assert.That(Day.HasQuotes, Is.False); Assert.That(Day.FeePercentFor("biotech"), Is.EqualTo(2)); Assert.That(Day.StockTermsText, Does.Not.Contain("Цена сегодня"));
        }
        [Test] public void V3WorkImportKeepsStockHistoryAndStartsQuotesNextDay()
        {
            StartWork(); Sell(Day, 0); Day.EndDay(); Frame();
            string v5 = Path.Combine(folder, "broker-employee-work-v5.json"), v3 = Path.Combine(folder, "broker-employee-work-v3.json");
            string legacy = LegacyWorkJson(File.ReadAllText(v5), 3); File.WriteAllText(v3, legacy); File.WriteAllText(v3 + ".bak", legacy);
            File.Delete(v5); if (File.Exists(v5 + ".bak")) File.Delete(v5 + ".bak");
            FreshGame(); Assert.That(NotebookText(), Does.Contain("копия v3"));
            Click("Продолжить рабочие дни"); Click("Начать рабочий день"); Assert.That(Day.DayNumber, Is.EqualTo(5)); Assert.That(Day.Cash, Is.EqualTo(30));
            Interact(OfficeStation.Desk); Click("База клиентов");
            Assert.That(NotebookText(), Does.Contain("Последняя покупка, день 4: «Регион Логистика» (RLOG), вложение $7500, комиссия фирмы $150, лично вам $30. Куплено до котировок; сегодня $25,67."));
            // A purchase without a recorded price never enters the portfolio: no invented reaction.
            Assert.That(NotebookText(), Does.Contain("Портфель: покупок с котировкой нет; бумаги до котировок в портфель не входят, цена не реагирует."));
            Click("История рабочих дней"); string history = NotebookText();
            Assert.That(history, Does.Contain("Борис: сделка · «Регион Логистика» (RLOG), вложение $7500, комиссия фирмы $150, лично вам $30"));
            Assert.That(history, Does.Contain("Котировки и ставки по риску — с дня 5; раньше комиссия 2% без цены.")); Assert.That(history, Does.Not.Contain("Бумаги записываются с дня"));
            Assert.That(history, Does.Contain("Портфели и реакция на цену — с дня 5; из более ранних покупок в портфель перенесена только последняя с ценой."));
            Click("Вернуться к работе / Esc"); Assert.That(File.Exists(v5), Is.False);
            SellAny(Day, 1); Day.EndDay(); Frame(); Assert.That(File.Exists(v5), Is.True);
            Assert.That(NotebookText(), Does.Contain("Продано сегодня: Оксана — OBLK $9500 (173 акции по $54,73, комиссия 3%)."));
            var saved = JsonUtility.FromJson<WorkCareerSave>(File.ReadAllText(v5));
            Assert.That(saved.schemaVersion, Is.EqualTo(5)); Assert.That(saved.marketFromDay, Is.EqualTo(5)); Assert.That(saved.portfolioFromDay, Is.EqualTo(5));
            Assert.That(saved.reports[1].soldPrices, Is.EqualTo(new[] { 0, 5473, 0 }));
            var clients = Get<WorkCareerState>("workCareer").Clients;
            Assert.That(clients[ClientCatalog.Index("boris")].portfolio, Is.Empty);
            Assert.That(clients[ClientCatalog.Index("oksana")].portfolio.Length, Is.EqualTo(1));
            Assert.That(clients[ClientCatalog.Index("oksana")].portfolio[0].priceCents, Is.EqualTo(5473));
            Assert.That(saved.reports[0].soldPrices, Is.EqualTo(new[] { 0, 0, 0 })); Assert.That(saved.reports[0].soldStockIds[0], Is.EqualTo("logistics"));
            Assert.That(File.ReadAllText(v3), Is.EqualTo(legacy)); Assert.That(File.ReadAllText(v3 + ".bak"), Is.EqualTo(legacy));
        }
    }
}
