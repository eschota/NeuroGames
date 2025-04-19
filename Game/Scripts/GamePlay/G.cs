using UnityEngine;
using System;

public class G : MonoBehaviour
{
    public static event Action<int> ChangeState;
    public static State CurrentState;
    public enum State
    {
        Start,
        Pause,
        Resume,
        Playing,
        Win,
        Lose,
        Validation
    }

    private static string[] StateNames = {
        "Старт",
        "Пауза",
        "Продолжить",
        "Играем",
        "Победа!",
        "Поражение",
        "Валидация"
    };

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        Debug.Log("Start");
        CurrentState = State.Start;
        ChangeState?.Invoke(0);
    }

    // Update is called once per frame
    void Update()
    {
        InputKeyBoard();
        
        // Обновляем UI состояния
        if (GUI.changed)
        {
            UpdateGameState();
        }
    }

    void InputKeyBoard()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CurrentState == State.Pause)
            {
                CurrentState = State.Resume;
                Debug.Log("Resume");
                ChangeState?.Invoke(1);
            }
            else if (CurrentState != State.Win) // Не ставим на паузу после победы
            {
                CurrentState = State.Pause;
                Debug.Log("Pause");
                ChangeState?.Invoke(0);
            }
        }
        if (Input.GetKeyDown(KeyCode.R))    
        { 
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
            Debug.Log("Restart");
        }
    }

    void OnGUI()
    {
        // Показываем текущее состояние игры
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 24;
        style.normal.textColor = Color.white;
        
        // Выбираем цвет в зависимости от состояния
        switch (CurrentState)
        {
            case State.Win:
                style.normal.textColor = Color.green;
                break;
            case State.Lose:
                style.normal.textColor = Color.red;
                break;
            case State.Validation:
                style.normal.textColor = Color.yellow;
                break;
            case State.Pause:
                style.normal.textColor = new Color(1f, 0.5f, 0f); // Оранжевый
                break;
        }
        
        // Показываем состояние в верхнем правом углу
        GUI.Label(new Rect(Screen.width - 200, 10, 190, 30), 
                 StateNames[(int)CurrentState], style);
        
        // Показываем подсказки по управлению
        if (CurrentState == State.Win)
        {
            style.fontSize = 20;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width/2 - 100, Screen.height - 40, 200, 30),
                     "Нажмите R для рестарта", style);
        }
    }

    private void UpdateGameState()
    {
        // Обновляем состояние игры
        switch (CurrentState)
        {
            case State.Pause:
                Time.timeScale = 0f;
                break;
            case State.Resume:
            case State.Playing:
                Time.timeScale = 1f;
                break;
            case State.Win:
                // Можно добавить эффекты победы
                break;
            case State.Validation:
                // Специальные настройки для валидационного раунда
                break;
        }
    }
}
