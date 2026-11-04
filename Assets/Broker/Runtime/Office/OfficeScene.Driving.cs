using Broker.Core;
using UnityEngine;

namespace Broker.Runtime.Office
{
    // The player's test sedan, CityLife.Car: its rig in the lay-by with a collider for the aim, getting in and out, and
    // the chase camera while driving. Core moves the car; this partial only shows it, so a drive changes nothing in the day.
    public sealed partial class OfficeScene
    {
        // Chase camera: from behind and above it looks at a point over the car a little ahead of it, follows the heading
        // with a lag, turns round the car with the mouse and stops short of the buildings (CityRoads.Solids).
        public const float ChaseDistance = 8.5f, ChaseLookHeight = 1.2f, ChaseLookAhead = 2.5f, ChaseFollow = 3.5f, ChasePitch = 10f;
        public const float ChaseMinPitch = 2, ChaseMaxPitch = 50, ChaseMargin = 0.4f, ChaseNearest = 1.6f;
        private CarRig playerCar;
        private float chaseYaw;
        private bool chaseSet;
        // Behind the wheel in the city.
        public bool Driving => IsInCity && Life.Driving;
        // Standing in the lay-by: E lets the driver out.
        public bool CanLeaveCar => Driving && Life.Car.Parked;
        public float CarSpeedKmh => Life.Car.Kmh;
        public string CarGear => Life.Car.GearLabel;

        private void BuildPlayerCar()
        {
            playerCar = PlaceCar(CarStyle.Sedan, PlayerCar.Paint, "Lay-by / test sedan", transform);
            var shape = CarModels.Sedan;
            var hitbox = playerCar.Root.gameObject.AddComponent<BoxCollider>();
            hitbox.center = new Vector3(0, shape.Height / 2, 0); hitbox.size = new Vector3(shape.Length, shape.Height, shape.Width);
            Interactable(playerCar.Root.gameObject, OfficeStation.Car, "Сесть за руль / тестовый седан", "Сесть в машину и проехать по кольцу");
            ShowPlayerCar(0);
        }
        // Behind the wheel: the walker sits down (no capsule, no aim) and the keys go to the car.
        public bool EnterCar()
        {
            if (!IsInCity || Player == null || !Life.EnterCar()) return false;
            Player.Seat(); chaseSet = false; ShowPlayerCar(0); return true;
        }
        // Out of the car only in the lay-by: the driver steps onto the square behind the railing, facing the car.
        public bool TryLeaveCar()
        {
            if (!Driving || !Life.LeaveCar()) return false;
            Player.Unseat(transform.TransformPoint(ExitPoint()), transform.rotation);
            ShowPlayerCar(0); return true;
        }
        private Vector3 ExitPoint() => new Vector3(Mathf.Clamp(Life.Car.X, CityRoads.BayMinX + 1, CityRoads.BayMaxX - 1), CityBlockPlan.SpawnY, CityRoads.ExitZ);
        // A reset or a trip to the office: the driver is out and the car stands back in the lay-by.
        private void ParkPlayerCar()
        {
            Life.ParkCar(); chaseSet = false;
            ShowPlayerCar(0);
        }
        // The rig follows the car: the root on the road, the body leaning about its centre of mass (nose up under power,
        // the roof out of a turn), the front wheels steering, every wheel rolling.
        private void ShowPlayerCar(float dt)
        {
            if (playerCar == null) return;
            var car = Life.Car;
            playerCar.Root.localPosition = new Vector3(car.X, CityBlockPlan.GroundY + RoadTop, car.Z);
            playerCar.Root.localRotation = Quaternion.Euler(0, -car.Heading * Mathf.Rad2Deg, 0);
            var lean = Quaternion.Euler(car.Roll, 0, car.Pitch);
            var centre = new Vector3(0, PlayerCar.CentreHeight, 0);
            var body = playerCar.Body.transform;
            body.localRotation = lean; body.localPosition = centre - lean * centre;
            PoseCar(playerCar, car.Rolled, car.Braking, -car.Steering * Mathf.Rad2Deg);
            if (Driving && Player != null && Player.Seated) ChaseCamera(dt);
        }
        private void ChaseCamera(float dt)
        {
            var car = Life.Car;
            // Unity yaw of the nose: heading 0 (+X) is yaw 90.
            float carYaw = 90 - car.Heading * Mathf.Rad2Deg;
            if (!chaseSet || dt >= 1) { chaseYaw = carYaw; chaseSet = true; }
            else chaseYaw = Mathf.LerpAngle(chaseYaw, carYaw, 1 - Mathf.Exp(-ChaseFollow * dt));
            float pitch = Mathf.Clamp(ChasePitch + Player.OrbitPitch, ChaseMinPitch, ChaseMaxPitch);
            var look = Quaternion.Euler(pitch, chaseYaw + Player.OrbitYaw, 0);
            var target = new Vector3(car.X + Mathf.Cos(car.Heading) * ChaseLookAhead, CityBlockPlan.GroundY + RoadTop + ChaseLookHeight,
                car.Z + Mathf.Sin(car.Heading) * ChaseLookAhead);
            var back = look * Vector3.back;
            float room = CityRoads.ClearFraction(target.x, target.z, target.x + back.x * ChaseDistance, target.z + back.z * ChaseDistance, ChaseMargin);
            float distance = Mathf.Max(ChaseNearest, ChaseDistance * room);
            Player.Eye.transform.SetPositionAndRotation(transform.TransformPoint(target + back * distance), transform.rotation * look);
        }
    }
}
