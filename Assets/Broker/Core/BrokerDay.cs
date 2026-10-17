using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;

namespace Broker.Core
{
    public enum CallStatus { NotStarted, Active, Closed }

    public sealed class BrokerDay
    {
        public const int Rent = 100;
        public const int Payroll = 240;
        public const int DealCommission = 150;
        public const float DurationSeconds = 600f;
        private readonly List<ClientRecord> clients;
        private readonly List<EmployeeRecord> employees;
        private readonly Queue<string> notifications = new Queue<string>();
        private int eventIndex;
        private int banterIndex;
        private int extraExpenses;
        public ReadOnlyCollection<ClientRecord> Clients { get; }
        public ReadOnlyCollection<EmployeeRecord> Employees { get; }
        public int DayNumber { get; }
        public int OpeningCash { get; }
        public int CarryDebt { get; }
        public int DailyRent => Rent + (DayNumber - 1) * 20;
        public int DailyPayroll => Payroll + (DayNumber - 1) * 20;
        public string DayTitle => DayNumber == 1 ? "Большие амбиции" : DayNumber == 2 ? "Вам уже перезванивают" : "Проверка на солидность";
        public int SelectedClientIndex { get; private set; }
        public ClientRecord Current => clients[SelectedClientIndex];
        public int Cash { get; private set; }
        public int Earned { get; private set; }
        public int Spent { get; private set; }
        public int Reputation { get; private set; } = 50;
        public float RemainingSeconds { get; private set; } = DurationSeconds;
        public bool DayEnded { get; private set; }
        public bool HasGoldenPhone { get; private set; }
        public bool CoffeeBought { get; private set; }
        public OfficeEvent PendingEvent { get; private set; }
        public int Trust => Current.Trust;
        public bool NeedsKnown => Current.NeedsKnown;
        public bool RisksExplained => Current.RisksExplained;
        public bool FeesExplained => Current.FeesExplained;
        public bool DealClosed => Current.DealClosed;
        public CallStatus Call => Current.Call;
        public bool RentCovered => Cash >= DailyRent;
        public int BalanceAfterRent => Cash - DailyRent;
        public int TotalExpenses => DailyRent + DailyPayroll + CarryDebt + extraExpenses;
        public int FinalBalance => Cash - TotalExpenses;
        public bool Successful => FinalBalance >= 0;
        public int DealsCount { get { int total = 0; foreach (var c in clients) if (c.DealClosed) total++; return total; } }
        public int TeamMorale => (employees[0].Morale + employees[1].Morale) / 2;

        public BrokerDay(int dayNumber = 1, int openingCash = 0, int reputation = 50, bool goldenPhone = false,
            int[] relationshipBonuses = null, bool[] returning = null, int carryDebt = 0)
        {
            if (dayNumber < 1 || dayNumber > 3 || openingCash < 0 || openingCash > 100000 || reputation < 0 || reputation > 100
                || carryDebt < 0 || carryDebt > 100000 || (relationshipBonuses != null && relationshipBonuses.Length != 3)
                || (returning != null && returning.Length != 3)) throw new ArgumentException("Invalid day opening state");
            DayNumber = dayNumber; OpeningCash = openingCash; Cash = openingCash; CarryDebt = carryDebt;
            Reputation = reputation; HasGoldenPhone = goldenPhone;
            clients = new List<ClientRecord> {
                new ClientRecord("boris", "Борис", "Владелец трёх автомоек", "Оборудование через год. Не любит сложные слова. Честность ценит больше презентации.", 150, 1),
                new ClientRecord("inna", "Инна", "Бывший финансовый директор", "Проверяет каждую цифру. Нужно обсудить срок вложения: один год. Подготовка досье поможет доверию.", 220, 0),
                new ClientRecord("artem", "Артём", "Наследник с большими планами", "Хочет личного внимания. Расскажи о персональном отчёте, а не обещай эксклюзивную доходность.", 180, 2)
            };
            for (int i = 0; i < clients.Count; i++)
            {
                int bonus = relationshipBonuses == null ? 0 : Math.Max(-1, Math.Min(2, relationshipBonuses[i]));
                clients[i].Trust = Math.Max(0, Math.Min(4, clients[i].Trust + bonus + (goldenPhone ? 1 : 0)));
                clients[i].Returning = returning != null && returning[i];
                clients[i].ReportReviewed = !clients[i].Returning;
            }
            employees = new List<EmployeeRecord> { new EmployeeRecord("Лена", "Досье за 20 секунд. Верит в порядок, но не в ваш галстук."), new EmployeeRecord("Макс", "Побочные продажи приносят 55. Энтузиазма больше, чем осторожности.") };
            Clients = clients.AsReadOnly(); Employees = employees.AsReadOnly();
            Notify("Лена: День " + DayNumber + ". На обязательства нужно $" + TotalExpenses + ". На визитках у нас уже холдинг.");
            if (CarryDebt > 0) Notify("Бухгалтерия: Долг $" + CarryDebt + " перенесён в счёт этого дня. Второй раз из кассы его не списываем.");
        }

