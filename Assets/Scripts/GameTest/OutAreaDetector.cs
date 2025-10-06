using Unity.VisualScripting;
using UnityEngine;

public class OutAreaDetector : MonoBehaviour
{
    public static OutAreaDetector i;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log(other.name+" HIT!!");
        if (other.gameObject.CompareTag("Ball"))
        {
            GameManager.i.GameFail();
        }
    }
}
