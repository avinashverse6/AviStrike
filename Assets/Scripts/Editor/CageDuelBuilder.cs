#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using CageDuel.Match;
using CageDuel.Player;
using CageDuel.UI;
using CageDuel.Weapons;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace CageDuel.EditorTools
{
    /// <summary>
    /// Generates all Cage Duel URP settings, procedural materials, prefabs, the ~40x40m enclosed arena,
    /// baked NavMesh, player/camera/weapon rig, training dummies, and mobile + desktop HUD from code.
    /// Menu: CageDuel > Build Everything
    /// </summary>
    public static class CageDuelBuilder
    {
        private const string GeneratedRoot = "Assets/Generated";
        private const string SettingsDir = GeneratedRoot + "/Settings";
        private const string MaterialsDir = GeneratedRoot + "/Materials";
        private const string PrefabsDir = GeneratedRoot + "/Prefabs";
        private const string NavMeshDir = GeneratedRoot + "/NavMesh";
        private const string ScenesDir = GeneratedRoot + "/Scenes";
        private const string ArenaScenePath = ScenesDir + "/CageDuelArena.unity";

        private static bool _waitingForTmpImport;

        private struct MaterialPalette
        {
            public Material FloorConcrete;
            public Material FloorAccent;
            public Material CurbConcrete;
            public Material FenceSteel;
            public Material PlatformSteel;
            public Material CrateWood;
            public Material CrateTrim;
            public Material ContainerBlueGray;
            public Material ContainerRust;
            public Material BarricadeBody;
            public Material BarricadeHazardTop;
            public Material PlayerArmor;
            public Material PlayerUndersuit;
            public Material PlayerVisor;
            public Material DummyArmor;
            public Material DummyVisor;
            public Material WeaponMetal;
            public Material MuzzleFlashParticle;
            public Material TracerUnlit;
        }

        [MenuItem("CageDuel/Build Everything", priority = 1)]
        public static void BuildEverythingMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Cage Duel Builder",
                    "Please exit Play Mode before running 'CageDuel > Build Everything'.",
                    "OK");
                return;
            }

            if (EnsureTmpEssentialsReadyOrDefer())
            {
                ExecuteFullBuild();
            }
        }

        private static bool EnsureTmpEssentialsReadyOrDefer()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                return true;
            }

            Debug.Log("[CageDuel] Importing TextMeshPro Essential Resources before building scene...");
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh();

            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                return true;
            }

            if (!_waitingForTmpImport)
            {
                _waitingForTmpImport = true;
                AssetDatabase.importPackageCompleted += OnTmpPackageImportCompleted;
                Debug.Log("[CageDuel] Waiting for TMP Essential Resources package import to finish — Build Everything will continue automatically.");
            }

            return false;
        }

        private static void OnTmpPackageImportCompleted(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpPackageImportCompleted;
            _waitingForTmpImport = false;
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
                ExecuteFullBuild();
            };
        }

        private static void ExecuteFullBuild()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EnsureDirectories();
            EnsureUrpPipelineConfigured();
            MaterialPalette palette = CreateOrUpdateMaterials();

            // Build reusable prefabs in a clean temporary setup scene
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cratePrefab = CreateCratePrefab(palette);
            GameObject barricadePrefab = CreateBarricadePrefab(palette);
            GameObject containerPrefab = CreateShippingContainerPrefab(palette);
            GameObject playerPrefab = CreatePlayerFighterPrefab(palette);
            GameObject dummyPrefab = CreateTargetDummyPrefab(palette);

            // Build main playable arena scene
            Scene arenaScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SetupLightingAndEnvironment();

            GameObject arenaRoot = BuildEnclosedArena(
                palette,
                cratePrefab,
                barricadePrefab,
                containerPrefab,
                out SpawnPoint[] spawnPoints);

            BakeAndSaveArenaNavMesh(arenaRoot);

            // Spawn Main Camera with ThirdPersonCameraRig
            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera mainCam = cameraGo.AddComponent<Camera>();
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 200f;
            mainCam.fieldOfView = 62f;
            cameraGo.AddComponent<AudioListener>();
            if (cameraGo.GetComponent<UniversalAdditionalCameraData>() == null)
            {
                cameraGo.AddComponent<UniversalAdditionalCameraData>();
            }
            ThirdPersonCameraRig cameraRig = cameraGo.AddComponent<ThirdPersonCameraRig>();

            // Instantiate Player at SpawnPoint A
            GameObject playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, arenaScene);
            playerInstance.name = "PlayerFighter";
            SpawnPoint playerSpawn = spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[0] : null;
            if (playerSpawn != null)
            {
                playerInstance.transform.SetPositionAndRotation(
                    playerSpawn.transform.position,
                    playerSpawn.transform.rotation);
            }

            // Instantiate 2 Armored Target Dummies for Phase 1 combat verification
            GameObject dummiesRoot = new GameObject("TrainingDummies");
            SpawnTargetDummyInstance(
                dummyPrefab,
                arenaScene,
                dummiesRoot.transform,
                "TargetDummy_MidLane",
                new Vector3(-4.8f, 0f, 5.5f),
                Quaternion.Euler(0f, 180f, 0f));
            SpawnTargetDummyInstance(
                dummyPrefab,
                arenaScene,
                dummiesRoot.transform,
                "TargetDummy_Platform",
                new Vector3(0f, 1.50f, 1.35f),
                Quaternion.Euler(0f, 180f, 0f));

            // Create EventSystem & Complete HUD (Desktop + Mobile Touch Controls)
            CreateEventSystem();
            BuildHudAndWirePlayer(playerInstance, mainCam, cameraRig, spawnPoints);

            // Snap initial camera transform behind player for Scene/Game preview
            cameraRig.SnapYawToTarget();
            cameraGo.transform.SetPositionAndRotation(
                playerInstance.transform.position + new Vector3(0.55f, 1.75f, -3.35f),
                Quaternion.Euler(8f, playerInstance.transform.eulerAngles.y, 0f));

            EditorSceneManager.MarkSceneDirty(arenaScene);
            EditorSceneManager.SaveScene(arenaScene, ArenaScenePath);
            RegisterSceneInBuildSettings(ArenaScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CageDuel] Build Everything succeeded! Scene saved to " + ArenaScenePath + ". Press Play to test.");
        }

        private static void EnsureDirectories()
        {
            string[] dirs = { GeneratedRoot, SettingsDir, MaterialsDir, PrefabsDir, NavMeshDir, ScenesDir };
            for (int i = 0; i < dirs.Length; i++)
            {
                Directory.CreateDirectory(dirs[i]);
            }
            AssetDatabase.Refresh();
        }

        private static void EnsureUrpPipelineConfigured()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                return;
            }

            string rendererPath = SettingsDir + "/CageDuel_URP_Renderer.asset";
            string pipelinePath = SettingsDir + "/CageDuel_URP_Asset.asset";

            UniversalRenderPipelineAsset existingAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (existingAsset == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                var defaultPostProcess = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                if (defaultPostProcess != null)
                {
                    rendererData.postProcessData = defaultPostProcess;
                }

                AssetDatabase.CreateAsset(rendererData, rendererPath);
                ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(rendererData);

                existingAsset = UniversalRenderPipelineAsset.Create(rendererData);
                existingAsset.shadowDistance = 55f;
                AssetDatabase.CreateAsset(existingAsset, pipelinePath);
                EditorUtility.SetDirty(existingAsset);
            }

            GraphicsSettings.defaultRenderPipeline = existingAsset;
            QualitySettings.renderPipeline = existingAsset;
            AssetDatabase.SaveAssets();
            Debug.Log("[CageDuel] Created and assigned URP Render Pipeline Asset at " + pipelinePath);
        }

        private static MaterialPalette CreateOrUpdateMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");

            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null) particleShader = unlitShader;

            return new MaterialPalette
            {
                FloorConcrete = CreateLitMaterial("Mat_FloorConcrete", litShader, new Color(0.24f, 0.26f, 0.29f), 0.0f, 0.20f),
                FloorAccent = CreateLitMaterial("Mat_FloorAccent", litShader, new Color(0.86f, 0.64f, 0.16f), 0.0f, 0.30f),
                CurbConcrete = CreateLitMaterial("Mat_CurbConcrete", litShader, new Color(0.36f, 0.38f, 0.41f), 0.0f, 0.25f),
                FenceSteel = CreateLitMaterial("Mat_FenceSteel", litShader, new Color(0.16f, 0.19f, 0.22f), 0.65f, 0.45f),
                PlatformSteel = CreateLitMaterial("Mat_PlatformSteel", litShader, new Color(0.29f, 0.33f, 0.38f), 0.55f, 0.42f),
                CrateWood = CreateLitMaterial("Mat_CrateWood", litShader, new Color(0.62f, 0.44f, 0.25f), 0.0f, 0.22f),
                CrateTrim = CreateLitMaterial("Mat_CrateTrim", litShader, new Color(0.38f, 0.26f, 0.14f), 0.1f, 0.25f),
                ContainerBlueGray = CreateLitMaterial("Mat_ContainerSlate", litShader, new Color(0.20f, 0.38f, 0.52f), 0.35f, 0.35f),
                ContainerRust = CreateLitMaterial("Mat_ContainerRust", litShader, new Color(0.64f, 0.27f, 0.18f), 0.35f, 0.32f),
                BarricadeBody = CreateLitMaterial("Mat_BarricadeBody", litShader, new Color(0.31f, 0.34f, 0.38f), 0.20f, 0.30f),
                BarricadeHazardTop = CreateLitMaterial("Mat_BarricadeTop", litShader, new Color(0.92f, 0.58f, 0.12f), 0.15f, 0.40f),
                PlayerArmor = CreateLitMaterial("Mat_PlayerArmor", litShader, new Color(0.16f, 0.52f, 0.88f), 0.25f, 0.55f),
                PlayerUndersuit = CreateLitMaterial("Mat_PlayerUndersuit", litShader, new Color(0.14f, 0.16f, 0.20f), 0.10f, 0.30f),
                PlayerVisor = CreateEmissiveMaterial("Mat_PlayerVisor", litShader, new Color(0.25f, 0.90f, 1.0f), new Color(0.15f, 0.65f, 0.85f)),
                DummyArmor = CreateLitMaterial("Mat_DummyArmor", litShader, new Color(0.84f, 0.22f, 0.24f), 0.25f, 0.50f),
                DummyVisor = CreateEmissiveMaterial("Mat_DummyVisor", litShader, new Color(1.0f, 0.72f, 0.15f), new Color(0.85f, 0.45f, 0.05f)),
                WeaponMetal = CreateLitMaterial("Mat_WeaponMetal", litShader, new Color(0.12f, 0.13f, 0.16f), 0.75f, 0.55f),
                MuzzleFlashParticle = CreateUnlitMaterial("Mat_MuzzleFlash", particleShader, new Color(1.0f, 0.78f, 0.24f, 1.0f)),
                TracerUnlit = CreateUnlitMaterial("Mat_BulletTracer", unlitShader, new Color(1.0f, 0.88f, 0.42f, 1.0f))
            };
        }

        private static Material CreateLitMaterial(string assetName, Shader shader, Color color, float metallic, float smoothness)
        {
            string path = MaterialsDir + "/" + assetName + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateEmissiveMaterial(string assetName, Shader shader, Color baseColor, Color emissionColor)
        {
            Material mat = CreateLitMaterial(assetName, shader, baseColor, 0.2f, 0.75f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emissionColor);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateUnlitMaterial(string assetName, Shader shader, Color color)
        {
            string path = MaterialsDir + "/" + assetName + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateCratePrefab(MaterialPalette palette)
        {
            GameObject root = new GameObject("CrateCover");
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(1.35f, 1.35f, 1.35f);
            box.center = new Vector3(0f, 0.675f, 0f);

            CreatePrimitivePart("CoreBox", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.675f, 0f), Quaternion.identity, new Vector3(1.31f, 1.31f, 1.31f),
                palette.CrateWood, false);

            CreatePrimitivePart("TopBand", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 1.28f, 0f), Quaternion.identity, new Vector3(1.36f, 0.12f, 1.36f),
                palette.CrateTrim, false);

            CreatePrimitivePart("BottomBand", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.07f, 0f), Quaternion.identity, new Vector3(1.36f, 0.12f, 1.36f),
                palette.CrateTrim, false);

            return SaveAsPrefabAndDestroyTemp(root, PrefabsDir + "/CrateCover.prefab");
        }

        private static GameObject CreateBarricadePrefab(MaterialPalette palette)
        {
            GameObject root = new GameObject("TacticalBarricade");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(2.6f, 1.25f, 0.55f);
            col.center = new Vector3(0f, 0.625f, 0f);

            ArenaCoverSpot coverSpot = root.AddComponent<ArenaCoverSpot>();
            coverSpot.Configure(1.25f, 1.15f);

            CreatePrimitivePart("WallBlock", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.56f, 0f), Quaternion.identity, new Vector3(2.6f, 1.12f, 0.52f),
                palette.BarricadeBody, false);

            CreatePrimitivePart("TopCap", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 1.185f, 0f), Quaternion.identity, new Vector3(2.64f, 0.13f, 0.56f),
                palette.BarricadeHazardTop, false);

            CreatePrimitivePart("LeftFoot", PrimitiveType.Cube, root.transform,
                new Vector3(-1.05f, 0.12f, 0f), Quaternion.identity, new Vector3(0.26f, 0.24f, 0.72f),
                palette.FenceSteel, false);

            CreatePrimitivePart("RightFoot", PrimitiveType.Cube, root.transform,
                new Vector3(1.05f, 0.12f, 0f), Quaternion.identity, new Vector3(0.26f, 0.24f, 0.72f),
                palette.FenceSteel, false);

            return SaveAsPrefabAndDestroyTemp(root, PrefabsDir + "/TacticalBarricade.prefab");
        }

        private static GameObject CreateShippingContainerPrefab(MaterialPalette palette)
        {
            GameObject root = new GameObject("ShippingContainer");
            BoxCollider col = root.AddComponent<BoxCollider>();
            col.size = new Vector3(2.55f, 2.75f, 6.4f);
            col.center = new Vector3(0f, 1.375f, 0f);

            CreatePrimitivePart("Hull", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 1.375f, 0f), Quaternion.identity, new Vector3(2.48f, 2.70f, 6.32f),
                palette.ContainerBlueGray, false);

            CreatePrimitivePart("RoofFrame", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 2.72f, 0f), Quaternion.identity, new Vector3(2.56f, 0.08f, 6.40f),
                palette.FenceSteel, false);

            // Vertical corrugation ribs along both long sides
            for (int i = -2; i <= 2; i++)
            {
                float z = i * 1.15f;
                CreatePrimitivePart("Rib_" + i, PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 1.375f, z), Quaternion.identity, new Vector3(2.56f, 2.52f, 0.14f),
                    palette.ContainerBlueGray, false);
            }

            return SaveAsPrefabAndDestroyTemp(root, PrefabsDir + "/ShippingContainer.prefab");
        }

        private static GameObject CreatePlayerFighterPrefab(MaterialPalette palette)
        {
            GameObject root = new GameObject("PlayerFighter");

            CharacterController cc = root.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.36f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.04f;

            HealthArmor vitals = root.AddComponent<HealthArmor>();
            PlayerInputRouter inputRouter = root.AddComponent<PlayerInputRouter>();
            ThirdPersonMotor motor = root.AddComponent<ThirdPersonMotor>();
            HitscanWeapon weapon = root.AddComponent<HitscanWeapon>();

            Transform visualBody = BuildHumanoidPrimitiveVisual(
                root.transform,
                palette.PlayerArmor,
                palette.PlayerUndersuit,
                palette.PlayerVisor,
                palette.WeaponMetal,
                true,
                out Transform weaponPivot,
                out Transform muzzlePoint);

            ParticleSystem muzzleFlash = CreateMuzzleFlashParticles(muzzlePoint, palette.MuzzleFlashParticle);
            ParticleSystem impactSparks = CreateImpactSparkParticles(root.transform, palette.MuzzleFlashParticle);
            LineRenderer tracer = CreateBulletTracer(root.transform, palette.TracerUnlit);

            vitals.Configure(visualBody, null, 100f, 50f, true, 3.0f);
            motor.Configure(inputRouter, null, vitals, visualBody);
            weapon.Configure(
                WeaponStats.CreateDefaultRifle(),
                inputRouter,
                null,
                motor,
                vitals,
                weaponPivot,
                muzzlePoint,
                muzzleFlash,
                impactSparks,
                tracer);

            return SaveAsPrefabAndDestroyTemp(root, PrefabsDir + "/PlayerFighter.prefab");
        }

        private static GameObject CreateTargetDummyPrefab(MaterialPalette palette)
        {
            GameObject root = new GameObject("TargetDummy");

            CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f;
            capsule.radius = 0.38f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            HealthArmor vitals = root.AddComponent<HealthArmor>();
            TargetDummy dummy = root.AddComponent<TargetDummy>();

            Transform visualBody = BuildHumanoidPrimitiveVisual(
                root.transform,
                palette.DummyArmor,
                palette.PlayerUndersuit,
                palette.DummyVisor,
                palette.WeaponMetal,
                false,
                out _,
                out _);

            vitals.Configure(visualBody, null, 100f, 50f, true, 3.0f);
            dummy.Configure(visualBody);

            return SaveAsPrefabAndDestroyTemp(root, PrefabsDir + "/TargetDummy.prefab");
        }

        private static Transform BuildHumanoidPrimitiveVisual(
            Transform parent,
            Material primaryArmorMat,
            Material undersuitMat,
            Material visorMat,
            Material gunMat,
            bool includeRifle,
            out Transform weaponPivot,
            out Transform muzzlePoint)
        {
            GameObject visualRoot = new GameObject("VisualBody");
            visualRoot.transform.SetParent(parent, false);

            // Legs
            CreatePrimitivePart("LeftLeg", PrimitiveType.Capsule, visualRoot.transform,
                new Vector3(-0.14f, 0.42f, 0f), Quaternion.identity, new Vector3(0.20f, 0.42f, 0.20f),
                undersuitMat, false);
            CreatePrimitivePart("RightLeg", PrimitiveType.Capsule, visualRoot.transform,
                new Vector3(0.14f, 0.42f, 0f), Quaternion.identity, new Vector3(0.20f, 0.42f, 0.20f),
                undersuitMat, false);

            // Torso & Chest Armor
            CreatePrimitivePart("Torso", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.08f, 0f), Quaternion.identity, new Vector3(0.48f, 0.56f, 0.28f),
                primaryArmorMat, false);
            CreatePrimitivePart("ChestRig", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.12f, 0.02f), Quaternion.identity, new Vector3(0.42f, 0.34f, 0.32f),
                undersuitMat, false);

            // Shoulders / Arms
            CreatePrimitivePart("LeftArm", PrimitiveType.Capsule, visualRoot.transform,
                new Vector3(-0.32f, 1.08f, 0.06f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.15f, 0.30f, 0.15f),
                primaryArmorMat, false);
            CreatePrimitivePart("RightArm", PrimitiveType.Capsule, visualRoot.transform,
                new Vector3(0.32f, 1.14f, 0.16f), Quaternion.Euler(68f, 0f, 0f), new Vector3(0.15f, 0.30f, 0.15f),
                primaryArmorMat, false);

            // Head & Tactical Visor
            CreatePrimitivePart("Head", PrimitiveType.Sphere, visualRoot.transform,
                new Vector3(0f, 1.58f, 0f), Quaternion.identity, new Vector3(0.34f, 0.36f, 0.34f),
                primaryArmorMat, false);
            CreatePrimitivePart("Visor", PrimitiveType.Cube, visualRoot.transform,
                new Vector3(0f, 1.59f, 0.14f), Quaternion.identity, new Vector3(0.26f, 0.10f, 0.12f),
                visorMat, false);

            weaponPivot = null;
            muzzlePoint = null;

            if (includeRifle)
            {
                GameObject gunPivotGo = new GameObject("WeaponPivot");
                gunPivotGo.transform.SetParent(visualRoot.transform, false);
                gunPivotGo.transform.localPosition = new Vector3(0.28f, 1.32f, 0.32f);
                weaponPivot = gunPivotGo.transform;

                CreatePrimitivePart("Receiver", PrimitiveType.Cube, weaponPivot,
                    new Vector3(0f, 0f, 0.12f), Quaternion.identity, new Vector3(0.08f, 0.13f, 0.48f),
                    gunMat, false);
                CreatePrimitivePart("Barrel", PrimitiveType.Cylinder, weaponPivot,
                    new Vector3(0f, 0.02f, 0.48f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.035f, 0.18f, 0.035f),
                    gunMat, false);
                CreatePrimitivePart("Magazine", PrimitiveType.Cube, weaponPivot,
                    new Vector3(0f, -0.12f, 0.14f), Quaternion.Euler(14f, 0f, 0f), new Vector3(0.055f, 0.18f, 0.09f),
                    undersuitMat, false);
                CreatePrimitivePart("Optic", PrimitiveType.Cube, weaponPivot,
                    new Vector3(0f, 0.09f, 0.10f), Quaternion.identity, new Vector3(0.055f, 0.055f, 0.14f),
                    visorMat, false);

                GameObject muzzleGo = new GameObject("MuzzlePoint");
                muzzleGo.transform.SetParent(weaponPivot, false);
                muzzleGo.transform.localPosition = new Vector3(0f, 0.02f, 0.68f);
                muzzlePoint = muzzleGo.transform;
            }

            return visualRoot.transform;
        }

        private static ParticleSystem CreateMuzzleFlashParticles(Transform muzzleParent, Material flashMat)
        {
            GameObject flashGo = new GameObject("MuzzleFlashParticles");
            flashGo.transform.SetParent(muzzleParent, false);
            flashGo.transform.localPosition = Vector3.zero;
            flashGo.transform.localRotation = Quaternion.identity;

            ParticleSystem ps = flashGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.08f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 7.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.20f);
            main.startColor = new Color(1.0f, 0.80f, 0.25f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 32;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.02f;

            var psRenderer = flashGo.GetComponent<ParticleSystemRenderer>();
            psRenderer.sharedMaterial = flashMat;
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;

            return ps;
        }

        private static ParticleSystem CreateImpactSparkParticles(Transform ownerRoot, Material sparkMat)
        {
            GameObject sparkGo = new GameObject("ImpactSparkParticles");
            sparkGo.transform.SetParent(ownerRoot, false);

            ParticleSystem ps = sparkGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.12f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new Color(1.0f, 0.85f, 0.35f, 1.0f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.04f;

            var psRenderer = sparkGo.GetComponent<ParticleSystemRenderer>();
            psRenderer.sharedMaterial = sparkMat;
            psRenderer.shadowCastingMode = ShadowCastingMode.Off;
            psRenderer.receiveShadows = false;

            return ps;
        }

        private static LineRenderer CreateBulletTracer(Transform ownerRoot, Material tracerMat)
        {
            GameObject tracerGo = new GameObject("BulletTracer");
            tracerGo.transform.SetParent(ownerRoot, false);

            LineRenderer lr = tracerGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = 0.035f;
            lr.endWidth = 0.012f;
            lr.sharedMaterial = tracerMat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        private static void SetupLightingAndEnvironment()
        {
            GameObject sunGo = new GameObject("Directional Light");
            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.96f, 0.90f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(48f, -36f, 0f);
            if (sunGo.GetComponent<UniversalAdditionalLightData>() == null)
            {
                sunGo.AddComponent<UniversalAdditionalLightData>();
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.46f, 0.54f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.39f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.25f);
        }

        private static GameObject BuildEnclosedArena(
            MaterialPalette palette,
            GameObject cratePrefab,
            GameObject barricadePrefab,
            GameObject containerPrefab,
            out SpawnPoint[] spawnPoints)
        {
            GameObject arenaRoot = new GameObject("Arena_40x40");

            // 1. Floor (40m x 40m solid box floor with top at y = 0)
            CreatePrimitivePart("FloorBase", PrimitiveType.Cube, arenaRoot.transform,
                new Vector3(0f, -0.5f, 0f), Quaternion.identity, new Vector3(40f, 1.0f, 40f),
                palette.FloorConcrete, true);

            // Decorative court/lane lines flush with floor (colliders stripped)
            GameObject markings = new GameObject("FloorMarkings");
            markings.transform.SetParent(arenaRoot.transform, false);
            CreatePrimitivePart("MidLine", PrimitiveType.Cube, markings.transform,
                new Vector3(0f, 0.005f, 0f), Quaternion.identity, new Vector3(39.2f, 0.01f, 0.18f),
                palette.FloorAccent, false);
            CreatePrimitivePart("SouthSpawnLine", PrimitiveType.Cube, markings.transform,
                new Vector3(0f, 0.005f, -13.5f), Quaternion.identity, new Vector3(39.2f, 0.01f, 0.12f),
                palette.FloorAccent, false);
            CreatePrimitivePart("NorthSpawnLine", PrimitiveType.Cube, markings.transform,
                new Vector3(0f, 0.005f, 13.5f), Quaternion.identity, new Vector3(39.2f, 0.01f, 0.12f),
                palette.FloorAccent, false);

            // 2. Enclosed Perimeter Fence Walls (~40x40m)
            BuildPerimeterFenceWalls(arenaRoot.transform, palette);

            // 3. Central Raised Platform + 2 Walkable Ramps
            BuildRaisedCenterPlatform(arenaRoot.transform, palette);

            // 4. Shipping Containers (2 large flanking lane containers, symmetrical for fair 1v1)
            GameObject containersGroup = new GameObject("ShippingContainers");
            containersGroup.transform.SetParent(arenaRoot.transform, false);
            InstantiateArenaPrefab(containerPrefab, containersGroup.transform, "Container_West",
                new Vector3(-11.8f, 0f, 2.4f), Quaternion.Euler(0f, 12f, 0f));
            EnchantContainerTint(
                InstantiateArenaPrefab(containerPrefab, containersGroup.transform, "Container_East",
                    new Vector3(11.8f, 0f, -2.4f), Quaternion.Euler(0f, 12f, 0f)),
                palette.ContainerRust);

            // 5. Supply Crate Clusters
            GameObject cratesGroup = new GameObject("SupplyCrates");
            cratesGroup.transform.SetParent(arenaRoot.transform, false);
            PlaceCrateCluster(cratePrefab, cratesGroup.transform, "Cluster_NW", new Vector3(-13.2f, 0f, 10.8f));
            PlaceCrateCluster(cratePrefab, cratesGroup.transform, "Cluster_SE", new Vector3(13.2f, 0f, -10.8f));
            PlaceCrateCluster(cratePrefab, cratesGroup.transform, "Cluster_SW", new Vector3(-13.2f, 0f, -10.8f));
            PlaceCrateCluster(cratePrefab, cratesGroup.transform, "Cluster_NE", new Vector3(13.2f, 0f, 10.8f));

            // 6. Spawn Sightline Blockers (protects Spawn A & B from frame-0 cross-map fire)
            CreatePrimitivePart("SpawnShield_South", PrimitiveType.Cube, arenaRoot.transform,
                new Vector3(0f, 1.15f, -14.2f), Quaternion.identity, new Vector3(3.4f, 2.3f, 0.45f),
                palette.CurbConcrete, true);
            CreatePrimitivePart("SpawnShield_North", PrimitiveType.Cube, arenaRoot.transform,
                new Vector3(0f, 1.15f, 14.2f), Quaternion.identity, new Vector3(3.4f, 2.3f, 0.45f),
                palette.CurbConcrete, true);

            // 7. 8 Tactical Chest-High Cover Spots (with ArenaCoverSpot markers for Phase 2 AI)
            GameObject coverGroup = new GameObject("CoverSpots");
            coverGroup.transform.SetParent(arenaRoot.transform, false);
            (string name, Vector3 pos, float yaw)[] coverDefinitions =
            {
                ("CoverSpot_01_SouthLeft",  new Vector3(-5.4f, 0f, -10.8f),  12f),
                ("CoverSpot_02_SouthRight", new Vector3( 5.4f, 0f, -10.8f), -12f),
                ("CoverSpot_03_NorthLeft",  new Vector3(-5.4f, 0f,  10.8f), -12f),
                ("CoverSpot_04_NorthRight", new Vector3( 5.4f, 0f,  10.8f),  12f),
                ("CoverSpot_05_MidWest",    new Vector3(-7.0f, 0f,  -3.8f),  24f),
                ("CoverSpot_06_MidEast",    new Vector3( 7.0f, 0f,   3.8f),  24f),
                ("CoverSpot_07_OuterWest",  new Vector3(-15.5f, 0f, -4.2f),   0f),
                ("CoverSpot_08_OuterEast",  new Vector3( 15.5f, 0f,  4.2f),   0f),
            };

            for (int i = 0; i < coverDefinitions.Length; i++)
            {
                InstantiateArenaPrefab(
                    barricadePrefab,
                    coverGroup.transform,
                    coverDefinitions[i].name,
                    coverDefinitions[i].pos,
                    Quaternion.Euler(0f, coverDefinitions[i].yaw, 0f));
            }

            // 8. 2 Spawn Points (stored outside NavMesh geometry children or as pure transforms without colliders)
            GameObject spawnsGroup = new GameObject("SpawnPoints");
            spawnsGroup.transform.SetParent(arenaRoot.transform, false);

            SpawnPoint spawnA = CreateSpawnPoint(
                spawnsGroup.transform,
                "SpawnPoint_A",
                0,
                "Spawn A (South)",
                new Vector3(0f, 0.02f, -17.0f),
                Quaternion.Euler(0f, 0f, 0f),
                new Color(0.20f, 0.78f, 1.0f, 0.9f));

            SpawnPoint spawnB = CreateSpawnPoint(
                spawnsGroup.transform,
                "SpawnPoint_B",
                1,
                "Spawn B (North)",
                new Vector3(0f, 0.02f, 17.0f),
                Quaternion.Euler(0f, 180f, 0f),
                new Color(1.0f, 0.35f, 0.35f, 0.9f));

            spawnPoints = new[] { spawnA, spawnB };
            return arenaRoot;
        }

        private static void BuildPerimeterFenceWalls(Transform arenaParent, MaterialPalette palette)
        {
            GameObject wallsRoot = new GameObject("PerimeterFenceWalls");
            wallsRoot.transform.SetParent(arenaParent, false);

            const float halfExtent = 20f;
            const float wallHeight = 4.4f;
            const float wallThickness = 0.45f;

            // North & South sides (along X)
            BuildSingleFenceSide(wallsRoot.transform, "Fence_North",
                new Vector3(0f, 0f, halfExtent), Quaternion.identity, 40f, wallHeight, wallThickness, palette);
            BuildSingleFenceSide(wallsRoot.transform, "Fence_South",
                new Vector3(0f, 0f, -halfExtent), Quaternion.identity, 40f, wallHeight, wallThickness, palette);

            // East & West sides (rotated 90 degrees)
            BuildSingleFenceSide(wallsRoot.transform, "Fence_East",
                new Vector3(halfExtent, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), 40f, wallHeight, wallThickness, palette);
            BuildSingleFenceSide(wallsRoot.transform, "Fence_West",
                new Vector3(-halfExtent, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), 40f, wallHeight, wallThickness, palette);
        }

        private static void BuildSingleFenceSide(
            Transform parent,
            string sideName,
            Vector3 centerPos,
            Quaternion sideRotation,
            float length,
            float height,
            float thickness,
            MaterialPalette palette)
        {
            GameObject side = new GameObject(sideName);
            side.transform.SetParent(parent, false);
            side.transform.SetPositionAndRotation(centerPos, sideRotation);

            // Full-height containment collider
            BoxCollider containmentCollider = side.AddComponent<BoxCollider>();
            containmentCollider.size = new Vector3(length + thickness, height, thickness);
            containmentCollider.center = new Vector3(0f, height * 0.5f, 0f);

            // Concrete base curb (1.0m tall)
            CreatePrimitivePart("ConcreteCurb", PrimitiveType.Cube, side.transform,
                new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(length + thickness, 1.0f, thickness),
                palette.CurbConcrete, false);

            // Horizontal steel fence rails
            float[] railHeights = { 1.05f, 2.65f, 4.28f };
            for (int r = 0; r < railHeights.Length; r++)
            {
                CreatePrimitivePart("Rail_" + r, PrimitiveType.Cube, side.transform,
                    new Vector3(0f, railHeights[r], 0f), Quaternion.identity, new Vector3(length, 0.10f, 0.12f),
                    palette.FenceSteel, false);
            }

            // Heavy posts every 4m + vertical cage bars every 1m
            int barSteps = Mathf.RoundToInt(length);
            for (int i = 0; i <= barSteps; i++)
            {
                float x = -length * 0.5f + i;
                bool isHeavyPost = (i % 4 == 0);
                float postWidth = isHeavyPost ? 0.22f : 0.055f;
                float barCenterY = isHeavyPost ? height * 0.5f : 2.68f;
                float barSizeY = isHeavyPost ? height : (height - 1.05f);

                CreatePrimitivePart(isHeavyPost ? "Post_" + i : "CageBar_" + i, PrimitiveType.Cube, side.transform,
                    new Vector3(x, barCenterY, 0f), Quaternion.identity, new Vector3(postWidth, barSizeY, postWidth),
                    palette.FenceSteel, false);
            }
        }

        private static void BuildRaisedCenterPlatform(Transform arenaParent, MaterialPalette palette)
        {
            GameObject platformRoot = new GameObject("RaisedCenterPlatform");
            platformRoot.transform.SetParent(arenaParent, false);

            const float deckHeight = 1.50f;
            const float deckSize = 6.0f;

            // Solid raised deck (top surface at y = 1.50m)
            CreatePrimitivePart("PlatformDeck", PrimitiveType.Cube, platformRoot.transform,
                new Vector3(0f, deckHeight * 0.5f, 0f), Quaternion.identity, new Vector3(deckSize, deckHeight, deckSize),
                palette.PlatformSteel, true);

            // Overwatch side parapets (East & West) leaving North & South open for ramps
            CreatePrimitivePart("Parapet_West", PrimitiveType.Cube, platformRoot.transform,
                new Vector3(-2.82f, deckHeight + 0.52f, 0f), Quaternion.identity, new Vector3(0.36f, 1.04f, 4.2f),
                palette.BarricadeBody, true);
            CreatePrimitivePart("Parapet_WestCap", PrimitiveType.Cube, platformRoot.transform,
                new Vector3(-2.82f, deckHeight + 1.08f, 0f), Quaternion.identity, new Vector3(0.40f, 0.10f, 4.24f),
                palette.BarricadeHazardTop, false);

            CreatePrimitivePart("Parapet_East", PrimitiveType.Cube, platformRoot.transform,
                new Vector3(2.82f, deckHeight + 0.52f, 0f), Quaternion.identity, new Vector3(0.36f, 1.04f, 4.2f),
                palette.BarricadeBody, true);
            CreatePrimitivePart("Parapet_EastCap", PrimitiveType.Cube, platformRoot.transform,
                new Vector3(2.82f, deckHeight + 1.08f, 0f), Quaternion.identity, new Vector3(0.40f, 0.10f, 4.24f),
                palette.BarricadeHazardTop, false);

            // Walkable NavMesh Ramps (~21.8 degree incline) on South and North sides
            BuildWalkableRamp(
                platformRoot.transform,
                "Ramp_South",
                new Vector3(0f, -0.02f, -6.80f),
                new Vector3(0f, deckHeight, -2.95f),
                2.5f,
                0.32f,
                palette.PlatformSteel);

            BuildWalkableRamp(
                platformRoot.transform,
                "Ramp_North",
                new Vector3(0f, -0.02f, 6.80f),
                new Vector3(0f, deckHeight, 2.95f),
                2.5f,
                0.32f,
                palette.PlatformSteel);
        }

        private static void BuildWalkableRamp(
            Transform parent,
            string rampName,
            Vector3 bottomSurfaceCenter,
            Vector3 topSurfaceCenter,
            float width,
            float thickness,
            Material mat)
        {
            Vector3 span = topSurfaceCenter - bottomSurfaceCenter;
            float length = span.magnitude + 0.15f;
            Quaternion rampRotation = Quaternion.LookRotation(span.normalized, Vector3.up);
            Vector3 surfaceMid = (bottomSurfaceCenter + topSurfaceCenter) * 0.5f;
            Vector3 boxCenter = surfaceMid - (rampRotation * Vector3.up) * (thickness * 0.5f);

            CreatePrimitivePart(
                rampName,
                PrimitiveType.Cube,
                parent,
                boxCenter,
                rampRotation,
                new Vector3(width, thickness, length),
                mat,
                true);
        }

        private static void PlaceCrateCluster(GameObject cratePrefab, Transform parent, string clusterName, Vector3 origin)
        {
            GameObject cluster = new GameObject(clusterName);
            cluster.transform.SetParent(parent, false);
            cluster.transform.localPosition = origin;

            InstantiateArenaPrefab(cratePrefab, cluster.transform, "Crate_BaseA",
                new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, 8f, 0f));
            InstantiateArenaPrefab(cratePrefab, cluster.transform, "Crate_BaseB",
                new Vector3(1.42f, 0f, 0.15f), Quaternion.Euler(0f, -14f, 0f));
            InstantiateArenaPrefab(cratePrefab, cluster.transform, "Crate_Top",
                new Vector3(0.45f, 1.35f, 0.05f), Quaternion.Euler(0f, 22f, 0f));
        }

        private static void EnchantContainerTint(GameObject containerInstance, Material alternateMat)
        {
            if (containerInstance == null || alternateMat == null)
            {
                return;
            }

            Renderer[] renderers = containerInstance.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].gameObject.name == "RoofFrame") continue;
                renderers[i].sharedMaterial = alternateMat;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderers[i]);
            }
        }

        private static SpawnPoint CreateSpawnPoint(
            Transform parent,
            string objectName,
            int index,
            string label,
            Vector3 position,
            Quaternion rotation,
            Color gizmoColor)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            SpawnPoint sp = go.AddComponent<SpawnPoint>();
            sp.Configure(index, label, gizmoColor);
            return sp;
        }

        private static void BakeAndSaveArenaNavMesh(GameObject arenaRoot)
        {
            NavMeshSurface surface = arenaRoot.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            if (surface.navMeshData != null)
            {
                string navAssetPath = NavMeshDir + "/CageDuelArena_NavMesh.asset";
                if (AssetDatabase.LoadAssetAtPath<NavMeshData>(navAssetPath) != null)
                {
                    AssetDatabase.DeleteAsset(navAssetPath);
                }
                AssetDatabase.CreateAsset(surface.navMeshData, navAssetPath);
                EditorUtility.SetDirty(surface);
                Debug.Log("[CageDuel] Baked and saved NavMesh to " + navAssetPath);
            }
            else
            {
                Debug.LogWarning("[CageDuel] NavMesh bake returned null NavMeshData. Check AI Navigation settings.");
            }
        }

        private static void SpawnTargetDummyInstance(
            GameObject dummyPrefab,
            Scene targetScene,
            Transform parent,
            string instanceName,
            Vector3 position,
            Quaternion rotation)
        {
            GameObject dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, targetScene);
            dummy.name = instanceName;
            dummy.transform.SetParent(parent, true);
            dummy.transform.SetPositionAndRotation(position, rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(dummy.transform);
        }

        private static void CreateEventSystem()
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<EventSystemBootstrap>();
#if ENABLE_INPUT_SYSTEM
            var module = esGo.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
