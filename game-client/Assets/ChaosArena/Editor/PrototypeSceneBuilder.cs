using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace ChaosArena.Editor
{
    public static class PrototypeSceneBuilder
    {
        private const string SceneDirectory = "Assets/Scenes";
        private const string ScenePath = SceneDirectory + "/Prototype.unity";
        private const string SettingsDirectory = "Assets/Settings";
        private const string PipelinePath = SettingsDirectory + "/PrototypeURP.asset";
        private const string MaterialDirectory = "Assets/Resources/ChaosArenaMaterials";
        private const string LitMaterialPath = MaterialDirectory + "/PrototypeLit.mat";
        private const string UnlitMaterialPath = MaterialDirectory + "/PrototypeUnlit.mat";

        [MenuItem("Tools/Chaos Arena/Rebuild Prototype Scene")]
        public static void Build()
        {
            Directory.CreateDirectory(SceneDirectory);
            Directory.CreateDirectory(SettingsDirectory);
            Directory.CreateDirectory(MaterialDirectory);

            ConfigureRendering();
            ConfigureMaterials();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Prototype Bootstrap").AddComponent<PrototypeBootstrap>();
            BuildNetworking();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "Independent Prototype";
            PlayerSettings.productName = "Chaos Arena Prototype";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prototype scene created at {ScenePath}");
        }

        /// <summary>
        /// Netcode needs a NetworkManager and a spawnable NetworkObject in the scene. The arena and fighters
        /// are assembled at runtime and so cannot be network prefabs, which is why replication is funnelled
        /// through one scene-placed NetMatch object instead of one NetworkObject per fighter.
        /// </summary>
        private static void BuildNetworking()
        {
            GameObject managerObject = new("Network Manager");
            NetworkManager manager = managerObject.AddComponent<NetworkManager>();
            UnityTransport transport = managerObject.AddComponent<UnityTransport>();
            managerObject.AddComponent<NetworkSession>();

            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.ConnectionApproval = false;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.PlayerPrefab = null;

            GameObject matchObject = new("Net Match");
            matchObject.AddComponent<NetworkObject>();
            matchObject.AddComponent<NetMatch>();
        }

        [MenuItem("Tools/Chaos Arena/Build Windows Prototype")]
        public static void BuildWindows() => BuildWindowsPlayer(true);

        /// <summary>
        /// Non-development build for sharing with playtesters: smaller, faster, and without the
        /// BurstDebugInformation_DoNotShip folder. Goes to its own directory so the development build
        /// used by the smoke test is never overwritten.
        /// </summary>
        [MenuItem("Tools/Chaos Arena/Build Windows Release")]
        public static void BuildWindowsRelease() => BuildWindowsPlayer(false);

        private static void BuildWindowsPlayer(bool development)
        {
            Build();
            string buildDirectory = development ? "Builds/Prototype01" : "Builds/Prototype01-Release";
            string executablePath = buildDirectory + "/ChaosArenaPrototype.exe";
            Directory.CreateDirectory(buildDirectory);

            BuildPlayerOptions options = new()
            {
                scenes = new[] { ScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.InvalidOperationException($"Windows prototype build failed: {report.summary.result}");
            }

            Debug.Log($"Windows prototype built at {executablePath} ({report.summary.totalSize} bytes)");
        }

        private static void ConfigureRendering()
        {
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                pipeline.LoadBuiltinRendererData();
                EditorUtility.SetDirty(pipeline);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void ConfigureMaterials()
        {
            EnsureMaterial(LitMaterialPath, "Universal Render Pipeline/Lit");
            EnsureMaterial(UnlitMaterialPath, "Universal Render Pipeline/Unlit");
        }

        private static void EnsureMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException($"Required URP shader was not found: {shaderName}");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
        }
    }
}
