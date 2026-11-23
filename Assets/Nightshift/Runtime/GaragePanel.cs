using UnityEngine;
using UnityEngine.UIElements;

namespace Nightshift
{
    sealed class GaragePanel
    {
        readonly GameManager game;
        readonly PanelSettings settings;
        readonly UIDocument document;
        readonly string[] statNames = { "Speed", "Maneuver", "Nitro", "Brake" };
        readonly VisualTreeAsset layout;

        public GaragePanel(GameManager game)
        {
            this.game = game;
            layout = Resources.Load<VisualTreeAsset>("Nightshift/Garage/Garage");
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("Nightshift/Garage/GarageTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.match = .37f;
            settings.sortingOrder = 20;
            var root = new GameObject("Nightshift Garage UI");
            root.SetActive(false);
            root.transform.SetParent(game.transform, false);
            document = root.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = layout;
        }

        public void Refresh(bool show)
        {
            bool wasOpen = document.gameObject.activeSelf;
            if (show != wasOpen) document.gameObject.SetActive(show);
            if (!show) return;
            var root = document.rootVisualElement;
            if (!wasOpen)
            {
                var garage = root.Q<VisualElement>("garage");
                garage.styleSheets.Add(Resources.Load<StyleSheet>("Nightshift/Garage/GarageStyle"));
                var choices = root.Q<VisualElement>("vehicles");
                choices.Clear();
                for (int i = 0; i < game.Vehicles.Count; i++)
                {
                    int selected = i;
                    var spec = game.Vehicles[i];
                    var button = new Button(() => game.SelectVehicle(selected)) { name = "vehicle-" + i, text = spec.maker };
                    button.tooltip = spec.DisplayName;
                    button.AddToClassList("car-choice");
                    choices.Add(button);
                }
                var back = root.Q<Button>("done");
                back.clicked -= game.CloseGarage;
                back.clicked += game.CloseGarage;
                root.Q<Button>("vehicle-" + game.VehicleIndex).Focus();
            }

            var vehicle = game.Vehicle;
            root.Q<Label>("maker").text = vehicle.maker.ToUpperInvariant();
            root.Q<Label>("model").text = vehicle.model;
            root.Q<Label>("description").text = vehicle.description;
            for (int i = 0; i < game.Vehicles.Count; i++)
                root.Q<Button>("vehicle-" + i).EnableInClassList("chosen", i == game.VehicleIndex);

            var ratings = vehicle.Ratings();
            string[] stats = { $"{Mathf.RoundToInt(vehicle.TopKmh)} km/h", $"{vehicle.handling:0}/90",
                $"{vehicle.NitroSeconds:0.0} s", $"{vehicle.braking:0.0}/2.5" };
            for (int i = 0; i < statNames.Length; i++)
            {
                string name = statNames[i];
                root.Q<VisualElement>(name + "-fill").style.width = Length.Percent(ratings[i] * 100);
                root.Q<Label>(name + "-value").text = stats[i];
            }
        }

        public void Dispose() => Object.Destroy(settings);
    }
}
