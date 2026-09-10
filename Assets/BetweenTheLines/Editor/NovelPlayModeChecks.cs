using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emotionalaw.Editor
{
    // Drives existing, serialized buttons. No test UI or GameObjects are created.
    [InitializeOnLoad]
    public static class NovelPlayModeChecks
    {
        private const string ActiveKey = "BTL.PlayModeAcceptance";
        private static NovelFlow flow;
        private static NovelPresenter presenter;
        private static int scenario;
        private static bool began;
        private static bool notebookChecked;
        private static double nextTick;
        private static double deadline;
        private static List<string> results;
        private static HashSet<string> visited;

        static NovelPlayModeChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Tools/Between the Lines/Run Play Mode Acceptance Tests")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            NovelProjectTools.ValidateReferences();
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(ActiveKey, false))
            {
                flow = UnityEngine.Object.FindFirstObjectByType<NovelFlow>();
                presenter = UnityEngine.Object.FindFirstObjectByType<NovelPresenter>();
                scenario = 0;
                began = notebookChecked = false;
                results = new List<string>();
                visited = new HashSet<string>();
                flow.Runner.onNodeStart.AddListener(OnNode);
                nextTick = EditorApplication.timeSinceStartup + .5;
                deadline = nextTick + 180;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                if (flow != null) flow.Runner.onNodeStart.RemoveListener(OnNode);
                SessionState.SetBool(ActiveKey, false);
            }
        }

        private static void OnNode(string name) => visited.Add(name);
        private static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
                Finish("FAIL: runtime error: " + message);
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextTick) return;
            nextTick = EditorApplication.timeSinceStartup + .025;
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout in scenario " + scenario);
                if (!began)
                {
                    if (flow.ClueCount != 0 && scenario == 0) throw new Exception("Initial progress is not empty");
                    began = true;
                    if (scenario == 0) flow.BeginGame(); else flow.PlayAgain();
                    return;
                }
                if (flow.IsAtEnding)
                {
                    int count = flow.ClueCount;
                    bool good = flow.ReadBool("$fearKnown") && flow.ReadBool("$sadnessKnown") && flow.ReadBool("$angerKnown")
                        && flow.ReadBool("$finalCorrect") && count == 6;
                    if (count != NovelProjectTools.ExpectedCounts[scenario] || good != (scenario == 0))
                        throw new Exception("Unexpected ending state");
                    string expectedNode = scenario == 0 ? "goodEndingScript" : scenario == 5 ? "badEndingJealous" : "badEndingIncomplete";
                    if (!visited.Contains(expectedNode)) throw new Exception("Wrong ending script");
                    if (scenario == 1 && visited.Contains("fear2")) throw new Exception("F2 was not skipped");
                    results.Add("PASS: " + NovelProjectTools.CaseNames[scenario] + " via scene buttons; " + count + "/6 clues");
                    scenario++;
                    if (scenario == NovelProjectTools.CaseNames.Length) { Finish(null); return; }
                    began = false;
                    visited.Clear();
                    return;
                }
                if (flow.PopupOpen) { flow.PopupContinue.onClick.Invoke(); return; }
                if (flow.NotebookOpen) { flow.NotebookClose.onClick.Invoke(); return; }
                if (presenter.IsPresentingLine)
                {
                    if (!notebookChecked)
                    {
                        int before = flow.ClueCount;
                        flow.OpenNotebook();
                        if (!flow.NotebookOpen || flow.ClueCount != before) throw new Exception("Notebook state failed");
                        notebookChecked = true;
                        results.Add("PASS: notebook opens during dialogue without changing progress");
                        return;
                    }
                    presenter.ContinueButton.onClick.Invoke();
                }
                else if (presenter.IsChoosing)
                {
                    string node = flow.Runner.Dialogue.CurrentNode;
                    presenter.ChoiceButtons[NovelProjectTools.Select(node, scenario)].onClick.Invoke();
                }
            }
            catch (Exception ex) { Finish("FAIL: " + ex.Message); }
        }

        private static void Finish(string failure)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            if (failure != null) results.Add(failure);
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllLines("Docs/Validation/PlayModeAcceptance.txt", results);
            SessionState.SetBool(ActiveKey, false);
            Debug.Log("BTL Play Mode: " + (failure ?? "7/7 scenarios PASS; replay between runs and notebook checked.") + "\n" + string.Join("\n", results));
            EditorApplication.isPlaying = false;
        }
    }
}
