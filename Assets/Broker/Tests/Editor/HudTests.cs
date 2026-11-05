using System;
using System.Collections.Generic;
using System.Linq;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class HudTests
    {
        // A monospaced stand-in for the fonts: 0.56 of the type size a letter.
        private sealed class Measure : IHudText
        { public float Width(string text, HudFont font) => (text ?? "").Length * HudPlan.FontSize(font) * 0.56f; }
        private static readonly Measure measure = new Measure();
        // Lines of the old HUD that the pictograms replaced.
        private static readonly string[] removed =
        {
            "МОЙКА КАПИТАЛ", "Деловой квартал", "· день", "личные", "Прогулка", "Телефон — звонки", "ДАЛЬШЕ", "Вернитесь в офис",
            "Карта — у стенда", "ПЕРЕХОД / СВЕТОФОР", "Кнопка на столбе", "передача", "задний ход", "Выйти можно только", "ручник", "WASD",
        };

        private static HudFrame Office() => new HudFrame { Day = 2, Cash = 120, Expenses = 300, WalkKeys = true };
        private static HudFrame City(float x, float z, string destination)
            => new HudFrame { Day = 2, Cash = 120, Expenses = 300, InCity = true, X = x, Z = z, DestinationId = destination };
        private static List<HudItem> Build(HudFrame frame)
        {
            var items = new HudPlan(measure).Build(frame).ToList(); AssertOnCanvas(items); return items;
        }
        private static void AssertOnCanvas(IEnumerable<HudItem> items)
        {
            foreach (var item in items)
            {
                if (item.Kind == HudKind.Crosshair) continue;
                string name = item.Kind + " " + (item.Text ?? item.Icon.ToString());
                Assert.That(item.W, Is.GreaterThan(0), name); Assert.That(item.H, Is.GreaterThan(0), name);
                Assert.That(item.X, Is.GreaterThanOrEqualTo(0), name); Assert.That(item.Y, Is.GreaterThanOrEqualTo(0), name);
                Assert.That(item.X + item.W, Is.LessThanOrEqualTo(HudPlan.CanvasWidth), name);
                Assert.That(item.Y + item.H, Is.LessThanOrEqualTo(HudPlan.CanvasHeight), name);
                Assert.That(item.Angle, Is.InRange(0f, 360f), name);
                if (item.Text != null) foreach (string line in removed) Assert.That(item.Text, Does.Not.Contain(line));
            }
        }
        private static List<HudIcon> Icons(IEnumerable<HudItem> items) => items.Where(i => i.Kind == HudKind.Icon).Select(i => i.Icon).ToList();
        private static List<string> Texts(IEnumerable<HudItem> items)
            => items.Where(i => i.Kind == HudKind.Text || i.Kind == HudKind.Key).Select(i => i.Text).ToList();
        private static HudItem Icon(IEnumerable<HudItem> items, HudIcon icon) => items.Single(i => i.Kind == HudKind.Icon && i.Icon == icon);
        // The line at the bottom: the key and what it does, or the way to the parking.
        private static List<HudItem> PromptLine(IEnumerable<HudItem> items)
            => items.Where(i => i.Y >= HudPlan.PromptY && i.Y <= HudPlan.PromptY + 8).ToList();
        private static double Length(double dx, double dz) => Math.Sqrt(dx * dx + dz * dz);

        // ---------- Pictograms ----------
        [Test] public void EveryPictogramIsItsOwnSoftEdgedShapeWithAClearMargin()
        {
            const int n = 48; var masks = HudIcons.All.Select(icon => HudIcons.Mask(icon, n)).ToArray();
            Assert.That(HudIcons.All.Length, Is.EqualTo(Enum.GetValues(typeof(HudIcon)).Length));
            for (int k = 0; k < masks.Length; k++)
            {
                var mask = masks[k]; string name = HudIcons.All[k].ToString();
                Assert.That(mask.Length, Is.EqualTo(n * n), name);
                Assert.That(mask.All(v => v >= 0 && v <= 1), Is.True, name);
                Assert.That(mask.Sum() / (n * n), Is.InRange(0.08, 0.6), name + " fills its square without flooding it");
                Assert.That(mask.Count(v => v > 0 && v < 1), Is.GreaterThan(n), name + " has anti-aliased edges");
                // A grid unit round the edge (two pixels at 48) stays clear: nothing is clipped by the square or the mipmaps.
                for (int i = 0; i < n; i++)
                    for (int ring = 0; ring < 2; ring++)
                        Assert.That(Math.Max(Math.Max(mask[ring * n + i], mask[(n - 1 - ring) * n + i]), Math.Max(mask[i * n + ring], mask[i * n + n - 1 - ring])),
                            Is.LessThan(0.05f), name + " keeps its margin");
                Assert.That(HudIcons.Mask(HudIcons.All[k], n), Is.EqualTo(mask), name + " comes out the same every time");
                for (int j = 0; j < k; j++)
                {
                    double difference = 0; for (int p = 0; p < n * n; p++) difference += Math.Abs(mask[p] - masks[j][p]);
                    Assert.That(difference / (n * n), Is.GreaterThan(0.08), name + " differs from " + HudIcons.All[j]);
                }
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => HudIcons.Mask(HudIcon.Sun, 0));
            Assert.Throws<ArgumentException>(() => HudIcons.Mask(HudIcon.Arrow, 24, float.NaN));
        }
        [Test] public void TheArrowPointsAheadAndTurnsClockwiseInSteps()
        {
            const int n = 48;
            Assert.That(HudIcons.Distance(HudIcon.Arrow, 12, 4), Is.LessThan(0), "the tip is up");
            Assert.That(HudIcons.Distance(HudIcon.Arrow, 12, 19), Is.GreaterThan(0), "the notch is down");
            var ahead = HudIcons.Mask(HudIcon.Arrow, n); var right = HudIcons.Mask(HudIcon.Arrow, n, 90); var back = HudIcons.Mask(HudIcon.Arrow, n, 180);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    Assert.That(right[y * n + x], Is.EqualTo(ahead[(n - 1 - x) * n + y]).Within(1e-3f));
                    Assert.That(back[y * n + x], Is.EqualTo(ahead[(n - 1 - y) * n + (n - 1 - x)]).Within(1e-3f));
                }
            Assert.That(right[24 * n + 40], Is.GreaterThan(0.9f), "a quarter turn clockwise puts the tip on the right");
            Assert.That(HudIcons.Sector(0), Is.Zero); Assert.That(HudIcons.Sector(360), Is.Zero); Assert.That(HudIcons.Sector(5), Is.Zero);
            Assert.That(HudIcons.Sector(6), Is.EqualTo(1)); Assert.That(HudIcons.Sector(11.25f), Is.EqualTo(1));
            Assert.That(HudIcons.Sector(-90), Is.EqualTo(24)); Assert.That(HudIcons.Sector(810), Is.EqualTo(8)); Assert.That(HudIcons.Sector(354), Is.EqualTo(31));
            Assert.That(HudIcons.Sector(float.NaN), Is.Zero); Assert.That(HudIcons.Sector(-1e9f), Is.InRange(0, HudIcons.Sectors - 1));
        }
        [Test] public void TheSkyTheStationsAndTheLandmarksHaveTheirOwnSigns()
        {
            Assert.That(HudIcons.ForHour(DayCycle.StartHour), Is.EqualTo(HudIcon.Sun));
            Assert.That(HudIcons.ForHour(DayCycle.GoldenHour), Is.EqualTo(HudIcon.SunLow));
            Assert.That(HudIcons.ForHour(DayCycle.DuskHour), Is.EqualTo(HudIcon.Moon));
            Assert.That(HudIcons.ForHour(DayCycle.NightHour), Is.EqualTo(HudIcon.Moon));
            Assert.That(HudIcons.ForStation(OfficeStation.Desk), Is.EqualTo(HudIcon.Monitor));
            Assert.That(HudIcons.ForStation(OfficeStation.Street), Is.EqualTo(HudIcon.Lift));
            Assert.That(HudIcons.ForStation(OfficeStation.ReturnOffice), Is.EqualTo(HudIcon.Lift));
            Assert.That(HudIcons.ForStation(OfficeStation.Exit), Is.EqualTo(HudIcon.Exit));
            Assert.That(HudIcons.ForStation(OfficeStation.CityExchange), Is.EqualTo(HudIcon.Candles));
            Assert.That(HudIcons.ForStation(OfficeStation.CrosswalkButton), Is.EqualTo(HudIcon.Button));
            Assert.That(HudIcons.ForStation(OfficeStation.Car), Is.EqualTo(HudIcon.Car));
            Assert.That(HudIcons.ForStation(OfficeStation.Employee), Is.EqualTo(HudIcon.Talk));
            Assert.That(HudIcons.ForStation(OfficeStation.Supervisor), Is.EqualTo(HudIcon.Talk));
            foreach (OfficeStation station in Enum.GetValues(typeof(OfficeStation))) Assert.That(HudIcons.All, Does.Contain(HudIcons.ForStation(station)));
            Assert.That(CityDistrictPlan.Destinations.Select(d => HudIcons.ForDestination(d.Id)), Is.Unique, "every landmark of the map has a sign of its own");
            Assert.That(HudIcons.ForDestination("exchange"), Is.EqualTo(HudIcon.Bank));
            Assert.That(HudIcons.ForDestination("parking"), Is.EqualTo(HudIcon.Parking));
            Assert.That(HudIcons.ForDestination("office"), Is.EqualTo(HudIcon.Lift));
        }

        // ---------- The way on foot ----------
        [Test] public void OnFootTheWayOverTheBoulevardGoesByTheZebraFromKerbToKerb()
        {
            var exchange = CityDistrictPlan.Find("exchange"); var park = CityDistrictPlan.Find("park"); var cafe = CityDistrictPlan.Find("cafe");
            Assert.That(CityDistrictPlan.Bearing(0, 0, 0, 5), Is.EqualTo(0).Within(1e-4f));
            Assert.That(CityDistrictPlan.Bearing(0, 0, 5, 0), Is.EqualTo(90).Within(1e-4f));
            Assert.That(CityDistrictPlan.Bearing(0, 0, 0, -5), Is.EqualTo(180).Within(1e-4f));
            Assert.That(CityDistrictPlan.Bearing(0, 0, -5, 0), Is.EqualTo(270).Within(1e-4f));
            Assert.That(CityDistrictPlan.AcrossBoulevard(CityBlockPlan.SpawnZ, exchange), Is.True);
            Assert.That(CityDistrictPlan.AcrossBoulevard(CityBlockPlan.SpawnZ, park), Is.False);
            Assert.That(CityDistrictPlan.AcrossBoulevard(60, cafe), Is.True);
            float south = CityCrosswalk.SouthCurbZ - 0.8f, north = CityCrosswalk.NorthCurbZ + 0.8f;
            CityDistrictPlan.NextLeg(-31, 16, exchange, out float x, out float z); Assert.That(x, Is.Zero); Assert.That(z, Is.EqualTo(south));
            CityDistrictPlan.NextLeg(1, south + 1, exchange, out x, out z); Assert.That(x, Is.Zero); Assert.That(z, Is.EqualTo(north), "at the kerb: over the zebra");
            CityDistrictPlan.NextLeg(6, 60, cafe, out x, out z); Assert.That(z, Is.EqualTo(north), "from the north the way starts at the north kerb");
            CityDistrictPlan.NextLeg(5, 15, park, out x, out z); Assert.That(x, Is.EqualTo(park.X)); Assert.That(z, Is.EqualTo(park.Z));
            Assert.That(CityDistrictPlan.WalkDistance(5, 15, park), Is.EqualTo(CityDistrictPlan.Distance(5, 15, park)));
            Assert.That(CityDistrictPlan.WalkDistance(-31, 16, exchange), Is.EqualTo(Length(-31, 16 - south) + (north - south) + Length(0, 68 - north)).Within(1e-3));
            Assert.That(CityDistrictPlan.WalkDistance(-31, 16, exchange), Is.GreaterThan(CityDistrictPlan.Distance(-31, 16, exchange)));
            Assert.Throws<ArgumentException>(() => CityDistrictPlan.NextLeg(float.NaN, 0, park, out x, out z));
            Assert.Throws<ArgumentException>(() => CityDistrictPlan.AcrossBoulevard(0, null));
        }

        // ---------- The HUD ----------
        [Test] public void InTheOfficeTheHudIsTheDayTheClockTheMoneyAndTheKeyToWhatIsAimedAt()
        {
            var frame = Office(); frame.Target = "Рабочий стол / звонки клиентам"; frame.TargetStation = OfficeStation.Desk;
            var items = Build(frame);
            Assert.That(Icons(items), Is.SupersetOf(new[] { HudIcon.Calendar, HudIcon.Sun, HudIcon.Coin, HudIcon.Bill, HudIcon.Monitor }));
            Assert.That(Texts(items), Is.SupersetOf(new[] { "2", "09:00", "$120", "$300", "E", "Рабочий стол" }));
            Assert.That(items.Single(i => i.Kind == HudKind.Meter).Value, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(items.Single(i => i.Kind == HudKind.Crosshair).Active, Is.True);
            Assert.That(Icons(items), Does.Not.Contain(HudIcon.Arrow), "no compass in the office");
            // The clock under its sun and the prompt are centred lines.
            var sun = Icon(items, HudIcon.Sun); var clock = items.Single(i => i.Text == "09:00");
            Assert.That((sun.X + clock.X + measure.Width("09:00", HudFont.Clock)) / 2, Is.EqualTo(HudPlan.Middle).Within(0.5f));
            var prompt = PromptLine(items); var title = prompt.Single(i => i.Kind == HudKind.Text);
            Assert.That(prompt.Select(i => i.Kind), Is.EqualTo(new[] { HudKind.Key, HudKind.Icon, HudKind.Text }));
            Assert.That((prompt.Min(i => i.X) + title.X + measure.Width(title.Text, HudFont.Heading)) / 2, Is.EqualTo(HudPlan.Middle).Within(0.5f));
            // The first seconds of the day: the walking keys with their pictograms.
            Assert.That(Icons(items), Is.SupersetOf(new[] { HudIcon.Walker, HudIcon.Mouse, HudIcon.Eye, HudIcon.Pointer, HudIcon.Pause }));
            Assert.That(Texts(items), Is.SupersetOf(new[] { "W", "A", "S", "D", "Esc" }));
            // Nothing aimed at: no prompt at all; later in the day no keys either.
            frame.Target = null; frame.WalkKeys = false; items = Build(frame);
            Assert.That(PromptLine(items), Is.Empty); Assert.That(items.Single(i => i.Kind == HudKind.Crosshair).Active, Is.False);
            Assert.That(Icons(items), Does.Not.Contain(HudIcon.Walker));
            Assert.That(Texts(items), Is.EquivalentTo(new[] { "2", "09:00", "$120", "$300" }), "four figures and nothing else");
            // The sky over the clock follows the hour.
            frame.Hour = 18.5f; Assert.That(Icons(Build(frame)), Does.Contain(HudIcon.SunLow));
            frame.Hour = 20.5f; Assert.That(Icon(Build(frame), HudIcon.Moon).Tone, Is.EqualTo(HudTone.Ink));
        }
        [Test] public void UnderAPanelOnlyTheTopRowStaysAndInputProblemsStayWords()
        {
            var frame = Office(); frame.Exploring = false; frame.Target = "Рабочий стол"; var items = Build(frame);
            Assert.That(items.Any(i => i.Kind == HudKind.Crosshair), Is.False); Assert.That(items.All(i => i.Y < 120), Is.True);
            Assert.That(Icons(items), Is.EquivalentTo(new[] { HudIcon.Calendar, HudIcon.Sun, HudIcon.Coin, HudIcon.Bill }));
            frame.Exploring = true; frame.InputAvailable = false; Assert.That(Texts(Build(frame)), Does.Contain(HudPlan.InputMissing));
            frame.InputAvailable = true; frame.Captured = false; Assert.That(Texts(Build(frame)), Does.Contain(HudPlan.ClickToPlay));
            frame.Captured = true; frame.WalkKeys = false; frame.Toast = new string('x', 140);
            var toast = Build(frame).Single(i => i.Kind == HudKind.Text && i.Text.StartsWith("xxx"));
            Assert.That(toast.Text.Length, Is.EqualTo(103)); Assert.That(toast.Text, Does.EndWith("…")); Assert.That(toast.Tone, Is.EqualTo(HudTone.Muted));
            frame.WalkKeys = true; Assert.That(Texts(Build(frame)).Any(t => t.StartsWith("xxx")), Is.False, "the keys come before the news");
            frame.Cash = 900; Assert.That(Build(frame).Single(i => i.Kind == HudKind.Meter).Value, Is.EqualTo(1));
        }
        [Test] public void TheHiredBrokerSeesTheDealsAndTheNextStepUnderTheFlag()
        {
            var frame = Office(); frame.Employee = true; frame.Deals = 2; frame.SalesTarget = 5; frame.Guidance = "Позвоните клиенту";
            var items = Build(frame);
            Assert.That(Texts(items), Does.Contain("2/5")); Assert.That(Icons(items), Does.Contain(HudIcon.Contract));
            Assert.That(Icons(items), Does.Not.Contain(HudIcon.Bill));
            Assert.That(items.Single(i => i.Kind == HudKind.Meter).Value, Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(Icon(items, HudIcon.Flag).Tone, Is.EqualTo(HudTone.Gold)); Assert.That(Texts(items), Does.Contain("Позвоните клиенту"));
            // In the city the next step starts with the lift back: its sign instead of the sentence.
            frame.InCity = true; items = Build(frame);
            Assert.That(Icons(items), Does.Contain(HudIcon.Lift)); Assert.That(Icons(items), Does.Not.Contain(HudIcon.Flag));
            frame.SalesTarget = 0; Assert.That(Build(frame).Single(i => i.Kind == HudKind.Meter).Value, Is.EqualTo(1));
        }
        [Test] public void TheCompassPointsToTheLandmarkAndOverTheZebraWhenTheBoulevardLiesBetween()
        {
            var exchange = CityDistrictPlan.Find("exchange");
            // South of the boulevard, the exchange to the north: first to the zebra's kerb, its sign between the arrow and the bank.
            var frame = City(-10, 15, "exchange"); var items = Build(frame); var arrow = Icon(items, HudIcon.Arrow);
            Assert.That(arrow.Angle, Is.EqualTo(CityDistrictPlan.Bearing(-10, 15, 0, CityCrosswalk.SouthCurbZ - 0.8f)).Within(0.01f));
            Assert.That(arrow.Tone, Is.EqualTo(HudTone.Gold)); Assert.That(arrow.Y, Is.InRange(70f, 120f), "under the clock");
            Assert.That(Icons(items), Is.SupersetOf(new[] { HudIcon.Crosswalk, HudIcon.Bank }));
            Assert.That(Icon(items, HudIcon.Crosswalk).X, Is.InRange(arrow.X, Icon(items, HudIcon.Bank).X));
            Assert.That(Texts(items), Does.Contain(Math.Ceiling(CityDistrictPlan.WalkDistance(-10, 15, exchange)) + " м"));
            // The arrow turns with the view: looking east, the same way is a quarter turn more to the left.
            frame.Heading = 90; Assert.That(Icon(Build(frame), HudIcon.Arrow).Angle, Is.EqualTo((arrow.Angle + 270) % 360).Within(0.01f));
            // At the near kerb the arrow turns over the zebra, straight ahead looking north.
            Assert.That(Icon(Build(City(0, CityCrosswalk.SouthCurbZ - 1, "exchange")), HudIcon.Arrow).Angle, Is.EqualTo(0).Within(0.01f));
            // On the same side: straight to the park, no zebra.
            items = Build(City(0, 15, "park"));
            Assert.That(Icon(items, HudIcon.Arrow).Angle, Is.EqualTo(CityDistrictPlan.Bearing(0, 15, -31, 16)).Within(0.01f));
            Assert.That(Icons(items), Does.Not.Contain(HudIcon.Crosswalk)); Assert.That(Icons(items), Does.Contain(HudIcon.Tree));
            // At the landmark: the green tick instead of the arrow and the metres.
            items = Build(City(-31, 16.5f, "park"));
            Assert.That(Icons(items), Does.Not.Contain(HudIcon.Arrow)); Assert.That(Icon(items, HudIcon.Check).Tone, Is.EqualTo(HudTone.Go));
            Assert.That(Texts(items).Any(t => t.EndsWith(" м")), Is.False);
            // Behind the wheel: the straight distance, no zebra.
            frame = City(-10, 15, "exchange"); frame.Driving = true; items = Build(frame);
            Assert.That(Icons(items), Does.Not.Contain(HudIcon.Crosswalk));
            Assert.That(Texts(items), Does.Contain(Math.Ceiling(CityDistrictPlan.Distance(-10, 15, exchange)) + " м"));
            // An unknown landmark falls back to the lift.
            Assert.That(Icons(Build(City(0, 15, "nowhere"))), Does.Contain(HudIcon.Lift));
        }
        [Test] public void NearTheCrossingTheSignalIsTheRedOrGreenManWithItsSeconds()
        {
            var signal = new CrosswalkSignal(); var frame = City(0, 25, "exchange"); frame.Crossing = signal;
            // The cars have their full green and nobody has asked: the red man, E and the button.
            var items = Build(frame); var man = Icon(items, HudIcon.Standing);
            Assert.That(man.Tone, Is.EqualTo(HudTone.Stop)); Assert.That(man.Y, Is.EqualTo(92), "under the day");
            Assert.That(Icons(items), Does.Contain(HudIcon.Button)); Assert.That(Icons(items), Does.Not.Contain(HudIcon.Hourglass));
            Assert.That(items.Any(i => i.Kind == HudKind.Key && i.Text == "E" && i.Y > man.Y && i.Y < man.Y + man.H), Is.True);
            // Asked: the hourglass and the seconds until the green man.
            signal.RequestWalk(); items = Build(frame);
            Assert.That(Icons(items), Does.Contain(HudIcon.Hourglass)); Assert.That(Icons(items), Does.Not.Contain(HudIcon.Button));
            Assert.That(Texts(items), Does.Contain(CityCrosswalk.Cue(signal).Seconds + " с"));
            // The walk: the green man and the seconds left in green.
            while (!signal.PedestrianGreen) signal.Advance(0.05, false, false);
            items = Build(frame); Assert.That(Icon(items, HudIcon.Walker).Tone, Is.EqualTo(HudTone.Go));
            var seconds = items.Single(i => i.Kind == HudKind.Text && i.Text == CityCrosswalk.Cue(signal).Seconds + " с");
            Assert.That(seconds.Tone, Is.EqualTo(HudTone.Go)); Assert.That(Icons(items), Does.Not.Contain(HudIcon.Standing));
            // Its end: the man blinks twice a second and the seconds turn gold.
            while (!signal.PedestrianFlashing) signal.Advance(0.05, false, false);
            frame.Time = 10; items = Build(frame); Assert.That(Icons(items), Does.Contain(HudIcon.Walker));
            Assert.That(items.Single(i => i.Kind == HudKind.Text && i.Text.EndsWith(" с")).Tone, Is.EqualTo(HudTone.Gold));
            frame.Time = 10.5; Assert.That(Icons(Build(frame)), Does.Not.Contain(HudIcon.Walker));
            // Under the next step when there is one; never in the office or behind the wheel.
            frame.Guidance = "Позвоните клиенту"; Assert.That(Build(frame).Single(i => i.Kind == HudKind.Text && i.Text.EndsWith(" с")).Y, Is.EqualTo(192));
            frame.InCity = false; Assert.That(Texts(Build(frame)).Any(t => t.EndsWith(" с")), Is.False);
            frame.InCity = true; frame.Driving = true; Assert.That(Texts(Build(frame)).Any(t => t.EndsWith(" с")), Is.False);
        }
        [Test] public void BehindTheWheelTheHudIsTheSpeedTheGearAndTheWayToTheParking()
        {
            var frame = City(10, 33, "office"); frame.Driving = true; frame.SpeedKmh = 47.4f; frame.Gear = "3"; frame.DriveKeys = true;
            var items = Build(frame);
            Assert.That(items.Any(i => i.Kind == HudKind.Crosshair), Is.False, "the driver does not aim");
            var speed = items.Single(i => i.Kind == HudKind.Text && i.Text == "47");
            Assert.That(speed.Font, Is.EqualTo(HudFont.Speed)); Assert.That(speed.Align, Is.EqualTo(HudAlign.Right));
            Assert.That(Texts(items), Does.Contain("км/ч"));
            var gear = items.Single(i => i.Kind == HudKind.Key && i.Text == "3");
            Assert.That(gear.Tone, Is.EqualTo(HudTone.Ink)); Assert.That(gear.X + gear.W, Is.LessThan(1400 - measure.Width("47", HudFont.Speed)));
            // Away from the lay-by: the parking sign, the arrow to it and the metres.
            var line = PromptLine(items);
            Assert.That(Icons(line), Is.EqualTo(new[] { HudIcon.Parking, HudIcon.Arrow }));
            Assert.That(line.Single(i => i.Kind == HudKind.Icon && i.Icon == HudIcon.Arrow).Angle,
                Is.EqualTo(CityDistrictPlan.Bearing(10, 33, CityRoads.ParkX, CityRoads.ParkZ)).Within(0.01f));
            Assert.That(line.Single(i => i.Kind == HudKind.Text).Text, Is.EqualTo(Math.Ceiling(Length(CityRoads.ParkX - 10, CityRoads.ParkZ - 33)) + " м"));
            Assert.That(line.All(i => i.Tone == HudTone.Ink), Is.True);
            frame.CarNotice = true; Assert.That(PromptLine(Build(frame)).All(i => i.Tone == HudTone.Gold), Is.True, "gold after E away from the lay-by");
            // The first seconds: the pedals, the wheel and the handbrake on the space bar.
            Assert.That(Icons(items), Is.SupersetOf(new[] { HudIcon.Gauge, HudIcon.Wheel, HudIcon.Handbrake, HudIcon.Space, HudIcon.Mouse, HudIcon.Eye, HudIcon.Pause }));
            Assert.That(Icons(items), Does.Not.Contain(HudIcon.Walker));
            frame.DriveKeys = false; Assert.That(Icons(Build(frame)), Does.Not.Contain(HudIcon.Handbrake));
            frame.Gear = "R"; Assert.That(Build(frame).Single(i => i.Kind == HudKind.Key && i.Text == "R").Tone, Is.EqualTo(HudTone.Gold));
            // In the lay-by: E, the door and «Выйти».
            frame.CanLeaveCar = true; frame.CarNotice = false; line = PromptLine(Build(frame));
            Assert.That(Texts(line), Is.EqualTo(new[] { "E", HudPlan.LeaveCar })); Assert.That(Icons(line), Is.EqualTo(new[] { HudIcon.Exit }));
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void ControlsAreCentredKeysWithPictogramsThatFitThePausePanel(bool driving, bool full)
        {
            var items = new HudPlan(measure).Controls(driving, full, 716).ToList(); AssertOnCanvas(items);
            float left = items.Min(i => i.X), right = items.Max(i => i.X + i.W);
            Assert.That((left + right) / 2, Is.EqualTo(HudPlan.Middle).Within(0.5f));
            Assert.That(left, Is.GreaterThanOrEqualTo(400)); Assert.That(right, Is.LessThanOrEqualTo(1040), "inside the pause panel");
            Assert.That(items.All(i => i.Y >= 716 && i.Y + i.H <= 716 + HudPlan.SmallKey), Is.True);
            Assert.That(items.Any(i => i.Kind == HudKind.Text), Is.False, "keys and pictograms only");
            // One after another without overlaps; the space bar's sign sits inside its key.
            var row = items.Where(i => !(i.Kind == HudKind.Icon && i.Icon == HudIcon.Space)).OrderBy(i => i.X).ToList();
            for (int k = 1; k < row.Count; k++) Assert.That(row[k].X, Is.GreaterThanOrEqualTo(row[k - 1].X + row[k - 1].W));
            var icons = Icons(items);
            Assert.That(icons, Is.SupersetOf(new[] { HudIcon.Mouse, HudIcon.Eye }));
            if (driving)
            {
                Assert.That(icons, Is.SupersetOf(new[] { HudIcon.Gauge, HudIcon.Wheel, HudIcon.Handbrake, HudIcon.Space }));
                var bar = items.Single(i => i.Kind == HudKind.Key && i.Text == ""); var sign = Icon(items, HudIcon.Space);
                Assert.That(sign.X + sign.W / 2, Is.EqualTo(bar.X + bar.W / 2).Within(0.01f));
                Assert.That(icons.Contains(HudIcon.Exit) && icons.Contains(HudIcon.Parking), Is.EqualTo(full), "the pause tells where to get out");
                Assert.That(icons.Contains(HudIcon.Pause), Is.EqualTo(!full));
            }
            else
            {
                Assert.That(icons, Is.SupersetOf(new[] { HudIcon.Walker, HudIcon.Pointer, HudIcon.Pause }));
                Assert.That(icons.Contains(HudIcon.Run), Is.EqualTo(full)); Assert.That(Texts(items).Contains("Shift"), Is.EqualTo(full));
            }
        }
        [Test] public void TheHudTitleIsThePartBeforeTheSlashKeptPerString()
        {
            var plan = new HudPlan(measure); string source = "Лифт в офис / вернуться к работе";
            Assert.That(plan.ShortTitle(source), Is.EqualTo("Лифт в офис"));
            Assert.That(plan.ShortTitle(source), Is.SameAs(plan.ShortTitle(source)));
            Assert.That(plan.ShortTitle("Кофе"), Is.EqualTo("Кофе")); Assert.That(plan.ShortTitle(null), Is.EqualTo(""));
            Assert.Throws<ArgumentNullException>(() => new HudPlan(null)); Assert.Throws<ArgumentNullException>(() => plan.Build(null));
        }
    }
}
