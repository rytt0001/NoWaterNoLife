﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using RimWorld;
using Verse;

namespace MizuMod
{
    public class Need_Water : Need
    {
        public const float NeedWaterVolumePerDay = 1.5f;
        public const float DrinkFromBuildingMargin = 1.5f;
        public const float MinWaterAmountPerOneDrink = 0.3f;

        private DefExtension_NeedWater extInt;
        private DefExtension_NeedWater Ext
        {
            get
            {
                if (extInt == null)
                {
                    extInt = def.GetModExtension<DefExtension_NeedWater>();
                }
                return extInt;
            }
        }

        private bool isSetRaceThirstRate = false;
        private float raceThirstRate = 1f;
        private float RaceThirstRate
        {
            get
            {
                if (isSetRaceThirstRate == false)
                {
                    isSetRaceThirstRate = true;
                    var raceThirstExt = pawn.def.GetModExtension<DefExtension_RaceThirstRate>();
                    raceThirstRate = (raceThirstExt == null) ? pawn.RaceProps.baseHungerRate : raceThirstExt.baseThirstRate;
                }
                return raceThirstRate;
            }
        }

        public int lastSearchWaterTick;

        public ThirstCategory CurCategory
        {
            get
            {
                if (CurLevelPercentage <= 0f)
                {
                    return ThirstCategory.Dehydration;
                }
                if (CurLevelPercentage < PercentageThreshUrgentlyThirsty)
                {
                    return ThirstCategory.UrgentlyThirsty;
                }
                if (CurLevelPercentage < PercentageThreshThirsty)
                {
                    return ThirstCategory.Thirsty;
                }
                if (CurLevelPercentage < PercentageThreshSlightlyThirsty)
                {
                    return ThirstCategory.SlightlyThirsty;
                }
                return ThirstCategory.Healthy;
            }
        }

        public bool Dehydrating => CurCategory == ThirstCategory.Dehydration;

        public float PercentageThreshUrgentlyThirsty => Ext.urgentlyThirstyBorder;

        public float PercentageThreshThirsty => Ext.thirstyBorder;

        public float PercentageThreshSlightlyThirsty => Ext.slightlyThirstyBorder;

        public float WaterWanted => MaxLevel - CurLevel;

        public float WaterFallPerTick => WaterFallPerTickAssumingCategory(CurCategory);

        public float WaterAmountBetweenThirstyAndHealthy => (1f - PercentageThreshThirsty) * MaxLevel;

        public int TicksUntilThirstyWhenHealthy => Mathf.CeilToInt(WaterAmountBetweenThirstyAndHealthy / WaterFallPerTick);

        public override int GUIChangeArrow => -1;

        public override float MaxLevel => pawn.BodySize * pawn.ageTracker.CurLifeStage.foodMaxFactor;

        public Need_Water(Pawn pawn) : base(pawn)
        {
            lastSearchWaterTick = Find.TickManager.TicksGame;
        }

        private float WaterFallPerTickAssumingCategory(ThirstCategory cat)
        {
            // 基本低下量(基本値＋温度補正)
            var fallPerTickBase = Ext.fallPerTickBase + Ext.fallPerTickFromTempCurve.Evaluate(pawn.AmbientTemperature - pawn.ComfortableTemperatureRange().max);

            // 食事と同じ値を利用
            fallPerTickBase *= pawn.ageTracker.CurLifeStage.hungerRateFactor * RaceThirstRate * pawn.health.hediffSet.GetThirstRateFactor();

            switch (cat)
            {
                case ThirstCategory.Healthy:
                    return fallPerTickBase;
                case ThirstCategory.SlightlyThirsty:
                    return fallPerTickBase;
                case ThirstCategory.Thirsty:
                    return fallPerTickBase * 0.5f;
                case ThirstCategory.UrgentlyThirsty:
                    return fallPerTickBase * 0.25f;
                case ThirstCategory.Dehydration:
                    return fallPerTickBase * 0.15f;
                default:
                    return 999f;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look<int>(ref lastSearchWaterTick, "lastSearchWaterTick");
        }

        public override void SetInitialLevel()
        {
            if (pawn.RaceProps.Humanlike)
            {
                CurLevelPercentage = 0.8f;
            }
            else
            {
                CurLevelPercentage = Rand.Range(0.5f, 0.8f);
            }
            if (Current.ProgramState == ProgramState.Playing)
            {
                lastSearchWaterTick = Find.TickManager.TicksGame;
            }
        }

        public override void DrawOnGUI(Rect rect, int maxThresholdMarkers = int.MaxValue, float customMargin = -1F, bool drawArrows = true, bool doTooltip = true)
        {
            if (threshPercents == null)
            {
                threshPercents = new List<float>()
                {
                    PercentageThreshUrgentlyThirsty,
                    PercentageThreshThirsty,
                    PercentageThreshSlightlyThirsty,
                };
            }

            base.DrawOnGUI(rect, maxThresholdMarkers, customMargin, drawArrows, doTooltip);
        }

        public override void NeedInterval()
        {
            if (pawn.RaceProps.IsMechanoid)
            {
                return;
            }

            if (base.IsFrozen)
            {
                return;
            }

            // 水分要求低下
            CurLevel -= WaterFallPerTick * 150f * MizuDef.GlobalSettings.forDebug.needWaterReduceRate;

            var directionFactor = -1;
            if (Dehydrating)
            {
                // 脱水症状深刻化方向に変更
                directionFactor = 1;
            }

            // 脱水症状進行度更新
            HealthUtility.AdjustSeverity(
                pawn,
                MizuDef.Hediff_Dehydration,
                directionFactor * Ext.dehydrationSeverityPerDay / 150 * MizuDef.GlobalSettings.forDebug.needWaterReduceRate);
        }

        public override string GetTipString()
        {
            return string.Concat(new string[]
            {
                base.LabelCap,
                ": ",
                base.CurLevelPercentage.ToStringPercent(),
                " (",
                CurLevel.ToString("0.##"),
                " / ",
                MaxLevel.ToString("0.##"),
                ")\n",
                def.description
            });
        }
    }
}
