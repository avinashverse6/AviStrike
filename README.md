# Cage Duel

**Cage Duel** (placeholder working title) is an original 1v1 mobile third-person shooter built for **Unity 2022.3 LTS** (or newer) using **C#** and the **Universal Render Pipeline (URP)**.

Every scene, prefab, material, UI layout, and baked NavMesh in this repository is generated **100% from C# Editor code** using Unity primitives (`Cube`, `Capsule`, `Cylinder`, `Sphere`) and procedural materials—no hand-written `.unity` / `.prefab` YAML files and no external binary assets are stored in git.

---

## Important Note About `ProjectSettings`

Hand-writing Unity YAML `ProjectSettings/*.asset` files (`ProjectSettings.asset`, `GraphicsSettings.asset`, `QualitySettings.asset`, `TagManager.asset`, etc.) outside Unity is fragile and can corrupt serialization across Unity versions. Therefore, **only `ProjectSettings/ProjectVersion.txt` (`2022.3.31f1`) is included** in git.

You have **two safe ways** to open and run this project:

### Option A — Open This Repo Directly in Unity Hub (Quickest)
1. Install **Unity 2022.3 LTS** (recommended `2022.3.31f1` or any newer `2022.3.x` LTS / Unity 6 build) with the **Android Build Support** module (`Android SDK & NDK Tools` + `OpenJDK`).
2. Open **Unity Hub** → click **Add** → **Add project from disk** → select this repository folder (`AviStrike`).
3. If Unity Hub asks to switch Editor version to match your installed `2022.3.x` LTS build, confirm **Continue**.
4. Unity will automatically generate clean `ProjectSettings/*.asset` and `.meta` files and resolve `Packages/manifest.json` (`URP`, `Input System`, `AI Navigation`, `TextMeshPro`, `Unity UI`).
5. **If Unity shows a prompt asking to enable the new Input System backend and restart the Editor, click `Yes`**.
   - *(You can also verify this anytime under `Edit > Project Settings > Player > Other Settings > Active Input Handling` → set to **`Input System Package (New)`** or **`Both`**).*
6. Once the Editor opens, click **`CageDuel > Build Everything`** in the top menu bar.
   - Because fresh `ProjectSettings` will not have a URP Pipeline Asset assigned yet, `CageDuel > Build Everything` **automatically creates and assigns** a `UniversalRenderPipelineAsset` + `UniversalRendererData` in `GraphicsSettings` and `QualitySettings` so materials never render magenta!

### Option B — Blank URP Project Fallback (Guaranteed Clean ProjectSettings)
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
