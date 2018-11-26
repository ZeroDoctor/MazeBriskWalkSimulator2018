using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTSCamera : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Zone that will receive on-screen cursor position.")]
    [Range(0f, 100f)]
    public float scrollZone = 30.0f;
    [Tooltip("Multiplier for camera sensitivity.")]
    [Range(0f, 300)]
    public float sensitivity = 120f;
    [Tooltip("Smoothing factor.")]
    [Range(0f, 10f)]
    public float smoothFactor = 0.2f;
    [Tooltip("Movement limits on the X-axis. X represents the lowest and Y the highest value.")]
    public Vector2 moveLimitsX;
    [Tooltip("Movement limits on the Y-axis. X represents the lowest and Y the highest value.")]
    public Vector2 scrollLimitsY;
    [Tooltip("Movement limits on the Z-axis. X represents the lowest and Y the highest value.")]
    public Vector2 moveLimitsZ;
    [Tooltip("Whether the position changes using the cursor.")]
    public bool useCursor = false;

    private Vector3 desiredPosition;
    private Vector3 currentPosition;

    // Use this for initialization
    private void Start()
    {
        desiredPosition = transform.position;
    }

    // Update is called once per frame
    private void Update()
    {
        
    }

    // LateUpdate is called every frame, if the Behaviour is enabled
    void LateUpdate()
    {
        if(useCursor)
        {
            currentPosition = Vector3.zero;

            if(Input.mousePosition.x < scrollZone)
            {
                currentPosition.x -= sensitivity * Time.deltaTime;
            }
            else if(Input.mousePosition.x > Screen.width - scrollZone)
            {
                currentPosition.x = sensitivity * Time.deltaTime;
            }

            if(Input.mousePosition.y < scrollZone)
            {
                currentPosition.z -= sensitivity * Time.deltaTime;
            }
            else if(Input.mousePosition.y > Screen.height - scrollZone)
            {
                currentPosition.z = sensitivity * Time.deltaTime;
            }
        }
        else
        {
            currentPosition.x = Input.GetAxis("Horizontal") * sensitivity * Time.deltaTime;
            currentPosition.z = Input.GetAxis("Vertical") * sensitivity * Time.deltaTime;
        }

        currentPosition.y = -Input.GetAxis("Mouse ScrollWheel") * sensitivity * Time.deltaTime;

        Vector3 move = new Vector3(currentPosition.x, currentPosition.y, currentPosition.z) + desiredPosition;
        move.x = Mathf.Clamp(move.x, moveLimitsX.x, moveLimitsX.y);
        move.y = Mathf.Clamp(move.y, scrollLimitsY.x, scrollLimitsY.y);
        move.z = Mathf.Clamp(move.z, moveLimitsX.x, moveLimitsX.y);
        desiredPosition = move;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothFactor);
    }
}
