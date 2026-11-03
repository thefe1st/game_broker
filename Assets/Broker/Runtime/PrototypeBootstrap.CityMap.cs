using System;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private string cityDestinationId = "office";
        private bool cityMapFromPause;
        // Walking map at 4 px per metre: walkable areas, the boulevard with its zebra, closed buildings in gold with the
        // open rooms (café, Exchange hall) cut out, the chosen landmark and the player. It never moves the player.
        private void DrawCityMap()
        {
            Panel(160, 64, 1120, 832, ""); Label(200, 92, 1040, 48, "Район / пешая навигация", theme.Title);
            Label(200, 148, 1040, 52, "Юг — площадь у офиса, сквер и офисная улица. Север — Биржа за бульваром: переходите по зебре на зелёный, кнопка на столбе. Открыты только зал Биржи и кофейня.", theme.Small);
            Panel(200, 216, 432, 480, "");
            const float left = 240, top = 248, scale = 4;
            Rect Map(CityRect r) => new Rect(left + (r.MinX - CityDistrictPlan.MinX) * scale, top + (CityDistrictPlan.MaxZ - r.MaxZ) * scale, r.Width * scale, r.Depth * scale);
            foreach (var area in CityDistrictPlan.Areas) Paint(Map(area), theme.Border);
            Paint(Map(CityDistrictPlan.Road), Broker.Runtime.Office.BrokerTheme.Hex("#0F1B2D"));
            for (int i = 0; i < 6; i++)
                Paint(Map(new CityRect(-CityCrosswalk.HalfWidth, CityDistrictPlan.SouthKerbZ + 0.6f + i * 2, CityCrosswalk.HalfWidth, CityDistrictPlan.SouthKerbZ + 1.6f + i * 2)), theme.Ink);
            foreach (var footprint in CityDistrictPlan.Footprints) Paint(Map(footprint), theme.Gold);
            foreach (var opening in CityDistrictPlan.Openings) Paint(Map(opening), theme.Border);
            Label(392, 276, 96, 24, "БИРЖА", theme.Tag);
            Label(248, 400, 120, 24, "БУЛЬВАР", theme.Tag);
            Label(380, 468, 120, 24, "ПЛОЩАДЬ", theme.Tag);
            Label(256, 504, 96, 24, "СКВЕР", theme.Tag);
            Label(504, 504, 96, 24, "ОФИСЫ", theme.Tag);
            var selected = CityDistrictPlan.Find(cityDestinationId);
            Paint(new Rect(left + (selected.X - CityDistrictPlan.MinX) * scale - 4, top + (CityDistrictPlan.MaxZ - selected.Z) * scale - 4, 8, 8), theme.Accent);
            if (office.IsInCity) Paint(new Rect(left + (office.MapX - CityDistrictPlan.MinX) * scale - 3, top + (CityDistrictPlan.MaxZ - office.MapZ) * scale - 3, 6, 6), theme.Ink);
            Label(224, 616, 384, 64, "Белый — вы; зелёный — ориентир. Золотой — закрытые здания, в них открыты только кофейня и зал Биржи.", theme.Small);
            var destinations = CityDistrictPlan.Destinations;
            for (int i = 0; i < destinations.Count; i++)
                if (Button(664, 216 + i * 54, 568, 44, "Ориентир / " + destinations[i].Name, ApplicationActive, destinations[i].Id == cityDestinationId)) cityDestinationId = destinations[i].Id;
            Label(664, 652, 568, 64, selected.Description, theme.Small);
            Label(200, 724, 700, 64, office.IsInCity ? "От вас: " + Math.Ceiling(CityDistrictPlan.Distance(office.MapX, office.MapZ, selected)) + " м по прямой · "
                + CityDistrictPlan.Direction(office.MapX, office.MapZ, selected) + ". Обходите клумбы и фасады пешком, бульвар — только по зебре."
                : "Карта не переносит игрока. Выйдите через лифт в квартал; выбранный ориентир появится на улице.", theme.Small);
            if (Button(936, 820, 296, 48, "Назад · Esc", ApplicationActive)) CloseCityInformation();
        }
        private void DrawCityCompanies()
        {
            Panel(160, 76, 1120, 808, ""); Label(200, 108, 1040, 48, "Компании / публичные карточки", theme.Title);
            Label(200, 170, 1040, 56, "Стенд — не реклама гарантированной доходности. Все компании вымышлены. Здесь только описания; предложение клиенту выбирается за рабочим столом.", theme.Small);
            var stocks = StockCatalog.Offers;
            for (int i = 0; i < stocks.Length; i++)
            {
                var stock = stocks[i]; float x = 200 + i % 2 * 528, y = 252 + i / 2 * 164;
                Label(x, y, 504, 32, stock.Ticker + " / " + stock.Name, theme.Heading);
                Label(x, y + 40, 504, 24, stock.RiskLabel + " · " + stock.HorizonMonths + " месяцев", theme.Small);
                Label(x, y + 72, 504, 80, stock.Description, theme.Small);
            }
            Label(200, 756, 700, 88, "Чтение не даёт бонуса, не закрывает претензию и не раскрывает условия за брокера. Цены дня — на табло в зале Биржи за бульваром; купить акции на улице нельзя. Пока открыт стенд, рабочее время стоит.", theme.Small);
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
