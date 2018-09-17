using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerShoot : NetworkBehaviour {

    private const string PLAYER_TAG = "Player";

    public PlayerWeapon weapon;

    [SerializeField]
    private Camera cam;

    [SerializeField]
    private LayerMask mask;

    private void Start()
    {
      if(cam == null)
        {
            Debug.LogError("PlayerShoot: No camera referenced!");
            this.enabled = false;
        }  
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            Shoot();
        }
    }

    [Client]
    private void Shoot()
    {
        RaycastHit hit;
        if(Physics.Raycast(cam.transform.position, cam.transform.forward, out hit, weapon.range, mask))
        {
            if(hit.collider.tag == PLAYER_TAG)
            {
                CmdPlayerShot(hit.collider.name);
            }
        }
    }

    [Command]
    private void CmdPlayerShot(string ID)
    {
        Debug.Log(ID + " has been shot...");
    }

}
