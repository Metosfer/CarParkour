using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PG
{
    public class ButtonCustom :Button
    {
        public bool PointerEnter { get; private set; }
        public bool Pressed { get; private set; }

        public System.Action OnPointerEnterAction { get; set; }
        public System.Action OnPointerExitAction { get; set; }
        public System.Action OnPointerDownAction { get; set; }
        public System.Action OnPointerUpAction { get; set; }

        public override void OnPointerEnter (PointerEventData eventData)
        {
            base.OnPointerEnter (eventData);
            PointerEnter = true;
            OnPointerEnterAction.SafeInvoke ();
        }

        public override void OnPointerExit (PointerEventData eventData)
        {
            base.OnPointerExit (eventData);
            PointerEnter = false;
            OnPointerExitAction.SafeInvoke ();
        }

        public override void OnPointerDown (PointerEventData eventData)
        {
            base.OnPointerDown (eventData);
            Pressed = true;
            OnPointerDownAction.SafeInvoke ();
        }

        public override void OnPointerUp (PointerEventData eventData)
        {
            base.OnPointerUp (eventData);
            Pressed = false;
            OnPointerUpAction.SafeInvoke ();
        }

        protected override void OnDisable ()
        {
            base.OnDisable ();
            if (Pressed)
            {
                Pressed = false;
                OnPointerUpAction.SafeInvoke ();
            }

            if (PointerEnter)
            {
                PointerEnter = false;
                OnPointerExitAction.SafeInvoke ();
            }
        }
    }
}
