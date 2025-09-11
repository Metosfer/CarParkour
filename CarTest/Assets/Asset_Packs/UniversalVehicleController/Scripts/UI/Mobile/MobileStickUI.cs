using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace PG
{
    public class MobileStickUI :MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Image StickIcon;
        public float MaxRadius = 200;
        public float DeadZone = 30;

        Vector2 StickPos;
        public Vector2 InputValue { get; private set; }
        public bool IsPressed { get; private set; }

        RectTransform StickImageRectTR;

        private void Start ()
        {
            StickImageRectTR = StickIcon.GetComponent<RectTransform> ();
        }

        private void OnDisable ()
        {
            IsPressed = false;
            InputValue = Vector2.zero;
        }

        public void OnBeginDrag (PointerEventData eventData)
        {
            IsPressed = true;
        }

        public void OnDrag (PointerEventData eventData)
        {
            StickPos = Vector2.ClampMagnitude (StickPos + eventData.delta, MaxRadius);

            InputValue = new Vector2 (
                Mathf.MoveTowards (StickPos.x, 0, DeadZone) / (MaxRadius + DeadZone),
                Mathf.MoveTowards (StickPos.y, 0, DeadZone) / (MaxRadius + DeadZone)
                );

            StickImageRectTR.anchoredPosition = StickPos;
        }
        
        public void OnEndDrag (PointerEventData eventData)
        {
            IsPressed = false;
            InputValue = Vector2.zero;
            StickPos = Vector2.zero;
            StickImageRectTR.anchoredPosition = Vector2.zero;
        }
    }
}