#elif ENABLE_LEGACY_INPUT_MANAGER
            esGo.AddComponent<StandaloneInputModule>();
#endif
        }

        private static void BuildHudAndWirePlayer(
            GameObject playerInstance,
            Camera mainCam,
            ThirdPersonCameraRig cameraRig,
            SpawnPoint[] spawnPoints)
        {
            HealthArmor playerVitals = playerInstance.GetComponent<HealthArmor>();
            PlayerInputRouter inputRouter = playerInstance.GetComponent<PlayerInputRouter>();
            ThirdPersonMotor playerMotor = playerInstance.GetComponent<ThirdPersonMotor>();
            HitscanWeapon playerWeapon = playerInstance.GetComponent<HitscanWeapon>();

            Transform visualBody = playerInstance.transform.Find("VisualBody");
            Transform weaponPivot = visualBody != null ? visualBody.Find("WeaponPivot") : null;
            Transform muzzlePoint = weaponPivot != null ? weaponPivot.Find("MuzzlePoint") : null;
            ParticleSystem muzzleParticles = muzzlePoint != null ? muzzlePoint.GetComponentInChildren<ParticleSystem>() : null;
            Transform sparkTransform = playerInstance.transform.Find("ImpactSparkParticles");
            ParticleSystem sparkParticles = sparkTransform != null ? sparkTransform.GetComponent<ParticleSystem>() : null;
            LineRenderer tracer = playerInstance.GetComponentInChildren<LineRenderer>();

            // Create HUD Canvas
            GameObject canvasGo = new GameObject("HUD_Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();

            // 1. Right-half Touch Look Zone (placed first so buttons on top receive clicks first)
            GameObject lookZoneGo = CreateUiElement("TouchLookZone", canvasRect);
            RectTransform lookZoneRect = lookZoneGo.GetComponent<RectTransform>();
            lookZoneRect.anchorMin = new Vector2(0.42f, 0f);
            lookZoneRect.anchorMax = new Vector2(1f, 1f);
            lookZoneRect.offsetMin = Vector2.zero;
            lookZoneRect.offsetMax = Vector2.zero;
            Image lookZoneImg = lookZoneGo.AddComponent<Image>();
            lookZoneImg.color = new Color(0f, 0f, 0f, 0f);
            lookZoneImg.raycastTarget = true;
            TouchLookZone touchLookZone = lookZoneGo.AddComponent<TouchLookZone>();

            // 2. Floating Damage Number Pool Container
            GameObject dmgContainerGo = CreateUiElement("DamageNumbersOverlay", canvasRect);
            StretchFullScreen(dmgContainerGo.GetComponent<RectTransform>());
            DamageNumberPopup damagePopup = dmgContainerGo.AddComponent<DamageNumberPopup>();
            damagePopup.Configure(dmgContainerGo.GetComponent<RectTransform>(), mainCam, playerInstance);

            // 3. Center Crosshair + Hit Marker
            HitMarkerUI hitMarkerUI = BuildCrosshairAndHitMarker(canvasRect);

            // 4. Top-Left Health + Armor Bars
            BuildVitalsPanel(
                canvasRect,
                out RectTransform hpFillRect,
                out TMP_Text hpValueText,
                out RectTransform armorFillRect,
                out TMP_Text armorValueText);

            // 5. Bottom-Center Weapon & Ammo Readout
            BuildWeaponAmmoPanel(
                canvasRect,
                out TMP_Text weaponNameText,
                out TMP_Text ammoValueText,
                out GameObject reloadBarRoot,
                out RectTransform reloadFillRect);

            // 6. Mobile Virtual Joystick (Bottom-Left)
            VirtualJoystick moveJoystick = BuildVirtualJoystick(canvasRect);

            // 7. Mobile Action Buttons (Bottom-Right)
            MobileActionButton fireBtn = BuildMobileButton(
                canvasRect, "Btn_Fire", "FIRE", new Vector2(-225f, 205f), 156f, 24f,
                new Color(0.85f, 0.32f, 0.16f, 0.86f),
                new Color(1.0f, 0.62f, 0.22f, 0.96f));
            MobileActionButton jumpBtn = BuildMobileButton(
                canvasRect, "Btn_Jump", "JUMP", new Vector2(-95f, 345f), 104f, 18f,
                new Color(0.16f, 0.22f, 0.30f, 0.82f),
                new Color(0.35f, 0.72f, 1.0f, 0.94f));
            MobileActionButton crouchBtn = BuildMobileButton(
                canvasRect, "Btn_Crouch", "CROUCH", new Vector2(-95f, 165f), 104f, 16f,
                new Color(0.16f, 0.22f, 0.30f, 0.82f),
                new Color(0.35f, 0.72f, 1.0f, 0.94f));
            MobileActionButton reloadBtn = BuildMobileButton(
                canvasRect, "Btn_Reload", "RELOAD", new Vector2(-385f, 130f), 100f, 16f,
                new Color(0.16f, 0.22f, 0.30f, 0.82f),
                new Color(0.95f, 0.75f, 0.22f, 0.94f));

            // 8. Center Eliminated / Respawn Banner
            BuildDeathBanner(canvasRect, out GameObject deathBannerRoot, out TMP_Text deathBannerText);

            // 9. Top-Right Desktop/Editor Mouse Lock Toggle & Controls Hint
            BuildEditorControlsOverlay(
                canvasRect,
                out Button mouseLockBtn,
                out TMP_Text mouseLockLabel,
                out GameObject desktopHintRoot);

            // Bind HUD Controller
            PlayerHudController hudController = canvasGo.AddComponent<PlayerHudController>();
            hudController.Configure(
                playerVitals,
                playerWeapon,
                inputRouter,
                hitMarkerUI,
                hpFillRect,
                hpValueText,
                armorFillRect,
                armorValueText,
                weaponNameText,
                ammoValueText,
                reloadBarRoot,
                reloadFillRect,
                deathBannerRoot,
                deathBannerText,
                mouseLockBtn,
                mouseLockLabel,
                desktopHintRoot);

            // Wire Scene References onto Player & Camera Rig and record prefab overrides!
            cameraRig.Configure(playerInstance.transform, playerMotor, inputRouter);
            playerVitals.Configure(visualBody, spawnPoints, 100f, 50f, true, 3.0f);
            inputRouter.ConfigureMobileControls(moveJoystick, touchLookZone, fireBtn, jumpBtn, crouchBtn, reloadBtn);
            playerMotor.Configure(inputRouter, cameraRig, playerVitals, visualBody);
            playerWeapon.Configure(
                WeaponStats.CreateDefaultRifle(),
                inputRouter,
                cameraRig,
                playerMotor,
                playerVitals,
                weaponPivot,
                muzzlePoint,
                muzzleParticles,
                sparkParticles,
                tracer);

            PrefabUtility.RecordPrefabInstancePropertyModifications(playerVitals);
            PrefabUtility.RecordPrefabInstancePropertyModifications(inputRouter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerMotor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(playerWeapon);

            EditorUtility.SetDirty(cameraRig);
            EditorUtility.SetDirty(hudController);
            EditorUtility.SetDirty(damagePopup);
            EditorUtility.SetDirty(hitMarkerUI);
        }

        private static HitMarkerUI BuildCrosshairAndHitMarker(RectTransform canvasRect)
        {
            GameObject rootGo = CreateUiElement("CrosshairRoot", canvasRect);
            RectTransform rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(120f, 120f);

            // Center dot
            CreateCrosshairBar(rootRect, "CenterDot", Vector2.zero, new Vector2(4f, 4f), 0f, new Color(1f, 1f, 1f, 0.85f));

            RectTransform top = CreateCrosshairBar(rootRect, "TickTop", new Vector2(0f, 14f), new Vector2(3f, 11f), 0f, Color.white);
            RectTransform bottom = CreateCrosshairBar(rootRect, "TickBottom", new Vector2(0f, -14f), new Vector2(3f, 11f), 0f, Color.white);
            RectTransform left = CreateCrosshairBar(rootRect, "TickLeft", new Vector2(-14f, 0f), new Vector2(11f, 3f), 0f, Color.white);
            RectTransform right = CreateCrosshairBar(rootRect, "TickRight", new Vector2(14f, 0f), new Vector2(11f, 3f), 0f, Color.white);

            // Diagonal hit marker ticks
            GameObject markerGroup = CreateUiElement("HitMarkerGroup", rootRect);
            RectTransform markerRect = markerGroup.GetComponent<RectTransform>();
            markerRect.sizeDelta = new Vector2(64f, 64f);

            var hitImages = new Image[4];
            float[] angles = { 45f, 135f, 225f, 315f };
            for (int i = 0; i < 4; i++)
            {
                float rad = angles[i] * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                RectTransform tick = CreateCrosshairBar(markerRect, "HitTick_" + i, dir * 13f, new Vector2(12f, 3f), angles[i], new Color(1f, 1f, 1f, 0f));
                hitImages[i] = tick.GetComponent<Image>();
            }

            HitMarkerUI ui = rootGo.AddComponent<HitMarkerUI>();
            ui.Configure(top, bottom, left, right, markerRect, hitImages);
            return ui;
        }

        private static RectTransform CreateCrosshairBar(
            RectTransform parent,
            string barName,
            Vector2 anchoredPos,
            Vector2 size,
            float rotZ,
            Color color)
        {
            GameObject go = CreateUiElement(barName, parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);

            Image img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private static void BuildVitalsPanel(
            RectTransform canvasRect,
            out RectTransform hpFillRect,
            out TMP_Text hpValueText,
            out RectTransform armorFillRect,
            out TMP_Text armorValueText)
        {
            GameObject panelGo = CreateUiElement("VitalsPanel", canvasRect);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(36f, -32f);
            panelRect.sizeDelta = new Vector2(360f, 108f);

            Image bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.72f);
            bg.raycastTarget = false;

            BuildSingleStatBar(
                panelRect,
                "ArmorBar",
                new Vector2(16f, -16f),
                new Vector2(328f, 32f),
                new Color(0.20f, 0.76f, 1.0f, 0.95f),
                "ARMOR  50 / 50",
                out armorFillRect,
                out armorValueText);

            BuildSingleStatBar(
                panelRect,
                "HealthBar",
                new Vector2(16f, -58f),
                new Vector2(328f, 34f),
                new Color(0.22f, 0.84f, 0.48f, 0.95f),
                "HP  100 / 100",
                out hpFillRect,
                out hpValueText);
        }

        private static void BuildSingleStatBar(
            RectTransform parent,
            string barName,
            Vector2 topLeftOffset,
            Vector2 barSize,
            Color fillColor,
            string defaultLabel,
            out RectTransform fillRect,
            out TMP_Text valueText)
        {
            GameObject trackGo = CreateUiElement(barName, parent);
            RectTransform trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.anchorMin = new Vector2(0f, 1f);
            trackRect.anchorMax = new Vector2(0f, 1f);
            trackRect.pivot = new Vector2(0f, 1f);
            trackRect.anchoredPosition = topLeftOffset;
            trackRect.sizeDelta = barSize;

            Image trackImg = trackGo.AddComponent<Image>();
            trackImg.color = new Color(0.14f, 0.17f, 0.22f, 0.9f);
            trackImg.raycastTarget = false;

            GameObject fillGo = CreateUiElement("Fill", trackRect);
            fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.raycastTarget = false;

            valueText = CreateTmpLabel(
                trackRect,
                "Label",
                defaultLabel,
                19f,
                TextAlignmentOptions.Left,
                Color.white);
            RectTransform textRect = valueText.GetComponent<RectTransform>();
            StretchFullScreen(textRect);
            textRect.offsetMin = new Vector2(12f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);
        }

        private static void BuildWeaponAmmoPanel(
            RectTransform canvasRect,
            out TMP_Text weaponNameText,
            out TMP_Text ammoValueText,
            out GameObject reloadBarRoot,
            out RectTransform reloadFillRect)
        {
            GameObject panelGo = CreateUiElement("WeaponAmmoPanel", canvasRect);
            RectTransform panelRect = panelGo.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 28f);
            panelRect.sizeDelta = new Vector2(280f, 82f);

            Image bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.72f);
            bg.raycastTarget = false;

            weaponNameText = CreateTmpLabel(
                panelRect,
                "WeaponName",
                "VANGUARD AR",
                16f,
                TextAlignmentOptions.Center,
                new Color(0.70f, 0.82f, 0.95f, 1f));
            RectTransform nameRect = weaponNameText.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -6f);
            nameRect.sizeDelta = new Vector2(0f, 24f);

            ammoValueText = CreateTmpLabel(
                panelRect,
                "AmmoCount",
                "30 / 120",
                30f,
                TextAlignmentOptions.Center,
                Color.white);
            RectTransform ammoRect = ammoValueText.GetComponent<RectTransform>();
            ammoRect.anchorMin = new Vector2(0f, 0f);
            ammoRect.anchorMax = new Vector2(1f, 0f);
            ammoRect.pivot = new Vector2(0.5f, 0f);
            ammoRect.anchoredPosition = new Vector2(0f, 8f);
            ammoRect.sizeDelta = new Vector2(0f, 44f);

            // Reload progress bar right above ammo box
            reloadBarRoot = CreateUiElement("ReloadProgressBar", panelRect);
            RectTransform reloadTrackRect = reloadBarRoot.GetComponent<RectTransform>();
            reloadTrackRect.anchorMin = new Vector2(0f, 1f);
            reloadTrackRect.anchorMax = new Vector2(1f, 1f);
            reloadTrackRect.pivot = new Vector2(0.5f, 0f);
            reloadTrackRect.anchoredPosition = new Vector2(0f, 6f);
            reloadTrackRect.sizeDelta = new Vector2(0f, 10f);

            Image reloadTrackImg = reloadBarRoot.AddComponent<Image>();
            reloadTrackImg.color = new Color(0.10f, 0.12f, 0.16f, 0.85f);
            reloadTrackImg.raycastTarget = false;

            GameObject reloadFillGo = CreateUiElement("Fill", reloadTrackRect);
            reloadFillRect = reloadFillGo.GetComponent<RectTransform>();
            reloadFillRect.anchorMin = Vector2.zero;
            reloadFillRect.anchorMax = new Vector2(0f, 1f);
            reloadFillRect.offsetMin = Vector2.zero;
            reloadFillRect.offsetMax = Vector2.zero;

            Image reloadFillImg = reloadFillGo.AddComponent<Image>();
            reloadFillImg.color = new Color(1.0f, 0.76f, 0.20f, 0.95f);
            reloadFillImg.raycastTarget = false;
            reloadBarRoot.SetActive(false);
        }

        private static VirtualJoystick BuildVirtualJoystick(RectTransform canvasRect)
        {
            GameObject stickGo = CreateUiElement("VirtualJoystick", canvasRect);
            RectTransform stickRect = stickGo.GetComponent<RectTransform>();
            stickRect.anchorMin = new Vector2(0f, 0f);
            stickRect.anchorMax = new Vector2(0f, 0f);
            stickRect.pivot = new Vector2(0.5f, 0.5f);
            stickRect.anchoredPosition = new Vector2(210f, 200f);
            stickRect.sizeDelta = new Vector2(210f, 210f);

            Image bgImg = stickGo.AddComponent<Image>();
            bgImg.color = new Color(0.85f, 0.92f, 1.0f, 0.36f);
            bgImg.raycastTarget = true;

            GameObject knobGo = CreateUiElement("Knob", stickRect);
            RectTransform knobRect = knobGo.GetComponent<RectTransform>();
            knobRect.sizeDelta = new Vector2(86f, 86f);
            knobRect.anchoredPosition = Vector2.zero;

            Image knobImg = knobGo.AddComponent<Image>();
            knobImg.color = new Color(0.92f, 0.96f, 1.0f, 0.78f);
            knobImg.raycastTarget = false;

            VirtualJoystick joystick = stickGo.AddComponent<VirtualJoystick>();
            joystick.Configure(stickRect, knobRect, bgImg, knobImg, 68f);
            return joystick;
        }

        private static MobileActionButton BuildMobileButton(
            RectTransform canvasRect,
            string objectName,
            string caption,
            Vector2 bottomRightOffset,
            float diameter,
            float fontSize,
            Color normalColor,
            Color pressedColor)
        {
            GameObject btnGo = CreateUiElement(objectName, canvasRect);
            RectTransform btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0f);
            btnRect.anchorMax = new Vector2(1f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = bottomRightOffset;
            btnRect.sizeDelta = new Vector2(diameter, diameter);

            Image img = btnGo.AddComponent<Image>();
            img.color = normalColor;
            img.raycastTarget = true;

            TMP_Text label = CreateTmpLabel(btnRect, "Label", caption, fontSize, TextAlignmentOptions.Center, Color.white);
            StretchFullScreen(label.GetComponent<RectTransform>());

            MobileActionButton actionBtn = btnGo.AddComponent<MobileActionButton>();
            actionBtn.Configure(img, normalColor, pressedColor, new Color(0.20f, 0.76f, 0.95f, 0.92f));
            return actionBtn;
        }

        private static void BuildDeathBanner(
            RectTransform canvasRect,
            out GameObject deathBannerRoot,
            out TMP_Text deathBannerText)
        {
            deathBannerRoot = CreateUiElement("DeathBanner", canvasRect);
            RectTransform bannerRect = deathBannerRoot.GetComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.5f, 0.5f);
            bannerRect.anchorMax = new Vector2(0.5f, 0.5f);
            bannerRect.sizeDelta = new Vector2(540f, 128f);
            bannerRect.anchoredPosition = new Vector2(0f, 120f);

            Image bg = deathBannerRoot.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.04f, 0.05f, 0.86f);
            bg.raycastTarget = false;

            deathBannerText = CreateTmpLabel(
                bannerRect,
                "BannerLabel",
                "ELIMINATED",
                36f,
                TextAlignmentOptions.Center,
                new Color(1.0f, 0.36f, 0.36f, 1f));
            StretchFullScreen(deathBannerText.GetComponent<RectTransform>());
            deathBannerRoot.SetActive(false);
        }

        private static void BuildEditorControlsOverlay(
            RectTransform canvasRect,
            out Button mouseLockButton,
            out TMP_Text mouseLockLabel,
            out GameObject desktopHintRoot)
        {
            GameObject btnGo = CreateUiElement("MouseLockToggleButton", canvasRect);
            RectTransform btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 1f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.pivot = new Vector2(1f, 1f);
            btnRect.anchoredPosition = new Vector2(-28f, -24f);
            btnRect.sizeDelta = new Vector2(380f, 44f);

            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.17f, 0.24f, 0.86f);
            mouseLockButton = btnGo.AddComponent<Button>();
            mouseLockButton.targetGraphic = btnImg;

            mouseLockLabel = CreateTmpLabel(
                btnRect,
                "Label",
                "[TAB] Mouse Aim: OFF (Touch UI Mode)",
                17f,
                TextAlignmentOptions.Center,
                new Color(0.92f, 0.96f, 1.0f, 1f));
            StretchFullScreen(mouseLockLabel.GetComponent<RectTransform>());

            desktopHintRoot = CreateUiElement("DesktopControlsHint", canvasRect);
            RectTransform hintRect = desktopHintRoot.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(1f, 1f);
            hintRect.anchorMax = new Vector2(1f, 1f);
            hintRect.pivot = new Vector2(1f, 1f);
            hintRect.anchoredPosition = new Vector2(-28f, -74f);
            hintRect.sizeDelta = new Vector2(520f, 52f);

            TMP_Text hintLabel = CreateTmpLabel(
                hintRect,
                "HintText",
                "PC: WASD Move | [Tab] Mouse Aim | LMB/F Fire | Space Jump | C Crouch | R Reload | K Self-Hit",
                14f,
                TextAlignmentOptions.Right,
                new Color(0.82f, 0.88f, 0.96f, 0.80f));
            hintLabel.enableWordWrapping = true;
            StretchFullScreen(hintLabel.GetComponent<RectTransform>());
        }

        private static GameObject CreateUiElement(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TMP_Text CreateTmpLabel(
            RectTransform parent,
            string name,
            string text,
            float fontSize,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject go = CreateUiElement(name, parent);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Bold;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            return label;
        }

        private static void StretchFullScreen(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreatePrimitivePart(
            string partName,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Quaternion localRot,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localRotation = localRot;
            part.transform.localScale = localScale;

            if (!keepCollider)
            {
                Collider col = part.GetComponent<Collider>();
                if (col != null)
                {
                    Object.DestroyImmediate(col);
                }
            }

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return part;
        }

        private static GameObject InstantiateArenaPrefab(
            GameObject prefabAsset,
            Transform parent,
            string instanceName,
            Vector3 localPos,
            Quaternion localRot)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPos;
            instance.transform.localRotation = localRot;
            return instance;
        }

        private static GameObject SaveAsPrefabAndDestroyTemp(GameObject tempRoot, string assetPath)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(tempRoot, assetPath);
            Object.DestroyImmediate(tempRoot);
            return saved;
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool alreadyPresent = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i].enabled = true;
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
