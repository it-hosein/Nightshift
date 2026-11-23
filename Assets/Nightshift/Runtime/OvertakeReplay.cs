using Unity.Cinemachine;
using UnityEngine;

namespace Nightshift
{
    sealed class OvertakeReplay
    {
        const int SampleRate = 24;
        const int Before = 54;
        const int After = 22;

        struct ActorPose
        {
            public bool active;
            public Vector3 position;
            public Quaternion rotation;

            public ActorPose(Transform actor)
            {
                active = actor.gameObject.activeSelf;
                position = actor.localPosition;
                rotation = actor.localRotation;
            }

            public void Apply(Transform actor)
            {
                actor.SetLocalPositionAndRotation(position, rotation);
                actor.gameObject.SetActive(active);
            }
        }

        sealed class Frame
        {
            public double distance;
            public readonly ActorPose[] poses;
            public Frame(int count) { poses = new ActorPose[count]; }
        }

        readonly Camera camera;
        readonly CinemachineBrain brain;
        readonly CinemachineCamera shot;
        readonly Transform wheel, roadside;
        readonly Transform[] actors;
        readonly Frame[] ring, pending, highlight;
        readonly ActorPose[] parked;
        readonly Highway highway;
        int write, count, rank, candidate, pendingCount, highlightCount;
        double parkedDistance;
        float lastSample, cursor, originalFov;
        Vector3 originalPosition;
        Quaternion originalRotation;
        public bool Available => highlightCount > 1;

        public OvertakeReplay(Camera camera, Transform[] actors, Highway highway, GameObject shotPrefab)
        {
            this.camera = camera;
            this.actors = actors;
            this.highway = highway;
            ring = Buffer(Before, actors.Length);
            pending = Buffer(Before + After, actors.Length);
            highlight = Buffer(Before + After, actors.Length);
            parked = new ActorPose[actors.Length];

            brain = camera.gameObject.AddComponent<CinemachineBrain>();
            brain.enabled = false;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
            var rig = Object.Instantiate(shotPrefab);
            wheel = rig.transform.Find("Wheel");
            roadside = rig.transform.Find("Roadside");
            rig.SetActive(false);
            shot = rig.AddComponent<CinemachineCamera>();
        }

        static Frame[] Buffer(int size, int actors)
        {
            var frames = new Frame[size];
            for (int i = 0; i < size; i++) frames[i] = new Frame(actors);
            return frames;
        }

        public void Reset()
        {
            write = count = rank = candidate = pendingCount = highlightCount = 0;
            lastSample = -1;
        }

        public void Capture(float time, double distance)
        {
            if (time < lastSample + 1f / SampleRate - .002f) return;
            lastSample = time;
            var tick = ring[write];
            tick.distance = distance;
            for (int i = 0; i < actors.Length; i++) tick.poses[i] = new ActorPose(actors[i]);
            write = (write + 1) % ring.Length;
            count = Mathf.Min(count + 1, ring.Length);

            if (candidate == 0) return;
            Copy(tick, pending[pendingCount++]);
            if (pendingCount == pending.Length) Seal();
        }

        public void MarkPass(int merit)
        {
            if (merit <= rank || merit <= candidate || count < 6) return;
            candidate = merit;
            pendingCount = 0;
            for (int n = 0; n < count; n++)
                Copy(ring[(write - count + n + ring.Length) % ring.Length], pending[pendingCount++]);
        }

        public void Seal()
        {
            if (candidate == 0) return;
            rank = candidate;
            highlightCount = pendingCount;
            for (int i = 0; i < highlightCount; i++) Copy(pending[i], highlight[i]);
            candidate = pendingCount = 0;
        }

        static void Copy(Frame source, Frame destination)
        {
            destination.distance = source.distance;
            System.Array.Copy(source.poses, destination.poses, source.poses.Length);
        }

        public void Play(double distance)
        {
            if (!Available) return;
            for (int i = 0; i < actors.Length; i++) parked[i] = new ActorPose(actors[i]);
            parkedDistance = distance;
            originalPosition = camera.transform.position;
            originalRotation = camera.transform.rotation;
            originalFov = camera.fieldOfView;
            cursor = 0;
            Present();
            shot.gameObject.SetActive(true);
            brain.enabled = true;
        }

        public bool Tick(float delta)
        {
            cursor += delta * SampleRate * .77f;
            if (cursor >= highlightCount - 1) return false;
            Present();
            return true;
        }

        void Present()
        {
            int index = Mathf.Min((int)cursor, highlightCount - 2);
            float blend = cursor - index;
            var a = highlight[index];
            var b = highlight[index + 1];
            highway.Update(a.distance + (b.distance - a.distance) * blend);
            for (int i = 0; i < actors.Length; i++)
            {
                var pose = a.poses[i];
                var next = b.poses[i];
                if (pose.active && next.active && (pose.position - next.position).sqrMagnitude < 100)
                {
                    pose.position = Vector3.Lerp(pose.position, next.position, blend);
                    pose.rotation = Quaternion.Slerp(pose.rotation, next.rotation, blend);
                }
                pose.Apply(actors[i]);
            }

            var player = actors[0];
            Transform angle = cursor < highlightCount * .51f ? roadside : wheel;
            Vector3 position = player.TransformPoint(angle.localPosition);
            Vector3 target = player.TransformPoint(new Vector3(0, .72f, 2.7f));
            shot.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            var lens = shot.Lens;
            lens.FieldOfView = angle == wheel ? 62 : 51;
            shot.Lens = lens;
        }

        public void Stop()
        {
            brain.enabled = false;
            shot.gameObject.SetActive(false);
            highway.Update(parkedDistance);
            for (int i = 0; i < actors.Length; i++) parked[i].Apply(actors[i]);
            camera.transform.SetPositionAndRotation(originalPosition, originalRotation);
            camera.fieldOfView = originalFov;
        }

        public void Dispose() => Object.Destroy(shot.gameObject);
    }
}
