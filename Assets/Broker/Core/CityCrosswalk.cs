using System;

namespace Broker.Core
{
    public enum CrosswalkPhase { CarsGo, CarsAmber, ClearToWalk, Walk, WalkEnding, ClearToCars }

    // Pedestrian-actuated signal of the boulevard crossing. Deterministic: the scene feeds frame time and two
    // facts — a car between the stop line and the far side of the zebra, people on the zebra. Nothing here moves anyone.
    public sealed class CrosswalkSignal
    {
        public const double CarsGoSeconds = 14, RequestedCarsGoSeconds = 6, AmberSeconds = 3, ClearToWalkSeconds = 1,
            WalkSeconds = 10, WalkEndingSeconds = 4, ClearToCarsSeconds = 2;
        public const double CycleSeconds = CarsGoSeconds + AmberSeconds + ClearToWalkSeconds + WalkSeconds + WalkEndingSeconds + ClearToCarsSeconds;
        public CrosswalkPhase Phase { get; private set; } = CrosswalkPhase.CarsGo;
        public double PhaseElapsed { get; private set; }
        // The button shortens the cars' green to 6 s from the start of the phase; it is spent when the amber starts.
        public bool WalkRequested { get; private set; }
        public bool CarsGreen => Phase == CrosswalkPhase.CarsGo;
        public bool CarsAmber => Phase == CrosswalkPhase.CarsAmber;
        public bool PedestrianGreen => Phase == CrosswalkPhase.Walk || Phase == CrosswalkPhase.WalkEnding;
        public bool PedestrianFlashing => Phase == CrosswalkPhase.WalkEnding;
        public double WalkSecondsLeft => Phase == CrosswalkPhase.Walk ? WalkSeconds + WalkEndingSeconds - PhaseElapsed
            : Phase == CrosswalkPhase.WalkEnding ? WalkEndingSeconds - PhaseElapsed : 0;
        // Planned wait without holds for cars or people still on the zebra.
        public double SecondsUntilWalk
        {
            get
            {
                double left = Math.Max(0, PhaseLength(Phase) - PhaseElapsed);
                switch (Phase)
                {
                    case CrosswalkPhase.CarsGo: return left + AmberSeconds + ClearToWalkSeconds;
                    case CrosswalkPhase.CarsAmber: return left + ClearToWalkSeconds;
                    case CrosswalkPhase.ClearToWalk: return left;
                    case CrosswalkPhase.ClearToCars: return left + (WalkRequested ? RequestedCarsGoSeconds : CarsGoSeconds) + AmberSeconds + ClearToWalkSeconds;
                    default: return 0;
                }
            }
        }
        // True when the press changes the plan: during the cars' green or the all-red before it.
        public bool RequestWalk()
        {
            if (WalkRequested || (Phase != CrosswalkPhase.CarsGo && Phase != CrosswalkPhase.ClearToCars)) return false;
            WalkRequested = true; return true;
        }
        public void Advance(double seconds, bool carInCrosswalk, bool peopleOnCrossing)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            PhaseElapsed += seconds;
            // Time of this call not yet spent: a late press ends the green now, but never skips the amber.
            double unspent = seconds;
            for (int guard = 0; guard < 12; guard++)
            {
                double length = PhaseLength(Phase);
                if (PhaseElapsed < length) return;
                // Holds: the walk waits for committed cars, the cars wait for everyone on the zebra.
                if ((Phase == CrosswalkPhase.ClearToWalk && carInCrosswalk) || (Phase == CrosswalkPhase.ClearToCars && peopleOnCrossing))
                { PhaseElapsed = length; return; }
                unspent = Math.Min(PhaseElapsed - length, unspent);
                PhaseElapsed = unspent;
                Phase = Phase == CrosswalkPhase.ClearToCars ? CrosswalkPhase.CarsGo : Phase + 1;
                if (Phase == CrosswalkPhase.CarsAmber) WalkRequested = false;
            }
        }
        private double PhaseLength(CrosswalkPhase phase)
        {
            switch (phase)
            {
                case CrosswalkPhase.CarsGo: return WalkRequested ? RequestedCarsGoSeconds : CarsGoSeconds;
                case CrosswalkPhase.CarsAmber: return AmberSeconds;
                case CrosswalkPhase.ClearToWalk: return ClearToWalkSeconds;
                case CrosswalkPhase.Walk: return WalkSeconds;
                case CrosswalkPhase.WalkEnding: return WalkEndingSeconds;
                default: return ClearToCarsSeconds;
            }
        }
    }

    // Geometry of the zebra between the kerb gates and the hint shown near it. Local coordinates match CityDistrictPlan.
    public static class CityCrosswalk
    {
        public const float HalfWidth = 2f, GateHalfWidth = 2.2f, CorridorX = 2.35f, EastboundStopX = -4f, WestboundStopX = 4f;
        public const float SouthCurbZ = 27.8f, NorthCurbZ = 42f;
        // The player counts as "on the crossing" until the capsule has fully left the gate line.
        public static bool PlayerInZone(float x, float z)
            => Math.Abs(x) < 2.5f && z > CityDistrictPlan.SouthKerbZ - 0.45f && z < CityDistrictPlan.NorthKerbZ + 0.45f;
        // Invisible gates in the kerb gaps: open on the pedestrians' green and never closed on someone already crossing.
        public static bool GatesOpen(CrosswalkSignal signal, bool playerInZone) => playerInZone || (signal != null && signal.PedestrianGreen);
        public static bool Near(float x, float z) => Math.Abs(x) < 12 && z > 20 && z < 50;
        public static string Hint(CrosswalkSignal signal)
        {
            if (signal == null) return "";
            if (signal.Phase == CrosswalkPhase.Walk) return "Пешеходам зелёный · " + Seconds(signal.WalkSecondsLeft) + "\nМашины стоят у стоп-линий";
            if (signal.Phase == CrosswalkPhase.WalkEnding) return "Зелёный мигает · " + Seconds(signal.WalkSecondsLeft) + "\nНе начинайте переход";
            if (signal.Phase == CrosswalkPhase.ClearToCars) return "Пешеходам красный\nМашины тронутся, когда зебра свободна";
            if (signal.WalkRequested || signal.Phase != CrosswalkPhase.CarsGo)
                return "Пешеходам красный\nЗелёный ≈ через " + Seconds(signal.SecondsUntilWalk);
            return "Пешеходам красный\nКнопка на столбе — E";
        }
        private static string Seconds(double value) => Math.Max(1, (int)Math.Ceiling(value - 1e-6)) + " с";
    }
}
