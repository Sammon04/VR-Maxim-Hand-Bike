using UnityEngine;
using TMPro;

public class SpinDisplay : MonoBehaviour
{

    [SerializeField] Rigidbody m_Target;
    [SerializeField] TextMeshProUGUI m_Text;
    [SerializeField] Vector3 m_Offset = new Vector3(0f, 0.3f, 0f);
    [SerializeField] Transform m_CameraTransform;

    void LateUpdate()
    {
        transform.position = m_Target.transform.position + m_Offset;

        transform.rotation = Quaternion.LookRotation(transform.position - m_CameraTransform.position);

        float radPerSec = m_Target.angularVelocity.magnitude;
        float rpm = radPerSec * Mathf.Rad2Deg / 360f * 60f;

        m_Text.text = $"RPM: {rpm:F1}";
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
