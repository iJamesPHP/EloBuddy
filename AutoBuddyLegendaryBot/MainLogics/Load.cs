﻿using System;
using System.Collections.Generic;
using System.Linq;
using AutoBuddy.Humanizers;
using AutoBuddy.Utilities;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using Color = System.Drawing.Color;

namespace AutoBuddy.MainLogics
{
    internal class Load
    {
        private const float waitTime = 40;
        private readonly LogicSelector currentLogic;
        private readonly float startTime;
        private string status = " ";
        public bool waiting;
        private float lastSliderSwitch;
        private bool waitingSlider;

        public Load(LogicSelector c)
        {
            currentLogic = c;
            startTime = Game.Time + waitTime + RandGen.r.NextFloat(-10, 20);
            if (MainMenu.GetMenu("AB").Get<CheckBox>("debuginfo").CurrentValue)
                Drawing.OnDraw += Drawing_OnDraw;
            MainMenu.GetMenu("AB").Get<CheckBox>("reselectlane").OnValueChange += Checkbox_OnValueChange;
            MainMenu.GetMenu("AB").Get<Slider>("lane").OnValueChange += Slider_OnValueChange;
        }


        private void Slider_OnValueChange(ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
        {
            lastSliderSwitch = Game.Time + 1;
            handleSlider();
        }

        private void handleSlider(bool x = true)
        {
            if (waitingSlider && x) return;
            if (lastSliderSwitch > Game.Time)
            {
                waitingSlider = true;
                Core.DelayAction(() => handleSlider(false), (int)((lastSliderSwitch - Game.Time) * 1000) + 50);
            }
            else
                ReselectLane();

        }

        private void Checkbox_OnValueChange(ValueBase<bool> sender, ValueBase<bool>.ValueChangeArgs args)
        {
            ReselectLane();
        }

        private void ReselectLane()
        {
            SetLane();
            waitingSlider = false;
            Chat.Print("Reselecting lane");
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            Drawing.DrawText(250, 70, Color.Gold, "Lane selector status: " + status);
        }

        public void Activate()
        {
        }

        public void SetLane()
        {
            if (MainMenu.GetMenu("AB").Get<Slider>("lane").CurrentValue != 1)
            {
                switch (MainMenu.GetMenu("AB").Get<Slider>("lane").CurrentValue)
                {
                    case 2:
                        SelectLane2(Lane.Top);
                        break;
                    case 3:
                        SelectLane2(Lane.Mid);
                        break;
                    case 4:
                        SelectLane2(Lane.Bot);
                        break;
                }
                return;
            }

            if (ObjectManager.Get<Obj_AI_Turret>().Count() == 24)
            {
                if (AutoWalker.p.Gold < 550 && MainMenu.GetMenu("AB").Get<CheckBox>("mid").CurrentValue)
                {
                    var p =
                        ObjectManager.Get<Obj_AI_Turret>()
                            .First(tur => tur.IsAlly && tur.Name.EndsWith("C_05_A"))
                            .Position;

                    Core.DelayAction(() => SafeFunctions.Ping(PingCategory.OnMyWay, p.Randomized()),
                        RandGen.r.Next(1500, 3000));
                    Core.DelayAction(() => SafeFunctions.SayChat("mid"), RandGen.r.Next(200, 1000));
                    AutoWalker.SetMode(Orbwalker.ActiveModes.Combo);
                    AutoWalker.WalkTo(
                        p.Extend(AutoWalker.MyNexus, 200 + RandGen.r.NextFloat(0, 100)).To3DWorld().Randomized());
                }


                CanSelectLane();
            }
            else
            {
                SelectMostPushedLane();
            }
        }

        public void Deactivate()
        {
        }

        private void CanSelectLane()
        {
            waiting = true;
            status = "Looking for free lane, time left " + (int)(startTime - Game.Time);
            if (Game.Time > startTime || GetChampLanes().All(cl => cl.lane != Lane.Unknown))
            {
                waiting = false;
                SelectLane();
            }
            else
            {
                Core.DelayAction(CanSelectLane, 500);
            }
        }

        private void SelectMostPushedLane()
        {
            status = "selected most pushed lane";
            var nMyNexus = ObjectManager.Get<Obj_HQ>().First(hq => hq.IsEnemy);

            var andrzej =
                ObjectManager.Get<Obj_AI_Minion>()
                    .Where(min => min.Name.Contains("Minion") && min.IsAlly && min.Health > 0)
                    .OrderBy(min => min.Distance(nMyNexus))
                    .First();

            Obj_AI_Base ally =
                ObjectManager.Get<Obj_AI_Turret>()
                    .Where(tur => tur.IsAlly && tur.Health > 0 && tur.GetLane() == andrzej.GetLane())
                    .OrderBy(tur => tur.Distance(andrzej))
                    .FirstOrDefault();
            if (ally == null)
            {
                ally =
                    ObjectManager.Get<Obj_AI_Turret>()
                        .Where(tur => tur.Health > 0 && tur.IsAlly
                                      && tur.GetLane() == Lane.HQ)
                        .OrderBy(tur => tur.Distance(andrzej))
                        .FirstOrDefault();
            }
            if (ally == null)
            {
                ally =
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.GetLane() == Lane.Spawn);
            }

            Obj_AI_Base enemy =
                ObjectManager.Get<Obj_AI_Turret>()
                    .Where(tur => tur.IsEnemy && tur.Health > 0 && tur.GetLane() == andrzej.GetLane())
                    .OrderBy(tur => tur.Distance(andrzej))
                    .FirstOrDefault();
            if (enemy == null)
            {
                enemy =
                    ObjectManager.Get<Obj_AI_Turret>()
                        .Where(tur => tur.Health > 0 && tur.IsEnemy
                                      && tur.GetLane() == Lane.HQ)
                        .OrderBy(tur => tur.Distance(andrzej))
                        .FirstOrDefault();
            }
            if (enemy == null)
            {
                enemy =
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsEnemy && tur.GetLane() == Lane.Spawn);
            }

