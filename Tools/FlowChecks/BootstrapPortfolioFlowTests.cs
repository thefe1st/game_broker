using System;
using System.IO;
using Broker.Core;
using Broker.Runtime.Office;
using NUnit.Framework;
using UnityEngine;

namespace Broker.FlowChecks
{
    // v5 portfolios on screen: rail, call card, stock screen, notebook and results repeat Core numbers; the UI itself changes nothing.
    public sealed partial class BootstrapFlowTests
    {
        private const string HonestPrice = "Цена упала — это риск акций. Гарантировать рост не могу.";
        private static int PriceTenths(string stockId, int buy, int day)
        { int price = StockMarket.PriceCents(stockId, buy); return (int)(((long)StockMarket.PriceCents(stockId, day) - price) * 1000 / price); }
        private static int NextVisit(string id, int after)
        { int day = after + 1; while (Array.IndexOf(ClientCatalog.Appointments(day), id) < 0) day++; return day; }
        // The fixed market: the first appointment from day 4 whose next appointment shows the wanted change of one purchase.
        private static int PortfolioBuyDay(string id, string stockId, Func<int, bool> wanted)
        {
            for (int buy = 4; buy < 4000; buy++)
                if (Array.IndexOf(ClientCatalog.Appointments(buy), id) >= 0 && wanted(PriceTenths(stockId, buy, NextVisit(id, buy)))) return buy;
            throw new InvalidOperationException("No such market case.");
        }
        private int SlotOf(string id) => Array.IndexOf(ClientCatalog.Appointments(Day.DayNumber), id);
        private void WorkDayNumber(int number) { while (Day.DayNumber < number) NextWorkDay(); Assert.That(Day.DayNumber, Is.EqualTo(number)); }
        // One work purchase, then the client's next appointment at the desk, call not started. Returns the change in tenths.
        private int PortfolioReaction(string id, string stockId, Func<int, bool> wanted)
        {
            StartWork(); int buy = PortfolioBuyDay(id, stockId, wanted); WorkDayNumber(buy); SellChosen(Day, SlotOf(id), stockId);
            WorkDayNumber(NextVisit(id, buy)); Interact(OfficeStation.Desk); Assert.That(Day.SelectClient(SlotOf(id)), Is.True);
            return PriceTenths(stockId, buy, Day.DayNumber);
        }
        private static bool Drop(int tenths) => tenths <= StockPortfolio.DropTenths && tenths > StockPortfolio.DeepDropTenths;

