using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent (typeof (Rigidbody))]
public class RotateRB : MonoBehaviour
{
    public Vector3 RotateVelocity = Vector3.zero;

    Rigidbody RB;

    private void Start ()
    {
        RB = GetComponent<Rigidbody> ();
    }

    // Update is called once per frame
    private void FixedUpdate ()
    {
        Quaternion deltaRotation = Quaternion.Euler(RotateVelocity * Time.fixedDeltaTime);
        RB.MoveRotation (RB.rotation * deltaRotation);

    }
}
