using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    #if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine;
public class WebGLKeyboardCapture : MonoBehaviour
{
    void Start()
    {
        WebGLInput.captureAllKeyboardInput = true;
    }
}
#endif

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
