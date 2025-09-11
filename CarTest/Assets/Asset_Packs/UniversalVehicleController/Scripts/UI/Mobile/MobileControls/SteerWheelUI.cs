using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PG.UI
{
    /// <summary>
    /// Mobile input SteerWheel.
    /// </summary>
    public class SteerWheelUI :MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float MaxSteerWheelAngle = 180;
        public float SteerWheelToDefaultSpeed = 360;

        public float HorizontalControl { get; private set; }
        public CarControllerInput UserInput { get; set; }

        float CurrentSteerAngle;

        bool WheelIsPressed;
        Vector2 PrevTouchPos;

        CarController Car => UserInput.Car;

        public void Init (CarControllerInput userInput)
        {
            UserInput = userInput;
        }

        private void Update ()
        {
            float targetAnge;
            float carVelocityAngleNormolized = Car? Car.VelocityAngle / 90: 0;
            bool needGetCarVelocity = Car && Car.VehicleDirection >= 0 && Car.CurrentSpeed > 1;
            
            if (!WheelIsPressed)
            {
                targetAnge = (needGetCarVelocity ? carVelocityAngleNormolized : 0) * MaxSteerWheelAngle;
                CurrentSteerAngle = Mathf.MoveTowards (CurrentSteerAngle, targetAnge, Time.deltaTime * SteerWheelToDefaultSpeed);
            }
            else
            {
                Vector2 pressedPos = (Vector2)transform.position - InputHelper.GetNearestTouchPosition ((Vector2)transform.position);

                float angleDelta = Vector2.SignedAngle (PrevTouchPos, pressedPos);
                PrevTouchPos = pressedPos;
                CurrentSteerAngle = Mathf.Clamp (CurrentSteerAngle + angleDelta, -MaxSteerWheelAngle, MaxSteerWheelAngle);
            }
            transform.rotation = Quaternion.AngleAxis (CurrentSteerAngle, Vector3.forward);

            targetAnge = -CurrentSteerAngle / MaxSteerWheelAngle;

            if (needGetCarVelocity)
            {
                targetAnge += carVelocityAngleNormolized;
            }

            HorizontalControl = targetAnge.Clamp (-1, 1);
        }

        public void OnPointerDown (PointerEventData eventData)
        {
            PrevTouchPos = (Vector2)transform.position - eventData.position;

            WheelIsPressed = true;
        }

        public void OnPointerUp (PointerEventData eventData)
        {
            WheelIsPressed = false;
        }
    }
}
