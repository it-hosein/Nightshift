using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nightshift
{
    public sealed partial class GameManager
    {
        const int TrialRows = 35;
        readonly TrafficObstacle[] trialRows = new TrafficObstacle[TrialRows];
        readonly bool[] trialCleared = new bool[TrialRows];
        DrivingExpansion expansion;
        MidnightRival rival;
        DriverRig rivalRig, ghostRig;
        TrialGhost ghost;
        OvertakeReplay replay;
        int vehicleIndex, rivalIndex = -1, combo;
        bool drafted, duelRivalPassed;
        float comboUntil, rivalContactCooldown;
        double lastPassSoundDistance;

        public RunMode Mode { get; private set; }
        public VehicleSpec Vehicle => expansion.vehicles[vehicleIndex];
        public IReadOnlyList<VehicleSpec> Vehicles => expansion.vehicles;
        public int VehicleIndex => vehicleIndex;
        public float TimeLeft => Mathf.Max(0, expansion.duelDuration - elapsed);
        public float RunTime => elapsed;
        public float TrialLength => expansion.trialDistance;
        public bool HasGhost => ghost.HasRecord;
        public float TrialBest => ghost.BestTime;
        public float GhostDelta => ghost.TimeDelta(elapsed, (float)Distance);
        public float RivalGap => rival == null ? 0 : (float)(Distance - rival.Distance);
        public string RivalName => rival?.Name ?? "RIVAL";
        public bool Drafting => drafted;
        public int Combo => elapsed < comboUntil ? combo : 0;
        public bool HasReplay => replay.Available;
        float Heading => Mathf.Clamp(lateral * 1.26f, -11, 11);

        void PrepareModes()
        {
            rivalRig = NewTrafficRig(2);
            ghostRig = NewTrafficRig(2);
            ghostRig.SetGhostMaterial(expansion.ghostMaterial);
            ghost = new TrialGhost(ghostRig);
            var actors = new List<Transform>
            {
                playerRig, rivalRig.Root, ghostRig.Root
            };
            foreach (var car in cruising.Items) actors.Add(car.VisualRoot);
            foreach (var car in moving.Items) actors.Add(car.VisualRoot);
            foreach (var car in stationary.Items) actors.Add(car.VisualRoot);
            foreach (var pickup in coins.Items) actors.Add(pickup.VisualRoot);
            foreach (var pickup in fuels.Items) actors.Add(pickup.VisualRoot);
            foreach (var pickup in hearts.Items) actors.Add(pickup.VisualRoot);
            replay = new OvertakeReplay(chase, actors.ToArray(), highway, expansion.replayShots);
        }

        public void OpenGarage()
        {
            if (State == GameState.Menu) Change(GameState.Garage);
        }

        public void CloseGarage()
        {
            if (State == GameState.Garage) Change(GameState.Menu);
        }

        public void SelectVehicle(int index)
        {
            if (State != GameState.Garage || index < 0 || index >= Vehicles.Count || vehicleIndex == index) return;
            vehicleIndex = index;
            driver.Equip(art, Vehicle.resourceName, Vehicle.paint);
            PlayerPrefs.SetString("nightshift-garage", Vehicle.id);
            PlayerPrefs.Save();
            hud.Refresh();
        }

        public void StartDuel() => StartMode(RunMode.Duel);
        public void StartTimeTrial() => StartMode(RunMode.TimeTrial);

        void StartMode(RunMode mode)
        {
            if (State != GameState.Menu) return;
            CommitScore();
            Mode = mode;
            record = PlayerPrefs.GetInt(RecordKey, 0);
            if (mode == RunMode.Duel) rivalIndex = (rivalIndex + 1) % expansion.rivals.Length;
            ResetRun();
            Change(GameState.Playing);
        }

        void RestartMode()
        {
            if (State != GameState.Paused && State != GameState.GameOver) return;
            CommitScore();
            if (State == GameState.GameOver && Mode == RunMode.Duel)
                rivalIndex = (rivalIndex + 1) % expansion.rivals.Length;
            ResetRun();
            Change(GameState.Playing);
        }

        void ResetModes()
        {
            Array.Clear(trialRows, 0, trialRows.Length);
            Array.Clear(trialCleared, 0, trialCleared.Length);
            replay.Reset();
            combo = 0;
            comboUntil = 0;
            rivalContactCooldown = 0;
            lastPassSoundDistance = double.NegativeInfinity;
            drafted = duelRivalPassed = false;
            rival = null;
            rivalRig.Root.gameObject.SetActive(Mode == RunMode.Duel);
            ghostRig.Root.gameObject.SetActive(false);
            if (Mode == RunMode.Duel)
            {
                var spec = expansion.rivals[rivalIndex];
                rivalRig.Equip(art, spec.vehicle.resourceName, spec.vehicle.paint);
                rival = new MidnightRival(rivalRig, spec);
                rival.Position(0);
            }
            if (Mode == RunMode.TimeTrial)
            {
                ghostRig.Equip(art, Vehicle.resourceName, Vehicle.paint);
                ghostRig.SetGhostMaterial(expansion.ghostMaterial);
                ghost.Begin(Vehicle.id, expansion.trialDistance);
                ghost.Capture(0, 0, 0, 0);
            }
        }

        void PlaceTrialTraffic(float dt)
        {
            // Clear retired assignments before another row can take a pooled car.
            for (int i = 0; i < trialRows.Length; i++)
            {
                if (trialRows[i] == null || trialRows[i].Active) continue;
                trialCleared[i] = true;
                trialRows[i] = null;
            }
            // Use the absolute course clock, independent of the player's speed or lane.
            for (int i = 0; i < trialRows.Length; i++)
            {
                if (trialCleared[i]) continue;
                var item = trialRows[i];
                int rowLane = (i * 2 + i / 5) % 3 - 1;
                float carSpeed = 14 + i % 5 * 2;
                double position = 83 + i * 99 + carSpeed * (double)elapsed;
                double range = position - Distance;
                if (range < -24)
                {
                    item?.Release();
                    trialRows[i] = null;
                    trialCleared[i] = true;
                    continue;
                }
                if (range > 540) continue;
                if (item == null)
                {
                    item = moving.Take() ?? cruising.Take();
                    if (item == null) continue;
                    trialRows[i] = item;
                    item.Spawn(position - carSpeed * .017, rowLane, carSpeed);
                }
                item.SetTrialPose(position, Distance, dt);
                if (item.Active) trafficOrder.Add(item);
            }
        }

        void AdvanceModes(float dt, double old, float oldX)
        {
            if (rival != null)
            {
                float previousGap = (float)(rival.Distance - old);
                rival.Step(dt, Distance, x, Speed, trafficOrder);
                float gap = (float)(rival.Distance - Distance);
                rivalContactCooldown -= dt;
                if (rivalContactCooldown <= 0 && Mathf.Abs((oldX + x) * .5f - rival.X) < 1.76f &&
                    (Mathf.Abs(gap) < 3.7f || previousGap * gap < 0))
                {
                    rivalContactCooldown = 1.3f;
                    rival.Collision();
                    Impact();
                    if (State != GameState.Playing) return;
                }
                else if (!duelRivalPassed && previousGap >= 0 && gap < 0)
                {
                    duelRivalPassed = true;
                    RegisterPass(Mathf.RoundToInt(rival.X / Route.Lane), true, rival.Root.position);
                }
            }

            drafted = false;
            if (!boosting && !braking)
            {
                foreach (var car in trafficOrder)
                {
                    if (car.Active && DraftWindow(car.Distance, car.Lane * Route.Lane)) { drafted = true; break; }
                }
                drafted |= rival != null && DraftWindow(rival.Distance, rival.X);
                if (drafted) Nitro = Mathf.Min(100, Nitro + expansion.draftCharge * dt);
            }

            if (Mode == RunMode.Duel && TimeLeft <= .001f)
            {
                string result = RivalGap > 1 ? "Duel won" : RivalGap < -1 ? "Duel lost" : "Photo finish";
                if (RivalGap > 1) score += 500;
                End(result);
            }
            else if (Mode == RunMode.TimeTrial)
            {
                if (Distance >= expansion.trialDistance)
                {
                    float fraction = (float)((expansion.trialDistance - old) / (Distance - old));
                    elapsed -= dt * (1 - fraction);
                    bool trialRecord = ghost.Finish(elapsed, Mathf.Lerp(oldX, x, fraction), Heading);
                    Distance = expansion.trialDistance;
                    End(trialRecord ? "New personal best" : "Time trial finished");
                }
                else
                {
                    ghost.Capture(elapsed, Distance, x, Heading);
                    ghost.Place(elapsed, Distance, dt);
                }
            }
        }

        bool DraftWindow(double target, float targetX)
        {
            double gap = target - Distance;
            return gap >= 7 && gap <= 32 && Mathf.Abs(x - targetX) < 1.12f;
        }

        void RegisterPass(int otherLane, bool opponent, Vector3 position)
        {
            float offset = Mathf.Abs(x - otherLane * Route.Lane);
            bool near = offset >= 1.78f && offset < 4.2f && hitTime <= 0.02f;
            if (near)
            {
                combo = Mathf.Min(5, Combo + 1);
                comboUntil = elapsed + expansion.comboWindow;
            }
            int points = (opponent ? 125 : 32) * (near ? combo : 1);
            score += points;
            if (near || opponent)
            {
                Message = opponent ? RivalName.ToUpperInvariant() + " OVERTAKEN"
                    : $"CLEAN PASS  ×{combo}   +{points}";
                toast = 1.3f;
                if (Distance - lastPassSoundDistance > 5)
                {
                    audioRig.Pass(position, Speed);
                    lastPassSoundDistance = Distance;
                }
            }
            replay.MarkPass((opponent ? 5000 : 0) + (near ? 500 + combo * 100 : 0) + Mathf.FloorToInt(Speed));
        }

        public void StartReplay()
        {
            if (State != GameState.GameOver || !HasReplay) return;
            effects.Clear();
            hud.SetImpact(0);
            replay.Play(Distance);
            Change(GameState.Replay);
        }

        public void StopReplay()
        {
            if (State != GameState.Replay) return;
            replay.Stop();
            Change(GameState.GameOver);
        }
    }
}
