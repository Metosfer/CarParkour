using UnityEngine;
using Photon.Pun;

// Sahneye bir kez ekleyin (ör: boş GameObject). Photon ağ kadans ve
// interpolasyonla ilgili global ayarları buradan yönetin.
[DefaultExecutionOrder(-500)]
public class PhotonNetworkConfigurator : MonoBehaviour
{
    [Header("Photon Rates")]
    [Tooltip("PhotonNetwork.SendRate (Hz): paket gönderim sıklığı")] 
    [Min(5)]
    [SerializeField] private int sendRate = 60;
    [Tooltip("PhotonNetwork.SerializationRate (Hz): state serileştirme sıklığı")] 
    [Min(5)]
    [SerializeField] private int serializationRate = 30;

    [Header("Smoothing Defaults")] 
    [Tooltip("Tüm araçlar için varsayılan interpolasyon geri zamanı (s)")]
    [Min(0f)]
    [SerializeField] private float interpolationBackTimeBase = 0.12f;
    [Tooltip("RTT çarpan faktörü (0..1): backTime += RTT * faktör")] 
    [Range(0f,1f)]
    [SerializeField] private float interpolationBackRttFactor = 0.5f;
    [Tooltip("Jitter tamponu (s)")]
    [Min(0f)]
    [SerializeField] private float interpolationJitterBuffer = 0.02f;

    [Header("Apply Mode")]
    [Tooltip("Sahne başladığında otomatik uygula")] 
    [SerializeField] private bool applyOnAwake = true;

    private void Awake()
    {
        if (applyOnAwake)
        {
            Apply();
        }
    }

    public void Apply()
    {
        PhotonNetwork.SendRate = Mathf.Clamp(sendRate, 5, 120);
        PhotonNetwork.SerializationRate = Mathf.Clamp(serializationRate, 5, 120);
        // Burada global ScriptableObject/Singleton ile oyun geneli smoothing paylaşılabilir.
        // Şimdilik değerleri static cache’de tutmuyoruz; CarManager kendi inspector’ından devam ediyor.
    }

    // Diğer scriptlerin okuyabilmesi için erişimciler
    public int SendRate => sendRate;
    public int SerializationRate => serializationRate;
    public float DefaultBackTimeBase => interpolationBackTimeBase;
    public float DefaultBackRttFactor => interpolationBackRttFactor;
    public float DefaultJitterBuffer => interpolationJitterBuffer;
}
