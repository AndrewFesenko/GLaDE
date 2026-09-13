using UnityEngine;

namespace GLaDE.Problems
{
    /// <summary>Inspector-wirable "back to the hub" action for UI buttons.</summary>
    public class ReturnToHubButton : MonoBehaviour
    {
        public void Go() => ProblemHub.ReturnToHub();
    }
}
