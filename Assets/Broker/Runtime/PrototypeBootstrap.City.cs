using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private void DrawCityPanel(bool pickup)
        {
            Panel(200, 100, 1040, 760, "");
            Label(240, 132, 960, 28, "КВАРТАЛ / ВЫБОР НА ПАУЗЕ", theme.Tag);
            Label(240, 180, 960, 56, pickup ? "ПЕЧАТЬ / ОБЕДЫ" : "БЮРО КВАРТАЛА", theme.Title);
            Label(240, 252, 960, 72, pickup ? "Павильон: Печатаем договоры, греем обеды. Желательно не одновременно." : day.CityNeighbourLine, theme.Text);
            bool canAct = office.IsInCity && day.Call != CallStatus.Active && !day.DayEnded && day.PendingEvent == null;
            if (!pickup && day.CityStage == CityWorkStage.Available)
            {
                CityWorkKind[] kinds = { CityWorkKind.Courier, CityWorkKind.Interview, CityWorkKind.TeamLunch };
                for (int i = 0; i < kinds.Length; i++)
                {
                    var kind = kinds[i]; float y = 344 + i * 104;
                    Label(240, y, 600, 32, BrokerDay.CityWorkName(kind), theme.Heading);
                    Label(240, y + 38, 600, 36, BrokerDay.CityWorkTerms(kind), theme.Small);
                    if (Button(872, y + 8, 328, 56, "Выбрать", canAct && day.Cash >= BrokerDay.CityWorkCost(kind), true))
                    { News(day.StartCityWork(kind)); EnterWorld(); }
                }
            }
            else
            {
                Label(240, 348, 960, 100, day.CityWorkStatus, theme.Heading);
                Label(240, 476, 960, 52, day.CityWorkTaken ? BrokerDay.CityWorkTerms(day.CityWork) : "Сначала выберите дело в бюро рядом с лифтом.", theme.Small);
                bool available = pickup ? day.CityStage == CityWorkStage.ReadyForPickup : day.CityStage == CityWorkStage.Carrying;
                if (Button(240, 560, 960, 64, pickup ? "Забрать заказ" : "Сдать дело и получить награду", canAct && available, true))
                { News(pickup ? day.PickupCityWork() : day.CompleteCityWork()); EnterWorld(); }
            }
            Label(240, 688, 960, 64, day.Call == CallStatus.Active ? "Сначала закончите активный звонок за рабочим столом."
                : "Одно дело в день. Подготовка идёт только в квартале. Заберите и сдайте до конца дня. Оплата не возвращается.", theme.Small);
            if (Button(240, 780, 960, 48, "Вернуться · Esc")) EnterWorld();
        }
    }
}
