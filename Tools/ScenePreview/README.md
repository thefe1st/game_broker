# Scene preview — offline stills of the procedural office and city

Run from repository root (.NET 8; PNG output and text overlays need ImageMagick `magick`
with Liberation Sans):

```bash
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview sunset-west office --fast
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview crosswalk --at=24
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- --check
```

The first form writes every shot to `preview/*.png`; the second renders only the named shots.
`--fast` turns off 2×2 supersampling, `--no-hud` skips the HUD, `--at=SECONDS` re-times the selected
shots on the boulevard clock (from a fresh start; 18–32 s is the pedestrians' green).

| Shot | What it shows |
|---|---|
| `plaza-north`, `plaza-office` | the plaza with the stands, looking north to the boulevard and south to the office |
| `sunset-west`, `east-street` | the western square against the sun, the business street |
| `boulevard` | traffic along the railing, the Exchange quarter beyond |
| `crosswalk` | the zebra on the pedestrians' green, cars at the stop lines, walkers crossing; the crossing HUD lines |
| `cars`, `cars-rear` | close-ups of the low-poly cars waiting at the red light: from the front off the zebra, from behind with the brake lamps on; no HUD |
| `exchange`, `exchange-hall` | the Exchange square and steps; the hall with the quote board of a work day, aimed at it (E and its title) |
| `cafe` | the café «Бык и медведь»: counter, barista, tables, aimed at the counter |
| `tunnel` | the eastern tunnel portal at the end of the boulevard |
| `office`, `office-employee` | the office as the owner and as the hired broker |
| `aerial` | both quarters from above, no HUD |

Street shots with a HUD enter the city like the elevator does (`VisitCity`) and put the player at the
camera, so walkers stop for it and the signal counts it on the zebra. Every shot starts the boulevard clock
afresh and advances signal, cars and walkers to its time, so a shot always shows the same moment. The
HUD repeats `DrawWorldHUD`: warm white text with a 2 px drop shadow and no panels — name and place top
left, the clock in the middle, money and the gold meter top right, the controls hint of a fresh day at
the bottom; on the street also the landmark (name, straight-line distance, direction) and, near the
crossing, «ПЕРЕХОД / СВЕТОФОР» with the signal state. Shots marked as aimed draw the gold crosshair and the
E key with the target's title as one centred line; the others print the shot's target line.

The project compiles the **actual Core, OfficeScene partials, SurfaceLibrary and
OfficeInteractable** at C# 9 (the Unity 6 language level) against narrow engine stand-ins in
`UnityStubs.cs` and `PreviewFakes.cs`. The builders run as in Play: same voxel meshes,
pixel textures, low-poly car meshes, lights, sky and fog values, same traffic positions for a given time.
`Renderer.cs` is a small CPU rasteriser that approximates the built-in forward path of the
project's **linear colour space with HDR cameras**: sRGB colours and textures are decoded before
lighting, light colour × intensity goes through Unity's gamma-to-linear, Standard BRDF, trilight
ambient, the `Broker/Sky` dome (`Core/SkyLook`, clouds at time 0; Unity's procedural skybox when
the material is not `Broker/Sky`), sky reflections, four soft shadow cascades, point lights, linear
fog, the corner shade of `Broker/Voxel` and 2×2 SSAA. Cameras with `CameraPost` get the screen pass
of `BrokerPost.shader` with the values of `Core/ScreenLook`: half-resolution SSAO, the sun's glow in
the haze (`ScreenLook.HazeGlowAt`), the bloom pyramid, exposure, warm balance and ACES, saturation,
contrast and vignette, then sRGB encoding. World TextMesh labels and the HUD are drawn by
ImageMagick, NOT by Unity fonts/IMGUI.

`--check` renders each shot at 240×160 and exits with 1 on builder warnings, an empty view
(less than 25% geometry) or a flat image (luminance spread below 0.08). It writes no files and
does not need ImageMagick; CI runs it as the `scene-preview` job.

This is NOT a Unity render or build. Shader variants, the real Quality-level shadow settings,
GPU filtering/mipmaps, colour management, IMGUI layout, font metrics, colliders, physics and
frame time still require Play Mode in the editor (checklists in `Docs/3D_SETUP.md` and `Docs/CITY_DISTRICT.md`). Use the
stills to compare composition, palette and lighting between commits; a passing check does not
prove that the scene looks identical in Unity. Do not copy the stand-ins into Assets.
