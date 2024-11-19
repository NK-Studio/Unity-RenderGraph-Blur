using UnityEngine;

public class ResetPosition : MonoBehaviour
{
    [SerializeField] private Vector3 initPosition;
    
    private void OnEnable()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
            rectTransform.anchoredPosition = initPosition;
        else
            transform.position = initPosition;
    }
}
