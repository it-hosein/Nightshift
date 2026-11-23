using UnityEngine;
using UnityEngine.Rendering;

namespace Nightshift
{
    public sealed class NightParticles
    {
        readonly ParticleSystem particles, smoke;
        readonly Transform exhaustMount;
        readonly Transform[] jets = new Transform[3];
        readonly Material flame;
        float smokeTimer;
        public NightParticles(Transform parent, NightMaterials materials, Transform exhaustMount)
        {
            this.exhaustMount = exhaustMount;
            particles = MakeSystem(parent, "Pooled VFX particles", materials.Fire, 700);
            var smokeMaterial = new Material(materials.Fire);
            smokeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            smoke = MakeSystem(parent, "Exhaust smoke", smokeMaterial, 55);
            var growth = smoke.sizeOverLifetime; growth.enabled = true;
            growth.size = new ParticleSystem.MinMaxCurve(2.8f, AnimationCurve.EaseInOut(0, .36f, 1, 1));

            flame = new Material(Shader.Find("Nightshift/Exhaust Flame"));
            for (int i = 0; i < jets.Length; i++)
            {
                var jet = new GameObject("Exhaust flame").transform;
                jet.SetParent(exhaustMount, false);
                jet.localPosition = new Vector3((i - 1) * .109f, i == 1 ? .426f : .42f, i == 1 ? -2.28f : -2.21f);
                jet.localRotation = Quaternion.Euler(-90, 0, 0);
                for (int side = 0; side < 2; side++)
                {
                    var sheet = NightMaterials.Primitive(jet, PrimitiveType.Quad, "Flame sheet", Vector3.up * .5f, Vector3.one, flame);
                    sheet.localRotation = Quaternion.Euler(0, side * 90, 0);
                    var renderer = sheet.GetComponent<MeshRenderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                jet.gameObject.SetActive(false);
                jets[i] = jet;
            }
        }

        static ParticleSystem MakeSystem(Transform parent, string name, Material material, int capacity)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent,false);
            var system = obj.AddComponent<ParticleSystem>();
            system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.playOnAwake = false; main.loop = true; main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var color = system.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(.95f,0),new GradientAlphaKey(.76f,.18f),new GradientAlphaKey(0,1) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            system.Play();
            return system;
        }

        public void SetPaused(bool paused)
        {
            if (paused) { particles.Pause(); smoke.Pause(); HideJets(); }
            else { particles.Play(); smoke.Play(); }
        }

        void HideJets()
        {
            foreach (var jet in jets) jet.gameObject.SetActive(false);
        }

        public void Clear()
        {
            particles.Clear(); smoke.Clear(); smokeTimer = 0; HideJets();
        }

        void Burst(Vector3 at, Color color, int count, float speed, float lifetime, float size, Vector3 drift)
        {
            for (int i = 0; i < count; i++)
            {
                var item = new ParticleSystem.EmitParams
                {
                    position = at + UnityEngine.Random.insideUnitSphere * .08f,
                    velocity = UnityEngine.Random.insideUnitSphere * speed + drift,
                    startColor = color,
                    startLifetime = lifetime * UnityEngine.Random.Range(.58f,1f),
                    startSize = size * UnityEngine.Random.Range(.65f,1.45f)
                };
                particles.Emit(item,1);
            }
        }

        public void Exhaust(float dt, float time, float speed, bool boosting)
        {
            flame.SetFloat("_Clock", time);
            for (int i = 0; i < jets.Length; i++)
            {
                var jet = jets[i];
                jet.gameObject.SetActive(boosting);
                float flicker = .5f + .5f * Mathf.Sin(time * 50 + i * 1.8f) * Mathf.Cos(time * 29 + i * .7f);
                jet.localScale = new Vector3(.18f + flicker * .06f, .69f + flicker * .77f, .18f + flicker * .06f);
            }

            smokeTimer -= dt;
            if (smokeTimer > 0) return;
            smokeTimer = boosting ? .09f : .057f;
            foreach (var jet in jets)
            {
                var puff = new ParticleSystem.EmitParams
                {
                    position = jet.position,
                    velocity = -exhaustMount.forward * (2.6f + speed * .28f) + Vector3.up * .46f
                        + exhaustMount.right * UnityEngine.Random.Range(-.31f,.31f),
                    startColor = new Color(.62f,.69f,.77f,boosting ? .31f : .46f),
                    startLifetime = UnityEngine.Random.Range(.34f,.57f),
                    startSize = .47f,
                    rotation = UnityEngine.Random.Range(0,360)
                };
                smoke.Emit(puff,1);
            }
        }

        public void Crash(Vector3 at) => Burst(at,new Color(1,.48f,.13f),58,13,1.17f,.16f,new Vector3(0,1.3f,-6));
        public void Pickup(Vector3 at, PickupKind kind)
        {
            Color tint = kind == PickupKind.Heart ? new Color(.84f,.46f,.50f)
                : kind == PickupKind.Fuel ? new Color(.62f,.70f,.49f) : new Color(1,.72f,.19f);
            Burst(at,tint,31,4.8f,.68f,.30f,new Vector3(0,1.8f,-5));
        }
    }

    public static class Transmission
    {
        static readonly int[] UpperSpeeds = { 55, 100, 155, 215, 270, 335 };
        public static int GearIndex(float kmh)
        {
            int index = 0;
            while (index < UpperSpeeds.Length - 1 && kmh >= UpperSpeeds[index]) index++;
            return index;
        }
        public static float Revolutions(float kmh, int gear)
        {
            int lower = gear == 0 ? 0 : UpperSpeeds[gear - 1];
            return Mathf.Lerp(gear == 0 ? 1100 : 5000, 8500, Mathf.InverseLerp(lower, UpperSpeeds[gear], kmh));
        }
    }

    public sealed class NightAudio
    {
        readonly AudioSource engine, music, effects;
        readonly AudioClip crash, coin, fuel, heart;
        bool muted;

        public NightAudio(AudioSource engine, AudioSource music, AudioSource effects,
            AudioClip crash, AudioClip coin, AudioClip fuel, AudioClip heart)
        {
            this.engine = engine;
            this.music = music;
            this.effects = effects;
            this.crash = crash;
            this.coin = coin;
            this.fuel = fuel;
            this.heart = heart;
            engine.Play();
            music.Play();
        }

        public void Tick(float speed,bool active,bool throttle)
        {
            if (muted) return;
            float km=speed*3.6f;
            float rpm = Transmission.Revolutions(km, Transmission.GearIndex(km));
            engine.pitch = Mathf.Lerp(engine.pitch, .56f + rpm / 5700, .12f);
            // Retain the relative levels from the previous driving mix, with nitro at unity.
            float volume = active ? (throttle ? 1f : .42f / .66f) : .014f / .66f;
            engine.volume = Mathf.Lerp(engine.volume, volume, .12f);
        }
        public void SetMute(bool value)
        {
            muted=value; music.mute=engine.mute=effects.mute=value;
        }
        public void Crash() { if(!muted) effects.PlayOneShot(crash); }
        public void Pickup(PickupKind kind) { if (!muted) effects.PlayOneShot(kind == PickupKind.Heart ? heart : kind == PickupKind.Fuel ? fuel : coin); }
    }
}
