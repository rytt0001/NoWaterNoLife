﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;
using Verse.AI;

namespace MizuMod
{
    public class JobDriver_DrawFromWaterNet : JobDriver_DrawWater
    {
        private Building_WaterNetWorkTable WorkTable => job.GetTarget(BillGiverInd).Thing as Building_WaterNetWorkTable;
        private WaterNet WaterNet
        {
            get
            {
                if (WorkTable == null)
                {
                    return null;
                }

                return WorkTable.InputWaterNet;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!base.TryMakePreToilReservations(errorOnFailed))
            {
                return false;
            }

            if (WorkTable == null)
            {
                return false;
            }

            if (WaterNet == null)
            {
                return false;
            }

            return true;
        }

        protected override void SetFailCondition()
        {
        }

        protected override Thing FinishAction()
        {
            var targetWaterType = WaterType.NoWater;

            if (Ext.canDrawFromFaucet)
            {
                // 蛇口の場合
                targetWaterType = WaterNet.StoredWaterTypeForFaucet;
            }
            else
            {
                // 自分自身の場合
                targetWaterType = WorkTable.TankComp.StoredWaterType;
            }

            // 水道網の水の種類から水アイテムの種類を決定
            var waterThingDef = MizuUtility.GetWaterThingDefFromWaterType(targetWaterType);
            if (waterThingDef == null)
            {
                return null;
            }

            // 水アイテムの水源情報を得る
            var compprop = waterThingDef.GetCompProperties<CompProperties_WaterSource>();
            if (compprop == null)
            {
                return null;
            }

            // 水道網から水を減らす
            if (Ext.canDrawFromFaucet)
            {
                // 蛇口の場合
                WaterNet.DrawWaterVolumeForFaucet(compprop.waterVolume * Ext.getItemCount);
            }
            else
            {
                // 自分自身の場合
                WorkTable.TankComp.DrawWaterVolume(compprop.waterVolume * Ext.getItemCount);
            }

            // 水を生成
            var createThing = ThingMaker.MakeThing(waterThingDef);
            if (createThing == null)
            {
                return null;
            }

            // 個数設定
            createThing.stackCount = Ext.getItemCount;
            return createThing;
        }
    }
}
