using System;
using UnityEngine;

namespace Nightshift
{
    public sealed class NightParticles
    {
        readonly ParticleSystem particles;
        readonly Color[] colors = { new Color(.30f,.85f,1),new Color(1,.15f,.28f),new Color(1,.66f,.23f),new Color(.55f,.33f,1) };

        public NightParticles(Transform parent, NightMaterials materials)
        {
            var obj = new GameObject("Pooled VFX particles"); obj.transform.SetParent(parent,false);
            particles = obj.AddComponent<ParticleSystem>(); particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = true; main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(.95f,0),new GradientAlphaKey(.76f,.18f),new GradientAlphaKey(0,1) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = materials.Fire;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            particles.Play();
        }

        public void SetPaused(bool paused)
        {
            if (paused) particles.Pause(); else particles.Play();
        }

        public void Clear() => particles.Clear();

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

        public void Exhaust(Vector3 at,bool throttle)
        {
            foreach (int side in Signs.Values)
                Burst(at + new Vector3(side*.35f,0,0),throttle ? new Color(1,.31f,.11f) : new Color(.42f,.52f,.66f,.35f),
                    3,1.6f,.43f,throttle ? .21f : .33f,new Vector3(0,.47f,-5));
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
