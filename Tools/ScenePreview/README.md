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
| `crosswalk` | the zebra on the pedestrians' green, cars at the stop lines, walkers crossing; crossing HUD panel |
| `exchange`, `exchange-hall` | the Exchange square and steps; the hall with the quote board of a work day |
| `cafe` | the café «Бык и медведь»: counter, barista, tables |
| `tunnel` | the eastern tunnel portal at the end of the boulevard |
| `office`, `office-employee` | the office as the owner and as the hired broker |
| `aerial` | both quarters from above, no HUD |

Street shots with a HUD enter the city like the elevator does (`VisitCity`) and put the player at the
camera, so walkers stop for it and the signal counts it on the zebra. Every shot starts the boulevard clock
afresh and advances signal, cars and walkers to its time, so a shot always shows the same moment. The
street HUD has the landmark panel (name, straight-line distance, direction) and, near the crossing, the
signal panel.

The project compiles the **actual Core, OfficeScene partials, SurfaceLibrary and
OfficeInteractable** at C# 9 (the Unity 6 language level) against narrow engine stand-ins in
`UnityStubs.cs` and `PreviewFakes.cs`. The builders run as in Play: same voxel meshes,
pixel textures, lights, sky and fog values, same traffic positions for a given time.
`Renderer.cs` is a small CPU rasteriser that approximates the built-in forward path:
Standard BRDF in gamma space, trilight ambient, Unity's procedural skybox with the
high-quality sun, sky reflections, four soft shadow cascades, point lights, linear fog and
2×2 SSAA. World TextMesh labels and the HUD are drawn by ImageMagick, NOT by Unity fonts/IMGUI.

`--check` renders each shot at 240×160 and exits with 1 on builder warnings, an empty view
(less than 25% geometry) or a flat image (luminance spread below 0.08). It writes no files and
does not need ImageMagick; CI runs it as the `scene-preview` job.

This is NOT a Unity render or build. Shader variants, the real Quality-level shadow settings,
GPU filtering/mipmaps, colour management, IMGUI layout, font metrics, colliders, physics and
frame time still require Play Mode in the editor (checklists in `Docs/3D_SETUP.md` and `Docs/CITY_DISTRICT.md`). Use the
stills to compare composition, palette and lighting between commits; a passing check does not
prove that the scene looks identical in Unity. Do not copy the stand-ins into Assets.
