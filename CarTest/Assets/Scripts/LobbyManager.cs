using System.Linq;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using TMPro;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("Ayarlar")]
    [Tooltip("Oyun sahnesinin adı (Build Settings listesine ekli olmalı)")]
    [SerializeField] private string gameSceneName = "deneme sahnesi";

    [Tooltip("Maksimum oyuncu sayısı (bu örnekte 2 önerilir)")]
    [SerializeField] private byte maxPlayers = 2;

    [Tooltip("Photon GameVersion (aynı versiyonlar birbirini görür)")]
    [SerializeField] private string gameVersion = "1.0";

    [Header("Varsayılanlar / UI İçin Yardımcılar")]
    [Tooltip("UI'dan set edilecek oda adı (InputField -> UI_SetRoomName ile)")]
    [SerializeField] private string roomName = "Oda-1";

    [Tooltip("Oyuncu takma adı (boşsa rastgele atanır)")]
    [SerializeField] private string playerNickname = "";

    [Header("UI Panelleri ve Metinler")] 
    [SerializeField] private GameObject lobbyPanel; // Assign in editor
    [SerializeField] private GameObject roomPanel;  // Assign in editor
    [SerializeField] private TMP_Text nicknameText; // LobbyPanel içi: oyuncu nickname
    [SerializeField] private TMP_Text roomNameText; // RoomPanel içi: oda adı
    [SerializeField] private TMP_Text statusText;   // Lobide/odada durum metni

    [Header("UI Butonları")] 
    [SerializeField] private Button startButton; // Başlat butonu (editörden assign)

    private const string ReadyPropKey = "ready"; // Player Custom Property anahtarı
    private bool localReady = false;

    private void Awake()
    {
        // Oyun başladığında sahne senkronu açık olsun ki MasterClient sahne değiştirince herkes geçsin
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    private void Start()
    {
        ConnectIfNeeded();
    UpdateAllUI();
    }

    private void ConnectIfNeeded()
    {
        if (PhotonNetwork.IsConnected)
        {
            if (!PhotonNetwork.InLobby)
                PhotonNetwork.JoinLobby();
            return;
        }

        if (string.IsNullOrWhiteSpace(PhotonNetwork.NickName))
        {
            PhotonNetwork.NickName = string.IsNullOrWhiteSpace(playerNickname)
                ? $"Oyuncu_{Random.Range(1000, 9999)}"
                : playerNickname;
        }

        PhotonNetwork.GameVersion = gameVersion;
        PhotonNetwork.ConnectUsingSettings();
        Debug.Log("[Lobby] Photon'a bağlanılıyor...");
    UpdateStatusUI();
    }

    // ——— UI'dan çağrılacak basit metodlar ———
    public void UI_SetRoomName(string newName)
    {
        roomName = string.IsNullOrWhiteSpace(newName) ? roomName : newName.Trim();
    }

    public void UI_CreateRoom()
    {
        CreateRoom(roomName);
    }

    public void UI_JoinRoom()
    {
        JoinRoom(roomName);
    }

    public void UI_JoinRandom()
    {
        JoinRandomRoom();
    }

    public void UI_LeaveRoom()
    {
        LeaveRoom();
    }

    public void UI_ToggleReady()
    {
        SetLocalReady(!localReady);
    }

    public void UI_SetReady(bool ready)
    {
        SetLocalReady(ready);
    }

    public void UI_StartGame()
    {
        TryStartGame();
    }

    // ——— Çekirdek Lobi İşlevleri ———
    public void CreateRoom(string targetRoomName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("[Lobby] Bağlı değil. Bağlanılıyor...");
            ConnectIfNeeded();
            return;
        }

        if (string.IsNullOrWhiteSpace(targetRoomName))
            targetRoomName = $"Oda_{Random.Range(1000, 9999)}";

        var options = new RoomOptions
        {
            MaxPlayers = maxPlayers,
            IsOpen = true,
            IsVisible = true
        };

        PhotonNetwork.CreateRoom(targetRoomName, options, TypedLobby.Default);
        Debug.Log($"[Lobby] Oda oluşturuluyor: {targetRoomName}");
    UpdateStatusUI();
    }

    public void JoinRoom(string targetRoomName)
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("[Lobby] Bağlı değil. Bağlanılıyor...");
            ConnectIfNeeded();
            return;
        }

        if (string.IsNullOrWhiteSpace(targetRoomName))
        {
            Debug.LogWarning("[Lobby] Oda adı boş olamaz. Önce UI_SetRoomName ile set edin.");
            return;
        }

        PhotonNetwork.JoinRoom(targetRoomName);
        Debug.Log($"[Lobby] Odaya katılınıyor: {targetRoomName}");
    }

    public void JoinRandomRoom()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("[Lobby] Bağlı değil. Bağlanılıyor...");
            ConnectIfNeeded();
            return;
        }
        PhotonNetwork.JoinRandomRoom();
        Debug.Log("[Lobby] Rastgele odaya katılma deneniyor...");
    }

    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
            Debug.Log("[Lobby] Odadan çıkılıyor...");
        }
    UpdateStatusUI();
    }

    private void SetLocalReady(bool ready)
    {
        localReady = ready;
        var props = new Hashtable {{ ReadyPropKey, ready }};
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        Debug.Log($"[Lobby] Hazır durumunuz: {ready}");
    UpdateStartButtonUI();
    }

    private bool AreAllPlayersReady(out int playerCount)
    {
        playerCount = PhotonNetwork.PlayerList?.Length ?? 0;
        if (playerCount == 0) return false;

        // Bu örnekte 2 kişi şartı
        if (playerCount < 2) return false;

        foreach (var p in PhotonNetwork.PlayerList)
        {
            if (!p.CustomProperties.TryGetValue(ReadyPropKey, out var v) || !(v is bool) || !(bool)v)
                return false;
        }
        return true;
    }

    private void TryStartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[Lobby] Sadece oda sahibi (MasterClient) oyunu başlatabilir.");
            return;
        }

        int count;
        if (!AreAllPlayersReady(out count))
        {
            Debug.LogWarning("[Lobby] Tüm oyuncular hazır değil veya oyuncu sayısı yetersiz (2 gerekli).");
            return;
        }

        if (string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("[Lobby] Geçerli bir sahne adı girin (Inspector -> gameSceneName)");
            return;
        }

        Debug.Log($"[Lobby] Oyun başlatılıyor. Oyuncu sayısı: {count}. Sahne: {gameSceneName}");
        PhotonNetwork.LoadLevel(gameSceneName);
    }

    // ——— Callbacks ———
    public override void OnConnectedToMaster()
    {
        Debug.Log("[Lobby] Master sunucuya bağlandı. Lobiye giriliyor...");
        PhotonNetwork.JoinLobby();
    UpdateAllUI();
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("[Lobby] Lobiye girildi. Oda oluşturabilir veya bir odaya katılabilirsiniz.");
    // Nickname UI güncelle
    UpdateNicknameUI();
    UpdatePanelsUI();
    UpdateStatusUI();
    }

    public override void OnCreatedRoom()
    {
        Debug.Log($"[Lobby] Oda oluşturuldu: {PhotonNetwork.CurrentRoom?.Name}");
        // Odaya girer girmez hazırı false yap (temiz başlangıç)
        SetLocalReady(false);
    UpdateRoomNameUI();
    UpdatePanelsUI();
    UpdateStartButtonUI();
    UpdateStatusUI();
    }

    public override void OnJoinedRoom()
    {
        Debug.Log($"[Lobby] Odaya girildi: {PhotonNetwork.CurrentRoom?.Name}. Oyuncu sayısı: {PhotonNetwork.CurrentRoom.PlayerCount}");
        // Otomatik olarak hazır değil
        SetLocalReady(false);
    UpdateRoomNameUI();
    UpdatePanelsUI();
    UpdateStartButtonUI();
    UpdateStatusUI();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[Lobby] Rastgele odaya girilemedi ({returnCode}). Yeni oda oluşturuluyor...");
        CreateRoom($"Oda_{Random.Range(1000, 9999)}");
    UpdateStatusUI();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogWarning($"[Lobby] Oda oluşturma başarısız ({returnCode}): {message}. Farklı bir ad denenecek.");
        CreateRoom($"Oda_{Random.Range(1000, 9999)}");
    UpdatePanelsUI();
    UpdateStatusUI();
    }

    public override void OnLeftRoom()
    {
        Debug.Log("[Lobby] Odadan çıkıldı.");
        localReady = false;
    UpdatePanelsUI();
    UpdateStartButtonUI();
    UpdateStatusUI();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"[Lobby] Oyuncu katıldı: {newPlayer.NickName}. Toplam: {PhotonNetwork.CurrentRoom.PlayerCount}");
    UpdateStartButtonUI();
    UpdateStatusUI();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"[Lobby] Oyuncu ayrıldı: {otherPlayer.NickName}. Toplam: {PhotonNetwork.CurrentRoom.PlayerCount}");
    UpdateStartButtonUI();
    UpdateStatusUI();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        if (changedProps == null || changedProps.Count == 0) return;

        if (changedProps.ContainsKey(ReadyPropKey))
        {
            var isReady = changedProps[ReadyPropKey] is bool b && b;
            Debug.Log($"[Lobby] {targetPlayer.NickName} hazır: {isReady}");
            UpdateStartButtonUI();
            UpdateStatusUI();
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[Lobby] Oda sahibi değişti: {newMasterClient.NickName}");
    UpdateStartButtonUI();
    UpdateStatusUI();
    }

    // ——— Yardımcı / Durum ———
    public bool IsInRoom => PhotonNetwork.InRoom;
    public bool IsMaster => PhotonNetwork.IsMasterClient;
    public bool LocalReady => localReady;
    public string CurrentRoomName => PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : string.Empty;
    public int CurrentPlayerCount => PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
    public bool CanMasterStart
    {
        get
        {
            int c; return PhotonNetwork.IsMasterClient && AreAllPlayersReady(out c);
        }
    }

    // ——— UI Güncelleyiciler ———
    private void UpdateAllUI()
    {
        UpdateNicknameUI();
        UpdateRoomNameUI();
        UpdatePanelsUI();
        UpdateStartButtonUI();
    UpdateStatusUI();
    }

    private void UpdatePanelsUI()
    {
        if (lobbyPanel != null) lobbyPanel.SetActive(!PhotonNetwork.InRoom);
        if (roomPanel != null) roomPanel.SetActive(PhotonNetwork.InRoom);
    }

    private void UpdateNicknameUI()
    {
        if (nicknameText != null)
        {
            var nick = string.IsNullOrWhiteSpace(PhotonNetwork.NickName) ? "" : PhotonNetwork.NickName;
            nicknameText.text = string.IsNullOrEmpty(nick) ? "" : $"Takma Ad: {nick}";
        }
    }

    private void UpdateRoomNameUI()
    {
        if (roomNameText != null)
        {
            var rn = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : "";
            roomNameText.text = string.IsNullOrEmpty(rn) ? "" : $"Oda: {rn}";
        }
    }

    private void UpdateStartButtonUI()
    {
        if (startButton != null)
        {
            startButton.interactable = CanMasterStart;
        }
    }

    private void UpdateStatusUI()
    {
        if (statusText == null)
            return;

        if (!PhotonNetwork.IsConnected)
        {
            statusText.text = "Durum: Bağlanıyor...";
            return;
        }

        if (!PhotonNetwork.InRoom)
        {
            statusText.text = "Lobidesiniz. Oda oluşturun veya bir odaya katılın.";
            return;
        }

        var room = PhotonNetwork.CurrentRoom;
        var players = PhotonNetwork.PlayerList;
        int playerCount = players != null ? players.Length : 0;
        int max = room != null ? room.MaxPlayers : maxPlayers;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Oda: {room.Name} ({playerCount}/{max})");

        foreach (var p in players.OrderByDescending(pp => pp.IsMasterClient))
        {
            bool isReady = p.CustomProperties != null && p.CustomProperties.TryGetValue(ReadyPropKey, out var v) && v is bool bb && bb;
            string rol = p.IsMasterClient ? "Kurucu" : "Misafir";
            string hazırStr = isReady ? "Hazır" : "Hazır Değil";
            sb.AppendLine($"{rol} {p.NickName}: {hazırStr}");
        }

        int dummy;
        if (AreAllPlayersReady(out dummy))
        {
            if (PhotonNetwork.IsMasterClient)
                sb.AppendLine("Başlatabilirsiniz.");
            else
                sb.AppendLine("Kurucu oyunu başlatabilir.");
        }
        else
        {
            if (playerCount < 2)
                sb.AppendLine("İkinci oyuncu bekleniyor...");
            else
                sb.AppendLine("Tüm oyuncular hazır olmalı.");
        }

        statusText.text = sb.ToString();
    }
}
