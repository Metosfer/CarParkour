using UnityEngine;
using Photon.Pun;

// Basit: Oyun sahnesinde bir kez bulunur ve aracı network üzerinden spawn eder.
// Prefab'ın Resources/ yolunda olması gerekir: Resources/Car.prefab gibi.
public class CarNetworkSpawner : MonoBehaviour
{
    [Tooltip("Resources altındaki araç prefab adı (Resources/Name.prefab)")]
    public string carPrefabName = "Car";

    [Tooltip("Spawn konumu (boşsa origin).")]
    public Transform spawnPoint;

    private void Start()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("[Spawner] Photon bağlı değil. Lobby'den girildiğinden emin olun.");
            return;
        }

        if (PhotonNetwork.IsMasterClient)
        {
            Vector3 pos = spawnPoint ? spawnPoint.position : Vector3.zero;
            Quaternion rot = spawnPoint ? spawnPoint.rotation : Quaternion.identity;
            PhotonNetwork.Instantiate(carPrefabName, pos, rot);
        }
    }
}
