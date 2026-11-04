using System;
using Broker.Core;
using UnityEngine;

namespace Broker.Runtime
{
    public sealed partial class PrototypeBootstrap
    {
        private string cityDestinationId = "office";
        private bool cityMapFromPause;
        // District map at 3.3 px per metre: the ring of roads with the lay-by, the walkable areas, the zebra, closed
        // buildings in gold with the open rooms (café, Exchange hall) cut out, the chosen landmark and the player (in the
        // car while driving). It never moves the player.
        private const float MapLeft = 212, MapTop = 234, MapScale = 3.3f, MapMinX = -62, MapMaxX = 62, MapMinZ = -32, MapMaxZ = 76;
        private static Rect MapRect(CityRect r)
        {
            float minX = Mathf.Max(r.MinX, MapMinX), maxX = Mathf.Min(r.MaxX, MapMaxX), minZ = Mathf.Max(r.MinZ, MapMinZ), maxZ = Mathf.Min(r.MaxZ, MapMaxZ);
            return new Rect(MapLeft + (minX - MapMinX) * MapScale, MapTop + (MapMaxZ - maxZ) * MapScale,
                Mathf.Max(0, maxX - minX) * MapScale, Mathf.Max(0, maxZ - minZ) * MapScale);
        }
        private static Rect MapDot(float x, float z, float size) => new Rect(MapLeft + (x - MapMinX) * MapScale - size / 2, MapTop + (MapMaxZ - z) * MapScale - size / 2, size, size);
        private void DrawCityMap()
        {
            Panel(160, 64, 1120, 832, ""); Label(200, 92, 1040, 48, "Район / навигация", theme.Title);
            Label(200, 148, 1040, 52, "Юг — площадь у офиса, сквер и офисная улица. Север — Биржа за бульваром: зебра на зелёный, кнопка на столбе. Кольцо дорог — для тестового седана с парковки у сквера.", theme.Small);
            Panel(200, 216, 432, 480, "");
            foreach (var area in CityDistrictPlan.Areas) Paint(MapRect(area), theme.Border);
            var road = Broker.Runtime.Office.BrokerTheme.Hex("#0F1B2D");
            foreach (var piece in CityRoads.Drivable) Paint(MapRect(piece), road);
            for (int i = 0; i < 6; i++)
                Paint(MapRect(new CityRect(-CityCrosswalk.HalfWidth, CityDistrictPlan.SouthKerbZ + 0.6f + i * 2, CityCrosswalk.HalfWidth, CityDistrictPlan.SouthKerbZ + 1.6f + i * 2)), theme.Ink);
            foreach (var footprint in CityDistrictPlan.Footprints) Paint(MapRect(footprint), theme.Gold);
            foreach (var building in CityRoads.Frontage) Paint(MapRect(building.Footprint), theme.Gold);
            foreach (var opening in CityDistrictPlan.Openings) Paint(MapRect(opening), theme.Border);
            Label(396, 256, 96, 24, "БИРЖА", theme.Tag);
            Label(278, 358, 120, 24, "БУЛЬВАР", theme.Tag);
            Label(312, 400, 96, 24, "ПАРКОВКА", theme.Tag);
            Label(400, 422, 120, 24, "ПЛОЩАДЬ", theme.Tag);
            Label(284, 446, 96, 24, "СКВЕР", theme.Tag);
            Label(488, 446, 96, 24, "ОФИСЫ", theme.Tag);
            Label(382, 536, 96, 24, "КОЛЬЦО", theme.Tag);
            var selected = CityDistrictPlan.Find(cityDestinationId);
            Paint(MapDot(selected.X, selected.Z, 8), theme.Accent);
            if (office.IsInCity) Paint(MapDot(office.MapX, office.MapZ, 6), theme.Ink);
            Label(224, 606, 384, 72, "Белый — вы, зелёный — ориентир, тёмное — дороги. Золотое — закрытые здания; открыты кофейня и зал Биржи.", theme.Small);
            var destinations = CityDistrictPlan.Destinations;
            for (int i = 0; i < destinations.Count; i++)
                if (Button(664, 216 + i * 48, 568, 40, "Ориентир / " + destinations[i].Name, ApplicationActive, destinations[i].Id == cityDestinationId)) cityDestinationId = destinations[i].Id;
            Label(664, 652, 568, 64, selected.Description, theme.Small);
            Label(200, 724, 700, 64, office.IsInCity ? "От вас: " + Math.Ceiling(CityDistrictPlan.Distance(office.MapX, office.MapZ, selected)) + " м по прямой · "
                + CityDistrictPlan.Direction(office.MapX, office.MapZ, selected) + (office.Driving ? ". В машине — только по кольцу; выйти можно на парковке у сквера."
                : ". Обходите клумбы и фасады пешком, бульвар — только по зебре.")
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
