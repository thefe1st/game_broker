using System;
using System.Collections.Generic;

namespace Broker.Core
{
    public enum HudKind { Text, Icon, Key, Meter, Crosshair }
    // The HUD's type sizes (HudPlan.FontSize) and what goes in them: headings and figures, small text, the clock, the letter
    // of the E key, the letters of the controls row and the speedometer.
    public enum HudFont { Heading, Text, Clock, Key, SmallKey, Speed }
    public enum HudAlign { Left, Center, Right, Middle }
    // Warm white, its muted shade, the gold accent, the signal's green and red (HudPlan.ToneHex).
    public enum HudTone { Ink, Muted, Gold, Go, Stop }

    // One thing on the HUD's 1440 × 960 canvas. Text: a label in the box, aligned in it (Middle: centred both ways). Icon: a
    // pictogram filling the box, turned clockwise by Angle (the arrow, in HudIcons.Sector steps). Key: a key's 2-pixel
    // outline with its letter centred (the space bar's pictogram is an Icon item of its own). Meter: a thin bar filled to
    // Value. Crosshair: the cross in the middle, wider and gold when Active. Every one of them has the HUD's dark drop
    // shadow one and two pixels down and right.
    public struct HudItem
    {
        public HudKind Kind;
        public float X, Y, W, H;
        public string Text;
        public HudFont Font;
        public HudAlign Align;
        public HudTone Tone;
        public HudIcon Icon;
        public float Angle, Value;
        public bool Active;
    }

    // Measures a line of HUD text in canvas pixels: the game's GUI styles or the preview's fonts.
    public interface IHudText { float Width(string text, HudFont font); }

    // What the HUD shows at a moment, gathered by the game (or the preview) from the day and the scene.
    public sealed class HudFrame
    {
        public int Day = 1;
        public float Hour = DayCycle.StartHour;
        // The hired broker sees the deals towards the target instead of the bills; the founder and the hired broker see the
        // next step of their day (Guidance).
        public bool Employee;
        public int Cash, Expenses, Deals, SalesTarget;
        public string Guidance;
        // The world is being explored (no panel, no decision): otherwise only the top row.
        public bool Exploring = true, InputAvailable = true, Captured = true;
        public bool InCity, Driving;
        // The title of what the crosshair rests on ("Лифт в офис / …": the part before the slash is shown) and its station.
        public string Target;
        public OfficeStation TargetStation;
        public bool CanLeaveCar, CarNotice;
        public float SpeedKmh;
        public string Gear = "1";
        // Where the player (or the car) is on the plan, where the view looks (CityDistrictPlan.Bearing's degrees), the
        // chosen landmark of the map.
        public float X, Z, Heading;
        public string DestinationId = "office";
        // The crossing's signal while walking near it, otherwise null.
        public CrosswalkSignal Crossing;
        // Seconds of real time, for the flashing green.
        public double Time;
        // The controls row of the first seconds of a day on foot or behind the wheel; the latest news otherwise.
        public bool WalkKeys, DriveKeys;
        public string Toast;
    }

    // Stage 8: the HUD in pictograms instead of words (the author: «надписи все пора убирать … меньше текста — больше
    // интуитивно понятного интерфейса и значков»). One layout for the game and the preview: the day in a calendar top left,
    // the clock under its sun or moon in the middle with the arrow to the chosen landmark below it, the money with its
    // coin and the bills (or the deals) under the gold meter top right, the key with the pictogram of what the crosshair
    // rests on at the bottom, the speed and the gear bottom right in the car, the controls as keys with pictograms, the
    // pedestrian signal as the green or red man with its seconds.
    public sealed class HudPlan
    {
        public const float CanvasWidth = 1440, CanvasHeight = 960, Middle = 720;
        public const float KeySize = 40, SmallKey = 26, PromptY = 808, ControlsY = 898;
        private static readonly int[] fontSizes = { 22, 16, 32, 20, 14, 44 };
        public static int FontSize(HudFont font) => fontSizes[(int)font];
        public static bool Bold(HudFont font) => font != HudFont.Text;
        public static string ToneHex(HudTone tone)
        {
            switch (tone)
            {
                case HudTone.Muted: return "#E4DACD";
                case HudTone.Gold: return "#E9B56D";
                case HudTone.Go: return "#8FE58A";
                case HudTone.Stop: return "#FF7A66";
                default: return "#F5F0E6";
            }
        }
        // Lines that stay words: input the game cannot take yet.
        public const string InputMissing = "Ввод недоступен · открой рабочий экран", ClickToPlay = "Нажми в окне Game", LeaveCar = "Выйти";

