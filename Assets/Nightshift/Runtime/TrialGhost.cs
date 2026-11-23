using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nightshift
{
    sealed class TrialGhost
    {
        [Serializable]
        struct Sample
        {
            public float time, distance, lateral, heading;

            public Sample(float time, float distance, float lateral, float heading)
            {
                this.time = time;
                this.distance = distance;
                this.lateral = lateral;
                this.heading = heading;
            }
        }

        [Serializable]
        sealed class Record
        {
            public float duration, courseLength;
            public Sample[] samples;
        }

        const int MaxSamples = 3200;
        readonly List<Sample> recording = new List<Sample>(1000);
        readonly DriverRig rig;
        string key;
        Record best;
        float nextSample, length;
        public Transform Root => rig.Root;
        public float BestTime => best?.duration ?? 0;
        public bool HasRecord => best != null;

        public TrialGhost(DriverRig rig) { this.rig = rig; }

        public void Begin(string vehicle, float courseLength)
        {
            nextSample = 0;
            length = courseLength;
            recording.Clear();
            Root.gameObject.SetActive(false);
            key = "nightshift-trial-v1-" + vehicle;
            best = null;
            if (!PlayerPrefs.HasKey(key)) return;
            try
            {
                var record = JsonUtility.FromJson<Record>(PlayerPrefs.GetString(key));
                if (Valid(record)) best = record;
            }
            catch (ArgumentException) { }
        }

        bool Valid(Record record)
        {
            if (record == null || Mathf.Abs(record.courseLength - length) > .01f ||
                record.samples == null || record.samples.Length < 2 || record.samples.Length > MaxSamples ||
                !(record.duration > 0 && record.duration < 330)) return false;
            float previousTime = -1, previousDistance = -1;
            foreach (var sample in record.samples)
            {
                if (!(sample.time > previousTime && sample.distance >= previousDistance &&
                      sample.distance >= 0 && sample.distance <= length &&
                      Mathf.Abs(sample.lateral) < 7 && Mathf.Abs(sample.heading) <= 12)) return false;
                previousTime = sample.time;
                previousDistance = sample.distance;
            }
            return Mathf.Abs(previousTime - record.duration) < .02f && Mathf.Abs(previousDistance - length) < .02f;
        }

        public void Capture(float time, double distance, float x, float heading)
        {
            if (time < nextSample || recording.Count >= MaxSamples - 1) return;
            nextSample = time + .1f;
            recording.Add(new Sample(time, (float)distance, x, heading));
        }

        public bool Finish(float time, float x, float heading)
        {
            if (best != null && best.duration <= time || recording.Count == 0) return false;
            if (recording[recording.Count - 1].time >= time) recording.RemoveAt(recording.Count - 1);
            recording.Add(new Sample(time, length, x, heading));
            var record = new Record { duration = time, courseLength = length, samples = recording.ToArray() };
            if (!Valid(record)) return false;
            best = record;
            PlayerPrefs.SetString(key, JsonUtility.ToJson(record));
            PlayerPrefs.Save();
            return true;
        }

        public void Place(float time, double player, float dt)
        {
            if (best == null || time > best.duration)
            {
                Root.gameObject.SetActive(false);
                return;
            }
            int index = UpperIndex(time, true);
            var a = best.samples[index - 1];
            var b = best.samples[index];
            float t = Mathf.InverseLerp(a.time, b.time, time);
            float distance = Mathf.Lerp(a.distance, b.distance, t);
            float gap = distance - (float)player;
            bool visible = gap > -25 && gap < 150;
            Root.gameObject.SetActive(visible);
            if (!visible) return;
            Root.localPosition = new Vector3(Route.Center(distance) + Mathf.Lerp(a.lateral, b.lateral, t),
                Route.Height(distance) + .075f, gap);
            Root.localRotation = Quaternion.Euler(0, Route.Yaw(distance) + Mathf.Lerp(a.heading, b.heading, t), 0);
            rig.Drive((b.distance - a.distance) / (b.time - a.time), 0, false, false, dt);
        }

        public float TimeDelta(float time, float distance)
        {
            if (best == null) return 0;
            int index = UpperIndex(distance, false);
            var a = best.samples[index - 1];
            var b = best.samples[index];
            return time - Mathf.Lerp(a.time, b.time, Mathf.InverseLerp(a.distance, b.distance, distance));
        }

        int UpperIndex(float value, bool byTime)
        {
            int lo = 1, hi = best.samples.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                var sample = best.samples[mid];
                if ((byTime ? sample.time : sample.distance) < value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
    }
}
