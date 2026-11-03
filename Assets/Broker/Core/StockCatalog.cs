using System;

namespace Broker.Core
{
    public enum StockRisk { Moderate = 1, Growth = 2, Speculative = 3 }

    // Fictional offering terms. Daily quotes and per-risk fees for repeatable careers live in StockMarket.
    public sealed class StockOffer
    {
        public string Id { get; }
        public string Ticker { get; }
        public string Name { get; }
        public string Sector { get; }
        public string Description { get; }
        public StockRisk Risk { get; }
        public int HorizonMonths { get; }
        // v4 fee by risk for work days and the founder office; intro/owner modes keep StockCatalog.FeePercent.
        public int FeePercent => Risk == StockRisk.Moderate ? 2 : Risk == StockRisk.Growth ? 3 : 5;
        public string RiskLabel => Risk == StockRisk.Moderate ? "Умеренный" : Risk == StockRisk.Growth ? "Повышенный" : "Высокий / спекулятивный";
        public string RiskDisclosure => Risk == StockRisk.Moderate
            ? "Есть риск убытка: цена акций может упасть, дивиденды не гарантированы."
            : Risk == StockRisk.Growth ? "Повышенный риск убытка: рост бизнеса может не оправдать ожиданий, цена акций может резко упасть."
            : "Высокий риск убытка: можно потерять всю вложенную сумму; покупателя для продажи может не оказаться.";
        internal StockOffer(string id, string ticker, string name, string sector, string description, StockRisk risk, int months)
        { Id = id; Ticker = ticker; Name = name; Sector = sector; Description = description; Risk = risk; HorizonMonths = months; }
    }

    public static class StockCatalog
    {
        // Fixed fee of the intro/owner modes and of days imported from v1-v3 saves.
        public const int FeePercent = 2;
        private static readonly StockOffer[] offers = {
            new StockOffer("utility", "SVET", "Городской Свет", "Коммунальная инфраструктура", "Действующий бизнес с обычными контрактами. В буклете меньше ракет, зато есть расходы на ремонт.", StockRisk.Moderate, 12),
            new StockOffer("logistics", "RLOG", "Регион Логистика", "Склады и перевозки", "Работающая сеть складов. Зависит от загрузки и стоимости топлива, не от силы вашего голоса.", StockRisk.Moderate, 12),
            new StockOffer("medical", "MDAT", "МедДата", "Медицинское ПО", "Разрабатывает учёт для клиник. Акции роста: новые контракты в планах, не в кассе.", StockRisk.Growth, 12),
            new StockOffer("cloud", "OBLK", "Облако Систем", "Облачные сервисы", "Расширяет сервис для малого бизнеса. Выручка растёт вместе с затратами; прибыль не обещана.", StockRisk.Growth, 12),
            new StockOffer("ore", "NEDR", "Новые Недра", "Разведка месторождений", "Малая внебиржевая компания. Есть лицензия на разведку, но ещё нет добычи. Красивый участок на карте — не выручка.", StockRisk.Speculative, 36),
            new StockOffer("biotech", "BIMP", "БиоИмпульс", "Биотехнологии", "Малая внебиржевая компания с разработкой до испытаний. Презентация готова раньше продукта; успех и ликвидность не гарантированы.", StockRisk.Speculative, 36)
        };
        public static StockOffer[] Offers => (StockOffer[])offers.Clone();
        public static StockOffer Find(string id) => Array.Find(offers, stock => stock.Id == id);
        public static string DefaultFor(string clientId) => clientId == "boris" || clientId == "denis" ? "logistics"
            : clientId == "vera" ? "utility" : clientId == "inna" ? "medical" : "cloud";
        public static StockRisk MaxRiskFor(string clientId) => clientId == "boris" || clientId == "vera" || clientId == "denis"
            ? StockRisk.Moderate : clientId == "artem" ? StockRisk.Speculative : StockRisk.Growth;
        public static int MaxHorizonFor(string clientId) => clientId == "artem" ? 36 : 12;
        public static string Suitability(string clientId, StockOffer stock)
        {
            if (stock == null || ClientCatalog.Index(clientId) < 0) return "Предложение не найдено.";
            if (stock.Risk > MaxRiskFor(clientId)) return "Клиент не готов к такому риску. Выберите менее рискованную акцию; давление и досье не заменяют согласие.";
            if (stock.HorizonMonths > MaxHorizonFor(clientId)) return "Горизонт бумаги не соответствует цели клиента. Выберите предложение до " + MaxHorizonFor(clientId) + " месяцев.";
            return "Соответствует игровому лимиту риска и горизонта. Это не гарантия успеха и не финансовая рекомендация.";
        }
        public static bool Fits(string clientId, StockOffer stock) => stock != null && ClientCatalog.Index(clientId) >= 0
            && stock.Risk <= MaxRiskFor(clientId) && stock.HorizonMonths <= MaxHorizonFor(clientId);
    }
}
