using UnityEngine;
using UnityEngine.UI;

namespace LP
{
    [RequireComponent(typeof(Button))]
    public class LogTestButton : MonoBehaviour
    {
        [SerializeField] private float logInterval = 0.5f;
        [SerializeField] private int logsPerCycle = 5;

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
            int logType = Random.Range(0, 10);

            if (logType < 6)
            {
                LogManager.Instance.Log($"Info log #{Time.frameCount}: Operation completed successfully", LogLevel.Info);
            }
            else if (logType < 9)
            {
                LogManager.Instance.Log($"Warning log #{Time.frameCount}: Resource usage is high", LogLevel.Warning);
            }
            else
            {
                LogManager.Instance.Log($"Error log #{Time.frameCount}: Failed to process request", LogLevel.Error);
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