            currentLogic.pushLogic.Reset(ally, enemy, andrzej.GetLane());
        }

        public void SelectLane2(Lane l)
        {
            status = "selected " + l;
            Obj_AI_Turret ally = null, enemy = null;

            if (l == Lane.Top)
            {
                ally =
                    (ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("L_03_A")) ??
                     ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("L_02_A"))) ??
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("L_01_A"));

                enemy =
                    (ObjectManager.Get<Obj_AI_Turret>()
                        .FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("L_03_A")) ??
                     ObjectManager.Get<Obj_AI_Turret>()
                         .FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("L_02_A"))) ??
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("L_01_A"));
            }
            else if (l == Lane.Bot)
            {
                ally =
                    (ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("R_03_A")) ??
                     ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("R_02_A"))) ??
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("R_01_A"));

                enemy =
                    (ObjectManager.Get<Obj_AI_Turret>()
                        .FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("R_03_A")) ??
                     ObjectManager.Get<Obj_AI_Turret>()
                         .FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("R_02_A"))) ??
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("R_01_A"));
            }
            else if (l == Lane.Mid)
            {
                ally =
                    (ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("C_05_A")) ??
                     ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("C_04_A"))) ??
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.Name.EndsWith("C_03_A"));

                enemy =
                    (ObjectManager.Get<Obj_AI_Turret>()
                        .FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("C_05_A")) ??
                     ObjectManager.Get<Obj_AI_Turret>()
                         .FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("C_04_A"))) ??
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsEnemy && tur.Name.EndsWith("C_03_A"));
            }

            if (ally == null)
                ally = ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.GetLane() == Lane.HQ);
            if (ally == null)
                ally =
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsAlly && tur.GetLane() == Lane.Spawn);

            if (enemy == null)
                enemy = ObjectManager.Get<Obj_AI_Turret>()
                    .FirstOrDefault(tur => tur.IsEnemy && tur.GetLane() == Lane.HQ);
            if (enemy == null)
                enemy =
                    ObjectManager.Get<Obj_AI_Turret>().FirstOrDefault(tur => tur.IsEnemy && tur.GetLane() == Lane.Spawn);

            currentLogic.pushLogic.Reset(ally, enemy, l);
        }

        private void SelectLane()
        {
            status = "selected free lane";
            var list = GetChampLanes();
            if (list.All(cl => cl.lane != Lane.Mid))
            {
                currentLogic.pushLogic.Reset(
                    ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("C_05_A")),
                    ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsEnemy && tur.Name.EndsWith("C_05_A")),
                    Lane.Mid);
                return;
            }
            if (list.Count(cl => cl.lane == Lane.Bot) < 2)
            {
                currentLogic.pushLogic.Reset(
                    ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("R_03_A")),
                    ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsEnemy && tur.Name.EndsWith("R_03_A")),
                    Lane.Bot);
                return;
            }
            if (list.Count(cl => cl.lane == Lane.Top) < 2)
            {
                currentLogic.pushLogic.Reset(
                    ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("L_03_A")),
                    ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsEnemy && tur.Name.EndsWith("L_03_A")),
                    Lane.Top);
            }
        }

        private static List<ChampLane> GetChampLanes(float maxDistance = 2000, float maxDistanceFront = 3000)
        {
            var top1 =
                ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("L_03_A"));
            var top2 =
                ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("L_02_A"));
            var mid1 =
                ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("C_05_A"));
            var mid2 =
                ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("C_04_A"));
            var bot1 =
                ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("R_03_A"));
            var bot2 =
                ObjectManager.Get<Obj_AI_Turret>().First(tur => tur.IsAlly && tur.Name.EndsWith("R_02_A"));

            var ret = new List<ChampLane>();

            foreach (var h in EntityManager.Heroes.Allies.Where(hero => hero.IsAlly && !hero.IsMe))
            {
                var lane = Lane.Unknown;
                if (h.Distance(top1) < maxDistanceFront || h.Distance(top2) < maxDistance) lane = Lane.Top;
                if (h.Distance(mid1) < maxDistanceFront || h.Distance(mid2) < maxDistance) lane = Lane.Mid;
                if (h.Distance(bot1) < maxDistanceFront || h.Distance(bot2) < maxDistance) lane = Lane.Bot;
                ret.Add(new ChampLane(h, lane));
            }
            return ret;
        }
    }
}