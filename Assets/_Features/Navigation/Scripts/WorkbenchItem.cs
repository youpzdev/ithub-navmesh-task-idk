using DG.Tweening;
using TMPro;
using UnityEngine;

public class WorkbenchItem : MonoBehaviour
{
    [SerializeField] private Transform visual;
    [SerializeField] private TMP_Text number;
    [SerializeField] private Renderer ring;

    private bool pulsing;

    public int Index { get; set; }
    public bool IsPoint { get; set; }
    public int Kind { get; set; }
    public Transform Visual => visual;

    public void Configure(Transform model, TMP_Text label, Renderer marker) { visual = model; number = label; ring = marker; }

    public void Show(int index, bool active, bool blocked)
    {
        Index = index;
        if (number != null) number.text = blocked ? "X" : (index + 1).ToString();
        if (ring != null) ring.material.color = blocked ? new Color(1, 0.2f, 0.23f) : active ? new Color(0.1f, 0.8f, 0.9f) : new Color(0.45f, 0.57f, 0.64f);
        if (IsPoint && number != null && pulsing != (active && !blocked))
        {
            pulsing = active && !blocked;
            number.transform.DOKill(); number.transform.localScale = Vector3.one;
            if (active && !blocked) number.transform.DOScale(1.15f, 0.65f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        }
    }

    public void Drop(AudioClip impactSound)
    {
        if (visual == null) return;
        visual.DOKill();
        if (number != null) number.transform.DOKill();
        visual.localPosition = Vector3.up * 6;
        visual.DOLocalMoveY(0, 0.3f).SetEase(Ease.InQuad).SetUpdate(true).OnComplete(() =>
        {
            Audio.Play(impactSound, 0.55f);
            visual.DOPunchScale(new Vector3(0.12f, -0.12f, 0.12f), 0.28f, 5, 0.4f);
            WorkbenchEffects.Emit(transform.position, new Color(0.72f, 0.78f, 0.8f), 18);
            WorkbenchEffects.Shake(0.12f);
        });
    }

    private void OnDisable()
    {
        pulsing = false;
        if (number != null) { number.transform.DOKill(); number.transform.localScale = Vector3.one; }
        if (visual == null) return;
        visual.DOKill();
        visual.localPosition = Vector3.zero;
        visual.localScale = Vector3.one;
    }
}
