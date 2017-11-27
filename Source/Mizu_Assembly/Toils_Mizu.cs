﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using RimWorld;
using Verse;
using Verse.AI;

namespace MizuMod
{
    public static class Toils_Mizu
    {
        public static T FailOnChangingTerrain<T>(this T f, TargetIndex index, List<WaterTerrainType> waterTerrainTypeList) where T : IJobEndable
        {
            f.AddEndCondition(() =>
            {
                Thing thing = f.GetActor().jobs.curJob.GetTarget(index).Thing;
                TerrainDef terrainDef = thing.Map.terrainGrid.TerrainAt(thing.Position);
                if (!waterTerrainTypeList.Contains(terrainDef.GetWaterTerrainType()))
                {
                    return JobCondition.Incompletable;
                }
                return JobCondition.Ongoing;
            });
            return f;
        }

        public static Toil DoRecipeWorkDrawing(TargetIndex billGiverIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                JobDriver_DoBill jobDriver_DoBill = (JobDriver_DoBill)actor.jobs.curDriver;

                jobDriver_DoBill.workLeft = curJob.bill.recipe.WorkAmountTotal(null);
                jobDriver_DoBill.billStartTick = Find.TickManager.TicksGame;
                jobDriver_DoBill.ticksSpentDoingRecipeWork = 0;

                curJob.bill.Notify_DoBillStarted(actor);
            };
            toil.tickAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                JobDriver_DoBill jobDriver_DoBill = (JobDriver_DoBill)actor.jobs.curDriver;

                jobDriver_DoBill.ticksSpentDoingRecipeWork++;
                curJob.bill.Notify_PawnDidWork(actor);

                IBillGiverWithTickAction billGiverWithTickAction = actor.CurJob.GetTarget(billGiverIndex).Thing as IBillGiverWithTickAction;
                if (billGiverWithTickAction != null)
                {
                    // 設備の時間経過処理
                    billGiverWithTickAction.UsedThisTick();
                }

                // 工数を進める処理
                float num = (curJob.RecipeDef.workSpeedStat != null) ? actor.GetStatValue(curJob.RecipeDef.workSpeedStat, true) : 1f;
                Building_WorkTable building_WorkTable = jobDriver_DoBill.BillGiver as Building_WorkTable;
                if (building_WorkTable != null)
                {
                    num *= building_WorkTable.GetStatValue(StatDefOf.WorkTableWorkSpeedFactor, true);
                }
                if (DebugSettings.fastCrafting)
                {
                    num *= 30f;
                }
                jobDriver_DoBill.workLeft -= num;

                // 椅子から快適さを得る
                actor.GainComfortFromCellIfPossible();

                // 完了チェック
                if (jobDriver_DoBill.workLeft <= 0f)
                {
                    jobDriver_DoBill.ReadyForNextToil();
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.WithEffect(() => toil.actor.CurJob.bill.recipe.effectWorking, billGiverIndex);
            toil.PlaySustainerOrSound(() => toil.actor.CurJob.bill.recipe.soundWorking);
            toil.WithProgressBar(billGiverIndex, delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.CurJob;
                return 1f - ((JobDriver_DoBill)actor.jobs.curDriver).workLeft / curJob.bill.recipe.WorkAmountTotal(null);
            }, false, -0.5f);
            toil.FailOn(() => toil.actor.CurJob.bill.suspended);
            return toil;
        }

        public static Toil FinishRecipeAndStartStoringProduct(Func<Thing> makeRecipeProduct)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                JobDriver_DoBill jobDriver_DoBill = (JobDriver_DoBill)actor.jobs.curDriver;

                // 経験値取得
                if (curJob.RecipeDef.workSkill != null)
                {
                    float xp = (float)jobDriver_DoBill.ticksSpentDoingRecipeWork * 0.11f * curJob.RecipeDef.workSkillLearnFactor;
                    actor.skills.GetSkill(curJob.RecipeDef.workSkill).Learn(xp, false);
                }

