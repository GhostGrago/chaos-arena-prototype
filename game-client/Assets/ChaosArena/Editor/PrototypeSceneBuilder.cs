using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

        [MenuItem("Tools/Chaos Arena/Build Windows Prototype")]
        public static void BuildWindows()
        {
            Build();
            const string buildDirectory = "Builds/Prototype01";
            const string executablePath = buildDirectory + "/ChaosArenaPrototype.exe";
            Directory.CreateDirectory(buildDirectory);

            BuildPlayerOptions options = new()
            {
                scenes = new[] { ScenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
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
