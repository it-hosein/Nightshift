using UnityEngine;
using UnityEngine.Rendering;

namespace Nightshift
{
    public sealed class SkyFireworks
    {
        readonly Shell[] shells;
        readonly float[] searchOffsets = { 0, -32, 32 };
        float next;
        int sequence;

        public SkyFireworks(Transform parent)
        {
            var shader = Shader.Find("Nightshift/Fireworks");
            var trails = new Material(shader);
            var sparks = new Material(shader);
            sparks.SetFloat("_Billboard", 1);
            shells = new[] { new Shell(parent, sparks, trails), new Shell(parent, sparks, trails) };
            Reset();
        }

        public void Reset()
        {
            next = 4.4f;
            sequence = 0;
            foreach (var shell in shells) shell.Reset();
        }

        static bool Open(double distance) => Route.Zone((int)(distance / Route.Length)) != 1;

        public void Update(float time, double distance, float speed)
        {
            bool open = Open(distance);
            if (time >= next && open && Open(distance + speed * 2.4f))
            {
                float lead = Mathf.Max(230, 165 + speed * 1.65f);
                foreach (float extra in searchOffsets)
                {
                    double at = distance + lead + extra;
                    if (!Open(at) || !Open(at + 23)) continue;
                    int index = sequence++;
                    int side = index % 2 == 0 ? 1 : -1;
                    for (int i = 0; i < shells.Length; i++)
                    {
                        double launch = at + i * 23;
                        int direction = i == 0 ? side : -side;
                        float offset = 24 + Route.Noise(index * 14 + i + 72) * 7;
                        float floor = Route.Height(launch);
                        float start = floor + Mathf.Max(3, Route.Land(launch, offset, direction) + 3);
                        float peak = floor + (i == 0 ? 45 : 40);
                        shells[i].Prepare(launch, Route.Center(launch) + offset * direction,
                            start, peak, time + i * .66f, (index * 2 + i) % 4);
                    }
                    next = time + 13 + Route.Noise(index + 296) * 5.4f;
                    break;
                }
            }
            foreach (var shell in shells) shell.Draw(time, distance, open);
        }

        sealed class Shell
        {
            const int Stars = 160, TrailSteps = 4, Quads = Stars + 1;
            const int TrailVertices = Stars * TrailSteps * 2;
            const float Flight = 1.12f, GoldenAngle = 2.39996323f;
            static readonly Color[] Main =
            {
                new Color(1,.74f,.34f).linear, new Color(1,.42f,.47f).linear,
                new Color(.40f,.81f,1).linear, new Color(.72f,.63f,1).linear
            };
            static readonly Color[] Secondary =
            {
                new Color(1,.90f,.65f).linear, new Color(1,.73f,.53f).linear,
                new Color(.96f,.95f,1).linear, new Color(.97f,.79f,.87f).linear
            };

            readonly Transform root;
            readonly Mesh points, trails;
            readonly Star[] stars = new Star[Stars];
            readonly Vector3[] pointVertices = new Vector3[Quads * 4], trailVertices = new Vector3[TrailVertices];
            readonly Color[] pointColors = new Color[Quads * 4], trailColors = new Color[TrailVertices];
            readonly Vector2[] pointData = new Vector2[Quads * 4], trailData = new Vector2[TrailVertices];
            readonly int[] pointIndices = new int[Quads * 6], trailIndices = new int[TrailVertices];
            readonly Bounds bounds = new Bounds(Vector3.zero, Vector3.one * 185);
            float time, x, start, peak;
            double at;
            Color flashColor;
            int activeQuads, activeTrailVertices;
            readonly float[] expansion = new float[TrailSteps+1], gravity = new float[TrailSteps+1];

            struct Star
            {
                public Vector3 Velocity;
                public Color Tint;
                public float Life, Phase;
            }

            public Shell(Transform parent, Material sparkMaterial, Material trailMaterial)
            {
                root = new GameObject("Pooled fireworks shell").transform;
                root.SetParent(parent, false);
                points = CreateRenderer("Spark billboards", sparkMaterial);
                trails = CreateRenderer("Spark trails", trailMaterial);
                var corners = new Vector2[Quads * 4];
                for (int i = 0; i < Quads; i++)
                {
                    int v = i * 4, p = i * 6;
                    corners[v] = Vector2.zero; corners[v+1] = Vector2.right;
                    corners[v+2] = Vector2.one; corners[v+3] = Vector2.up;
                    pointIndices[p] = v; pointIndices[p+1] = v+1; pointIndices[p+2] = v+2;
                    pointIndices[p+3] = v; pointIndices[p+4] = v+2; pointIndices[p+5] = v+3;
                }
                points.vertices = pointVertices;
                points.uv = corners;
                trails.vertices = trailVertices;
                for (int i = 0; i < TrailVertices; i++) trailIndices[i] = i;
                Reset();
            }

            Mesh CreateRenderer(string name, Material material)
            {
                var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(root, false);
                var mesh = new Mesh { name = name };
                mesh.MarkDynamic();
                obj.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                return mesh;
            }

            public void Reset()
            {
                time = float.PositiveInfinity;
                root.gameObject.SetActive(false);
            }

