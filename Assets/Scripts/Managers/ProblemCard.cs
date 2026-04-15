using UnityEngine;
using UnityEngine.SceneManagement;

public enum ProblemCategory
{
    Truss,
    Beam,
    Frame,
    Pulley,
    Friction
}

public class ProblemCard : MonoBehaviour
{
    public string sceneName;
    public ProblemCategory category;

    public void LoadProblem()
    {
        SceneManager.LoadScene(sceneName);
    }
}