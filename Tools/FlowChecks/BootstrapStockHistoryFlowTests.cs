using System.IO;
using Broker.Core;
using Broker.Runtime.Office;
using NUnit.Framework;
using UnityEngine;

namespace Broker.FlowChecks
{
    public sealed partial class BootstrapFlowTests
    {
        private static void SellChosen(BrokerDay d, int i, string stockId)
        {
            Assert.That(d.SelectClient(i), Is.True); d.SelectStock(stockId); Assert.That(d.Current.StockId, Is.EqualTo(stockId));
            SellAny(d, i);
        }
        [Test] public void WorkResultsAndNotebookShowChosenStockAfterReload()
        {
            StartWork(); Interact(OfficeStation.Desk); Click("Акции / RLOG");
            Assert.That(NotebookText(), Does.Contain("Проданная бумага после завершения дня попадёт в историю и портфель клиента по цене покупки: до трёх последних покупок, см. «База клиентов».")); Click("Вернуться к звонку / Esc");
            SellChosen(Day, 0, "utility"); Click("База клиентов");
            // SVET on day 4 is $17,83: Борис's $7500 buys 420 shares; the moderate rate stays 2%.
            Assert.That(NotebookText(), Does.Contain("Сегодня в списке: сделка закрыта: SVET $7500 (420 акций по $17,83, комиссия 2%); в историю и портфель попадёт после завершения дня."));
            Assert.That(NotebookText(), Does.Contain("Покупок в рабочих днях ещё не было."));
            Assert.That(NotebookText(), Does.Contain("Портфель пуст: после первой покупки цена будет влиять на настроение клиента.")); Click("Вернуться к работе / Esc");
            Day.EndDay(); Frame();
            Assert.That(NotebookText(), Does.Contain("Продано сегодня: Борис — SVET $7500 (420 акций по $17,83, комиссия 2%). За рабочие дни: 1 дней, 1 сделок."));
            var saved = JsonUtility.FromJson<WorkCareerSave>(File.ReadAllText(Path.Combine(folder, "broker-employee-work-v5.json")));
            Assert.That(saved.schemaVersion, Is.EqualTo(5)); Assert.That(saved.marketFromDay, Is.EqualTo(4)); Assert.That(saved.portfolioFromDay, Is.EqualTo(4));
            Assert.That(saved.reports[0].soldStockIds, Is.EqualTo(new[] { "utility", "", "" })); Assert.That(saved.reports[0].soldPrices, Is.EqualTo(new[] { 1783, 0, 0 }));
            var position = Get<WorkCareerState>("workCareer").Clients[ClientCatalog.Index("boris")].portfolio;
            Assert.That(position.Length, Is.EqualTo(1)); Assert.That(position[0].stockId + " " + position[0].day + " " + position[0].priceCents, Is.EqualTo("utility 4 1783"));
            FreshGame(); Click("Продолжить рабочие дни"); Click("Начать рабочий день"); Interact(OfficeStation.Desk); Click("База клиентов");
            Assert.That(NotebookText(), Does.Contain("Последняя покупка, день 4: «Городской Свет» (SVET): 420 акций по $17,83, вложение $7500, комиссия 2% — фирме $150, лично вам $30."
                + " Сегодня $17,87 (+0,2% к цене покупки). Цена не обещание."));
            // 420 × $17,83 = $7488,60 → 420 × $17,87 = $7505,40: calm, no reaction.
            Assert.That(NotebookText(), Does.Contain("Портфель по ценам дня 5 (1 из 3): SVET, день 4: 420 акций по $17,83 → сегодня $17,87 (+0,2%). Всего $7488,60 → $7505,40 (+0,2%): спокойно — в пределах ±5%."));
            Click("История рабочих дней"); string history = NotebookText();
            Assert.That(history, Does.Contain("Борис: сделка · «Городской Свет» (SVET): 420 акций по $17,83, вложение $7500, комиссия 2% — фирме $150, лично вам $30"));
            Assert.That(history, Does.Not.Contain("Бумаги записываются с дня")); Assert.That(history, Does.Not.Contain("Котировки и ставки по риску"));
            Assert.That(history, Does.Not.Contain("Портфели и реакция на цену")); Assert.That(history, Does.Contain("Вера: не звонили"));
        }
        [Test] public void WorkResultsWithoutSalesSayNoDeals()
        {
            StartWork(); Day.EndDay(); Frame(); Assert.That(NotebookText(), Does.Contain("Сегодня сделок нет. За рабочие дни: 1 дней, 0 сделок."));
        }
        [Test] public void FounderShowsStillEmployedPurchaseThenOwnSaleInResultsAndHistory()
        {
            OpenFirm(); var firm = Get<FirmCareerState>("firmCareer"); int opening = firm.OpeningDayNumber;
            string first = Day.Clients[0].Id, name = ClientCatalog.Create(first).Name; var employed = firm.Clients[ClientCatalog.Index(first)];
            Assert.That(employed.lastStockDay, Is.GreaterThan(0)); Interact(OfficeStation.Desk); Click("База клиентов"); string text = NotebookText();
            // The carried purchase keeps its exact quote; today's price is shown for comparison only.
            Assert.That(employed.lastStockPrice, Is.EqualTo(StockMarket.PriceCents(employed.lastStockId, employed.lastStockDay)));
            Assert.That(text, Does.Contain("Последняя покупка ещё в найме, день " + employed.lastStockDay));
            Assert.That(text, Does.Contain(StockMarket.SharesText(StockMarket.Shares(first, employed.lastStockPrice)) + " по " + StockMarket.Money(employed.lastStockPrice)
                + ". Комиссия тогда ушла прежней фирме." + StockHistory.TodayQuote(employed.lastStockId, employed.lastStockPrice, opening)));
            Assert.That(text, Does.Not.Contain("лично вам"));
            Click("Вернуться к работе / Esc"); SellAny(Day, 0); Day.EndDay(); Frame();
            string stockId = StockCatalog.DefaultFor(first); int price = StockMarket.PriceCents(stockId, opening);
            Assert.That(NotebookText(), Does.Contain("Продано сегодня: " + name + " — " + StockHistory.Short(first, stockId, price) + "."));
            Assert.That(Get<FirmCareerState>("firmCareer").LastReport.soldPrices[0], Is.EqualTo(price));
            Click("Следующий день конторы"); Click("Начать день своей конторы"); Interact(OfficeStation.Desk); Click("База клиентов");
            Assert.That(NotebookText(), Does.Contain("Последняя покупка, день " + opening + ": " + StockHistory.Describe(first, stockId, false, price) + "."));
            Click("История своей конторы"); text = NotebookText();
            Assert.That(text, Does.Contain(name + ": сделка · " + StockHistory.Describe(first, stockId, false, price)));
            Assert.That(text, Does.Contain("комиссия " + StockMarket.FeePercent(stockId) + "% — фирме $" + StockMarket.Commission(first, stockId)));
            Assert.That(text, Does.Not.Contain("лично вам")); Assert.That(text, Does.Not.Contain("Бумаги записываются с дня"));
            Assert.That(text, Does.Not.Contain("Котировки и ставки по риску"));
        }
        [Test] public void LegacyV2FounderImportShowsPreHistoryDealsAndRecordsNextDay()
        {
            OpenFirm(); SellAny(Day, 0); Day.EndDay(); Frame();
            var s = LegacyFirm(Get<FirmCareerState>("firmCareer").Capture(), 2); string first = s.reports[0].clientIds[0];
            File.Delete(FirmPath); if (File.Exists(FirmPath + ".bak")) File.Delete(FirmPath + ".bak");
            string v2 = Path.Combine(folder, "broker-founder-v2.json"), bytes = JsonUtility.ToJson(s); File.WriteAllText(v2, bytes);
            FreshGame(); Click("Продолжить свою контору"); Assert.That(NotebookText(), Does.Contain("копия конторы v2"));
            Click("Начать день своей конторы"); Interact(OfficeStation.Desk); Click("База клиентов");
            Assert.That(NotebookText(), Does.Contain("Последняя покупка — до истории акций")); Click("История своей конторы");
            Assert.That(NotebookText(), Does.Contain(ClientCatalog.Create(first).Name + ": сделка · до истории акций"));
            Assert.That(NotebookText(), Does.Contain("Бумаги записываются с дня " + (s.sourceDay + 1)));
            Click("Вернуться к работе / Esc"); Assert.That(File.Exists(FirmPath), Is.False);
            SellAny(Day, 0); Day.EndDay(); Frame(); Assert.That(File.Exists(FirmPath), Is.True); Assert.That(NotebookText(), Does.Contain("Продано сегодня: "));
            Assert.That(JsonUtility.FromJson<FirmCareerSave>(File.ReadAllText(FirmPath)).reports[1].soldStockIds[0], Is.Not.Empty);
            Assert.That(File.ReadAllText(v2), Is.EqualTo(bytes));
        }
    }
}
