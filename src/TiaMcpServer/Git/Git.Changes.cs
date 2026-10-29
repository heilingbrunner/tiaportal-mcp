using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TiaMcpServer.Git
{
    /// <summary>What git says happened to one file since the last commit.</summary>
    public enum GitChangeKind
    {
        Modified,
        Added,
        Untracked,
        Renamed,

        /// <summary>The file is gone from the working tree; there is nothing to read.</summary>
        Deleted,

        /// <summary>A merge conflict is open; the file may hold conflict markers.</summary>
        Unmerged
    }

    /// <summary>One changed file: its full path on disk and what changed.</summary>
    public sealed class GitChange
    {
        public GitChange(string fullPath, GitChangeKind kind)
        {
            FullPath = fullPath;
            Kind = kind;
        }

        public string FullPath { get; }

        public GitChangeKind Kind { get; }
    }

    /// <summary>The changes of a repository, split by whether they lie inside the folder that was asked about.</summary>
    public sealed class GitChangeSet
    {
        public GitChangeSet(string repositoryRoot, IReadOnlyList<GitChange> inside, IReadOnlyList<GitChange> outside)
        {
            RepositoryRoot = repositoryRoot;
            Inside = inside;
            Outside = outside;
        }

        public string RepositoryRoot { get; }

        /// <summary>Changed files below the folder.</summary>
        public IReadOnlyList<GitChange> Inside { get; }

        /// <summary>Changed files elsewhere in the repository; reported so they do not vanish silently.</summary>
        public IReadOnlyList<GitChange> Outside { get; }
    }

    public static partial class GitRepo
    {
        // The files that changed since the last commit below one folder: modified, staged and
        // untracked ones. ImportPlcProgramChanges uses it to import only what was edited
        // after the export, instead of the whole exported tree.
        //
        // Callers: Portal.ImportPlcProgramChanges in Siemens/Portal.Plc.cs. Affected API: none
        // existing. File I/O: none of its own; git reads the repository.
        //
        // 'git status --porcelain=v1 -z' is the format meant for programs: paths are never
        // quoted, so umlauts and spaces survive, and they are always relative to the repository
        // root whatever the working directory is. '-uall' lists untracked files one by one
        // instead of collapsing a new folder to its name.

        /// <summary>
        /// The changed files below <paramref name="directory"/>. A folder in a repository without
        /// changes returns an empty list; a folder outside any repository throws.
        /// </summary>
        public static IReadOnlyList<GitChange> GetChanges(string directory)
        {
            var root = TryGetRepositoryRoot(directory)
                ?? throw new GitException(
                    $"'{directory}' is not inside a git repository. Run 'git init' there and commit the exported files as a baseline before looking for changes.");

            var output = Run(directory, "status", "--porcelain=v1", "-z", "-uall", "--", ".");

            return Below(ParseStatus(output, root), directory);
        }

        /// <summary>
        /// Like <see cref="GetChanges"/>, but also returns the changes elsewhere in the repository,
        /// so a caller can say that a file was seen and left out instead of dropping it silently.
        /// Looks at the whole repository, which is the price for that.
        /// </summary>
        public static GitChangeSet GetChangeSet(string directory)
        {
            var root = TryGetRepositoryRoot(directory)
                ?? throw new GitException(
                    $"'{directory}' is not inside a git repository. Run 'git init' there and commit the exported files as a baseline before looking for changes.");

            var all = ParseStatus(Run(directory, "status", "--porcelain=v1", "-z", "-uall"), root);
            var inside = Below(all, directory);

            return new GitChangeSet(root, inside, all.Except(inside).ToList());
        }

        /// <summary>The root folder of the repository that contains <paramref name="directory"/>, or null when there is none.</summary>
        public static string? TryGetRepositoryRoot(string directory)
        {
            try
            {
                var output = Run(directory, "rev-parse", "--show-toplevel").TrimEnd('\r', '\n');

                return Path.GetFullPath(output);
            }
            catch (GitException ex) when (IsNotARepository(ex))
            {
                return null;
            }
        }

        /// <summary>
        /// Parses the output of 'git status --porcelain=v1 -z'. An entry is 'XY path' ended by a
        /// NUL; a rename or copy is followed by one more NUL-ended entry with the old path, which
        /// is skipped because only the new file exists.
        /// </summary>
        public static IReadOnlyList<GitChange> ParseStatus(string porcelainOutput, string repositoryRoot)
        {
            var changes = new List<GitChange>();
            var entries = porcelainOutput.Split('\0');

            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];

                // 'XY ' plus at least one character of path; the output ends with an empty piece.
                if (entry.Length < 4)
                {
                    continue;
                }

                var index = entry[0];
                var worktree = entry[1];

                if (IsRenameOrCopy(index) || IsRenameOrCopy(worktree))
                {
                    i++;
                }

                changes.Add(new GitChange(
                    Path.GetFullPath(Path.Combine(repositoryRoot, entry.Substring(3))),
                    Classify(index, worktree)));
            }

            return changes;
        }

        /// <summary>The changes whose file lies inside <paramref name="directory"/> (not merely next to it).</summary>
        public static IReadOnlyList<GitChange> Below(IEnumerable<GitChange> changes, string directory)
        {
            var prefix = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            return changes
                .Where(c => c.FullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static bool IsRenameOrCopy(char status)
        {
            return status == 'R' || status == 'C';
        }

        private static GitChangeKind Classify(char index, char worktree)
        {
            if (index == '?' && worktree == '?')
            {
                return GitChangeKind.Untracked;
            }

            if (index == 'U' || worktree == 'U' || (index == 'A' && worktree == 'A') || (index == 'D' && worktree == 'D'))
            {
                return GitChangeKind.Unmerged;
            }

            if (index == 'D' || worktree == 'D')
            {
                return GitChangeKind.Deleted;
            }

            if (index == 'R' || worktree == 'R')
            {
                return GitChangeKind.Renamed;
            }

            if (index == 'A' || index == 'C')
            {
                return GitChangeKind.Added;
            }

            return GitChangeKind.Modified;
        }
    }
}