        private readonly IHudText text;
        private readonly List<HudItem> items = new List<HudItem>();
        private string titleSource, title;
        public HudPlan(IHudText text) { this.text = text ?? throw new ArgumentNullException(nameof(text)); }

        public IReadOnlyList<HudItem> Build(HudFrame f)
        {
            if (f == null) throw new ArgumentNullException(nameof(f));
            items.Clear();
            DayCard(f); Clock(f); Money(f);
            if (!f.Exploring) return items;
            if (!f.Driving) items.Add(new HudItem { Kind = HudKind.Crosshair, Active = f.Target != null });
            Prompt(f);
            if (f.Guidance != null) Guidance(f);
            if (f.Driving) Speedometer(f);
            if (f.Driving && f.DriveKeys) ControlsRow(true, false, ControlsY);
            else if (!f.Driving && f.WalkKeys) ControlsRow(false, false, ControlsY);
            else if (f.Toast != null)
            {
                string news = f.Toast.Length > 105 ? f.Toast.Substring(0, 102) + "…" : f.Toast;
                AddText(news, 240, 892, 960, 48, HudFont.Text, HudAlign.Center, HudTone.Muted);
            }
            if (f.InCity)
            {
                Compass(f);
                if (!f.Driving && f.Crossing != null) Crossing(f, f.Guidance != null ? 184 : 92);
            }
            return items;
        }
        // The controls alone as one centred row at y, for the pause and the start of the day: on foot (with the run when
        // `full`) or behind the wheel (with getting out in the lay-by when `full`).
        public IReadOnlyList<HudItem> Controls(bool driving, bool full, float y)
        {
            items.Clear(); ControlsRow(driving, full, y); return items;
        }
        // "Лифт в офис / …" → "Лифт в офис", kept while the same title string comes back.
        public string ShortTitle(string source)
        {
            if (ReferenceEquals(source, titleSource)) return title;
            titleSource = source; int slash = (source ?? "").IndexOf('/');
            title = (slash < 0 ? source ?? "" : source.Substring(0, slash)).Trim();
            return title;
        }

        // ---------- Top row ----------
        private void DayCard(HudFrame f)
        {
            AddIcon(HudIcon.Calendar, 40, 26, 44, HudTone.Ink);
            AddText(f.Day.ToString(), 40, 43.6f, 44, 22, HudFont.Key, HudAlign.Middle, HudTone.Ink);
        }
        private void Clock(HudFrame f)
        {
            string clock = DayCycle.Clock(f.Hour); var sky = HudIcons.ForHour(f.Hour);
            float width = text.Width(clock, HudFont.Clock), left = Middle - (30 + 8 + width) / 2;
            AddIcon(sky, left, 31, 30, sky == HudIcon.Moon ? HudTone.Ink : HudTone.Gold);
            AddText(clock, left + 38, 26, width + 8, 44, HudFont.Clock, HudAlign.Left, HudTone.Ink);
        }
        private void Money(HudFrame f)
        {
            string cash = "$" + f.Cash; float width = text.Width(cash, HudFont.Heading);
            AddText(cash, 1040, 30, 360, 30, HudFont.Heading, HudAlign.Right, HudTone.Ink);
            AddIcon(HudIcon.Coin, 1400 - width - 8 - 24, 32, 24, HudTone.Gold);
            float value = f.Employee ? f.Deals / (float)Math.Max(1, f.SalesTarget) : f.Cash / (float)Math.Max(1, f.Expenses);
            items.Add(new HudItem { Kind = HudKind.Meter, X = 1144, Y = 68, W = 256, H = 4, Value = Math.Min(1, Math.Max(0, value)), Tone = HudTone.Gold });
            string goal = f.Employee ? f.Deals + "/" + f.SalesTarget : "$" + f.Expenses; float goalWidth = text.Width(goal, HudFont.Text);
            AddText(goal, 1144, 80, 256, 24, HudFont.Text, HudAlign.Right, HudTone.Muted);
            AddIcon(f.Employee ? HudIcon.Contract : HudIcon.Bill, 1400 - goalWidth - 6 - 20, 80, 20, HudTone.Muted);
        }
        private void Guidance(HudFrame f)
        {
            // In the city the next step is always back in the office: the lift says so instead of a sentence.
            AddIcon(f.InCity ? HudIcon.Lift : HudIcon.Flag, 40, 88, 24, HudTone.Gold);
            AddText(f.Guidance, 72, 90, 520, 84, HudFont.Text, HudAlign.Left, HudTone.Muted);
        }

