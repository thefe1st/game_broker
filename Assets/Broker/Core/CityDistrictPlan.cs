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
    // Pedestrian district on the near side of traffic. No fast travel, shops or rewards.
    public static class CityDistrictPlan
    {
        public const float MinX = -44, MaxX = 44, MinZ = -12, MaxZ = 28;
        private static readonly CityDestination[] destinations = {
            new CityDestination("office", "Лифт в офис", "Возврат к рабочему месту. Не завершает день и не сохраняет текущие звонки.", 0, 10),
            new CityDestination("square", "Центральная площадь", "Карта района и стенд вымышленных компаний. Информация не раскрывает условия за брокера.", 0, 19),
            new CityDestination("park", "Западный сквер", "Пешеходный сквер со скамьями и деревьями. Прогулка тратит рабочее время, денег и бонусов не даёт.", -31, 16),
            new CityDestination("business", "Офисная улица", "Фасады небольших офисов на востоке. Это внешний район, не новые доступные филиалы или магазины.", 31, 16)
        };
        public static ReadOnlyCollection<CityDestination> Destinations { get; } = Array.AsReadOnly(destinations);
        public static CityDestination Find(string id) => Array.Find(destinations, d => d.Id == id);
        public static bool IsPedestrianArea(float x, float z)
        {
            if (!Finite(x) || !Finite(z) || x < MinX || x > MaxX || z < MinZ || z > MaxZ) return false;
            return z >= 8 || x <= -17 || x >= 17;
        }
        public static bool IsBuildingFootprint(float x, float z) => z >= -10 && z <= -2
            && ((x >= -39 && x <= -21) || (x >= 21 && x <= 39));
        public static bool CanWalk(float x, float z) => IsPedestrianArea(x, z) && !IsBuildingFootprint(x, z);
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
            return Math.Abs(dx) > Math.Abs(dz) ? dx > 0 ? "Восток" : "Запад" : dz > 0 ? "Север / бульвар" : "Юг / фасады";
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
