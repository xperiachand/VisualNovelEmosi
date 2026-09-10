using System;
using UnityEngine;
using UnityEngine.UI;

namespace Emotionalaw
{
    public sealed class CharacterPortrait : MonoBehaviour
    {
        [Serializable]
        private struct Expression
        {
            public string mood;
            public Sprite sprite;
        }

        [SerializeField] private string characterName;
        [SerializeField] private Image portrait;
        [SerializeField] private Sprite neutral;
        [SerializeField] private Expression[] expressions;
        [SerializeField] private Color speakingColor = Color.white;
        [SerializeField] private Color listeningColor = new Color(.52f, .54f, .60f, 1);

        public void Present(string speaker, string mood)
        {
            bool speaking = speaker == characterName;
            portrait.color = speaking ? speakingColor : listeningColor;
            if (!speaking) return;
            portrait.sprite = neutral;
            foreach (var expression in expressions)
                if (expression.mood == mood && expression.sprite != null)
                {
                    portrait.sprite = expression.sprite;
                    break;
                }
        }

        public void ResetPortrait()
        {
            portrait.sprite = neutral;
            portrait.color = listeningColor;
        }
    }
}
