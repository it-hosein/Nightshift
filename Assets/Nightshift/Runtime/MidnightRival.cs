using System.Collections.Generic;
using UnityEngine;

namespace Nightshift
{
    sealed class MidnightRival
    {
        readonly DriverRig rig;
        readonly RivalSpec spec;
        readonly DuelTuning tuning;
        readonly float[] clearance = new float[3];
        float thinkIn, lateral, recovery, cooldown;
        public float Nitro { get; private set; } = 100;
        public bool Boosting { get; private set; }
        int lane;
        public double Distance { get; private set; } = -27;
        public float X { get; private set; }
        public float Speed { get; private set; } = 54;
        public Transform Root => rig.Root;
        public string Name => spec.name;

        public MidnightRival(DriverRig rig, RivalSpec spec, DuelTuning tuning)
        {
            this.rig = rig;
            this.spec = spec;
            this.tuning = tuning;
            lane = 1;
            X = Route.Lane;
        }

        public void Step(float dt, double player, float playerX, float playerSpeed, IReadOnlyList<TrafficObstacle> cars)
        {
            thinkIn -= dt;
            recovery = Mathf.Max(0, recovery - dt);
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

            if (thinkIn <= 0 && recovery <= 0)
            {
                thinkIn = Mathf.Max(.16f, spec.reactionTime * tuning.reactionScale);
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

            float cruise = spec.pace + tuning.paceOffset + Mathf.Clamp(playerGap * .11f, -6, 12);
            Boosting = recovery <= 0 && playerGap > tuning.boostGap && playerGap < 105 &&
                Nitro > 7 && cooldown <= 0 && slowest > cruise + 5;
            if (Boosting)
            {
                cruise += tuning.boostGain;
                Nitro = Mathf.Max(0, Nitro - (spec.vehicle != null ? spec.vehicle.nitroUse : 21) * dt);
                if (Nitro <= 7) cooldown = tuning.boostCooldown;
            }
            else if (recovery <= 0) Nitro = Mathf.Min(100, Nitro + tuning.recharge * dt);

            float target = Mathf.Min(cruise, slowest);
            float response = target < Speed ? 5 : recovery > 0 ? .26f : .85f;
            Speed = Mathf.Lerp(Speed, target, 1 - Mathf.Exp(-dt * response));
            Distance += Speed * (double)dt;
            lateral += ((lane * Route.Lane - X) * (recovery > 0 ? 9 : 48) - lateral * (recovery > 0 ? 5 : 13)) * dt;
            X = Mathf.Clamp(X + lateral * dt, -DuelContact.RoadLimit, DuelContact.RoadLimit);
            Position(player, dt, Boosting);
        }

        public void Position(double player, float dt = 0, bool boost = false)
        {
            Root.localPosition = new Vector3(Route.Center(Distance) + X, Route.Height(Distance) + .034f,
                (float)(Distance - player));
            Root.localRotation = Quaternion.Euler(0, Route.Yaw(Distance) + Mathf.Clamp(lateral, -9, 9), 0);
            rig.Drive(Speed, lateral, boost, false, dt);
        }

        public void Collision(DuelContact contact)
        {
            Speed = Mathf.Max(14, Speed - contact.RivalLoss);
            lateral = Mathf.Clamp(lateral + contact.RivalKick, -20, 20);
            recovery = contact.RivalRecovery;
            cooldown = Mathf.Max(cooldown, recovery + .25f);
            Boosting = false;
        }
    }
}
