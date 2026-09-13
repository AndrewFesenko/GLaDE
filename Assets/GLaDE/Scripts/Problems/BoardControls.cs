using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>Convenience moves for a grabbable board: snap to face the player, and toggle a larger size.</summary>
    public class BoardControls : MonoBehaviour
    {
        public float bigScale = 1.35f;
        public float faceDistance = 0f;   // 0 = keep current distance

        Vector3 homePos; Quaternion homeRot; Vector3 homeScale;
        bool big;

        void Awake()
        {
            homePos = transform.position; homeRot = transform.rotation; homeScale = transform.localScale;
        }

        /// <summary>Turns the board to face the player's head and levels it.</summary>
        public void FaceMe()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 toBoard = transform.position - cam.transform.position;
            if (faceDistance > 0f) transform.position = cam.transform.position + toBoard.normalized * faceDistance;
            toBoard = transform.position - cam.transform.position;
            transform.rotation = Quaternion.LookRotation(toBoard.normalized, Vector3.up);   // +z away from the viewer; front face (-z) toward them
        }

        public void ToggleSize()
        {
            big = !big;
            transform.localScale = homeScale * (big ? bigScale : 1f);
        }

        public bool IsBig => big;

        public void ResetPose()
        {
            transform.SetPositionAndRotation(homePos, homeRot);
            transform.localScale = homeScale; big = false;
        }
    }
}
