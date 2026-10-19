using System;

namespace Broker.Core
{
    public enum CityWorkKind { None, Courier, Interview, TeamLunch }
    public enum CityWorkStage { Available, Preparing, ReadyForPickup, Carrying, Completed, Expired }

    // Daily, engine-free state machine. No provider or wall-clock time can grant rewards.
    public sealed partial class BrokerDay
    {
        public CityWorkKind CityWork { get; private set; }
        public CityWorkStage CityStage { get; private set; }
        public float CityWorkRemaining { get; private set; }
        public bool CityWorkTaken => CityWork != CityWorkKind.None;
        public string CityWorkTitle => CityWorkName(CityWork);
        public string CityNeighbourLine => DayNumber == 1
            ? "Вера: Холдинг? Отлично. У нас как раз некому забрать бумаги из павильона."
            : DayNumber == 2 ? "Вера: Вас уже знают. Пока в основном курьер и бухгалтер. Это тоже аудитория."
            : "Вера: Последний рывок? Тогда выбирайте: деньги, доброе имя или сытые сотрудники.";
        public static string CityWorkName(CityWorkKind kind)
        {
            switch (kind)
            {
                case CityWorkKind.Courier: return "Бумаги важнее галстука";
                case CityWorkKind.Interview: return "Не обещаем Луну";
                case CityWorkKind.TeamLunch: return "Обед — не прогноз";
                default: return "Дела квартала";
            }
        }
        public static int CityWorkCost(CityWorkKind kind) => kind == CityWorkKind.TeamLunch ? 30 : 0;
        public static int CityWorkDuration(CityWorkKind kind) => kind == CityWorkKind.Courier ? 60
            : kind == CityWorkKind.Interview ? 45 : kind == CityWorkKind.TeamLunch ? 30 : 0;
        public static string CityWorkTerms(CityWorkKind kind)
        {
            switch (kind)
            {
                case CityWorkKind.Courier: return "60 с в квартале · бесплатно · награда $65";
                case CityWorkKind.Interview: return "45 с в квартале · бесплатно · +8 репутации";
                case CityWorkKind.TeamLunch: return "30 с в квартале · цена $30 сразу · +20 настроения";
                default: return "Выберите одно дело на день.";
            }
        }
        public string CityWorkStatus
        {
            get
            {
                switch (CityStage)
                {
                    case CityWorkStage.Preparing: return CityWorkTitle + ": подготовка " + (int)Math.Ceiling(CityWorkRemaining) + " с. Время идёт только в квартале.";
                    case CityWorkStage.ReadyForPickup: return "Готово! Заберите заказ в павильоне «ПЕЧАТЬ / ОБЕДЫ» у левой клумбы.";
                    case CityWorkStage.Carrying: return "Заказ у вас. Вернитесь в «БЮРО КВАРТАЛА» рядом с лифтом за наградой.";
                    case CityWorkStage.Completed: return CityWorkTitle + ": завершено. Следующее дело — завтра.";
                    case CityWorkStage.Expired: return CityWorkTitle + ": день закончился, награды нет. Оплата не возвращается.";
                    default: return "Одно дело на день: бюро квартала рядом с уличным лифтом.";
                }
            }
        }
        private bool CanChangeCityWork => !DayEnded && PendingEvent == null && Call != CallStatus.Active;
        public string StartCityWork(CityWorkKind kind)
        {
            if (!CanChangeCityWork) return "Сначала завершите звонок или текущее происшествие.";
            if (kind == CityWorkKind.None || !Enum.IsDefined(typeof(CityWorkKind), kind)) return "Нет такого дела.";
            if (CityStage != CityWorkStage.Available) return "Сегодня дело уже выбрано. " + CityWorkStatus;
            int cost = CityWorkCost(kind);
            if (Cash < cost) return "Для заказа обедов нужно $30 в кассе.";
            Spend(cost); CityWork = kind; CityStage = CityWorkStage.Preparing; CityWorkRemaining = CityWorkDuration(kind);
            return "Вера: Заявку приняли. " + CityWorkTerms(kind) + ". Затем заберите заказ в павильоне и принесите в бюро до конца дня. Оплата не возвращается.";
        }
        private void AdvanceCityActivity(float seconds, bool inCity)
        {
            if (!inCity || CityStage != CityWorkStage.Preparing || Call == CallStatus.Active) return;
            CityWorkRemaining = Math.Max(0, CityWorkRemaining - seconds);
            if (CityWorkRemaining > 0) return;
            CityStage = CityWorkStage.ReadyForPickup;
            Notify("Павильон: Заказ готов. " + CityWorkStatus);
        }
        public string PickupCityWork()
        {
            if (!CanChangeCityWork) return "Сейчас забрать заказ нельзя.";
            if (CityStage != CityWorkStage.ReadyForPickup) return CityWorkStatus;
            CityStage = CityWorkStage.Carrying;
            return (CityWork == CityWorkKind.Courier ? "Вы забрали пакет документов. Печать «срочно» почти высохла. "
                : CityWork == CityWorkKind.Interview ? "Вы забрали выпуск газеты с честным интервью: «Доходность не обещаем, трубку берём». "
                : "Вы забрали обеды. На контейнерах написано «активы». Не перепутайте с бумагами. ") + CityWorkStatus;
        }
        public string CompleteCityWork()
        {
            if (!CanChangeCityWork) return "Сейчас сдать дело нельзя.";
            if (CityStage != CityWorkStage.Carrying) return CityWorkStatus;
            // Set the terminal state before rewards. Repeated interactions cannot pay twice.
            CityStage = CityWorkStage.Completed;
            if (CityWork == CityWorkKind.Courier) { Credit(65); return "Вера: Документы доставлены. +$65. Теперь у холдинга есть логистическое подразделение. Это вы."; }
            if (CityWork == CityWorkKind.Interview) { Reputation = Math.Min(100, Reputation + 8); return "Вера: Выпуск получен. +8 репутации (до 100). Впервые солидность подтверждена не золотой краской."; }
            ChangeMorale(20);
            return "Вера отправила обеды в офис. +20 настроения обоим сотрудникам (до 100). Финансовый голод пока остался.";
        }
        private void ExpireCityActivity()
        {
            if (CityStage != CityWorkStage.Preparing && CityStage != CityWorkStage.ReadyForPickup && CityStage != CityWorkStage.Carrying) return;
            CityStage = CityWorkStage.Expired; CityWorkRemaining = 0;
            Notify("Бюро квартала: День закрыт. Незавершённое дело не оплачено; стоимость обедов не возвращается.");
        }
    }
}
