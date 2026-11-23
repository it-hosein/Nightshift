using System;
using UnityEngine;

namespace Nightshift
{
    public enum RunMode { Endless, Duel, TimeTrial }

    [CreateAssetMenu(menuName = "Nightshift/Driving Expansion")]
    public sealed class DrivingExpansion : ScriptableObject
    {
        public VehicleSpec[] vehicles;
        public RivalSpec[] rivals;
        public Material ghostMaterial;
        public AudioClip passBy;
        public UnityEngine.Audio.AudioMixer audioMixer;
        public GameObject replayShots;
        [Min(15)] public float duelDuration = 90;
        [Min(500)] public float trialDistance = 2400;
        [Min(0)] public float draftCharge = 13;
        [Min(1)] public float comboWindow = 5;
        [Min(0)] public float boostFov = 5;
    }

    [Serializable]
    public sealed class RivalSpec
    {
        public string name;
        public VehicleSpec vehicle;
        public float pace;
        public float foresight;
        public float reactionTime;
        public float clearance;
    }
}
