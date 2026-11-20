using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nightshift
{
    public sealed class NightMaterials
    {
        public readonly Material Asphalt, Markings, Marker, Metal, Concrete, Lamp, ColdLamp, Land, Tree, Water;
        public readonly Material AmberGlow, BlueGlow, RedGlow, HeadGlow, CourseGlow, Shadow, Fire, Sky;
        public readonly Material Gold, GoldDark, Fuel, Rubber, Glass;
        readonly Material heartMaterial;
        readonly Mesh heartMesh;
        public readonly Material[] Paints;
        public readonly GameObject CarAsset;
        public readonly GameObject[] TrafficAssets;
        static readonly Color[] PaintColors =
        {
            new Color(.76f,.055f,.105f), new Color(.52f,.64f,.74f), new Color(.23f,.37f,.62f),
            new Color(.33f,.22f,.32f), new Color(.69f,.49f,.25f), new Color(.18f,.25f,.34f)
        };

        public NightMaterials()
        {
            Asphalt = Surface("Asphalt", new Color(.28f, .31f, .37f), .23f, .13f);
            Asphalt.mainTexture = Resources.Load<Texture2D>("Nightshift/Asphalt");
            Markings = Surface("Reflective lane paint", new Color(.72f, .76f, .77f), .38f);
            Marker = Lit("Amber road stud", new Color(.88f, .52f, .14f), 1.6f);
            Metal = Surface("Galvanized steel", new Color(.32f, .38f, .47f), .62f, .58f);
            Concrete = Surface("Cast concrete", new Color(.24f, .28f, .33f), .22f);
            Lamp = Lit("Sodium lamps", new Color(1, .65f, .36f), 5.5f);
            ColdLamp = Lit("Tunnel fluorescents", new Color(.51f, .81f, 1), 4.5f);
            Land = new Material(Shader.Find("Nightshift/Mountain"));
            Land.mainTexture = Resources.Load<Texture2D>("Nightshift/Rock");
            Tree = Surface("Pine", new Color(.068f, .13f, .14f), .10f);
            Water = Surface("River", new Color(.18f, .29f, .45f), .82f, .64f);
            Gold = Lit("Coin gold", new Color(.98f, .66f, .13f), 1.35f);
            GoldDark = Surface("Coin stamping", new Color(.65f, .34f, .055f), .58f, .71f);
            Fuel = Lit("Fuel sage", new Color(.57f,.65f,.46f), .24f);
            heartMaterial = Lit("Red enamel heart", new Color(.79f,.28f,.33f), .33f);
            heartMesh = BuildHeartMesh();
            Rubber = Surface("Rubber", new Color(.07f, .09f, .12f), .38f);
            Glass = Surface("Tinted glass", new Color(.16f,.23f,.37f), .93f, .38f);
            Paints = new Material[PaintColors.Length];
            for (int i = 0; i < Paints.Length; i++) Paints[i] = Surface("Metallic paint " + i, PaintColors[i], .87f, .63f);

            Texture2D halo = RenderHalo();
            AmberGlow = GlowMaterial("Sodium light wash", halo, new Color(1, .58f, .31f, .21f));
            BlueGlow = GlowMaterial("Tunnel light wash", halo, new Color(.55f, .76f, 1, .21f));
            RedGlow = GlowMaterial("Brake light reflection", halo, new Color(1,.05f,.13f,.31f));
            HeadGlow = GlowMaterial("Headlight wash", halo, new Color(.64f,.79f,1,.35f));
            CourseGlow = GlowMaterial("Course cyan underglow", halo, new Color(.40f,.77f,1,.32f));
            Shadow = GlowMaterial("Ground contact shadow", Resources.Load<Texture2D>("Nightshift/CarAO"),
                new Color(.01f,.019f,.035f,.89f));
            Shadow.SetFloat("_Shadow", 1);
            Fire = new Material(Shader.Find("Particles/Standard Unlit"));
            Fire.mainTexture = halo;
            Fire.SetFloat("_Mode", 4);
            Fire.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            Fire.SetFloat("_DstBlend", (float)BlendMode.One);
            Fire.SetFloat("_ZWrite", 0);
            Fire.EnableKeyword("_ALPHABLEND_ON"); Fire.renderQueue = (int)RenderQueue.Transparent;

            Sky = new Material(Shader.Find("Skybox/Panoramic"));
            Sky.SetTexture("_MainTex", Resources.Load<Texture>("Nightshift/Moon"));
            Sky.SetFloat("_Exposure", .20f);
            Sky.SetFloat("_Rotation", 157);
            CarAsset = Resources.Load<GameObject>("Nightshift/Car");
            TrafficAssets = new[] { Resources.Load<GameObject>("Nightshift/Porsche"), Resources.Load<GameObject>("Nightshift/Lamborghini"),
                Resources.Load<GameObject>("Nightshift/Bugatti"), Resources.Load<GameObject>("Nightshift/BMW") };

        }

        public static Material Surface(string name, Color color, float gloss, float metal = 0)
        {
            var mat = new Material(Shader.Find("Standard")) { name = name, color = color };
            mat.SetFloat("_Glossiness", gloss);
            mat.SetFloat("_Metallic", metal);
            return mat;
        }

        public static Material Lit(string name, Color color, float brightness)
        {
            var mat = Surface(name, color, .46f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * brightness);
            return mat;
        }

        static Texture2D RenderHalo()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x - 31.5f, y - 31.5f).magnitude / 31.5f;
                float a = Mathf.Pow(Mathf.Clamp01(1 - r), 2.5f);
                pixels[y * size + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels(pixels); tex.Apply(false, true);
            return tex;
        }

        static Material GlowMaterial(string name, Texture tex, Color tint) =>
            new Material(Shader.Find("Nightshift/Highway Glow")) { name = name, mainTexture = tex, color = tint };

        public Transform Glow(Transform parent, string name, Material mat)
        {
            Transform t = Primitive(parent, PrimitiveType.Quad, name, Vector3.zero, Vector3.one, mat);
            t.localRotation = Quaternion.Euler(90, 0, 0);
            var renderer = t.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return t;
        }

        public static Transform Primitive(Transform parent, PrimitiveType kind, string name, Vector3 at, Vector3 size, Material art)
        {
            var obj = GameObject.CreatePrimitive(kind); obj.name = name;
            if (obj.TryGetComponent<Collider>(out var collider)) { collider.enabled = false; UnityEngine.Object.Destroy(collider); }
            var tr = obj.transform;
            tr.SetParent(parent, false); tr.localPosition = at; tr.localScale = size;
            obj.GetComponent<MeshRenderer>().sharedMaterial = art;
            return tr;
        }

        public Transform Coin(Transform parent)
        {
            var root = new GameObject("Pooled coin").transform; root.SetParent(parent, false);
            Primitive(root, PrimitiveType.Cylinder, "Coin", Vector3.zero, new Vector3(.64f,.041f,.64f), Gold)
                .localRotation = Quaternion.Euler(90, 0, 0);
            Primitive(root, PrimitiveType.Cylinder, "Inset", Vector3.zero, new Vector3(.47f,.047f,.47f), GoldDark)
                .localRotation = Quaternion.Euler(90, 0, 0);
            foreach (int side in Signs.Values)
                Primitive(root, PrimitiveType.Cube, "Stamp", new Vector3(0,0,.0505f * side), new Vector3(.065f,.23f,.018f), Gold)
                    .localRotation = Quaternion.Euler(0, 0, 19);
            return root;
        }

        public Transform Heart(Transform parent)
        {
            var heart = new GameObject("Pooled heart", typeof(MeshFilter), typeof(MeshRenderer));
            heart.transform.SetParent(parent, false);
            heart.GetComponent<MeshFilter>().sharedMesh = heartMesh;
            heart.GetComponent<MeshRenderer>().sharedMaterial = heartMaterial;
            return heart.transform;
        }

        static Mesh BuildHeartMesh()
        {
            const int sides = 64, rings = 4;
            var points = new List<Vector3>(sides * rings + 2);
            var triangles = new List<int>(sides * 24);
            for (int ring = 0; ring < rings; ring++)
            {
                float radius = ring == 0 || ring == rings - 1 ? .85f : 1;
                float z = ring < 2 ? -.10f : .10f;
                if (radius == 1) z *= .60f;
                for (int i = 0; i < sides; i++)
                {
                    float t = i * Mathf.PI * 2 / sides, sin = Mathf.Sin(t);
                    float x = 16 * sin * sin * sin;
                    float y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t) + 2;
                    points.Add(new Vector3(x * .024f * radius, y * .024f * radius, z));
                }
            }
            int back = points.Count, front = back + 1;
            points.Add(new Vector3(0, 0, -.10f)); points.Add(new Vector3(0, 0, .10f));
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                triangles.Add(back); triangles.Add(i); triangles.Add(next);
                triangles.Add(front); triangles.Add(3 * sides + next); triangles.Add(3 * sides + i);
                for (int ring = 0; ring < rings - 1; ring++)
                {
                    int a = ring * sides + i, b = ring * sides + next;
                    triangles.Add(a); triangles.Add(a+sides); triangles.Add(b);
                    triangles.Add(b); triangles.Add(a+sides); triangles.Add(b+sides);
                }
            }
            var mesh = new Mesh { name = "Beveled collection heart" };
            mesh.SetVertices(points); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public Transform Can(Transform parent)
        {
            var root = new GameObject("Pooled fuel can").transform; root.SetParent(parent, false);
            Primitive(root, PrimitiveType.Cube, "Jerrycan", Vector3.zero, new Vector3(.58f,.70f,.29f), Fuel);
            Primitive(root, PrimitiveType.Cube, "Carry grip", new Vector3(0,.42f,0), new Vector3(.34f,.083f,.15f), Fuel);
            foreach (int side in Signs.Values)
            {
                Primitive(root, PrimitiveType.Cube, "Handle mount", new Vector3(.135f*side,.35f,0), new Vector3(.065f,.15f,.15f), Fuel);
                foreach (int face in Signs.Values)
                    Primitive(root, PrimitiveType.Cube, "Pressed rib", new Vector3(0,0,face*.147f), new Vector3(.36f,.045f,.023f), Rubber)
                        .localRotation = Quaternion.Euler(0, 0, side * 46);
            }
            Primitive(root, PrimitiveType.Cylinder, "Cap", new Vector3(.23f,.37f,0), new Vector3(.144f,.065f,.144f), Rubber);
            return root;
        }
    }

    public sealed class DriverRig
    {
        public Transform Root { get; }
        public Transform ExhaustMount => shell;
        readonly Transform shell;
        readonly List<Transform> wheels = new List<Transform>(4);
        readonly Material tail, tailLight;
        readonly bool player;
        readonly Transform courseGlow;
        readonly Material courseGlowMaterial;
        readonly Light courseLamp;
        readonly Color redColor = new Color(1,.04f,.08f);
        float pitch, roll;

        public DriverRig(Transform parent, NightMaterials art, bool player, int palette)
        {
            this.player = player;
            Root = new GameObject(player ? "Ferrari 458 Italia" : "Pooled traffic car").transform;
            Root.SetParent(parent, false);
            shell = new GameObject("Suspension").transform; shell.SetParent(Root, false);
            if (art.CarAsset == null) throw new InvalidOperationException("The imported Ferrari FBX is missing.");
            var model = player ? art.CarAsset : art.TrafficAssets[(palette - 1) % art.TrafficAssets.Length];
            var car = UnityEngine.Object.Instantiate(model != null ? model : art.CarAsset, shell).transform;
            tailLight = NightMaterials.Lit("Taillight LED", redColor, 3.7f);
            car.localPosition = Vector3.zero;
            var renderers = car.GetComponentsInChildren<Renderer>();
            foreach (var t in car.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals("wheel_fl", StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("wheel_fr", StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("wheel_rl", StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("wheel_rr", StringComparison.OrdinalIgnoreCase)) wheels.Add(t);
            }
            if (model == null || model == art.CarAsset)
                AlignFerrari(car, renderers);
            else
            {
                var headlights = car.Find("Headlights");
                var taillights = car.Find("Taillights");
                if (headlights != null && taillights != null &&
                    headlights.GetComponent<Renderer>().bounds.center.z < taillights.GetComponent<Renderer>().bounds.center.z)
                    car.localRotation *= Quaternion.Euler(0, 180, 0);
            }
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
                string n = r.name.ToLowerInvariant();
                if (n == "body" || n == "paint") r.sharedMaterial = art.Paints[palette % art.Paints.Length];
                else if (n == "glass") r.sharedMaterial = art.Glass;
                else if (n == "lights_red" || n == "taillights") r.sharedMaterial = tailLight;
                else if (n == "trim") r.sharedMaterial = art.Rubber;
                else if (n == "headlights") r.sharedMaterial = NightMaterials.Lit("Headlight housing", new Color(.64f,.76f,1), .8f);
            }
            float scale = player ? Mathf.Clamp(4.55f / Mathf.Max(.1f, bounds.size.z), .1f, 100) : 1;
            car.localScale *= scale;
            car.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;

            SetGroundGlow(art, art.Shadow, "Contact shadow", 0, .016f, -.11f, 2.45f, 5.12f);
            tail = player ? new Material(art.RedGlow) : art.RedGlow;
            foreach (int side in Signs.Values)
                SetGroundGlow(art, tail, "Tail light reflection", .83f * side, .027f, -4.18f, 2.12f, 5.25f);

            if (!player) return;
            courseGlowMaterial = new Material(art.CourseGlow);
            courseGlow = art.Glow(Root, "Speed-responsive course underglow", courseGlowMaterial);
            courseGlow.localPosition = new Vector3(0,.028f,-.12f);
            courseGlow.localScale = new Vector3(4.6f,6.9f,1);
            courseGlow.gameObject.SetActive(false);

            var lamp = new GameObject("Course underbody light", typeof(Light));
            lamp.transform.SetParent(Root, false);
            lamp.transform.localPosition = new Vector3(0,.24f,0);
            courseLamp = lamp.GetComponent<Light>();
            courseLamp.type = LightType.Point;
            courseLamp.color = new Color(.40f,.77f,1);
            courseLamp.range = 3.6f;
            courseLamp.enabled = false;
            foreach (int side in Signs.Values)
            {
                SetGroundGlow(art, art.HeadGlow, "Headlight reflection", side * .84f, .031f, 11, 4.39f, 22);
                Transform spot = new GameObject("LED headlight", typeof(Light)).transform;
                spot.SetParent(Root, false); spot.localPosition = new Vector3(side * .76f,.58f,1.92f);
                spot.localRotation = Quaternion.Euler(9, side * 3, 0);
                var light = spot.GetComponent<Light>();
                light.type = LightType.Spot; light.color = new Color(.66f,.81f,1);
                light.intensity = 2.43f; light.range = 34; light.spotAngle = 48;
            }
        }

        void AlignFerrari(Transform car, Renderer[] renderers)
        {
            // Measure the imported car's axes so an FBX import rotation cannot invert the rig.
            if (wheels.Count != 4) return;
            Vector3 frontLeft = Vector3.zero, frontRight = Vector3.zero;
            Vector3 rearLeft = Vector3.zero, rearRight = Vector3.zero;
            foreach (var wheel in wheels)
            {
                switch (wheel.name.ToLowerInvariant())
                {
                    case "wheel_fl": frontLeft = wheel.position; break;
                    case "wheel_fr": frontRight = wheel.position; break;
                    case "wheel_rl": rearLeft = wheel.position; break;
                    case "wheel_rr": rearRight = wheel.position; break;
                }
            }
            Vector3 forward = (frontLeft + frontRight) - (rearLeft + rearRight);
            Vector3 across = (frontRight + rearRight) - (frontLeft + rearLeft);
            Vector3 up = Vector3.Cross(forward, across);
            Vector3 axleCenter = (frontLeft + frontRight + rearLeft + rearRight) * .25f;
            foreach (var renderer in renderers)
            {
                if (!renderer.name.Equals("body", StringComparison.OrdinalIgnoreCase)) continue;
                if (Vector3.Dot(up, renderer.bounds.center - axleCenter) < 0) up = -up;
                car.rotation = shell.rotation * Quaternion.Inverse(Quaternion.LookRotation(forward, up)) * car.rotation;
                break;
            }
        }

        void SetGroundGlow(NightMaterials art, Material material, string name, float x, float y, float z, float width, float length)
        {
            Transform g = art.Glow(Root, name, material);
            g.localPosition = new Vector3(x,y,z); g.localScale = new Vector3(width,length,1);
        }

        public void SetCourseUnderglow(bool enabled, float speed)
        {
            if (!player) return;
            courseGlow.gameObject.SetActive(enabled);
            courseLamp.enabled = enabled;
            if (!enabled) return;
            float factor = Mathf.Clamp01(speed / 90);
            var color = courseGlowMaterial.color;
            color.a = Mathf.Lerp(.19f,.78f,factor);
            courseGlowMaterial.color = color;
            courseLamp.intensity = Mathf.Lerp(.28f,1.75f,factor);
        }

        public void Drive(float speed, float lateral, bool throttle, bool brake, float dt)
        {
            float lerp = 1 - Mathf.Exp(-dt * 7);
            pitch = Mathf.Lerp(pitch, brake ? 1.67f : throttle ? -.97f : 0, lerp);
            roll = Mathf.Lerp(roll, -lateral * .68f, lerp);
            shell.localRotation = Quaternion.Euler(pitch,0,roll);
            foreach (var wheel in wheels) wheel.Rotate(Vector3.right, speed * dt / .351f * Mathf.Rad2Deg, Space.Self);
            if (!player) return;
            tail.color = new Color(1,.04f,.11f,brake ? .72f : .39f);
            tailLight.SetColor("_EmissionColor", redColor * (brake ? 7.2f : 3.7f));
        }
    }
}
