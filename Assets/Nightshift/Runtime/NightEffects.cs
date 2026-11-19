using System;
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
        readonly Color[] colors = { new Color(.30f,.85f,1),new Color(1,.15f,.28f),new Color(1,.66f,.23f),new Color(.55f,.33f,1) };

        public NightParticles(Transform parent, NightMaterials materials, Transform exhaustMount)
        {
            this.exhaustMount = exhaustMount;
            particles = MakeSystem(parent, "Pooled VFX particles", materials.Fire, 700);
            var smokeMaterial = new Material(materials.Fire);
            smokeMaterial.SetFloat("_Mode", 2);
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
        public void Pickup(Vector3 at,bool fuel) =>
            Burst(at,fuel ? new Color(.51f,1,.25f) : new Color(1,.72f,.19f),31,4.8f,.68f,.30f,new Vector3(0,1.8f,-5));
        public void Fireworks(Vector3 at,int seed)
        {
            Burst(at,colors[seed%colors.Length],115,13,2.5f,.44f,new Vector3(0,-1.1f,-13));
            Burst(at+new Vector3(8,5,6),colors[(seed+1)%colors.Length],65,10.5f,2.2f,.42f,new Vector3(0,-1.25f,-13));
        }
    }

    public sealed class NightAudio
    {
        const int Rate = 24000;
        readonly AudioSource engine, music, effects;
        readonly AudioClip crash, coin, fuel;
        bool muted;
        delegate double Sample(double t, int index);

        public NightAudio(Transform parent)
        {
            engine = Source(parent,"Engine loop",true);
            music = Source(parent,"Nightshift soundtrack",true);
            effects = Source(parent,"Driving sound effects",false);
            engine.clip = Make("Combustion",1.0,(t, i) => (Math.Sin(t*Tau*38) + .42*Math.Sin(t*Tau*76)
                + .25*Math.Sin(t*Tau*114) + .14*Math.Sin(t*Tau*190)) * (.45+.22*Math.Sin(t*Tau*19)) * .43);
            const double beatDuration = .254;
            double[] bass = { 55,55,65.406,48.999 };
            double[] note = { 4,6,8,5,6,4,8,5 };
            music.clip = Make("Nightshift original synth loop",beatDuration*32,(t, i) =>
            {
                int beat = Math.Min(31,(int)(t/beatDuration));
                double q = t-beat*beatDuration;
                double kick = Beat(beat%4 < 2 ? q+(beat%4)*beatDuration : q+(beat%4-2)*beatDuration, .44, 51, 9) * .37;
                double arpT = q+((beat+1)%2)*beatDuration;
                double a = bass[beat/8]*note[beat%8];
                double arp = Math.Sin(arpT*Tau*a) * Fade(arpT,.26) * .12;
                double leadT = q+(beat%4)*beatDuration;
                double lead = Math.Sin(leadT*Tau*bass[beat/8]) * Fade(leadT,.76) * .25;
                return kick+arp+lead;
            });
            var random = new System.Random(117);
            crash = Make("Impact",.66,(t, i) => ((random.NextDouble()*2-1)*.42+Math.Sin(Tau*(88*t-55*t*t))*.49)*Math.Exp(-t*10));
            coin = Make("Coin",.32,(t,i) => Math.Sin(Tau*740*t)*Fade(t,.22)*.28 + Math.Sin(Tau*1109*t)*Fade(t-.065,.24)*.33);
            fuel = Make("Refuel",.43,(t,i) => Math.Sin(Tau*392*t)*Fade(t,.21)*.33 + Math.Sin(Tau*698*t)*Fade(t-.07,.33)*.42);
            engine.volume=0; music.volume=.21f; effects.volume=.58f;
            engine.Play(); music.Play();
        }

        const double Tau = Math.PI * 2;
        static double Fade(double t,double length) =>
            t < 0 || t >= length ? 0 : Math.Min(1,t*105) * Math.Pow(1-t/length,2.25);
        static double Beat(double t,double length,double freq,double decay) =>
            Math.Sin(Tau*(freq*t-32*t*t)) * Fade(t,length) * Math.Exp(-t*decay);

        static AudioClip Make(string name,double seconds,Sample sample)
        {
            int count=(int)(seconds*Rate); var data=new float[count];
            for(int i=0;i<count;i++) data[i]=(float)sample((double)i/Rate,i);
            var clip=AudioClip.Create(name,count,1,Rate,false);
            clip.SetData(data,0); return clip;
        }

        static AudioSource Source(Transform parent,string name,bool loop)
        {
            var go=new GameObject(name,typeof(AudioSource)); go.transform.SetParent(parent,false);
            var source=go.GetComponent<AudioSource>(); source.playOnAwake=false; source.loop=loop; source.spatialBlend=0; return source;
        }

        public void Tick(float speed,bool active,bool throttle)
        {
            if (muted) return;
            float km=speed*3.6f;
            int gear=Mathf.Min(6,Mathf.FloorToInt(km/49)+1);
            engine.pitch=Mathf.Lerp(engine.pitch,.82f+(km-(gear-1)*49)*.025f+gear*.073f,.12f);
            engine.volume=Mathf.Lerp(engine.volume,active ? throttle ? .23f : .145f : .014f,.12f);
        }
        public void SetMute(bool value)
        {
            muted=value; music.mute=engine.mute=effects.mute=value;
        }
        public void Crash() { if(!muted) effects.PlayOneShot(crash); }
        public void Pickup(bool isFuel) { if(!muted) effects.PlayOneShot(isFuel ? fuel : coin); }
    }
}
