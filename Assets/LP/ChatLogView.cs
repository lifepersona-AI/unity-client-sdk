using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LP
{
    public class ChatLogView : MonoBehaviour
    {
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject logEntryPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private bool autoScrollToBottom = true;
        [SerializeField] private bool captureUnityLogs = true;

        private void OnEnable()
        {
            LogManager.Instance.OnLogAdded += HandleLogAdded;

            if (captureUnityLogs)
            {
                Application.logMessageReceived += HandleUnityLog;
            }

            // Display existing logs
            foreach (var log in LogManager.Instance.Logs)
            {
                CreateLogUI(log);
            }
        }

        private void OnDisable()
        {
            LogManager.Instance.OnLogAdded -= HandleLogAdded;

            if (captureUnityLogs)
            {
                Application.logMessageReceived -= HandleUnityLog;
            }
        }

        private void HandleUnityLog(string message, string stackTrace, LogType type)
        {
            LogLevel level = type switch
            {
                LogType.Error or LogType.Exception or LogType.Assert => LogLevel.Error,
                LogType.Warning => LogLevel.Warning,
                _ => LogLevel.Info
            };

            LogManager.Instance.Log(message, level);
        }

        private void HandleLogAdded(LogEntry entry)
        {
            CreateLogUI(entry);

            if (autoScrollToBottom)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private void CreateLogUI(LogEntry entry)
        {
            GameObject logObj = Instantiate(logEntryPrefab, contentContainer);

            var textComponent = logObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = entry.ToString();
                textComponent.color = GetColorForLevel(entry.Level);
            }
            else
            {
                var text = logObj.GetComponentInChildren<Text>();
                if (text != null)
                {
                    text.text = entry.ToString();
                    text.color = GetColorForLevel(entry.Level);
                }
            }
        }

        private Color GetColorForLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Warning => Color.yellow,
                LogLevel.Error => Color.red,
                _ => Color.white
            };
        }

        public void ClearLogs()
        {
            foreach (Transform child in contentContainer)
            {
                Destroy(child.gameObject);
            }
            LogManager.Instance.Clear();
        }
    }
}