        [Test] public void PriceDropShowsOnRailCardAndNotebookAndHonestAnswerReachesResultsAndHistory()
        {
            int tenths = PortfolioReaction("inna", "medical", Drop); string change = StockMarket.Percent(tenths), name = ClientCatalog.Create("inna").Name;
            var inna = Day.Current; Assert.That(inna.Mood, Is.EqualTo(PortfolioMood.Drop)); Assert.That(inna.PriceConcern, Is.True);
            float seconds = Day.RemainingSeconds; int cash = Day.Cash; var files = Directory.GetFiles(folder); var bytes = Array.ConvertAll(files, File.ReadAllText);
            string desk = NotebookText();
            Assert.That(desk, Does.Contain("Падение " + change)); Assert.That(desk, Does.Contain("Портфель " + change)); Assert.That(desk, Does.Contain("Цена · —"));
            Click("База клиентов"); string notebook = NotebookText();
            Assert.That(notebook, Does.Contain("Портфель по ценам дня " + Day.DayNumber + " (1 из 3): MDAT, день "));
            Assert.That(notebook, Does.Contain("(" + change + "): падение — звонок начнётся с вопроса о цене."));
            Assert.That(notebook, Does.Contain("Сегодня в списке: ещё не звонили. Портфель " + change + ": падение цены ещё не объяснено. Сначала обсудите прошлый отчёт."));
            Click("Вернуться к работе / Esc");
            // Looking changes nothing: no clock, money or file writes.
            Assert.That(Day.RemainingSeconds, Is.EqualTo(seconds)); Assert.That(Day.Cash, Is.EqualTo(cash));
            for (int i = 0; i < files.Length; i++) Assert.That(File.ReadAllText(files[i]), Is.EqualTo(bytes[i]));
            Click("Позвонить"); desk = NotebookText();
            Assert.That(desk, Does.Contain("Мои акции MDAT упали на " + StockMarket.Percent(-tenths).TrimStart('+')));
            Assert.That(desk, Does.Contain("Объясни падение цены честно"));
            Set("message", HonestPrice); Click("Сказать");
            Assert.That(inna.PriceConcernResolved, Is.True); desk = NotebookText();
            Assert.That(desk, Does.Contain("Цена · OK")); Assert.That(desk, Does.Contain("Честно — уже лучше."));
            Day.EndDay(); Frame();
            Assert.That(NotebookText(), Does.Contain("Портфели: " + name + " — падение " + change + ", объяснили честно."));
            Click("Следующий рабочий день"); Click("Начать рабочий день"); Interact(OfficeStation.Desk); Click("История дней");
            Assert.That(NotebookText(), Does.Contain(name + ": звонок без сделки · падение цены объяснили честно"));
        }
        [Test] public void DeepDropLeavesOnlyModerateStocksOnScreenAndAnIgnoredClientCostsRelationship()
        {
            int tenths = PortfolioReaction("inna", "medical", t => t <= StockPortfolio.DeepDropTenths);
            string change = StockMarket.Percent(tenths), name = ClientCatalog.Create("inna").Name;
            Assert.That(Day.Current.Mood, Is.EqualTo(PortfolioMood.DeepDrop)); Assert.That(NotebookText(), Does.Contain("Сильное падение " + change));
            Click("Акции / " + Day.CurrentStock.Ticker); string text = NotebookText();
            Assert.That(text, Does.Contain("Предложение для «" + name + "» · портфель " + change));
            Click(Caption("medical"));
            Assert.That(NotebookText(), Does.Contain("После сильного падения портфеля клиент сегодня согласен только на умеренный риск: SVET или RLOG."));
            Click(Caption("utility")); Assert.That(NotebookText(), Does.Not.Contain("После сильного падения портфеля"));
            Click("Вернуться к звонку / Esc");
            var work = Get<WorkCareerState>("workCareer"); int before = work.Clients[ClientCatalog.Index("inna")].relationship;
            Day.EndDay(); Frame();
            Assert.That(NotebookText(), Does.Contain("Портфели: " + name + " — сильное падение " + change + ", не позвонили: отношения −1."));
            Assert.That(Get<WorkCareerState>("workCareer").Clients[ClientCatalog.Index("inna")].relationship, Is.EqualTo(before - 1));
        }
        [Test] public void GrowthShowsOnCardNotebookAndResultsWithoutPriceChip()
        {
            int tenths = PortfolioReaction("inna", "medical", t => t >= StockPortfolio.GrowthTenths);
            string change = StockMarket.Percent(tenths), name = ClientCatalog.Create("inna").Name;
            Assert.That(Day.Current.Mood, Is.EqualTo(PortfolioMood.Growth)); string desk = NotebookText();
            Assert.That(desk, Does.Contain("Портфель " + change)); Assert.That(desk, Does.Not.Contain("Цена · "));
            // The unfinished report outranks good news on the rail.
            Assert.That(desk, Does.Contain("Вернулся за отчётом"));
            Click("База клиентов"); Assert.That(NotebookText(), Does.Contain("Портфель " + change + ": рост, +1 доверия на звонок. Сначала обсудите прошлый отчёт."));
            Click("Вернуться к работе / Esc"); Day.EndDay(); Frame();
            Assert.That(NotebookText(), Does.Contain("Портфели: " + name + " — рост " + change + ", +1 доверия."));
        }
        [Test] public void V4WorkImportStartsPortfoliosNextDayFromTheLastPricedPurchase()
        {
            StartWork(); SellChosen(Day, 0, "utility"); Day.EndDay(); Frame();
            string v5 = Path.Combine(folder, "broker-employee-work-v5.json"), v4 = Path.Combine(folder, "broker-employee-work-v4.json");
            string legacy = LegacyWorkJson(File.ReadAllText(v5), 4); File.WriteAllText(v4, legacy); File.WriteAllText(v4 + ".bak", legacy);
            File.Delete(v5); if (File.Exists(v5 + ".bak")) File.Delete(v5 + ".bak");
            Assert.That(legacy, Does.Not.Contain("portfolio")); Assert.That(legacy, Does.Not.Contain("concernResolved"));
            FreshGame(); Assert.That(NotebookText(), Does.Contain("копия v4"));
            Click("Продолжить рабочие дни"); Click("Начать рабочий день"); Assert.That(Day.DayNumber, Is.EqualTo(5));
            Interact(OfficeStation.Desk); Click("База клиентов");
            Assert.That(NotebookText(), Does.Contain("Портфель по ценам дня 5 (1 из 3): SVET, день 4: 420 акций по $17,83 → сегодня $17,87 (+0,2%)."));
            Click("История рабочих дней"); string history = NotebookText();
            Assert.That(history, Does.Contain("Портфели и реакция на цену — с дня 5; из более ранних покупок в портфель перенесена только последняя с ценой."));
            Assert.That(history, Does.Not.Contain("Котировки и ставки по риску"));
            Click("Вернуться к работе / Esc"); Assert.That(File.Exists(v5), Is.False);
            Day.EndDay(); Frame(); Assert.That(File.Exists(v5), Is.True);
            var saved = JsonUtility.FromJson<WorkCareerSave>(File.ReadAllText(v5));
            Assert.That(saved.schemaVersion, Is.EqualTo(5)); Assert.That(saved.portfolioFromDay, Is.EqualTo(5));
            Assert.That(saved.reports[0].concernResolved, Is.EqualTo(new bool[3]));
            var boris = Get<WorkCareerState>("workCareer").Clients[ClientCatalog.Index("boris")].portfolio;
            Assert.That(boris.Length, Is.EqualTo(1)); Assert.That(boris[0].priceCents, Is.EqualTo(1783));
            Assert.That(File.ReadAllText(v4), Is.EqualTo(legacy)); Assert.That(File.ReadAllText(v4 + ".bak"), Is.EqualTo(legacy));
        }
        [Test] public void FounderNotebookMarksPortfolioBoughtWhileEmployed()
        {
            OpenFirm(); int opening = Get<FirmCareerState>("firmCareer").OpeningDayNumber;
            Interact(OfficeStation.Desk); Click("База клиентов"); string text = NotebookText();
            Assert.That(text, Does.Contain("Портфель по ценам дня " + opening + " (")); Assert.That(text, Does.Contain("(ещё в найме)"));
            Click("История своей конторы"); Assert.That(NotebookText(), Does.Not.Contain("Портфели и реакция на цену"));
        }
    }
}
