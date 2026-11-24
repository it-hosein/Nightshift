using UnityEngine;

namespace Nightshift
{
    sealed class DuelContact
    {
        public const float RoadLimit = 4.9f;
        public const float CarSpacing = 4.25f;
        public const float CarWidth = 1.84f;

        public readonly float PlayerKick, RivalKick, PlayerLoss, RivalSpeedChange, RivalRecovery;
        public readonly bool RearHit, Longitudinal;
        public readonly float SeparationSide;

        DuelContact(float offset, float approach, bool rear, bool longitudinal)
        {
            RearHit = rear;
            Longitudinal = longitudinal;
            SeparationSide = Mathf.Sign(offset);
            float side = Mathf.Clamp(offset * 1.65f, -1, 1);
            float force = Mathf.Clamp(approach * .46f, 2.8f, 9f);
            PlayerKick = -side * force;
            RivalKick = side * force * (rear ? .38f : .76f);
            PlayerLoss = Mathf.Clamp(approach * (rear ? .64f : .42f), 2.5f, 17f);
            RivalSpeedChange = rear ? Mathf.Clamp(approach * .42f, 2, 13) : -force * .34f;
            RivalRecovery = rear ? .40f : .57f;
        }

        public static DuelContact Resolve(float oldGap, float gap, float oldSide, float side, float dt)
        {
            if (!Sweep(oldGap, gap, CarSpacing, out float startZ, out float endZ) ||
                !Sweep(oldSide, side, CarWidth, out float startX, out float endX)) return null;
            float first = Mathf.Max(0, Mathf.Max(startZ, startX));
            float last = Mathf.Min(1, Mathf.Min(endZ, endX));
            if (first > last) return null;

            float offset = Mathf.Lerp(oldSide, side, first);
            float extent = Mathf.Lerp(oldGap, gap, first);
            bool crossing = oldGap * gap < 0;
            bool longitudinal = crossing || (startZ >= startX && Mathf.Abs(extent) > 1.95f);
            bool rear = longitudinal && oldGap >= 0 && oldGap > gap;
            float change = longitudinal ? Mathf.Abs(gap - oldGap) : Mathf.Abs(side - oldSide);
            return new DuelContact(offset, change / Mathf.Max(.001f, dt), rear, longitudinal);
        }

        static bool Sweep(float before, float after, float extent, out float first, out float last)
        {
            float change = after - before;
            if (Mathf.Abs(change) < .0001f)
            {
                first = float.NegativeInfinity;
                last = float.PositiveInfinity;
                return Mathf.Abs(before) <= extent;
            }
            float a = (-extent - before) / change;
            float b = (extent - before) / change;
            first = Mathf.Min(a, b);
            last = Mathf.Max(a, b);
            return true;
        }
    }
}
