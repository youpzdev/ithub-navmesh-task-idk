using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class WorkbenchDamageView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text label;

    private Camera sceneCamera;
    private WorkbenchActor owner;

    private Sequence motion;

    public float Damage { get; private set; }
    public TMP_Text Label => label;

    public void Configure(TMP_Text text) { label = text; }

    public void Show(float damage, Camera camera, WorkbenchActor actor)
    {
        motion?.Kill();
        sceneCamera = camera; owner = actor; Damage = damage;
        label.text = "-" + damage.ToString("0.##", CultureInfo.InvariantCulture);
        label.alpha = 1;
        transform.localScale = Vector3.one * 0.82f;
        transform.rotation = camera.transform.rotation;
        motion = DOTween.Sequence().Append(transform.DOMoveY(transform.position.y + 1.7f, 0.7f).SetEase(Ease.OutQuad))
            .Join(transform.DOScale(1f, 0.12f).SetEase(Ease.OutBack))
            .Insert(0.22f, DOTween.To(() => label.alpha, value => label.alpha = value, 0f, 0.48f))
            .OnComplete(Release);
    }

    private void LateUpdate()
    {
        if (motion == null) { Release(); return; }
        if (sceneCamera) transform.rotation = sceneCamera.transform.rotation;
    }

    public void Release()
    {
        if (!gameObject.activeSelf) return;
        Pooling.Destroy(gameObject);
        if (gameObject.activeSelf) { gameObject.SetActive(false); Destroy(gameObject); }
    }

    private void OnDisable()
    {
        motion?.Kill(); motion = null;
        label.alpha = 0;
        if (owner) owner.ForgetDamageView(this);
        owner = null;
    }
}
