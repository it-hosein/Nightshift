using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nightshift
{
    public sealed class ObjectPool<T> where T : class
    {
        public readonly T[] Items;
        readonly Func<T, bool> available;
        public ObjectPool(int capacity, Func<int, T> make, Func<T, bool> available)
        {
            this.available = available;
            Items = new T[capacity];
            for (int i = 0; i < capacity; i++) Items[i] = make(i);
        }
        public T Take()
        {
            foreach (T item in Items) if (available(item)) return item;
            return null;
        }
    }

    public static class Route
    {
        public const int Length = 96, Cycle = 19;
        public const float Lane = 3.7f;
        public static float Center(double s) => (float)(13 * Math.Sin(s * .003) + 5 * Math.Sin(s * .008));
        public static float Height(double s) => (float)(1.2 * Math.Sin(s * .007) + 2.7 * Math.Sin(s * .0027));
        public static float Yaw(double s) => Mathf.Atan2(Center(s + 2) - Center(s - 2), 4) * Mathf.Rad2Deg;
        public static int Mod(int n, int d) => (n % d + d) % d;
        public static int Zone(int n)
        {
            int i = Mod(n, Cycle);
            return i >= 5 && i < 8 ? 1 : i >= 11 && i < 14 ? 2 : 0;
        }
        public static float Noise(double n)
        {
            double v = Math.Sin(n * 127.1 + 71.7) * 43758.5453;
            return (float)(v - Math.Floor(v));
        }
        static float Smooth(float a, float b, float v) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, v));
        public static float Land(double s, float d, int side)
        {
            float q = (float)((s % (Length * Cycle) + Length * Cycle) % (Length * Cycle));
            float crag = ((float)Math.Sin(d * .073 + s * .015 + side) + .53f * (float)Math.Sin(s * .043 - d * .105)
                + .25f * (float)Math.Sin(s * .17 + d * .29)) * Mathf.Clamp((d - 8) * .13f, 0, 11);
            float folds = 29 * Mathf.Pow(.5f + .5f * (float)Math.Sin(s * .012 + side * .9 + d * .019), 2.8f);
            float gorge = Smooth(1020, 1100, q) * (1 - Smooth(1325, 1400, q));
            return Smooth(8, 76, d) * (14 + folds + crag) + Smooth(18, 120, d) * 10
                - gorge * 15 * (1 - Smooth(58, 145, d)) - .34f;
        }
    }

    public sealed class Highway
    {
        const int Count = 10;
        readonly ObjectPool<RoadSegment> chunks;
        int previous = int.MinValue;

        public Highway(Transform root, NightMaterials art)
        {
            chunks = new ObjectPool<RoadSegment>(Count, i => new RoadSegment(root, art), s => !s.Active);
        }

        public void Update(double distance)
        {
            int section = (int)Math.Floor(distance / Route.Length);
            if (section != previous)
            {
                int min = section - 2, max = section + 7;
                foreach (var chunk in chunks.Items)
                    if (chunk.Active && (chunk.Index < min || chunk.Index > max)) chunk.Release();
                for (int i = min; i <= max; i++)
                {
                    bool exists = false;
                    foreach (var chunk in chunks.Items)
                        if (chunk.Active && chunk.Index == i) { exists = true; break; }
                    if (!exists) chunks.Take().Configure(i);
                }
                previous = section;
            }
            foreach (var chunk in chunks.Items) chunk.Place(distance);
        }
    }

    sealed class RoadSegment
    {
        readonly Transform root;
        readonly MeshData road, markings, reflectors, walls, rails, warm, cool, land, trees, water;
        readonly Transform[] glows = new Transform[6];
        readonly NightMaterials art;
        public int Index { get; private set; }
        public bool Active => root.gameObject.activeSelf;
        double start;

        public RoadSegment(Transform parent, NightMaterials art)
        {
            this.art = art;
            root = new GameObject("Pooled road segment").transform;
            root.SetParent(parent, false);
            road = new MeshData(root, art.Asphalt, "Asphalt");
            markings = new MeshData(root, art.Markings, "Lane markings");
            reflectors = new MeshData(root, art.Marker, "Road studs");
            walls = new MeshData(root, art.Concrete, "Structure");
            rails = new MeshData(root, art.Metal, "Guardrails");
            warm = new MeshData(root, art.Lamp, "Street lamps");
            cool = new MeshData(root, art.ColdLamp, "Tunnel lights");
            land = new MeshData(root, art.Land, "Mountain terrain");
            trees = new MeshData(root, art.Tree, "Pines");
            water = new MeshData(root, art.Water, "River");
            for (int i = 0; i < glows.Length; i++) glows[i] = art.Glow(root, "Street reflection", art.AmberGlow);
            Release();
        }

        public void Release() => root.gameObject.SetActive(false);
        public void Place(double distance)
        {
            root.localPosition = new Vector3(Route.Center(start), Route.Height(start), (float)(start - distance));
        }

        Vector3 At(float d, float x = 0, float y = 0) =>
            new Vector3(Route.Center(start + d) - Route.Center(start) + x, Route.Height(start + d) - Route.Height(start) + y, d);
        Quaternion Yaw(float d) => Quaternion.Euler(0, Route.Yaw(start + d), 0);

        public void Configure(int index)
        {
            Index = index;
            start = index * (double)Route.Length;
            int type = Route.Zone(index);
            root.gameObject.SetActive(true);
            road.Clear(); markings.Clear(); reflectors.Clear(); walls.Clear(); rails.Clear();
            warm.Clear(); cool.Clear(); land.Clear(); trees.Clear(); water.Clear();

            for (int d = 0; d < Route.Length; d += 4)
            {
                road.Quad(At(d, -6.6f), At(d + 4, -6.6f), At(d + 4, 6.6f), At(d, 6.6f),
                    Color.white, new Vector2(7.5f, .96f), d * .24f);
                foreach (int side in Signs.Values)
                {
                    float x = 5.77f * side;
                    markings.Quad(At(d, x - .06f, .023f), At(d + 4, x - .06f, .023f),
                        At(d + 4, x + .06f, .023f), At(d, x + .06f, .023f), Color.white, Vector2.one, 0);
                }
            }
            foreach (int side in Signs.Values)
            {
                for (int d = 4; d < Route.Length; d += 9)
                    markings.Box(At(d, side * Route.Lane * .5f, .024f), new Vector3(.11f, .016f, 3.7f), Yaw(d));
                for (int d = 3; d < Route.Length; d += 6)
                {
                    rails.Box(At(d, side * 6.38f, type == 2 ? .86f : .58f), new Vector3(.13f, .29f, 6.08f), Yaw(d));
                    rails.Box(At(d, side * 6.38f, .28f), new Vector3(.14f, .68f, .14f), Yaw(d));
                    reflectors.Box(At(d, side * 6.29f, .59f), new Vector3(.045f, .09f, .19f), Yaw(d));
                }
                BuildLand(side);
                if (type != 1)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        int d = 13 + k * 32;
                        rails.Box(At(d, side * 6.9f, 3.15f), new Vector3(.12f, 6.3f, .12f), Yaw(d));
                        rails.Box(At(d, side * 6.27f, 6.28f), new Vector3(1.26f, .10f, .14f), Yaw(d));
                        warm.Box(At(d, side * 5.63f, 6.21f), new Vector3(.32f, .11f, .66f), Yaw(d));
                        SetGlow(glows[(side > 0 ? 3 : 0) + k], At(d, side * 4.3f, .034f), 7.8f, 13f,
                            type == 2 ? art.BlueGlow : art.AmberGlow);
                    }
                    for (int k = 0; k < 27; k++)
                    {
                        double seed = index * 67.0 + side * 181 + k * 3.71;
                        float d = Route.Noise(seed) * Route.Length;
                        float x = (type == 2 ? 24 : 10) + Route.Noise(seed + 2) * 50;
                        float y = Route.Land(start + d, x, side);
                        float h = 3.5f + Route.Noise(seed + 3) * 6.8f;
                        trees.Cone(At(d, side * x, y + h * .22f), h * .24f, h * .71f, 7);
                        trees.Cone(At(d, side * x, y + h * .50f), h * .15f, h * .52f, 7);
                    }
                }
                if (type == 2)
                {
                    foreach (int d in new[] { 12, 84 })
                        walls.Box(At(d, side * 5.85f, -7.5f), new Vector3(1.6f, 14, 2.7f), Yaw(d));
                    for (int d = 2; d < Route.Length; d += 4)
                        walls.Box(At(d, side * 6.25f, -.55f), new Vector3(.72f, .67f, 4.06f), Yaw(d));
                    Vector3 crown = At(46, side * 7.1f, 15.8f);
                    walls.Box(At(46, side * 7.1f, 8), new Vector3(.69f, 17.2f, .92f), Yaw(46));
                    cool.Box(At(46, side * 7.1f, 16.6f), new Vector3(.75f, .12f, 1.04f), Yaw(46));
                    foreach (int offset in new[] { -38, -28, -18, 18, 28, 38 })
                    {
                        Vector3 foot = At(46 + offset, side * 7.1f, 1.02f);
                        Vector3 cable = crown - foot;
                        rails.Box((foot + crown) * .5f, new Vector3(.043f, cable.magnitude, .043f),
                            Quaternion.FromToRotation(Vector3.up, cable.normalized));
                    }
                }
            }
            if (type == 1)
            {
                for (int d = 2; d < Route.Length; d += 4)
                {
                    for (int k = 0; k <= 14; k++)
                    {
                        float a = Mathf.PI * k / 14, x = 7.35f * Mathf.Cos(a), y = 1.0f + 6.9f * Mathf.Sin(a);
                        Quaternion rot = Yaw(d) * Quaternion.Euler(0, 0, a * Mathf.Rad2Deg - 90);
                        walls.Box(At(d, x, y), new Vector3(1.74f, .53f, 4.22f), rot);
                        if (d % 12 == 2) rails.Box(At(d, x * .968f, y * .978f), new Vector3(1.83f, .48f, .22f), rot);
                    }
                    foreach (int side in Signs.Values)
                    {
                        walls.Box(At(d, side * 7.14f, .60f), new Vector3(.59f, 1.20f, 4.20f), Yaw(d));
                        if (d % 8 == 2) cool.Box(At(d, side * 3.77f, 6.75f), new Vector3(.14f, .12f, 2.75f), Yaw(d));
                    }
                }
                for (int i = 0; i < 6; i++) SetGlow(glows[i], At(8 + i * 16, 0, .037f), 12, 14, art.BlueGlow);
            }
            if (type == 2)
            {
                walls.Box(At(46, 0, 15.9f), new Vector3(14.6f, .49f, .57f), Yaw(46));
                water.Quad(At(0, -54, -12), At(96, -54, -12), At(96, 54, -12), At(0, 54, -12), Color.white, Vector2.one, 0);
            }
            road.Apply(); markings.Apply(); reflectors.Apply(); walls.Apply(); rails.Apply();
            warm.Apply(); cool.Apply(); land.Apply(); trees.Apply(); water.Apply();
        }

        void BuildLand(int side)
        {
            const int cols = 22, rows = 17;
            int startVertex = land.Vertices.Count;
            for (int i = 0; i < rows; i++)
            for (int j = 0; j < cols; j++)
            {
                float d = i * 6, x = 6.55f + j * 6.15f, y = Route.Land(start + d, x, side);
                float grain = Route.Noise((start + d) * 11 + j * 19 + side * 7);
                Color gravel = new Color(.058f, .073f, .105f) * (.76f + grain * .62f);
                float snow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(23, 48, y + 8 * grain));
                land.Vertex(At(d, side * x, y), new Vector2(x * .17f, (float)((start + d) * .17)), Color.Lerp(gravel, new Color(.39f, .46f, .55f), snow));
            }
            for (int i = 0; i < rows - 1; i++)
            for (int j = 0; j < cols - 1; j++)
            {
                int a = startVertex + i * cols + j;
                if (side > 0) land.Faces(a, a + cols, a + cols + 1, a + 1);
                else land.Faces(a + 1, a + cols + 1, a + cols, a);
            }
        }
        void SetGlow(Transform glow, Vector3 position, float width, float length, Material material)
        {
            glow.localPosition = position;
            glow.localRotation = Yaw(position.z) * Quaternion.Euler(90, 0, 0);
            glow.localScale = new Vector3(width, length, 1);
            glow.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    internal static class Signs { public static readonly int[] Values = { -1, 1 }; }

    sealed class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>(2048);
        readonly List<Vector2> uv = new List<Vector2>(2048);
        readonly List<Color> colors = new List<Color>(2048);
        readonly List<int> indices = new List<int>(4096);
        readonly Mesh mesh = new Mesh();
        static readonly Vector3[] Corners =
        {
            new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
            new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f)
        };
        static readonly int[,] Sides = { { 1,0,3,2 }, { 4,5,6,7 }, { 0,4,7,3 }, { 5,1,2,6 }, { 3,7,6,2 }, { 0,1,5,4 } };
        public MeshData(Transform root, Material material, string name)
        {
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(root, false);
            mesh.name = name; mesh.MarkDynamic();
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = name == "Structure" ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        public void Clear() { Vertices.Clear(); uv.Clear(); colors.Clear(); indices.Clear(); }
        public void Vertex(Vector3 p, Vector2 t, Color c) { Vertices.Add(p); uv.Add(t); colors.Add(c); }
        public void Faces(int a, int b, int c, int d)
        {
            indices.Add(a); indices.Add(b); indices.Add(c); indices.Add(a); indices.Add(c); indices.Add(d);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, Vector2 tile, float offset)
        {
            int i = Vertices.Count;
            Vertex(a, new Vector2(0,offset), color); Vertex(b, new Vector2(0,tile.y + offset), color);
            Vertex(c, new Vector2(tile.x,tile.y + offset), color); Vertex(d, new Vector2(tile.x,offset), color);
            Faces(i,i+1,i+2,i+3);
        }
        public void Box(Vector3 center, Vector3 size, Quaternion rotation)
        {
            for (int face = 0; face < 6; face++)
            {
                int first = Vertices.Count;
                for (int k = 0; k < 4; k++)
                    Vertex(center + rotation * Vector3.Scale(size, Corners[Sides[face,k]]), Vector2.zero, Color.white);
                Faces(first,first+1,first+2,first+3);
            }
        }
        public void Cone(Vector3 at, float width, float height, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count, b = (i + 1) * Mathf.PI * 2 / count;
                int start = Vertices.Count;
                Vertex(at + Vector3.up * height, Vector2.up, Color.white);
                Vertex(at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * width, Vector2.zero, Color.white);
                Vertex(at + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * width, Vector2.right, Color.white);
                indices.Add(start); indices.Add(start+2); indices.Add(start+1);
            }
        }
        public void Apply()
        {
            mesh.Clear();
            mesh.SetVertices(Vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        }
    }
}
