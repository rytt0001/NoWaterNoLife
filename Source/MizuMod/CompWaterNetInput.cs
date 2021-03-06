﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace MizuMod
{
    public class CompWaterNetInput : CompWaterNet
    {
        public new CompProperties_WaterNetInput Props => (CompProperties_WaterNetInput)props;

        public virtual float MaxInputWaterFlow => Props.maxInputWaterFlow;
        //public virtual CompProperties_WaterNetInput.InputType InputType
        //{
        //    get
        //    {
        //        return this.Props.inputType;
        //    }
        //}
        public virtual List<CompProperties_WaterNetInput.InputType> InputTypes => Props.inputTypes;
        public virtual CompProperties_WaterNetInput.InputWaterFlowType InputWaterFlowType => Props.inputWaterFlowType;
        public virtual List<WaterType> AcceptWaterTypes => Props.acceptWaterTypes;
        public virtual float BaseRainFlow => Props.baseRainFlow;
        public virtual float RoofEfficiency => Props.roofEfficiency;
        public virtual int RoofDistance => Props.roofDistance;

        // 水道網から流し込まれる水量
        // Maxを超えていることもある
        public float InputWaterFlow { get; set; }
        public WaterType InputWaterType { get; set; }

        private bool HasTank => TankComp != null;
        public bool IsReceiving =>
                // 水道網から入力するタイプで、現在の入力量が0ではない⇒入力中
                InputTypes.Contains(CompProperties_WaterNetInput.InputType.WaterNet) && InputWaterFlow > 0f;

        // 入力機能が働いているか
        public override bool IsActivated
        {
            get
            {
                var isOK = base.IsActivated && IsActivatedForWaterNet && (!HasTank || TankComp.AmountCanAccept > 0.0f);
                if (InputWaterFlowType == CompProperties_WaterNetInput.InputWaterFlowType.Constant)
                {
                    isOK &= InputWaterFlow >= MaxInputWaterFlow;
                }
                return isOK;
            }
        }

        public CompWaterNetInput() : base()
        {
            InputWaterType = WaterType.NoWater;
        }

        public override string CompInspectStringExtra()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(base.CompInspectStringExtra());

            if (stringBuilder.ToString() != string.Empty)
            {
                stringBuilder.AppendLine();
            }
            stringBuilder.Append(MizuStrings.InspectWaterFlowInput.Translate() + ": " + InputWaterFlow.ToString("F2") + " L/day");
            stringBuilder.Append(string.Concat(new string[]
            {
                "(",
                MizuStrings.GetInspectWaterTypeString(InputWaterType),
                ")",
            }));

            return stringBuilder.ToString();
        }
    }
}
