﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Verse;
using Verse.AI;

namespace MizuMod
{
    public class JobDriver_DrawWaterByPrisoner : JobDriver
    {
        private const int DrawTicks = 500;
        private const TargetIndex DrawerIndex = TargetIndex.A;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            var drawer = job.targetA.Thing;
            PathEndMode peMode = drawer.def.hasInteractionCell ? PathEndMode.InteractionCell : PathEndMode.ClosestTouch;

            yield return Toils_Goto.GotoThing(DrawerIndex, peMode);

            yield return Toils_Mizu.DrawWater(DrawerIndex, DrawTicks);

            yield return Toils_Mizu.FinishDrawWater(DrawerIndex);
        }
    }
}
