using System.Text.RegularExpressions;

namespace Broker.Core
{
    // Deliberately narrow alternative to the explicit disclosure button.
    public static class StockDialogueRules
    {
        public static bool DisclosesSpeculativeRisk(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length > 500) return false;
            string text = message.ToLowerInvariant().Replace('ё', 'е');
            return Regex.IsMatch(text, @"\bвысокий риск\b")
                && Regex.IsMatch(text, @"\bможно потерять всю (?:вложенную )?сумму\b")
                && Regex.IsMatch(text, @"\bпокупателя (?:для продажи )?может не оказаться\b")
                && NoUnexpectedNegation(text);
        }
        private static bool NoUnexpectedNegation(string text)
        { return !Regex.IsMatch(Regex.Replace(text, @"\bпокупателя (?:для продажи )?может не оказаться\b", ""), @"\bне\b|\bнельзя\b|\bневозможно\b"); }
    }
}
