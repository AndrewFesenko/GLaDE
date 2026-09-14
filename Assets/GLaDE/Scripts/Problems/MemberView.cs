using UnityEngine;

namespace GLaDE.Problems
{
    public enum MemberState { Normal, Highlight, Cut, Ghost, Tension, Compression, ZeroForce }

    /// <summary>One truss member (or beam segment) in the room. Knows its end points in world space and can change appearance.</summary>
    public class MemberView : MonoBehaviour
    {
        public string memberId;
        public string nodeA;
        public string nodeB;
        public MeshRenderer body;
        public CapsuleCollider capsule;
        public VisualTheme theme;

        public MemberState State { get; private set; } = MemberState.Normal;
        public Vector3 WorldA => transform.parent.TransformPoint(localA);
        public Vector3 WorldB => transform.parent.TransformPoint(localB);
        public Vector3 localA, localB;

        MemberState stateBeforeHighlight = MemberState.Normal;
        float baseRadius, length;

        /// <summary>Scales the member girth (1 = as built), e.g. to show force magnitude.</summary>
        public void SetThickness(float factor)
        {
            if (body == null) return;
            body.transform.localScale = new Vector3(baseRadius * 2 * factor, length * 0.5f, baseRadius * 2 * factor);
        }

        /// <summary>Configures the cylinder between two local points of the structure root.</summary>
        public void Configure(string id, string a, string b, Vector3 la, Vector3 lb, float radius, VisualTheme t)
        {
            memberId = id; nodeA = a; nodeB = b; localA = la; localB = lb; theme = t;
            name = "Member " + id;
            Vector3 mid = (la + lb) * 0.5f;
            Vector3 dir = lb - la;
            float len = dir.magnitude;
            transform.localPosition = mid;
            transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
            transform.localScale = Vector3.one;
            if (body == null)
            {
                var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cyl.name = "Body";
                MeshFactory.SafeDestroy(cyl.GetComponent<Collider>());
                cyl.transform.SetParent(transform, false);
                body = cyl.GetComponent<MeshRenderer>();
            }
            baseRadius = radius; length = len;
            body.transform.localScale = new Vector3(radius * 2, len * 0.5f, radius * 2);
            if (capsule == null) capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = radius * 2.2f;   // a little fat so it is easy to point at
            capsule.height = len;
            SetState(MemberState.Normal);
        }

        public void SetState(MemberState s)
        {
            State = s;
            if (body == null || theme == null) return;
            switch (s)
            {
                case MemberState.Normal: body.sharedMaterial = theme.member; break;
                case MemberState.Highlight: body.sharedMaterial = theme.memberHighlight; break;
                case MemberState.Cut: body.sharedMaterial = theme.memberCut; break;
                case MemberState.Ghost: body.sharedMaterial = theme.memberGhost; break;
                case MemberState.Tension: body.sharedMaterial = theme.memberTension; break;
                case MemberState.Compression: body.sharedMaterial = theme.memberCompression; break;
                case MemberState.ZeroForce: body.sharedMaterial = theme.memberZero; break;
            }
        }

        /// <summary>Temporary highlight that remembers the state to go back to.</summary>
        public void SetHighlighted(bool on)
        {
            if (on)
            {
                if (State != MemberState.Highlight) stateBeforeHighlight = State;
                SetState(MemberState.Highlight);
            }
            else if (State == MemberState.Highlight) SetState(stateBeforeHighlight);
        }

        /// <summary>Signed distance of the plane from each end; true if the member crosses the plane strictly between its ends.</summary>
        public bool IntersectsPlane(Plane plane, out Vector3 point, out float tFromA)
        {
            Vector3 a = WorldA, b = WorldB;
            float da = plane.GetDistanceToPoint(a), db = plane.GetDistanceToPoint(b);
            point = a; tFromA = 0;
            const float margin = 0.01f;
            if (da * db > 0) return false;
            if (Mathf.Abs(da) < margin && Mathf.Abs(db) < margin) return false; // lies in the plane
            float t = da / (da - db);
            if (t < 0.04f || t > 0.96f) return false; // do not count cuts through the joints themselves
            tFromA = t;
            point = Vector3.Lerp(a, b, t);
            return true;
        }
    }
}
