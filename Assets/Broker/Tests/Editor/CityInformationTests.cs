using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class CityInformationTests
    {
        [Test] public void ExchangeBoardIsClosedWithoutQuotes()
        {
            var day = new BrokerDay(); Assert.That(day.HasQuotes, Is.False);
            Assert.That(ExchangeBoard.Rows(day), Is.Empty); Assert.That(ExchangeBoard.Rows(null), Is.Empty);
            Assert.That(ExchangeBoard.Title(day), Is.EqualTo(ExchangeBoard.ClosedTitle)); Assert.That(ExchangeBoard.Title(null), Is.EqualTo("ТОРГИ ЗАКРЫТЫ"));
            for (int column = 0; column < 3; column++) Assert.That(ExchangeBoard.Column(day, column), Is.Empty);
        }
        [Test] public void ExchangeBoardShowsTheSixStocksOfAWorkDayWithoutChangingIt()
        {
            var day = WorkCareerState.FromIntro(WorkCareerTests.Intro(true)).CreateDay(); Assert.That(day.HasQuotes, Is.True);
            int cash = day.Cash; float seconds = day.RemainingSeconds; int trust = day.Clients[0].Trust;
            var rows = ExchangeBoard.Rows(day); Assert.That(rows.Count, Is.EqualTo(StockCatalog.Offers.Length)); Assert.That(rows.Count, Is.EqualTo(6));
            for (int i = 0; i < rows.Count; i++)
            {
                var offer = StockCatalog.Offers[i]; var row = rows[i];
                Assert.That(row.Ticker, Is.EqualTo(offer.Ticker)); Assert.That(row.Name, Is.EqualTo(offer.Name));
                Assert.That(row.PriceCents, Is.EqualTo(StockMarket.PriceCents(offer.Id, day.DayNumber)));
                Assert.That(row.PreviousCents, Is.EqualTo(StockMarket.PriceCents(offer.Id, day.DayNumber - 1)));
                Assert.That(row.Price, Is.EqualTo(StockMarket.Money(row.PriceCents))); Assert.That(row.Change, Is.EqualTo(StockMarket.Change(row.PreviousCents, row.PriceCents)));
                Assert.That(row.Direction, Is.EqualTo(Math.Sign(row.PriceCents - row.PreviousCents)));
            }
            Assert.That(ExchangeBoard.Title(day), Is.EqualTo("КОТИРОВКИ ДНЯ " + day.DayNumber));
            Assert.That(ExchangeBoard.Column(day, 0).Split('\n'), Has.Length.EqualTo(6)); Assert.That(ExchangeBoard.Column(day, 1), Does.StartWith("$"));
            Assert.That(ExchangeBoard.Column(day, 2), Does.Contain("%"));
            Assert.That(day.Cash, Is.EqualTo(cash)); Assert.That(day.RemainingSeconds, Is.EqualTo(seconds)); Assert.That(day.Clients[0].Trust, Is.EqualTo(trust));
        }
        [Test] public void CafeChatterGivesThreeDifferentHarmlessLinesADay()
        {
            var first = new HashSet<string>(); var all = new HashSet<string>();
            for (int dayNumber = 1; dayNumber <= 200; dayNumber++)
            {
                var lines = CityChatter.For(dayNumber); Assert.That(lines, Has.Length.EqualTo(3));
                Assert.That(new HashSet<string>(lines).Count, Is.EqualTo(3), "day " + dayNumber);
                Assert.That(CityChatter.For(dayNumber), Is.EqualTo(lines));
                foreach (string line in lines)
                {
                    Assert.That(line.Length, Is.InRange(20, 110)); Assert.That(line, Does.Not.Contain("$").And.Not.Contain("%"));
                    foreach (var offer in StockCatalog.Offers) Assert.That(line, Does.Not.Contain(offer.Ticker).And.Not.Contain(offer.Name));
                    all.Add(line);
                }
                if (dayNumber <= 30) first.Add(lines[0]);
            }
            Assert.That(first.Count, Is.GreaterThanOrEqualTo(6), "the talk changes from day to day");
            Assert.That(all.Count, Is.EqualTo(CityChatter.PoolSize));
        }
    }
}
