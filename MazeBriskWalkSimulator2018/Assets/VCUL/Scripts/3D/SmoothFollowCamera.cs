using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SmoothFollowCamera : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Reference to the target GameObject.")]
    public Transform target;
    [Tooltip("Current relative offset to the target.")]
    public Vector3 offset;
    [Tooltip("Smoothing factor for rapid changes on the Y-axis.")]
    public float heightDamping = 2.0f;
    [Tooltip("Rigidbody of the target to retrieve velocity.")]
    public Rigidbody targetRigidbody;
    [Tooltip("Smoothing factor for the rotation.")]
    public float rotationSnapTime = 0.3f;
    [Tooltip("Smoothing factor for the distance on the Z-axis.")]
    public float distanceSnapTime;
    [Tooltip("Smoothing factor for rapid changes on the distance or Z-axis.")]
    public float distanceMultiplier;

    private float usedDistance;
    private float wantedRotationAngle;
    private float wantedHeight;
    private float currentRotationAngle;
    private float currentHeight;
    private Quaternion currentRotation;
    private Vector3 wantedPosition;
    private Vector3 velocity;

    // Use this for initialization
    private void Start()
    {
        
    }

    // Update is called once per frame
    private void Update()
    {
        
    }

    // LateUpdate is called every frame, if the Behaviour is enabled 
    private void LateUpdate()
    {
        wantedHeight = target.position.y + offset.y;
        currentHeight = transform.position.y;

        wantedRotationAngle = target.eulerAngles.y;
        currentRotationAngle = transform.eulerAngles.y;

        currentRotationAngle = Mathf.SmoothDampAngle(currentRotationAngle, wantedRotationAngle, ref velocity.y, rotationSnapTime);

        currentHeight = Mathf.Lerp(currentHeight, wantedHeight, heightDamping * Time.deltaTime);

        wantedPosition = target.position;
        wantedPosition.y = currentHeight;

        usedDistance = Mathf.SmoothDampAngle(usedDistance, offset.z + (targetRigidbody.velocity.magnitude * distanceMultiplier), ref velocity.z, distanceSnapTime);

        wantedPosition += Quaternion.Euler(0, currentRotationAngle, 0) * new Vector3(0, 0, -usedDistance);

        transform.position = wantedPosition;

        transform.LookAt(target.position);
    }
}
