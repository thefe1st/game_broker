using System.IO;
using System.Linq;
using Broker.Core;
using Broker.Runtime.Office;
using NUnit.Framework;
using UnityEngine;

namespace Broker.FlowChecks
{
    public sealed partial class BootstrapFlowTests
    {
        private string[] TrustAndStocks() => Day.Clients.Select(c => c.Trust + ":" + c.StockId).ToArray();
        [TestCase(OfficeStation.CityExchange, OfficeView.CityExchange)] [TestCase(OfficeStation.CityCafe, OfficeView.CityCafe)]
        public void ExchangeAndCafeOpenOnlyOnStreetStopClockAndGrantNothing(OfficeStation station, OfficeView view)
        {
            Begin(); Interact(station); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Interact(OfficeStation.Street); Interact(station); Assert.That(Get<OfficeNavigation>("navigation").View, Is.EqualTo(view));
            float seconds = Day.RemainingSeconds; var before = TrustAndStocks(); var files = Directory.GetFiles(folder).Select(File.ReadAllText).ToArray();
            Frame(); Frame();
            Assert.That(Day.RemainingSeconds, Is.EqualTo(seconds)); Assert.That(Day.Cash, Is.Zero);
            Assert.That(Day.RisksExplained || Day.FeesExplained, Is.False); Assert.That(TrustAndStocks(), Is.EqualTo(before));
            Assert.That(Get<OfficeScene>("office").WorldMotionEnabled, Is.False); Assert.That(UnityEngine.Networking.UnityWebRequest.Sent, Is.Zero);
            Assert.That(Directory.GetFiles(folder).Select(File.ReadAllText).ToArray(), Is.EqualTo(files));
            OfficeInput.Escape = true; Call("Update"); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Frame(); Assert.That(Day.RemainingSeconds, Is.LessThan(seconds)); Assert.That(Get<OfficeScene>("office").WorldMotionEnabled, Is.True);
        }
        [Test] public void ExchangeBoardIsClosedInTheIntroChapter()
        {
            Begin(); Interact(OfficeStation.Street); Interact(OfficeStation.CityExchange); string text = NotebookText();
            Assert.That(Day.HasQuotes, Is.False); Assert.That(text, Does.Contain(ExchangeBoard.ClosedTitle).And.Contain("цены не моделируются"));
            foreach (var stock in StockCatalog.Offers) Assert.That(text, Does.Not.Contain(stock.Ticker + " / "));
            Click("Назад · Esc"); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
        }
        [Test] public void WorkDayExchangeListsEveryTickerWithPriceChangeAndFee()
        {
            StartWork(); Interact(OfficeStation.Street); Interact(OfficeStation.CityExchange);
            string text = NotebookText(); var rows = ExchangeBoard.Rows(Day); var before = TrustAndStocks();
            Assert.That(Day.HasQuotes, Is.True); Assert.That(rows.Count, Is.EqualTo(StockCatalog.Offers.Length));
            Assert.That(text, Does.Contain(ExchangeBoard.Title(Day))); Assert.That(text, Does.Not.Contain(ExchangeBoard.ClosedTitle));
            for (int i = 0; i < rows.Count; i++)
            {
                var stock = StockCatalog.Offers[i];
                Assert.That(text, Does.Contain(rows[i].Ticker + " / " + rows[i].Name)); Assert.That(text, Does.Contain(rows[i].Price));
                Assert.That(text, Does.Contain(rows[i].Change)); Assert.That(text, Does.Contain(stock.FeePercent + "% · " + stock.HorizonMonths + " мес."));
                Assert.That(rows[i].PriceCents, Is.EqualTo(Day.QuoteCents(stock.Id)));
            }
            OfficeInput.Escape = true; Call("Update"); Assert.That(TrustAndStocks(), Is.EqualTo(before));
        }
        [Test] public void CafeShowsThreeLinesOfTheDayWithoutTickersOrPurchases()
        {
            Begin(); Interact(OfficeStation.Street); Interact(OfficeStation.CityCafe);
            string text = NotebookText(); var lines = CityChatter.For(Day.DayNumber);
            Assert.That(lines.Distinct().Count(), Is.EqualTo(3));
            foreach (var line in lines) Assert.That(text, Does.Contain(line));
            foreach (var stock in StockCatalog.Offers) Assert.That(text, Does.Not.Contain(stock.Ticker));
            GUI.Click = "Купить"; Call("OnGUI"); Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null;
            Click("Назад · Esc"); Assert.That(Day.Cash, Is.Zero);
        }
        [Test] public void CrosswalkButtonOnlyAsksTheSignalOnTheStreetAndOpensNoPanel()
        {
            Begin(); var office = Get<OfficeScene>("office");
            Interact(OfficeStation.CrosswalkButton); Assert.That(office.CrossingRequests, Is.Zero);
            Interact(OfficeStation.Street); float seconds = Day.RemainingSeconds;
            Interact(OfficeStation.CrosswalkButton);
            Assert.That(office.CrossingRequests, Is.EqualTo(1)); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
            Frame(); Assert.That(Day.RemainingSeconds, Is.LessThan(seconds)); Assert.That(Day.Cash, Is.Zero);
            Call("PauseGameplay"); Interact(OfficeStation.CrosswalkButton); Assert.That(office.CrossingRequests, Is.EqualTo(1));
        }
        [Test] public void CrossingCueFollowsTheSignalOnlyInTheCity()
        {
            Begin(); var office = Get<OfficeScene>("office"); office.CrossingSignal = new CrosswalkSignal();
            Assert.That(Pictograms(), Does.Not.Contain("Standing"));
            Interact(OfficeStation.Street); var pictures = Pictograms();
            // The cars have their full green and nobody has asked: the red man and the button on the pole.
            Assert.That(pictures, Does.Contain("Standing")); Assert.That(pictures, Does.Contain("Button"));
            Assert.That(pictures, Does.Not.Contain("Hourglass"));
            // Asked: the hourglass and the seconds until the green man.
            office.CrossingSignal.RequestWalk(); pictures = Pictograms();
            Assert.That(pictures, Does.Contain("Hourglass")); Assert.That(pictures, Does.Not.Contain("Button"));
            Assert.That(GUI.Labels.Any(label => label.EndsWith(" с")), Is.True);
            Assert.That(string.Join("\n", GUI.Labels), Does.Not.Contain("ПЕРЕХОД / СВЕТОФОР"));
            office.CrossingSignal = null; Assert.That(Pictograms(), Does.Not.Contain("Standing"));
        }
        [Test] public void MapOffersEveryLandmarkIncludingExchangeCafeAndCrossing()
        {
            Begin(); Interact(OfficeStation.Street); Interact(OfficeStation.CityMap);
            Assert.That(CityDistrictPlan.Destinations.Select(d => d.Id), Is.SupersetOf(new[] { "exchange", "cafe", "crossing" }));
            foreach (var destination in CityDistrictPlan.Destinations)
            {
                Click("Ориентир / " + destination.Name); Assert.That(Get<string>("cityDestinationId"), Is.EqualTo(destination.Id));
                string text = NotebookText(); Assert.That(text, Does.Contain(destination.Description)); Assert.That(text, Does.Contain("От вас:"));
            }
            Click("Назад · Esc"); Assert.That(Get<OfficeNavigation>("navigation").IsExploring, Is.True);
        }
    }
}
