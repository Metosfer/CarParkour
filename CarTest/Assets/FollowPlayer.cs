using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayer : MonoBehaviour
{
    [Header("Hedef")]
    [Tooltip("Takip edilecek hedef (oyuncu/araç)")]
    [SerializeField] private Transform target;

    [Header("Takip Konumu")]
    [Tooltip("Hedeften olan uzaklık (metre)")]
    [Min(0f)]
    [SerializeField] private float distance = 8f;
    [Tooltip("Hedeften yukarı ofset (metre)")]
    [SerializeField] private float height = 3f;
    [Tooltip("Her zaman hedefin arkasında kal (hedefin forward yönüne göre)")]
    [SerializeField] private bool stayBehindTarget = true;
    [Tooltip("Dünya eksenlerinde yaw (Y) ve pitch (X) ile sabit açı kullan (stayBehindTarget kapalıysa)")]
    [SerializeField] private float yawDeg = 0f;
    [SerializeField] private float pitchDeg = 15f;
    [Tooltip("Yumuşatma kullan")]
    [SerializeField] private bool useSmoothing = true;
    [Tooltip("Pozisyon yumuşatma hızı (lerp/s)")]
    [Min(0.1f)]
    [SerializeField] private float followSmooth = 10f;

    [Header("Rotasyon")]
    [Tooltip("Başlangıç rotasyonunu koru (hedefin rotasyonunu alma)")]
    [SerializeField] private bool keepInitialRotation = true;
    [Tooltip("Sabit bir rotasyon kullan (keepInitialRotation kapalıysa)")]
    [SerializeField] private Vector3 fixedEuler = new Vector3(15f, 0f, 0f);

    private Quaternion initialRotation;

    private void Awake()
    {
        initialRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos;
        if (stayBehindTarget)
        {
            // Hedefin ileri yönüne göre her zaman arkasında kal
            Vector3 fwd = target.forward;
            // Roll/pitch etkisini azaltmak için yatay düzleme projeksiyon
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = target.forward; // fallback
            Vector3 dir = -fwd.normalized;
            desiredPos = target.position + Vector3.up * height + dir * Mathf.Max(0f, distance);
        }
        else
        {
            // Dünya eksenlerine göre sabit açı ile ofset yönü
            Quaternion yaw = Quaternion.AngleAxis(yawDeg, Vector3.up);
            Quaternion pitch = Quaternion.AngleAxis(pitchDeg, Vector3.right);
            Vector3 dir = (yaw * pitch) * Vector3.back; // arkaya doğru bakacak şekilde
            desiredPos = target.position + Vector3.up * height + dir.normalized * Mathf.Max(0f, distance);
        }

        if (useSmoothing)
        {
            float t = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, t);
        }
        else
        {
            transform.position = desiredPos;
        }

        // Rotasyon: hedefin rotasyonunu alma, sabit tut
        transform.rotation = keepInitialRotation ? initialRotation : Quaternion.Euler(fixedEuler);
    }
}
