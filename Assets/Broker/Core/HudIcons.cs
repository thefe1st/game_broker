using System;
using System.Collections.Generic;

namespace Broker.Core
{
    // The HUD's pictograms (stage 8, the author: «меньше текста — больше интуитивно понятного интерфейса и значков»).
    public enum HudIcon
    {
        // The top row: the day, the light of the hour, the money, the bills, the deals and the next step.
        Calendar, Sun, SunLow, Moon, Coin, Bill, Contract, Flag,
        // What the crosshair rests on.
        Monitor, Talk, Cup, Star, Exit, Lift, Pin, Chart, Candles, Button, Car,
        // Finding the way: the arrow, «you are there», the zebra across the boulevard, the lay-by and the landmarks.
        Arrow, Check, Crosswalk, Parking, Tree, Tower, Column, Bank,
        // The pedestrian signal and the wait for it.
        Walker, Standing, Hourglass,
        // The controls: the mouse and the look, the hand that acts, the pause, the run; the speed dial, the wheel, the handbrake
        // and the space bar's mark.
        Mouse, Eye, Pointer, Pause, Run, Gauge, Wheel, Handbrake, Space
    }

    // Engine-free vector pictograms for the HUD on a 24-unit grid (y down), rasterised into white coverage masks that the
    // game and the preview tint. Every glyph is a list of signed-distance shapes added or cut in order, so the edges come out
    // anti-aliased at any size and the arrow turns to any angle.
    public static class HudIcons
    {
        public const float Grid = 24;
        // The arrow is pre-turned in this many steps for the game (degrees = step × 360 / Sectors).
        public const int Sectors = 32;
        public static readonly HudIcon[] All = (HudIcon[])Enum.GetValues(typeof(HudIcon));

