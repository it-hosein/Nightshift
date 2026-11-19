using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.PostProcessing;

namespace Nightshift
{
    public enum GameState { Menu, Playing, Paused, GameOver }
    public enum PickupKind { Coin, Fuel, Heart }

    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public GameState State { get; private set; }
        public double Distance { get; private set; }
        public float Speed { get; private set; }
        public float Fuel { get; private set; }
        public float Nitro { get; private set; }
        public bool NitroEmptyWhileHeld => Nitro <= 0 && throttle != null && throttle.IsPressed();
        public int Lives { get; private set; }
        public int Score => Mathf.FloorToInt(score);
        public int Best => Mathf.Max(record, Score);
        public int Zone => Route.Zone((int)(Distance / Route.Length));
        public string EndReason { get; private set; }
        public string Message { get; private set; }
        public bool MessageVisible => toast > 0;
        public bool Muted { get; private set; }

        const string RecordKey = "nightshift-best";
        float score, x, lateral, elapsed, hitTime, toast, cameraX, cameraY, nitroIdle, fireAt, launchAt;
        double rocketDistance;
        Vector3 launchOrigin;
        int rocketSeed;
        double nextTraffic, nextCoin, nextFuel, nextHeart;
        int lane, record, spawnIndex;
        bool boosting, braking;
        Highway highway;
        DriverRig driver;
        NightAudio audioRig;
        NightParticles effects;
        GameHud hud;
        Camera chase;
        InputActionMap driving, menus;
        InputAction throttle, brake;
        ObjectPool<TrafficObstacle> cruising, moving;
        readonly List<TrafficObstacle> trafficOrder = new List<TrafficObstacle>(25);
        readonly TrafficObstacle[] laneLeaders = new TrafficObstacle[3];
        ObjectPool<CoinPickup> coins;
        ObjectPool<FuelPickup> fuels;
        ObjectPool<HeartPickup> hearts;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance == null) new GameObject("Nightshift GameManager").AddComponent<GameManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;
            QualitySettings.shadowDistance = 64;
            QualitySettings.pixelLightCount = 5;
            record = PlayerPrefs.GetInt(RecordKey, 0);
            var art = new NightMaterials();
            chase = SetupScene(art);
            highway = new Highway(transform, art);
            driver = new DriverRig(transform, art, true, 0);
            cruising = new ObjectPool<TrafficObstacle>(11, i => NewTraffic(art, i, false), item => !item.Active);
            moving = new ObjectPool<TrafficObstacle>(14, i => NewTraffic(art, i + 2, true), item => !item.Active);
            coins = new ObjectPool<CoinPickup>(24, i => new CoinPickup(art.Coin(transform)), item => !item.Active);
            fuels = new ObjectPool<FuelPickup>(8, i => new FuelPickup(art.Can(transform)), item => !item.Active);
            hearts = new ObjectPool<HeartPickup>(2, i => new HeartPickup(art.Heart(transform)), item => !item.Active);
            effects = new NightParticles(transform, art, driver.ExhaustMount);
            audioRig = new NightAudio(transform);
            CreateActions();
            hud = new GameHud(this);
            Change(GameState.Menu);
            ResetRun();
        }

        TrafficObstacle NewTraffic(NightMaterials art, int i, bool driving) =>
            driving ? (TrafficObstacle)new MovingTraffic(new DriverRig(transform, art, false, i + 1))
                    : new CruisingTraffic(new DriverRig(transform, art, false, i + 1));

        Camera SetupScene(NightMaterials art)
        {
            RenderSettings.skybox = art.Sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.065f, .09f, .145f);
            RenderSettings.fogDensity = .0031f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.37f, .47f, .68f);
            RenderSettings.ambientEquatorColor = new Color(.20f, .24f, .33f);
            RenderSettings.ambientGroundColor = new Color(.11f, .12f, .17f);
            RenderSettings.reflectionIntensity = .47f;
            DynamicGI.UpdateEnvironment();

            var sun = new GameObject("Moonlight", typeof(Light)).GetComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.transform.rotation = Quaternion.Euler(34, -43, 0);
            sun.type = LightType.Directional;
            sun.color = new Color(.64f, .77f, 1); sun.intensity = .92f;
            sun.shadows = LightShadows.Soft; sun.shadowStrength = .77f;
            RenderSettings.sun = sun;
            var soft = new GameObject("Fill", typeof(Light)).GetComponent<Light>();
            soft.transform.SetParent(transform, false); soft.transform.rotation = Quaternion.Euler(24, 153, 0);
            soft.type = LightType.Directional; soft.color = new Color(.55f, .68f, 1); soft.intensity = .34f;

            var cameraObject = new GameObject("Third-person chase camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";
            var cam = cameraObject.GetComponent<Camera>();
            cam.allowHDR = true; cam.allowMSAA = true;
            cam.farClipPlane = 1040; cam.nearClipPlane = .12f;
            var resources = Resources.Load<PostProcessResources>("NightshiftPostProcessResources");
            if (resources == null)
            {
                Debug.LogWarning("Nightshift post-processing resources were not imported.");
                return cam;
            }
            var post = cameraObject.AddComponent<PostProcessLayer>();
            post.Init(resources);
            post.volumeLayer = 1 << 0;
            post.volumeTrigger = cam.transform;
            post.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;
            var volume = new GameObject("Night grading and bloom", typeof(PostProcessVolume)).GetComponent<PostProcessVolume>();
            volume.transform.SetParent(transform, false); volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
            volume.sharedProfile = profile;
            var bloom = profile.AddSettings<Bloom>();
            bloom.enabled.Override(true); bloom.intensity.Override(2.4f); bloom.threshold.Override(1.12f); bloom.softKnee.Override(.72f);
            var vignette = profile.AddSettings<Vignette>();
            vignette.enabled.Override(true); vignette.intensity.Override(.23f); vignette.smoothness.Override(.50f);
            var grade = profile.AddSettings<ColorGrading>();
            grade.enabled.Override(true); grade.tonemapper.Override(Tonemapper.ACES);
            return cam;
        }

        void CreateActions()
        {
            driving = new InputActionMap("Driving");
            var left = driving.AddAction("LaneLeft", InputActionType.Button, "<Keyboard>/a");
            left.AddBinding("<Keyboard>/leftArrow"); left.AddBinding("<Gamepad>/dpad/left");
            left.performed += _ => lane = Mathf.Max(-1, lane - 1);
            var right = driving.AddAction("LaneRight", InputActionType.Button, "<Keyboard>/d");
            right.AddBinding("<Keyboard>/rightArrow"); right.AddBinding("<Gamepad>/dpad/right");
            right.performed += _ => lane = Mathf.Min(1, lane + 1);
            throttle = driving.AddAction("Nitro", InputActionType.Button, "<Keyboard>/w");
            throttle.AddBinding("<Keyboard>/upArrow"); throttle.AddBinding("<Gamepad>/rightTrigger");
            brake = driving.AddAction("Brake", InputActionType.Button, "<Keyboard>/s");
            brake.AddBinding("<Keyboard>/downArrow"); brake.AddBinding("<Keyboard>/space"); brake.AddBinding("<Gamepad>/leftTrigger");

            menus = new InputActionMap("Menu");
            var pause = menus.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            pause.AddBinding("<Keyboard>/p"); pause.AddBinding("<Gamepad>/start");
            pause.performed += _ => TogglePause();
            var mute = menus.AddAction("Mute", InputActionType.Button, "<Keyboard>/m");
            mute.performed += _ => ToggleMute();
            menus.Enable();
        }

        public void StartRun()
        {
            CommitScore(); ResetRun(); Change(GameState.Playing);
        }

        public void ToMenu()
        {
            CommitScore(); Change(GameState.Menu); ResetRun();
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) { CommitScore(); Change(GameState.Paused); }
            else if (State == GameState.Paused) Change(GameState.Playing);
        }

        public void ToggleMute()
        {
            Muted = !Muted; audioRig.SetMute(Muted);
            hud.Refresh();
        }

        public void Exit()
        {
            CommitScore();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            ToMenu();
#else
            Application.Quit();
#endif
        }

        void Change(GameState state)
        {
            State = state;
            if (state == GameState.Playing) driving.Enable();
            else driving.Disable();
            effects.SetPaused(state == GameState.Paused || state == GameState.GameOver);
            hud.Refresh();
        }

        void ResetRun()
        {
            Distance = 0; x = 0; lane = 0; lateral = 0; Speed = 35.55f; elapsed = 0; score = 0;
            Lives = 3; Fuel = 100; Nitro = 100; nitroIdle = 0; hitTime = 0; toast = 0; cameraX = 0; cameraY = 0;
            nextTraffic = 163; nextCoin = 201; nextFuel = 515; nextHeart = 1080;
            spawnIndex = 0; fireAt = 7; launchAt = -1; boosting = false; braking = false;
            foreach (var item in cruising.Items) item.Release();
            foreach (var item in moving.Items) item.Release();
            foreach (var item in coins.Items) item.Release();
            foreach (var item in fuels.Items) item.Release();
            foreach (var item in hearts.Items) item.Release();
            effects.Clear();
            hud.SetImpact(0);
            moving.Take().Spawn(98, -1, 19);
            cruising.Take().Spawn(136, 1, 17);
            for (int i = 0; i < 3; i++) coins.Take().Spawn(73 + i * 6, 0);
            highway.Update(Distance);
            Position(0);
            UpdateItems(0, 0, 0, false);
            hud.Tick();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .11f);
            if (State == GameState.Menu)
            {
                Position(dt); audioRig.Tick(Speed, false, false); return;
            }
            if (State != GameState.Playing)
            {
                audioRig.Tick(Speed, false, false); return;
            }
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / .017f));
            for (int i = 0; i < steps && State == GameState.Playing; i++) Simulate(dt / steps);
            highway.Update(Distance);
            Position(dt);
            if (State == GameState.Playing) effects.Exhaust(dt, elapsed, Speed, boosting && !braking);
            audioRig.Tick(Speed, State == GameState.Playing, boosting);
            hud.Tick();
        }

        void Simulate(float dt)
        {
            double old = Distance;
            float oldX = x;
            elapsed += dt;
            bool requested = throttle.IsPressed();
            braking = brake.IsPressed();
            boosting = requested && !braking && Nitro > 0;
            if (boosting)
            {
                Nitro = Mathf.Max(0, Nitro - 20 * dt);
                nitroIdle = 0;
            }
            else if (!requested)
            {
                nitroIdle += dt;
                if (nitroIdle >= 2.5f) Nitro = Mathf.Min(100, Nitro + 7 * dt);
            }
            float cruise = Mathf.Lerp(35.55f, 62, Mathf.Clamp01((float)Distance / 13000 + elapsed / 310));
            float goal = braking ? 21 : boosting ? 88 : cruise;
            Speed += (goal - Speed) * (braking ? 1.48f : boosting ? .43f : .31f) * dt;
            Distance += Speed * (double)dt;
            score += Speed * dt * .68f;
            Fuel = Mathf.Max(0, Fuel - dt * (1.02f + Speed * .009f + (boosting ? .31f : 0)));
            hitTime = Mathf.Max(0, hitTime - 1.66f * dt);
            toast = Mathf.Max(0, toast - dt);
            lateral += ((lane * Route.Lane - x) * 64 - lateral * 13) * dt;
            x += lateral * dt;
            if (Fuel <= 0) { End("Out of fuel"); return; }
            SpawnRows();
            UpdateItems(dt, old, oldX, true);
            if (State != GameState.Playing) return;
            TickFireworks();
        }

        void TickFireworks()
        {
            if (launchAt < 0 && elapsed >= fireAt && Zone != 1)
            {
                launchAt = elapsed; rocketDistance = Distance + 168; rocketSeed = spawnIndex;
                float side = Route.Noise(rocketSeed + 21) > .5f ? 1 : -1;
                launchOrigin = new Vector3(Route.Center(rocketDistance) + side * (33 + 17 * Route.Noise(rocketSeed + 18)),
                    Route.Height(rocketDistance) + 6, 0);
                fireAt = elapsed + 12 + Route.Noise(rocketSeed) * 7;
            }
            if (launchAt < 0) return;
            float t = elapsed - launchAt;
            Vector3 point = launchOrigin + new Vector3(0, 43 * (1 - Mathf.Pow(1 - Mathf.Clamp01(t), 1.4f)),
                (float)(rocketDistance - Distance));
            if (t < 1) effects.RocketSpark(point);
            else { effects.Fireworks(point, rocketSeed); launchAt = -1; }
        }

        void SpawnRows()
        {
            float pressure = Mathf.Clamp01(elapsed / 180 + (float)Distance / 22000);
            while (Distance + 335 >= nextTraffic)
            {
                double at = nextTraffic;
                int i = spawnIndex++, l = LaneAt(i * 2.78);
                NewObstacle(at, l, i);
                if (i > 6 && Route.Noise(i + 37) < pressure * .62f) NewObstacle(at + 16, l == 1 ? 0 : l + 1, i + 1);
                nextTraffic += Mathf.Lerp(116, 46, pressure) + 21 * Route.Noise(i + 1);
            }
            if (Distance + 335 >= nextCoin)
            {
                double at = nextCoin;
                int l = LaneAt(at);
                bool blocked = TrafficNear(at, l, 12);
                if (!blocked) for (int i = 0; i < 3; i++) coins.Take()?.Spawn(at + 6 * i, l);
                nextCoin += 145 + 30 * Route.Noise(at + 1);
            }
            if (Distance + 335 >= nextFuel)
            {
                double at = nextFuel;
                int l = LaneAt(at + 22);
                if (TrafficNear(at, l, 10))
                    l = l == 1 ? -1 : l + 1;
                fuels.Take()?.Spawn(at, l);
                nextFuel += 535 + 70 * Route.Noise(at);
            }
            if (Distance + 335 >= nextHeart)
            {
                double at = nextHeart;
                int preferred = LaneAt(at + 61) + 1;
                for (int i = 0; i < 3; i++)
                {
                    int l = (preferred + i) % 3 - 1;
                    if (TrafficNear(at, l, 20) || PickupNear(coins.Items, at, l) || PickupNear(fuels.Items, at, l)) continue;
                    hearts.Take()?.Spawn(at, l);
                    break;
                }
                nextHeart += 1200 + 600 * Route.Noise(at + 62);
            }
        }

        static bool PickupNear<T>(T[] items, double at, int lane) where T : Pickup
        {
            foreach (var item in items)
                if (item.Active && item.Lane == lane && Math.Abs(item.Distance - at) < 20) return true;
            return false;
        }

        static int LaneAt(double seed) => Mathf.Min(2, (int)(Route.Noise(seed) * 3)) - 1;
        bool TrafficNear(double at, int lane, double gap)
        {
            return TrafficNear(cruising.Items, at, lane, gap) || TrafficNear(moving.Items, at, lane, gap);
        }

        static bool TrafficNear(TrafficObstacle[] cars, double at, int lane, double gap)
        {
            foreach (var car in cars)
                if (car.Active && car.Lane == lane && Math.Abs(car.Distance - at) < gap) return true;
            return false;
        }

        void NewObstacle(double at, int targetLane, int i)
        {
            if (TrafficNear(at, targetLane, TrafficObstacle.MinimumGap + 5)) return;
            var pool = i % 4 == 0 ? cruising : moving;
            pool.Take()?.Spawn(at, targetLane, 15 + Route.Noise(i + 8) * 12);
        }

        void UpdateItems(float dt, double old, float oldX, bool collide)
        {
            trafficOrder.Clear();
            foreach (var car in cruising.Items) if (car.Active) trafficOrder.Add(car);
            foreach (var car in moving.Items) if (car.Active) trafficOrder.Add(car);
            trafficOrder.Sort((a, b) => b.Distance.CompareTo(a.Distance));
            Array.Clear(laneLeaders, 0, laneLeaders.Length);
            foreach (var car in trafficOrder)
            {
                int index = car.Lane + 1;
                car.Follow(dt, Distance, laneLeaders[index]);
                if (car.Active) laneLeaders[index] = car;
            }
            if (collide)
            {
                foreach (var car in trafficOrder)
                {
                    double before = car.PreviousDistance - old;
                    double after = car.Distance - Distance;
                    CheckContact(car, before, after, oldX);
                    if (car.Active && State == GameState.Playing && !car.Passed && before >= 0 && after < 0)
                    {
                        car.Passed = true; score += 32;
                    }
                }
            }
            TickPickups(coins.Items, dt, old, oldX, collide);
            TickPickups(fuels.Items, dt, old, oldX, collide);
            TickPickups(hearts.Items, dt, old, oldX, collide);
        }

        void TickPickups<T>(T[] items, float dt, double old, float oldX, bool collide) where T : Pickup
        {
            foreach (var item in items)
            {
                if (!item.Active) continue;
                double before = item.Distance - old;
                item.Tick(dt, Distance);
                if (collide && !item.Collected) CheckContact(item, before, item.Distance - Distance, oldX);
            }
        }

        void CheckContact(Interactable item, double before, double after, float oldX)
        {
            if (!item.Active || State != GameState.Playing) return;
            bool hit = Math.Abs(after) <= item.Length || (before > item.Length && after < -item.Length);
            if (hit && Mathf.Abs((oldX + x) * .5f - item.Lane * Route.Lane) < item.Width)
                item.Contact(this);
        }

        void Position(float dt)
        {
            float height = Route.Height(Distance);
            driver.Root.localPosition = new Vector3(Route.Center(Distance) + x, height + .034f, 0);
            driver.Root.localRotation = Quaternion.Euler(0, Route.Yaw(Distance) + Mathf.Clamp(lateral * 1.26f, -11, 11), 0);
            driver.Drive(Speed, lateral, boosting, braking, State == GameState.Playing ? dt : 0);
            float flex = 1 - Mathf.Exp(-dt * 5);
            cameraX = Mathf.Lerp(cameraX, x * .62f, flex);
            cameraY = Mathf.Lerp(cameraY, height, dt <= 0 ? 1 : 1 - Mathf.Exp(-dt * 8));
            if (State == GameState.Menu)
            {
                float t = Time.unscaledTime;
                chase.transform.localPosition = new Vector3(4.7f + Mathf.Sin(t * .53f), 2.55f, -6.1f);
                chase.transform.LookAt(new Vector3(-4.9f, 1.08f, 4));
                chase.fieldOfView = 51; return;
            }
            float wobble = hitTime * hitTime;
            chase.transform.localPosition = new Vector3(Route.Center(Distance) + .66f + cameraX + Mathf.Sin(elapsed * 113) * wobble,
                cameraY + 3.31f + Mathf.Sin(elapsed * 83) * wobble, -8.5f - Speed * .025f);
            chase.transform.LookAt(new Vector3(Route.Center(Distance + 18) + cameraX * .86f, Route.Height(Distance + 18) + .81f, 17.8f));
            float fov = 53 + (Speed - 30) * .235f;
            chase.fieldOfView = dt <= 0 ? fov : Mathf.Lerp(chase.fieldOfView, fov, flex);
            hud.SetImpact(wobble);
        }

        public void Impact()
        {
            if (hitTime > .23f) return;
            Lives--; Speed = Mathf.Max(22, Speed * .68f); hitTime = .67f;
            Message = "COLLISION"; toast = 1.4f;
            effects.Crash(driver.Root.position + new Vector3(0, .56f, 1.45f));
            audioRig.Crash();
            if (Lives <= 0) End("Vehicle disabled");
        }

        public void Collect(PickupKind kind, Vector3 position)
        {
            switch (kind)
            {
                case PickupKind.Fuel:
                    Fuel = Mathf.Min(100, Fuel + 32);
                    Message = "FUEL +32";
                    break;
                case PickupKind.Heart:
                    Message = Lives < 3 ? "LIFE +1" : "LIVES FULL";
                    Lives = Mathf.Min(3, Lives + 1);
                    break;
                default:
                    score += 125;
                    Message = "+125 SCORE";
                    break;
            }
            toast = 1.5f;
            effects.Pickup(position, kind); audioRig.Pickup(kind);
        }

        void End(string reason)
        {
            EndReason = reason; CommitScore(); Change(GameState.GameOver);
        }

        void CommitScore()
        {
            int now = Score;
            if (record >= now) return;
            record = now; PlayerPrefs.SetInt(RecordKey, record); PlayerPrefs.Save();
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && State == GameState.Playing) TogglePause();
        }
        void OnApplicationQuit() => CommitScore();
        void OnDestroy()
        {
            if (Instance != this) return;
            if (driving != null) foreach (var action in driving.actions) action.Dispose();
            if (menus != null) foreach (var action in menus.actions) action.Dispose();
            Instance = null;
        }
    }

    public abstract class Interactable
    {
        protected readonly Transform Root;
        public double Distance { get; private set; }
        public int Lane { get; private set; }
        protected float Velocity;
        protected float Spin;
        public bool Active => Root.gameObject.activeSelf;
        public virtual float Length => 2.1f;
        public virtual float Width => 1.32f;
        protected virtual float Height => .75f;
        protected Interactable(Transform root) { Root = root; Release(); }
        public virtual void Spawn(double distance, int lane, float velocity = 0)
        {
            Distance = distance; Lane = lane; Velocity = velocity; Spin = 0;
            Root.gameObject.SetActive(true);
        }
        public void Release() => Root.gameObject.SetActive(false);
        public virtual void Tick(float dt, double player)
        {
            Distance += Velocity * (double)dt;
            float q = (float)(Distance - player);
            if (q < -24 || q > 760) { Release(); return; }
            Spin = Mathf.Repeat(Spin + dt * 145, 360);
            Root.localPosition = new Vector3(Route.Center(Distance) + Lane * Route.Lane, Route.Height(Distance) + Height, q);
            Root.localRotation = Quaternion.Euler(0, Route.Yaw(Distance) + (Length < 3 ? Spin : 0), 0);
        }
        public abstract void Contact(GameManager game);
    }

    public abstract class TrafficObstacle : Interactable
    {
        public const float MinimumGap = 8.5f;
        readonly DriverRig rig;
        float cruise;
        public double PreviousDistance { get; private set; }
        public bool Passed;
        protected TrafficObstacle(DriverRig rig) : base(rig.Root) { this.rig = rig; }
        protected override float Height => .034f;
        public override float Length => 3.97f;
        public override float Width => 1.77f;
        public void Follow(float dt, double player, TrafficObstacle leader)
        {
            PreviousDistance = Distance;
            if (dt > 0)
            {
                float target = cruise;
                if (leader != null)
                {
                    float desiredGap = MinimumGap + Velocity * .42f;
                    float space = (float)(leader.PreviousDistance - Distance) - desiredGap;
                    target = Mathf.Min(target, leader.Velocity + Mathf.Max(0, space) * .7f);
                }
                Velocity = Mathf.Lerp(Velocity, target, 1 - Mathf.Exp(-(target < Velocity ? 6 : 1.75f) * dt));
                if (leader != null)
                    Velocity = Mathf.Min(Velocity, Mathf.Max(0, (float)(leader.Distance - Distance - MinimumGap) / dt));
            }
            Tick(dt, player);
            if (Active) rig.Drive(Velocity, 0, false, false, dt);
        }
        public override void Spawn(double distance, int lane, float velocity = 0)
        {
            cruise = velocity; base.Spawn(distance, lane, velocity); Passed = false;
        }
        public override void Contact(GameManager game) { game.Impact(); Release(); }
    }
    public sealed class CruisingTraffic : TrafficObstacle
    {
        public CruisingTraffic(DriverRig rig) : base(rig) { }
        public override void Spawn(double distance, int lane, float velocity = 0) => base.Spawn(distance, lane, Mathf.Clamp(velocity, 15, 19));
    }
    public sealed class MovingTraffic : TrafficObstacle
    {
        public MovingTraffic(DriverRig rig) : base(rig) { }
        public override void Spawn(double distance, int lane, float velocity = 0) => base.Spawn(distance, lane, Mathf.Max(12, velocity));
    }
    public abstract class Pickup : Interactable
    {
        const float FadeTime = .26f;
        float remaining;
        public bool Collected => remaining > 0;
        protected abstract PickupKind Kind { get; }
        protected Pickup(Transform root) : base(root) { }

        public override void Spawn(double distance, int lane, float velocity = 0)
        {
            base.Spawn(distance, lane, velocity);
            remaining = 0;
            Root.localScale = Vector3.one;
        }

        public override void Tick(float dt, double player)
        {
            base.Tick(dt, player);
            if (!Collected) return;
            remaining = Mathf.Max(0, remaining - dt);
            Root.localScale = Vector3.one * (remaining / FadeTime);
            if (remaining <= 0) Release();
        }

        public override void Contact(GameManager game)
        {
            game.Collect(Kind, Root.position);
            remaining = FadeTime;
        }
    }

    public sealed class CoinPickup : Pickup
    {
        public CoinPickup(Transform root) : base(root) { }
        protected override PickupKind Kind => PickupKind.Coin;
    }
    public sealed class FuelPickup : Pickup
    {
        public FuelPickup(Transform root) : base(root) { }
        protected override PickupKind Kind => PickupKind.Fuel;
    }
    public sealed class HeartPickup : Pickup
    {
        public HeartPickup(Transform root) : base(root) { }
        protected override PickupKind Kind => PickupKind.Heart;
        protected override float Height => .87f;
    }
}
