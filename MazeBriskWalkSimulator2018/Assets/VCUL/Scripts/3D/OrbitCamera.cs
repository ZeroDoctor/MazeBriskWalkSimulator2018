using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Reference to the transform of the target GameObject.")]
    public Transform target;
    [Tooltip("Current relative offset to the target.")]
    public Vector3 offset;
    [Tooltip("Rotation speed for the X-axis.")]
    public float rotationSpeed = 20f;
    [Tooltip("Vector to set the axis to rotate around. Values should be between -1 and 1.")]
    public Vector3 rotationAxis;

    // Use this for initialization
    private void Start()
    {
        if(target == null)
        {
            Debug.LogWarning(gameObject.name + ": No target found!");
        }
    }

    // Update is called once per frame
    private void Update()
    {
        
    }

    // LateUpdate is called every frame, if the Behaviour is enabled
    private void LateUpdate()
    {
        transform.RotateAround(target.position, rotationAxis, rotationSpeed * Time.deltaTime);
    }
}
