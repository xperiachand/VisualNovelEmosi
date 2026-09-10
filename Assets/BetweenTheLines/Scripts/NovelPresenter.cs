#nullable enable
using System;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

namespace Emotionalaw
{
    // All visual objects are serialized in BetweenTheLines.unity, including the three choices.
    public sealed class NovelPresenter : DialoguePresenterBase
    {
        [SerializeField] private NovelFlow flow = null!;
        [SerializeField] private GameObject linePanel = null!;
        [SerializeField] private Text speakerText = null!;
        [SerializeField] private Text lineText = null!;
        [SerializeField] private Text continueText = null!;
        [SerializeField] private Button continueButton = null!;
        [SerializeField] private GameObject choicesPanel = null!;
        [SerializeField] private Text questionText = null!;
        [SerializeField] private Text choiceKindText = null!;
        [SerializeField] private Button[] choiceButtons = null!;
        [SerializeField] private Text[] choiceLabels = null!;
        [SerializeField] private CharacterPortrait[] portraits = null!;
        [SerializeField, Min(0)] private float secondsPerCharacter = .022f;

        private bool lineActive;
        private bool typing;
        private bool hurry;
        private bool advance;
        private bool choosing;
        private int selected = -1;
        private string question = "";
        private string kind = "WHAT DO YOU SAY?";
        private string lastLine = "";

        public bool IsPresentingLine => lineActive;
        public bool IsChoosing => choosing;
        public Button[] ChoiceButtons => choiceButtons;
        public Button ContinueButton => continueButton;

        private void Awake()
        {
            continueButton.onClick.AddListener(Continue);
            choiceButtons[0].onClick.AddListener(ChooseFirst);
            choiceButtons[1].onClick.AddListener(ChooseSecond);
            choiceButtons[2].onClick.AddListener(ChooseThird);
        }

        private void OnDestroy()
        {
            continueButton.onClick.RemoveListener(Continue);
            choiceButtons[0].onClick.RemoveListener(ChooseFirst);
            choiceButtons[1].onClick.RemoveListener(ChooseSecond);
            choiceButtons[2].onClick.RemoveListener(ChooseThird);
        }

        public void Continue()
        {
            if (!lineActive || flow.IsModalOpen) return;
            if (typing) hurry = true;
            else advance = true;
        }

        private void ChooseFirst() => Choose(0);
        private void ChooseSecond() => Choose(1);
        private void ChooseThird() => Choose(2);
        private void Choose(int index)
        {
            if (!choosing || flow.IsModalOpen || !choiceButtons[index].interactable) return;
            selected = index;
            choosing = false;
            foreach (var button in choiceButtons) button.interactable = false;
        }

        public void SetQuestion(string label, string text)
        {
            kind = label;
            question = text;
        }

        public void ResetPresentation()
        {
            lineActive = typing = choosing = advance = hurry = false;
            selected = -1;
            question = lastLine = "";
            kind = "WHAT DO YOU SAY?";
            linePanel.SetActive(false);
            choicesPanel.SetActive(false);
            foreach (var portrait in portraits) portrait.ResetPortrait();
        }

        public override YarnTask OnDialogueStartedAsync()
        {
            ResetPresentation();
            return YarnTask.CompletedTask;
        }

        public override YarnTask OnDialogueCompleteAsync()
        {
            lineActive = choosing = false;
            linePanel.SetActive(false);
            choicesPanel.SetActive(false);
            return YarnTask.CompletedTask;
        }

        public override async YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
        {
            choicesPanel.SetActive(false);
            linePanel.SetActive(true);
            lastLine = line.TextWithoutCharacterName.Text;
            speakerText.text = line.CharacterName == "Player" ? "YOU" : (line.CharacterName ?? "").ToUpperInvariant();
            string mood = "neutral";
            foreach (string tag in line.Metadata ?? Array.Empty<string>())
                if (tag.StartsWith("mood:")) mood = tag.Substring(5);
            foreach (var portrait in portraits) portrait.Present(line.CharacterName ?? "", mood);
            lineActive = typing = true;
            hurry = advance = false;
            continueText.text = "TAP TO REVEAL";
            continueButton.Select();
            float elapsed = 0;
            int count = 0;
            lineText.text = "";
            while (count < lastLine.Length && !hurry && !token.IsHurryUpRequested && !token.IsNextContentRequested)
            {
                if (!flow.IsModalOpen)
                {
                    elapsed += Time.unscaledDeltaTime;
                    count = secondsPerCharacter <= 0 ? lastLine.Length : Mathf.Min(lastLine.Length, (int)(elapsed / secondsPerCharacter));
                    lineText.text = lastLine.Substring(0, count);
                }
                await YarnTask.Yield();
            }
            lineText.text = lastLine;
            typing = false;
            continueText.text = "CONTINUE  >";
            while (!advance && !token.IsNextContentRequested) await YarnTask.Yield();
            lineActive = false;
        }

        public override async YarnTask<DialogueOption?> RunOptionsAsync(DialogueOption[] options, LineCancellationToken token)
        {
            if (options.Length != 3 || choiceButtons.Length != 3 || choiceLabels.Length != 3)
                throw new InvalidOperationException("Between the Lines requires exactly three serialized choices.");
            lineActive = false;
            linePanel.SetActive(false);
            questionText.text = string.IsNullOrEmpty(question) ? lastLine : question;
            choiceKindText.text = kind;
            question = "";
            kind = "WHAT DO YOU SAY?";
            selected = -1;
            for (int i = 0; i < 3; i++)
            {
                choiceLabels[i].text = options[i].Line.TextWithoutCharacterName.Text;
                choiceButtons[i].interactable = options[i].IsAvailable;
            }
            choosing = true;
            choicesPanel.SetActive(true);
            choiceButtons[0].Select();
            while (selected < 0 && !token.IsNextContentRequested) await YarnTask.Yield();
            choosing = false;
            choicesPanel.SetActive(false);
            return selected >= 0 ? options[selected] : null;
        }
    }
}
