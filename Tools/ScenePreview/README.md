# Scene preview — offline stills of the procedural office and city

Run from repository root (.NET 8; PNG output and text overlays need ImageMagick `magick`
with Liberation Sans):

```bash
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview sunset-west office --fast
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview crosswalk --at=24
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview crosswalk plaza-office --hour=19.75
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- --check
```

The first form writes every shot to `preview/*.png`; the second renders only the named shots.
`--fast` turns off 2×2 supersampling, `--no-hud` skips the HUD, `--at=SECONDS` re-times the selected
shots on the boulevard clock (from a fresh start; 18–32 s is the pedestrians' green). `--hour=H` shows
the selected shots at that hour of the city clock (`Core/DayCycle`, 18.5 = 18:30); without it every shot
has its own hour, and all but the evening ones below are the approved bright day, 15:00.

| Shot | What it shows |
|---|---|
| `plaza-north`, `plaza-office` | the plaza with the stands, looking north to the boulevard and south to the office |
| `sunset-west`, `east-street` | the western square against the sun, the business street |
| `square-west` | the western square close up: a raised bed with its tree and shrubs, a street lamp, conifers and benches |
| `street-works` | the service works in the eastern side street from above: the striped barrier with its lamp, the cones and a refuse container |
| `boulevard` | traffic along the railing, the Exchange quarter beyond |
| `crosswalk` | the zebra on the pedestrians' green, cars at the stop lines, walkers crossing; the crossing HUD lines |
| `cars`, `cars-rear` | close-ups of the low-poly cars waiting at the red light: from the front off the zebra, from behind with the brake lamps on; no HUD |
| `exchange`, `exchange-hall` | the Exchange square and steps; the hall with the quote board of a work day, aimed at it (E and its title) |
| `exchange-square` | the west side of the Exchange square: the rostral column, the press kiosk and the bull on its cheek |
| `cafe` | the café «Бык и медведь»: counter, barista, tables, aimed at the counter |
| `tunnel` | the eastern tunnel portal at the end of the boulevard |
| `parking` | the red test sedan in its lay-by by the square, aimed at (E «Сесть за руль») |
| `drive-boulevard`, `drive-south`, `drive-corner` | behind the wheel with the chase camera: among the traffic on the boulevard, on the southern street of the ring past the office's windowed back, turning into the eastern street; speedometer, gear and the driving keys |
| `drive-parked` | the sedan back in the lay-by: E «Выйти из машины» |
| `office`, `office-employee` | the office as the owner and as the hired broker |
| `aerial` | both quarters from above, no HUD |
| `golden-boulevard`, `sunset-crosswalk`, `dusk-crosswalk` | the end of the work day (stage 4 prototype): the boulevard at the golden hour, 18:00; the crosswalk at sunset, 19:00, with the street lamps coming on, and in the blue hour, 19:45, with every light on; no HUD |
| `night-plaza`, `night-boulevard`, `night-office`, `night-aerial` | night, 20:30 (the aerial at 21:00, the end of the day): lit windows and shop fronts, street lamps and their pools of light, the light over the office door, headlights; the office with its lamps and the night city in the windows; no HUD |

Street shots with a HUD enter the city like the elevator does (`VisitCity`) and put the player at the
camera, so walkers stop for it and the signal counts it on the zebra. Driving shots seat the player in
the test sedan (`EnterCar`), put it at the shot's position, heading and speed, hold the shot's throttle
and steering for its last `DriveSeconds` in 1/30 s frames through the real `PlayerCar`, kerbs and chase
camera, and render from that camera. Every shot starts the boulevard clock
afresh and advances signal, cars and walkers to its time, so a shot always shows the same moment. The
HUD repeats `DrawWorldHUD`: warm white text with a 2 px drop shadow and no panels — name and place top
left, the clock in the middle, money and the gold meter top right, the controls hint of a fresh day at
the bottom; on the street also the landmark (name, straight-line distance, direction) and, near the
crossing, «ПЕРЕХОД / СВЕТОФОР» with the signal state; behind the wheel the speedometer with the gear
bottom right, the exit line (E «Выйти из машины» in the lay-by, «Выйти можно только на парковке»
elsewhere) and the driving keys instead of the walking ones. Shots marked as aimed draw the gold crosshair and the
E key with the target's title as one centred line; the others print the shot's target line.

The project compiles the **actual Core, OfficeScene partials, SurfaceLibrary and
OfficeInteractable** at C# 9 (the Unity 6 language level) against narrow engine stand-ins in
`UnityStubs.cs` and `PreviewFakes.cs`. The builders run as in Play: same voxel meshes,
pixel textures, low-poly car meshes, lights, sky and fog values, same traffic positions for a given time.
`Renderer.cs` is a small CPU rasteriser that approximates the built-in forward path of the
project's **linear colour space with HDR cameras**: sRGB colours and textures are decoded before
lighting, light colour × intensity goes through Unity's gamma-to-linear, Standard BRDF, trilight
ambient, the `Broker/Sky` dome (`Core/SkyLook`, clouds at time 0, the hour's palette and sun or moon;
Unity's procedural skybox when the material is not `Broker/Sky`), sky reflections, four soft shadow
cascades, point lights (the street lamps after dusk), emission (lit windows, lamp glass), linear
fog, the corner shade of `Broker/Voxel` and 2×2 SSAA. Cameras with `CameraPost` get the screen pass
of `BrokerPost.shader` with the values of `Core/ScreenLook`: half-resolution SSAO, the sun's (or the
moon's) glow in the haze (`ScreenLook.HazeGlowAt`, in the sky's glow colour of the hour), the bloom
pyramid, exposure, warm balance and ACES, saturation, contrast and vignette, then sRGB encoding. World TextMesh labels and the HUD are drawn by
ImageMagick, NOT by Unity fonts/IMGUI.

`--check` renders each shot at 240×160 and exits with 1 on builder warnings, an empty view
(less than 25% geometry) or a flat image (luminance spread below 0.08). It writes no files and
does not need ImageMagick; CI runs it as the `scene-preview` job.

This is NOT a Unity render or build. Shader variants, the real Quality-level shadow settings,
GPU filtering/mipmaps, colour management, IMGUI layout, font metrics, colliders, physics and
frame time still require Play Mode in the editor (checklists in `Docs/3D_SETUP.md` and `Docs/CITY_DISTRICT.md`). Use the
stills to compare composition, palette and lighting between commits; a passing check does not
prove that the scene looks identical in Unity. Do not copy the stand-ins into Assets.
