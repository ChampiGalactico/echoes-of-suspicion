using TMPro;
using UnityEngine;

/// <summary>
/// Attach to the ObjectiveText TMP element inside each role's HUD panel.
/// Registers itself so ObjectiveManager can find it on the local client.
///
/// SETUP:
/// 1. In the Runner's ObjectivePanel → ObjectiveText, add this component.
/// 2. In the Guide's ObjectivePanel → ObjectiveText, add this component.
/// 3. Set the role field to match (Runner or Guide).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class ObjectiveDisplay : MonoBehaviour
{
    [SerializeField]
    private PlayerRole role;

    private TMP_Text _text;

    // ── Static access per role ───────────────────────────

    private static ObjectiveDisplay _runnerDisplay;
    private static ObjectiveDisplay _guideDisplay;

    /// <summary>Get the display for a given role. May be null if HUD not loaded.</summary>
    public static ObjectiveDisplay GetDisplay(PlayerRole r)
    {
        return r == PlayerRole.Runner ? _runnerDisplay : _guideDisplay;
    }

    // ── Lifecycle ────────────────────────────────────────

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();

        if (role == PlayerRole.Runner)
            _runnerDisplay = this;
        else
            _guideDisplay = this;
    }

    private void OnDestroy()
    {
        if (role == PlayerRole.Runner && _runnerDisplay == this)
            _runnerDisplay = null;
        else if (role == PlayerRole.Guide && _guideDisplay == this)
            _guideDisplay = null;
    }

    // ── Public API ───────────────────────────────────────

    public void SetObjective(string text)
    {
        if (_text != null)
            _text.text = text;
    }

    public string GetObjective()
    {
        return _text != null ? _text.text : "";
    }
}
