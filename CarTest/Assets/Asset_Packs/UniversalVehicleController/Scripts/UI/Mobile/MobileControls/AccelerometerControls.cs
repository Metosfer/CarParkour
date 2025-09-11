using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PG.UI
{
    /// <summary>
    /// Mobile input accelerometer.
    /// </summary>
    public class AccelerometerControls :BaseControls
    {
        [Header ("Accelerometer settings")]
        public float DeadZone = 5f;
        public float MaxAngle = 45f;
        public float AccelerometerLerpSpeed = 500;

        [Header ("Buttons")]
        public ButtonCustom AccelerationBtn;
        public ButtonCustom BrakeReverseBtn;
        public ButtonCustom HandBrakeBtn;
        public ButtonCustom BoostBtn;

        float HorizontalAxis;

        CarController Car => UserInput.Car;

        public override void Init (CarControllerInput userInput)
        {
            base.Init (userInput);

            AccelerationBtn.OnPointerDownAction += (() => UserInput.SetAcceleration (1));
            AccelerationBtn.OnPointerUpAction += (() => UserInput.SetAcceleration (0));

            BrakeReverseBtn.OnPointerDownAction += (() => UserInput.SetBrakeReverse (1));
            BrakeReverseBtn.OnPointerUpAction += (() => UserInput.SetBrakeReverse (0));

            HandBrakeBtn.OnPointerEnterAction += (() => UserInput.SetHandBrake (true));
            HandBrakeBtn.OnPointerExitAction += (() => UserInput.SetHandBrake (false));

            BoostBtn.OnPointerEnterAction += (() => UserInput.SetBoost (true));
            BoostBtn.OnPointerExitAction += (() => UserInput.SetBoost (false));
        }

        private void OnEnable ()
        {
            InputHelper.EnableAccelerometer ();
        }

        private void OnDisable ()
        {
            InputHelper.DisableAccelerometer ();
        }

        private void OnApplicationFocus (bool focus)
        {
            if (focus && gameObject.activeInHierarchy)
            {
                InputHelper.EnableAccelerometer ();
            }
        }

        private void Update ()
        {
            //The tilt of the phone sets the velocity vector to the desired angle.
            float axisX = InputHelper.GetAccelerometerData().x * 90;
            float targetAnge = 0;
            if (axisX > DeadZone || axisX < -DeadZone)
            {
                targetAnge = Mathf.Clamp ((axisX + (axisX > 0 ? -DeadZone : DeadZone)) / (MaxAngle), -1, 1) * 90;
            }

            if (Car != null && !Car.InHandBrake && Car.VehicleDirection >= 0 && Car.CurrentSpeed > 1)
            {
                targetAnge += Car.VelocityAngle;
            }

            targetAnge = targetAnge.Clamp (-90, 90) / 90;

            HorizontalAxis = Mathf.Lerp (HorizontalAxis, targetAnge, Time.deltaTime * AccelerometerLerpSpeed);
            UserInput.SetSteer (HorizontalAxis);
        }
    }
}
