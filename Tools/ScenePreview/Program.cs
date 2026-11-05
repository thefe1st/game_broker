// Renders still previews of the procedural office and city without the Unity editor:
//   dotnet run --project Tools/ScenePreview -c Release -- <output folder> [shot names...] [--fast] [--no-hud] [--at=SECONDS] [--hour=H]
//       [--mood=calm|smile|worry]
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
        // The crosshair rests on an interactable: the HUD shows its key and title.
        public bool Aim;
        // Seconds on the boulevard clock from a fresh start: 18–32 s is the pedestrians' green.
        public double Traffic = 1.6;
        // Quotes: a work day with prices on the Exchange board. Crossing: the HUD shows the signal panel.
        public bool Quotes, Crossing;
        // Chase shots: the player drives the test car from (CarX, CarZ), heading CarHeading (radians from +X toward +Z) at
        // CarSpeed m/s, holding the keys for the last DriveSeconds of the boulevard clock; the view is the game's chase
        // camera and the HUD the driving one.
        public bool Drive; public float CarX, CarZ, CarHeading, CarSpeed, Throttle, Steer; public double DriveSeconds;
        // Hour of the city clock (Core/DayCycle); without one the approved bright day, DayCycle.DayHour.
        public float? Hour;
        // Stage 5, staged: these passers-by (walkers first, then the standing figures of their style) stand still in a row,
        // LineupGap apart from left to right and centred on LineupAt, each turned to the camera. The game never lines them
        // up; the row is put back after the shot.
        public WalkerStyle[] Lineup; public Vector3 LineupAt; public float LineupGap = 0.85f;
        // The office staff after the passers-by in the same row, brought out from their places in the office (Orlov's desk
        // is only shown in the employee career: his figure joins the row from it) and put back after the shot.
        public PersonStyle[] Staff;
    }

    public static class Program
    {
        private static readonly Shot[] Shots =
        {
            new Shot { Name = "plaza-north", Local = new Vector3(0, CityBlockPlan.GroundY + 1.7f, 15), Yaw = 0, Pitch = -4, City = true, Target = "Прогулка · лифт ведёт в офис" },
            new Shot { Name = "plaza-office", Local = new Vector3(3.2f, CityBlockPlan.GroundY + 1.7f, 22.6f), Yaw = 192, Pitch = -9, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 14 },
            new Shot { Name = "sunset-west", Local = new Vector3(-12, CityBlockPlan.GroundY + 1.7f, 21), Yaw = -96, Pitch = -2, City = true, Target = "Прогулка · лифт ведёт в офис" },
            new Shot { Name = "square-west", Local = new Vector3(-18.5f, CityBlockPlan.GroundY + 1.7f, 14f), Yaw = -40, Pitch = -6, City = true, Target = "Прогулка · лифт ведёт в офис" },
            new Shot { Name = "east-street", Local = new Vector3(31, CityBlockPlan.GroundY + 1.7f, 13), Yaw = 200, Pitch = 2, City = true, Target = "Прогулка · лифт ведёт в офис" },
            new Shot { Name = "street-works", Local = new Vector3(41.2f, CityBlockPlan.GroundY + 3.6f, -13.9f), Yaw = 3, Pitch = 36, City = true, Target = "Прогулка · лифт ведёт в офис" },
            new Shot { Name = "boulevard", Local = new Vector3(-19, CityBlockPlan.GroundY + 1.7f, 26.6f), Yaw = 66, Pitch = 1, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 1.8 },
            new Shot { Name = "crosswalk", Local = new Vector3(2.2f, CityBlockPlan.GroundY + 1.7f, 24.4f), Yaw = -14, Pitch = -3, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 21, Crossing = true },
            new Shot { Name = "exchange", Local = new Vector3(-4.6f, CityBlockPlan.GroundY + 1.82f, 42.5f), Yaw = 10, Pitch = -8, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 10 },
            // The west side of the Exchange square: the rostral column, the press kiosk and the bull on its cheek.
            new Shot { Name = "exchange-square", Local = new Vector3(-4.6f, CityBlockPlan.GroundY + 1.7f, 42.6f), Yaw = -55, Pitch = -16, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 10 },
            new Shot { Name = "exchange-hall", Local = new Vector3(-1.5f, CityBlockPlan.GroundY + 2.8f, 62.2f), Yaw = 4, Pitch = 3, City = true, Target = "Табло котировок", Quotes = true, Aim = true },
            new Shot { Name = "cafe", Local = new Vector3(22.5f, CityBlockPlan.GroundY + 1.65f, -2.9f), Yaw = 132, Pitch = -9, City = true, Target = "Стойка кофейни «Бык и медведь»", Aim = true },
            // Close-ups of the low-poly cars waiting at the red light (the walkers' green): the eastbound queue from the
            // zebra, then from behind with the taxi that joins it and the brake lamps on.
            new Shot { Name = "cars", Local = new Vector3(0.9f, CityBlockPlan.GroundY + 1.55f, 29.6f), Yaw = -66, Pitch = -8, Fov = 55, City = true, Hud = false, Traffic = 29 },
            new Shot { Name = "cars-rear", Local = new Vector3(-16.0f, CityBlockPlan.GroundY + 1.8f, 28.1f), Yaw = 62, Pitch = -8, Fov = 55, City = true, Hud = false, Traffic = 28 },
            new Shot { Name = "tunnel", Local = new Vector3(36, CityBlockPlan.GroundY + 1.7f, 27.2f), Yaw = 80, Pitch = 1, City = true, Target = "Прогулка · лифт ведёт в офис", Traffic = 6 },
            // The test sedan: aimed at in its lay-by, then behind the wheel on the boulevard, round the ring and back.
            new Shot { Name = "parking", Local = new Vector3(-22.9f, CityBlockPlan.GroundY + 1.7f, 24.9f), Yaw = -38, Pitch = 20, City = true, Target = "Сесть за руль", Aim = true },
            new Shot { Name = "drive-boulevard", City = true, Drive = true, CarX = -44, CarZ = 30.4f, CarHeading = 0, CarSpeed = 11, Throttle = 0.4f, DriveSeconds = 1.5, Traffic = 4 },
            new Shot { Name = "drive-south", City = true, Drive = true, CarX = -34, CarZ = -22, CarHeading = 0, CarSpeed = 13, Throttle = 0.5f, DriveSeconds = 1.6, Traffic = 8 },
            new Shot { Name = "drive-corner", City = true, Drive = true, CarX = 37, CarZ = -22, CarHeading = 0, CarSpeed = 9, Throttle = 0.15f, Steer = -0.7f, DriveSeconds = 1.7, Traffic = 8 },
            new Shot { Name = "drive-parked", City = true, Drive = true, CarX = CityRoads.ParkX, CarZ = CityRoads.ParkZ, CarHeading = CityRoads.ParkHeading, DriveSeconds = 0.5, Traffic = 12 },
            new Shot { Name = "office", Local = new Vector3(-1.2f, 1.7f, -3.6f), Yaw = 8, Pitch = 9, Target = "Телефон — звонки" },
            new Shot { Name = "office-employee", Local = new Vector3(-2.2f, 1.7f, -3.2f), Yaw = 32, Pitch = 10, Employee = true, Target = "Телефон — звонки" },
            new Shot { Name = "aerial", Local = new Vector3(0, 60, -32), Yaw = 0, Pitch = 42, Fov = 60, City = true, Hud = false },
            // Stage 4: the end of the work day on the city clock (Core/DayCycle); every shot above is the approved bright day.
            new Shot { Name = "golden-boulevard", Local = new Vector3(-19, CityBlockPlan.GroundY + 1.7f, 26.6f), Yaw = 66, Pitch = 1, City = true, Hud = false, Traffic = 1.8, Hour = DayCycle.GoldenHour },
            new Shot { Name = "sunset-crosswalk", Local = new Vector3(2.2f, CityBlockPlan.GroundY + 1.7f, 24.4f), Yaw = -14, Pitch = -3, City = true, Hud = false, Traffic = 21, Crossing = true, Hour = DayCycle.SunsetHour },
            new Shot { Name = "dusk-crosswalk", Local = new Vector3(2.2f, CityBlockPlan.GroundY + 1.7f, 24.4f), Yaw = -14, Pitch = -3, City = true, Hud = false, Traffic = 21, Crossing = true, Hour = DayCycle.DuskHour },
            new Shot { Name = "night-plaza", Local = new Vector3(3.2f, CityBlockPlan.GroundY + 1.7f, 22.6f), Yaw = 192, Pitch = -9, City = true, Hud = false, Traffic = 14, Hour = 20.5f },
            new Shot { Name = "night-boulevard", Local = new Vector3(-19, CityBlockPlan.GroundY + 1.7f, 26.6f), Yaw = 66, Pitch = 1, City = true, Hud = false, Traffic = 1.8, Hour = 20.5f },
            new Shot { Name = "night-office", Local = new Vector3(-1.2f, 1.7f, -3.6f), Yaw = 8, Pitch = 9, Hud = false, Hour = 20.5f },
            new Shot { Name = "night-aerial", Local = new Vector3(0, 60, -32), Yaw = 0, Pitch = 42, Fov = 60, City = true, Hud = false, Hour = DayCycle.EndHour },
            // Stage 5: the people. Two staged rows of all twelve kinds of passers-by on the office plaza, three faces close up,
            // then the staff at their desks and the café at their places; no HUD.
            new Shot { Name = "people-lineup", Local = new Vector3(0, CityBlockPlan.GroundY + 1.5f, 13.7f), Yaw = 0, Pitch = 7, Fov = 40, City = true, Hud = false,
                Lineup = new[] { WalkerStyle.Clerk, WalkerStyle.Banker, WalkerStyle.Casual, WalkerStyle.Coat, WalkerStyle.Courier, WalkerStyle.Student }, LineupAt = new Vector3(0, 0, 18.6f) },
            new Shot { Name = "people-lineup-2", Local = new Vector3(0, CityBlockPlan.GroundY + 1.5f, 13.7f), Yaw = 0, Pitch = 7, Fov = 40, City = true, Hud = false,
                Lineup = new[] { WalkerStyle.Elder, WalkerStyle.Tourist, WalkerStyle.Analyst, WalkerStyle.Barista, WalkerStyle.Guard, WalkerStyle.Trader }, LineupAt = new Vector3(0, 0, 18.6f) },
            new Shot { Name = "people-faces", Local = new Vector3(0, CityBlockPlan.GroundY + 1.62f, 16.4f), Yaw = 0, Pitch = 2, Fov = 30, City = true, Hud = false,
                Lineup = new[] { WalkerStyle.Banker, WalkerStyle.Casual, WalkerStyle.Elder }, LineupAt = new Vector3(0, 0, 18.6f), LineupGap = 0.62f },
            new Shot { Name = "people-office", Local = new Vector3(-2.35f, 1.62f, 0.3f), Yaw = -28, Pitch = 1, Fov = 46, Hud = false },
            new Shot { Name = "people-cafe", Local = new Vector3(24.35f, CityBlockPlan.GroundY + 1.55f, -2.65f), Yaw = 153, Pitch = 9, Fov = 58, City = true, Hud = false },
            new Shot { Name = "people-supervisor", Local = new Vector3(-2.65f, 1.62f, -1.75f), Yaw = -158, Pitch = 4, Fov = 30, Employee = true, Hud = false },
            // The three the player talks to — Lena, Max and Orlov — side by side on the plaza, then their faces close up.
            new Shot { Name = "people-staff", Local = new Vector3(0, CityBlockPlan.GroundY + 1.15f, 15.3f), Yaw = 0, Pitch = 2, Fov = 40, City = true, Hud = false, Traffic = 14,
                Lineup = new WalkerStyle[0], Staff = new[] { PersonStyle.Lena, PersonStyle.Max, PersonStyle.Orlov }, LineupAt = new Vector3(0, 0, 18.6f) },
            new Shot { Name = "people-staff-faces", Local = new Vector3(0, CityBlockPlan.GroundY + 1.64f, 16.4f), Yaw = 0, Pitch = 1, Fov = 28, City = true, Hud = false, Traffic = 14,
                Lineup = new WalkerStyle[0], Staff = new[] { PersonStyle.Lena, PersonStyle.Max, PersonStyle.Orlov }, LineupAt = new Vector3(0, 0, 18.6f), LineupGap = 0.52f },
            // Stage 6: the office and the café close up — the director's desk from his chair, the room towards the door from
            // the windows, the door with the display stand and the street elevator, the coffee corner, the café's counter and
            // shelf.
            new Shot { Name = "office-desk", Local = new Vector3(1.0f, 1.62f, 0.45f), Yaw = 180, Pitch = 30, Fov = 56, Hud = false },
            new Shot { Name = "office-entrance", Local = new Vector3(1.5f, 2.3f, 4.1f), Yaw = 200, Pitch = 14, Fov = 72, Hud = false },
            new Shot { Name = "office-door", Local = new Vector3(-1.6f, 1.75f, 0.4f), Yaw = 128, Pitch = 10, Fov = 66, Hud = false },
            new Shot { Name = "office-coffee", Local = new Vector3(-3.75f, 1.75f, -2.85f), Yaw = -42, Pitch = 22, Fov = 50, Hud = false },
            new Shot { Name = "cafe-counter", Local = new Vector3(25.6f, CityBlockPlan.GroundY + 1.6f, -4.4f), Yaw = 192, Pitch = 8, Fov = 64, City = true, Hud = false },
            // After the author's answer: the executive wing west of the office — the meeting room from its doorway, the lounge
            // with the chesterfield under the yacht, the aquarium from the sofa's corner — and the turned workstations.
            new Shot { Name = "office-meeting", Local = new Vector3(-6.75f, 1.75f, 0.75f), Yaw = -62, Pitch = 10, Fov = 72, Hud = false },
            new Shot { Name = "office-lounge", Local = new Vector3(-6.75f, 1.7f, -0.6f), Yaw = -118, Pitch = 12, Fov = 72, Hud = false },
            new Shot { Name = "office-aquarium", Local = new Vector3(-11.3f, 1.65f, -3.7f), Yaw = 55, Pitch = 8, Fov = 66, Hud = false },
            new Shot { Name = "office-staff", Local = new Vector3(5.3f, 1.8f, 0.2f), Yaw = -50, Pitch = 8, Fov = 70, Hud = false },
        };

        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture; CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            // Same colour space as the Unity project (Player settings), so lighting is tuned for what the editor shows.
            ColorPipe.Linear = ProjectIsLinear();
            QualitySettings.activeColorSpace = ColorPipe.Linear ? ColorSpace.Linear : ColorSpace.Gamma;
            bool check = args.Contains("--check"), noHud = args.Contains("--no-hud"), fast = args.Contains("--fast");
            // --at=SECONDS re-times the selected shots on the boulevard clock (to pick a moment for a new shot).
            var at = args.FirstOrDefault(a => a.StartsWith("--at="));
            double? retime = at == null ? null : double.Parse(at.Substring(5), CultureInfo.InvariantCulture);
            // --hour=H shows the selected shots at that hour of the city clock (18.5 = 18:30) instead of their own.
            var hourArg = args.FirstOrDefault(a => a.StartsWith("--hour="));
            hourOverride = hourArg == null ? null : float.Parse(hourArg.Substring(7), CultureInfo.InvariantCulture);
            var positional = args.Where(a => !a.StartsWith("--")).ToList();
            string output = check ? null : positional.Count > 0 ? positional[0] : "preview";
            var wanted = new HashSet<string>(positional.Skip(check ? 0 : 1));
            if (output != null) Directory.CreateDirectory(output);
            var clock = Stopwatch.StartNew();
            var root = new GameObject("Office scene");
            var scene = root.AddComponent<OfficeScene>();
            // --mood=MOOD: everybody's expression in the selected shots, as the office scene object's Mood in the Inspector
            // (calm by default).
            var moodArg = args.FirstOrDefault(a => a.StartsWith("--mood="));
            if (moodArg != null) scene.Mood = (FaceMood)Enum.Parse(typeof(FaceMood), moodArg.Substring(7), true);
            var day = new BrokerDay(); var quotesDay = new BrokerDay(6, mode: CareerMode.EmployeeWork, clientIds: ClientCatalog.Appointments(6));
            Console.WriteLine($"scene built in {clock.ElapsedMilliseconds} ms: {GameObject.All.Count} objects, warnings {UnityEngine.Debug.Warnings.Count}");
            foreach (var warning in UnityEngine.Debug.Warnings.Distinct().Take(8)) Console.WriteLine("  warning: " + warning);
            int failures = UnityEngine.Debug.Warnings.Count;
            if (check)
            {
                // Faces (stage 5): every expression of the Inspector reaches every person, also one chosen after the people
                // were built and before the first Refresh.
                var faces = GameObject.All.Where(o => o.name == "Person / face").ToList(); var chosen = scene.Mood;
                var before = faces.Select(o => o.GetComponent<MeshFilter>().sharedMesh).ToList();
                foreach (var mood in new[] { FaceMood.Smile, FaceMood.Worry, FaceMood.Calm })
                {
                    scene.Mood = mood; scene.Refresh(day);
                    var meshes = faces.Select(o => o.GetComponent<MeshFilter>().sharedMesh).ToList();
                    bool ok = faces.Count > 0 && faces.All(o => o.GetComponent<MeshRenderer>().enabled && o.GetComponent<MeshFilter>().sharedMesh != null)
                        && meshes.Zip(before, (now, was) => now != was).All(changed => changed);
                    if (!ok) failures++;
                    Console.WriteLine($"{(ok ? "faces" : "FAIL faces")} {mood}: {faces.Count} people");
                    before = meshes;
                }
                scene.Mood = chosen; scene.Refresh(day);
            }
            bool employee = false;
            foreach (var shot in Shots.OrderBy(s => s.Employee))
            {
                if (wanted.Count > 0 && !wanted.Contains(shot.Name)) continue;
                if (shot.Employee && !employee) { employee = true; scene.ConfigureCareerMode(CareerMode.Employee); day = new BrokerDay(mode: CareerMode.Employee); }
                var renderer = check ? new Renderer { Width = 240, Height = 160, Supersample = 1 } : new Renderer { Supersample = fast ? 1 : 2 };
                var camera = Prepare(scene, root, shot, shot.Quotes ? quotesDay : day, retime ?? shot.Traffic);
                var putBack = shot.Lineup != null ? LineUp(scene, shot) : null;
                renderer.Render(SceneSoup.Collect(), camera);
                putBack?.Invoke();
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

        private static float? hourOverride;

        private static ViewCamera Prepare(OfficeScene scene, GameObject root, Shot shot, BrokerDay day, double traffic)
        {
            scene.Present(false, 1, Vector2.zero);
            // Street shots stand the player where the camera is, so the passers-by stop for it as they do in the game.
            if (shot.Drive) scene.VisitCity(true);
            else if (shot.City && shot.Hud)
            {
                scene.VisitCity(true);
                scene.Player.ResetAt(root.transform.TransformPoint(new Vector3(shot.Local.x, shot.Local.y - 1.65f, shot.Local.z)));
            }
            else scene.ResetPlayer();
            if (shot.Drive)
            {
                // Into the car, onto its start, then frame by frame with the keys held, as the game advances the city.
                SetTraffic(scene, Math.Max(0, traffic - shot.DriveSeconds));
                scene.EnterCar();
                scene.Life.Car.Place(shot.CarX, shot.CarZ, shot.CarHeading, shot.CarSpeed);
                scene.Player.Drive = new DriveInput(shot.Throttle, shot.Steer, false);
                const double frame = 1 / 30.0;
                for (double t = 0; t < shot.DriveSeconds - 1e-9; t += frame) scene.AdvanceCity(Math.Min(frame, shot.DriveSeconds - t));
            }
            else SetTraffic(scene, traffic);
            scene.Refresh(day);
            // Refresh follows the work day (9:00 on a fresh one); the shot shows its own hour.
            scene.SetHour(hourOverride ?? shot.Hour ?? DayCycle.DayHour);
            var camera = ViewCamera.From(scene.Player.Eye);
            var rotation = shot.Drive ? scene.Player.Eye.transform.rotation : Quaternion.Euler(shot.Pitch, shot.Yaw, 0);
            if (!shot.Drive)
            {
                camera.Position = root.transform.TransformPoint(shot.Local);
                camera.Right = rotation * Vector3.right; camera.Up = rotation * Vector3.up; camera.Forward = rotation * Vector3.forward; camera.Fov = shot.Fov;
            }
            // Badges turn toward the active camera in Refresh; aim them at the preview camera instead.
            foreach (var badge in GameObject.All.Where(g => g.name == "Office / badge"))
                badge.transform.rotation = rotation;
            return camera;
        }

        // The staged row of a Lineup shot: walkers of each style first, then a standing figure of that style, moved into the
        // row and turned to the camera, limbs and head at rest; the staff of the shot after them. Returns how to put them
        // back (the figures and the staff stay where the scene built them; the walkers are placed again by the next shot's
        // clock anyway).
        private static Action LineUp(OfficeScene scene, Shot shot)
        {
            var used = new List<(Transform person, Transform parent, Vector3 position, Quaternion rotation, List<(Transform part, Quaternion rotation)> parts)>();
            var walkers = scene.Life.Crowd.Walkers;
            GameObject Find(string name) => GameObject.All.FirstOrDefault(g => g.name == name && g.activeInHierarchy && !used.Any(u => u.person == g.transform));
            var row = new List<GameObject>();
            foreach (var style in shot.Lineup)
            {
                GameObject person = null;
                for (int i = 0; i < walkers.Count && person == null; i++) if (walkers[i].Style == style) person = Find("City / passer-by " + i);
                person ??= CityCrowd.Figures.Any(f => f.Style == style && !f.Seated) ? Find("City / " + style) : null;
                row.Add(person ?? throw new InvalidOperationException("nobody of style " + style + " to line up"));
            }
            foreach (var style in shot.Staff ?? new PersonStyle[0])
            {
                string name = style == PersonStyle.Lena ? "Lena - person" : style == PersonStyle.Max ? "Max - person" : "Orlov / figure";
                row.Add(GameObject.All.FirstOrDefault(g => g.name == name && g.activeSelf) ?? throw new InvalidOperationException("no " + name + " to line up"));
            }
            for (int k = 0; k < row.Count; k++)
            {
                var t = row[k].transform; var parts = new List<(Transform, Quaternion)>();
                foreach (Transform part in t) parts.Add((part, part.localRotation));
                used.Add((t, t.parent, t.localPosition, t.localRotation, parts));
                // Out of a hidden place (Orlov's desk in the owner's office) onto the scene itself.
                if (!row[k].activeInHierarchy) t.SetParent(scene.transform, false);
                float x = shot.LineupAt.x + (k - (row.Count - 1) / 2f) * shot.LineupGap, z = shot.LineupAt.z;
                var at = new Vector3(x, CityBlockPlan.GroundY + CityDistrictPlan.FloorY(x, z), z);
                // Models face −Z: the root turns so that −Z points at the camera.
                var to = shot.Local - at;
                t.localPosition = at; t.localRotation = Quaternion.Euler(0, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + 180, 0);
                foreach (Transform part in t) part.localRotation = Quaternion.identity;
            }
            return () =>
            {
                foreach (var u in used)
                {
                    if (u.person.parent != u.parent) u.person.SetParent(u.parent, false);
                    u.person.localPosition = u.position; u.person.localRotation = u.rotation;
                    foreach (var (part, rotation) in u.parts) part.localRotation = rotation;
                }
            };
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

        // Mirrors PrototypeBootstrap.DrawWorldHUD on its 1440 × 960 canvas: plain text with a drop shadow straight over the
        // world (no cards), the gold meter, the key of a targeted interactable, the crosshair and the start-of-day hint.
        private static void AddHud(List<string> args, Shot shot, BrokerDay day, OfficeScene scene)
        {
            var ink = Hex("#F5F0E6"); var muted = Hex("#E4DACD"); var gold = Hex("#E9B56D");
            const string shadow = "rgba(10,6,5,0.8)", track = "rgba(20,13,10,0.45)";
            void Rect(float x, float y, float w, float h, string fill) =>
                args.AddRange(new[] { "-gravity", "NorthWest", "-stroke", "none", "-fill", fill, "-draw", string.Format(CultureInfo.InvariantCulture, "rectangle {0},{1} {2},{3}", x, y, x + w - 1, y + h - 1) });
            // Text in the box from x to x + w, aligned like its GUIStyle (left, centre or right): shadow pass, then the text.
            void Text(float x, float y, float w, string value, int size, Color color, bool bold, TextAnchor align = TextAnchor.UpperLeft)
            {
                string gravity = align == TextAnchor.UpperCenter ? "North" : align == TextAnchor.UpperRight ? "NorthEast" : "NorthWest";
                float anchor = align == TextAnchor.UpperCenter ? x + w / 2 - 720 : align == TextAnchor.UpperRight ? 1440 - (x + w) : x;
                foreach (var (shift, fill) in new[] { (1f, shadow), (2f, shadow), (0f, Rgba(color, 1)) })
                    args.AddRange(new[] { "-gravity", gravity, "-font", bold ? "Liberation-Sans-Bold" : "Liberation-Sans", "-pointsize", size.ToString(CultureInfo.InvariantCulture),
                        "-fill", fill, "-stroke", "none", "-annotate", Offset(anchor + (align == TextAnchor.UpperRight ? -shift : shift), y + shift - size * 0.12f), value });
            }
            void Outline(float x, float y, float size, string fill)
            {
                const float t = 2;
                Rect(x, y, size, t, fill); Rect(x, y + size - t, size, t, fill); Rect(x, y + t, t, size - 2 * t, fill); Rect(x + size - t, y + t, t, size - 2 * t, fill);
            }
            bool employee = shot.Employee;
            Text(40, 30, 360, "МОЙКА КАПИТАЛ", 22, ink, true);
            Text(40, 60, 360, shot.City ? "Деловой квартал" : (employee ? "Наёмный брокер · день " : "Офис · день ") + day.DayNumber, 16, muted, false);
            // The city clock, as in the game: the hour of the shot's light.
            Text(620, 26, 200, DayCycle.Clock(scene.Hour), 32, ink, true, TextAnchor.UpperCenter);
            Text(1040, 30, 360, employee ? "$" + day.Cash + " · личные" : "$" + day.Cash + " / " + day.TotalExpenses, 22, ink, true, TextAnchor.UpperRight);
            float meter = employee ? day.DealsCount / (float)Math.Max(1, day.SalesTarget) : day.Cash / (float)Math.Max(1, day.TotalExpenses);
            Rect(1146, 70, 256, 4, shadow); Rect(1144, 68, 256, 4, track); if (meter > 0) Rect(1144, 68, 256 * Math.Clamp(meter, 0, 1), 4, Rgba(gold, 1));
            // As in the game: the E key and a title as one centred line.
            void KeyLine(string title)
            {
                float width = Math.Min(560, TextWidth(title, 22, true)), left = 720 - (40 + 12 + width) / 2;
                Outline(left + 2, 810, 40, shadow); Outline(left, 808, 40, Rgba(ink, 1));
                Text(left, 816, 40, "E", 20, ink, true, TextAnchor.UpperCenter); Text(left + 52, 812, width + 8, title, 22, ink, true);
            }
            if (shot.Drive)
            {
                // Behind the wheel: no crosshair; where to get out, the speed and gear bottom right, the keys of the first seconds.
                var car = scene.Life.Car;
                if (scene.CanLeaveCar) KeyLine("Выйти из машины");
                else Text(420, 812, 600, "Выйти можно только на парковке", 22, ink, true, TextAnchor.UpperCenter);
                Text(1160, 764, 240, ((int)Math.Round(car.Kmh)).ToString(), 44, ink, true, TextAnchor.UpperRight);
                Text(1040, 818, 360, car.Gear < 0 ? "км/ч · задний ход" : "км/ч · передача " + car.GearLabel, 22, ink, true, TextAnchor.UpperRight);
                Text(220, 900, 1000, "W/S · газ и тормоз     A/D · руль     Пробел · ручник     Мышь · обзор     Esc · пауза", 16, muted, false, TextAnchor.UpperCenter);
            }
            else
            {
                float arm = shot.Aim ? 10 : 7; string cross = shot.Aim ? Rgba(gold, 1) : "rgba(255,255,255,0.9)", crossShadow = "rgba(0,0,0,0.35)";
                Rect(720 - arm + 1, 480, arm * 2, 2, crossShadow); Rect(720, 480 - arm + 1, 2, arm * 2, crossShadow);
                Rect(720 - arm, 479, arm * 2, 2, cross); Rect(719, 480 - arm, 2, arm * 2, cross);
                if (shot.Aim) KeyLine(shot.Target);
                else Text(420, 812, 600, shot.Target ?? "", 22, ink, true, TextAnchor.UpperCenter);
            }
            if (employee)
            {
                Text(40, 104, 560, "ДАЛЬШЕ / ОРИЕНТИР " + day.DealsCount + "/" + day.SalesTarget, 16, gold, true);
                foreach (var (shift, fill) in new[] { (1, shadow), (2, shadow), (0, Rgba(muted, 1)) })
                    args.AddRange(new[] { "(", "-size", "560x", "-background", "none", "-font", "Liberation-Sans", "-pointsize", "16", "-fill", fill,
                        "caption:" + day.EmploymentNextAction, ")", "-gravity", "NorthWest", "-geometry", Offset(40 + shift, 128 + shift), "-composite" });
            }
            // A fresh day: the controls hint of the first 15 seconds.
            if (!shot.Drive) Text(320, 900, 800, "WASD · ходить     Мышь · обзор     E · действие     Esc · пауза", 16, muted, false, TextAnchor.UpperCenter);
            if (shot.City)
            {
                // As in the game: the chosen landmark (the office lift by default), its distance and direction from the
                // player or the car.
                var destination = CityDistrictPlan.Find("office");
                float x = shot.Drive ? scene.MapX : shot.Local.x, z = shot.Drive ? scene.MapZ : shot.Local.z;
                Text(40, 288, 360, destination.Name, 22, ink, true);
                Text(40, 318, 360, Math.Ceiling(CityDistrictPlan.Distance(x, z, destination)) + " м · "
                    + CityDistrictPlan.Direction(x, z, destination) + "\nКарта — у стенда / в паузе", 16, muted, false);
            }
            if (shot.Crossing)
            {
                Text(40, 384, 360, "ПЕРЕХОД / СВЕТОФОР", 16, gold, true);
                Text(40, 410, 360, scene.Life.Hint, 16, muted, false);
            }
        }
        // Width in pixels of one line of HUD text, measured by ImageMagick with the font the overlay uses.
        private static float TextWidth(string text, int size, bool bold)
        {
            var info = new ProcessStartInfo("magick") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in new[] { "-font", bold ? "Liberation-Sans-Bold" : "Liberation-Sans", "-pointsize", size.ToString(CultureInfo.InvariantCulture), "label:" + text, "-format", "%w", "info:" })
                info.ArgumentList.Add(a);
            using var process = Process.Start(info); string output = process.StandardOutput.ReadToEnd(); process.WaitForExit();
            return float.TryParse(output.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float width) ? width : text.Length * size * 0.55f;
        }
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var c); return c; }
        private static bool ProjectIsLinear()
        {
            for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            {
                string path = Path.Combine(dir.FullName, "ProjectSettings", "ProjectSettings.asset");
                if (File.Exists(path)) return File.ReadAllText(path).Contains("m_ActiveColorSpace: 1");
            }
            Console.WriteLine("warning: ProjectSettings not found, assuming linear colour space");
            return true;
        }

        private static void Run(string file, List<string> args)
        {
            var info = new ProcessStartInfo(file) { RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in args) info.ArgumentList.Add(a);
            using var process = Process.Start(info); string error = process.StandardError.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(file + " failed: " + error);
        }
    }
}
