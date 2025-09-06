using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class FollowPlayer : MonoBehaviour
{
    [Header("Ayarlar")]
    [Tooltip("Sahnede aranacak oyuncu obje adı")] 
    [SerializeField] private string playerObjectName = "Player";
    [Tooltip("Ayrıca 'Player' etiketiyle aramayı dene (opsiyonel)")] 
    [SerializeField] private bool alsoTryTag = true;
    [Tooltip("Kontrol sıklığı (saniye)")] 
    [Min(0.05f)]
    [SerializeField] private float checkInterval = 0.5f;

    private CinemachineVirtualCamera vcam;
    private Transform cachedTarget;
    private float nextCheckTime;

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
    }

    private void OnEnable()
    {
        TryAssignTarget();
        nextCheckTime = Time.time + checkInterval;
    }

    private void Update()
    {
        // Hedef yoksa veya sahneden silindiyse periyodik kontrol
        if (Time.time >= nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            if (cachedTarget == null)
            {
                TryAssignTarget();
            }
        }
    }

    public void ForceReassign()
    {
        cachedTarget = null;
        TryAssignTarget();
    }

    private void TryAssignTarget()
    {
        if (vcam == null) return;

        var t = FindPlayerTransform();
        if (t != null)
        {
            cachedTarget = t;
            vcam.Follow = cachedTarget;
            vcam.LookAt = cachedTarget;
        }
        else
        {
            // Bulunamadıysa referansları temizle (opsiyonel)
            vcam.Follow = null;
            vcam.LookAt = null;
        }
    }

    private Transform FindPlayerTransform()
    {
        // Önce isimle ara
        if (!string.IsNullOrWhiteSpace(playerObjectName))
        {
            var go = GameObject.Find(playerObjectName);
            if (go != null) return go.transform;
        }

        // Opsiyonel: etikete göre dene
        if (alsoTryTag)
        {
            try
            {
                var goByTag = GameObject.FindGameObjectWithTag("Player");
                if (goByTag != null) return goByTag.transform;
            }
            catch (System.Exception)
            {
                // Tag yoksa exception gelebilir; yoksay
            }
        }
        return null;
    }
}