        // Coverage 0…1 of a size × size square, row 0 at the top, turned clockwise by `degrees` round its centre.
        public static float[] Mask(HudIcon icon, int size, float degrees = 0)
        {
            if (size < 1 || size > 1024) throw new ArgumentOutOfRangeException(nameof(size));
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) throw new ArgumentException("Invalid angle.", nameof(degrees));
            var glyph = Lookup(icon); var mask = new float[size * size];
            float scale = size / Grid; double a = degrees * Math.PI / 180;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / scale - Grid / 2, py = (y + 0.5f) / scale - Grid / 2;
                    float gx = px * cos + py * sin + Grid / 2, gy = -px * sin + py * cos + Grid / 2;
                    mask[y * size + x] = Math.Min(1, Math.Max(0, 0.5f - glyph.Distance(gx, gy) * scale));
                }
            return mask;
        }
        // The pre-turned step nearest to an angle in degrees (any sign, any number of turns): 0 … Sectors − 1.
        public static int Sector(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees)) return 0;
            double turns = degrees / 360.0; turns -= Math.Floor(turns);
            return (int)Math.Round(turns * Sectors) % Sectors;
        }
        // Signed distance in grid units from a point of the grid to the glyph's edge (negative inside).
        public static float Distance(HudIcon icon, float x, float y) => Lookup(icon).Distance(x, y);

        // The sky over the clock: the sun by day, the low sun from the golden hour, the moon from dusk.
        public static HudIcon ForHour(float hour) => hour >= DayCycle.DuskHour ? HudIcon.Moon : hour >= DayCycle.GoldenHour ? HudIcon.SunLow : HudIcon.Sun;
        // What the crosshair rests on, as on its sign in the world.
        public static HudIcon ForStation(OfficeStation station)
        {
            switch (station)
            {
                case OfficeStation.Desk: return HudIcon.Monitor;
                case OfficeStation.Coffee: case OfficeStation.CityCafe: return HudIcon.Cup;
                case OfficeStation.Upgrade: return HudIcon.Star;
                case OfficeStation.Exit: return HudIcon.Exit;
                case OfficeStation.Street: case OfficeStation.ReturnOffice: return HudIcon.Lift;
                case OfficeStation.CityMap: return HudIcon.Pin;
                case OfficeStation.CityCompanies: return HudIcon.Chart;
                case OfficeStation.CityExchange: return HudIcon.Candles;
                case OfficeStation.CrosswalkButton: return HudIcon.Button;
                case OfficeStation.Car: return HudIcon.Car;
                default: return HudIcon.Talk;
            }
        }
        // A landmark of the district map (CityDistrictPlan.Destinations).
        public static HudIcon ForDestination(string id)
        {
            switch (id)
            {
                case "square": return HudIcon.Pin;
                case "park": return HudIcon.Tree;
                case "business": return HudIcon.Tower;
                case "cafe": return HudIcon.Cup;
                case "crossing": return HudIcon.Crosswalk;
                case "northsquare": return HudIcon.Column;
                case "exchange": return HudIcon.Bank;
                case "parking": return HudIcon.Parking;
                default: return HudIcon.Lift;
            }
        }

        // ---------- Shapes ----------
        private abstract class Shape { public abstract float D(float x, float y); }
        private sealed class Disc : Shape
        {
            private readonly float cx, cy, r;
            public Disc(float cx, float cy, float r) { this.cx = cx; this.cy = cy; this.r = r; }
            public override float D(float x, float y) => Length(x - cx, y - cy) - r;
        }
        // A rectangle from (x0, y0) to (x1, y1) with corners of radius r.
        private sealed class Box : Shape
        {
            private readonly float cx, cy, hx, hy, r;
            public Box(float x0, float y0, float x1, float y1, float r = 0)
            { cx = (x0 + x1) / 2; cy = (y0 + y1) / 2; hx = Math.Abs(x1 - x0) / 2; hy = Math.Abs(y1 - y0) / 2; this.r = Math.Min(r, Math.Min(hx, hy)); }
            public override float D(float x, float y)
            {
                float qx = Math.Abs(x - cx) - hx + r, qy = Math.Abs(y - cy) - hy + r;
                return Length(Math.Max(qx, 0), Math.Max(qy, 0)) + Math.Min(Math.Max(qx, qy), 0) - r;
            }
        }
        // A stroke of half-width r along a polyline, round at the ends and the bends.
        private sealed class Path : Shape
        {
            private readonly float[] xs, ys; private readonly float r;
            public Path(float r, params float[] points)
            {
                if (points.Length < 2 || points.Length % 2 != 0) throw new ArgumentException("Path needs x, y pairs.");
                this.r = r; xs = new float[points.Length / 2]; ys = new float[points.Length / 2];
                for (int i = 0; i < xs.Length; i++) { xs[i] = points[2 * i]; ys[i] = points[2 * i + 1]; }
            }
            public Path(float r, List<float> points) : this(r, points.ToArray()) { }
            public override float D(float x, float y)
            {
                float best = float.MaxValue;
                if (xs.Length == 1) return Length(x - xs[0], y - ys[0]) - r;
                for (int i = 0; i + 1 < xs.Length; i++) best = Math.Min(best, Segment(x, y, xs[i], ys[i], xs[i + 1], ys[i + 1]));
                return best - r;
            }
        }
        // A filled polygon, points in order.
        private sealed class Poly : Shape
        {
            private readonly float[] xs, ys; private readonly float round;
            public Poly(float round, params float[] points)
            {
                if (points.Length < 6 || points.Length % 2 != 0) throw new ArgumentException("Polygon needs three x, y pairs.");
                this.round = round; xs = new float[points.Length / 2]; ys = new float[points.Length / 2];
                for (int i = 0; i < xs.Length; i++) { xs[i] = points[2 * i]; ys[i] = points[2 * i + 1]; }
            }
            public override float D(float x, float y)
            {
                int n = xs.Length; float best = float.MaxValue; bool inside = false;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    best = Math.Min(best, Segment(x, y, xs[j], ys[j], xs[i], ys[i]));
                    if ((ys[i] > y) != (ys[j] > y) && x < (xs[j] - xs[i]) * (y - ys[i]) / (ys[j] - ys[i]) + xs[i]) inside = !inside;
                }
                return (inside ? -best : best) - round;
            }
        }
        // The rim of a shape, half-width r on either side of its edge.
        private sealed class Rim : Shape
        {
            private readonly Shape shape; private readonly float r;
            public Rim(Shape shape, float r) { this.shape = shape; this.r = r; }
            public override float D(float x, float y) => Math.Abs(shape.D(x, y)) - r;
        }
        // What two shapes share.
        private sealed class Both : Shape
        {
            private readonly Shape a, b;
            public Both(Shape a, Shape b) { this.a = a; this.b = b; }
            public override float D(float x, float y) => Math.Max(a.D(x, y), b.D(x, y));
        }
        // A shape scaled by `scale` round the grid's corner and moved by (dx, dy).
        private sealed class Placed : Shape
        {
            private readonly Shape shape; private readonly float scale, dx, dy;
            public Placed(Shape shape, float scale, float dx, float dy) { this.shape = shape; this.scale = scale; this.dx = dx; this.dy = dy; }
            public override float D(float x, float y) => shape.D((x - dx) / scale, (y - dy) / scale) * scale;
        }
        private sealed class Glyph : Shape
        {
            private readonly List<(Shape shape, bool cut)> parts = new List<(Shape, bool)>();
            public override float D(float x, float y) => Distance(x, y);
            public Glyph Add(Shape shape) { parts.Add((shape, false)); return this; }
            public Glyph Cut(Shape shape) { parts.Add((shape, true)); return this; }
            public float Distance(float x, float y)
            {
                float d = float.MaxValue;
                foreach (var (shape, cut) in parts) d = cut ? Math.Max(d, -shape.D(x, y)) : Math.Min(d, shape.D(x, y));
                return d;
            }
        }
        private static float Length(float x, float y) => (float)Math.Sqrt(x * x + y * y);
        private static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax, ey = by - ay, wx = x - ax, wy = y - ay, ee = ex * ex + ey * ey;
            float t = ee <= 0 ? 0 : Math.Min(1, Math.Max(0, (wx * ex + wy * ey) / ee));
            return Length(wx - ex * t, wy - ey * t);
        }
        // An arc round (cx, cy), anticlockwise on the screen from a0 to a1 degrees (0 = right, 90 = up), as a stroke.
        private static Path Arc(float cx, float cy, float radius, float a0, float a1, float r, int steps = 16)
        {
            var points = new List<float>();
            for (int i = 0; i <= steps; i++)
            {
                double a = (a0 + (a1 - a0) * i / steps) * Math.PI / 180;
                points.Add(cx + radius * (float)Math.Cos(a)); points.Add(cy - radius * (float)Math.Sin(a));
            }
            return new Path(r, points);
        }
        // A stroke from the centre outwards at angle a (degrees, 0 = right, 90 = up), from radius r0 to r1.
        private static Path Ray(float cx, float cy, float a, float r0, float r1, float r)
        {
            double t = a * Math.PI / 180; float c = (float)Math.Cos(t), s = (float)Math.Sin(t);
            return new Path(r, cx + c * r0, cy - s * r0, cx + c * r1, cy - s * r1);
        }

        // ---------- Glyphs ----------
        private static readonly Dictionary<HudIcon, Glyph> glyphs = new Dictionary<HudIcon, Glyph>();
        private static Glyph Lookup(HudIcon icon)
        {
            lock (glyphs)
            {
                if (!glyphs.TryGetValue(icon, out var glyph)) glyphs[icon] = glyph = Build(icon);
                return glyph;
            }
        }
        private const float Line = 1.15f;
        private static Glyph Build(HudIcon icon)
        {
            var g = new Glyph();
            switch (icon)
            {
                case HudIcon.Calendar:
                    // A page with a dark band and two rings; the HUD writes the day's number in the empty part.
                    g.Add(new Rim(new Box(3, 4.5f, 21, 21.5f, 2.6f), Line * 0.9f)).Add(new Both(new Box(3, 4.5f, 21, 21.5f, 2.6f), new Box(0, 0, 24, 9.2f)));
                    foreach (float x in new[] { 8f, 16f }) g.Cut(new Path(1.9f, x, 1.6f, x, 6.6f)).Add(new Path(0.95f, x, 2.3f, x, 6.2f));
                    break;
                case HudIcon.Sun:
                    g.Add(new Disc(12, 12, 4.4f));
                    for (int k = 0; k < 8; k++) g.Add(Ray(12, 12, k * 45, 7.2f, 9.5f, Line * 0.9f));
                    break;
                case HudIcon.SunLow:
                    // Half the sun over the horizon, its rays upwards.
                    g.Add(new Both(new Disc(12, 16.6f, 5.4f), new Box(0, 0, 24, 15.2f))).Add(new Path(Line * 0.9f, 2.5f, 17.6f, 21.5f, 17.6f));
                    foreach (float a in new[] { 0f, 45f, 90f, 135f, 180f }) g.Add(Ray(12, 16.6f, a, 7.6f, a % 180 == 0 ? 9.4f : 10.2f, Line * 0.85f));
                    g.Cut(new Box(0, 17.6f + Line, 24, 24)).Add(new Path(Line * 0.9f, 2.5f, 17.6f, 21.5f, 17.6f)).Add(new Path(Line * 0.9f, 6.5f, 21, 17.5f, 21));
                    break;
                case HudIcon.Moon:
                    g.Add(new Disc(11.4f, 12.6f, 8.6f)).Cut(new Disc(16.6f, 8.4f, 7.4f)).Add(new Disc(18.6f, 15.8f, 1.2f)).Add(new Disc(15.2f, 3.6f, 0.9f));
                    break;
                case HudIcon.Coin:
                    // A coin with the dollar knocked out of it.
                    g.Add(new Disc(12, 12, 9.8f)).Cut(Arc(12, 9.15f, 2.85f, 35, 270, 0.95f)).Cut(Arc(12, 14.85f, 2.85f, 90, -145, 0.95f))
                        .Cut(new Path(0.95f, 12, 4.7f, 12, 6.6f)).Cut(new Path(0.95f, 12, 17.4f, 12, 19.3f));
                    break;
                case HudIcon.Bill:
                    // A bill with a torn edge and three lines.
                    g.Add(new Poly(0.4f, 5, 2.8f, 19, 2.8f, 19, 21.2f, 17.25f, 19.6f, 15.5f, 21.2f, 13.75f, 19.6f, 12, 21.2f, 10.25f, 19.6f, 8.5f, 21.2f, 6.75f, 19.6f, 5, 21.2f));
                    foreach (float y in new[] { 7.4f, 11f, 14.6f }) g.Cut(new Path(0.85f, 8.4f, y, y > 14 ? 12.6f : 15.6f, y));
                    break;
                case HudIcon.Contract:
                    // A signed page: the corner turned, a tick on it.
                    g.Add(new Poly(0.3f, 5, 2.5f, 14.2f, 2.5f, 19, 7.3f, 19, 21.5f, 5, 21.5f)).Cut(new Poly(0, 13.4f, 1, 20, 1, 20, 8.1f, 13.4f, 8.1f))
                        .Add(new Poly(0.3f, 14.6f, 2.9f, 14.6f, 6.9f, 18.6f, 6.9f)).Cut(new Path(1.25f, 8.2f, 13.6f, 11, 16.4f, 15.9f, 10.4f));
                    break;
                case HudIcon.Flag:
                    g.Add(new Path(Line, 6, 3, 6, 21.4f)).Add(new Poly(0.3f, 7, 3.4f, 19.2f, 3.4f, 15.8f, 7.6f, 19.2f, 11.8f, 7, 11.8f));
                    break;
                case HudIcon.Monitor:
                    // The broker's screen: a rising line on it, the stand and its foot.
                    g.Add(new Rim(new Box(2.6f, 3.2f, 21.4f, 16, 1.8f), Line * 0.9f)).Add(new Path(0.85f, 6.2f, 12.6f, 9.6f, 9.4f, 12.4f, 11.4f, 17.6f, 6.6f))
                        .Add(new Box(10.5f, 16, 13.5f, 19.4f)).Add(new Box(6.8f, 19.2f, 17.2f, 21.2f, 1));
                    break;
                case HudIcon.Talk:
                    g.Add(new Box(2.6f, 3.4f, 21.4f, 16.8f, 4.6f)).Add(new Poly(0.3f, 6.4f, 14.5f, 12, 14.5f, 5.4f, 21.2f));
                    foreach (float x in new[] { 7.6f, 12f, 16.4f }) g.Cut(new Disc(x, 10.1f, 1.5f));
                    break;
                case HudIcon.Cup:
                    // The café's cup on its saucer with two wisps of steam, the handle to the right (as on its sign).
                    g.Add(new Poly(0.4f, 4.6f, 9.4f, 16.8f, 9.4f, 15.2f, 17.4f, 6.2f, 17.4f)).Add(Arc(16.4f, 12.6f, 2.7f, -80, 80, 1.0f))
                        .Add(new Box(2.8f, 18.6f, 19.6f, 20.6f, 1));
                    foreach (float x in new[] { 8.2f, 12.6f }) g.Add(new Path(0.8f, x, 7.2f, x + 1.1f, 5.5f, x, 3.8f, x + 0.9f, 2.2f));
                    break;
                case HudIcon.Star:
                {
                    var points = new List<float>();
                    for (int k = 0; k < 10; k++)
                    {
                        double a = (90 + k * 36) * Math.PI / 180; float r = k % 2 == 0 ? 10 : 4.2f;
                        points.Add(12 + r * (float)Math.Cos(a)); points.Add(12.8f - r * (float)Math.Sin(a));
                    }
                    g.Add(new Poly(0.6f, points.ToArray()));
                    break;
                }
                case HudIcon.Exit:
                    // Out through the door, as over the office door: the frame on the right, an arrow running into it.
                    g.Add(new Path(Line, 12.6f, 21, 12.6f, 3, 21, 3, 21, 21)).Cut(new Box(10, 8.3f, 15, 15.7f)).Add(new Path(1.3f, 2.6f, 12, 12.2f, 12))
                        .Add(new Poly(0.3f, 17.6f, 12, 11.6f, 6.4f, 11.6f, 17.6f));
                    break;
                case HudIcon.Lift:
                    g.Add(new Rim(new Box(4.2f, 2.4f, 19.8f, 21.6f, 2.4f), Line * 0.9f)).Add(new Poly(0.3f, 12, 5.6f, 16.2f, 10.2f, 7.8f, 10.2f))
                        .Add(new Poly(0.3f, 12, 18.4f, 16.2f, 13.8f, 7.8f, 13.8f));
                    break;
                case HudIcon.Pin:
                    g.Add(new Disc(12, 9.4f, 7)).Add(new Poly(0, 5.9f, 12.6f, 18.1f, 12.6f, 12, 22)).Cut(new Disc(12, 9.4f, 2.7f));
                    break;
                case HudIcon.Chart:
                    // Three rising bars under a rising line with its head: the market going up, as on the companies' stand.
                    for (int k = 0; k < 3; k++) g.Add(new Box(3.6f + k * 6.2f, 15.6f - k * 3.4f, 7.6f + k * 6.2f, 21.4f, 0.6f));
                    g.Cut(new Path(2.2f, 3, 11.4f, 9, 6.6f, 12.6f, 9.4f, 18.4f, 4.4f)).Add(new Path(1.05f, 3, 11.4f, 9, 6.6f, 12.6f, 9.4f, 18.4f, 4.4f))
                        .Add(new Poly(0.3f, 21.6f, 1.8f, 20.6f, 8.4f, 15.2f, 3.0f));
                    break;
                case HudIcon.Candles:
                    // The quotes board: three candles of a price chart, wicks through them.
                    foreach (var (x, top, body0, body1, bottom) in new[] { (5.5f, 6f, 9f, 15f, 18f), (12f, 9f, 11.4f, 19f, 21.6f), (18.5f, 2.4f, 5f, 11.6f, 14.6f) })
                        g.Add(new Path(0.7f, x, top, x, bottom)).Add(new Box(x - 2.3f, body0, x + 2.3f, body1, 0.5f));
                    break;
                case HudIcon.Button:
                    g.Add(new Rim(new Box(3, 3, 21, 21, 4.6f), Line * 0.9f)).Add(new Disc(12, 12, 5));
                    break;
                case HudIcon.Car:
                    // The sedan from the side: body and cabin, two windows, the wheels standing clear of it.
                    g.Add(new Poly(0.9f, 2.6f, 16.4f, 2.6f, 12.8f, 5.4f, 11.6f, 8.4f, 7.6f, 15.2f, 7.6f, 18.6f, 11.4f, 21.4f, 12.4f, 21.4f, 16.4f))
                        .Cut(new Poly(0.2f, 8.9f, 9.2f, 11.4f, 9.2f, 11.4f, 11.5f, 6.9f, 11.5f)).Cut(new Poly(0.2f, 12.8f, 9.2f, 14.7f, 9.2f, 16.9f, 11.5f, 12.8f, 11.5f))
                        .Cut(new Disc(7, 17.2f, 3.3f)).Cut(new Disc(17, 17.2f, 3.3f)).Add(new Disc(7, 17.2f, 2.3f)).Add(new Disc(17, 17.2f, 2.3f));
                    break;
                case HudIcon.Arrow:
                    g.Add(new Poly(0.5f, 12, 2.4f, 19.4f, 20.6f, 12, 16.2f, 4.6f, 20.6f));
                    break;
                case HudIcon.Check:
                    g.Add(new Disc(12, 12, 9.8f)).Cut(new Path(1.4f, 7.2f, 12.4f, 10.6f, 15.8f, 16.9f, 8.8f));
                    break;
                case HudIcon.Crosswalk:
                    // The crossing's sign: the walker over the zebra's stripes.
                    g.Add(new Placed(Lookup(HudIcon.Walker), 0.78f, 3, 0.3f));
                    for (int k = 0; k < 4; k++) g.Add(new Box(2.6f + k * 5, 19.6f, 6.4f + k * 5, 22, 0.5f));
                    break;
                case HudIcon.Parking:
                    // The parking sign: a square with the P knocked out.
                    g.Add(new Box(2.4f, 2.4f, 21.6f, 21.6f, 4.4f)).Cut(new Box(8.2f, 6, 11.4f, 18.4f, 0.4f))
                        .Cut(new Both(new Rim(new Box(8.2f, 6, 16.8f, 13.6f, 3.8f), 1.6f), new Box(9.6f, 0, 24, 24)));
                    break;
                case HudIcon.Tree:
                    g.Add(new Disc(12, 8.8f, 6.6f)).Add(new Disc(7.4f, 12, 4.4f)).Add(new Disc(16.6f, 12, 4.4f)).Cut(new Box(0, 16, 24, 24))
                        .Cut(new Path(2.2f, 12, 15.4f, 12, 17)).Add(new Path(1.2f, 12, 10.6f, 12, 21.4f)).Add(new Path(0.8f, 7, 21.4f, 17, 21.4f));
                    break;
                case HudIcon.Tower:
                    // Office fronts: a tall block and a low one with rows of windows.
                    g.Add(new Box(3.6f, 2.6f, 13.4f, 21.4f, 0.6f)).Add(new Box(14.8f, 9.4f, 20.6f, 21.4f, 0.6f));
                    for (int row = 0; row < 4; row++)
                        foreach (float x in new[] { 6.6f, 10.4f }) g.Cut(new Box(x - 1.1f, 5.2f + row * 3.8f, x + 1.1f, 7.2f + row * 3.8f, 0.2f));
                    for (int row = 0; row < 2; row++) g.Cut(new Box(16.6f, 12.2f + row * 3.8f, 18.8f, 14.2f + row * 3.8f, 0.2f));
                    break;
                case HudIcon.Column:
                    // The rostral column of the Exchange square: capital, fluted shaft, plinth.
                    g.Add(new Box(6.6f, 2.6f, 17.4f, 5.2f, 0.6f)).Add(new Box(8.8f, 5.8f, 15.2f, 18.4f, 0.4f)).Add(new Box(6, 19, 18, 21.6f, 0.6f));
                    foreach (float x in new[] { 10.9f, 13.1f }) g.Cut(new Path(0.45f, x, 7.4f, x, 16.8f));
                    break;
                case HudIcon.Bank:
                    // The Exchange: a pediment on four columns over steps.
                    g.Add(new Poly(0.4f, 12, 2.2f, 21.4f, 7.6f, 2.6f, 7.6f));
                    for (int k = 0; k < 4; k++) g.Add(new Box(4.2f + k * 4.6f, 9.4f, 6.8f + k * 4.6f, 17.8f, 0.3f));
                    g.Add(new Box(2.6f, 19, 21.4f, 21.6f, 0.5f));
                    break;
                case HudIcon.Walker:
                    // The green man of the signal, in his stride.
                    g.Add(new Disc(13.4f, 3.6f, 2.25f)).Add(new Path(1.55f, 12.8f, 7.6f, 11.6f, 13.2f)).Add(new Path(1.3f, 11.6f, 13.2f, 14.2f, 16.6f, 15, 21))
                        .Add(new Path(1.3f, 11.6f, 13.2f, 9.6f, 16.8f, 7, 20.4f)).Add(new Path(1.05f, 12.6f, 8.4f, 15.4f, 11.2f, 17.4f, 11.6f))
                        .Add(new Path(1.05f, 12.4f, 8.6f, 9.8f, 10.8f, 8.6f, 13.6f));
                    break;
                case HudIcon.Standing:
                    // The red man of the signal, standing still.
                    g.Add(new Disc(12, 3.7f, 2.3f)).Add(new Box(9.5f, 7, 14.5f, 14.4f, 1.5f)).Add(new Path(1.25f, 10.75f, 13.6f, 10.75f, 21.2f))
                        .Add(new Path(1.25f, 13.25f, 13.6f, 13.25f, 21.2f)).Cut(new Path(0.45f, 12, 15, 12, 22)).Add(new Path(1.05f, 7.6f, 8.2f, 7.6f, 14.8f))
                        .Add(new Path(1.05f, 16.4f, 8.2f, 16.4f, 14.8f));
                    break;
                case HudIcon.Hourglass:
                    g.Add(new Path(Line, 5.6f, 2.8f, 18.4f, 2.8f)).Add(new Path(Line, 5.6f, 21.2f, 18.4f, 21.2f))
                        .Add(new Rim(new Poly(0, 7.2f, 3.6f, 16.8f, 3.6f, 13, 12, 16.8f, 20.4f, 7.2f, 20.4f, 11, 12), 0.85f))
                        .Add(new Poly(0.2f, 9.2f, 20, 14.8f, 20, 12, 16)).Add(new Poly(0.2f, 9.6f, 6.4f, 14.4f, 6.4f, 12, 10.2f));
                    break;
                case HudIcon.Mouse:
                    g.Add(new Rim(new Box(6, 2.4f, 18, 21.6f, 6), Line * 0.9f)).Add(new Path(Line * 0.9f, 12, 5.6f, 12, 9));
                    break;
                case HudIcon.Eye:
                {
                    var lens = new Both(new Disc(12, 20.6f, 12.6f), new Disc(12, 3.4f, 12.6f));
                    g.Add(new Rim(lens, Line * 0.9f)).Add(new Disc(12, 12, 3.4f)).Cut(new Disc(13.3f, 10.7f, 1));
                    break;
                }
                case HudIcon.Pointer:
                    // The hand that presses: the index finger up, the fist, the thumb to the left.
                    g.Add(new Path(1.75f, 10.2f, 3.6f, 10.2f, 12.8f)).Add(new Box(8.4f, 10.4f, 18.6f, 21.2f, 3.2f))
                        .Add(new Path(1.6f, 8.6f, 15.4f, 5.6f, 12.6f)).Cut(new Path(0.4f, 12.2f, 11.6f, 12.2f, 13.6f)).Cut(new Path(0.4f, 15.4f, 11.6f, 15.4f, 13.6f));
                    break;
                case HudIcon.Pause:
                    g.Add(new Box(6.4f, 4.4f, 10.2f, 19.6f, 1.2f)).Add(new Box(13.8f, 4.4f, 17.6f, 19.6f, 1.2f));
                    break;
                case HudIcon.Run:
                    // Faster: the walker leaning into a run, two speed lines behind him.
                    g.Add(new Disc(15.6f, 3.8f, 2.25f)).Add(new Path(1.55f, 14.6f, 7.8f, 12.2f, 13)).Add(new Path(1.3f, 12.2f, 13, 16, 15.6f, 16.8f, 20.8f))
                        .Add(new Path(1.3f, 12.2f, 13, 10, 16.8f, 5.8f, 18.4f)).Add(new Path(1.05f, 14.2f, 8.6f, 17.4f, 11.4f, 20, 10.4f))
                        .Add(new Path(1.05f, 14, 8.8f, 11, 9.4f, 9.4f, 12)).Add(new Path(0.8f, 3, 7.4f, 6.8f, 7.4f)).Add(new Path(0.8f, 2.6f, 11.4f, 5.8f, 11.4f));
                    break;
                case HudIcon.Gauge:
                    // Speed up and slow down: the speedometer's dial with its needle.
                    g.Add(Arc(12, 14, 9.4f, 210, -30, Line, 24));
                    foreach (float a in new[] { 180f, 135f, 90f, 45f, 0f }) g.Add(Ray(12, 14, a, 5.6f, 7.2f, 0.8f));
                    g.Add(Ray(12, 14, 50, 0, 7.4f, 1.0f)).Add(new Disc(12, 14, 2.4f)).Add(new Path(0.8f, 8.4f, 20.6f, 15.6f, 20.6f));
                    break;
                case HudIcon.Wheel:
                    g.Add(new Rim(new Disc(12, 12, 8.6f), 1.35f)).Add(new Disc(12, 13, 2.9f)).Add(new Path(1.15f, 3.6f, 11, 9.6f, 12.6f))
                        .Add(new Path(1.15f, 14.4f, 12.6f, 20.4f, 11)).Add(new Path(1.15f, 12, 15, 12, 20.4f));
                    break;
                case HudIcon.Handbrake:
                    // The dashboard's handbrake: a P in a ring between two brackets.
                    g.Add(new Rim(new Disc(12, 12, 6.6f), Line * 0.85f)).Add(Arc(12, 12, 9.5f, 140, 220, Line * 0.85f, 8)).Add(Arc(12, 12, 9.5f, -40, 40, Line * 0.85f, 8))
                        .Add(new Path(0.95f, 10.4f, 8.6f, 10.4f, 15.6f)).Add(new Path(0.95f, 10.4f, 8.6f, 12.4f, 8.6f)).Add(Arc(12.4f, 10.6f, 2, 90, -90, 0.95f, 10))
                        .Add(new Path(0.95f, 10.4f, 12.6f, 12.4f, 12.6f));
                    break;
                case HudIcon.Space:
                    g.Add(new Path(Line, 4.6f, 9.4f, 4.6f, 14.6f, 19.4f, 14.6f, 19.4f, 9.4f));
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(icon));
            }
            return g;
        }
    }
}
