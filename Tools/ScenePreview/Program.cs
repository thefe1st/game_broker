// Renders still previews of the procedural office and city without the Unity editor:
//   dotnet run --project Tools/ScenePreview -c Release -- <output folder> [shot names...] [--fast] [--no-hud] [--at=SECONDS]
//   dotnet run --project Tools/ScenePreview -c Release -- --check      (CI smoke check, no image files)
// The real OfficeScene builders run against UnityStubs.cs; Renderer.cs approximates the built-in forward renderer.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Broker.Core;
using Broker.Runtime.Office;
using UnityEngine;

namespace Broker.Tools.ScenePreview
{
    public sealed class Shot
    {
        public string Name; public Vector3 Local; public float Yaw, Pitch, Fov = 72; public bool Employee, City, Hud = true; public string Target;
        // Seconds on the boulevard clock from a fresh start: 18–32 s is the pedestrians' green.
        public double Traffic = 1.6;
        // Quotes: a work day with prices on the Exchange board. Crossing: the HUD shows the signal panel.
        public bool Quotes, Crossing;
    }

    public static class Program
    {
        private static readonly Shot[] Shots =
        {
            new Shot { Name = "plaza-north", Local = new Vector3(0, CityBlockPlan.GroundY + 1.7f, 15), Yaw = 0, Pitch = -4, City = true, Target = "Прогулка · лифт ведёт в офис" },
            new Shot { Name = "plaza-office", Local = new Vector3(3.2f, CityBlockPlan.GroundY + 1.7f, 22.6f), Yaw = 192, Pitch = -9, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 14 },
            new Shot { Name = "sunset-west", Local = new Vector3(-12, CityBlockPlan.GroundY + 1.7f, 21), Yaw = -96, Pitch = -2, City = true, Target = "Прогулка · лифт ведёт в офис" },
            new Shot { Name = "east-street", Local = new Vector3(31, CityBlockPlan.GroundY + 1.7f, 13), Yaw = 200, Pitch = 2, City = true, Target = "Прогулка · лифт ведёт в офис" },
            new Shot { Name = "boulevard", Local = new Vector3(-19, CityBlockPlan.GroundY + 1.7f, 26.6f), Yaw = 66, Pitch = 1, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 1.8 },
            new Shot { Name = "crosswalk", Local = new Vector3(2.2f, CityBlockPlan.GroundY + 1.7f, 24.4f), Yaw = -14, Pitch = -3, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 21, Crossing = true },
            new Shot { Name = "exchange", Local = new Vector3(-4.6f, CityBlockPlan.GroundY + 1.82f, 42.5f), Yaw = 10, Pitch = -8, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 10 },
            new Shot { Name = "exchange-hall", Local = new Vector3(-1.5f, CityBlockPlan.GroundY + 2.8f, 62.2f), Yaw = 4, Pitch = 3, City = true, Target = "Табло котировок", Quotes = true },
            new Shot { Name = "cafe", Local = new Vector3(22.5f, CityBlockPlan.GroundY + 1.65f, -2.9f), Yaw = 132, Pitch = -9, City = true, Target = "Стойка кофейни «Бык и медведь»" },
            new Shot { Name = "tunnel", Local = new Vector3(36, CityBlockPlan.GroundY + 1.7f, 27.2f), Yaw = 80, Pitch = 1, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 6 },
            new Shot { Name = "office", Local = new Vector3(-1.2f, 1.7f, -3.6f), Yaw = 8, Pitch = 9, Target = "Телефон — звонки" },
            new Shot { Name = "office-employee", Local = new Vector3(-2.2f, 1.7f, -3.2f), Yaw = 32, Pitch = 10, Employee = true, Target = "Телефон — звонки" },
            new Shot { Name = "aerial", Local = new Vector3(0, 60, -32), Yaw = 0, Pitch = 42, Fov = 60, City = true, Hud = false },
        };

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            bool check = args.Contains("--check"), noHud = args.Contains("--no-hud"), fast = args.Contains("--fast");
            // --at=SECONDS re-times the selected shots on the boulevard clock (to pick a moment for a new shot).
            var at = args.FirstOrDefault(a => a.StartsWith("--at="));
            double? retime = at == null ? null : double.Parse(at.Substring(5), CultureInfo.InvariantCulture);
            var positional = args.Where(a => !a.StartsWith("--")).ToList();
            string output = check ? null : positional.Count > 0 ? positional[0] : "preview";
            var wanted = new HashSet<string>(positional.Skip(check ? 0 : 1));
            if (output != null) Directory.CreateDirectory(output);
            var clock = Stopwatch.StartNew();
            var root = new GameObject("Office scene");
            var scene = root.AddComponent<OfficeScene>();
            var day = new BrokerDay(); var quotesDay = new BrokerDay(6, mode: CareerMode.EmployeeWork, clientIds: ClientCatalog.Appointments(6));
            Console.WriteLine($"scene built in {clock.ElapsedMilliseconds} ms: {GameObject.All.Count} objects, warnings {UnityEngine.Debug.Warnings.Count}");
            foreach (var warning in UnityEngine.Debug.Warnings.Distinct().Take(8)) Console.WriteLine("  warning: " + warning);
            int failures = UnityEngine.Debug.Warnings.Count;
            bool employee = false;
            foreach (var shot in Shots.OrderBy(s => s.Employee))
            {
                if (wanted.Count > 0 && !wanted.Contains(shot.Name)) continue;
                if (shot.Employee && !employee) { employee = true; scene.ConfigureCareerMode(CareerMode.Employee); day = new BrokerDay(mode: CareerMode.Employee); }
                var renderer = check ? new Renderer { Width = 240, Height = 160, Supersample = 1 } : new Renderer { Supersample = fast ? 1 : 2 };
                var camera = Prepare(scene, root, shot, shot.Quotes ? quotesDay : day, retime ?? shot.Traffic);
                renderer.Render(SceneSoup.Collect(), camera);
                if (check)
                {
                    // Smoke check: the builders ran, the shot sees geometry and lighting produced a non-flat image.
                    float covered = renderer.Coverage(), spread = renderer.LuminanceSpread();
                    bool ok = covered > 0.25f && spread > 0.08f;
                    if (!ok) failures++;
                    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {shot.Name}: geometry {covered:P0}, luminance spread {spread:0.00}");
                    continue;
                }
                string ppm = Path.Combine(output, shot.Name + ".ppm"), png = Path.Combine(output, shot.Name + ".png");
                renderer.SavePpm(ppm);
                var overlay = new List<string> { ppm };
                AddLabels(overlay, renderer, camera);
                if (shot.Hud && !noHud) AddHud(overlay, shot, day, scene);
                overlay.Add(png);
                Run("magick", overlay); File.Delete(ppm);
                Console.WriteLine($"{shot.Name}: {renderer.Stats}");
            }
            Console.WriteLine($"done in {clock.Elapsed.TotalSeconds:0.0} s" + (check ? $", {failures} problem(s)" : ""));
            return failures == 0 ? 0 : 1;
        }

