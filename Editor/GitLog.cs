using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Abuksigun.PackageShortcuts
{
    public static class GitLog
    {
        const int BottomPanelHeight = 200;
        const int FileListHeight = 150;
        
        static readonly List<string> emptyList = new();

        [MenuItem("Assets/Git Log", true)]
        public static bool Check() => PackageShortcuts.GetSelectedGitModules().Any();

        [MenuItem("Assets/Git Log", priority = 100)]
        public static async void Invoke()
        {
            var filesScrollPosition = Vector2.zero;
            var scrollPosition = Vector2.zero;
            Task<CommandResult> logTask = null;
            Task<string> currentBranchTask = null;
            var selectionPerModule = new Dictionary<string, List<string>>();
            int tab = 0;
            string selectedCommit = null;

            await GUIShortcuts.ShowModalWindow("Git Log", new Vector2Int(500, 650), (window) => {
                var modules = PackageShortcuts.GetSelectedGitModules();
                if (!modules.Any())
                    return;
                tab = modules.Count() > 1 ? GUILayout.Toolbar(tab, modules.Select(x => x.Name).ToArray()) : 0;

                var module = modules.Skip(tab).First();

                if (logTask == null || currentBranchTask != module.CurrentBranch)
                {
                    currentBranchTask = module.CurrentBranch;
                    logTask = module.RunGitReadonly($"log --graph --abbrev-commit --decorate --format=format:\"%C(bold blue)%h%C(reset) - %C(bold green)(%ar)%C(reset) %C(white)%s%C(reset) %C(dim white)- %an%C(reset)%C(auto)%d%C(reset)\" --all");
                }

                var selection = selectionPerModule.GetValueOrDefault(module.Guid, emptyList);
                var windowWidth = GUILayout.Width(window.position.width);
                using (var scroll = new GUILayout.ScrollViewScope(scrollPosition, windowWidth, GUILayout.Height(window.position.height - BottomPanelHeight)))
                {
                    if (logTask.GetResultOrDefault() is { }  log)
                    {
                        foreach (var commit in log.Output.Trim().Split('\n'))
                        {
                            string commitHash = Regex.Match(commit, @"([0-9a-f]{7}) - ")?.Groups[1].Value;
                            var style = selectedCommit == commitHash ? GUIShortcuts.SelectedStyle : GUIShortcuts.IdleStyle;
                            if (GUILayout.Toggle(selectedCommit == commitHash, commit, style) && !string.IsNullOrEmpty(commitHash))
                            {
                                if (commitHash != selectedCommit)
                                    selection.Clear();
                                selectedCommit = commitHash;
                            }
                        }
                    }
                    scrollPosition = scroll.scrollPosition;
                }
                
                if (!string.IsNullOrEmpty(selectedCommit))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button($"Checkout {selectedCommit}"))
                        {
                            if (EditorUtility.DisplayDialog("Are you sure you want CHECKOUT to COMMIT", selectedCommit, "Yes", "No"))
                                _ = module.RunGit($"checkout {selectedCommit}");
                        }
                        if (GUILayout.Button($"Reset soft {selectedCommit}"))
                        {
                            if (EditorUtility.DisplayDialog("Are you sure you want RESET to COMMIT", selectedCommit, "Yes", "No"))
                                _ = module.RunGit($"reset --soft {selectedCommit}");
                        }
                        if (GUILayout.Button($"Diff {selectedCommit}"))
                        {
                            _ = Diff.ShowDiff(module, selection, false, $"{selectedCommit}~1", selectedCommit);
                        }
                    }
                }
                if (module.GitRepoPath.GetResultOrDefault() is { } gitRepoPath && module.DiffFiles($"{selectedCommit}~1", selectedCommit).GetResultOrDefault() is { } diffFiles)
                    GUIShortcuts.DrawList(gitRepoPath, diffFiles, selection, ref filesScrollPosition, true, windowWidth, GUILayout.Height(FileListHeight));
            });
        }
    }
}