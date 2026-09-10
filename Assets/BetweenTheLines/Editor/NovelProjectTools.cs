using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;

namespace Emotionalaw.Editor
{
    public static class NovelProjectTools
    {
        private const string ScenePath = "Assets/Scenes/BetweenTheLines.unity";
        private const string StoryPath = "Assets/BetweenTheLines/Story/BetweenTheLines.yarnproject";

        // Repairs serialized import references only. Never creates UI or overwrites layout.
        [MenuItem("Tools/Between the Lines/Connect Imported References")]
        public static void ConnectReferences()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open BetweenTheLines.unity first.");
            var project = AssetDatabase.LoadAssetAtPath<YarnProject>(StoryPath);
            if (project == null || project.Program == null) throw new InvalidOperationException("Yarn Project did not compile.");
            var runner = UnityEngine.Object.FindFirstObjectByType<DialogueRunner>();
            Undo.RecordObject(runner, "Connect imported Yarn project");
            runner.SetProject(project);
            EditorUtility.SetDirty(runner);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            int repairedFonts = 0;
            foreach (var label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (label.font != null) continue;
                Undo.RecordObject(label, "Connect built-in font");
                label.font = font;
                EditorUtility.SetDirty(label);
                repairedFonts++;
            }
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
            EditorSceneManager.SaveScene(runner.gameObject.scene);
            Debug.Log($"BTL: Connected Yarn project ({project.NodeNames.Length} nodes); repaired {repairedFonts} fonts. Scene saved. No UI was generated.");
            ValidateReferences();
        }

        [MenuItem("Tools/Between the Lines/Validate Scene References")]
        public static void ValidateReferences()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open BetweenTheLines.unity first.");
            int objects = 0;
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                objects++;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                    throw new InvalidOperationException("Missing script on " + transform.name);
                foreach (var component in transform.GetComponents<MonoBehaviour>())
                {
                    if (!(component is NovelFlow) && !(component is NovelPresenter) && !(component is CharacterPortrait)) continue;
                    var so = new SerializedObject(component);
                    var property = so.GetIterator();
                    while (property.NextVisible(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null)
                            throw new InvalidOperationException("Unassigned reference: " + component.GetType().Name + "." + property.propertyPath);
                }
            }
            foreach (var label in UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (label.font == null) throw new InvalidOperationException("Missing font: " + label.name);
            var runner = UnityEngine.Object.FindFirstObjectByType<DialogueRunner>();
            if (runner.YarnProject == null || runner.YarnProject.Program == null) throw new InvalidOperationException("Missing compiled Yarn project.");
            Debug.Log($"BTL: Scene references PASS. {objects} authored GameObjects; all gameplay references and fonts assigned.");
        }

        internal static int Select(string node, int scenario)
        {
            if (scenario == 1 && node == "fear1") return 1;
            if (scenario == 2 && node == "sadness2") return 1;
            if (scenario == 3 && node == "angerIdentification") return 1;
            if (scenario == 4 && node == "anger2") return 1;
            if (scenario == 5 && node == "finalDeduction") return 0;
            if (scenario == 6 && new[] { "fear1", "sadness1", "anger1" }.Contains(node)) return 1;
            if (node == "fearIdentification") return 1;
            if (node == "sadnessIdentification" || node == "finalDeduction") return 2;
            return 0;
        }

        internal static readonly string[] CaseNames = {
            "Perfect run", "Fail first Fear / skip F2", "Pass S1 / fail S2", "All clues / wrong Anger",
            "Five clues / correct final", "Wrong final", "All first clues missed"
        };
        internal static readonly int[] ExpectedCounts = { 6, 4, 5, 6, 5, 6, 0 };

        [MenuItem("Tools/Between the Lines/Run Story Acceptance Tests")]
        public static void TestStory()
        {
            var project = AssetDatabase.LoadAssetAtPath<YarnProject>(StoryPath);
            if (project == null || project.Program == null) throw new InvalidOperationException("Yarn must compile first.");
            var results = new List<string>();
            for (int scenario = 0; scenario < CaseNames.Length; scenario++)
            {
                var store = new Yarn.MemoryVariableStore();
                var dialogue = new Yarn.Dialogue(store);
                {
                    dialogue.SetProgram(project.Program);
                    string node = "";
                    string ending = "";
                    var visited = new HashSet<string>();
                    dialogue.NodeStartHandler = name => { node = name; visited.Add(name); };
                    dialogue.LineHandler = line => { };
                    dialogue.CommandHandler = command => {
                        if (command.Text.StartsWith("ending ")) ending = command.Text.Contains("good") ? "good" : "bad";
                    };
                    dialogue.OptionsHandler = options => {
                        if (options.Options.Length != 3) throw new InvalidOperationException("Option count at " + node);
                        dialogue.SetSelectedOption(options.Options[Select(node, scenario)].ID);
                    };
                    dialogue.SetNode("opening");
                    int steps = 0;
                    do { dialogue.Continue(); if (++steps > 1000) throw new InvalidOperationException("Story loop"); }
                    while (dialogue.IsActive);
                    int count = 0;
                    foreach (string key in new[] { "$fear1", "$fear2", "$sadness1", "$sadness2", "$anger1", "$anger2" })
                        if (store.TryGetValue(key, out bool value) && value) count++;
                    if (count != ExpectedCounts[scenario] || ending != (scenario == 0 ? "good" : "bad"))
                        throw new InvalidOperationException(CaseNames[scenario] + $": unexpected {ending}, {count} clues");
                    if (scenario == 1 && visited.Contains("fear2")) throw new InvalidOperationException("F2 gating failed");
                    if (scenario == 6 && new[] { "fear2", "sadness2", "anger2" }.Any(visited.Contains))
                        throw new InvalidOperationException("First clue gating failed");
                    results.Add("PASS: " + CaseNames[scenario] + $" ({count}/6, {ending})");
                }
            }
            Directory.CreateDirectory("Docs/Validation");
            File.WriteAllLines("Docs/Validation/StoryAcceptance.txt", results);
            Debug.Log("BTL: 7/7 Yarn VM acceptance tests PASS.\n" + string.Join("\n", results));
        }
    }
}
