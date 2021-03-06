﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;

namespace MizuMod
{
    public class Building_WaterBox : Building_WaterNetWorkTable, IBuilding_WaterNet, IBuilding_DrinkWater
    {
        private readonly List<float> graphicThreshold = new List<float>()
        {
            0.05f,
            0.35f,
            0.65f,
            0.95f,
            100f,
        };

        private int graphicIndex = 0;
        private int prevGraphicIndex = 0;

        public override Graphic Graphic => MizuGraphics.LinkedWaterBoxes[graphicIndex].GetColoredVersion(MizuGraphics.WaterBoxes[graphicIndex].Shader, DrawColor, DrawColorTwo);

        public WaterType WaterType
        {
            get
            {
                if (TankComp == null)
                {
                    return WaterType.Undefined;
                }

                return TankComp.StoredWaterType;
            }
        }

        public float WaterVolume
        {
            get
            {
                if (TankComp == null)
                {
                    return 0f;
                }

                return TankComp.StoredWaterVolume;
            }
        }

        public bool IsEmpty
        {
            get
            {
                if (TankComp == null)
                {
                    return true;
                }

                if (TankComp.StoredWaterVolume <= 0f)
                {
                    return true;
                }

                return false;
            }
        }

        public bool CanDrinkFor(Pawn p)
        {
            if (p.needs == null || p.needs.Water() == null)
            {
                return false;
            }

            if (TankComp == null)
            {
                return false;
            }

            if (TankComp.StoredWaterType == WaterType.Undefined || TankComp.StoredWaterType == WaterType.NoWater)
            {
                return false;
            }

            // タンクの水量が十分にある
            return TankComp.StoredWaterVolume >= p.needs.Water().WaterWanted * Need_Water.DrinkFromBuildingMargin;
        }

        public bool CanDrawFor(Pawn p)
        {
            if (TankComp == null)
            {
                return false;
            }

            if (TankComp.StoredWaterType == WaterType.Undefined || TankComp.StoredWaterType == WaterType.NoWater)
            {
                return false;
            }

            var waterItemDef = MizuDef.List_WaterItem.First((def) => def.GetCompProperties<CompProperties_WaterSource>().waterType == TankComp.StoredWaterType);
            var compprop = waterItemDef.GetCompProperties<CompProperties_WaterSource>();

            // 汲める予定の水アイテムの水の量より多い
            return p.CanManipulate() && TankComp.StoredWaterVolume >= compprop.waterVolume;
        }

        public void DrawWater(float amount)
        {
            if (TankComp == null)
            {
                return;
            }

            TankComp.DrawWaterVolume(amount);
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look<int>(ref graphicIndex, "graphicIndex");
            prevGraphicIndex = graphicIndex;
        }

        public override void Tick()
        {
            base.Tick();

            prevGraphicIndex = graphicIndex;
            if (TankComp == null)
            {
                graphicIndex = 0;
                return;
            }

            for (var i = 0; i < graphicThreshold.Count; i++)
            {
                if (TankComp.StoredWaterVolumePercent < graphicThreshold[i])
                {
                    graphicIndex = i;
                    break;
                }
            }

            if (graphicIndex != prevGraphicIndex)
            {
                DirtyMapMesh(Map);
            }
        }
    }
}
