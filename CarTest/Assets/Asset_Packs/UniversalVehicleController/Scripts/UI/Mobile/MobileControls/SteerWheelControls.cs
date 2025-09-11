using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG.UI
{
    public class SteerWheelControls :BaseControls
    {
        public SteerWheelUI SteerWheelUI;
        [Header ("Buttons")]
        public ButtonCustom AccelerationBtn;
        public ButtonCustom BrakeReverseBtn;
        public ButtonCustom HandBrakeBtn;
        public ButtonCustom BoostBtn;

        public override void Init (CarControllerInput userInput)
        {
            base.Init (userInput);

            SteerWheelUI.Init (userInput);

            AccelerationBtn.OnPointerDownAction += (() => UserInput.SetAcceleration (1));
            AccelerationBtn.OnPointerUpAction += (() => UserInput.SetAcceleration (0));

            BrakeReverseBtn.OnPointerDownAction += (() => UserInput.SetBrakeReverse (1));
            BrakeReverseBtn.OnPointerUpAction += (() => UserInput.SetBrakeReverse (0));

            HandBrakeBtn.OnPointerEnterAction += (() => UserInput.SetHandBrake (true));
            HandBrakeBtn.OnPointerExitAction += (() => UserInput.SetHandBrake (false));

            BoostBtn.OnPointerEnterAction += (() => UserInput.SetBoost (true));
            BoostBtn.OnPointerExitAction += (() => UserInput.SetBoost (false));
        }

        private void Update ()
        {
            UserInput.SetSteer (SteerWheelUI.HorizontalControl);
        }
    }
}
