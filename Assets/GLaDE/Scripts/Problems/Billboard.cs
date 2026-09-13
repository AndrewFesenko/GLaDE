using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>Keeps a label facing the player's head. Yaw only by default so text stays upright.</summary>
    public class Billboard : MonoBehaviour
    {
        public bool yawOnly = true;
        Transform target;

        void LateUpdate()
        {
            if (target == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                target = cam.transform;
            }
            Vector3 dir = transform.position - target.position;
            if (yawOnly) dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
    }
}
