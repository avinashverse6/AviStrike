# Cage Duel

**Cage Duel** (placeholder working title) is an original 1v1 mobile third-person shooter built for **Unity 2022.3 LTS** (or newer) using **C#** and the **Universal Render Pipeline (URP)**.

The playable scene, prefabs, materials, UI layout, and baked NavMesh are generated **100% from C# Editor code** using Unity primitives (`Cube`, `Capsule`, `Cylinder`, `Sphere`) and procedural materials—no hand-written `.unity` / `.prefab` YAML files and no external binary assets are stored in git.

---

## Important Note About `ProjectSettings`

Hand-writing Unity YAML `ProjectSettings/*.asset` files (`ProjectSettings.asset`, `GraphicsSettings.asset`, `QualitySettings.asset`, `TagManager.asset`, etc.) outside Unity is fragile and can corrupt serialization across Unity versions. Therefore, **only `ProjectSettings/ProjectVersion.txt` (`2022.3.31f1`) is included** in git.

You have **two safe ways** to open and run this project:

### Option A — Open This Repo Directly in Unity Hub (Quickest)
1. Install **Unity 2022.3 LTS** (recommended `2022.3.31f1` or a newer `2022.3.x` LTS patch; newer major Editors may require package upgrades) with the **Android Build Support** module (`Android SDK & NDK Tools` + `OpenJDK`).
2. Open **Unity Hub** → click **Add** → **Add project from disk** → select this repository folder (`AviStrike`).
3. If Unity Hub asks to switch Editor version to match your installed `2022.3.x` LTS build, confirm **Continue**.
4. Unity will automatically generate clean `ProjectSettings/*.asset` and `.meta` files and resolve `Packages/manifest.json` (`URP`, `Input System`, `AI Navigation`, `TextMeshPro`, `Unity UI`).
5. **If Unity shows a prompt asking to enable the new Input System backend and restart the Editor, click `Yes`**.
   - *(You can also verify this anytime under `Edit > Project Settings > Player > Other Settings > Active Input Handling` → set to **`Input System Package (New)`** or **`Both`**).*
6. Once the Editor opens, click **`CageDuel > Build Everything`** in the top menu bar.
   - Because fresh `ProjectSettings` will not have a URP Pipeline Asset assigned yet, `CageDuel > Build Everything` **automatically creates and assigns** a `UniversalRenderPipelineAsset` + `UniversalRendererData` in `GraphicsSettings` and `QualitySettings` for the generated materials.

### Option B — Blank URP Project Fallback (Editor-Generated ProjectSettings)
If opening without pre-generated `ProjectSettings` ever causes any version-specific quirk in your Unity install, do this instead:
1. Open **Unity Hub** → **New project** → select **Unity 2022.3 LTS** → choose the **3D (URP)** template → create the project.
2. Inside Unity, open **`Window > Package Manager`**, switch dropdown to **Unity Registry**, and install:
   - **Input System** (`com.unity.inputsystem`) — click **Yes** when prompted to restart.
   - **AI Navigation** (`com.unity.ai.navigation`).
   *(Universal RP and TextMeshPro are already included in the 3D URP template.)*
3. Copy this repository's **`Assets/Scripts`** folder into your new Unity project's `Assets/` directory (`<YourProject>/Assets/Scripts`).
4. Let Unity compile the scripts, then click **`CageDuel > Build Everything`** in the top menu bar.

---

## Generating the Game (`CageDuel > Build Everything`)

1. Wait for Unity to finish compiling C# scripts (check bottom-right spinner).
2. In Unity's top menu bar, click:
   ```text
   CageDuel > Build Everything
   ```
