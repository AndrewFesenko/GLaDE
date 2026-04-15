using UnityEngine;
using TMPro;

public class StaticsProblem : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text problemText;
    public TMP_Text answerText;

    [Header("Load Labels (Text beside each bar)")]
    public TMP_Text lbLabel;
    public TMP_Text kcLabel;
    public TMP_Text jdLabel;

    [Header("Dimension Labels")]
    public TMP_Text heightLabel;
    public TMP_Text lengthLabel;

    [Header("Beam Meshes")]
    public GameObject beamKJ;
    public GameObject beamKD;
    public GameObject beamCD;

    [Header("Load Arrows (Optional)")]
    public GameObject arrowLB;
    public GameObject arrowKC;
    public GameObject arrowJD;

    [Header("Buttons")]
    public TMP_Text showAnswerButtonText;   // Text label on the Show/Hide Answer button

    double Flb, Fkc, Fjd;
    double height, length;
    double Gy, Ay, phi, Fkj, Fkd, Fcd;

    string baseProblemText =
        "Statics Problem F6-9\nDetermine the forces in members KJ, KD, and CD.";

    bool answerVisible = false;

    void Start()
    {
        if (problemText) problemText.text = baseProblemText;
        ShuffleProblem();   // create first random problem immediately
    }

    public void ShuffleProblem()
    {
        Flb = Random.Range(-20, 21);
        Fkc = Random.Range(-20, 21);
        Fjd = Random.Range(-20, 21);
        height = Random.Range(2, 11);
        length = Random.Range(2, 11);

        if (lbLabel) lbLabel.text = $"{Flb:F1} kN";
        if (kcLabel) kcLabel.text = $"{Fkc:F1} kN";
        if (jdLabel) jdLabel.text = $"{Fjd:F1} kN";
        if (heightLabel) heightLabel.text = $"{height:F1} m";
        if (lengthLabel) lengthLabel.text = $"{length:F1} m";

        ResetColors();
        if (answerText) answerText.text = "";
        answerVisible = false;
        if (showAnswerButtonText) showAnswerButtonText.text = "Show Answer";

        if (problemText)
        {
            problemText.text = "New random problem generated!";
            CancelInvoke(nameof(RestoreProblemText));
            Invoke(nameof(RestoreProblemText), 1f);
        }

        UpdateArrow(arrowLB, Flb);
        UpdateArrow(arrowKC, Fkc);
        UpdateArrow(arrowJD, Fjd);
    }

    void RestoreProblemText()
    {
        if (problemText) problemText.text = baseProblemText;
    }

    void ResetColors()
    {
        ResetBeam(beamKJ);
        ResetBeam(beamKD);
        ResetBeam(beamCD);
    }

    void ResetBeam(GameObject beam)
    {
        if (!beam) return;
        var r = beam.GetComponent<MeshRenderer>();
        if (r) r.material.color = Color.gray;
    }

    void UpdateArrow(GameObject arrow, double force)
    {
        if (!arrow) return;
        float magnitude = Mathf.Abs((float)force);
        float newScaleY = 0.8f + (magnitude / 20f) * 1.2f;
        arrow.transform.localScale = new Vector3(0.05f, newScaleY, 0.05f);
        arrow.transform.localRotation = force >= 0
            ? Quaternion.identity
            : Quaternion.Euler(180, 0, 0);
    }

    public void Solve()
    {
        double sumFy = Flb + Fkc + Fjd;
        Gy = ((Fjd * (length * 3.0)) + (Fkc * (2.0 * length)) + (Flb * (1.0 * length))) / (length * 6.0);
        Ay = sumFy - Gy;
        phi = System.Math.Atan(length / height);
        double a = (-Fkc * length) - (Flb * 2.0 * length) + (Ay * 3.0 * length);
        Fkj = -a / height;
        Fkd = (Ay - Flb - Fkc) / System.Math.Cos(phi);
        Fcd = -Fkj - Fkd * System.Math.Sin(phi);
    }

    string TC(double v) => v < 0 ? "Compression" : "Tension";

    void ColorBeam(GameObject beam, double force)
    {
        if (!beam) return;
        var r = beam.GetComponent<MeshRenderer>();
        if (!r) return;
        r.material.color = force > 0 ? Color.blue : Color.red;
    }

    public void OnShowAnswerClicked()
    {
        // Toggle between showing and hiding the answer
        if (!answerVisible)
        {
            Solve();

            if (answerText)
            {
                answerText.text =
                    $"Gy = {Gy:F2} kN\n" +
                    $"Ay = {Ay:F2} kN\n" +
                    $"Fkj = {Fkj:F2} kN ({TC(Fkj)})\n" +
                    $"Fkd = {Fkd:F2} kN ({TC(Fkd)})\n" +
                    $"Fcd = {Fcd:F2} kN ({TC(Fcd)})";
            }

            ColorBeam(beamKJ, Fkj);
            ColorBeam(beamKD, Fkd);
            ColorBeam(beamCD, Fcd);

            answerVisible = true;
            if (showAnswerButtonText) showAnswerButtonText.text = "Hide Answer";
        }
        else
        {
            if (answerText) answerText.text = "";
            ResetColors();
            answerVisible = false;
            if (showAnswerButtonText) showAnswerButtonText.text = "Show Answer";
        }
    }
}
