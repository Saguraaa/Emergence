using System;
using UnityEngine;
using UnityEngine.UI;

namespace Emergence.UI.Settings
{
    public sealed class TabContainer : MonoBehaviour
    {
        [SerializeField] private Button[] tabs;
        [SerializeField] private GameObject[] pages;
        private Text[] labels;
        public event Action<int> TabChanged;

        public void Initialize()
        {
            if (tabs == null || tabs.Length != 3 || pages == null || pages.Length != 3)
                throw new MissingReferenceException(name + ": configure the display, audio and controls tabs and pages.");
            labels = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                if (tabs[i] == null || pages[i] == null)
                    throw new MissingReferenceException(name + ": tabs[" + i + "] and pages[" + i + "] are required.");
                labels[i] = tabs[i].GetComponentInChildren<Text>();
                if (tabs[i].image == null || labels[i] == null)
                    throw new MissingReferenceException(tabs[i].name + ": Image and Text are required.");
            }
            tabs[0].onClick.AddListener(ShowDisplay);
            tabs[1].onClick.AddListener(ShowAudio);
            tabs[2].onClick.AddListener(ShowControls);
            Select(0);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= 3) throw new ArgumentOutOfRangeException(nameof(index));
            for (int i = 0; i < 3; i++)
            {
                bool selected = i == index;
                pages[i].SetActive(selected);
                tabs[i].image.color = selected ? new Color32(255, 224, 101, 255) : new Color32(39, 57, 69, 255);
                labels[i].color = selected ? new Color32(20, 24, 27, 255) : new Color32(239, 234, 211, 255);
            }
            TabChanged?.Invoke(index);
        }

        public void Shutdown()
        {
            tabs[0].onClick.RemoveListener(ShowDisplay);
            tabs[1].onClick.RemoveListener(ShowAudio);
            tabs[2].onClick.RemoveListener(ShowControls);
        }

        private void ShowDisplay() => Select(0);
        private void ShowAudio() => Select(1);
        private void ShowControls() => Select(2);
    }
}