        public bool SelectClient(int index)
        {
            if (DayEnded || PendingEvent != null || Call == CallStatus.Active || index < 0 || index >= clients.Count) return false;
            SelectedClientIndex = index; return true;
        }
        public string StartCall()
        {
            if (DayEnded || PendingEvent != null) return "Сначала разберитесь с текущими делами.";
            if (Call != CallStatus.NotStarted) return "Этот звонок уже начат или завершён. Выберите другого клиента.";
            Current.Call = CallStatus.Active;
            if (Current.Returning)
                return Current.Name + ": Снова вы. Прошлый разговор помню: сначала отчёт, потом новые предложения.";
            if (Current.Id == "inna") return "Инна: Давайте без вступления. Комиссия, риски, срок — именно в таком порядке.";
            if (Current.Id == "artem") return "Артём: А у вас есть что-нибудь для особенных клиентов? Только не называйте меня обычным.";
            return "Борис: У меня три автомойки. Чем ваши бумажки лучше четвёртой?";
        }
        public string Say(string message)
        {
            if (DayEnded || Call != CallStatus.Active || PendingEvent != null) return "Сейчас разговор недоступен.";
            if (string.IsNullOrWhiteSpace(message)) return "Введите реплику.";
            string text = message.Trim().ToLowerInvariant();
            if (text.Length > 500) return "Для прототипа сократите реплику до 500 символов.";
            bool negated = Regex.IsMatch(text, @"не\s+гарантир\w*|никаких\s+гарантий|без\s+гарантий|не\s+обещаю\s+гарант\w*");
            bool guarantee = Regex.IsMatch(text, @"гарантир\w*|гарантирован\w*|без\s+риска|безрисков\w*|риска\s+нет|нет\s+риска");
            // Still lexical, not a semantic classifier. Known negation examples are supported.
            if (guarantee && !negated)
            {
                Current.Trust = Math.Max(0, Trust - 1); Reputation = Math.Max(0, Reputation - 10);
                if (Trust == 0) { Current.Call = CallStatus.Closed; return Current.Name + ": Гарантии без риска? Спасибо, на сегодня достаточно."; }
                return Current.Name + ": Уже подозрительно. Давайте без гарантий.";
            }
            var answers = new List<string>();
            if (Current.Returning && Has(text, "отчёт", "отчет", "результат", "динамик", "итог"))
            { Current.ReportReviewed = true; answers.Add("Обсудили отчёт по прошлому разговору. Теперь можно к новым условиям."); }
            if (Has(text, "цель", "целях", "нужно", "важно", "задач"))
            {
                if (!NeedsKnown) Current.Trust = Math.Min(4, Trust + 1);
                Current.NeedsKnown = true;
                answers.Add(Current.Id == "boris" ? "Деньги нужны через год на оборудование. Всю сумму терять не готов." : Current.Id == "inna" ? "Часть свободного капитала. Без обещаний чудес и с понятным сроком." : "Хочу личное сопровождение, а не рассылку «уважаемый клиент»." );
            }
            if (Has(text, "риск", "убыт", "потер")) { Current.RisksExplained = true; answers.Add("Риск убытка услышал" + (Current.Id == "inna" ? "а" : "") + ". Продолжайте."); }
            if (Regex.IsMatch(text, @"(?<![\d.,-])2\s*%|два\s+процента")) { Current.FeesExplained = true; answers.Add("Комиссия 2%. Хорошо, что не пришлось искать лупу."); }
            if (Current.Id == "inna" && Has(text, "год", "12 месяц", "срок"))
            {
                if (!Current.SpecialDiscussed) Current.Trust = Math.Min(4, Trust + 1);
                Current.SpecialDiscussed = true; answers.Add("Год — понятный срок. Теперь к сути предложения.");
            }
            if (Current.Id == "artem" && Has(text, "персональн", "личн", "отчёт", "отчет"))
            { Current.SpecialDiscussed = true; answers.Add("Персональный отчёт? Уже лучше. Только имя не перепутайте."); }
            return Current.Name + ": " + (answers.Count > 0 ? string.Join(" ", answers) : "Мне нужны конкретные условия: цель, риск и комиссия. Красивые слова оставьте для вывески.");
        }
        public string OfferDeal()
        {
            if (DayEnded || Call != CallStatus.Active || PendingEvent != null) return "Сейчас нет доступного звонка.";
            if (!Current.ReportReviewed) return Current.Name + ": Сначала обсудим отчёт по прошлому разговору.";
            if (!NeedsKnown) return Current.Name + ": Вы даже не спросили, зачем мне это.";
            if (!RisksExplained) return Current.Name + ": Сначала расскажите, что может пойти не так.";
            if (!FeesExplained) return Current.Name + ": А ваша комиссия какая?";
            if (Current.Id == "inna" && !Current.SpecialDiscussed) return "Инна: А срок вложения мы так и не обсудили.";
            if (Current.Id == "artem" && !Current.SpecialDiscussed) return "Артём: Где обещанное личное внимание? Расскажите о персональном отчёте.";
            if (Trust < 2) return Current.Name + ": Пока не доверяю. Подготовьтесь лучше.";
            Current.DealClosed = true; Current.Call = CallStatus.Closed;
            Credit(Current.Commission); Reputation = Math.Min(100, Reputation + 5);
            Notify("Бухгалтерия: +" + Current.Commission + " комиссии. Сделок: " + DealsCount + "/3.");
            return Current.Name + ": Договорились, начнём с небольшой суммы. Но за обещания спрошу.\nКомиссия: +" + Current.Commission + ". Все деньги и продукты вымышлены.";
        }
        public string EndCall()
        {
            if (DayEnded || Call != CallStatus.Active) return "Нет активного звонка.";
            Current.Call = CallStatus.Closed;
            return Current.Name + ": Ладно, на этом всё. Хотя трубку вы тоже могли бы продавать.";
        }
        public string AssignTask(int workerIndex, WorkKind work, int clientIndex)
        {
            if (DayEnded || PendingEvent != null || workerIndex < 0 || workerIndex >= employees.Count
                || clientIndex < 0 || clientIndex >= clients.Count || work == WorkKind.Idle || !Enum.IsDefined(typeof(WorkKind), work)) return "Поручение недоступно.";
            var worker = employees[workerIndex];
            if (worker.Work != WorkKind.Idle) return worker.Name + ": Сначала закончу текущее.";
            if (work == WorkKind.Research && clients[clientIndex].Researched) return "Досье уже готово.";
            if (work == WorkKind.Leads && worker.SideDealDone) return worker.Name + ": Моя побочная сделка сегодня уже закрыта.";
            worker.Work = work; worker.TargetClient = clientIndex;
            worker.Duration = work == WorkKind.Research ? (workerIndex == 0 ? 20 : 35) : work == WorkKind.Leads ? 40 : 18;
            if (work != WorkKind.Break && worker.Morale < 40) worker.Duration *= 1.5f;
            worker.Remaining = worker.Duration;
            worker.Morale = Math.Max(0, worker.Morale - (work == WorkKind.Break ? 0 : 6));
            return worker.Name + (work == WorkKind.Research ? ": Готовлю досье на клиента «" + clients[clientIndex].Name + "»." : work == WorkKind.Leads ? ": Обзвоню старые контакты. Один раз, не до бесконечности." : ": Ушла" + (workerIndex == 1 ? "… ладно, ушёл" : "") + " на перерыв.");
        }
        public string BuyCoffee()
        {
            if (DayEnded || PendingEvent != null || CoffeeBought || Cash < 40) return "Кофе недоступен: нужен 40, покупка один раз за день.";
            Spend(40); CoffeeBought = true; ChangeMorale(20); return "Команда получила кофе. -40 денег, +20 настроения. Теперь хотя бы бодро паникуем.";
        }
        public string BuyGoldenPhone()
        {
            if (DayEnded || PendingEvent != null || HasGoldenPhone || Cash < 90) return "Телефон недоступен: нужен 90, покупка один раз.";
            Spend(90); HasGoldenPhone = true;
            foreach (var c in clients) if (c.Call != CallStatus.Closed) c.Trust = Math.Min(4, c.Trust + 1);
            return "Куплен золотистый телефон. -90 денег, +1 доверия незавершённых клиентов. Слышно по-прежнему так себе.";
        }
        public void AdvanceTime(float seconds)
        {
            if (DayEnded || PendingEvent != null || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            float actual = Math.Min(seconds, RemainingSeconds);
            RemainingSeconds = Math.Max(0, RemainingSeconds - actual);
            foreach (var worker in employees)
            {
                if (worker.Work == WorkKind.Idle) continue;
                worker.Remaining = Math.Max(0, worker.Remaining - actual);
                if (worker.Remaining > 0) continue;
                if (worker.Work == WorkKind.Research)
                {
                    var client = clients[worker.TargetClient];
                    if (!client.Researched) { client.Researched = true; client.Trust = Math.Min(4, client.Trust + 1); }
                    Notify(worker.Name + ": Досье «" + client.Name + "» готово. +1 доверия (один раз). " + client.Brief);
                }
                else if (worker.Work == WorkKind.Leads)
                {
                    int amount = (worker.Name == "Макс" ? 55 : 35) + (worker.Morale >= 75 ? 15 : 0);
                    worker.SideDealDone = true; Credit(amount);
                    Notify(worker.Name + ": Побочная сделка закрыта. +" + amount + " комиссии. Основных клиентов не трогаю.");
                }
                else { worker.Morale = Math.Min(100, worker.Morale + 15); Notify(worker.Name + ": Перерыв окончен. +15 настроения."); }
                worker.Work = WorkKind.Idle;
            }
            if (RemainingSeconds <= 0) { EndDay(); return; }
            MaybeEvent();
            if (PendingEvent == null) OfficeBanter();
        }
        private void OfficeBanter()
        {
            float elapsed = DurationSeconds - RemainingSeconds;
            float[] thresholds = { 30, 100, 230, 400 };
            string[] lines = {
                "Макс: В резюме написал «стрессоустойчивый». Можно вычеркнуть?",
                "Лена: Скрепки закончились. Пока не обещайте клиентам обеспеченные бумаги.",
                "Макс: Клиент спросил, где наш аналитический отдел. Я сказал: вышел за кофе.",
                "Лена: Зарплата сегодня — это план или тоже прогноз доходности?"
            };
            if (banterIndex < thresholds.Length && elapsed >= thresholds[banterIndex]) Notify(lines[banterIndex++]);
        }
        private void MaybeEvent()
        {
            float elapsed = DurationSeconds - RemainingSeconds;
            if (DayNumber > 1) { LaterDayEvent(elapsed); return; }
            if (eventIndex == 0 && (elapsed >= 60 || DealsCount >= 1))
                PendingEvent = new OfficeEvent(0, "Кофемашина объявила дефолт", "Макс стучит по кофемашине дипломом. Лена предлагает вызвать мастера.", "Ремонт: -45 денег, +12 настроения", "Пусть пьют воду: -12 настроения", 45);
            else if (eventIndex == 1 && (elapsed >= 180 || DealsCount >= 2))
                PendingEvent = new OfficeEvent(1, "Макс продал слишком красивое будущее", "Старый контакт жалуется на вчерашнее обещание Макса. Нужен ответ руководителя.", "Компенсация: -60 денег, +8 репутации", "Замять историю: -15 репутации", 60);
            else if (eventIndex == 2 && (elapsed >= 300 || DealsCount >= 3))
                PendingEvent = new OfficeEvent(2, "Арендодатель заметил слово «холдинг»", "Он требует дополнительный депозит. Видимо, прочитал ваши визитки.", "Заплатить сейчас: -80 денег", "Отсрочка: +100 к расходам в конце дня", 80);
        }
        private void LaterDayEvent(float elapsed)
        {
            if (eventIndex == 0 && (elapsed >= 60 || DealsCount >= 1))
                PendingEvent = new OfficeEvent(0, DayNumber == 2 ? "Команда просит нормальный обед" : "Утро последнего рывка",
                    DayNumber == 2 ? "Лена предлагает накормить команду. Макс уже назвал сухарики корпоративным питанием." : "Команда ждёт знака внимания перед финальным днём. Речь про светлое будущее уже слышали.",
                    "Поддержать команду: -45 денег, +12 настроения", "Обойтись речью: -12 настроения", 45);
            else if (eventIndex == 1 && (elapsed >= 180 || DealsCount >= 2))
                PendingEvent = new OfficeEvent(1, DayNumber == 2 ? "Вчерашний контакт требует объяснений" : "Проверка громких обещаний",
                    "Старый рекламный текст Макса снова всплыл. Можно оплатить исправление и извиниться или сделать вид, что это не ваша визитка.",
                    "Исправить и извиниться: -60 денег, +8 репутации", "Замять историю: -15 репутации", 60);
            else if (eventIndex == 2 && (elapsed >= 300 || DealsCount >= 3))
                PendingEvent = new OfficeEvent(2, DayNumber == 2 ? "Счёт за корпоративную связь" : "Последний дополнительный счёт",
                    "Поставщик хочет деньги сейчас. Отсрочка возможна, но выйдет дороже к закрытию дня.",
                    "Заплатить сейчас: -80 денег", "Отсрочка: +100 к расходам в конце дня", 80);
        }
        public string ResolveEvent(int choice)
        {
            if (DayEnded || PendingEvent == null || choice < 0 || choice > 1) return "Нет такого решения.";
            int id = PendingEvent.Id;
            int cost = choice == 0 ? PendingEvent.FirstChoiceCost : 0;
            if (Cash < cost) return "Не хватает денег. Выберите бесплатное решение.";
            Spend(cost);
            if (id == 0) ChangeMorale(choice == 0 ? 12 : -12);
            if (id == 1) Reputation = Math.Max(0, Math.Min(100, Reputation + (choice == 0 ? 8 : -15)));
            if (id == 2 && choice == 1) extraExpenses += 100;
            PendingEvent = null; eventIndex++;
            return "Решение принято. " + (cost > 0 ? "Потрачено " + cost + "." : id == 2 ? "В конце дня счёт будет выше на 100." : "Сэкономили деньги. Последствия остались.");
        }
        public void EndDay()
        {
            if (DayEnded) return;
            DayEnded = true; PendingEvent = null;
            foreach (var client in clients) if (client.Call == CallStatus.Active) client.Call = CallStatus.Closed;
            foreach (var worker in employees) worker.Work = WorkKind.Idle;
        }
        public string PopNotification() { return notifications.Count == 0 ? null : notifications.Dequeue(); }
        private void Notify(string text) { notifications.Enqueue(text); }
        private void Credit(int amount) { Cash += amount; Earned += amount; }
        private void Spend(int amount) { Cash -= amount; Spent += amount; }
        private void ChangeMorale(int amount) { foreach (var e in employees) e.Morale = Math.Max(0, Math.Min(100, e.Morale + amount)); }
        private static bool Has(string text, params string[] parts) { foreach (var part in parts) if (text.IndexOf(part, StringComparison.Ordinal) >= 0) return true; return false; }
    }
}