                // 生産物の生成
                Thing thing = makeRecipeProduct();
                if (thing == null)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded, true);
                    return;
                }
                
                curJob.bill.Notify_IterationCompleted(actor, null);
                RecordsUtility.Notify_BillDone(actor, new List<Thing>() { thing });

                // 床置き指定
                if (curJob.bill.GetStoreMode() == BillStoreModeDefOf.DropOnFloor)
                {
                    if (!GenPlace.TryPlaceThing(thing, actor.Position, actor.Map, ThingPlaceMode.Near, null))
                    {
                        Log.Error(string.Concat(new object[]
                        {
                            actor,
                            " could not drop recipe product ",
                            thing,
                            " near ",
                            actor.Position
                        }));
                    }
                    actor.jobs.EndCurrentJob(JobCondition.Succeeded, true);
                    return;
                }

                // 最適な倉庫まで持っていく
                thing.SetPositionDirect(actor.Position);
                IntVec3 c;
                if (StoreUtility.TryFindBestBetterStoreCellFor(thing, actor, actor.Map, StoragePriority.Unstored, actor.Faction, out c, true))
                {
                    actor.carryTracker.TryStartCarry(thing);
                    curJob.targetA = thing;
                    curJob.targetB = c;
                    curJob.count = 99999;
                    return;
                }
                if (!GenPlace.TryPlaceThing(thing, actor.Position, actor.Map, ThingPlaceMode.Near, null))
                {
                    Log.Error(string.Concat(new object[]
                    {
                        "Bill doer could not drop product ",
                        thing,
                        " near ",
                        actor.Position
                    }));
                }
                actor.jobs.EndCurrentJob(JobCondition.Succeeded, true);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public static Toil StartCarryFromInventory(TargetIndex thingIndex)
        {
            // 水(食事)を持ち物から取り出す
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(thingIndex).Thing;
                if (actor.inventory != null && thing != null)
                {
                    actor.inventory.innerContainer.Take(thing);
                    actor.carryTracker.TryStartCarry(thing);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.FailOnDestroyedOrNull(thingIndex);
            return toil;
        }

        public static Toil StartPathToDrinkSpot(TargetIndex thingIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                IntVec3 intVec = IntVec3.Invalid;

                intVec = RCellFinder.SpotToChewStandingNear(actor, actor.CurJob.GetTarget(thingIndex).Thing);
                actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, intVec);
                actor.pather.StartPath(intVec, PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }

        private static Toil DrinkSomeone(TargetIndex thingIndex, Func<Toil, Func<LocalTargetInfo>> funcGetter)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Thing thing = actor.CurJob.GetTarget(thingIndex).Thing;
                CompWater comp = thing.TryGetComp<CompWater>();
                if (comp == null)
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable, true);
                    return;
                }
                actor.rotationTracker.FaceCell(actor.Position);
                if (!thing.CanDrinkWaterNow())
                {
                    actor.jobs.EndCurrentJob(JobCondition.Incompletable, true);
                    return;
                }
                actor.jobs.curDriver.ticksLeftThisToil = CompProperties_Water.BaseDrinkTicks;
                if (thing.Spawned)
                {
                    thing.Map.physicalInteractionReservationManager.Reserve(actor, actor.CurJob, thing);
                }
            };
            toil.tickAction = delegate
            {
                toil.actor.GainComfortFromCellIfPossible();
            };
            toil.WithProgressBar(thingIndex, delegate
            {
                Pawn actor = toil.actor;
                Thing thing = actor.CurJob.GetTarget(thingIndex).Thing;
                if (thing == null)
                {
                    return 1f;
                }
                return 1f - (float)toil.actor.jobs.curDriver.ticksLeftThisToil / (float)CompProperties_Water.BaseDrinkTicks;
            }, false, -0.5f);
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.FailOnDestroyedOrNull(thingIndex);
            toil.AddFinishAction(delegate
            {
                Pawn actor = toil.actor;
                if (actor == null)
                {
                    return;
                }
                if (actor.CurJob == null)
                {
                    return;
                }
                Thing thing = actor.CurJob.GetTarget(thingIndex).Thing;
                if (thing == null)
                {
                    return;
                }
                if (actor.Map.physicalInteractionReservationManager.IsReservedBy(actor, thing))
                {
                    actor.Map.physicalInteractionReservationManager.Release(actor, actor.CurJob, thing);
                }
            });

            // エフェクト追加
            toil.WithEffect(delegate
            {
                Pawn actor = toil.actor;
                LocalTargetInfo target = toil.actor.CurJob.GetTarget(thingIndex);
                if (!target.HasThing)
                {
                    return null;
                }
                EffecterDef effecter = null;
                CompWater comp = target.Thing.TryGetComp<CompWater>();
                if (comp != null)
                {
                    effecter = comp.GetEffect;
                }
                return effecter;
            }, funcGetter(toil));
            toil.PlaySustainerOrSound(delegate
            {
                Pawn actor = toil.actor;
                if (!actor.RaceProps.Humanlike)
                {
                    return null;
                }
                LocalTargetInfo target = toil.actor.CurJob.GetTarget(thingIndex);
                if (!target.HasThing)
                {
                    return null;
                }
                CompWater comp = target.Thing.TryGetComp<CompWater>();
                if (comp == null)
                {
                    return null;
                }
                return comp.Props.getSound;
            });
            return toil;
        }

        public static Toil Drink(TargetIndex thingIndex)
        {
            return Toils_Mizu.DrinkSomeone(thingIndex, (toil) =>
            {
                return () =>
                {
                    if (!toil.actor.CurJob.GetTarget(thingIndex).HasThing)
                    {
                        return null;
                    }
                    return toil.actor.CurJob.GetTarget(thingIndex).Thing;
                };
            });
        }

        public static Toil FeedToPatient(TargetIndex thingIndex, TargetIndex patientIndex)
        {
            return Toils_Mizu.DrinkSomeone(thingIndex, (toil) =>
            {
                return () =>
                {
                    if (!toil.actor.CurJob.GetTarget(patientIndex).HasThing) return null;

                    var patient = toil.actor.CurJob.GetTarget(patientIndex).Thing as Pawn;
                    if (patient == null) return null;

                    return patient;
                };
            });
        }

        private static Toil FinishDrinkSomeone(TargetIndex thingIndex, Func<Toil, Pawn> pawnGetter)
        {
            Toil toil = new Toil();
            toil.initAction = () =>
            {
                Thing thing = toil.actor.jobs.curJob.GetTarget(thingIndex).Thing;
                Pawn getter = pawnGetter(toil);
                if (getter == null) return;

                float wantedWaterAmount = getter.needs.water().WaterWanted;
                float gotWaterAmount = MizuUtility.GetWater(getter, thing, wantedWaterAmount);
                if (!getter.Dead)
                {
                    getter.needs.water().CurLevel += gotWaterAmount;
                }
                getter.records.AddTo(MizuDef.Record_WaterDrank, gotWaterAmount);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public static Toil FinishDrink(TargetIndex thingIndex)
        {
            return Toils_Mizu.FinishDrinkSomeone(thingIndex, (toil) =>
            {
                return toil.actor;
            });
        }

        public static Toil FinishDrinkPatient(TargetIndex thingIndex, TargetIndex patientIndex)
        {
            return Toils_Mizu.FinishDrinkSomeone(thingIndex, (toil) =>
            {
                if (!toil.actor.CurJob.GetTarget(patientIndex).HasThing) return null;

                return toil.actor.CurJob.GetTarget(patientIndex).Thing as Pawn;
            });
        }

        public static Toil DrinkTerrain(TargetIndex thingIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                actor.rotationTracker.FaceCell(actor.Position);
                actor.jobs.curDriver.ticksLeftThisToil = CompProperties_Water.BaseDrinkTicks;
            };
            toil.tickAction = delegate
            {
                toil.actor.GainComfortFromCellIfPossible();
            };
            toil.WithProgressBar(thingIndex, delegate
            {
                return 1f - (float)toil.actor.jobs.curDriver.ticksLeftThisToil / (float)CompProperties_Water.BaseDrinkTicks;
            }, false, -0.5f);
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.FailOn((t) =>
            {
                Pawn actor = toil.actor;
                return actor.CurJob.targetA.Cell.IsForbidden(actor) || !actor.CanReach(actor.CurJob.targetA.Cell, PathEndMode.OnCell, Danger.Deadly);
            });

            // エフェクト追加
            toil.PlaySustainerOrSound(delegate
            {
                return DefDatabase<SoundDef>.GetNamed("Ingest_Beer");
            });
            return toil;
        }

        public static Toil FinishDrinkTerrain(TargetIndex terrainVecIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Need_Water need_water = actor.needs.water();

                float numWater = need_water.MaxLevel - need_water.CurLevel;

                TerrainDef terrain = actor.Map.terrainGrid.TerrainAt(actor.CurJob.GetTarget(terrainVecIndex).Cell);
                WaterTerrainType drankTerrainType = terrain.GetWaterTerrainType();

                if (actor.needs.mood != null)
                {
                    // 地面から直接飲んだ
                    actor.needs.mood.thoughts.memories.TryGainMemory(MizuDef.Thought_DrankWaterDirectly);

                    ThoughtDef thoughtDef = MizuUtility.GetThoughtDefFromTerrainType(drankTerrainType);
                    if (thoughtDef != null)
                    {
                        // 水の種類による心情
                        actor.needs.mood.thoughts.memories.TryGainMemory(thoughtDef);
                    }
                }

                if (drankTerrainType == WaterTerrainType.SeaWater)
                {
                    // 海水の場合の健康状態悪化
                    actor.health.AddHediff(HediffMaker.MakeHediff(MizuDef.Hediff_DrankSeaWater, actor));
                }

                if (!actor.Dead)
                {
                    actor.needs.water().CurLevel += numWater;
                }
                actor.records.AddTo(MizuDef.Record_WaterDrank, numWater);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        public static Toil DropCarriedThing(TargetIndex prisonerIndex, TargetIndex dropSpotIndex)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;

                // そもそも何も運んでいない
                if (actor.carryTracker == null || actor.carryTracker.CarriedThing == null) return;

                // ターゲットが場所ではなく物
                if (actor.CurJob.GetTarget(dropSpotIndex).HasThing) return;

                Thing dropThing = null;

                // その場に置いてみる
                bool isDropSuccess = actor.carryTracker.TryDropCarriedThing(actor.CurJob.GetTarget(dropSpotIndex).Cell, ThingPlaceMode.Direct, out dropThing);

                if (!isDropSuccess)
                {
                    // その場に置けなかったら近くに置いてみる
                    isDropSuccess = actor.carryTracker.TryDropCarriedThing(actor.CurJob.GetTarget(dropSpotIndex).Cell, ThingPlaceMode.Near, out dropThing);
                }

                // その場or近くに置けなかった
                if (!isDropSuccess) return;

                if (actor.Map.reservationManager.ReservedBy(dropThing, actor))
                {
                    // 持ってる人に予約されているなら、解放する
                    actor.Map.reservationManager.Release(dropThing, actor, actor.CurJob);
                }

                // 相手が囚人でない可能性
                if (!actor.CurJob.GetTarget(prisonerIndex).HasThing) return;

                Pawn prisoner = actor.CurJob.GetTarget(prisonerIndex).Thing as Pawn;

                // 囚人がポーンではない
                if (prisoner == null) return;

                // 置いた水を囚人に予約させる
                prisoner.Reserve(dropThing, actor.CurJob);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.atomicWithPrevious = true;
            return toil;
        }
    }
}