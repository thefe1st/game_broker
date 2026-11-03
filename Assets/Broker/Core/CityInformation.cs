using System;
using System.Collections.ObjectModel;
using System.Text;

namespace Broker.Core
{
    public sealed class ExchangeQuote
    {
        public string Ticker { get; } public string Name { get; } public int PriceCents { get; } public int PreviousCents { get; }
        public string Price => StockMarket.Money(PriceCents);
        public string Change => StockMarket.Change(PreviousCents, PriceCents);
        public int Direction => Math.Sign(PriceCents - PreviousCents);
        internal ExchangeQuote(string ticker, string name, int price, int previous) { Ticker = ticker; Name = name; PriceCents = price; PreviousCents = previous; }
    }

    // Quotes board in the Exchange hall: the day's prices of the six fictional stocks and the change since yesterday,
    // from StockMarket. Only where the day has quotes (work days, own office); elsewhere trading is closed.
    // Read-only: it does not pick a stock, change trust, money or terms.
    public static class ExchangeBoard
    {
        public const string ClosedTitle = "ТОРГИ ЗАКРЫТЫ";
        public static ReadOnlyCollection<ExchangeQuote> Rows(BrokerDay day)
        {
            if (day == null || !day.HasQuotes) return Array.AsReadOnly(new ExchangeQuote[0]);
            var offers = StockCatalog.Offers; var rows = new ExchangeQuote[offers.Length];
            for (int i = 0; i < offers.Length; i++)
                rows[i] = new ExchangeQuote(offers[i].Ticker, offers[i].Name, day.QuoteCents(offers[i].Id), StockMarket.PriceCents(offers[i].Id, day.DayNumber - 1));
            return Array.AsReadOnly(rows);
        }
        public static string Title(BrokerDay day) => day != null && day.HasQuotes ? "КОТИРОВКИ ДНЯ " + day.DayNumber : ClosedTitle;
        // One column per line block for the board in the hall: tickers, prices, changes.
        public static string Column(BrokerDay day, int column)
        {
            var text = new StringBuilder();
            foreach (var row in Rows(day))
            {
                if (text.Length > 0) text.Append('\n');
                text.Append(column == 0 ? row.Ticker : column == 1 ? row.Price : row.Change);
            }
            return text.ToString();
        }
    }

    // Café small talk: three satirical lines a day from a fixed pool. No tickers, prices or advice — atmosphere only.
    public static class CityChatter
    {
        private static readonly string[] lines =
        {
            "Аналитик у окна третий день предсказывает разворот. Пока развернулся только его стул.",
            "Бык заказал двойной эспрессо, медведь — без кофеина. Спорят с самого открытия, кто из них прав.",
            "Брокер из соседнего офиса обещал клиенту «почти гарантию». Клиент попросил «почти чек».",
            "Капучино сегодня с пенкой повыше. Это не инфляция, это волатильность пенки.",
            "Посетитель спросил, куда вложить сдачу. Посоветовали в чаевые: риск низкий, доходность моральная.",
            "Новая примета у Биржи: если колонна блестит, её просто помыли.",
            "Стажёр выучил слово «диверсификация» и теперь берёт три разных круассана.",
            "Кто-то оставил на салфетке график. Бариста уверен, что это нарисованный кот.",
            "В очереди обсуждали «верняк». Бариста посоветовал сначала дочитать мелкий шрифт.",
            "Охранник Биржи говорит, что видел все кризисы. Пропустил только обеденный перерыв.",
            "Говорят, в зале новый трейдер: смотрит на табло так, будто оно ему должно.",
            "Медведь у стойки уверен, что всё упадёт. Пока упала только его ложка.",
            "Клиент спросил, можно ли купить акции кофейни. Можно только кофе, и то не сегодня.",
            "Утром кто-то кричал «на луну!». Оказалось, заказывал лунный пирог.",
            "Бариста ведёт свой индекс настроения: сегодня в зале два вздоха на один смех.",
        };
        public static int PoolSize => lines.Length;
        // Three different lines, the same for the same day number.
        public static string[] For(int dayNumber)
        {
            var result = new string[3]; int start = (int)(VoxelNoise.Hash(dayNumber, 13, 7, 83) % (uint)lines.Length);
            int stride = 1 + (int)(VoxelNoise.Hash(dayNumber, 17, 5, 89) % (uint)(lines.Length - 1));
            // With an odd pool size any stride from 1 to size−1 gives three different lines.
            for (int i = 0; i < 3; i++) result[i] = lines[(start + i * stride) % lines.Length];
            return result;
        }
    }
}
