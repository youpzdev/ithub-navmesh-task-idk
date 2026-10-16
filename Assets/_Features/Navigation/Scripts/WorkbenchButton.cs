using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WorkbenchButton : Button
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private AudioClip clickSound;
    [SerializeField] private Color idleColor = new(0.19f, 0.21f, 0.25f);
    [SerializeField] private Color selectedColor = new(0.16f, 0.43f, 0.61f);
    private bool selected;
    private Tween scaleTween;

    public void Configure(TMP_Text text) { label = text; }
    public void ConfigureAudio(AudioClip clip) { clickSound = clip; }
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && IsActive() && IsInteractable() && clickSound) Audio.Play(clickSound, 0.45f);
        base.OnPointerClick(eventData);
    }

    public void SetText(string text) { if (label.text != text) label.text = text; }
    public void SetSelected(bool value)
    {
        if (selected == value) return;
        selected = value;
        RefreshColor();
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        base.OnPointerDown(eventData);
        if (eventData.button == PointerEventData.InputButton.Left && IsInteractable()) ScaleTo(0.94f, 0.07f);
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        if (eventData.button == PointerEventData.InputButton.Left) ScaleTo(1f, 0.2f);
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (IsInteractable()) ScaleTo(1.015f, 0.12f);
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        ScaleTo(1f, 0.16f);
    }

    public override void OnSubmit(BaseEventData eventData)
    {
        if (IsActive() && IsInteractable() && clickSound) Audio.Play(clickSound, 0.45f);
        base.OnSubmit(eventData);
        if (!IsInteractable()) return;
        transform.localScale = Vector3.one * 0.94f;
        ScaleTo(1f, 0.2f);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        RefreshColor();
    }

    private void RefreshColor()
    {
        if (!targetGraphic) return;
        targetGraphic.color = !IsInteractable() ? new Color(0.13f, 0.15f, 0.18f) : selected ? selectedColor : idleColor;
        if (label) label.color = IsInteractable() ? new Color(0.94f, 0.96f, 0.98f) : new Color(0.48f, 0.52f, 0.58f);
    }

    private void ScaleTo(float scale, float duration)
    {
        scaleTween?.Kill();
        scaleTween = transform.DOScale(scale, duration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        transform.localScale = Vector3.one;
        RefreshColor();
    }

    protected override void OnDisable()
    {
        scaleTween?.Kill();
        scaleTween = null;
        transform.localScale = Vector3.one;
        base.OnDisable();
    }
}