            public void Prepare(double at, float x, float start, float peak, float time, int seed)
            {
                this.at = at; this.x = x; this.start = start; this.peak = peak; this.time = time;
                flashColor = Secondary[seed];
                float phase = Route.Noise(seed + 91) * Mathf.PI * 2;
                for (int i = 0; i < Stars; i++)
                {
                    bool outer = i < 128;
                    int row = outer ? i : i - 128;
                    float y = 1 - 2 * (row + .5f) / (outer ? 128 : Stars - 128);
                    float ring = Mathf.Sqrt(1 - y * y), angle = phase + row * GoldenAngle;
                    double sample = seed * 241 + i * 3.83;
                    float speed = (outer ? 27 : 16) * (.78f + .44f * Route.Noise(sample));
                    stars[i] = new Star
                    {
                        Velocity = new Vector3(Mathf.Cos(angle) * ring, y, Mathf.Sin(angle) * ring) * speed,
                        Tint = i % 6 == 0 || !outer ? Secondary[seed] : Main[seed],
                        Life = 1.52f + Route.Noise(sample + 1) * .98f,
                        Phase = Route.Noise(sample + 2) * 7
                    };
                }
            }

            public void Draw(float now, double distance, bool open)
            {
                float age = now - time;
                bool visible = open && age >= 0 && age <= Flight + 2.55f;
                root.gameObject.SetActive(visible);
                if (!visible) return;
                root.localPosition = new Vector3(x, peak, (float)(at - distance));
                int quads, vertices;
                if (age < Flight) Ascend(age, out quads, out vertices);
                else
                {
                    Burst(age - Flight);
                    quads = Quads; vertices = TrailVertices;
                }
                points.vertices = pointVertices; points.colors = pointColors; points.uv2 = pointData;
                if (activeQuads != quads)
                {
                    points.SetIndices(pointIndices, 0, quads * 6, MeshTopology.Triangles, 0, false);
                    activeQuads = quads;
                }
                points.bounds = bounds;
                trails.vertices = trailVertices; trails.colors = trailColors; trails.uv2 = trailData;
                if (activeTrailVertices != vertices)
                {
                    trails.SetIndices(trailIndices, 0, vertices, MeshTopology.Lines, 0, false);
                    activeTrailVertices = vertices;
                }
                trails.bounds = bounds;
            }

            float Height(float age) => (start - peak) * Mathf.Pow(1 - Mathf.Clamp01(age / Flight), 1.6f);

            void Ascend(float age, out int quads, out int vertices)
            {
                const int steps = 16;
                var amber = new Color(1,.59f,.19f);
                PutPoint(0, Vector3.up * Height(age), 1.7f, 4.6f, amber);
                for (int i = 0; i < steps; i++)
                {
                    PutTrail(i*2, Vector3.up * Height(age - i*.024f), amber, 3 * (1 - i / (float)steps));
                    PutTrail(i*2+1, Vector3.up * Height(age - (i+1)*.024f), amber, 3 * (1 - (i+1) / (float)steps));
                }
                quads = 1; vertices = steps * 2;
            }

            void Burst(float age)
            {
                for (int i = 0; i <= TrailSteps; i++)
                {
                    float t = Mathf.Max(0, age - i * .083f);
                    expansion[i] = 1.28f * (1 - Mathf.Exp(-t / 1.28f));
                    gravity[i] = -3.25f * t * t;
                }
                PutPoint(Stars, Vector3.zero, 12 + 22 * age,
                    age < .26f ? 3.0f * Mathf.Exp(-age * 18) : 0, flashColor);
                for (int i = 0; i < Stars; i++)
                {
                    Star star = stars[i];
                    float remaining = Mathf.Clamp01(1 - age / star.Life);
                    float strength = 5.8f * Mathf.Pow(remaining, 1.33f) * (.78f + .22f * Mathf.Sin(age * 30 + star.Phase));
                    Vector3 from = Position(star.Velocity, 0);
                    PutPoint(i, from, 1.65f, strength, star.Tint);
                    for (int step = 0; step < TrailSteps; step++)
                    {
                        Vector3 to = Position(star.Velocity, step + 1);
                        int k = (i * TrailSteps + step) * 2;
                        PutTrail(k, from, star.Tint, strength * .72f * (1 - step / (float)TrailSteps));
                        PutTrail(k+1, to, star.Tint, strength * .72f * (1 - (step + 1) / (float)TrailSteps));
                        from = to;
                    }
                }
            }

            Vector3 Position(Vector3 velocity, int step) =>
                velocity * expansion[step] + Vector3.up * gravity[step];

            void PutPoint(int index, Vector3 at, float size, float strength, Color tint)
            {
                int first = index * 4;
                for (int i = first; i < first + 4; i++)
                {
                    pointVertices[i] = at;
                    pointColors[i] = tint;
                    pointData[i] = new Vector2(size, strength);
                }
            }

            void PutTrail(int i, Vector3 at, Color tint, float strength)
            {
                trailVertices[i] = at;
                trailColors[i] = tint;
                trailData[i] = new Vector2(0, strength);
            }
        }
    }
}
