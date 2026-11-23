using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nightshift
{
    public sealed class DriverRig
    {
        public Transform Root { get; }
        public Transform ExhaustMount => shell;
        static readonly string[] TrafficModels = { "Porsche", "Lamborghini", "Bugatti", "BMW" };
        readonly Transform shell;
        readonly List<Transform> wheels = new List<Transform>(4);
        readonly MaterialPropertyBlock lampProperties = new MaterialPropertyBlock();
        readonly MeshRenderer[] tailReflections;
        readonly List<Renderer> tailLights = new List<Renderer>(2);
        readonly MeshRenderer courseGlow;
        readonly Light courseLamp;
        readonly bool player;
        Transform modelRoot;
        float pitch, roll;

        public DriverRig(Transform root, NightMaterials art, bool player, int palette, VehicleSpec vehicle = null)
        {
            this.player = player;
            Root = root;
            shell = Root.Find("Suspension");
            if (player)
            {
                tailReflections = new[] { Root.Find("Left tail reflection").GetComponent<MeshRenderer>(),
                    Root.Find("Right tail reflection").GetComponent<MeshRenderer>() };
                courseGlow = Root.Find("Course underglow").GetComponent<MeshRenderer>();
                courseLamp = Root.Find("Course underbody light").GetComponent<Light>();
            }

            string fallback = player ? "Car" : TrafficModels[(palette - 1) % TrafficModels.Length];
            Equip(art, vehicle != null ? vehicle.resourceName : fallback, vehicle != null ? vehicle.paint : palette);
        }

        public void Equip(NightMaterials art, string name, int palette)
        {
            if (modelRoot != null)
            {
                modelRoot.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(modelRoot.gameObject);
            }
            wheels.Clear();
            tailLights.Clear();
            pitch = roll = 0;
            shell.localRotation = Quaternion.identity;
            var model = Resources.Load<GameObject>("Nightshift/" + name);
            if (model == null) throw new InvalidOperationException($"Missing imported vehicle: {name}.");
            var car = UnityEngine.Object.Instantiate(model, shell).transform;
            modelRoot = car;
            car.localPosition = Vector3.zero;
            var renderers = car.GetComponentsInChildren<Renderer>(true);
            foreach (var t in car.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Equals("wheel_fl", StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("wheel_fr", StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("wheel_rl", StringComparison.OrdinalIgnoreCase) ||
                    t.name.Equals("wheel_rr", StringComparison.OrdinalIgnoreCase)) wheels.Add(t);
            }
            if (name == "Car")
                AlignFerrari(car, renderers);
            else
            {
                var headlights = Array.Find(renderers, r => r.name == "Headlights");
                var taillights = Array.Find(renderers, r => r.name == "Taillights");
                if (headlights != null && taillights != null &&
                    shell.InverseTransformPoint(RendererCenter(headlights)).z < shell.InverseTransformPoint(RendererCenter(taillights)).z)
                    car.localRotation *= Quaternion.Euler(0, 180, 0);
            }
            if (renderers.Length == 0) throw new InvalidOperationException($"No renderers in {name}.");
            Bounds bounds = Measure(renderers);
            foreach (var r in renderers)
            {
                string n = r.name.ToLowerInvariant();
                if (n == "body" || n == "paint") r.sharedMaterial = art.Paints[palette % art.Paints.Length];
                else if (n == "glass") r.sharedMaterial = art.Glass;
                else if (n == "lights_red" || n == "taillights")
                {
                    r.sharedMaterial = player ? art.PlayerTaillight : art.TrafficTaillight;
                    if (player) tailLights.Add(r);
                }
                else if (n == "trim") r.sharedMaterial = art.Rubber;
                else if (!player && (n == "headlights" || n == "lights")) r.sharedMaterial = art.TrafficHeadlight;
                else if (n == "headlights") r.sharedMaterial = art.PlayerHeadlight;
            }
            float scale = player ? Mathf.Clamp(4.55f / Mathf.Max(.1f, bounds.size.z), .1f, 100) : 1;
            car.localScale *= scale;
            car.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
        }

        static Vector3 RendererCenter(Renderer renderer) => renderer.transform.TransformPoint(renderer.localBounds.center);

        Bounds Measure(Renderer[] renderers)
        {
            var result = new Bounds(shell.InverseTransformPoint(RendererCenter(renderers[0])), Vector3.zero);
            foreach (var renderer in renderers)
            {
                Bounds bounds = renderer.localBounds;
                Matrix4x4 toRig = shell.worldToLocalMatrix * renderer.localToWorldMatrix;
                foreach (int sx in Signs.Values)
                foreach (int sy in Signs.Values)
                foreach (int sz in Signs.Values)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(sx, sy, sz));
                    result.Encapsulate(toRig.MultiplyPoint3x4(corner));
                }
            }
            return result;
        }

        public void SetGhostMaterial(Material material)
        {
            for (int i = 0; i < Root.childCount; i++)
                Root.GetChild(i).gameObject.SetActive(Root.GetChild(i) == shell);

            foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = material;
                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
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
                if (Vector3.Dot(up, RendererCenter(renderer) - axleCenter) < 0) up = -up;
                car.rotation = shell.rotation * Quaternion.Inverse(Quaternion.LookRotation(forward, up)) * car.rotation;
                break;
            }
        }

        public void SetCourseUnderglow(bool enabled, float speed)
        {
            if (!player) return;
            courseGlow.gameObject.SetActive(enabled);
            courseLamp.enabled = enabled;
            if (!enabled) return;
            float factor = Mathf.Clamp01(speed / 90);
            var color = courseGlow.sharedMaterial.color;
            color.a = Mathf.Lerp(.19f,.78f,factor);
            SetColor(courseGlow, "_Color", color);
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
            foreach (var reflection in tailReflections)
            {
                Color tint = reflection.sharedMaterial.color;
                tint.a = brake ? .74f : .40f;
                SetColor(reflection, "_Color", tint);
            }
            foreach (var light in tailLights)
                SetColor(light, "_EmissionColor", new Color(1, .04f, .08f) * (brake ? 7.45f : 3.85f));
        }

        void SetColor(Renderer renderer, string property, Color color)
        {
            renderer.GetPropertyBlock(lampProperties);
            lampProperties.SetColor(property, color);
            renderer.SetPropertyBlock(lampProperties);
        }
    }
}
