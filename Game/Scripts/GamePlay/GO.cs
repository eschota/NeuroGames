using UnityEngine;

public class GO : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        gameObject.layer = LayerMask.NameToLayer("GO");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
