using UnityEngine;
using UnityEngine.XR;

namespace GLaDE.Problems
{
    /// <summary>
    /// Spawns the XR Interaction Simulator when running in the editor without a headset, so the whole
    /// experience can be driven with mouse and keyboard. Does nothing in builds or when an HMD is active.
    /// </summary>
    public class SimulatorBootstrap : MonoBehaviour
    {
        public GameObject simulatorPrefab;
        public bool onlyInEditor = true;

        static GameObject s_Instance;

        void Awake()
        {
            if (simulatorPrefab == null) return;
            if (onlyInEditor && !Application.isEditor) return;
            if (XRSettings.isDeviceActive) return;
            if (s_Instance != null) return;
            s_Instance = Instantiate(simulatorPrefab);
            s_Instance.name = "XR Interaction Simulator (auto)";
        }
    }
}
