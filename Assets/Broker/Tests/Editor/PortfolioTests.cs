using System;
using System.Collections.Generic;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    // v5: client portfolios and their reaction to the day's quotes. The market is fixed, so every case is found by a deterministic search.
    public sealed class PortfolioTests
    {
        private const string Honest = "Цена упала — это риск акций. Гарантировать рост не могу.";
        private static StockPosition Position(string stockId, int day) => new StockPosition { stockId = stockId, day = day, priceCents = StockMarket.PriceCents(stockId, day) };
        // Change of one position from the purchase day, in tenths of a percent.
        private static int Tenths(string stockId, int buy, int day)
        { int price = StockMarket.PriceCents(stockId, buy); return (int)(((long)StockMarket.PriceCents(stockId, day) - price) * 1000 / price); }
        private static bool IsDrop(int tenths) => tenths <= StockPortfolio.DropTenths && tenths > StockPortfolio.DeepDropTenths;
        private static int NextAppointment(string id, int after)
        { int day = after + 1; while (Array.IndexOf(ClientCatalog.Appointments(day), id) < 0) day++; return day; }
        // First appointment of the client from day 4 whose next (skip = 1: second next) appointment shows the wanted change.
        private static int FindBuyDay(string id, string stockId, Func<int, bool> wanted, int skip = 0)
        {
            for (int buy = 4; buy < 4000; buy++)
            {
                if (Array.IndexOf(ClientCatalog.Appointments(buy), id) < 0) continue;
                int day = buy; for (int n = 0; n <= skip; n++) day = NextAppointment(id, day);
                if (wanted(Tenths(stockId, buy, day))) return buy;
            }
            throw new InvalidOperationException("No such market case.");
        }
        private static WorkCareerState Work() => WorkCareerState.FromIntro(WorkCareerTests.Intro());
        private static int Slot(BrokerDay d, string id) => Array.IndexOf(ClientCatalog.Appointments(d.DayNumber), id);
        private static WorkClientState Client(WorkCareerState c, string id) => c.Clients[ClientCatalog.Index(id)];
        private static void Skip(WorkCareerState c) { var d = c.CreateDay(); d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); }
        private static void SkipTo(WorkCareerState c, int day) { while (c.NextDayNumber < day) Skip(c); }
        private static void Buy(WorkCareerState c, string id, string stockId, int day)
        {
            SkipTo(c, day); var d = c.CreateDay(); StockHistoryTests.SellStock(d, Slot(d, id), stockId);
            d.EndDay(); Assert.That(c.CompleteDay(d), Is.True);
        }
        // One purchase, then the client's next appointment with the call not yet started.
        private static BrokerDay Reaction(WorkCareerState c, string id, string stockId, int buy)
        {
            Buy(c, id, stockId, buy); SkipTo(c, NextAppointment(id, buy));
            var d = c.CreateDay(); Assert.That(d.SelectClient(Slot(d, id)), Is.True); return d;
        }
        // Trust of the same appointment without any price reaction: the public constructor with the arguments CreateDay uses.
        private static int TrustWithoutPortfolio(WorkCareerState c, int slot)
        {
            var ids = ClientCatalog.Appointments(c.NextDayNumber); var bonuses = new int[3]; var returns = new bool[3]; var complaints = new bool[3];
            for (int i = 0; i < 3; i++) { var x = Client(c, ids[i]); bonuses[i] = x.relationship; returns[i] = x.awaitingReport; complaints[i] = x.complaintPending; }
            return new BrokerDay(c.NextDayNumber, c.PersonalSavings, c.Reputation, relationshipBonuses: bonuses, returning: returns,
                mode: CareerMode.EmployeeWork, clientIds: ids, complaints: complaints).Clients[slot].Trust;
        }
        private static void AssertSame(StockPosition[] actual, StockPosition[] expected)
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int n = 0; n < actual.Length; n++)
            {
                Assert.That(actual[n].stockId, Is.EqualTo(expected[n].stockId)); Assert.That(actual[n].day, Is.EqualTo(expected[n].day));
                Assert.That(actual[n].priceCents, Is.EqualTo(expected[n].priceCents));
            }
        }

        [Test] public void MoodUsesInclusiveThresholdsInTenthsOfAPercent()
        {
            Assert.That(StockPortfolio.Mood("inna", null, 10), Is.EqualTo(PortfolioMood.None));
            Assert.That(StockPortfolio.Mood("inna", new StockPosition[0], 10), Is.EqualTo(PortfolioMood.None));
            Assert.That(StockPortfolio.ChangeTenths("inna", new StockPosition[0], 10), Is.Zero);
            Assert.That(StockPortfolio.IsConcern(PortfolioMood.Drop) && StockPortfolio.IsConcern(PortfolioMood.DeepDrop), Is.True);
            Assert.That(StockPortfolio.IsConcern(PortfolioMood.None) || StockPortfolio.IsConcern(PortfolioMood.Calm) || StockPortfolio.IsConcern(PortfolioMood.Growth), Is.False);
            // The fixed market has exact boundary changes: −15,0% is a deep drop, −5,0% a drop, +5,0% growth.
            var wanted = new Dictionary<int, PortfolioMood> { { -151, PortfolioMood.DeepDrop }, { -150, PortfolioMood.DeepDrop }, { -149, PortfolioMood.Drop },
                { -50, PortfolioMood.Drop }, { -49, PortfolioMood.Calm }, { 0, PortfolioMood.Calm }, { 49, PortfolioMood.Calm }, { 50, PortfolioMood.Growth } };
            var found = new HashSet<int>();
            foreach (var stock in StockCatalog.Offers)
                for (int buy = 4; buy < 3000 && found.Count < wanted.Count; buy++)
                    for (int day = buy + 1; day <= buy + 30; day++)
                    {
                        int tenths = Tenths(stock.Id, buy, day);
                        if (!wanted.ContainsKey(tenths)) continue;
                        var positions = new[] { Position(stock.Id, buy) };
                        Assert.That(StockPortfolio.ChangeTenths("artem", positions, day), Is.EqualTo(tenths));
                        Assert.That(StockPortfolio.Mood("artem", positions, day), Is.EqualTo(wanted[tenths]), stock.Id + " " + buy + " → " + day);
                        found.Add(tenths);
                    }
            Assert.That(found.Count, Is.EqualTo(wanted.Count));
        }
        [Test] public void ChangeIsWeightedByCostAndTheWorstPositionIsNamed()
        {
            var positions = new[] { Position("medical", 10), Position("ore", 11), Position("cloud", 14) }; long cost = 0, value = 0;
            foreach (var p in positions)
            { int shares = StockMarket.Shares("artem", p.priceCents); cost += (long)shares * p.priceCents; value += (long)shares * StockMarket.PriceCents(p.stockId, 20); }
            Assert.That(StockPortfolio.CostCents("artem", positions), Is.EqualTo(cost));
            Assert.That(StockPortfolio.ValueCents("artem", positions, 20), Is.EqualTo(value));
            Assert.That(StockPortfolio.ChangeTenths("artem", positions, 20), Is.EqualTo((int)((value - cost) * 1000 / cost)));
            int worst = 0;
            for (int i = 1; i < 3; i++) if (Tenths(positions[i].stockId, positions[i].day, 20) < Tenths(positions[worst].stockId, positions[worst].day, 20)) worst = i;
            Assert.That(StockPortfolio.WorstIndex(positions, 20), Is.EqualTo(worst));
            Assert.That(StockPortfolio.WorstIndex(new[] { Position("cloud", 14), Position("cloud", 14) }, 20), Is.Zero, "a tie names the oldest");
            Assert.That(StockPortfolio.WorstIndex(new StockPosition[0], 20), Is.EqualTo(-1));
            Assert.That(StockMarket.Percent(74), Is.EqualTo("+7,4%")); Assert.That(StockMarket.Percent(-150), Is.EqualTo("−15,0%")); Assert.That(StockMarket.Percent(0), Is.EqualTo("0,0%"));
        }
        [Test] public void DescribeListsPositionsTotalAndReaction()
        {
            var client = new WorkClientState { id = "inna", deals = 2, lastStockId = "medical", lastStockDay = 9, lastStockPrice = StockMarket.PriceCents("medical", 9),
                portfolio = new[] { Position("cloud", 5), Position("medical", 9) } };
            string text = StockPortfolio.Describe(client, 13, 7);
            Assert.That(text, Does.StartWith("Портфель по ценам дня 13 (2 из 3): OBLK, день 5: "));
            Assert.That(text, Does.Contain("MDAT, день 9: ")); Assert.That(text, Does.Contain(" → сегодня "));
            Assert.That(text.Split(new[] { "(ещё в найме)" }, StringSplitOptions.None).Length, Is.EqualTo(2), "only the purchase before the opening day");
            Assert.That(text, Does.Contain("Всего " + StockMarket.Money(StockPortfolio.CostCents("inna", client.portfolio)) + " → "
                + StockMarket.Money(StockPortfolio.ValueCents("inna", client.portfolio, 13)) + " (" + StockMarket.Percent(StockPortfolio.ChangeTenths("inna", client.portfolio, 13)) + ")"));
            Assert.That(text, Does.EndWith(StockPortfolio.MoodNote(StockPortfolio.Mood("inna", client.portfolio, 13)) + "."));
            Assert.That(StockPortfolio.Describe(client, 13), Does.Not.Contain("ещё в найме"));
            Assert.That(StockPortfolio.Describe(new WorkClientState { id = "inna" }, 13), Does.StartWith("Портфель пуст"));
            Assert.That(StockPortfolio.Describe(new WorkClientState { id = "inna", deals = 1 }, 13), Does.Contain("покупок с котировкой нет"));
            Assert.That(StockPortfolio.Line("inna", client.portfolio[1], 13), Does.StartWith("MDAT, день 9: ").And.Contain(StockMarket.Money(client.portfolio[1].priceCents)));
        }
        [Test] public void PurchasesAccumulateUpToThreeAndEachDayReadsThemBeforeCalls()
        {
            var c = Work(); var bought = new List<StockPosition>(); int savings = c.PersonalSavings;
            while (bought.Count < 5)
            {
                var d = c.CreateDay(); int slot = Slot(d, "boris");
                if (slot >= 0)
                {
                    var before = Client(c, "boris").portfolio; var record = d.Clients[slot];
                    AssertSame(record.Portfolio, before);
                    Assert.That(record.Mood, Is.EqualTo(StockPortfolio.Mood("boris", before, d.DayNumber)));
                    Assert.That(record.PortfolioChangeTenths, Is.EqualTo(StockPortfolio.ChangeTenths("boris", before, d.DayNumber)));
                    Assert.That(BrokerDay.PortfolioBadge(record), Is.EqualTo(before.Length == 0 ? "" : "Портфель " + StockMarket.Percent(record.PortfolioChangeTenths)));
                    WorkCareerTests.Sell(d, slot); bought.Add(Position("logistics", d.DayNumber));
                }
                d.EndDay(); Assert.That(c.CompleteDay(d), Is.True); Assert.That(WorkCareerState.TryRestore(c.Capture(), out c), Is.True);
                int count = Math.Min(StockPortfolio.MaxPositions, bought.Count);
                AssertSame(Client(c, "boris").portfolio, bought.GetRange(bought.Count - count, count).ToArray());
            }
            // A pushed-out position was sold by the client outside the game: deals and money keep all five purchases.
            Assert.That(Client(c, "boris").deals, Is.EqualTo(5));
            Assert.That(c.PersonalSavings, Is.EqualTo(savings + 5 * BrokerDay.PersonalPayout(StockMarket.Commission("boris", "logistics"))));
            // Past the seven-day ledger the folded checkpoint keeps the same three positions.
            var latest = Client(c, "boris").portfolio; for (int n = 0; n < 8; n++) Skip(c);
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out c), Is.True);
            AssertSame(c.Capture().checkpoint.clients[ClientCatalog.Index("boris")].portfolio, latest); AssertSame(Client(c, "boris").portfolio, latest);
            Assert.That(Client(c, "boris").lastStockDay, Is.EqualTo(latest[2].day));
        }
        [Test] public void DropOpensWithAPriceQuestionAndBlocksTheOfferUntilAnHonestAnswer()
        {
            var c = Work(); int buy = FindBuyDay("inna", "medical", IsDrop); var d = Reaction(c, "inna", "medical", buy); int slot = Slot(d, "inna");
            Assert.That(d.Current.Mood, Is.EqualTo(PortfolioMood.Drop)); Assert.That(d.Current.PriceConcern, Is.True);
            Assert.That(d.Current.PortfolioChangeTenths, Is.EqualTo(Tenths("medical", buy, d.DayNumber)));
            Assert.That(d.Trust, Is.EqualTo(TrustWithoutPortfolio(c, slot)), "a plain drop does not cost trust");
            for (int i = 0; i < slot; i++) { d.SelectClient(i); d.StartCall(); d.EndCall(); }
            d.SelectClient(slot); Assert.That(d.RecommendedClientIndex, Is.EqualTo(slot));
            Assert.That(d.EmploymentNextAction, Does.Contain("Портфель клиента упал: начните с честного объяснения цены."));
            string opening = d.StartCall();
            Assert.That(opening, Does.Contain("Мои акции MDAT упали на")); Assert.That(opening, Does.Contain("с покупки в день " + buy));
            Assert.That(opening, Does.Contain("весь портфель " + StockMarket.Percent(d.Current.PortfolioChangeTenths))); Assert.That(opening, Does.Contain("Только без сказок"));
            Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.PriceConcern));
            Assert.That(BrokerPresentation.ShortHint(ConversationStage.PriceConcern, "inna"), Is.EqualTo("Объясни падение цены честно"));
            Assert.That(d.NextConversationStep, Does.Contain("Гарантировать рост не могу"));
            Assert.That(d.OfferDeal(), Does.Contain("Сначала объясните, что с моими акциями.")); Assert.That(d.DealClosed, Is.False);
            string answer = d.Say(Honest);
            Assert.That(answer, Does.Contain("Честно — уже лучше")); Assert.That(answer, Does.Contain("Теперь отчёт по прошлому разговору."));
            Assert.That(d.Current.PriceConcernResolved, Is.True); Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Report));
            WorkCareerTests.SellAfterStarted(d); d.EndDay();
            Assert.That(d.PriceReactionsSummary, Is.EqualTo("Портфели: " + d.Current.Name + " — падение " + StockMarket.Percent(d.Current.PortfolioChangeTenths) + ", объяснили честно."));
            int relationship = Client(c, "inna").relationship;
            Assert.That(c.CompleteDay(d), Is.True); Assert.That(c.LastReport.concernResolved[slot], Is.True);
            Assert.That(Client(c, "inna").relationship, Is.EqualTo(Math.Min(2, relationship + 1))); Assert.That(Client(c, "inna").portfolio.Length, Is.EqualTo(2));
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out _), Is.True);
        }
        [TestCase("ignored", -1)] [TestCase("settled", 0)] [TestCase("unanswered", -1)]
        public void ConcernChangesTheRelationshipByTheCallOutcome(string outcome, int change)
        {
            var c = Work(); var d = Reaction(c, "inna", "medical", FindBuyDay("inna", "medical", IsDrop)); int slot = Slot(d, "inna");
            int before = Client(c, "inna").relationship;
            if (outcome != "ignored") { d.StartCall(); if (outcome == "settled") d.Say(Honest); d.EndCall(); }
            d.EndDay(); string summary = d.PriceReactionsSummary;
            Assert.That(summary, Does.Contain(outcome == "ignored" ? "не позвонили: отношения −1" : outcome == "settled" ? "объяснили честно" : "не объяснили"));
            Assert.That(c.CompleteDay(d), Is.True); Assert.That(c.LastReport.concernResolved[slot], Is.EqualTo(outcome == "settled"));
            // An honest answer spares the −1 of a call without a sale; an ignored concern costs 1 even without a call.
            Assert.That(before, Is.GreaterThan(-1)); Assert.That(Client(c, "inna").relationship - before, Is.EqualTo(change));
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out _), Is.True);
        }
        [Test] public void PromisedRecoveryIsAFalseGuaranteeAndTheConcernStaysOpen()
        {
            var c = Work(); var d = Reaction(c, "inna", "medical", FindBuyDay("inna", "medical", IsDrop)); int slot = Slot(d, "inna");
            d.StartCall(); d.Say("Какая цель?"); int trust = d.Trust; Assert.That(trust, Is.GreaterThan(1));
            string reply = d.Say("Не переживайте, цена точно вернётся.");
            Assert.That(reply, Does.Contain("Рост цены никто обещать не может — это та же гарантия.")); Assert.That(reply, Does.Contain("претензия к гарантии"));
            Assert.That(d.Current.FalseGuaranteeMade, Is.True); Assert.That(d.Trust, Is.EqualTo(trust - 1));
            d.Say(Honest); Assert.That(d.Current.PriceConcernResolved, Is.False); Assert.That(d.NextConversationStep, Does.Contain("ложная гарантия"));
            Assert.That(d.OfferDeal(), Does.Contain("Сначала объясните, что с моими акциями.")); Assert.That(d.DealClosed, Is.False);
            d.EndCall(); d.EndDay(); Assert.That(d.PriceReactionsSummary, Does.Contain("ложная гарантия"));
            int relationship = Client(c, "inna").relationship;
            Assert.That(c.CompleteDay(d), Is.True); Assert.That(c.LastReport.guaranteed[slot], Is.True); Assert.That(c.LastReport.concernResolved[slot], Is.False);
            Assert.That(Client(c, "inna").complaintPending, Is.True); Assert.That(Client(c, "inna").relationship, Is.EqualTo(Math.Max(-1, relationship - 2)));
            // Before quotes the same words are not a market forecast.
            var intro = new EmploymentState().CreateDay(); intro.StartCall(); trust = intro.Trust;
            intro.Say("Цена точно вернётся."); Assert.That(intro.Trust, Is.EqualTo(trust)); Assert.That(intro.Current.FalseGuaranteeMade, Is.False);
        }
        [Test] public void DeepDropCostsTrustAndLeavesOnlyModerateStocksForTheDay()
        {
            var c = Work(); int buy = FindBuyDay("inna", "medical", t => t <= StockPortfolio.DeepDropTenths);
            var d = Reaction(c, "inna", "medical", buy); int slot = Slot(d, "inna");
            Assert.That(d.Current.Mood, Is.EqualTo(PortfolioMood.DeepDrop)); Assert.That(d.Trust, Is.EqualTo(Math.Max(0, TrustWithoutPortfolio(c, slot) - 1)));
            foreach (var stock in StockCatalog.Offers)
                Assert.That(d.FitsToday(stock), Is.EqualTo(stock.Risk == StockRisk.Moderate), stock.Id);
            Assert.That(d.StockSuitability(StockCatalog.Find("cloud")), Does.Contain("только на умеренный риск: SVET или RLOG"));
            Assert.That(d.StockSuitability(StockCatalog.Find("ore")), Is.EqualTo(StockCatalog.Suitability("inna", StockCatalog.Find("ore"))), "the catalog limit is named first");
            Assert.That(d.StartCall(), Does.Contain("сегодня никакого повышенного риска"));
            Assert.That(d.NextConversationStep, Does.Contain("SVET или RLOG"));
            Assert.That(d.Say(Honest), Does.Contain("Убыток это не отменяет")); Assert.That(d.Current.PriceConcernResolved, Is.True);
            d.Say("Обсудим отчёт по прошлой сделке. Какая цель?");
            Assert.That(d.OfferDeal(), Does.Contain("только на умеренный риск")); Assert.That(d.DealClosed, Is.False);
            Assert.That(d.SelectStock("logistics"), Does.Contain("Соответствует игровому лимиту"));
            d.EndDay(); Assert.That(d.PriceReactionsSummary, Does.Contain("сильное падение")); Assert.That(c.CompleteDay(d), Is.True);
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out _), Is.True);
        }
        [Test] public void DeepDropSaleOfAModerateStockIsRecorded()
        {
            var c = Work(); var d = Reaction(c, "inna", "medical", FindBuyDay("inna", "medical", t => t <= StockPortfolio.DeepDropTenths)); int slot = Slot(d, "inna");
            d.SelectStock("logistics"); d.StartCall(); d.Say(Honest); WorkCareerTests.SellAfterStarted(d); d.EndDay();
            Assert.That(c.CompleteDay(d), Is.True); Assert.That(c.LastReport.soldStockIds[slot], Is.EqualTo("logistics"));
            Assert.That(c.LastReport.concernResolved[slot], Is.True); Assert.That(Client(c, "inna").portfolio[1].stockId, Is.EqualTo("logistics"));
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out _), Is.True);
        }
        [Test] public void GrowthAddsOneTrustAndTheClientSaysItIsNotAPromise()
        {
            var c = Work(); var d = Reaction(c, "inna", "medical", FindBuyDay("inna", "medical", t => t >= StockPortfolio.GrowthTenths)); int slot = Slot(d, "inna");
            int without = TrustWithoutPortfolio(c, slot); Assert.That(without, Is.LessThan(4));
            Assert.That(d.Current.Mood, Is.EqualTo(PortfolioMood.Growth)); Assert.That(d.Current.PriceConcern, Is.False); Assert.That(d.Trust, Is.EqualTo(without + 1));
            Assert.That(BrokerDay.PortfolioBadge(d.Current), Does.StartWith("Портфель +"));
            Assert.That(d.StartCall(), Does.Contain("Приятно, но это не обещание.")); Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Report));
            d.EndDay(); Assert.That(d.PriceReactionsSummary, Is.EqualTo("Портфели: " + d.Current.Name + " — рост " + StockMarket.Percent(d.Current.PortfolioChangeTenths) + ", +1 доверия."));
        }
        [Test] public void ComplaintIsSettledBeforeThePriceConcernInTheSameCall()
        {
            // Inna buys; on her next call she hears a false guarantee; on the call after that she has a complaint and a price drop.
            var c = Work(); int buy = FindBuyDay("inna", "medical", IsDrop, skip: 1); Buy(c, "inna", "medical", buy);
            SkipTo(c, NextAppointment("inna", buy)); var d = c.CreateDay(); d.SelectClient(Slot(d, "inna")); d.StartCall();
            d.Say("Гарантирую прибыль."); d.EndCall(); d.EndDay(); Assert.That(c.CompleteDay(d), Is.True);
            SkipTo(c, NextAppointment("inna", NextAppointment("inna", buy))); d = c.CreateDay(); int slot = Slot(d, "inna"); d.SelectClient(slot);
            Assert.That(d.Current.ComplaintPending, Is.True); Assert.That(d.Current.Mood, Is.EqualTo(PortfolioMood.Drop));
            Assert.That(d.EmploymentNextAction, Does.Contain("претензию к гарантии"));
            Assert.That(d.StartCall(), Does.Contain("И ещё: весь портфель")); Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.Complaint));
            string reply = d.Say("Признаю ошибку. Есть риск убытка.");
            Assert.That(d.Current.ComplaintResolved, Is.True); Assert.That(d.Current.PriceConcernResolved, Is.False, "one risk sentence settles only the complaint");
            Assert.That(reply, Does.Contain("Теперь объясните, что с ценой моих акций.")); Assert.That(d.ConversationStep, Is.EqualTo(ConversationStage.PriceConcern));
            d.Say(Honest); Assert.That(d.Current.PriceConcernResolved, Is.True); d.EndCall(); d.EndDay();
            int relationship = Client(c, "inna").relationship; Assert.That(c.CompleteDay(d), Is.True);
            Assert.That(c.LastReport.complaintResolved[slot] && c.LastReport.concernResolved[slot], Is.True);
            Assert.That(Client(c, "inna").complaintPending, Is.False); Assert.That(Client(c, "inna").relationship, Is.EqualTo(Math.Min(2, relationship + 1)));
        }
        [TestCase("Цена точно вернётся.", true)]
        [TestCase("Не переживайте, акции обязательно вырастут.", true)]
        [TestCase("Портфель отыграется, вот увидите.", true)]
        [TestCase("Котировки восстановятся к осени.", true)]
        [TestCase("Курс вернётся, точно.", true)]
        [TestCase("Сто процентов поднимется.", true)]
        [TestCase("Гарантировать не могу, но цена точно вернётся.", true)]
        [TestCase("Цена упала — это риск акций, но бумаги наверняка подорожают.", true)]
        [TestCase("Цена упала — это риск акций. Гарантировать рост не могу.", false)]
        [TestCase("Цена может вырасти, а может упасть.", false)]
        [TestCase("Если цена вырастет, хорошо.", false)]
        [TestCase("Не могу обещать, что цена вернётся.", false)]
        [TestCase("Обещать не могу, что акции вырастут.", false)]
        [TestCase("Вернётся ли цена, не знаю.", false)]
        [TestCase("Цена вырастет или упадёт — никто не знает.", false)]
        [TestCase("Никто не знает, вернётся ли цена.", false)]
        [TestCase("Я не уверен, что акции вырастут.", false)]
        [TestCase("Цена не вернётся быстро, возможно, вырастет позже.", false)]
        [TestCase("Обсудим отчёт. Есть риск убытка.", false)]
        public void RecoveryPromiseIsAForecastStatedAsAFact(string text, bool promise)
            => Assert.That(DialogueRules.Has(DialogueRules.Analyze(text, 3), DialogueIntent.Rebound), Is.EqualTo(promise), text);
        [Test] public void TamperedPortfoliosAndReactionsAreRejected()
        {
            // Inna: a drop settled honestly, then a second purchase; boris has no purchases.
            var c = Work(); var d = Reaction(c, "inna", "medical", FindBuyDay("inna", "medical", IsDrop)); int slot = Slot(d, "inna");
            d.StartCall(); d.Say(Honest); WorkCareerTests.SellAfterStarted(d); d.EndDay(); Assert.That(c.CompleteDay(d), Is.True);
            int last = c.Capture().reports.Length - 1, other = (slot + 1) % 3;
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out _), Is.True);
            var s = c.Capture(); s.reports[last].concernResolved[slot] = false; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "sale with an open concern");
            s = c.Capture(); s.reports[last].concernResolved[other] = true; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "settled without a concern");
            s = c.Capture(); s.reports[last].concernResolved = null; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = c.Capture(); s.reports[last].concernResolved = new bool[2]; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = c.Capture(); s.portfolioFromDay = 3; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "before quotes");
            s = c.Capture(); s.portfolioFromDay = c.NextDayNumber + 1; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "after the next day");
            s = c.Capture(); s.portfolioFromDay = c.NextDayNumber; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "the settled concern had no reaction");
            // Folded into the checkpoint, the positions are checked against the client's purchases.
            for (int n = 0; n < 8; n++) Skip(c);
            Assert.That(WorkCareerState.TryRestore(c.Capture(), out _), Is.True); int inna = ClientCatalog.Index("inna"), boris = ClientCatalog.Index("boris");
            Assert.That(c.Capture().checkpoint.clients[inna].portfolio.Length, Is.EqualTo(2)); Assert.That(c.Capture().checkpoint.clients[boris].portfolio, Is.Empty);
            s = c.Capture(); s.checkpoint.clients[inna].portfolio[0].priceCents++; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "not the day's quote");
            s = c.Capture(); Array.Reverse(s.checkpoint.clients[inna].portfolio); Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "not by day");
            s = c.Capture(); s.checkpoint.clients[inna].portfolio = new[] { s.checkpoint.clients[inna].portfolio[0] }; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "last is not the last purchase");
            s = c.Capture(); s.checkpoint.clients[inna].portfolio = null; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "null reads as empty");
            s = c.Capture(); var p = s.checkpoint.clients[inna].portfolio;
            s.checkpoint.clients[inna].portfolio = new[] { p[0], p[0], p[0], p[1] }; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "more than three");
            s = c.Capture(); s.checkpoint.clients[inna].portfolio[0].day = NextAppointment("boris", 3); s.checkpoint.clients[inna].portfolio[0].priceCents = StockMarket.PriceCents("medical", NextAppointment("boris", 3));
            Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "not an appointment of the client");
            s = c.Capture(); s.checkpoint.clients[boris].portfolio = new[] { Position("logistics", 4) }; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False, "no such purchase");
            s = c.Capture(); s.checkpoint.clients[inna].deals = 1; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
        }
        [Test] public void V4SaveGetsTheLastQuotedPurchaseAndReactionsFromTheNextDay()
        {
            var c = Work();
            // Days 4–8 (folded): everyone buys; days 9–15 (ledger): only the first slot of every second day.
            for (int n = 0; n < 12; n++)
            {
                var d = c.CreateDay(); for (int i = 0; i < 3; i++) if (n < 5 || (i == 0 && n % 2 == 0)) WorkCareerTests.Sell(d, i);
                d.EndDay(); Assert.That(c.CompleteDay(d), Is.True);
            }
            var s = StockHistoryTests.LegacyV4(c.Capture());
            Assert.That(WorkCareerState.TryRestore(s, out var loaded), Is.True); Assert.That(s.schemaVersion, Is.EqualTo(4)); Assert.That(s.reports[0].concernResolved, Is.Null);
            Assert.That(loaded.Capture().schemaVersion, Is.EqualTo(5)); Assert.That(loaded.PortfolioFromDay, Is.EqualTo(16)); Assert.That(loaded.MarketFromDay, Is.EqualTo(4));
            Assert.That(loaded.PersonalSavings, Is.EqualTo(c.PersonalSavings)); Assert.That(loaded.TotalDeals, Is.EqualTo(c.TotalDeals));
            bool shorter = false;
            for (int i = 0; i < 6; i++)
            {
                var native = c.Clients[i].portfolio; var imported = loaded.Clients[i].portfolio;
                Assert.That(imported.Length, Is.InRange(1, native.Length)); AssertSame(imported, new List<StockPosition>(native).GetRange(native.Length - imported.Length, imported.Length).ToArray());
                shorter |= imported.Length < native.Length;
            }
            Assert.That(shorter, Is.True, "earlier purchases are not guessed");
            var day = loaded.CreateDay();
            foreach (var client in day.Clients) Assert.That(client.Mood, Is.EqualTo(StockPortfolio.Mood(client.Id, Client(loaded, client.Id).portfolio, 16)));
            // v4 data cannot carry v5 fields.
            s = StockHistoryTests.LegacyV4(c.Capture()); s.portfolioFromDay = 4; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = StockHistoryTests.LegacyV4(c.Capture()); s.checkpoint.clients[0].portfolio = c.Clients[0].portfolio; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = StockHistoryTests.LegacyV4(c.Capture()); s.reports[0].concernResolved = new[] { true, false, false }; Assert.That(WorkCareerState.TryRestore(s, out _), Is.False);
            s = StockHistoryTests.LegacyV4(c.Capture()); s.reports[0].concernResolved = new bool[3]; s.checkpoint.clients[0].portfolio = new StockPosition[0];
            Assert.That(WorkCareerState.TryRestore(s, out _), Is.True, "Unity writes missing arrays as empty");
        }
        [Test] public void FounderKeepsEmployeePortfoliosAndReactsToThemFromTheOpeningDay()
        {
            var work = FirmCareerTests.ReadyWork(); var firm = FirmCareerState.FromWork(work); int opening = firm.OpeningDayNumber;
            Assert.That(firm.PortfolioFromDay, Is.EqualTo(opening)); bool carried = false;
            for (int i = 0; i < 6; i++) { AssertSame(firm.Clients[i].portfolio, work.Clients[i].portfolio); carried |= work.Clients[i].portfolio.Length > 0; }
            Assert.That(carried, Is.True);
            var d = firm.CreateDay();
            foreach (var client in d.Clients)
            {
                var positions = firm.Clients[ClientCatalog.Index(client.Id)].portfolio;
                Assert.That(client.Mood, Is.EqualTo(StockPortfolio.Mood(client.Id, positions, opening)));
                if (positions.Length > 0) Assert.That(StockPortfolio.Describe(firm.Clients[ClientCatalog.Index(client.Id)], opening, opening), Does.Contain("(ещё в найме)"));
            }
            for (int n = 0; n < 9; n++) FirmCareerTests.Finish(firm, true);
            Assert.That(FirmCareerState.TryRestore(firm.Capture(), out firm), Is.True); bool own = false;
            foreach (var client in firm.Clients) foreach (var p in client.portfolio) own |= p.day >= opening;
            Assert.That(own, Is.True);
            var reset = firm.RestartFromOrigin(); Assert.That(reset.PortfolioFromDay, Is.EqualTo(opening));
            for (int i = 0; i < 6; i++) AssertSame(reset.Clients[i].portfolio, work.Clients[i].portfolio);
            // A v4 founder file: the carried or own last purchase becomes the only folded position; reactions start on the next day.
            var s = StockHistoryTests.LegacyV4(firm.Capture()); Assert.That(FirmCareerState.TryRestore(s, out var loaded), Is.True);
            Assert.That(loaded.PortfolioFromDay, Is.EqualTo(firm.NextDayNumber)); Assert.That(loaded.Balance, Is.EqualTo(firm.Balance));
            foreach (var client in loaded.Capture().origin.clients) Assert.That(client.portfolio.Length, Is.EqualTo(client.lastStockPrice > 0 ? 1 : 0));
            s = StockHistoryTests.LegacyV4(firm.Capture()); s.reports[0].concernResolved = new[] { false, false, true }; Assert.That(FirmCareerState.TryRestore(s, out _), Is.False);
        }
    }
}
