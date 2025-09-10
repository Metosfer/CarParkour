using UnityEngine;
using Photon.Pun;

// Sahneye bir kez ekleyin (ör: boş GameObject). Photon ağ kadans ve
// interpolasyonla ilgili global ayarları buradan yönetin.
[DefaultExecutionOrder(-500)]
public class PhotonNetworkConfigurator : MonoBehaviour
{
    [Header("Photon Rates")]
    [Tooltip("PhotonNetwork.SendRate (Hz): Paket gönderme sıklığı. Daha yüksek değerler daha duyarlı hareket sağlar ancak bant genişliği tüketir. Örnek: 120 Hz, 2 kişilik oyun için yüksek duyarlılık.")] 
    [Min(5)]
    [SerializeField] private int sendRate = 120;
    [Tooltip("PhotonNetwork.SerializationRate (Hz): Durum serileştirme sıklığı. SendRate'e eşit veya daha düşük olmalı. Örnek: 60 Hz, lagı minimize etmek için kullanılır.")] 
    [Min(5)]
    [SerializeField] private int serializationRate = 60;

    [Header("Smoothing Defaults")] 
    [Tooltip("Tüm araçlar için varsayılan interpolasyon geri zamanı (s). Daha düşük değerler daha az gecikme sağlar ancak jitter artabilir. Örnek: 0.08 s, dengeli gecikme ve jitter.")]
    [Min(0f)]
    [SerializeField] private float interpolationBackTimeBase = 0.08f;
    [Tooltip("RTT çarpan faktörü (0..1): Geri zamanı ping'e göre ayarlar. Örnek: 0.5, yarım ping kadar geri zaman ekler.")] 
    [Range(0f,1f)]
    [SerializeField] private float interpolationBackRttFactor = 0.5f;
    [Tooltip("Jitter tamponu (s). Paket aralıklarındaki değişkenliği tamponlar. Örnek: 0.02 s, küçük jitter için yeterli.")]
    [Min(0f)]
    [SerializeField] private float interpolationJitterBuffer = 0.02f;

    [Header("Frame Pacing (Opsiyonel)")]
    [Tooltip("VSync ve hedef FPS'i buradan zorla (mikro takılmaları azaltmaya yardımcı olabilir)")]
    [SerializeField] private bool enforceFramePacing = false;
    [Tooltip("QualitySettings.vSyncCount (0: kapalı, 1: her vsync, 2: iki vsync)")]
    [Min(0)]
    [SerializeField] private int vSyncCount = 0;
    [Tooltip("Application.targetFrameRate (örn: 120). 0 = sınırsız/varsayılan")]
    [Min(0)]
    [SerializeField] private int targetFrameRate = 120;

    [Header("Physics Timestep (Opsiyonel)")]
    [Tooltip("Sabit zaman adımını (FixedUpdate) zorla. 0 ise dokunma.")]
    [Min(0f)]
    [SerializeField] private float fixedDeltaTime = 0.0167f; // ~60 Hz
    [Tooltip("Maksimum izin verilen deltaTime (ani droplarda). 0 ise dokunma.")]
    [Min(0f)]
    [SerializeField] private float maximumDeltaTime = 0.0667f; // ~15 FPS

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

        if (enforceFramePacing)
        {
            QualitySettings.vSyncCount = Mathf.Max(0, vSyncCount);
            Application.targetFrameRate = targetFrameRate;
        }

        if (fixedDeltaTime > 0f)
        {
            Time.fixedDeltaTime = fixedDeltaTime;
        }
        if (maximumDeltaTime > 0f)
        {
            Time.maximumDeltaTime = maximumDeltaTime;
        }
    }

    // Diğer scriptlerin okuyabilmesi için erişimciler
    public int SendRate => sendRate;
    public int SerializationRate => serializationRate;
    public float DefaultBackTimeBase => interpolationBackTimeBase;
    public float DefaultBackRttFactor => interpolationBackRttFactor;
    public float DefaultJitterBuffer => interpolationJitterBuffer;
}
