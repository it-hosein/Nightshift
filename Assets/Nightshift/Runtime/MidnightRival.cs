using System.Collections.Generic;
using UnityEngine;

namespace Nightshift
{
    sealed class MidnightRival
    {
        readonly DriverRig rig;
        readonly RivalSpec spec;
        readonly float[] clearance = new float[3];
        float thinkIn, lateral, nitro = 100, cooldown;
        int lane;
        public double Distance { get; private set; } = -27;
        public float X { get; private set; }
        public float Speed { get; private set; } = 54;
        public Transform Root => rig.Root;
        public string Name => spec.name;

        public MidnightRival(DriverRig rig, RivalSpec spec)
        {
            this.rig = rig;
            this.spec = spec;
            lane = 1;
            X = Route.Lane;
        }

        public void Step(float dt, double player, float playerX, float playerSpeed, IReadOnlyList<TrafficObstacle> cars)
        {
            thinkIn -= dt;
            cooldown = Mathf.Max(0, cooldown - dt);
            for (int i = 0; i < clearance.Length; i++) clearance[i] = float.PositiveInfinity;
            float slowest = float.PositiveInfinity;
            foreach (var car in cars)
            {
                if (!car.Active) continue;
                float gap = (float)(car.Distance - Distance);
                if (gap > -spec.clearance && gap < clearance[car.Lane + 1])
                    clearance[car.Lane + 1] = gap;
                if (gap >= -2 && gap < spec.foresight &&
                    Mathf.Abs(X - car.Lane * Route.Lane) < 1.92f)
                {
                    float allowance = Mathf.Max(0, gap - 8) * .75f;
                    slowest = Mathf.Min(slowest, car.TrafficSpeed + allowance);
                }
            }

            float playerGap = (float)(player - Distance);
            if (playerGap > -spec.clearance && Mathf.Abs(playerGap) < 23)
            {
                int playerLane = Mathf.Clamp(Mathf.RoundToInt(playerX / Route.Lane), -1, 1);
                clearance[playerLane + 1] = Mathf.Min(clearance[playerLane + 1], playerGap);
                if (playerGap >= -2 && Mathf.Abs(playerX - X) < 1.92f)
                    slowest = Mathf.Min(slowest, playerSpeed + Mathf.Max(0, playerGap - 9) * .7f);
            }

            if (thinkIn <= 0)
            {
                thinkIn = Mathf.Max(.16f, spec.reactionTime);
                if (clearance[lane + 1] < spec.foresight)
                {
                    int best = lane;
                    for (int candidate = Mathf.Max(-1, lane - 1); candidate <= Mathf.Min(1, lane + 1); candidate++)
                    {
                        if (clearance[candidate + 1] > spec.clearance &&
                            clearance[candidate + 1] > clearance[best + 1] + 5)
                            best = candidate;
                    }
                    lane = best;
                }
            }

            float cruise = spec.pace + Mathf.Clamp(playerGap * .11f, -6, 12);
            bool boost = playerGap > 11 && playerGap < 95 && nitro > 8 && cooldown <= 0 && slowest > cruise + 5;
            if (boost)
            {
                cruise += 12;
                nitro = Mathf.Max(0, nitro - 21 * dt);
                if (nitro < 9) cooldown = 4;
            }
            else nitro = Mathf.Min(100, nitro + 6 * dt);

            float target = Mathf.Min(cruise, slowest);
            Speed = Mathf.Lerp(Speed, target, 1 - Mathf.Exp(-dt * (target < Speed ? 5 : .85f)));
            Distance += Speed * (double)dt;
            lateral += ((lane * Route.Lane - X) * 48 - lateral * 13) * dt;
            X += lateral * dt;
            Position(player, dt, boost);
        }

        public void Position(double player, float dt = 0, bool boost = false)
        {
            Root.localPosition = new Vector3(Route.Center(Distance) + X, Route.Height(Distance) + .034f,
                (float)(Distance - player));
            Root.localRotation = Quaternion.Euler(0, Route.Yaw(Distance) + Mathf.Clamp(lateral, -9, 9), 0);
            rig.Drive(Speed, lateral, boost, false, dt);
        }

        public void Collision() => Speed *= .74f;
    }
}
