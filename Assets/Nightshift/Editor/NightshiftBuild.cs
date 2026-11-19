using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Nightshift.EditorTools
{
    public static class NightshiftBuild
    {
        const string Scene = "Assets/Scenes/Nightshift.unity";

        [InitializeOnLoadMethod]
        static void PrepareProject()
        {
            EditorApplication.delayCall += EnsureRuntimeShaders;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
        }

        static void EnsureRuntimeShaders()
        {
            var graphics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (graphics.Length == 0) return;
            var settings = new SerializedObject(graphics[0]);
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            if (list == null) return;
            foreach (string name in new[] { "Standard", "Skybox/Panoramic", "Particles/Standard Unlit" })
            {
                var shader = Shader.Find(name);
                if (shader == null) continue;
                bool included = false;
                for (int i = 0; i < list.arraySize; i++)
                    included |= list.GetArrayElementAtIndex(i).objectReferenceValue == shader;
                if (included) continue;
                int index = list.arraySize++;
                list.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Nightshift/Open Game Scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(Scene);
        }

        [MenuItem("Nightshift/Build Windows (64-bit)")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/Nightshift.exe");

        [MenuItem("Nightshift/Build Linux (64-bit)")]
        public static void Linux() => Build(BuildTarget.StandaloneLinux64, "Builds/Linux/Nightshift.x86_64");

        [MenuItem("Nightshift/Build WebGL")]
        public static void Web() => Build(BuildTarget.WebGL, "Builds/WebGL");

        static void Build(BuildTarget target, string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Install the matching Build Support module in Unity Hub.");
            EnsureRuntimeShaders();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new[] { Scene }, path, target, BuildOptions.None);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Nightshift build failed: {report.summary.result}");
            Debug.Log($"Nightshift build complete: {report.summary.outputPath}");
        }
    }
}
