using UnityEngine;

[CreateAssetMenu(menuName = "Statics/Problem Data")]
public class ProblemData : ScriptableObject
{
    public string problemName;
    public ProblemCategory category;
    public Sprite thumbnail;
    public bool unlocked;
}