3. What this single command does automatically:
   - Imports **TMP Essential Resources** silently if TextMeshPro's default font asset has not been imported yet (and automatically resumes building once the import completes).
   - Ensures a valid **URP Render Pipeline Asset** is created and assigned in `Graphics` and `Quality` settings.
   - Generates procedural URP materials under `Assets/Generated/Materials/`.
   - Generates prefabs under `Assets/Generated/Prefabs/`.
   - Generates the complete enclosed ~40×40m arena scene at `Assets/Generated/Scenes/CageDuelArena.unity`, bakes and saves the `NavMesh` asset under `Assets/Generated/NavMesh/`, wires the third-person player, camera, weapon, target dummies, and mobile/desktop HUD, and adds `CageDuelArena.unity` to **Scenes In Build**.
4. Press **Play** in the Unity Editor!

---

## How to Build the Android APK

1. In **Unity Hub**, go to **Installs** → click the gear icon next to your Unity 2022.3 LTS install → **Add modules** → ensure **Android Build Support**, **Android SDK & NDK Tools**, and **OpenJDK** are installed.
2. Open the project and run **`CageDuel > Build Everything`** at least once so `Assets/Generated/Scenes/CageDuelArena.unity` exists and is checked in `Scenes In Build`.
3. Open **`File > Build Settings...`**:
   - Verify `Assets/Generated/Scenes/CageDuelArena.unity` is listed and checked at index `0`.
   - Select **Android** in the Platform list and click **Switch Platform** (wait for shader/script recompile).
4. Click **Player Settings...** (bottom-left of Build Settings) and configure under **Player** (Android tab):
   - **Company Name**: e.g. `CageDuelStudio`
   - **Product Name**: `Cage Duel`
   - **Resolution and Presentation > Default Orientation**: **`Landscape Left`** (or `Auto Rotation` with only `Landscape Left` and `Landscape Right` enabled).
   - **Other Settings > Identification > Override Default Package Name** (if applicable): e.g. `com.cageduel.game`
   - **Other Settings > Identification > Minimum API Level**: **Android 7.0 'Nougat' (API level 24)** or higher (API 26+ recommended).
   - **Other Settings > Configuration > Scripting Backend**: **`IL2CPP`**
   - **Other Settings > Configuration > Target Architectures**: Check **`ARM64`** (required for modern Android devices).
   - **Other Settings > Configuration > Active Input Handling**: **`Input System Package (New)`** or **`Both`**.
5. Close Player Settings, return to **`File > Build Settings...`**, ensure **Build App Bundle (Google Play)** is **unchecked** (so Unity outputs a standalone `.apk` instead of `.aab`), and click **Build** (or **Build And Run** with an Android phone connected via USB Debugging).
6. Choose an output path such as `Builds/CageDuel.apk`, transfer/install onto your Android device (`adb install -r Builds/CageDuel.apk`), and launch!

---

## Repository Folder Structure

```text
Assets/
  Scripts/
    Player/     # Third-person motor, camera rig, input router, health/armor, target dummy
    Weapons/    # Hitscan weapon, stats, recoil, ammo/reload, muzzle flash & tracers
    AI/         # NavMesh bot logic (Phase 2)
    Match/      # Spawn points, cover spot markers, round/match state (Phase 1-2)
    UI/         # Virtual joystick, touch look, mobile buttons, hit markers, damage numbers, HUD
    Net/        # Netcode for GameObjects + Unity Relay 1v1 sync (Phase 3)
    Editor/     # CageDuel > Build Everything procedural generator
Packages/
  manifest.json # URP, Input System, AI Navigation, TextMeshPro, Unity UI
ProjectSettings/
  ProjectVersion.txt
```

---

## Development Progress

### Step 0 — Project Bootstrap (Completed)
- Created standard Unity `.gitignore`, Git LFS `.gitattributes`, MIT `LICENSE`, `CREDITS.md`, `Packages/manifest.json` (URP, Input System, AI Navigation, TextMeshPro), `ProjectSettings/ProjectVersion.txt`, and `Assets/Scripts/{Player,Weapons,AI,Match,UI,Net,Editor}` folder hierarchy.
- Added setup documentation for Unity Hub, `CageDuel > Build Everything`, and Android APK builds.