        private static ViewCamera Prepare(OfficeScene scene, GameObject root, Shot shot, BrokerDay day, double traffic)
        {
            scene.Present(false, 1, Vector2.zero);
            // Street shots stand the player where the camera is, so the passers-by stop for it as they do in the game.
            if (shot.City && shot.Hud)
            {
                scene.VisitCity(true);
                scene.Player.ResetAt(root.transform.TransformPoint(new Vector3(shot.Local.x, shot.Local.y - 1.65f, shot.Local.z)));
            }
            else scene.ResetPlayer();
            SetTraffic(scene, traffic);
            scene.Refresh(day);
            var camera = ViewCamera.From(scene.Player.Eye);
            var rotation = Quaternion.Euler(shot.Pitch, shot.Yaw, 0);
            camera.Position = root.transform.TransformPoint(shot.Local);
            camera.Right = rotation * Vector3.right; camera.Up = rotation * Vector3.up; camera.Forward = rotation * Vector3.forward; camera.Fov = shot.Fov;
            // Labels turn toward the active camera in Refresh; aim them at the preview camera instead.
            foreach (var label in GameObject.All.Select(g => g.GetComponent<TextMesh>()).Where(t => t != null && t.name == "Employee label"))
                label.transform.rotation = rotation;
            return camera;
        }

