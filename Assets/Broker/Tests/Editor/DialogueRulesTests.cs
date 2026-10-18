using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class DialogueRulesTests
    {
        [TestCase("Комиссия 2%.", true)]
        [TestCase("НАША КОМИССИЯ — ДВА ПРОЦЕНТА", true)]
        [TestCase("Комиссия составляет два процента", true)]
        [TestCase("Вознаграждение 2 процента", true)]
        [TestCase("2% — наша комиссия", true)]
        [TestCase("Комиссия 2,0%", true)]
        [TestCase("Доходность 2%", false)]
        [TestCase("Комиссия 12%", false)]
        [TestCase("Комиссия 22%", false)]
        [TestCase("Комиссия 0.2%", false)]
        [TestCase("Комиссия 0,2%", false)]
        [TestCase("Комиссия 2,5%", false)]
        [TestCase("Комиссия не 2%", false)]
        [TestCase("Комиссия не составляет 2%", false)]
        [TestCase("Комиссия -2%", false)]
        [TestCase("Комиссия 2% не взимается", false)]
        [TestCase("Комиссия двадцать два процента", false)]
        [TestCase("Не обсуждаю комиссию 2%", false)]
        [TestCase("Доходность 2%, комиссия 12%", false)]
        public void FeeRequiresExactAmountAndFeeContext(string text, bool expected)
        { Assert.That(DialogueRules.Has(DialogueRules.Analyze(text), DialogueIntent.Fee), Is.EqualTo(expected)); }

        [TestCase("Не гарантирую прибыль. Есть риск убытка.", false)]
        [TestCase("Не обещаю гарантированную прибыль", false)]
        [TestCase("Прибыль не гарантирована", false)]
        [TestCase("Гарантирую персональный отчёт", false)]
        [TestCase("Не могу гарантировать доход", false)]
        [TestCase("Риски есть, доход не гарантирован", false)]
        [TestCase("Это не без риска", false)]
        [TestCase("Не бывает безрисковых инвестиций", false)]
        [TestCase("Гарантированная прибыль!", true)]
        [TestCase("Гарантирую доход", true)]
        [TestCase("Гарантирую 20%", true)]
        [TestCase("Риска нет", true)]
        [TestCase("Убытков не будет", true)]
        [TestCase("Невозможно потерять деньги", true)]
        [TestCase("Не гарантирую прибыль, но риска нет", true)]
        [TestCase("Не гарантирую доход. Гарантирую возврат", true)]
        [TestCase("Не гарантирую прибыль и гарантирую возврат", true)]
        public void GuaranteeNegationDoesNotShieldOtherPromises(string text, bool expected)
        { Assert.That(DialogueRules.Has(DialogueRules.Analyze(text), DialogueIntent.Guarantee), Is.EqualTo(expected)); }

        [TestCase("Срок — год", true)]
        [TestCase("Разместим на один год", true)]
        [TestCase("Срок 12 месяцев", true)]
        [TestCase("Двенадцать месяцев", true)]
        [TestCase("Годовой срок", true)]
        [TestCase("Срок", false)]
        [TestCase("Срок пять лет", false)]
        [TestCase("Срок 21 год", false)]
        [TestCase("Срок не год", false)]
        [TestCase("Не 12 месяцев", false)]
        [TestCase("Я звонил год назад", false)]
        public void HorizonNeedsAnActualYear(string text, bool expected)
        { Assert.That(DialogueRules.Has(DialogueRules.Analyze(text), DialogueIntent.Year), Is.EqualTo(expected)); }

        [TestCase("Не расскажу про риски", DialogueIntent.Risk)]
        [TestCase("Не обсуждаю риск", DialogueIntent.Risk)]
        [TestCase("Отчёт не готов", DialogueIntent.Report)]
        [TestCase("Без договора", DialogueIntent.Written)]
        [TestCase("Договор не нужен", DialogueIntent.Written)]
        [TestCase("Письменно не будем", DialogueIntent.Written)]
        [TestCase("Не зафиксируем условия", DialogueIntent.Written)]
        [TestCase("Резерв не нужен", DialogueIntent.Reserve)]
        [TestCase("Не оставим резерв", DialogueIntent.Reserve)]
        [TestCase("Личная жизнь", DialogueIntent.Personal)]
        [TestCase("Личный менеджер не нужен", DialogueIntent.Contact)]
        [TestCase("Не буду на связи", DialogueIntent.Contact)]
        [TestCase("Не буду на связи", DialogueIntent.Personal)]
        [TestCase("Не будем заключать договор", DialogueIntent.Written)]
        public void MentionOrRefusalIsNotDisclosure(string text, DialogueIntent intent)
        { Assert.That(DialogueRules.Has(DialogueRules.Analyze(text), intent), Is.False); }

        [TestCase("Для чего нужны деньги?")]
        [TestCase("Что для вас важно?")]
        [TestCase("Чего вы хотите?")]
        [TestCase("Какая у вас цель?")]
        [TestCase("На что вам нужны деньги?")]
        [TestCase("Какая у вас задача?")]
        public void SupportsDifferentDiscoveryPhrases(string text)
        { Assert.That(DialogueRules.Has(DialogueRules.Analyze(text), DialogueIntent.Needs), Is.True); }
        [TestCase("Риск убытка не исключён")]
        [TestCase("Есть риск потерять деньги")]
        [TestCase("Это не без риска")]
        public void HonestRiskWordingsAreRecognized(string text)
        { Assert.That(DialogueRules.Has(DialogueRules.Analyze(text), DialogueIntent.Risk), Is.True); }
        [TestCase("Это не последний шанс")]
        [TestCase("Предложение действует не только сегодня")]
        public void KnownNegatedPressureDoesNotPunishPlayer(string text)
        { Assert.That(DialogueRules.Has(DialogueRules.Analyze(text), DialogueIntent.Pressure), Is.False); }
        [Test] public void SpeechFriendlyMessageCanCarrySeveralIntents()
        {
            var facts = DialogueRules.Analyze("Какая цель? Есть риск убытка. Комиссия два процента. Срок двенадцать месяцев. Персональный отчет.");
            foreach (var intent in new[] { DialogueIntent.Needs, DialogueIntent.Risk, DialogueIntent.Fee, DialogueIntent.Year, DialogueIntent.Personal })
                Assert.That(DialogueRules.Has(facts, intent), Is.True, intent.ToString());
        }
        [Test] public void EmptyAndOverlongInputHasNoIntents()
        {
            Assert.That(DialogueRules.Analyze(null), Is.EqualTo(DialogueIntent.None));
            Assert.That(DialogueRules.Analyze(new string('a', 501)), Is.EqualTo(DialogueIntent.None));
        }
    }
}
