using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Broker.Core
{
    [Flags]
    public enum DialogueIntent
    {
        None = 0, Needs = 1, Risk = 2, Fee = 4, Year = 8, Personal = 16, Report = 32,
        Guarantee = 64, Pressure = 128, Respect = 256, Reserve = 512, Written = 1024,
        Contact = 2048, Jargon = 4096, AdmitError = 8192
    }

    // Deterministic Russian phrase rules, NOT semantic understanding or a financial fact checker.
    // Clause boundaries prevent a negation in one statement from hiding a promise in another.
    public static class DialogueRules
    {
        public static DialogueIntent Analyze(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Trim().Length > 500) return DialogueIntent.None;
            string text = message.Trim().ToLowerInvariant().Replace('ё', 'е').Replace('\u00a0', ' ')
                .Replace('—', '-').Replace('–', '-');
            DialogueIntent result = DialogueIntent.None;
            string[] clauses = Regex.Split(text, @"(?<!\d)[.,]|[.,](?!\d)|[!?;\r\n]+|\b(?:но|однако|зато)\b");
            foreach (string clause in clauses)
            {
                if (UnsafePromise(clause)) result |= DialogueIntent.Guarantee;
                if (!Match(clause, @"\bне\s+(?:призна|ошиб|был\s+неправ|я\s+ошиб)|\bне\s+моя\s+ошибка")
                    && Match(clause, @"\bпризнаю\s+(?:свою\s+)?ошибку|\bя\s+ошиб(?:ся|лась)|\bя\s+был(?:а)?\s+неправ(?:а)?|\bмоя\s+ошибка"))
                    result |= DialogueIntent.AdmitError;
                if (!Denies(clause, @"цел\w*") && Match(clause, @"\bцел(?:ь|и|ях|ью|ям)\b|\bзачем\b|для\s+чего|чего\s+(?:вы\s+)?(?:хотите|ждете)|что\s+(?:для\s+вас\s+)?(?:важно|нужно)|на\s+что\s+(?:вам\s+)?(?:нужны|планируете)|какая\s+(?:у\s+вас\s+)?задача"))
                    result |= DialogueIntent.Needs;
                if (HasRisk(clause))
                    result |= DialogueIntent.Risk;
                if (HasFee(clause)) result |= DialogueIntent.Fee;
                if (HasYear(clause)) result |= DialogueIntent.Year;
                if (!Denies(clause, @"персональн\w*|личн\w*|сопровожд\w*") && !Match(clause, @"\bне\s+(?:буду|остаюсь)\s+на\s+связи") && Match(clause, @"\b(?:персональн|личн)\w*\s+(?:отчет|сопровожд|менеджер|контакт)\w*|\bбуду\s+на\s+связи"))
                    result |= DialogueIntent.Personal;
                if (!Denies(clause, @"отчет\w*|результат\w*|динамик\w*|итог\w*") && Match(clause, @"\b(?:отчет|результат|динамик|итог)\w*"))
                    result |= DialogueIntent.Report;
                if (Match(Regex.Replace(clause, @"\bне\s+(?:последний\s+шанс|только\s+сегодня)", " "), @"\bреша(?:йте|йтесь)\s+сейчас|последний\s+шанс|только\s+сегодня|иначе\s+пожалеете|не\s+думайте|срочно\s+подписывайте")) result |= DialogueIntent.Pressure;
                if (Match(clause, @"решение\s+за\s+вами|не\s+тороплю|без\s+давления|время\s+подумать|можете\s+подумать|не\s+буду\s+торопить")) result |= DialogueIntent.Respect;
                if (!Denies(clause, @"резерв\w*|запас\w*|подушк\w*") && Match(clause, @"\bрезерв\w*|\bподушк\w*|не\s+вкладыва\w*\s+вс[юе]|не\s+всю\s+сумму|остав\w*.{0,25}(?:запас|деньг|средств)|часть\s+свободн\w*\s+(?:средств|денег|капитал)"))
                    result |= DialogueIntent.Reserve;
                if (!Denies(clause, @"договор\w*|письменн\w*|документ\w*") && !Match(clause, @"\bне\s+(?:зафиксир|пропиш)\w*") && Match(clause, @"\bдоговор\w*|\bписьменн\w*|в\s+документ\w*|зафиксир\w*|пропиш\w*"))
                    result |= DialogueIntent.Written;
                if (!Denies(clause, @"менеджер\w*|контакт\w*|связ\w*") && !Match(clause, @"\bне\s+(?:буду|остаюсь)\s+на\s+связи") && Match(clause, @"(?:личн|персональн)\w*\s+менеджер\w*|(?:буду|остаюсь)\s+на\s+связи|пишите\s+мне|звоните\s+мне|мой\s+номер|прям\w*\s+контакт|свяж\w*\s+со\s+мной"))
                    result |= DialogueIntent.Contact;
                if (Match(clause, @"\b(?:диверсификац|волатильност|синерги|мультипликатор)\w*")) result |= DialogueIntent.Jargon;
            }
            return result;
        }

        public static bool Has(DialogueIntent value, DialogueIntent intent) => (value & intent) != 0;
        private static bool Match(string text, string pattern) => Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant);

        private static bool Denies(string clause, string topic)
        {
            return Match(clause, @"\bне\s+(?:буд\w*\s+)?(?:обсужд\w*|расскаж\w*|говор\w*|объясн\w*|раскры\w*|покаж\w*|предостав\w*|остав\w*|зафиксир\w*|пропиш\w*|заключ\w*).{0,35}(?:" + topic + ")")
                || Match(clause, @"\b(?:без|никаких|не\s+нужен|не\s+нужны|не\s+будет)\s+(?:" + topic + ")")
                || Match(clause, "(?:" + topic + @").{0,15}\bне\s+(?:буд\w*|нуж\w*|готов\w*|обсужд\w*|предостав\w*|покаж\w*|составля\w*)");
        }

        private static bool UnsafePromise(string clause)
        {
            string safe = Regex.Replace(clause, @"\bне\s+(?:могу\s+|буду\s+)?гарантир\w*|\bне\s+обещаю\s+гарантир\w*|\b(?:прибыл|доход|возврат)\w*\s+не\s+гарантир\w*|\bне\s+без\s+риска|\bне\s+бывает\s+безрисков\w*", " ");
            return Match(safe, @"гарантир\w*.{0,25}(?:прибыл|доход|заработ|возврат|сохранност|\d+\s*%)|(?:прибыл|доход|возврат)\w*.{0,15}гарантир\w*|\bбез\s+(?:риска|потерь)|\bбезрисков\w*|\b(?:риска|убытков)\s+нет|\bнет\s+(?:риска|убытков)|\bневозможно\s+потерять|\bубытков\s+не\s+будет");
        }

        private static bool HasRisk(string clause)
        {
            string disclosure = Regex.Replace(clause, @"\bне\s+без\s+риска\b", "есть риск");
            return !Denies(disclosure, @"риск\w*|убыт\w*|потер\w*")
                && Match(disclosure, @"\bриск\w*|\bубыт\w*|\bпотер(?:я|и|ять|ей|ь)\w*");
        }

        private static bool HasFee(string clause)
        {
            if (Denies(clause, @"комисси\w*|сбор\w*|вознаграждени\w*")
                || Match(clause, @"\b(?:комисси|сбор|вознаграждени)\w*.{0,30}\bне\s+(?:взима|бер|плат)\w*")) return false;
            var fee = Regex.Match(clause, @"\b(?:комисси|сбор|вознаграждени)\w*");
            if (!fee.Success) return false;
            string after = clause.Substring(fee.Index + fee.Length);
            var number = Regex.Match(after, @"(?<![\d.,-])(?<amount>\d+(?:[.,]\d+)?)\s*(?:%|процент\w*)");
            if (number.Success && number.Index <= 35)
            {
                if (Match(after.Substring(0, number.Index), @"\bне\b")) return false;
                return decimal.TryParse(number.Groups["amount"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal amount) && amount == 2m;
            }
            if (Match(after, @"^\s*(?:[-:=]\s*)?(?:(?:составляет|равна|будет|всего|ровно)\s+)?два\s+процента\b")) return true;
            return Match(clause, @"(?<![\d.,-])2\s*%\s*(?:[-:=]\s*)?(?:это\s+)?(?:наша\s+)?комисси\w*");
        }

        private static bool HasYear(string clause)
        {
            if (Denies(clause, @"срок\w*|год\w*|месяц\w*") || Match(clause, @"\bне\s+(?:на\s+)?(?:12|двенадцать)\s+месяц\w*|\bне\s+(?:на\s+)?(?:один\s+)?год\b|\bгод\s+назад|\b(?:[2-9]\d*|1\d+|0|два|три|пять|десять)\s+(?:год\w*|лет)\b")) return false;
            return Match(clause, @"\bгодов\w*\s+(?:срок|горизонт)|\b(?:12|двенадцать)\s+месяц\w*|\bна\s+(?:один\s+|1\s+)?год\b|\b(?:срок\w*|горизонт\w*|влож\w*|размещ\w*).{0,20}\b(?:один\s+|1\s+)?год\b");
        }
    }
}
