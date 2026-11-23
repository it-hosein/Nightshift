using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UIElements;

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

                ValidateExpansion();

                Debug.Log("Nightshift scene and asset references validated.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void ValidateExpansion()
        {
            var expansion = Resources.Load<DrivingExpansion>("Nightshift/DrivingExpansion");
            Require(expansion != null, "The driving expansion catalog could not be imported.");
            var cars = expansion.vehicles;
            Require(cars != null && cars.Length >= 6 && cars.All(c => c != null)
                && cars.Select(c => c.id).Distinct().Count() == cars.Length, "Assign six distinct vehicle profiles.");
            foreach (var car in cars)
            {
                var model = Resources.Load<GameObject>("Nightshift/" + car.resourceName);
                Require(model != null && model.GetComponentsInChildren<Renderer>(true).Length > 0,
                    $"No imported renderers for {car.DisplayName}.");
                Require(car.nitroUse > 0 && car.handling > 0 && car.braking > 0 && car.topSpeed > 70,
                    $"Invalid driving parameters for {car.DisplayName}.");
            }
            Require(expansion.rivals != null && expansion.rivals.Length >= 2
                && expansion.rivals.All(r => r != null && r.vehicle != null && r.reactionTime > 0 && r.foresight > r.clearance),
                "Assign a usable profile for each duel rival.");
            var audio = expansion.audioMixer;
            Require(audio != null && expansion.passBy != null && audio.FindSnapshot("Driving") != null
                && audio.FindSnapshot("Replay") != null, "The driving audio mixer or pass clip is missing.");
            foreach (string bus in new[] { "Engine", "Music", "Effects" })
                Require(audio.FindMatchingGroups(bus).Any(g => g.name == bus), $"Missing audio mixer bus: {bus}.");
            Require(expansion.ghostMaterial != null && expansion.ghostMaterial.shader != null
                && !ShaderUtil.ShaderHasError(expansion.ghostMaterial.shader), "The ghost shader failed to import.");
            Require(expansion.replayShots != null && expansion.replayShots.transform.Find("Wheel") != null
                && expansion.replayShots.transform.Find("Roadside") != null, "Missing replay camera anchors.");

            const string garage = "Nightshift/Garage/";
            var layout = Resources.Load<VisualTreeAsset>(garage + "Garage");
            Require(layout != null && Resources.Load<StyleSheet>(garage + "GarageStyle") != null
                && Resources.Load<ThemeStyleSheet>(garage + "GarageTheme") != null, "The Garage UI Toolkit assets are missing.");
            var tree = layout.CloneTree();
            Require(tree.Q<Button>("done") != null && tree.Q<VisualElement>("vehicles") != null,
                "The garage layout is missing its buttons.");
            foreach (string stat in new[] { "Speed", "Maneuver", "Nitro", "Brake" })
                Require(tree.Q<VisualElement>(stat + "-fill") != null && tree.Q<Label>(stat + "-value") != null,
                    $"The garage layout is missing the {stat} stat bar.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
