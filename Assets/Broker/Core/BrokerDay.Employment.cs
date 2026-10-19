namespace Broker.Core
{
    // Intro and repeatable work are separate modes; the intro length is not a career cap.
    public enum CareerMode { Owner, Employee, EmployeeWork }

    public sealed partial class BrokerDay
    {
        public CareerMode Mode { get; }
        public bool IsEmployee => Mode != CareerMode.Owner;
        public bool IsRepeatingWork => Mode == CareerMode.EmployeeWork;
        public const int PersonalCommissionPercent = 20;
        public int FirmRevenue { get; private set; }
        public int FirmRetained => IsEmployee ? FirmRevenue - Earned : FirmRevenue;
        public static int PersonalPayout(int commission) => commission * PersonalCommissionPercent / 100;
        public int SalesTarget => IsRepeatingWork ? 2 : DayNumber; // Soft learning targets: 1 / 2 / 3. No fines or hard failure.
        public string EmploymentTitle => IsRepeatingWork ? "Клиенты помнят разговор" : DayNumber == 1 ? "Ваш первый рабочий день"
            : DayNumber == 2 ? "План не звонит сам" : "Разбор первых результатов";
        public string SupervisorBrief => IsRepeatingWork
            ? "Орлов: Сегодня три контакта из постоянной базы. Ориентир — две сделки, без личных штрафов. После прошлой сделки сначала обсудите отчёт, даже если клиент пропустил несколько дней. Вам по-прежнему 20% комиссии; счета платит фирма."
            : DayNumber == 1
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
                    + (Clients[next].Returning ? (IsRepeatingWork ? " Сначала обсудите отчёт по прошлой сделке." : " Сначала обсудите вчерашний отчёт.") : " Начните с цели клиента.");
            }
        }
        public string ColleagueAdvice(int index) => IsRepeatingWork
            ? (index == 0 ? "Лена: Вере нужны письменные условия. Денису — годовой срок и резерв на печь. Посмотрите досье до предложения, не после отказа."
                : "Макс: Оксане нужны и сопровождение, и договор. А прошлый покупатель не забыл про отчёт, даже если его не было в списке несколько дней.")
            : index == 0
            ? "Лена: Я такой же брокер, как вы. Откройте досье в звонке: Инне нужен годовой срок и письменный договор. Я не ваш аналитический отдел — поручения будут, когда появится своя команда."
            : "Макс: План давит, понимаю. Но «последний шанс» клиент слышал и вчера. Сначала выясните, что ему нужно. Борису оставьте резерв, Артёму — живой контакт после сделки.";
    }
}
