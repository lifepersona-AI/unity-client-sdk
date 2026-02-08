using UnityEngine;
using UnityEngine.UI;

namespace LP
{
    [RequireComponent(typeof(Button))]
    public class LogTestButton : MonoBehaviour
    {
        [SerializeField] private float logInterval = 0.3f;
        [SerializeField] private int logsPerCycle = 20;

        private Button _button;
        private bool _isGenerating;
        private float _timer;
        private int _logCount;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(ToggleLogGeneration);
        }

        private void Update()
        {
            if (!_isGenerating) return;

            _timer += Time.deltaTime;
            if (_timer >= logInterval)
            {
                _timer = 0f;
                GenerateRandomLog();

                _logCount++;
                if (_logCount >= logsPerCycle)
                {
                    _logCount = 0;
                    _isGenerating = false;
                    UpdateButtonText();
                }
            }
        }

        private void ToggleLogGeneration()
        {
            _isGenerating = !_isGenerating;
            _logCount = 0;
            _timer = 0f;
            UpdateButtonText();
        }

        private void UpdateButtonText()
        {
            var text = _button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.text = _isGenerating ? "Generating..." : "Generate Logs";
            }

            var tmpText = _button.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = _isGenerating ? "Generating..." : "Generate Logs";
            }
        }

        private void GenerateRandomLog()
        {
            string[] shortMessages = new[]
            {
                "Connection established",
                "Task completed",
                "Data saved",
                "Request sent",
                "Response received"
            };

            string[] mediumMessages = new[]
            {
                "User authentication successful for session ID: {0}",
                "Network request completed with status code: {0}",
                "Loading configuration from remote server...",
                "Processing batch operation with {0} items",
                "Cache updated with {0} new entries"
            };

            string[] longMessages = new[]
            {
                "Performing database synchronization: This operation may take several minutes depending on the amount of data. Please ensure stable network connection during this process.",
                "Application performance metrics: CPU usage at {0}%, Memory: {1}MB, Network latency: {2}ms. System is operating within normal parameters.",
                "Multiple validation errors detected in the submitted form data. Please review the following fields and ensure all required information is provided correctly before resubmitting.",
                "Background task execution completed. Total execution time: {0} seconds. Processed {1} records with {2} warnings and {3} errors encountered during the operation."
            };

            int logType = Random.Range(0, 10);
            int lengthType = Random.Range(0, 10);
            string message;

            // Pick message length (40% short, 40% medium, 20% long)
            if (lengthType < 4)
            {
                message = shortMessages[Random.Range(0, shortMessages.Length)];
            }
            else if (lengthType < 8)
            {
                string template = mediumMessages[Random.Range(0, mediumMessages.Length)];
                message = string.Format(template, Random.Range(1000, 9999));
            }
            else
            {
                string template = longMessages[Random.Range(0, longMessages.Length)];
                message = string.Format(template, Random.Range(10, 60), Random.Range(100, 999), Random.Range(10, 200), Random.Range(0, 5));
            }

            // Pick log level (60% info, 30% warning, 10% error)
            if (logType < 6)
            {
                LogManager.Instance.Log(message, LogLevel.Info);
            }
            else if (logType < 9)
            {
                LogManager.Instance.Log($"Warning: {message}", LogLevel.Warning);
            }
            else
            {
                LogManager.Instance.Log($"Error: {message}", LogLevel.Error);
            }
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(ToggleLogGeneration);
            }
        }
    }
}