        // ---------- The bottom line ----------
        private void Prompt(HudFrame f)
        {
            if (!f.InputAvailable) { AddText(InputMissing, 420, 812, 600, 32, HudFont.Heading, HudAlign.Center, HudTone.Ink); return; }
            if (!f.Captured) { AddText(ClickToPlay, 420, 812, 600, 32, HudFont.Heading, HudAlign.Center, HudTone.Ink); return; }
            if (f.Driving)
            {
                if (f.CanLeaveCar) KeyPrompt(HudIcon.Exit, LeaveCar);
                else ParkingGuide(f);
                return;
            }
            if (f.Target != null) KeyPrompt(HudIcons.ForStation(f.TargetStation), ShortTitle(f.Target));
        }
        // E, the pictogram and the short title as one centred line.
        private void KeyPrompt(HudIcon icon, string label)
        {
            float width = Math.Min(560, text.Width(label, HudFont.Heading)), left = Middle - (KeySize + 12 + 32 + 10 + width) / 2;
            AddKey("E", left, PromptY, KeySize, KeySize, HudFont.Key, HudTone.Ink);
            AddIcon(icon, left + KeySize + 12, PromptY + 4, 32, HudTone.Ink);
            AddText(label, left + KeySize + 12 + 32 + 10, PromptY + 4, width + 8, 32, HudFont.Heading, HudAlign.Left, HudTone.Ink);
        }
        // Away from the lay-by: the parking sign, the arrow to it and the metres; in gold for a moment after E.
        private void ParkingGuide(HudFrame f)
        {
            var tone = f.CarNotice ? HudTone.Gold : HudTone.Ink;
            double distance = Math.Sqrt(Square(CityRoads.ParkX - f.X) + Square(CityRoads.ParkZ - f.Z));
            string metres = Math.Ceiling(distance) + " м"; float width = text.Width(metres, HudFont.Heading);
            float left = Middle - (32 + 8 + 30 + 10 + width) / 2;
            AddIcon(HudIcon.Parking, left, PromptY + 4, 32, tone);
            AddArrow(CityDistrictPlan.Bearing(f.X, f.Z, CityRoads.ParkX, CityRoads.ParkZ) - f.Heading, left + 40, PromptY + 5, 30, tone);
            AddText(metres, left + 80, PromptY + 4, width + 8, 32, HudFont.Heading, HudAlign.Left, tone);
        }
        // Behind the wheel: the speed in big figures bottom right, km/h under it, the gear in a box beside it (gold in reverse).
        private void Speedometer(HudFrame f)
        {
            string speed = ((int)Math.Round(f.SpeedKmh)).ToString(); float width = text.Width(speed, HudFont.Speed);
            AddText(speed, 1160, 764, 240, 52, HudFont.Speed, HudAlign.Right, HudTone.Ink);
            AddText("км/ч", 1160, 818, 240, 22, HudFont.Text, HudAlign.Right, HudTone.Muted);
            AddKey(f.Gear ?? "", 1400 - width - 14 - 36, 772, 36, 36, HudFont.Key, f.Gear == "R" ? HudTone.Gold : HudTone.Ink);
        }

