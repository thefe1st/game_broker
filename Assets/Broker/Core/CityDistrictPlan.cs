using System;
using System.Collections.ObjectModel;

namespace Broker.Core
{
    public sealed class CityDestination
    {
        public string Id { get; } public string Name { get; } public string Description { get; }
        public float X { get; } public float Z { get; }
        internal CityDestination(string id, string name, string description, float x, float z)
        { Id = id; Name = name; Description = description; X = x; Z = z; }
    }
    // Axis-aligned rectangle in local metres, bounds inclusive.
    public readonly struct CityRect
    {
        public float MinX { get; } public float MinZ { get; } public float MaxX { get; } public float MaxZ { get; }
        public CityRect(float minX, float minZ, float maxX, float maxZ)
        {
            if (!(maxX >= minX) || !(maxZ >= minZ)) throw new ArgumentException("Empty rectangle.");
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
        }
        public static CityRect Around(float x, float z, float halfX, float halfZ) => new CityRect(x - halfX, z - halfZ, x + halfX, z + halfZ);
        public float Width => MaxX - MinX; public float Depth => MaxZ - MinZ;
        public float CenterX => (MinX + MaxX) / 2; public float CenterZ => (MinZ + MaxZ) / 2;
        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        // Distance from a point to the rectangle; 0 inside.
        public double Distance(float x, float z)
        {
            double dx = Math.Max(Math.Max(MinX - x, x - MaxX), 0), dz = Math.Max(Math.Max(MinZ - z, z - MaxZ), 0);
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
    // Walkable plan of both quarters: the old district south of the boulevard, the signalised crossing and the
    // Exchange quarter to the north. The lay-by of the test car (CityRoads) is cut out of the square. No fast travel,
    // shops or rewards. Local coordinates match CityBlockPlan.
    public static class CityDistrictPlan
    {
        public const float MinX = -44, MaxX = 44, MinZ = -12, MaxZ = 76;
        // Kerb lines of the boulevard and the floor heights above CityBlockPlan.GroundY.
        public const float SouthKerbZ = 28.5f, NorthKerbZ = 41.3f, RoadFloorY = 0.03f, NorthFloorY = 0.12f, PodiumFloorY = 1.1f;
        public const float StepsMinZ = 54.8f, PodiumMinZ = 56.8f, PodiumMaxZ = 75.2f, StepsHalfWidth = 9f, PodiumHalfWidth = 12.6f;
        private static readonly CityRect[] areas = {
            new CityRect(-44, 8, 44, 25.8f),              // plaza with the western square and the eastern street
            new CityRect(-44, 25.8f, -32.9f, 28), new CityRect(-16.6f, 25.8f, 44, 28),   // the kerb side, clear of the lay-by and its tapers
            new CityRect(-44, -12, -17, 8), new CityRect(17, -12, 44, 8),   // side streets in front of the facades
            new CityRect(-2, 28, 2, 41.6f),               // zebra crossing between the kerb gates
            new CityRect(-44, 41.6f, 44, 44.5f),          // north pavement along the tower fronts
            new CityRect(-16, 44.5f, 16, StepsMinZ),      // Exchange square
            new CityRect(-StepsHalfWidth, StepsMinZ, StepsHalfWidth, PodiumMinZ),           // front steps
            new CityRect(-PodiumHalfWidth, PodiumMinZ, PodiumHalfWidth, PodiumMaxZ),        // podium and colonnade
        };
        private static readonly CityRect[] footprints = {
            new CityRect(-39, -10, -21, -2), new CityRect(27, -10, 39, -2),   // closed street facades
            new CityRect(21, -10, 27, -2),                // café shell
            new CityRect(-9, 60, 9, 72),                  // Exchange hall walls
        };
        private static readonly CityRect[] openings = {
            new CityRect(21.5f, -9.5f, 26.5f, -2.5f), new CityRect(22, -2.5f, 23.5f, -2),   // café room and door
            new CityRect(-8.5f, 60.5f, 8.5f, 71.5f), new CityRect(-1.5f, 60, 1.5f, 60.5f),  // Exchange hall and door
        };
        private static readonly CityDestination[] destinations = {
            new CityDestination("office", "Лифт в офис", "Возврат к рабочему месту. Не завершает день и не сохраняет текущие звонки.", 0, 10),
            new CityDestination("square", "Центральная площадь", "Карта района и стенд вымышленных компаний. Информация не раскрывает условия за брокера.", 0, 19),
            new CityDestination("park", "Западный сквер", "Пешеходный сквер со скамьями и деревьями. Прогулка тратит рабочее время, денег и бонусов не даёт.", -31, 16),
            new CityDestination("business", "Офисная улица", "Фасады небольших офисов на востоке; открыта только кофейня. Это не новые филиалы или магазины.", 31, 16),
            new CityDestination("cafe", "Кофейня «Бык и медведь»", "Стойка, бариста и разговоры дня. Купить ничего нельзя, бонусов нет.", 24, -5),
            new CityDestination("crossing", "Переход через бульвар", "Светофор с кнопкой. Машины ждут у стоп-линий, пока на зебре люди.", 0, 26.5f),
            new CityDestination("northsquare", "Биржевая площадь", "Ростральные колонны, газоны и киоск на северной стороне бульвара.", 0, 49),
            new CityDestination("exchange", "Зал Биржи", "Табло котировок дня. Только просмотр: бумагу клиенту выбирают за рабочим столом.", 0, 68),
            new CityDestination("parking", "Парковка у сквера", "Тестовый седан в кармане у бульвара: E у машины — сесть за руль. Выйти можно только здесь. Езда не тратит деньги и не даёт бонусов.",
                CityRoads.ParkX, CityRoads.ExitZ - 0.35f),
        };
        public static ReadOnlyCollection<CityDestination> Destinations { get; } = Array.AsReadOnly(destinations);
        public static ReadOnlyCollection<CityRect> Areas { get; } = Array.AsReadOnly(areas);
        public static ReadOnlyCollection<CityRect> Footprints { get; } = Array.AsReadOnly(footprints);
        public static ReadOnlyCollection<CityRect> Openings { get; } = Array.AsReadOnly(openings);
        // Asphalt between the kerbs inside the district, for the map.
        public static CityRect Road { get; } = new CityRect(MinX, SouthKerbZ, MaxX, NorthKerbZ);
        public static CityDestination Find(string id) => Array.Find(destinations, d => d.Id == id);
        public static bool IsPedestrianArea(float x, float z)
        {
            if (!Finite(x) || !Finite(z) || x < MinX || x > MaxX || z < MinZ || z > MaxZ) return false;
            foreach (var area in areas) if (area.Contains(x, z)) return true;
            return false;
        }
        public static bool IsBuildingFootprint(float x, float z)
        {
            bool inside = false;
            foreach (var block in footprints) if (block.Contains(x, z)) { inside = true; break; }
            if (!inside) return false;
            foreach (var hole in openings) if (hole.Contains(x, z)) return false;
            return true;
        }
        public static bool CanWalk(float x, float z) => IsPedestrianArea(x, z) && !IsBuildingFootprint(x, z);
        // Floor height above GroundY: plaza 0, zebra 3 cm, north pavement 12 cm, steps rising to the podium.
        public static float FloorY(float x, float z)
        {
            if (z < SouthKerbZ) return 0;
            if (z <= NorthKerbZ) return RoadFloorY;
            if (z >= PodiumMinZ && z <= PodiumMaxZ && Math.Abs(x) <= PodiumHalfWidth) return PodiumFloorY;
            if (z > StepsMinZ && z < PodiumMinZ && Math.Abs(x) <= StepsHalfWidth)
                return NorthFloorY + (PodiumFloorY - NorthFloorY) * (z - StepsMinZ) / (PodiumMinZ - StepsMinZ);
            return NorthFloorY;
        }
        public static double Distance(float x, float z, CityDestination destination)
        {
            if (!Finite(x) || !Finite(z) || destination == null) throw new ArgumentException("Invalid map position/destination.");
            double dx = (double)destination.X - x, dz = (double)destination.Z - z;
            return Math.Sqrt(dx * dx + dz * dz);
        }
        public static string Direction(float x, float z, CityDestination destination)
        {
            double distance = Distance(x, z, destination); if (distance <= 2) return "Вы у ориентира";
            float dx = destination.X - x, dz = destination.Z - z;
            string side = Math.Abs(dx) > Math.Abs(dz) ? dx > 0 ? "Восток" : "Запад" : dz > 0 ? "Север" : "Юг";
            return Side(z) * Side(destination.Z) < 0 ? side + " · через переход" : side;
        }
        // −1 south of the boulevard, +1 north of it, 0 on the road.
        private static int Side(float z) => z < SouthKerbZ ? -1 : z > NorthKerbZ ? 1 : 0;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