### Phase 1 — Core Gameplay (Implemented; Unity verification pending)

This is a **single-player combat sandbox**, not a completed 1v1 match yet.

- **Arena generation:** approximately 40×40 metres, solid perimeter fence collision,
  four crate clusters, two container obstacles, central 1.5m platform with two ramps,
  eight barricade/cover markers and two opposing spawn points. The generator bakes
  the arena geometry with AI Navigation and persists the NavMesh data asset.
- **Player:** camera-relative CharacterController movement, jump, toggle crouch with
  overhead-clearance checks, shoulder camera collision avoidance and recoil recovery.
- **Input/UI:** desktop keyboard/mouse and pointer-isolated mobile joystick, right-side
  look zone and fire/jump/crouch/reload buttons. UI circles/rings are generated at runtime.
- **Weapon:** automatic Vanguard AR, two-stage camera/muzzle hitscan with self-hit
  filtering and obstruction checks, spread/bloom, recoil, 30-round magazine,
  120 reserve rounds, 1.75s reload, procedural muzzle/impact particles and tracer.
- **Combat:** 100 health, 50 armor, 65% armor absorption while armor lasts, 1.5×
  height-based headshots, floating damage numbers, hit markers, death collapse,
  and three-second sandbox respawn. Two stationary armored dummies provide targets.
- **HUD:** health/armor bars, ammo/reserve, reload progress, spread crosshair,
  hit confirmation, elimination/respawn banner and desktop cursor-lock toggle.

#### Script map

| Folder | Implemented scripts |
| --- | --- |
| Player | `DamageModel` (`IDamageable`, damage records, combat events), `HealthArmor`, `TargetDummy`, `PlayerInputRouter`, `ThirdPersonMotor`, `ThirdPersonCameraRig` |
| Weapons | `WeaponStats`, `HitscanWeapon` |
| Match | `SpawnPoint`, `ArenaCoverSpot` (markers only, no round manager) |
| UI | `ProceduralUISprites`, `VirtualJoystick`, `TouchLookZone`, `MobileActionButton`, `EventSystemBootstrap`, `HitMarkerUI`, `DamageNumberPopup`, `PlayerHudController` |
| Editor | `CageDuelBuilder` |

#### Generated output and scene layout

`Assets/Generated/` contains `Settings/` (URP assets if needed), `Materials/`,
`Prefabs/` (crate, barricade, container, fighter and dummy), `NavMesh/` and
`Scenes/CageDuelArena.unity`. TMP Essential Resources are imported from the installed
TextMeshPro package, not downloaded custom assets.

The scene includes `Arena_40x40` (floor, perimeter, platform/ramps, containers,
crates, cover and spawns), `PlayerFighter`, `Main Camera`, `TrainingDummies`,
`EventSystem`, lighting and `HUD_Canvas`. Player scene references are recorded as
prefab overrides so they survive reopening the scene.

**Rebuilding replaces generated prefabs and the arena scene.** Save your work first;
customize copies outside `Assets/Generated` or change the generator. Do not rely on
manual edits to generated objects surviving the next build.

#### Controls

| Action | Desktop | Mobile |
| --- | --- | --- |
| Move | WASD / arrow keys | Left virtual joystick |
| Look | Mouse while aim is locked | Drag right-side look zone |
| Toggle mouse aim | Tab or HUD toggle; Esc unlocks | Not applicable |
| Fire | Hold left mouse while locked, or F | Hold FIRE |
| Jump | Space | Tap JUMP |
| Toggle crouch | C / left Ctrl | Tap CROUCH |
| Reload | R; auto-reload when magazine empties with reserve available | Tap RELOAD |
| Sandbox self-damage | K (30 raw damage per press) | No dedicated debug button |

Mouse aim is initially unlocked. Click the Game view and press Tab for desktop
shooting, or leave it unlocked to exercise the touch UI using a mouse. Real
simultaneous movement/look/fire must be checked on a touch device.

