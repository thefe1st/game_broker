using Broker.Core;
using Broker.Runtime.Office;
using NUnit.Framework;
using UnityEngine;

namespace Broker.FlowChecks
{
    public sealed partial class BootstrapFlowTests
    {
        [TestCase(false)] [TestCase(true)]
        public void StockCatalogPausesClockAndPreservesActiveCallAndDraft(bool escape)
        {
            Begin(); Interact(OfficeStation.Desk); Click("Позвонить"); Set("message", "Черновик акции");
            Click("Акции / " + Day.CurrentStock.Ticker); float seconds = Day.RemainingSeconds; Frame();
            Assert.That(Day.RemainingSeconds, Is.EqualTo(seconds)); var text = NotebookText();
            foreach (var s in StockCatalog.Offers) Assert.That(text, Does.Contain(s.Name));
            Click("NEDR / Новые Недра\nВысокий / спекулятивный · 36 мес."); Click("Выбрать / NEDR");
            Assert.That(Day.Current.StockId, Is.EqualTo("ore")); Assert.That(Day.Cash, Is.Zero);
            if (escape) { OfficeInput.Escape = true; Call("Update"); } else Click("Вернуться к звонку / Esc");
            Assert.That(OverlayOpen, Is.False); Assert.That(Day.Call, Is.EqualTo(CallStatus.Active));
            Assert.That(Get<string>("message"), Is.EqualTo("Черновик акции"));
        }
        [Test] public void ExplicitTermsButtonSpeaksCanonicalSelectedStockWithoutNetworkOrSelling()
        {
            Begin(); Interact(OfficeStation.Desk); Click("Позвонить"); Click("Раскрыть свойства акции");
            Assert.That(Day.RisksExplained && Day.FeesExplained, Is.True); Assert.That(Day.NeedsKnown || Day.DealClosed, Is.False);
            Assert.That(Day.Earned, Is.Zero); Call("OpenCallLog", false);
            Assert.That(NotebookText(), Does.Contain(Day.CurrentStock.Name)); Assert.That(NotebookText(), Does.Contain("вложения $7500"));
            Assert.That(UnityEngine.Networking.UnityWebRequest.Sent, Is.Zero);
        }
        [Test] public void RestartClearsUnsavedSelectionAndModeSwitchClosesStockOverlay()
        {
            OpenFirm(); Interact(OfficeStation.Desk); Click("Акции / " + Day.CurrentStock.Ticker); Day.SelectStock("biotech");
            Click("Вернуться к звонку / Esc"); Confirm("RestartDay"); Click("Да, начать день заново");
            Assert.That(Day.Current.StockId, Is.EqualTo(StockCatalog.DefaultFor(Day.Current.Id))); Assert.That(Get<bool>("showStocks"), Is.False);
        }
        [Test] public void AcceptedProposalAllowsReadOnlyCatalogButNotSubstitution()
        {
            Begin(); Interact(OfficeStation.Desk); Day.StartCall(); Day.Say("Какая цель? Есть риск убытка. Комиссия 2%."); Day.OfferDeal();
            Assert.That(Day.Current.ObjectionRaised, Is.True); Click("Акции / " + Day.CurrentStock.Ticker);
            Click("SVET / Городской Свет\nУмеренный · 12 мес."); GUI.Click = "Выбрать / SVET"; Call("OnGUI");
            Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null; Assert.That(Day.Current.StockId, Is.EqualTo("logistics"));
            Assert.That(NotebookText(), Does.Contain("зафиксирована"));
        }
        [Test] public void FocusLossAndVoiceRecordingCannotOpenOrApplyStockChoice()
        {
            Begin(); Interact(OfficeStation.Desk); Call("OnApplicationFocus", false); Call("OpenStocks"); Assert.That(Get<bool>("showStocks"), Is.False);
            Call("OnApplicationFocus", true); Click("Продолжить · Esc");
            Get<Broker.Runtime.Voice.VoiceSession>("voice").BeginRecording(true); Call("OpenStocks");
            Assert.That(Get<bool>("showStocks"), Is.False); Get<Broker.Runtime.Voice.VoiceSession>("voice").Cancel();
            Click("Акции / " + Day.CurrentStock.Ticker); Call("OnApplicationFocus", false);
            GUI.Click = "Выбрать / RLOG"; Call("OnGUI"); Assert.That(GUI.Click, Is.Not.Null); GUI.Click = null;
        }
    }
}
