using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
public class CrosshairScript : MonoBehaviour {

	[SerializeField] private GameObject crosshair;

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		Player player = Utils.ClientLocalPlayer();
		crosshair.SetActive(player != null);
		if(!player)
			return;
	}
}
