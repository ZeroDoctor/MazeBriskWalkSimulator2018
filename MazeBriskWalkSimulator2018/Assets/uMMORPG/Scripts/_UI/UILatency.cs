using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class UILatency : MonoBehaviour
{
    public Text latencyText;

    public float goodThreshold = 0.3f;
    public float okayThreshold = 2;

    public Color goodColor = Color.green;
    public Color okayColor = Color.yellow;
    public Color badColor = Color.red;


    void Update()
    {
        
    }
}
