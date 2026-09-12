using UnityEngine;

public class LightManager : MonoBehaviour
{

    public GameObject lightObject;

    public GameObject targetObject;

    public GameObject neutral_target;

    public bool aimlight = false;
    public float lerpSpeed = 2.0f;

    private float lerpProgress = 0.0f;

    void Update()
    {

        aimlight = targetObject.activeSelf;

        float targetProgress = aimlight ? 1.0f : 0.0f;
        lerpProgress = Mathf.Lerp(lerpProgress, targetProgress, Time.deltaTime * lerpSpeed);

        Vector3 targetPosition = Vector3.Lerp(
            neutral_target.transform.position,
            targetObject.transform.position,
            lerpProgress
        );


        if(targetProgress <= 1)
        {
            lightObject.transform.LookAt(targetPosition);
        }
        
    }
}