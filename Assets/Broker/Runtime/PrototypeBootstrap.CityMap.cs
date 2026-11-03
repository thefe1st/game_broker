using System;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private string cityDestinationId = "office";
        private bool cityMapFromPause;
        private void DrawCityMap()
        {
            Panel(160, 92, 1120, 780, ""); Label(200, 124, 1040, 48, "Район / пешая навигация", theme.Title);
            Label(200, 184, 1040, 60, "Север — бульвар. Сквер на западе, офисная улица на востоке. Площадь соединяет обе стороны. Проезжая часть отделена; входы в декорационные здания не открыты.", theme.Small);
            Panel(200, 268, 432, 380, "");
            Label(224, 288, 384, 28, "СЕВЕР / БУЛЬВАР", theme.Tag);
            Paint(new Rect(224, 336, 352, 160), theme.Border);
            Paint(new Rect(332, 416, 136, 80), Broker.Runtime.Office.BrokerTheme.Hex("#0F1B2D"));
            Paint(new Rect(244, 456, 72, 32), theme.Gold);
            Paint(new Rect(484, 456, 72, 32), theme.Gold);
            Label(236, 520, 116, 48, "ЗАПАД\nСквер", theme.Small);
            Label(360, 520, 112, 48, "Площадь\nОфис", theme.Small);
            Label(488, 520, 112, 48, "ВОСТОК\nОфисы", theme.Small);
            var selected = CityDistrictPlan.Find(cityDestinationId);
            Paint(new Rect(224 + (selected.X - CityDistrictPlan.MinX) * 4 - 4, 336 + (CityDistrictPlan.MaxZ - selected.Z) * 4 - 4, 8, 8), theme.Accent);
            if (office.IsInCity) Paint(new Rect(224 + (office.MapX - CityDistrictPlan.MinX) * 4 - 3, 336 + (CityDistrictPlan.MaxZ - office.MapZ) * 4 - 3, 6, 6), theme.Ink);
            Label(224, 592, 384, 40, "Белый — вы; зелёный — ориентир.\nЗолотой — непроходные фасады.", theme.Small);
            foreach (var destination in CityDistrictPlan.Destinations)
            {
                int index = CityDistrictPlan.Destinations.IndexOf(destination); float y = 268 + index * 124;
                if (Button(664, y, 568, 44, "Ориентир / " + destination.Name, ApplicationActive, destination.Id == cityDestinationId)) cityDestinationId = destination.Id;
                Label(664, y + 48, 568, 68, destination.Description, theme.Small);
            }
            var target = CityDistrictPlan.Find(cityDestinationId);
            Label(200, 776, 700, 64, office.IsInCity ? "От вас: " + Math.Ceiling(CityDistrictPlan.Distance(office.MapX, office.MapZ, target)) + " м по прямой · "
                + CityDistrictPlan.Direction(office.MapX, office.MapZ, target) + ". Обходите клумбы и фасады пешком."
                : "Карта не переносит игрока. Выйдите через лифт в квартал; выбранный ориентир появится на улице.", theme.Small);
            if (Button(936, 784, 296, 48, "Назад · Esc", ApplicationActive)) CloseCityInformation();
        }
        private void DrawCityCompanies()
        {
            Panel(160, 76, 1120, 808, ""); Label(200, 108, 1040, 48, "Компании / публичные карточки", theme.Title);
            Label(200, 170, 1040, 56, "Стенд не биржа и не реклама гарантированной доходности. Все компании вымышлены. Здесь только информация; предложение клиенту выбирается за рабочим столом.", theme.Small);
            var stocks = StockCatalog.Offers;
            for (int i = 0; i < stocks.Length; i++)
            {
                var stock = stocks[i]; float x = 200 + i % 2 * 528, y = 252 + i / 2 * 164;
                Label(x, y, 504, 32, stock.Ticker + " / " + stock.Name, theme.Heading);
                Label(x, y + 40, 504, 24, stock.RiskLabel + " · " + stock.HorizonMonths + " месяцев", theme.Small);
                Label(x, y + 72, 504, 80, stock.Description, theme.Small);
            }
            Label(200, 756, 700, 88, "Чтение не даёт бонуса, не закрывает претензию и не раскрывает условия за брокера. Нет рыночных котировок/покупки акций на улице. Пока открыт стенд, рабочее время стоит.", theme.Small);
            if (Button(936, 796, 296, 48, "Назад · Esc", ApplicationActive)) CloseCityInformation();
        }
        private void OpenCityMapFromPause()
        {
            if (!ApplicationActive || selectingMode || intro || day.DayEnded || UtilityOverlayOpen || busy || voice.Working || voice.Recording) return;
            voice.Cancel(); cityMapFromPause = true; navigation.Open(OfficeStation.CityMap); office.Player.SetControl(false);
        }
        private void CloseCityInformation()
        {
            if (cityMapFromPause) { cityMapFromPause = false; navigation.Pause(); office.Player.SetControl(false); }
            else EnterWorld();
        }
    }
}