        // ---------- Finding the way ----------
        // Under the clock: the arrow toward the landmark (toward the zebra first when the boulevard lies between), the zebra
        // when the way goes over it, the landmark's pictogram and the metres; at the landmark a tick instead of the arrow.
        private void Compass(HudFrame f)
        {
            var destination = CityDistrictPlan.Find(f.DestinationId) ?? CityDistrictPlan.Find("office");
            var icon = HudIcons.ForDestination(destination.Id);
            if (CityDistrictPlan.Distance(f.X, f.Z, destination) <= 2)
            {
                AddIcon(HudIcon.Check, Middle - 30, 80, 26, HudTone.Go); AddIcon(icon, Middle + 4, 80, 26, HudTone.Ink);
                return;
            }
            bool viaZebra = !f.Driving && CityDistrictPlan.AcrossBoulevard(f.Z, destination);
            float toX = destination.X, toZ = destination.Z;
            if (!f.Driving) CityDistrictPlan.NextLeg(f.X, f.Z, destination, out toX, out toZ);
            double distance = f.Driving ? CityDistrictPlan.Distance(f.X, f.Z, destination) : CityDistrictPlan.WalkDistance(f.X, f.Z, destination);
            string metres = Math.Ceiling(distance) + " м"; float width = text.Width(metres, HudFont.Heading);
            float total = 28 + 8 + (viaZebra ? 24 + 6 : 0) + 26 + 8 + width, left = Middle - total / 2;
            AddArrow(CityDistrictPlan.Bearing(f.X, f.Z, toX, toZ) - f.Heading, left, 79, 28, HudTone.Gold); left += 36;
            if (viaZebra) { AddIcon(HudIcon.Crosswalk, left, 81, 24, HudTone.Muted); left += 30; }
            AddIcon(icon, left, 80, 26, HudTone.Ink); left += 34;
            AddText(metres, left, 79, width + 8, 30, HudFont.Heading, HudAlign.Left, HudTone.Ink);
        }
        // Near the crossing on foot, under the day: the green man with the seconds of the walk (blinking, the seconds in
        // gold, at its end), or the red man with the E key and the button, or the hourglass with the seconds of the wait.
        private void Crossing(HudFrame f, float y)
        {
            var cue = CityCrosswalk.Cue(f.Crossing);
            if (cue.Green)
            {
                if (!cue.Flashing || Math.Floor(f.Time * 2) % 2 == 0) AddIcon(HudIcon.Walker, 40, y, 40, HudTone.Go);
                AddText(cue.Seconds + " с", 92, y + 8, 120, 30, HudFont.Heading, HudAlign.Left, cue.Flashing ? HudTone.Gold : HudTone.Go);
                return;
            }
            AddIcon(HudIcon.Standing, 40, y, 40, HudTone.Stop);
            if (cue.Press) { AddKey("E", 92, y + 7, SmallKey, SmallKey, HudFont.SmallKey, HudTone.Ink); AddIcon(HudIcon.Button, 124, y + 8, 24, HudTone.Ink); }
            else
            {
                AddIcon(HudIcon.Hourglass, 92, y + 8, 24, HudTone.Muted);
                if (cue.Seconds > 0) AddText(cue.Seconds + " с", 122, y + 8, 120, 30, HudFont.Heading, HudAlign.Left, HudTone.Ink);
            }
        }