        // Every shot starts the boulevard clock afresh, so the same shot always shows the same signal, cars and walkers.
        private static void SetTraffic(OfficeScene scene, double seconds)
        {
            scene.WorldMotionEnabled = false; scene.ResetCity(); scene.AdvanceCity(seconds);
        }

        private static string Rgba(Color c, float alpha = -1) =>
            string.Format(CultureInfo.InvariantCulture, "rgba({0},{1},{2},{3:0.###})", (int)(c.r * 255), (int)(c.g * 255), (int)(c.b * 255), alpha < 0 ? c.a : alpha);

        // World TextMesh labels: billboards sized like Unity (em = fontSize × characterSize / 10), hidden when occluded or seen from behind.
        private static void AddLabels(List<string> args, Renderer renderer, ViewCamera camera)
        {
            foreach (var go in GameObject.All)
            {
                var text = go.GetComponent<TextMesh>(); var meshRenderer = go.GetComponent<MeshRenderer>();
                if (text == null || !go.activeInHierarchy || meshRenderer == null || !meshRenderer.enabled || string.IsNullOrEmpty(text.text)) continue;
                var position = go.transform.position; var facing = go.transform.rotation * Vector3.forward;
                if (Vector3.Dot(facing, position - camera.Position) <= 0) continue;
                if (!renderer.Project(position, out float sx, out float sy, out float depth)) continue;
                if (sx < -200 || sy < -50 || sx > renderer.Width + 200 || sy > renderer.Height + 50) continue;
                if (renderer.DepthAt(sx, sy) < depth - 0.08f) continue;
                float em = text.fontSize * text.characterSize / 10f * go.transform.lossyScale.y, pixels = em * renderer.FocalPixels / depth;
                if (pixels < 5) continue;
                args.AddRange(new[] { "-gravity", "Center", "-font", "Liberation-Sans", "-pointsize", pixels.ToString("0.#", CultureInfo.InvariantCulture),
                    "-fill", Rgba(text.color), "-stroke", "none", "-annotate", Offset(sx - renderer.Width / 2f, sy - renderer.Height / 2f), text.text });
            }
        }
        private static string Offset(float dx, float dy) =>
            (dx >= 0 ? "+" : "-") + Math.Abs(dx).ToString("0", CultureInfo.InvariantCulture) + (dy >= 0 ? "+" : "-") + Math.Abs(dy).ToString("0", CultureInfo.InvariantCulture);

