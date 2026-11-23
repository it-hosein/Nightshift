using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;

namespace Nightshift.EditorTools
{
    public static class NightshiftValidation
    {
        const string ScenePath = "Assets/Scenes/Nightshift.unity";
        const string AssetsPath = "Assets/Nightshift/Assets";

        [MenuItem("Nightshift/Validate Scene and Assets")]
        public static void Validate()
        {
            var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                var roots = scene.GetRootGameObjects();
                var game = roots.Select(r => r.GetComponent<GameManager>()).SingleOrDefault(g => g != null);
                Require(game != null, "The scene needs a GameManager.");
                var config = new SerializedObject(game);
                foreach (string field in new[]
                    { "art", "chase", "playerRig", "controls", "engine", "music", "soundEffects",
                      "collisionClip", "coinClip", "fuelClip", "heartClip" })
                {
                    Require(config.FindProperty(field).objectReferenceValue != null, $"GameManager is missing {field}.");
                }

                var rig = (Transform)config.FindProperty("playerRig").objectReferenceValue;
                Require(PrefabUtility.IsPartOfPrefabInstance(rig) && rig.Find("Suspension") != null,
                    "The player needs the PlayerRig prefab instance and its Suspension mount.");
                var camera = (Camera)config.FindProperty("chase").objectReferenceValue;
                Require(camera.CompareTag("MainCamera") && camera.GetComponent<AudioListener>() != null
                    && camera.GetComponent<PostProcessLayer>() != null, "The chase camera is missing a required component.");
                var volume = roots.SelectMany(r => r.GetComponentsInChildren<PostProcessVolume>()).SingleOrDefault();
                Require(volume != null && volume.sharedProfile != null && volume.sharedProfile.settings.Count == 3
                    && volume.sharedProfile.settings.All(s => s != null), "The night grading profile is incomplete.");
                foreach (string field in new[] { "engine", "music" })
                {
                    var source = (AudioSource)config.FindProperty(field).objectReferenceValue;
                    Require(source.clip != null && source.loop, $"The {field} source needs a looping AudioClip.");
                }

                var input = (InputActionAsset)config.FindProperty("controls").objectReferenceValue;
                foreach (string action in new[]
                    { "Driving/LaneLeft", "Driving/LaneRight", "Driving/Nitro", "Driving/Brake", "Menu/Pause", "Menu/Mute" })
                    Require(input.FindAction(action, true).bindings.Count > 0, $"There are no bindings for {action}.");

                var catalog = (NightMaterials)config.FindProperty("art").objectReferenceValue;
                Require(catalog.Paints.Length == 6 && catalog.Paints.All(paint => paint != null),
                    "Assign six vehicle paint materials in the NightMaterials asset.");
                foreach (var item in new[]
                    { catalog.TrafficRig, catalog.HazardLights, catalog.Coin, catalog.Fuel, catalog.Heart,
                      catalog.StreetLamp, catalog.TunnelLight, catalog.RouteSign, catalog.BridgeSign })
                    Require(item != null && PrefabUtility.IsPartOfPrefabAsset(item), "A catalog prefab is missing or invalid.");

                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { AssetsPath + "/Prefabs" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var filters = prefab.GetComponentsInChildren<MeshFilter>(true);
                    Require(filters.Length > 0 && filters.All(f => f.sharedMesh != null),
                        $"A mesh is missing from {path}.");
                    Require(prefab.GetComponentsInChildren<MeshRenderer>(true).All(r => r.sharedMaterial != null),
                        $"A material is missing from {path}.");
                }

                foreach (string model in new[] { "Car", "Porsche", "Lamborghini", "Bugatti", "BMW" })
                    Require(Resources.Load<GameObject>("Nightshift/" + model) != null, $"Missing vehicle model {model}.");

                Debug.Log("Nightshift scene and asset references validated.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
