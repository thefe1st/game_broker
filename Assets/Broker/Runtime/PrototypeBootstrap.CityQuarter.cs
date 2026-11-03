using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    // The two open rooms of the city. The Exchange hall shows the day's quotes from StockMarket, the café the day's
    // small talk. Both only inform: no stock choice, money, trust, terms or bonus; the working clock stands meanwhile.
    public sealed partial class PrototypeBootstrap
    {
        private void DrawCityExchange()
        {
            Panel(160, 76, 1120, 808, ""); Label(200, 108, 1040, 48, "Зал Биржи / табло котировок", theme.Title);
            var rows = ExchangeBoard.Rows(day);
            if (rows.Count > 0)
            {
                Label(200, 168, 1040, 52, ExchangeBoard.Title(day) + " · цена на сегодня и изменение к вчерашнему дню. Компании вымышлены; бумагу клиенту выбирают за рабочим столом.", theme.Small);
                Label(224, 236, 360, 24, "БУМАГА", theme.Tag); Label(600, 236, 168, 24, "ЦЕНА", theme.Tag);
                Label(784, 236, 144, 24, "ЗА ДЕНЬ", theme.Tag); Label(944, 236, 296, 24, "РИСК · КОМИССИЯ · СРОК", theme.Tag);
                var stocks = StockCatalog.Offers;
                for (int i = 0; i < rows.Count; i++)
                {
                    var row = rows[i]; var stock = stocks[i]; float y = 276 + i * 68;
                    Paint(new Rect(200, y + 6, 6, 44), row.Direction > 0 ? theme.Green : row.Direction < 0 ? theme.Red : theme.Border);
                    Label(224, y, 360, 30, row.Ticker + " / " + row.Name, theme.Heading);
                    Label(224, y + 34, 360, 24, stock.Sector, theme.Small);
                    Label(600, y, 168, 32, row.Price, theme.Heading);
                    Label(784, y, 144, 32, row.Change, theme.Heading);
                    Label(944, y, 296, 56, stock.RiskLabel + "\n" + stock.FeePercent + "% · " + stock.HorizonMonths + " мес.", theme.Small);
                }
            }
            else
            {
                Label(200, 176, 1040, 64, ExchangeBoard.ClosedTitle, theme.Display);
                Label(200, 256, 1040, 80, "В этом режиме цены не моделируются: котировки идут в рабочих днях по найму и в своей конторе. Описания компаний — на стенде на площади.", theme.Small);
            }
            Label(200, 708, 700, 80, "Табло только для чтения: оно не выбирает бумагу, не меняет доверие, деньги и условия и не даёт бонуса. Пока открыт зал, рабочее время стоит.", theme.Small);
            if (Button(936, 796, 296, 48, "Назад · Esc", ApplicationActive)) CloseCityInformation();
        }
        private void DrawCityCafe()
        {
            Panel(280, 156, 880, 648, ""); Label(320, 188, 800, 48, "Кофейня «Бык и медведь»", theme.Title);
            Label(320, 248, 800, 28, "У СТОЙКИ ГОВОРЯТ / ДЕНЬ " + day.DayNumber, theme.Tag);
            var lines = CityChatter.For(day.DayNumber);
            for (int i = 0; i < lines.Length; i++) Label(320, 292 + i * 100, 800, 88, "— " + lines[i], theme.Text);
            Label(320, 604, 800, 80, "Здесь ничего не продают и не советуют бумаги: это атмосфера, а не подсказка. Денег, доверия и бонусов нет; пока вы слушаете, рабочее время стоит.", theme.Small);
            if (Button(320, 716, 800, 56, "Назад · Esc", ApplicationActive)) CloseCityInformation();
        }
    }
}
