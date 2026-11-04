# Передача работы: PR #8 «Графика» (в работе)

Ветка `wip/pr8-graphics` от `main` 13c4a47. Не слита: полный прогон тестов и CI ещё не запускались. Перед слиянием удалить этот файл.

## Сделано
- Linear + HDR: `ProjectSettings` `m_ActiveColorSpace: 1`.
- Пост без пакетов: `Shaders/BrokerPost.shader` + `Runtime/Office/CameraPost.cs` — SSAO (половина разрешения, билатеральное размытие), bloom (пирамида 6 уровней), ACES, тёплый баланс, насыщенность, контраст, виньетка, дизеринг. Параметры — `Core/ScreenLook.cs`.
- Воксельный AO по углам: `Core/VoxelModel.cs` (`VoxelMesh.Shades`; грани сливаются только при равных уровнях; диагональ переворачивается) + шейдер `Broker/Voxel` (Standard surface, fallback Diffuse).
- Небо `Broker/Sky`: `Core/SkyLook.cs` + `Shaders/BrokerSky.shader` — тёплый горизонт = дымка, зенит, ореол солнца, облака. Сцена пишет все значения SkyLook в материал.
- Атмосфера: солнце ≈17°, теплее; ambient в тени холоднее; `Haze = SkyLook.Horizon`.
- Три шейдера в `GraphicsSettings` → `m_AlwaysIncludedShaders`.
- Превью (`Tools/ScenePreview`) повторяет путь Unity: linear, BRDF, вершинный AO, пост, небо.
- Журнал: `Docs/DEVELOPMENT_PLAN.md`, запись «3 ноября 2026 — графика уровня нового скриншота автора».
- Сборка превью — 0 ошибок; пробный кадр `crosswalk` рендерится.

## Осталось по PR #8
1. Настройка: дымка и туман (FogStart/FogEnd, силуэты вдали), GlowEmission и bloom, сила AO, облака. Сравнить с референсом на 13 кадрах превью.
2. HUD без плашек: `PrototypeBootstrap.World.cs` `DrawWorldHUD` + `Tools/ScenePreview/Program.cs` `AddHud`. Текст с тенью, стили HUD в `BrokerTheme` (и поля в фейке `Tools/FlowChecks/BootstrapFakes.cs`). Сохранить «ПЕРЕХОД / СВЕТОФОР» и подсказку перехода (тест `CrossingPanelFollowsTheSceneHintOnlyInTheCity`). Кэшировать строку цели.
3. Тесты:
   - VoxelTests: одиночный куб = 255, внутренний угол = 0, правила слияния, обход граней наружу, `Shades.Length` = числу вершин, лимит < 65000.
   - ScreenLookTests: ACES, Grade, виньетка, BloomShare, sRGB туда и обратно.
   - SkyLookTests: горизонт = Horizon, зенит темнее, ярче к солнцу, ниже горизонта → Ground, без NaN, облака гаснут у горизонта.
4. Полный прогон (Core, Flow, Voice, Server, синтаксис, превью `--check` 13/13). Документы: `VOXEL_STYLE.md`, `Tools/ScenePreview/README.md`, «Реализовано» в журнале. Перегенерировать 13 превью. PR → CI 12/12 → squash-merge.

Ограничение: Unity не запускалась; шейдеры проверены только glslang (HLSL → SPIR-V, linear и gamma).

## Дальше
- PR #9 «Аудит: баги и оптимизация»: метрики сцены (рендереры, материалы, свет, треугольники), static batching, точечные источники, аллокации OnGUI, культура и парсинг чисел, тесты.
- PR #10–#11 «Экономика и покупки», сохранения v6:
  - проверить экономику;
  - машины: автосалон, классы от ≈$900 (б/у седан) до ≈$30k (спорткар), содержание, перепродажа, аркадная езда по своей дорожной сети;
  - недвижимость: жилой квартал как на референсе — жить или сдавать за доход в день;
  - вывод денег владельцем, статус у клиентов.
  - Порядок: правила в журнал как «тестовый баланс» → код → миграции v6→v5→… → UI → тесты → документы.

## Порядок работы
- Каждый шаг — свой PR: create_branch → push_files → create_pull_request → CI 12/12 → squash-merge.
- Проверка шейдеров: glslang + CGIncludes Unity 2019.4; из `CGPROGRAM` компилируются vert/frag с `-D SHADER_API_D3D11`, отдельно linear и `UNITY_COLORSPACE_GAMMA`.
- Превью: `dotnet build Tools/ScenePreview/ScenePreview.csproj -c Release -o out`, затем `dotnet out/ScenePreview.dll <папка> [кадры] [--fast] [--check]`.
