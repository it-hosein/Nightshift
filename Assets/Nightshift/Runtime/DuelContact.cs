using UnityEngine;

namespace Nightshift
{
    sealed class DuelContact
    {
        public const float RoadLimit = 4.9f;
        const float HalfLength = 3.85f;
        const float HalfWidth = 1.82f;

        public readonly float PlayerKick, RivalKick, PlayerLoss, RivalLoss, RivalRecovery;

        DuelContact(float direction, float severity, bool rearHit, bool frontHit)
        {
            PlayerKick = -direction * severity * (rearHit ? .62f : 1);
            RivalKick = direction * severity * (rearHit ? 1.42f : 1);
            PlayerLoss = severity * (frontHit ? 1.35f : rearHit ? .65f : .48f);
            RivalLoss = severity * (rearHit ? 1.7f : frontHit ? .58f : .56f);
            RivalRecovery = rearHit ? 1.05f : .54f;
        }

        public static DuelContact Resolve(float oldGap, float gap, float oldSide, float side)
        {
            float first = 0, last = 1;
            if (!Sweep(oldGap, gap, HalfLength, ref first, ref last) ||
                !Sweep(oldSide, side, HalfWidth, ref first, ref last)) return null;

            float extent = Mathf.Lerp(oldGap, gap, first);
            float offset = Mathf.Lerp(oldSide, side, first);
            float direction = Mathf.Abs(offset) > .04f ? Mathf.Sign(offset)
                : Mathf.Clamp((oldSide - side) * 7, -.45f, .45f);
            bool rearHit = extent > 1.8f && oldGap > gap;
            bool frontHit = extent < -1.8f && oldGap < gap;
            float closing = Mathf.Abs(gap - oldGap);
            float severity = Mathf.Lerp(6.8f, 11.0f, Mathf.Clamp01(closing * 2 + Mathf.Abs(side - oldSide)));
            return new DuelContact(direction, severity, rearHit, frontHit);
        }

        static bool Sweep(float before, float after, float extent, ref float first, ref float last)
        {
            float change = after - before;
            if (Mathf.Abs(change) < .0001f) return Mathf.Abs(before) <= extent;
            float a = (-extent - before) / change;
            float b = (extent - before) / change;
            first = Mathf.Max(first, Mathf.Min(a, b));
            last = Mathf.Min(last, Mathf.Max(a, b));
            return first <= last;
        }
    }
}
