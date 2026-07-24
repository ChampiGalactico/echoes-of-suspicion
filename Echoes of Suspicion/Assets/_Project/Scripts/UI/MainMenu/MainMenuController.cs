using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public sealed class MainMenuController : MonoBehaviour
{
    [Header("Vistas")]
    [SerializeField]
    private CanvasGroup splashView;

    [SerializeField]
    private CanvasGroup mainMenuView;

    [SerializeField]
    private CanvasGroup optionsView;

    [SerializeField]
    private CanvasGroup joinView;

    [Header("Navegación")]
    [SerializeField]
    private GameObject firstSelectedButton;

    [SerializeField]
    private GameObject optionsFirstSelectedButton;

    [SerializeField]
    private GameObject joinFirstSelectedObject;

    [Header("Transición")]
    [SerializeField, Min(0.05f)]
    private float transitionDuration = 0.6f;

    private IDisposable inputSubscription;
    private Coroutine activeTransition;
    private bool menuWasOpened;

    private void Awake()
    {
        ConfigureInitialState();
    }

    private void OnEnable()
    {
        StartListeningForInput();
    }

    private void OnDisable()
    {
        StopListeningForInput();
    }

    private void ConfigureInitialState()
    {
        if (splashView == null ||
            mainMenuView == null ||
            optionsView == null ||
            joinView == null)
        {
            Debug.LogError(
                "MainMenuController: faltan referencias de las vistas.",
                this
            );

            enabled = false;
            return;
        }

        SetViewState(
            splashView,
            alpha: 1f,
            interactable: false
        );

        SetViewState(
            mainMenuView,
            alpha: 0f,
            interactable: false
        );

        SetViewState(
            optionsView,
            alpha: 0f,
            interactable: false
        );

        SetViewState(
            joinView,
            alpha: 0f,
            interactable: false
        );

        menuWasOpened = false;
    }

    private void StartListeningForInput()
    {
        if (menuWasOpened)
        {
            return;
        }

        inputSubscription =
            InputSystem.onAnyButtonPress.CallOnce(
                _ => OpenMainMenuFromSplash()
            );
    }

    private void StopListeningForInput()
    {
        inputSubscription?.Dispose();
        inputSubscription = null;
    }

    private void OpenMainMenuFromSplash()
    {
        if (menuWasOpened)
        {
            return;
        }

        menuWasOpened = true;
        StopListeningForInput();

        StartViewTransition(
            splashView,
            mainMenuView,
            firstSelectedButton,
            disableSourceObject: true
        );
    }

    public void OpenOptions()
    {
        StartViewTransition(
            mainMenuView,
            optionsView,
            optionsFirstSelectedButton,
            disableSourceObject: false
        );
    }

    public void ReturnToMainMenuFromOptions()
    {
        StartViewTransition(
            optionsView,
            mainMenuView,
            firstSelectedButton,
            disableSourceObject: false
        );
    }

    public void OpenJoinView()
    {
        StartViewTransition(
            mainMenuView,
            joinView,
            joinFirstSelectedObject,
            disableSourceObject: false
        );
    }

    public void ReturnToMainMenuFromJoin()
    {
        StartViewTransition(
            joinView,
            mainMenuView,
            firstSelectedButton,
            disableSourceObject: false
        );
    }

    private void StartViewTransition(
        CanvasGroup source,
        CanvasGroup destination,
        GameObject selectedObject,
        bool disableSourceObject
    )
    {
        if (activeTransition != null)
        {
            return;
        }

        activeTransition = StartCoroutine(
            FadeBetweenViews(
                source,
                destination,
                selectedObject,
                disableSourceObject
            )
        );
    }

    private IEnumerator FadeBetweenViews(
        CanvasGroup source,
        CanvasGroup destination,
        GameObject selectedObject,
        bool disableSourceObject
    )
    {
        source.interactable = false;
        source.blocksRaycasts = false;

        destination.gameObject.SetActive(true);
        destination.interactable = false;
        destination.blocksRaycasts = false;

        EventSystem.current?.SetSelectedGameObject(null);

        float elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / transitionDuration);

            float smoothProgress =
                progress * progress * (3f - 2f * progress);

            source.alpha = 1f - smoothProgress;
            destination.alpha = smoothProgress;

            yield return null;
        }

        source.alpha = 0f;

        if (disableSourceObject)
        {
            source.gameObject.SetActive(false);
        }

        destination.alpha = 1f;
        destination.interactable = true;
        destination.blocksRaycasts = true;

        SelectObject(selectedObject);

        activeTransition = null;
    }

    private static void SetViewState(
        CanvasGroup view,
        float alpha,
        bool interactable
    )
    {
        view.gameObject.SetActive(true);
        view.alpha = alpha;
        view.interactable = interactable;
        view.blocksRaycasts = interactable;
    }

    private static void SelectObject(GameObject selectedObject)
    {
        if (EventSystem.current == null ||
            selectedObject == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectedObject);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}