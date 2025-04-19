using UnityEngine;

public class GO_AIM : GO
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    { 
        gameObject.layer = LayerMask.NameToLayer("AIM");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
