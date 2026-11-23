using UnityEngine;
using UnityEngine.Rendering;

namespace Nightshift
{
    public sealed class NightMaterials : ScriptableObject
    {
        public Material Asphalt, Markings, Marker, Metal, Concrete, ColdLamp, Land, Tree, Water;
        public Material AmberGlow, BlueGlow, Fire;
        public Material TrafficHeadlight, TrafficTaillight, PlayerHeadlight, PlayerTaillight;
        public Material Rubber, Glass;
        public Material[] Paints;
        public GameObject TrafficRig, HazardLights, Coin, Fuel, Heart, StreetLamp, TunnelLight, RouteSign, BridgeSign;

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
    }
}
