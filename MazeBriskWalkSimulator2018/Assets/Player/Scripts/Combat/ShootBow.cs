using UnityEngine;

public class ShootBow : MonoBehaviour {
    public float damage = 10f;
    public float knockBack = 50f;
    public float range = 100f;
    public float fireRate = 15f;
    public float nextTimeToFire = 0f;
    public bool autoFire = false;

    public Camera playerCam;
    public GameObject impactEffect;

    void Update() {
        if(autoFire) {
            if(Input.GetButtonDown("Fire1") && Time.time >= nextTimeToFire) {
                nextTimeToFire = Time.time + 1f / fireRate;
                Shoot();
            }
        } else {
            if(Input.GetButtonDown("Fire1")) {
                Shoot();
            }
        }
        
    }

    void Shoot() {
        RaycastHit hit; 
        if (Physics.Raycast(playerCam.transform.position, playerCam.transform.forward, out hit, range)) {
            Entity entity = hit.transform.GetComponent<Entity>();

            if(entity != null) {
                //base.DealDamageAt(entity, amount, 0, 0);
            }

            if(hit.rigidbody != null) {
                hit.rigidbody.AddForce(-hit.normal * knockBack);
            }

            GameObject impactGo = Instantiate(impactEffect, hit.point, Quaternion.LookRotation(hit.normal));
            Destroy(impactGo, 2f);
        }

    }
}