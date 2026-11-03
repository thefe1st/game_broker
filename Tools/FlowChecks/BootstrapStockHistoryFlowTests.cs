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
            Assert.That(NotebookText(), Does.Contain("Проданная бумага попадёт в историю после завершения дня")); Click("Вернуться к звонку / Esc");
            SellChosen(Day, 0, "utility"); Click("База клиентов");
            Assert.That(NotebookText(), Does.Contain("Сегодня в списке: сделка закрыта: SVET $7500 (в историю попадёт после завершения дня)"));
            Assert.That(NotebookText(), Does.Contain("Покупок в рабочих днях ещё не было.")); Click("Вернуться к работе / Esc");
            Day.EndDay(); Frame(); Assert.That(NotebookText(), Does.Contain("Продано сегодня: Борис — SVET $7500. За рабочие дни: 1 дней, 1 сделок."));
            var saved = JsonUtility.FromJson<WorkCareerSave>(File.ReadAllText(Path.Combine(folder, "broker-employee-work-v3.json")));
            Assert.That(saved.schemaVersion, Is.EqualTo(3)); Assert.That(saved.reports[0].soldStockIds, Is.EqualTo(new[] { "utility", "", "" }));
            FreshGame(); Click("Продолжить рабочие дни"); Click("Начать рабочий день"); Interact(OfficeStation.Desk); Click("База клиентов");
            Assert.That(NotebookText(), Does.Contain("Последняя покупка, день 4: «Городской Свет» (SVET), вложение $7500, комиссия фирмы $150, лично вам $30."));
            Click("История рабочих дней"); string history = NotebookText();
            Assert.That(history, Does.Contain("Борис: сделка · «Городской Свет» (SVET), вложение $7500, комиссия фирмы $150, лично вам $30"));
            Assert.That(history, Does.Not.Contain("Бумаги записываются с дня")); Assert.That(history, Does.Contain("Вера: не звонили"));
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
            Assert.That(text, Does.Contain("Последняя покупка ещё в найме, день " + employed.lastStockDay)); Assert.That(text, Does.Not.Contain("лично вам"));
            Click("Вернуться к работе / Esc"); SellAny(Day, 0); Day.EndDay(); Frame();
            string stock = StockCatalog.Find(StockCatalog.DefaultFor(first)).Ticker;
            Assert.That(NotebookText(), Does.Contain("Продано сегодня: " + name + " — " + stock + " $" + StockHistory.Investment(first) + "."));
            Click("Следующий день конторы"); Click("Начать день своей конторы"); Interact(OfficeStation.Desk); Click("База клиентов");
            Assert.That(NotebookText(), Does.Contain("Последняя покупка, день " + opening + ": «"));
            Click("История своей конторы"); text = NotebookText();
            Assert.That(text, Does.Contain(name + ": сделка · «")); Assert.That(text, Does.Contain("комиссия фирмы $" + ClientCatalog.Create(first).Commission));
            Assert.That(text, Does.Not.Contain("лично вам")); Assert.That(text, Does.Not.Contain("Бумаги записываются с дня"));
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