        // ---------- Controls ----------
        private struct Group
        {
            public string[] Keys; public HudIcon[] Icons;
            public Group(HudIcon[] icons, params string[] keys) { Icons = icons; Keys = keys; }
        }
        // A key named Mouse is the mouse's pictogram, a blank one the space bar.
        private const string MouseKey = "mouse";
        private static readonly Group[] walking =
        {
            new Group(new[] { HudIcon.Walker }, "W", "A", "S", "D"), new Group(new[] { HudIcon.Eye }, MouseKey),
            new Group(new[] { HudIcon.Pointer }, "E"), new Group(new[] { HudIcon.Pause }, "Esc"),
        };
        private static readonly Group[] walkingFull =
        {
            new Group(new[] { HudIcon.Walker }, "W", "A", "S", "D"), new Group(new[] { HudIcon.Run }, "Shift"), new Group(new[] { HudIcon.Eye }, MouseKey),
            new Group(new[] { HudIcon.Pointer }, "E"), new Group(new[] { HudIcon.Pause }, "Esc"),
        };
        private static readonly Group[] driving =
        {
            new Group(new[] { HudIcon.Gauge }, "W", "S"), new Group(new[] { HudIcon.Wheel }, "A", "D"), new Group(new[] { HudIcon.Handbrake }, " "),
            new Group(new[] { HudIcon.Eye }, MouseKey), new Group(new[] { HudIcon.Pause }, "Esc"),
        };
        private static readonly Group[] drivingFull =
        {
            new Group(new[] { HudIcon.Gauge }, "W", "S"), new Group(new[] { HudIcon.Wheel }, "A", "D"), new Group(new[] { HudIcon.Handbrake }, " "),
            new Group(new[] { HudIcon.Exit, HudIcon.Parking }, "E"), new Group(new[] { HudIcon.Eye }, MouseKey),
        };
        private float KeyWidth(string key) => key == MouseKey ? 24 : key == " " ? 58 : Math.Max(SmallKey, text.Width(key, HudFont.SmallKey) + 14);
        private void ControlsRow(bool car, bool full, float y)
        {
            var groups = car ? full ? drivingFull : driving : full ? walkingFull : walking;
            const float keyGap = 4, iconGap = 6, groupGap = 30, icon = 24;
            float total = -groupGap;
            foreach (var group in groups)
            {
                total += groupGap;
                foreach (string key in group.Keys) total += KeyWidth(key) + keyGap;
                total += iconGap - keyGap + group.Icons.Length * (icon + 2) - 2;
            }
            float x = Middle - total / 2;
            foreach (var group in groups)
            {
                foreach (string key in group.Keys)
                {
                    float w = KeyWidth(key);
                    if (key == MouseKey) AddIcon(HudIcon.Mouse, x, y + 1, icon, HudTone.Ink);
                    else if (key == " ") { AddKey("", x, y, w, SmallKey, HudFont.SmallKey, HudTone.Ink); AddIcon(HudIcon.Space, x + (w - 20) / 2, y + 3, 20, HudTone.Ink); }
                    else AddKey(key, x, y, w, SmallKey, HudFont.SmallKey, HudTone.Ink);
                    x += w + keyGap;
                }
                x += iconGap - keyGap;
                foreach (var pictogram in group.Icons) { AddIcon(pictogram, x, y + 1, icon, HudTone.Muted); x += icon + 2; }
                x += groupGap - 2;
            }
        }

        // ---------- Items ----------
        private void AddText(string value, float x, float y, float w, float h, HudFont font, HudAlign align, HudTone tone)
            => items.Add(new HudItem { Kind = HudKind.Text, Text = value, X = x, Y = y, W = w, H = h, Font = font, Align = align, Tone = tone });
        private void AddIcon(HudIcon icon, float x, float y, float size, HudTone tone)
            => items.Add(new HudItem { Kind = HudKind.Icon, Icon = icon, X = x, Y = y, W = size, H = size, Tone = tone });
        private void AddArrow(float degrees, float x, float y, float size, HudTone tone)
            => items.Add(new HudItem { Kind = HudKind.Icon, Icon = HudIcon.Arrow, X = x, Y = y, W = size, H = size, Tone = tone, Angle = (degrees % 360 + 360) % 360 });
        private void AddKey(string label, float x, float y, float w, float h, HudFont font, HudTone tone)
            => items.Add(new HudItem { Kind = HudKind.Key, Text = label, X = x, Y = y, W = w, H = h, Font = font, Align = HudAlign.Middle, Tone = tone });
        private static double Square(double value) => value * value;
    }
}
