using System;

namespace Broker.Core
{
    public enum CallStatus { NotStarted, Active, Closed }

    // Authoritative rules. Dialogue providers must never write money directly.
    public sealed class BrokerDay
    {
        public const int Rent = 100;
        public const int DealCommission = 150;
        public const float DurationSeconds = 240f;

        public int Cash { get; private set; }
        public int Trust { get; private set; } = 1;
        public float RemainingSeconds { get; private set; } = DurationSeconds;
        public bool NeedsKnown { get; private set; }
        public bool RisksExplained { get; private set; }
        public bool FeesExplained { get; private set; }
        public bool DealClosed { get; private set; }
        public bool DayEnded { get; private set; }
        public CallStatus Call { get; private set; }
        public bool RentCovered => Cash >= Rent;
        public int BalanceAfterRent => Cash - Rent;

        public string StartCall()
        {
            if (DayEnded) return "Рабочий день уже закончился.";
            if (Call != CallStatus.NotStarted) return "Этот звонок уже начат или завершён.";
            Call = CallStatus.Active;
            return "Борис: У меня три автомойки. Чем ваши бумажки лучше четвёртой?";
        }

        public string Say(string message)
        {
            if (DayEnded || Call != CallStatus.Active) return "Сейчас нет активного звонка.";
            if (string.IsNullOrWhiteSpace(message)) return "Введите реплику.";
            string text = message.Trim().ToLowerInvariant();
            if (text.Length > 500) return "Для прототипа сократите реплику до 500 символов.";

            // Prototype only: lexical triggers, not semantic understanding.
            if (ContainsAny(text, "гарант", "без риска", "без рисков", "безрисков"))
            {
                Trust = Math.Max(0, Trust - 1);
                if (Trust == 0)
                {
                    Call = CallStatus.Closed;
                    return "Борис: Без риска бывает только закрытая автомойка. До свидания.";
                }
                return "Борис: Гарантии? Уже подозрительно.";
            }
            if (ContainsAny(text, "цель", "целях", "нужно", "важно", "задач"))
            {
                if (!NeedsKnown) Trust = Math.Min(3, Trust + 1);
                NeedsKnown = true;
                return "Борис: Деньги нужны через год на оборудование. Терять их целиком я не готов.";
            }
            if (ContainsAny(text, "риск", "убыт", "потер"))
            {
                RisksExplained = true;
                return "Борис: Значит, убыток возможен. Хорошо, что сказали прямо.";
            }
            if (ContainsAny(text, "комис", "2%", "2 %"))
            {
                FeesExplained = true;
                return "Борис: Два процента комиссии, понял. Только без мелкого шрифта.";
            }
            return "Борис: Давайте по делу: зачем мне это, какие риски и сколько вы берёте?";
        }

        public string OfferDeal()
        {
            if (DayEnded || Call != CallStatus.Active) return "Сейчас нет активного звонка.";
            if (!NeedsKnown) return "Борис: Вы даже не спросили, зачем мне деньги.";
            if (!RisksExplained) return "Борис: Сначала расскажите, что может пойти не так.";
            if (!FeesExplained) return "Борис: А ваша комиссия какая?";
            if (Trust < 2) return "Борис: Пока не доверяю. Давайте закончим.";
            DealClosed = true;
            Cash += DealCommission;
            Call = CallStatus.Closed;
            return "Борис: Ладно, попробуем с небольшой суммой. Только без золотого унитаза за мой счёт.\nКомиссия конторы: +150. Сделка учебная и полностью вымышленная.";
        }

        public string EndCall()
        {
            if (DayEnded || Call != CallStatus.Active) return "Нет активного звонка.";
            Call = CallStatus.Closed;
            return "Борис: Ну, хоть трубку продать не попытались.";
        }

        public void AdvanceTime(float seconds)
        {
            if (DayEnded || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            RemainingSeconds = Math.Max(0f, RemainingSeconds - seconds);
            if (RemainingSeconds <= 0f) EndDay();
        }

        public void EndDay()
        {
            if (DayEnded) return;
            DayEnded = true;
            if (Call == CallStatus.Active) Call = CallStatus.Closed;
        }

        private static bool ContainsAny(string text, params string[] fragments)
        {
            foreach (string fragment in fragments)
                if (text.IndexOf(fragment, StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
