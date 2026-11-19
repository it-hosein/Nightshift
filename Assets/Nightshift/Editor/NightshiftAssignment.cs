using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Nightshift.EditorTools
{
    public sealed class NightshiftAssignment : EditorWindow
    {
        enum Platform { Windows64, Linux64 }

        [SerializeField] string firstName = "", lastName = "", studentId = "", repository = "";
        [SerializeField] Platform platform;

        [MenuItem("Nightshift/Build Assignment ZIP…")]
        static void Open()
        {
            var window = GetWindow<NightshiftAssignment>(true, "Nightshift Assignment");
            window.minSize = new Vector2(490, 230);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Build the Unity player and package the coursework.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            firstName = EditorGUILayout.TextField("First name (Latin)", firstName);
            lastName = EditorGUILayout.TextField("Last name (Latin)", lastName);
            studentId = EditorGUILayout.TextField("Student ID", studentId);
            repository = EditorGUILayout.TextField("GitHub repository URL", repository);
            platform = (Platform)EditorGUILayout.EnumPopup("Build platform", platform);

            EditorGUILayout.Space();
            var playing = EditorApplication.isPlayingOrWillChangePlaymode;
            if (playing) EditorGUILayout.HelpBox("Exit Play mode before building.", MessageType.Info);
            using (new EditorGUI.DisabledScope(playing || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Build and save ZIP")) Export();
            }
        }

        void Export()
        {
            try
            {
                string name = AssignmentArchive.Filename(firstName, lastName, studentId);
                string url = AssignmentArchive.Repository(repository);
                var project = Directory.GetParent(Application.dataPath).FullName;
                var report = Path.Combine(project, "Documentation", "Report.pdf");
                if (!File.Exists(report)) throw new FileNotFoundException("The coursework report PDF is missing.", report);

                string folder = EditorUtility.OpenFolderPanel("Select the folder for " + name, project, "");
                if (string.IsNullOrEmpty(folder)) return;
                string destination = Path.Combine(folder, name);
                if (File.Exists(destination))
                    throw new IOException("Choose another folder or move the existing ZIP.");

                string stage = Path.Combine(project, "Builds", "AssignmentWork", Guid.NewGuid().ToString("N"));
                string buildFolder = Path.Combine(stage, "Build");
                bool windows = platform == Platform.Windows64;
                string executable = windows ? "Nightshift.exe" : "Nightshift.x86_64";
                try
                {
                    NightshiftBuild.Build(windows ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneLinux64,
                        Path.Combine(buildFolder, executable));
                    EditorUtility.DisplayProgressBar("Nightshift", "Writing and checking the submission ZIP", .8f);
                    AssignmentArchive.Write(destination, buildFolder, executable, report, url);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    try
                    {
                        if (Directory.Exists(stage)) Directory.Delete(stage, true);
                    }
                    catch (IOException error) { Debug.LogWarning(error.Message); }
                    catch (UnauthorizedAccessException error) { Debug.LogWarning(error.Message); }
                }
                EditorUtility.RevealInFinder(destination);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorUtility.DisplayDialog("Could not build the assignment ZIP", error.Message, "OK");
            }
        }
    }
}
