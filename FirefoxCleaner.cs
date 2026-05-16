using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace TempFileCleaner
{
    #region [Testing]
    public static class Test
    {
        public static async Task Run(bool dryRun = true)
        {
            var engine = new CleanupEngine();
            var result = await engine.SweepAsync(Targets.All, dryRun);
            Debug.WriteLine($"[INFO] Total deleted: {result.TotalDeletedFiles}, Total failed: {result.TotalFailedFiles}");
            Debug.WriteLine($"[INFO] Folder Results:");
            foreach (var folder in result.FolderResults)
                Debug.WriteLine($" - {folder}");
        }
    }
    #endregion

    #region [Models]
    public enum CleanupPolicy
    {
        SafeToDelete,
        Conditional,
        DoNotTouch
    }

    public class CleanupTarget
    {
        public string Name { get; set; }
        public string[] Paths { get; set; }
        public CleanupPolicy Policy { get; set; }
    }

    public class CleanupResult
    {
        public List<FolderCleanupResult> FolderResults { get; } = new();

        public int TotalDeletedFiles => FolderResults.Sum(f => f.DeletedFiles);
        public int TotalFailedFiles => FolderResults.Sum(f => f.FailedFiles);
        public int TotalSkippedFolders => FolderResults.Count(f => f.Skipped);
    }

    public class FolderCleanupResult
    {
        public string Folder { get; set; }
        public int DeletedFiles { get; set; }
        public int FailedFiles { get; set; }
        public bool Skipped { get; set; }
        public override string ToString() => $"{Folder} | Deleted: {DeletedFiles}, Failed: {FailedFiles}, Skipped: {Skipped}";
    }
    #endregion

    #region [Cleaner Methods]
    public static class Targets
    {
        public static List<CleanupTarget> All = new()
        {
            // TODO: Add existing Windows 11 temp locations here.

            // Firefox Classic
            new CleanupTarget
            {
                Name = "Firefox Cache (Classic Version)",
                Paths = new[]
                {
                    @"%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2",
                    @"%LOCALAPPDATA%\Mozilla\Firefox\Profiles\*\cache2\entries"
                },
                Policy = CleanupPolicy.SafeToDelete
            },

            // Firefox Store Version (UWP/WinUI)
            new CleanupTarget
            {
                Name = "Firefox Cache (AppStore Version)",
                Paths = new[]
                {
                    @"%LOCALAPPDATA%\Packages\Mozilla.Firefox_*\LocalCache\Local\Mozilla\Firefox\Profiles\*\cache2",
                    @"%LOCALAPPDATA%\Packages\Mozilla.Firefox_*\LocalCache\Local\Mozilla\Firefox\Profiles\*\cache2\entries"
                },
                Policy = CleanupPolicy.SafeToDelete
            }
        };
    }

    public static class PathExpander
    {
        public static IEnumerable<string> Expand(string pattern)
        {
            var expanded = Environment.ExpandEnvironmentVariables(pattern);

            // If no wildcard, return directly
            if (!expanded.Contains("*"))
                return Directory.Exists(expanded) ? new[] { expanded } : Enumerable.Empty<string>();

            // Split into root + wildcard segment
            var root = expanded.Substring(0, expanded.IndexOf('*')).TrimEnd('\\');
            if (!Directory.Exists(root))
                return Enumerable.Empty<string>();

            var wildcardPart = expanded.Substring(root.Length).TrimStart('\\');

            return Directory.GetDirectories(root, wildcardPart.Split('\\')[0])
                            .Select(d => Path.Combine(d, wildcardPart.Substring(wildcardPart.IndexOf('\\') + 1)))
                            .Where(Directory.Exists);
        }
    }

    public class CleanupEngine
    {
        public async Task<CleanupResult> SweepAsync(IEnumerable<CleanupTarget> targets, bool dryRun = false)
        {
            var result = new CleanupResult();

            foreach (var target in targets)
            {
                foreach (var pattern in target.Paths)
                {
                    var expanded = PathExpander.Expand(pattern);
                    foreach (var folder in expanded)
                    {
                        var folderResult = await CleanFolderAsync(folder, target.Policy, dryRun);
                        result.FolderResults.Add(folderResult);
                    }
                }
            }

            return result;
        }

        private async Task<FolderCleanupResult> CleanFolderAsync(string folder, CleanupPolicy policy, bool dryRun)
        {
            var result = new FolderCleanupResult { Folder = folder };

            if (policy == CleanupPolicy.DoNotTouch)
            {
                result.Skipped = true;
                return result;
            }

            if (!Directory.Exists(folder))
                return result;

            var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    if (!dryRun)
                        File.Delete(file);

                    result.DeletedFiles++;
                }
                catch
                {
                    result.FailedFiles++;
                }
            }

            return result;
        }
    }
    #endregion
}