        // Mirrors PrototypeBootstrap.DrawWorldHUD on its 1440 × 960 canvas: translucent wash, thin white rules, crosshair.
        private static void AddHud(List<string> args, Shot shot, BrokerDay day, OfficeScene scene)
        {
            var ink = Hex("#F3F7FB"); var muted = Hex("#BAC6D7"); var accent = Hex("#83E4D1"); var border = Hex("#384B63");
            void Rect(float x, float y, float w, float h, string fill) =>
                args.AddRange(new[] { "-gravity", "NorthWest", "-stroke", "none", "-fill", fill, "-draw", string.Format(CultureInfo.InvariantCulture, "rectangle {0},{1} {2},{3}", x, y, x + w - 1, y + h - 1) });
            void Panel(float x, float y, float w, float h)
            {
                Rect(x, y, w, h, "rgba(10,13,18,0.34)"); string line = "rgba(255,255,255,0.82)"; const float t = 1.5f, corner = 12;
                Rect(x, y, w, t, line); Rect(x, y + h - t, w, t, line);
                Rect(x, y, t, corner, line); Rect(x + w - t, y, t, corner, line);
                Rect(x, y + h - corner, t, corner, line); Rect(x + w - t, y + h - corner, t, corner, line);
            }
            void Text(float x, float y, string value, int size, Color color, bool bold) =>
                args.AddRange(new[] { "-gravity", "NorthWest", "-font", bold ? "Liberation-Sans-Bold" : "Liberation-Sans", "-pointsize", size.ToString(CultureInfo.InvariantCulture),
                    "-fill", Rgba(color, 1), "-stroke", "none", "-annotate", Offset(x, y - size * 0.12f), value });
            bool employee = shot.Employee;
            Panel(32, 32, 320, 80); Text(56, 44, "МОЙКА КАПИТАЛ", 22, ink, true);
            Text(56, 80, shot.City ? "Деловой квартал" : (employee ? "Наёмный брокер · день " : "Офис · день ") + day.DayNumber, 16, muted, false);
            int seconds = (int)Math.Ceiling(day.RemainingSeconds);
            Panel(616, 32, 208, 80); Text(648, 48, $"{seconds / 60:00}:{seconds % 60:00}", 32, ink, true);
            Panel(1104, 32, 304, 80); Text(1128, 44, employee ? "$" + day.Cash + " · личные" : "$" + day.Cash + " / " + day.TotalExpenses, 22, ink, true);
            float meter = employee ? day.DealsCount / (float)Math.Max(1, day.SalesTarget) : day.Cash / (float)Math.Max(1, day.TotalExpenses);
            Rect(1128, 96, 256, 5, Rgba(border, 1)); if (meter > 0) Rect(1128, 96, 256 * Math.Clamp(meter, 0, 1), 5, Rgba(accent, 1));
            float arm = 7; string cross = "rgba(255,255,255,0.9)", shadow = "rgba(0,0,0,0.35)";
            Rect(720 - arm + 1, 480, arm * 2, 2, shadow); Rect(720, 480 - arm + 1, 2, arm * 2, shadow);
            Rect(720 - arm, 479, arm * 2, 2, cross); Rect(719, 480 - arm, 2, arm * 2, cross);
            Panel(420, 784, 600, 88); Text(452, 808, shot.Target ?? "", 22, ink, true);
            if (shot.City)
            {
                // As in the game: the chosen landmark (the office lift by default), its distance and direction.
                var destination = CityDistrictPlan.Find("office");
                Panel(32, 288, 320, 144); Text(56, 308, destination.Name, 22, ink, true);
                Text(56, 348, Math.Ceiling(CityDistrictPlan.Distance(shot.Local.x, shot.Local.z, destination)) + " м · "
                    + CityDistrictPlan.Direction(shot.Local.x, shot.Local.z, destination) + "\nКарта — у стенда / в паузе", 16, muted, false);
            }
            if (shot.Crossing)
            {
                Panel(32, 448, 320, 96); Text(56, 462, "ПЕРЕХОД / СВЕТОФОР", 14, accent, true);
                Text(56, 490, scene.Life.Hint, 16, muted, false);
            }
            if (employee)
            {
                Panel(384, 136, 672, 124); Text(408, 152, "ДАЛЬШЕ / ОРИЕНТИР " + day.DealsCount + "/" + day.SalesTarget, 16, accent, true);
                args.AddRange(new[] { "(", "-size", "624x", "-background", "none", "-font", "Liberation-Sans", "-pointsize", "16", "-fill", Rgba(muted, 1),
                    "caption:" + day.EmploymentNextAction, ")", "-gravity", "NorthWest", "-geometry", "+408+190", "-composite" });
            }
        }
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }

        private static void Run(string file, List<string> args)
        {
            var info = new ProcessStartInfo(file) { RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in args) info.ArgumentList.Add(a);
            using var process = Process.Start(info); string error = process.StandardError.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(file + " failed: " + error);
        }
    }
}
