using UnityEngine;

namespace Nightshift
{
    [CreateAssetMenu(menuName = "Nightshift/Vehicle")]
    public sealed class VehicleSpec : ScriptableObject
    {
        public string id;
        public string maker;
        public string model;
        [TextArea] public string description;
        public string resourceName;
        [Range(0, 5)] public int paint;
        [Range(75, 105)] public float topSpeed = 88;
        [Range(0.3f, 0.65f)] public float acceleration = .43f;
        [Range(48, 90)] public float handling = 64;
        [Range(15, 25)] public float nitroUse = 20;
        [Range(1, 2.5f)] public float braking = 1.48f;
        public float NitroSeconds => 100f / nitroUse;
        public string DisplayName => maker + " " + model;

        public float[] Ratings() => new[]
        {
            Rated(TopKmh, 270, 365),
            Rated(handling, 48, 90),
            Rated(NitroSeconds, 4, 6.67f),
            Rated(braking, 1, 2.5f)
        };

        public float TopKmh => topSpeed * 3.6f;

        static float Rated(float value, float low, float high) =>
            Mathf.Lerp(.38f, 1, Mathf.InverseLerp(low, high, value));
    }
}
