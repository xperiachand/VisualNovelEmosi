using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yarn.Unity;

namespace Emotionalaw
{
    public sealed class NovelFlow : MonoBehaviour
    {
        [Serializable]
        private struct ClueSlot
        {
            public string variable;
            [TextArea] public string description;
            public Text label;
        }

        [Header("Story")]
        [SerializeField] private DialogueRunner runner;
        [SerializeField] private NovelPresenter presenter;
        [SerializeField] private ClueSlot[] clues;
        [Header("Authored screens")]
        [SerializeField] private GameObject startScreen;
        [SerializeField] private GameObject howToScreen;
        [SerializeField] private GameObject novelScreen;
        [SerializeField] private GameObject endScreen;
        [Header("HUD")]
        [SerializeField] private Text clueCountText;
        [SerializeField] private Text chapterText;
        [Header("Clue and interpretation popup")]
        [SerializeField] private GameObject popup;
        [SerializeField] private Text popupTitle;
        [SerializeField] private Text popupBody;
        [SerializeField] private Button popupContinue;
        [Header("Notebook / final review")]
        [SerializeField] private GameObject notebook;
        [SerializeField] private Text notebookSubtitle;
        [SerializeField] private Text notebookCloseLabel;
        [SerializeField] private Button notebookClose;
        [Header("Ending")]
        [SerializeField] private Text endingKind;
        [SerializeField] private Text endingTitle;
        [SerializeField] private Text endingSupport;
        [SerializeField] private Text endingStats;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button startButton;

        private bool popupDismissed;
        private bool reviewDismissed;
        private bool starting;
        private GameObject priorSelection;
        public bool IsModalOpen => popup.activeSelf || notebook.activeSelf;
        public bool IsAtEnding => endScreen.activeSelf;
        public DialogueRunner Runner => runner;
        public Button PopupContinue => popupContinue;
        public Button NotebookClose => notebookClose;
        public bool PopupOpen => popup.activeSelf;
        public bool NotebookOpen => notebook.activeSelf;

        private void Awake()
        {
            runner.AddCommandHandler<string>("clue", RevealClue);
            runner.AddCommandHandler<string>("feedback", ShowFeedback);
            runner.AddCommandHandler("review", ReviewClues);
            runner.AddCommandHandler<string, string>("question", presenter.SetQuestion);
            runner.AddCommandHandler<string>("chapter", SetChapter);
            runner.AddCommandHandler<string>("ending", ShowEnding);
            popupContinue.onClick.AddListener(DismissPopup);
            notebookClose.onClick.AddListener(CloseNotebook);
        }

        private void Start()
        {
            popup.SetActive(false);
            notebook.SetActive(false);
            ShowScreen(startScreen);
            startButton.Select();
        }

        private void OnDestroy()
        {
            popupDismissed = reviewDismissed = true;
            popupContinue.onClick.RemoveListener(DismissPopup);
            notebookClose.onClick.RemoveListener(CloseNotebook);
            foreach (string command in new[] { "clue", "feedback", "review", "question", "chapter", "ending" })
                runner.RemoveCommandHandler(command);
        }

        public bool ReadBool(string variable) => runner.VariableStorage.TryGetValue(variable, out bool value) && value;
        public string ReadString(string variable) => runner.VariableStorage.TryGetValue(variable, out string value) ? value : "";
        public int ClueCount
        {
            get
            {
                int count = 0;
                foreach (var clue in clues) if (ReadBool(clue.variable)) count++;
                return count;
            }
        }

        private void ShowScreen(GameObject selected)
        {
            startScreen.SetActive(selected == startScreen);
            howToScreen.SetActive(selected == howToScreen);
            novelScreen.SetActive(selected == novelScreen);
            endScreen.SetActive(selected == endScreen);
        }

        public void ShowHowTo() => ShowScreen(howToScreen);
        public void BeginGame() => BeginAsync().Forget();
        public void PlayAgain() => BeginAsync().Forget();

