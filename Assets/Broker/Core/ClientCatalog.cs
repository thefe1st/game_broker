using System;

namespace Broker.Core
{
    // Stable identities; three appointments a day from six people, not six simultaneous calls.
    public static class ClientCatalog
    {
        private static readonly string[] ids = { "boris", "inna", "artem", "vera", "denis", "oksana" };
        public static string[] Ids => (string[])ids.Clone();
        public static int Index(string id) => Array.IndexOf(ids, id);
        public static bool NeedsYear(string id) => id == "inna" || id == "denis";
        public static bool NeedsService(string id) => id == "artem" || id == "oksana";
        public static string[] Appointments(int day)
        {
            if (day < 4) throw new ArgumentOutOfRangeException(nameof(day));
            switch ((day - 4) % 4)
            {
                case 0: return new[] { "boris", "vera", "denis" };
                case 1: return new[] { "inna", "oksana", "vera" };
                case 2: return new[] { "artem", "denis", "oksana" };
                default: return new[] { "boris", "inna", "artem" };
            }
        }
        public static ClientRecord Create(string id, bool employee = true)
        {
            switch (id)
            {
                case "boris": return new ClientRecord(id, "Борис", "Владелец трёх автомоек", "Оборудование через год. Не любит жаргон и боится вложить всё. Нужен резерв для бизнеса.", 150, 1);
                case "inna": return new ClientRecord(id, "Инна", "Бывший финансовый директор", employee ? "Проверяет цифры, годовой срок и письменные условия. Раскройте условия и ответьте на вопрос о договоре." : "Проверяет цифры, годовой срок и письменные условия. Подготовка досье поможет доверию.", 220, 0);
                case "artem": return new ClientRecord(id, "Артём", "Наследник с большими планами", "Хочет персональный отчёт и живой контакт после сделки. Обещания эксклюзивной доходности не помогут.", 180, 2);
                case "vera": return new ClientRecord(id, "Вера", "Владелица стоматологии", "Размещает свободную часть выручки. После разговора хочет письменные условия: комиссия и риск должны остаться в договоре, не только в вашей памяти.", 200, 1);
                case "denis": return new ClientRecord(id, "Денис", "Владелец пекарни", "Копит на вторую печь через год. Требует назвать годовой срок, затем оставить резерв для ремонта первой печи. Не готов вкладывать всё.", 170, 1);
                case "oksana": return new ClientRecord(id, "Оксана", "Независимый дизайнер", "Неровный доход, не хочет потеряться после сделки. Нужны персональное сопровождение и письменный договор. Красивые презентации она делает сама.", 190, 1);
                default: throw new ArgumentException("Unknown client identity", nameof(id));
            }
        }
    }
}
