using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using System.Linq;

/// <summary>
/// Переводит Unity в event-driven рендеринг — как браузер.
/// Рендер происходит только когда есть взаимодействие с UI или явный запрос.
/// Подключи этот скрипт на любой GameObject на сцене.
/// </summary>
public class EventDrivenRenderer : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("FPS в простое — 0 означает полную остановку рендера")]
    [SerializeField] private int _idleFps = 0;
    [Tooltip("FPS во время взаимодействия")]
    [SerializeField] private int _activeFps = 60;
    [Tooltip("Сколько секунд после последнего взаимодействия держать активный FPS")]
    [SerializeField] private float _activeTimeout = 0.5f;

    private float _lastInteractionTime = -999f;
    private bool _isIdle = false;

    private void Awake()
    {
        // Отключаем vsync — мы сами управляем частотой
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = _idleFps;
    }

    private void Update()
    {
        bool hasInput = CheckInputSystem();

        if (hasInput)
        {
            _lastInteractionTime = Time.unscaledTime;
            if (_isIdle)
            {
                _isIdle = false;
                Application.targetFrameRate = _activeFps;
            }
        }
        else if (!_isIdle && Time.unscaledTime - _lastInteractionTime > _activeTimeout)
        {
            _isIdle = true;
            Application.targetFrameRate = _idleFps;
        }
    }

    private bool CheckInputSystem()
    {
        // Проверяем клавиатуру
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.isPressed)
            return true;

        // Проверяем мышь
        var mouse = Mouse.current;
        if (mouse != null)
        {
            // Движение мыши
            if (mouse.delta.ReadValue() != Vector2.zero)
                return true;

            // Нажатия кнопок мыши
            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.middleButton.isPressed)
                return true;

            // Скролл
            if (mouse.scroll.ReadValue() != Vector2.zero)
                return true;
        }

        // Проверяем геймпад
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            // Проверяем все кнопки
            if (gamepad.aButton.isPressed || gamepad.bButton.isPressed || 
                gamepad.xButton.isPressed || gamepad.yButton.isPressed ||
                gamepad.leftShoulder.isPressed || gamepad.rightShoulder.isPressed ||
                gamepad.startButton.isPressed || gamepad.selectButton.isPressed)
                return true;

            if (gamepad.leftStick.ReadValue() != Vector2.zero || gamepad.rightStick.ReadValue() != Vector2.zero)
                return true;

            if (gamepad.leftTrigger.ReadValue() > 0 || gamepad.rightTrigger.ReadValue() > 0)
                return true;
        }

        // Проверяем тачскрин
        var touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.touches.Count > 0)
            return true;

        return false;
    }

    /// <summary>
    /// Вызови этот метод если хочешь принудительно отрендерить один кадр
    /// например после программного изменения UI без взаимодействия пользователя
    /// </summary>
    public void RequestRender(float durationSeconds = 0.1f)
    {
        _lastInteractionTime = Time.unscaledTime + durationSeconds;
        _isIdle = false;
        Application.targetFrameRate = _activeFps;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        // Когда окно теряет фокус — сразу в простой
        if (!hasFocus)
        {
            _isIdle = true;
            Application.targetFrameRate = _idleFps;
        }
    }
}