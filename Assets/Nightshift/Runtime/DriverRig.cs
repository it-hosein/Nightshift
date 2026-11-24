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
        readonly struct Wheel
        {
            public readonly Transform pivot;
            public readonly Vector3 axis;
            public readonly Quaternion restingRotation;
            public readonly float radius;
            public Wheel(Transform pivot, Vector3 axis, float radius)
            {
                this.pivot = pivot;
                this.axis = axis;
                restingRotation = pivot.localRotation;
                this.radius = radius;
            }
        }

        readonly List<Wheel> wheels = new List<Wheel>(4);
        readonly List<Renderer> paintwork = new List<Renderer>();
        readonly MaterialPropertyBlock lampProperties = new MaterialPropertyBlock();
        readonly MeshRenderer[] tailReflections;
        readonly Transform[] headBeams;
        readonly List<Renderer> tailLights = new List<Renderer>(2);
        readonly MeshRenderer courseGlow;
        readonly Light courseLamp;
        readonly bool player;
        Transform modelRoot;
        float pitch, roll;
        public double WheelTravel { get; private set; }

        public DriverRig(Transform root, NightMaterials art, bool player, int palette, VehicleSpec vehicle = null)
        {
            this.player = player;
            Root = root;
            shell = Root.Find("Suspension");
            if (player)
            {
                headBeams = new[] { Root.Find("Left LED headlight"), Root.Find("Right LED headlight") };
                foreach (var beam in headBeams)
                {
                    beam.SetParent(shell, false);
                    var lamp = beam.GetComponent<Light>();
                    lamp.color = new Color(.91f,.94f,1);
                    lamp.renderMode = LightRenderMode.ForcePixel;
                }
                Root.Find("Left headlight reflection").gameObject.SetActive(false);
                Root.Find("Right headlight reflection").gameObject.SetActive(false);
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
            WheelTravel = 0;
            tailLights.Clear();
            paintwork.Clear();
            pitch = roll = 0;
            shell.localRotation = Quaternion.identity;
            var model = Resources.Load<GameObject>("Nightshift/" + name);
            if (model == null) throw new InvalidOperationException($"Missing imported vehicle: {name}.");
            var car = new GameObject(name + " visual rig").transform;
            car.SetParent(shell, false);
            modelRoot = car;
            var modelInstance = UnityEngine.Object.Instantiate(model, car).transform;
            modelInstance.localPosition = Vector3.zero;
            var original = modelInstance.GetComponentsInChildren<Renderer>(true);
            var details = Resources.Load<GameObject>("Nightshift/VehicleDetails/" + name);
            if (details != null)
            {
                var instance = UnityEngine.Object.Instantiate(details, car);
                instance.transform.localPosition = Vector3.zero;
                bool separateWheels = name == "BMW" || name == "Porsche" || name == "Bugatti" || name == "Lamborghini";
                if (separateWheels)
                    foreach (var renderer in original)
                    {
                        string piece = renderer.name;
                        if (piece == "Rim" || piece == "Wheel" || (name == "Bugatti" && piece == "Metal"))
                            renderer.enabled = false;
                    }
            }

            var lenses = Resources.Load<GameObject>("Nightshift/VehicleLamps/" + name);
            if (lenses != null)
            {
                var module = UnityEngine.Object.Instantiate(lenses, car);
                module.transform.localPosition = Vector3.zero;
            }
            foreach (var renderer in car.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name.StartsWith("Detail Head", StringComparison.OrdinalIgnoreCase))
                    renderer.enabled = false;
            }

            Transform fl = FindPart(car, "wheel_fl");
            Transform fr = FindPart(car, "wheel_fr");
            Transform rl = FindPart(car, "wheel_rl");
            Transform rr = FindPart(car, "wheel_rr");
            var renderers = Array.FindAll(car.GetComponentsInChildren<Renderer>(true), renderer => renderer.enabled);
            if (renderers.Length == 0) throw new InvalidOperationException($"No renderers in {name}.");
            AlignCar(car, renderers, fl, fr, rl, rr);

            Bounds bounds = Measure(renderers, shell);
            float scale = player ? Mathf.Clamp(4.55f / Mathf.Max(.1f, bounds.size.z), .1f, 100) : 1;
            car.localScale *= scale;
            car.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            AddWheel(fl);
            AddWheel(fr);
            AddWheel(rl);
            AddWheel(rr);
            PositionHeadBeams(car);

            foreach (var renderer in renderers)
            {
                string part = renderer.name.ToLowerInvariant();
                if (part == "body" || part == "paint" || part.StartsWith("paint ", StringComparison.Ordinal)) paintwork.Add(renderer);
                else if (part == "glass") renderer.sharedMaterial = art.Glass;
                else if (part == "lights_red" || part == "taillights" || part.StartsWith("detail tail", StringComparison.Ordinal)
                    || (name == "Car" && part == "brakes"))
                {
                    renderer.sharedMaterial = player ? art.PlayerTaillight : art.TrafficTaillight;
                    if (player) tailLights.Add(renderer);
                }
                else if (part.StartsWith("headlamp emitter", StringComparison.Ordinal) || part.EndsWith("dipped projector", StringComparison.Ordinal))
                    renderer.sharedMaterial = player ? art.PlayerHeadlight : art.TrafficHeadlight;
                else if (part == "headlights" || part == "lights")
                    renderer.sharedMaterial = art.VehicleTrim;
                else if (part == "rim" || part.StartsWith("rim_", StringComparison.Ordinal)
                    || part.StartsWith("detail metal", StringComparison.Ordinal) || part == "detail fixed rim")
                    renderer.sharedMaterial = art.Metal;
                else if (part.StartsWith("detail rubber", StringComparison.Ordinal) || part == "detail fixed wheel")
                    renderer.sharedMaterial = art.Rubber;
                else if (part == "trim" || part == "detail fixed metal")
                    renderer.sharedMaterial = art.VehicleTrim;
            }
            SetPaint(art.Paints[palette % art.Paints.Length]);
        }

        void PositionHeadBeams(Transform car)
        {
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "L" : "R";
                var emitter = FindPart(car, "Headlamp Emitter " + side)?.GetComponent<Renderer>();
                if (emitter == null) continue;
                Vector3 socket = shell.InverseTransformPoint(RendererCenter(emitter));
                float sign = Mathf.Sign(socket.x);
                if (player)
                {
                    var beam = headBeams[sign < 0 ? 0 : 1];
                    beam.localPosition = socket + new Vector3(0, 0, .045f);
                    beam.localRotation = Quaternion.LookRotation(new Vector3(sign * .045f, -socket.y / 13, 1));
                }
                else
                {
                    var reflection = Root.Find((sign < 0 ? "Left" : "Right") + " headlight reflection");
                    if (reflection != null)
                    {
                        reflection.localPosition = new Vector3(socket.x, .034f, socket.z + 5.3f);
                        reflection.localScale = new Vector3(2.72f, 10.5f, 1);
                    }
                }
            }
        }

        public void SetPaint(Material paint)
        {
            foreach (var renderer in paintwork) renderer.sharedMaterial = paint;
        }

        static Transform FindPart(Transform model, string name)
        {
            foreach (var child in model.GetComponentsInChildren<Transform>(true))
                if (child.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        void AddWheel(Transform pivot)
        {
            if (pivot == null) return;
            var pieces = pivot.GetComponentsInChildren<Renderer>(true);
            if (pieces.Length == 0) return;
            Bounds bounds = Measure(pieces, shell);
            float radius = Mathf.Clamp((bounds.size.y + bounds.size.z) * .25f, .20f, .46f);
            wheels.Add(new Wheel(pivot, pivot.InverseTransformDirection(shell.right), radius));
        }

        static Vector3 RendererCenter(Renderer renderer) => renderer.transform.TransformPoint(renderer.localBounds.center);

        static Bounds Measure(Renderer[] renderers, Transform space)
        {
            var result = new Bounds(space.InverseTransformPoint(RendererCenter(renderers[0])), Vector3.zero);
            foreach (var renderer in renderers)
            {
                Bounds bounds = renderer.localBounds;
                Matrix4x4 toRig = space.worldToLocalMatrix * renderer.localToWorldMatrix;
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

        void AlignCar(Transform car, Renderer[] renderers, Transform fl, Transform fr, Transform rl, Transform rr)
        {
            if (fl != null && fr != null && rl != null && rr != null)
            {
                Vector3 forward = (fl.position + fr.position) - (rl.position + rr.position);
                Vector3 across = (fr.position + rr.position) - (fl.position + rl.position);
                Vector3 up = Vector3.Cross(forward, across);
                Vector3 center = (fl.position + fr.position + rl.position + rr.position) * .25f;
                var body = Array.Find(renderers, r => r.name.Equals("Body", StringComparison.OrdinalIgnoreCase)
                    || r.name.Equals("Paint", StringComparison.OrdinalIgnoreCase));
                if (body != null && Vector3.Dot(up, RendererCenter(body) - center) < 0) up = -up;
                if (forward.sqrMagnitude > .000001f && up.sqrMagnitude > .000001f)
                {
                    car.rotation = shell.rotation * Quaternion.Inverse(Quaternion.LookRotation(forward, up)) * car.rotation;
                    return;
                }
            }

            var headlights = Array.Find(renderers, r => r.name.Equals("Headlights", StringComparison.OrdinalIgnoreCase));
            var taillights = Array.Find(renderers, r => r.name.Equals("Taillights", StringComparison.OrdinalIgnoreCase));
            if (headlights != null && taillights != null &&
                shell.InverseTransformPoint(RendererCenter(headlights)).z < shell.InverseTransformPoint(RendererCenter(taillights)).z)
                car.localRotation = Quaternion.AngleAxis(180, Vector3.up) * car.localRotation;
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
            if (dt > 0) ApplyWheelTravel(WheelTravel + speed * (double)dt);
            if (!player) return;
            foreach (var reflection in tailReflections)
            {
                Color tint = reflection.sharedMaterial.color;
                tint.a = brake ? .74f : .40f;
                SetColor(reflection, "_Color", tint);
            }
            foreach (var light in tailLights)
                SetColor(light, "_EmissionColor", new Color(1, .04f, .08f) * (brake ? 7.45f : 4.6f));
        }

        public void ApplyWheelTravel(double meters)
        {
            WheelTravel = meters;
            foreach (var wheel in wheels)
            {
                float angle = (float)((meters / wheel.radius * Mathf.Rad2Deg) % 360);
                wheel.pivot.localRotation = wheel.restingRotation * Quaternion.AngleAxis(angle, wheel.axis);
            }
        }

        void SetColor(Renderer renderer, string property, Color color)
        {
            renderer.GetPropertyBlock(lampProperties);
            lampProperties.SetColor(property, color);
            renderer.SetPropertyBlock(lampProperties);
        }
    }
}
