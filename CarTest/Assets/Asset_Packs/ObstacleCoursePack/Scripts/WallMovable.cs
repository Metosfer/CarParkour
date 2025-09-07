using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class WallMovable : MonoBehaviourPun
{
	public bool isDown = true; //If the wall starts down, if not you must modify to false
	public bool isRandom = true; //If you want that the wall go down random
	public float speed = 2f;

	[Header("Kill Player")]
	[Tooltip("Temas eden 'Player' etiketli objeyi respawn et.")]
	public string playerTag = "Player";
	[Tooltip("Respawn noktası (assign)")]
	public Transform spawnPoint;
	[Tooltip("Respawn'da hızları sıfırla")] public bool resetVelocities = true;

	private float height; //Height of the platform
	private float posYDown; //Start position of the Y coord
	private bool isWaiting = false; //If the wall is waiting up or down
	private bool canChange = true; //If the wall is thinking if should go down or not

	void Awake()
    {
		height = transform.localScale.y;
		if(isDown)
			posYDown = transform.position.y;
		else
			posYDown = transform.position.y - height;
	}

    // Update is called once per frame
    void Update()
    {
		if (isDown)
		{
			if (transform.position.y < posYDown + height)
			{
				transform.position += Vector3.up * Time.deltaTime * speed;
			}
			else if (!isWaiting)
				StartCoroutine(WaitToChange(0.25f));
		}
		else
		{
			if (!canChange)
				return;

			if (transform.position.y > posYDown)
			{
				transform.position -= Vector3.up * Time.deltaTime * speed;
			}
			else if (!isWaiting)
				StartCoroutine(WaitToChange(0.25f));
		}
	}

	//Function that wait before go down or up
	IEnumerator WaitToChange(float time)
	{
		isWaiting = true;
		yield return new WaitForSeconds(time);
		isWaiting = false;
		isDown = !isDown;

		if (isRandom && !isDown) //If is wall up and is random
		{
			int num = Random.Range(0, 2);
			//Debug.Log(num);
			if (num == 1)
				StartCoroutine(Retry(1.5f));
		}
	}

	//Function that checks every 1.25secs if can go down the wall
	IEnumerator Retry(float time)
	{
		canChange = false;
		yield return new WaitForSeconds(time);
		int num = Random.Range(0, 2);
		//Debug.Log("2-"+num);
		if (num == 1)
			StartCoroutine(Retry(1.25f));
		else
			canChange = true;
	}

	private void OnCollisionEnter(Collision collision)
	{
		TryRespawnPlayer(collision.collider);
	}

	private void OnTriggerEnter(Collider other)
	{
		TryRespawnPlayer(other);
	}

	private void TryRespawnPlayer(Collider col)
	{
		if (col == null) return;
		if (spawnPoint == null)
		{
			Debug.LogWarning("[Wall] SpawnPoint atanmadı, respawn yapılamıyor.");
			return;
		}

		GameObject go = col.gameObject;
		// Etiket kontrolü: doğrudan veya kök objede Player etiketi var mı?
		bool isPlayer = (go.CompareTag(playerTag) || (go.transform.root != null && go.transform.root.CompareTag(playerTag)));
		if (!isPlayer) return;

		Transform root = go.transform.root != null ? go.transform.root : go.transform;
		PhotonView pv = root.GetComponentInParent<PhotonView>();

		if (PhotonNetwork.IsConnected)
		{
			if (PhotonNetwork.IsMasterClient)
			{
				RespawnOnAuthority(root);
			}
			else
			{
				if (photonView != null)
				{
					int viewId = pv != null ? pv.ViewID : -1;
					photonView.RPC("RPC_RequestRespawn", RpcTarget.MasterClient, viewId);
				}
			}
		}
		else
		{
			// Offline mod: direkt taşı
			DoTeleport(root);
		}
	}

	[PunRPC]
	private void RPC_RequestRespawn(int viewId)
	{
		if (!PhotonNetwork.IsMasterClient) return;
		PhotonView targetView = viewId > 0 ? PhotonView.Find(viewId) : null;
		Transform target = targetView != null ? targetView.transform.root : null;
		if (target == null) return;
		RespawnOnAuthority(target);
	}

	private void RespawnOnAuthority(Transform target)
	{
		// Otorite tarafında taşı; CarManager otorite sync ile herkese yayar
		DoTeleport(target);
	}

	private void DoTeleport(Transform target)
	{
		if (target == null || spawnPoint == null) return;
		Rigidbody rb = target.GetComponentInChildren<Rigidbody>();
		if (rb && resetVelocities)
		{
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
		}
		target.position = spawnPoint.position;
		target.rotation = spawnPoint.rotation;
	}
}
