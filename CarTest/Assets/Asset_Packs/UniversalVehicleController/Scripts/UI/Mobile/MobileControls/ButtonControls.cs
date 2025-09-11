using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PG.UI
{
    public class ButtonControls :BaseControls
    {
        [Header ("Buttons")]
        public ButtonCustom AccelerationBtn;
        public ButtonCustom BrakeReverseBtn;
        public ButtonCustom LeftSteerBtn;
        public ButtonCustom RightSteerBtn;
        public ButtonCustom HandBrakeBtn;
        public ButtonCustom BoostBtn;

        public override void Init (CarControllerInput userInput)
        {
            base.Init (userInput);

            AccelerationBtn.OnPointerDownAction += (() => UserInput.SetAcceleration (1));
            AccelerationBtn.OnPointerUpAction += (() => UserInput.SetAcceleration (0));

            BrakeReverseBtn.OnPointerDownAction += (() => UserInput.SetBrakeReverse (1));
            BrakeReverseBtn.OnPointerUpAction += (() => UserInput.SetBrakeReverse (0));

            LeftSteerBtn.OnPointerEnterAction += (() => UserInput.SetSteer (-1));
            LeftSteerBtn.OnPointerExitAction += (() => UserInput.SetSteer (0));

            RightSteerBtn.OnPointerEnterAction += (() => UserInput.SetSteer (1));
            RightSteerBtn.OnPointerExitAction += (() => UserInput.SetSteer (0));

            HandBrakeBtn.OnPointerEnterAction += (() => UserInput.SetHandBrake (true));
            HandBrakeBtn.OnPointerExitAction += (() => UserInput.SetHandBrake (false));

            BoostBtn.OnPointerEnterAction += (() => UserInput.SetBoost (true));
            BoostBtn.OnPointerExitAction += (() => UserInput.SetBoost (false));
        }
    }
}
