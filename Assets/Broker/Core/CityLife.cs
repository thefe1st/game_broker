using System;

namespace Broker.Core
{
    // One clock for the boulevard: the signal, the cars and the walkers advance together in small steps, so a long
    // frame or a preview jump gives the same picture as many short frames. The scene feeds frame time and the
    // player's position; nothing here touches the working day, money or clients.
    public sealed class CityLife
    {
        private const double Step = 0.05;
        public CrosswalkSignal Signal { get; } = new CrosswalkSignal();
        public CityTraffic Traffic { get; } = new CityTraffic();
        public CityCrowd Crowd { get; } = new CityCrowd();
        public double Elapsed { get; private set; }
        // The player is between the kerb gates; the cars wait for the player like for any walker.
        public bool PlayerOnCrossing { get; private set; }
        public bool GatesOpen => CityCrosswalk.GatesOpen(Signal, PlayerOnCrossing);
        public string Hint => CityCrosswalk.Hint(Signal);
        public void Advance(double seconds, bool playerPresent, float playerX, float playerZ)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            PlayerOnCrossing = playerPresent && CityCrosswalk.PlayerInZone(playerX, playerZ);
            while (seconds > 1e-9)
            {
                double dt = Math.Min(Step, seconds); seconds -= dt;
                Signal.Advance(dt, Traffic.CarInCrosswalk, Crowd.AnyOnCrossing || PlayerOnCrossing);
                Traffic.Advance(dt, Signal);
                Crowd.Advance(dt, Signal, playerPresent, playerX, playerZ);
                Elapsed += dt;
            }
        }
    }
}
