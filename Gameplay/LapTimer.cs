using TMPro;
using UnityEngine;

namespace MultiplayerFramework
{
    public class LapTimer : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI timerText;

        private bool isRunning = false;
        private float elapsedTime = 0f;

        public void StartTimer()
        {
            elapsedTime = 0f;
            isRunning = true;
            if (timerText != null)
                timerText.gameObject.SetActive(true);
        }

        public void StopTimer()
        {
            isRunning = false;
        }

        public void ResetTimer()
        {
            isRunning = false;
            elapsedTime = 0f;

            if (timerText != null)
                timerText.text = "0:00.000"; // alapértelmezett megjelenítés

            StartTimer();
        }

        void Update()
        {
            if (!isRunning) return;
            if (timerText == null) return;

            elapsedTime += Time.deltaTime;

            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            int milliseconds = Mathf.FloorToInt((elapsedTime * 1000f) % 1000f);

            timerText.text = $"{minutes}:{seconds:00}.{milliseconds:000}";
        }
    }
}