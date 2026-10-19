namespace Broker.Core
{
    // Career stage and scenario length are separate concepts. This is an intro, not a sandbox cap.
    public enum CareerMode { Owner, Employee }

    public sealed partial class BrokerDay
    {
        public CareerMode Mode { get; }
        public bool IsEmployee => Mode == CareerMode.Employee;
        public const int PersonalCommissionPercent = 20;
        public int FirmRevenue { get; private set; }
        public int FirmRetained => IsEmployee ? FirmRevenue - Earned : FirmRevenue;
        public static int PersonalPayout(int commission) => commission * PersonalCommissionPercent / 100;
        public int SalesTarget => DayNumber; // Soft learning targets: 1 / 2 / 3. No fines or hard failure.
        public string EmploymentTitle => DayNumber == 1 ? "Ваш первый рабочий день"
            : DayNumber == 2 ? "План не звонит сам" : "Разбор первых результатов";
        public string SupervisorBrief => DayNumber == 1
            ? "Руководитель Орлов: Вы приняты брокером, не директором. Начните с одной сделки: выясните цель, раскройте риск и комиссию, ответьте на вопрос клиента. Вам — 20% комиссии фирмы. Аренду и зарплаты оплачивает фирма."
            : DayNumber == 2
                ? "Орлов: Сегодня ориентир — две сделки. Цифры нужны к вечеру, но вчерашние клиенты помнят обещания. Сначала обсудите отчёт. Давление и гарантии могут стоить доверия — план этого не отменяет."
                : "Орлов: Сегодня ориентир — три сделки. Вечером разберём вашу комиссию, отношения с клиентами и качество продаж. Это итоги вступления, не конец карьеры и не автоматическое открытие компании.";
        public string SupervisorFeedback => Reputation < 40
            ? "Орлов: Комиссия есть не только у сделки, но и у обещаний. Клиенты теряют доверие — начните с условий и рисков, а не с гарантий."
            : DealsCount >= SalesTarget
                ? "Орлов: Ориентир выполнен. Теперь важно, чтобы завтра клиент снова взял трубку."
                : "Орлов: Пока " + DealsCount + " из " + SalesTarget + ". Это ориентир, не штраф из личного кармана. Не закрывайте вопросы клиента красивыми обещаниями.";
        // Read-only guidance. It never selects a client or awards money by itself.
        public int RecommendedClientIndex
        {
            get
            {
                if (DayEnded) return -1;
                if (Call == CallStatus.Active) return SelectedClientIndex;
                for (int i = 0; i < Clients.Count; i++) if (Clients[i].Call == CallStatus.NotStarted) return i;
                return -1;
            }
        }
        public string EmploymentNextAction
        {
            get
            {
                if (DayEnded) return "День завершён. Посмотрите расчёт личной комиссии.";
                if (Call == CallStatus.Active) return Current.Name + ": " + NextConversationStep;
                int next = RecommendedClientIndex;
                if (next < 0) return "Все три звонка завершены. Подойдите к входной двери и подтвердите окончание дня.";
                string lead = DealsCount >= SalesTarget ? "Ориентир выполнен; можно продолжить. " : "";
                return lead + "За рабочим столом выберите «" + Clients[next].Name + "» и нажмите «Позвонить»."
                    + (Clients[next].Returning ? " Сначала обсудите вчерашний отчёт." : " Начните с цели клиента.");
            }
        }
        public string ColleagueAdvice(int index) => index == 0
            ? "Лена: Я такой же брокер, как вы. Откройте досье в звонке: Инне нужен годовой срок и письменный договор. Я не ваш аналитический отдел — поручения будут, когда появится своя команда."
            : "Макс: План давит, понимаю. Но «последний шанс» клиент слышал и вчера. Сначала выясните, что ему нужно. Борису оставьте резерв, Артёму — живой контакт после сделки.";
    }
}