#### Phase 1 verification checklist

1. **Compile/generate:** open with Unity 2022.3 LTS, resolve packages, enable Input
   System and run `CageDuel > Build Everything`. Check Console for zero compile
   errors and the build-success log. Verify the generated scene is in Build Settings.
2. **Persistence:** save/reopen the scene before Play. Confirm the player still has
   camera, UI, weapon and spawn references. Run the generator a second time and
   check that it completes without duplicated scene roots or missing materials.
3. **Movement/camera:** walk, strafe and jump; collide with perimeter walls,
   containers and crates. Walk both ramps onto the platform. Rotate near walls and
   verify the camera pulls inward instead of clipping through them.
4. **Crouch/cover:** toggle crouch behind a barricade. Verify reduced height and
   speed, lowered camera, and tighter spread. For an overhead-clearance test, add
   a temporary cube in a copy of the scene with its underside between crouching
   and standing head height: standing must remain blocked until clear.
5. **Shooting/recoil:** aim at each dummy. Hold fire and check magazine decrement,
   muzzle flash, tracer, impact sparks, rising/recovering recoil and widening/recovering
   crosshair. Shoot a crate/wall: it must block damage to the dummy behind it.
   Fire from tight cover to verify the muzzle cannot shoot through an obstruction
   simply because the shoulder camera can see around it.
6. **Reload:** partially empty the magazine and press R. Check reload progress and
   temporary firing lockout; magazine fills only using available reserve. Empty a
   magazine to verify auto-reload. Reloading a full magazine should do nothing.
7. **Damage/armor/headshots:** select a dummy in Play Mode and inspect its
   `HealthArmor` component (Inspector Debug mode exposes private current values).
   A fresh body hit is 22 total: 14.3 armor and 7.7 health. A fresh head hit is 33
   total: 21.45 armor and 11.55 health. Once armor is exhausted, damage goes to
   health. Numbers are rounded actual damage, so fatal hits may show less.
   Expect cyan armor-dominant numbers, white health numbers, gold headshots and
   red KO numbers; hit markers are white, gold for headshots and red for kills.
8. **Dummy death:** deplete a dummy's health. Confirm collapse, disabled hitbox,
   no further damage while dead and restoration after about three seconds.
9. **Player death:** repeatedly press K. Confirm armor/health HUD loss, elimination
   banner and disabled movement/firing, then restoration of vitals/ammo and respawn
   at a marked spawn after about three seconds. These are sandbox resets, not rounds.
10. **NavMesh:** select `Arena_40x40` and its `NavMeshSurface`; enable the navigation
    visualization in Scene view. Check walkable floor, both ramps and platform,
    obstacle cutouts and the saved `CageDuelArena_NavMesh.asset`. Reopen the scene
    and confirm the baked data reference persists. No bot runs in Phase 1.
11. **Android:** follow the APK instructions above. In landscape, simultaneously
    hold joystick, drag look and hold FIRE; test all action buttons. Release each
    finger and ensure no stuck motion/fire. Check HUD readability on your device.

#### Validation status and remaining scope

Source/API review, delimiter/preprocessor checks and `git diff --check` were run
in the coding workspace. **Unity Editor and a C# compiler are not installed here**:
Unity compilation, running the generator, actual NavMesh baking, Play Mode, APK
building and physical-device testing have **not** been executed. The checklist
above is the required Editor/device acceptance pass, not a claim of passed tests.

Unity 2022.3 LTS with the pinned manifest is the target; newer major versions may
need package/API migration. Materials, shapes and effects are placeholders, and
headshots use hit height rather than an animated skeleton. No performance or
60 FPS claim is made for Android yet.

**Stopped before Phase 2.** Bot behavior, best-of-three rounds, timer, score,
win/lose screens and weapon switching/loadout are not implemented (a pistol stats
preset exists only). Networking/Relay and Phase 4 polish are also deferred. No paid
assets or services have been added, and no third-party game branding/assets are used.
