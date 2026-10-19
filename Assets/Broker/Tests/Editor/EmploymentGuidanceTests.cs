using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class EmploymentGuidanceTests
    {
        [Test] public void FirstObjectivePointsToBorisWithoutStartingCallOrPaying()
        {
            var d = new EmploymentState().CreateDay();
            Assert.That(d.RecommendedClientIndex, Is.Zero); Assert.That(d.EmploymentNextAction, Does.Contain("Борис"));
            Assert.That(d.Call, Is.EqualTo(CallStatus.NotStarted)); Assert.That(d.FirmRevenue + d.Cash, Is.Zero);
        }
        [Test] public void ActiveCallOverridesOtherClientRecommendations()
        {
            var d = new EmploymentState().CreateDay(); d.SelectClient(1); d.StartCall();
            Assert.That(d.RecommendedClientIndex, Is.EqualTo(1)); Assert.That(d.EmploymentNextAction, Does.StartWith("Инна:"));
            Assert.That(d.EmploymentNextAction, Does.Contain(d.NextConversationStep));
        }
        [Test] public void ClosedSelectedClientDoesNotTrapPlayerOnFinishedCall()
        {
            var d = new EmploymentState().CreateDay(); d.StartCall(); d.EndCall();
            Assert.That(d.RecommendedClientIndex, Is.EqualTo(1)); Assert.That(d.EmploymentNextAction, Does.Contain("Инна"));
            Assert.That(d.SelectedClientIndex, Is.Zero); // A hint is not an implicit action.
        }
        [Test] public void MeetingSoftTargetDoesNotHideRemainingClients()
        {
            var d = new EmploymentState().CreateDay(); EmploymentTests.Sell(d, 0);
            Assert.That(d.EmploymentNextAction, Does.Contain("Ориентир выполнен"));
            Assert.That(d.RecommendedClientIndex, Is.EqualTo(1));
        }
        [Test] public void AfterAllCallsHintExplainsHowToCloseDay()
        {
            var d = new EmploymentState().CreateDay();
            for (int i = 0; i < 3; i++) { d.SelectClient(i); d.StartCall(); d.EndCall(); }
            Assert.That(d.RecommendedClientIndex, Is.EqualTo(-1)); Assert.That(d.EmploymentNextAction, Does.Contain("входной двери"));
            Assert.That(d.DayEnded, Is.False);
        }
        [Test] public void ReturningClientReminderUsesSavedPreviousDay()
        {
            var c = new EmploymentState(); var d = c.CreateDay(); EmploymentTests.Sell(d, 0); d.EndDay(); c.CompleteDay(d);
            Assert.That(c.CreateDay().EmploymentNextAction, Does.Contain("вчерашний отчёт"));
        }
        [Test] public void FinishedDayNeverRecommendsAnotherCall()
        { var d = new EmploymentState().CreateDay(); d.EndDay(); Assert.That(d.RecommendedClientIndex, Is.EqualTo(-1)); Assert.That(d.EmploymentNextAction, Does.Contain("завершён")); }
    }
}
