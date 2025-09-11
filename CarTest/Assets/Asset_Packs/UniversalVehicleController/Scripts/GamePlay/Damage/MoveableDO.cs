using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PG
{
    /// <summary>
    /// The moved object, upon damage just moves from the side of the damage velocity.
    /// </summary>
    public class MoveableDO :DamageableObject
    {
        protected Vector3 InitialPos;

        public override void Awake ()
        {
            base.Awake ();

            InitialPos = transform.localPosition;
        }

        public virtual void MoveObject(Vector3 damageVelocity)
        {
            transform.localPosition += damageVelocity;
        }

        public override void RestoreObject ()
        {
            base.RestoreObject ();
            transform.localPosition = InitialPos;
        }
    }
}
