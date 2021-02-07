﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using Verse.AI;
using RimWorld;

namespace MizuMod
{
    public class JobDriver_GetSnow : JobDriver
    {
        private float workDone;

        private const TargetIndex GetSnowCellIndex = TargetIndex.A;

        private const float GetWorkPerSnowDepth = 100f;

        private float TotalNeededWork => GetWorkPerSnowDepth * WorkGiver_GetSnow.ConsumeSnowPerOne;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 移動
            yield return Toils_Goto.GotoCell(GetSnowCellIndex, PathEndMode.Touch);

            // 雪を集める
            var getToil = new Toil();
            getToil.tickAction = () =>
            {
                var actor = getToil.actor;
                var statValue = actor.GetStatValue(StatDefOf.WorkSpeedGlobal, true);
                var num = statValue;
                workDone += num;
                if (workDone >= WorkGiver_GetSnow.ConsumeSnowPerOne * 100f)
				{
                    var snowDepth = Map.snowGrid.GetDepth(TargetLocA);
                    snowDepth = Math.Max(0f, snowDepth - WorkGiver_GetSnow.ConsumeSnowPerOne);
                    Map.snowGrid.SetDepth(TargetLocA, snowDepth);
                    ReadyForNextToil();
                    return;
                }
            };
            getToil.defaultCompleteMode = ToilCompleteMode.Never;
            getToil.WithEffect(EffecterDefOf.ClearSnow, GetSnowCellIndex);
            getToil.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
            getToil.WithProgressBar(GetSnowCellIndex, () => workDone / TotalNeededWork, true, -0.5f);
            getToil.FailOnCannotTouch(GetSnowCellIndex, PathEndMode.Touch);
            yield return getToil;

            // 雪玉生成
            var makeToil = new Toil();
            makeToil.initAction = () =>
            {
                var actor = makeToil.actor;
                var snowThing = ThingMaker.MakeThing(MizuDef.Thing_Snowball);
                snowThing.stackCount = 1;

                if (!GenPlace.TryPlaceThing(snowThing, actor.Position, actor.Map, ThingPlaceMode.Near, null))
                {
                    Log.Error(string.Concat(new object[]
                    {
                        actor,
                        " could not drop recipe product ",
                        snowThing,
                        " near ",
                        TargetLocA
                    }));
                }
            };
            makeToil.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return makeToil;
        }
    }
}
