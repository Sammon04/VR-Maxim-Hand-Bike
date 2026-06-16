using UnityEngine;

public class SpinMover : MonoBehaviour
{

    [SerializeField] Rigidbody m_Spinner;
    [SerializeField] Rigidbody m_Mover;

    void FixedUpdate()
    {
        float radPerSec = 0f;
        if (m_Spinner.angularVelocity.z >= 0)
        {
            radPerSec = m_Spinner.angularVelocity.magnitude;
        } else
        {
            radPerSec = -m_Spinner.angularVelocity.magnitude;
        }
        
        
        m_Mover.AddForce(Vector3.forward * radPerSec);
    }

}