        private async YarnTask BeginAsync()
        {
            if (starting) return;
            starting = true;
            try
            {
                popupDismissed = reviewDismissed = true;
                if (runner.IsDialogueRunning) await runner.Stop();
                runner.VariableStorage.Clear();
                presenter.ResetPresentation();
                popup.SetActive(false);
                notebook.SetActive(false);
                clueCountText.text = "CLUES  0 / 6";
                ShowScreen(novelScreen);
                await runner.StartDialogue("opening");
            }
            finally { starting = false; }
        }

        private void SetChapter(string title) => chapterText.text = title;

        private async YarnTask RevealClue(string key)
        {
            foreach (var clue in clues)
            {
                if (clue.variable != "$" + key) continue;
                clueCountText.text = "CLUES  " + ClueCount + " / 6";
                await ShowPopup("CLUE UNLOCKED", clue.description);
                return;
            }
            throw new ArgumentException("Unknown clue: " + key);
        }

        private YarnTask ShowFeedback(string emotion)
        {
            bool correct = ReadBool("$" + emotion + "Known");
            return ShowPopup(correct ? "EMOTION IDENTIFIED" : "INTERPRETATION RECORDED",
                correct ? emotion.ToUpperInvariant() : "Kesimpulanmu telah dicatat. Cerita berlanjut.");
        }

        private async YarnTask ShowPopup(string title, string body)
        {
            popupTitle.text = title;
            popupBody.text = body;
            popupDismissed = false;
            popup.SetActive(true);
            popupContinue.Select();
            while (!popupDismissed && !destroyCancellationToken.IsCancellationRequested) await YarnTask.Yield();
            if (this != null) popup.SetActive(false);
        }

        private void DismissPopup() => popupDismissed = true;

        private void RefreshNotebook()
        {
            foreach (var clue in clues)
                clue.label.text = ReadBool(clue.variable) ? clue.description : "?  Unknown clue";
        }

        public void OpenNotebook()
        {
            if (IsModalOpen || !novelScreen.activeSelf) return;
            priorSelection = EventSystem.current.currentSelectedGameObject;
            RefreshNotebook();
            notebookSubtitle.text = "Only the clues you discovered are shown.";
            notebookCloseLabel.text = "BACK TO CONVERSATION";
            notebook.SetActive(true);
            notebookClose.Select();
        }

        public void CloseNotebook()
        {
            notebook.SetActive(false);
            reviewDismissed = true;
            if (priorSelection != null && priorSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(priorSelection);
            priorSelection = null;
        }

        private async YarnTask ReviewClues()
        {
            RefreshNotebook();
            notebookSubtitle.text = "Review what you know before drawing your conclusion.";
            notebookCloseLabel.text = "CONTINUE TO FINAL DEDUCTION";
            reviewDismissed = false;
            notebook.SetActive(true);
            notebookClose.Select();
            while (!reviewDismissed && !destroyCancellationToken.IsCancellationRequested) await YarnTask.Yield();
        }

        private void ShowEnding(string result)
        {
            bool good = result == "good";
            endingKind.text = good ? "GOOD ENDING" : "BAD ENDING";
            endingTitle.text = good ? "SEEN" : "MISREAD";
            endingSupport.text = good ? "You understood what Rey couldn't say directly."
                : "You saw the reaction, but missed what was underneath it.";
            endingStats.text = "Clues found                       " + ClueCount + " / 6\n\n"
                + "Fear                                    " + (ReadBool("$fearKnown") ? "Identified" : "Missed") + "\n\n"
                + "Sadness                              " + (ReadBool("$sadnessKnown") ? "Identified" : "Missed") + "\n\n"
                + "Anger                                  " + (ReadBool("$angerKnown") ? "Identified" : "Missed") + "\n\n"
                + "Final emotion                     " + ReadString("$finalAnswer");
            ShowScreen(endScreen);
            replayButton.Select();
        }
    }
}
