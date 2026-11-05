using System;

namespace Broker.Core
{
    // One clock for the streets: the signal, the boulevard and highway cars, the walkers and the player's car advance together in small
    // steps, so a long frame or a preview jump gives the same picture as many short frames. The scene feeds frame time,
    // the player's position and the driver's keys; nothing here touches the working day, money or clients.
    public sealed class CityLife
    {
        private const double Step = 0.05;
        public CrosswalkSignal Signal { get; } = new CrosswalkSignal();
        public CityTraffic Traffic { get; } = new CityTraffic();
        // The highway beyond the new quarter, with traffic of its own.
        public HighwayTraffic Highway { get; } = new HighwayTraffic();
        public CityCrowd Crowd { get; } = new CityCrowd();
        // The test sedan in its lay-by by the western square; it moves only while the player drives it.
        public PlayerCar Car { get; } = new PlayerCar();
        public bool Driving { get; private set; }
        // The driver's keys for the next Advance; ignored on foot.
        public DriveInput Input { get; set; }
        public double Elapsed { get; private set; }
        // The player is between the kerb gates; the cars wait for the player like for any walker.
        public bool PlayerOnCrossing { get; private set; }
        public bool GatesOpen => CityCrosswalk.GatesOpen(Signal, PlayerOnCrossing);
        // The zebra between the kerbs with a little room at its sides: the player's car there holds the walkers' green
        // like a boulevard car past the stop line.
        public static readonly CityRect ZebraZone = new CityRect(-CityCrosswalk.HalfWidth - 0.6f, CityDistrictPlan.SouthKerbZ,
            CityCrosswalk.HalfWidth + 0.6f, CityDistrictPlan.NorthKerbZ);
        public bool CarOnZebra => Car.Footprint.Overlaps(ZebraZone);
        // Behind the wheel: the keys go to the car from the next step.
        public bool EnterCar()
        {
            if (Driving) return false;
            Driving = true; Input = DriveInput.None; return true;
        }
        // The driver gets out only with the car standing in the lay-by; it stays there with the handbrake on.
        public bool LeaveCar()
        {
            if (!Driving || !Car.Parked) return false;
            Car.Stop(); Driving = false; Input = DriveInput.None; return true;
        }
        // Back to the lay-by (a reset, a trip to the office): the driver is out, the car stands parked.
        public void ParkCar() { Driving = false; Input = DriveInput.None; Car.Park(); }
        public void Advance(double seconds, bool playerPresent, float playerX, float playerZ)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            bool walking = playerPresent && !Driving;
            PlayerOnCrossing = walking && CityCrosswalk.PlayerInZone(playerX, playerZ);
            var keys = Driving ? Input : DriveInput.None;
            while (seconds > 1e-9)
            {
                double dt = Math.Min(Step, seconds); seconds -= dt;
                var car = Car.Footprint;
                Signal.Advance(dt, Traffic.CarInCrosswalk || car.Overlaps(ZebraZone), Crowd.AnyOnCrossing || PlayerOnCrossing);
                Traffic.Advance(dt, Signal, car, Car.VelocityX);
                Highway.Advance(dt, car, Car.VelocityX);
                Crowd.Advance(dt, Signal, walking, playerX, playerZ, car);
                Car.Advance(dt, keys, Traffic, Crowd, Highway);
                Elapsed += dt;
            }
        }
    }
}
