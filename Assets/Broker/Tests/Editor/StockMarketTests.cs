using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    // v4 market: deterministic fictional quotes, fee by risk, whole shares. Public Core API only.
    public sealed class StockMarketTests
    {
        // Golden quotes: a silent formula change would rewrite every saved purchase price.
        [TestCase("utility", 4, 1783)] [TestCase("utility", 12, 1907)]
        [TestCase("logistics", 4, 2556)] [TestCase("logistics", 7, 2733)]
        [TestCase("medical", 4, 3489)] [TestCase("medical", 5, 3358)]
        [TestCase("cloud", 5, 5473)] [TestCase("cloud", 12, 6494)]
        [TestCase("ore", 4, 164)] [TestCase("biotech", 4, 447)]
        public void QuotesAreDeterministicGoldenValues(string id, int day, int cents)
        { Assert.That(StockMarket.PriceCents(id, day), Is.EqualTo(cents)); Assert.That(StockMarket.Chart(id, day)[StockMarket.ChartDays - 1], Is.EqualTo(cents)); }
        [Test] public void QuotesMoveButStayWithinPublishedBoundsForYears()
        {
            foreach (var s in StockCatalog.Offers)
            {
                int min = StockMarket.MinPriceCents(s.Id), max = StockMarket.MaxPriceCents(s.Id), low = int.MaxValue, high = 0, changes = 0;
                Assert.That(min, Is.GreaterThan(0)); Assert.That(max, Is.GreaterThan(min));
                for (long d = -400; d <= 40000; d++)
                {
                    int p = StockMarket.PriceCents(s.Id, d); low = Math.Min(low, p); high = Math.Max(high, p);
                    if (p != StockMarket.PriceCents(s.Id, d - 1)) changes++;
                }
                Assert.That(low, Is.GreaterThanOrEqualTo(min), s.Ticker); Assert.That(high, Is.LessThanOrEqualTo(max), s.Ticker);
                Assert.That(changes, Is.GreaterThan(30000), s.Ticker); Assert.That(high - low, Is.GreaterThan((max - min) / 2), s.Ticker);
                Assert.That(StockMarket.PriceCents(s.Id, int.MaxValue), Is.InRange(min, max)); Assert.That(StockMarket.PriceCents(s.Id, int.MinValue), Is.InRange(min, max));
            }
            Assert.Throws<ArgumentException>(() => StockMarket.PriceCents("unknown", 4));
        }
        [Test] public void FeeFollowsRiskAndCommissionIsWholeDollarsOfTheFixedInvestment()
        {
            Assert.That(Array.ConvertAll(new[] { "utility", "logistics", "medical", "cloud", "ore", "biotech", "unknown" }, StockMarket.FeePercent),
                Is.EqualTo(new[] { 2, 2, 3, 3, 5, 5, 0 }));
            foreach (string id in ClientCatalog.Ids)
            {
                var c = ClientCatalog.Create(id); Assert.That(StockMarket.Investment(id), Is.EqualTo(StockHistory.Investment(id)));
                Assert.That(StockMarket.CommissionAt(id, 2), Is.EqualTo(c.Commission)); Assert.That(StockMarket.Commission(id, "logistics"), Is.EqualTo(c.Commission));
                Assert.That(StockMarket.Commission(id, "medical"), Is.EqualTo(StockMarket.Investment(id) * 3 / 100));
            }
            Assert.That(StockMarket.Commission("inna", "medical"), Is.EqualTo(330)); Assert.That(StockMarket.Commission("artem", "ore"), Is.EqualTo(450));
            Assert.That(StockMarket.Commission("oksana", "cloud"), Is.EqualTo(285)); Assert.That(StockMarket.Commission("boris", "unknown"), Is.Zero);
        }
        [TestCase("boris", 150, false, false)] [TestCase("vera", 200, false, false)] [TestCase("denis", 170, false, false)]
        [TestCase("inna", 330, true, false)] [TestCase("oksana", 285, true, false)] [TestCase("artem", 450, true, true)]
        public void RatesFollowClientLimits(string id, int max, bool three, bool five)
        {
            Assert.That(StockMarket.MaxCommission(id), Is.EqualTo(max)); Assert.That(StockMarket.CanPay(id, 2), Is.True);
            Assert.That(StockMarket.CanPay(id, 3), Is.EqualTo(three)); Assert.That(StockMarket.CanPay(id, 5), Is.EqualTo(five));
            Assert.That(StockMarket.CanPay(id, 4), Is.False); Assert.That(StockMarket.CanPay(id, 0), Is.False);
        }
        [TestCase(0, "0 акций")] [TestCase(1, "1 акция")] [TestCase(2, "2 акции")] [TestCase(4, "4 акции")] [TestCase(5, "5 акций")]
        [TestCase(11, "11 акций")] [TestCase(12, "12 акций")] [TestCase(14, "14 акций")] [TestCase(21, "21 акция")] [TestCase(22, "22 акции")]
        [TestCase(111, "111 акций")] [TestCase(173, "173 акции")] [TestCase(214, "214 акций")] [TestCase(293, "293 акции")] [TestCase(420, "420 акций")]
        public void SharesTextUsesRussianPlurals(int shares, string text) { Assert.That(StockMarket.SharesText(shares), Is.EqualTo(text)); }
        [Test] public void SharesAreWholeAndMoneyAndChangeUseCommaFormat()
        {
            Assert.That(StockMarket.Shares("boris", 1783), Is.EqualTo(420)); Assert.That(StockMarket.Shares("inna", 3358), Is.EqualTo(327));
            Assert.That(StockMarket.Shares("boris", 0), Is.Zero); Assert.That(StockMarket.Shares("boris", 750001), Is.Zero);
            Assert.That(StockMarket.ValidPurchasePrice("boris", 750000), Is.True); Assert.That(StockMarket.ValidPurchasePrice("boris", 750001), Is.False);
            Assert.That(StockMarket.ValidPurchasePrice("boris", 0), Is.False); Assert.That(StockMarket.ValidPurchasePrice("boris", -5), Is.False);
            Assert.That(StockMarket.Money(1783), Is.EqualTo("$17,83")); Assert.That(StockMarket.Money(5), Is.EqualTo("$0,05"));
            Assert.That(StockMarket.Money(0), Is.EqualTo("$0,00")); Assert.That(StockMarket.Money(-250), Is.EqualTo("−$2,50"));
            Assert.That(StockMarket.Change(2556, 2733), Is.EqualTo("+6,9%")); Assert.That(StockMarket.Change(1783, 1773), Is.EqualTo("−0,5%"));
            Assert.That(StockMarket.Change(3, 2), Is.EqualTo("−33,3%")); Assert.That(StockMarket.Change(100, 100), Is.EqualTo("0,0%"));
            Assert.That(StockMarket.Change(0, 100), Is.EqualTo("0,0%"));
        }
        [Test] public void ChartIsSevenDaysOldestFirstEndingWithTheDay()
        {
            var chart = StockMarket.Chart("logistics", 7); Assert.That(chart.Length, Is.EqualTo(7));
            for (int i = 0; i < 7; i++) Assert.That(chart[i], Is.EqualTo(StockMarket.PriceCents("logistics", 1 + i)));
            Assert.That(chart[3], Is.EqualTo(2556)); Assert.That(chart[6], Is.EqualTo(2733));
        }
        private static WorkCareerState WorkAt(int day)
        {
            var c = WorkCareerState.FromIntro(WorkCareerTests.Intro(true));
            while (c.NextDayNumber < day) { var d = c.CreateDay(); d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); }
            return c;
        }
        [Test] public void WorkDayAsksForTheSelectedRateAndDoesNotAcceptTheOldTwoPercent()
        {
            var d = WorkAt(5).CreateDay(); Assert.That(d.DayNumber, Is.EqualTo(5)); Assert.That(d.SelectClient(0), Is.True); Assert.That(d.Current.Id, Is.EqualTo("inna"));
            Assert.That(d.HasQuotes, Is.True); Assert.That(d.CurrentStock.Id, Is.EqualTo("medical")); Assert.That(d.CurrentFeePercent, Is.EqualTo(3));
            Assert.That(d.CurrentCommission, Is.EqualTo(330)); Assert.That(d.CurrentQuoteCents, Is.EqualTo(3358));
            Assert.That(d.StockTermsText, Does.Contain("Комиссия 3%: $330 от вложения $11000. Цена сегодня $33,58 за акцию: на вложение — 327 акций. Цена может упасть."));
            d.StartCall(); d.Say("Обсудим отчёт по прошлому разговору. Какая цель? Есть риск убытка. Комиссия 2%.");
            Assert.That(d.FeesExplained, Is.False); Assert.That(d.NextConversationStep, Does.Contain("назови комиссию 3%"));
            string reply = d.Say("Комиссия три процента."); Assert.That(d.FeesExplained, Is.True);
            Assert.That(reply, Does.Contain("Комиссия 3% — выше, чем у простых бумаг"));
            d.SelectStock("utility"); Assert.That(d.FeesExplained, Is.False); Assert.That(d.CurrentFeePercent, Is.EqualTo(2)); Assert.That(d.CurrentCommission, Is.EqualTo(220));
            d.Say("Комиссия 2%."); Assert.That(d.FeesExplained, Is.True);
        }
        [Test] public void SpeculativeSaleBooksFivePercentSharesAndPriceIntoTheCareer()
        {
            var c = WorkAt(6); var d = c.CreateDay(); Assert.That(d.Clients[0].Id, Is.EqualTo("artem")); int before = c.PersonalSavings;
            StockHistoryTests.SellStock(d, 0, "ore"); Assert.That(d.Current.SoldPriceCents, Is.EqualTo(StockMarket.PriceCents("ore", 6)));
            Assert.That(d.Earned, Is.EqualTo(90)); Assert.That(d.FirmRevenue, Is.EqualTo(450));
            d.EndDay(); Assert.That(c.CompleteDay(d), Is.True);
            Assert.That(c.PersonalSavings, Is.EqualTo(before + 90)); Assert.That(c.LastReport.soldPrices[0], Is.EqualTo(175));
            var artem = c.Clients[ClientCatalog.Index("artem")]; Assert.That(artem.dealsAt5, Is.EqualTo(1)); Assert.That(artem.dealsAt3, Is.Zero);
            Assert.That(artem.lastStockPrice, Is.EqualTo(175)); Assert.That(StockHistory.Short("artem", "ore", 175), Is.EqualTo("NEDR $9000 (5142 акции по $1,75, комиссия 5%)"));
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out var copy), Is.True); Assert.That(copy.PersonalSavings, Is.EqualTo(c.PersonalSavings));
        }
        [Test] public void ReturningClientReportComparesPurchasePriceWithToday()
        {
            var c = WorkAt(4); var d = c.CreateDay(); WorkCareerTests.Sell(d, 0); Assert.That(d.Current.StockId, Is.EqualTo("logistics"));
            d.EndDay(); Assert.That(c.CompleteDay(d), Is.True);
            while (c.NextDayNumber < 7) { d = c.CreateDay(); d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); }
            d = c.CreateDay(); Assert.That(d.SelectClient(0), Is.True); Assert.That(d.Current.Id, Is.EqualTo("boris"));
            Assert.That(d.ReportQuoteNote, Is.EqualTo(" RLOG: куплены в день 4 по $25,56, сегодня $27,33 (+6,9%). Цена не обещание."));
            int cash = d.Cash; d.StartCall(); string reply = d.Say("Обсудим отчёт по прошлому разговору.");
            Assert.That(reply, Does.Contain(d.ReportQuoteNote)); Assert.That(d.Cash, Is.EqualTo(cash));
        }
        [Test] public void IntroAndOwnerKeepTheFixedTwoPercentWithoutQuotes()
        {
            foreach (var d in new[] { new BrokerDay(mode: CareerMode.Employee), new BrokerDay() })
            {
                Assert.That(d.HasQuotes, Is.False);
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(d.SelectClient(i), Is.True);
                    foreach (var s in StockCatalog.Offers) Assert.That(d.FeePercentFor(s.Id), Is.EqualTo(2));
                    d.SelectStock("biotech"); Assert.That(d.CurrentFeePercent, Is.EqualTo(2)); Assert.That(d.CurrentCommission, Is.EqualTo(d.Current.Commission));
                    Assert.That(d.StockTermsText, Does.Not.Contain("Цена сегодня")); Assert.That(d.ReportQuoteNote, Is.Empty);
                }
            }
        }
    }
}
