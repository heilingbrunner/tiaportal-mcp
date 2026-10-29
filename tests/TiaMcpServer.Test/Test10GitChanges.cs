using System;
using System.IO;
using System.Linq;
using TiaMcpServer.Git;

namespace TiaMcpServer.Test
{
    /// <summary>
    /// Tests for the git layer behind ImportPlcProgramChanges: parsing 'git status --porcelain=v1 -z'
    /// and reading the changes of a real temporary repository. These tests do not connect to TIA
    /// Portal; the repository tests are inconclusive when git is not installed.
    /// </summary>
    [TestClass]
    [DoNotParallelize]
    public class Test10GitChanges
    {
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "repo-root");

        private static string Entry(string status, string path) => status + " " + path + "\0";

        private static string FullPath(string relative) => Path.GetFullPath(Path.Combine(Root, relative));

        [TestMethod]
        public void Test_1001_ParseStatus_MapsEachStatusToAKind()
        {
            // Arrange
            var output =
                Entry(" M", "a/modified.scl") +
                Entry("M ", "a/staged.scl") +
                Entry("A ", "a/added.db") +
                Entry("??", "a/new.udt") +
                Entry(" D", "a/gone.scl") +
                Entry("D ", "a/gone-staged.scl") +
                Entry("UU", "a/conflict.scl");

            // Act
            var changes = GitRepo.ParseStatus(output, Root);

            // Assert
            var kinds = changes.ToDictionary(c => Path.GetFileName(c.FullPath), c => c.Kind);
            Assert.AreEqual(7, changes.Count);
            Assert.AreEqual(GitChangeKind.Modified, kinds["modified.scl"]);
            Assert.AreEqual(GitChangeKind.Modified, kinds["staged.scl"]);
            Assert.AreEqual(GitChangeKind.Added, kinds["added.db"]);
            Assert.AreEqual(GitChangeKind.Untracked, kinds["new.udt"]);
            Assert.AreEqual(GitChangeKind.Deleted, kinds["gone.scl"]);
            Assert.AreEqual(GitChangeKind.Deleted, kinds["gone-staged.scl"]);
            Assert.AreEqual(GitChangeKind.Unmerged, kinds["conflict.scl"]);
        }

        [TestMethod]
        public void Test_1002_ParseStatus_ResolvesPathsAgainstTheRepositoryRoot()
        {
            // Act
            var changes = GitRepo.ParseStatus(Entry(" M", "PLC_1/Program blocks/Main.scl"), Root);

            // Assert
            Assert.AreEqual(FullPath("PLC_1/Program blocks/Main.scl"), changes.Single().FullPath);
        }

        [TestMethod]
        public void Test_1003_ParseStatus_RenameKeepsTheNewPathAndSkipsTheOldOne()
        {
            // Arrange: a rename is written as 'R  new\0old\0'
            var output = "R  b/new.scl\0b/old.scl\0" + Entry(" M", "b/other.scl");

            // Act
            var changes = GitRepo.ParseStatus(output, Root);

            // Assert
            Assert.AreEqual(2, changes.Count, "the old path of a rename is not an entry of its own");
            Assert.AreEqual(GitChangeKind.Renamed, changes[0].Kind);
            Assert.AreEqual(FullPath("b/new.scl"), changes[0].FullPath);
            Assert.AreEqual(FullPath("b/other.scl"), changes[1].FullPath);
        }

        [TestMethod]
        public void Test_1004_ParseStatus_KeepsSpacesAndUmlautsUnquoted()
        {
            // Act
            var changes = GitRepo.ParseStatus(Entry(" M", "Program blocks/Prüfung 1/FB_Größe.scl"), Root);

            // Assert
            Assert.AreEqual(FullPath("Program blocks/Prüfung 1/FB_Größe.scl"), changes.Single().FullPath);
        }

        [TestMethod]
        public void Test_1005_ParseStatus_EmptyOutputIsNoChange()
        {
            // Act and Assert
            Assert.AreEqual(0, GitRepo.ParseStatus(string.Empty, Root).Count);
        }

        [TestMethod]
        public void Test_1006_Below_KeepsOnlyFilesInsideTheDirectory()
        {
            // Arrange
            var changes = GitRepo.ParseStatus(
                Entry(" M", "PLC_1/Main.scl") + Entry(" M", "PLC_10/Main.scl") + Entry(" M", "README.md"), Root);

            // Act
            var below = GitRepo.Below(changes, FullPath("PLC_1"));

            // Assert
            Assert.AreEqual(1, below.Count, "PLC_10 is a sibling, not a child, of PLC_1");
            Assert.AreEqual(FullPath("PLC_1/Main.scl"), below[0].FullPath);
        }

        [TestMethod]
        public void Test_1007_GetChanges_ReportsEditsOfARealRepository()
        {
            var directory = NewDirectory();

            try
            {
                // Arrange
                InitRepository(directory);
                var tracked = Path.Combine(directory, "tracked.scl");
                var untouched = Path.Combine(directory, "untouched.scl");
                File.WriteAllText(tracked, "one");
                File.WriteAllText(untouched, "one");
                Commit(directory);

                File.WriteAllText(tracked, "two");
                File.WriteAllText(Path.Combine(directory, "new file.udt"), "new");

                // Act
                var changes = GitRepo.GetChanges(directory);

                // Assert
                var names = changes.Select(c => Path.GetFileName(c.FullPath)).OrderBy(n => n).ToList();
                CollectionAssert.AreEqual(new[] { "new file.udt", "tracked.scl" }, names);
                Assert.AreEqual(GitChangeKind.Untracked, changes.Single(c => c.FullPath.EndsWith("new file.udt")).Kind);
                Assert.IsTrue(changes.All(c => File.Exists(c.FullPath)), "full paths point at the files on disk");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void Test_1008_GetChanges_ScopesToTheGivenSubfolder()
        {
            var directory = NewDirectory();

            try
            {
                // Arrange
                InitRepository(directory);
                var inner = Directory.CreateDirectory(Path.Combine(directory, "PLC_1")).FullName;
                File.WriteAllText(Path.Combine(inner, "a.scl"), "one");
                File.WriteAllText(Path.Combine(directory, "outside.md"), "one");
                Commit(directory);

                File.WriteAllText(Path.Combine(inner, "a.scl"), "two");
                File.WriteAllText(Path.Combine(directory, "outside.md"), "two");

                // Act
                var changes = GitRepo.GetChanges(inner);

                // Assert
                Assert.AreEqual(1, changes.Count, "files outside the folder are not reported");
                Assert.AreEqual("a.scl", Path.GetFileName(changes[0].FullPath));
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void Test_1009_GetChanges_CleanTreeHasNoChanges()
        {
            var directory = NewDirectory();

            try
            {
                // Arrange
                InitRepository(directory);
                File.WriteAllText(Path.Combine(directory, "a.scl"), "one");
                Commit(directory);

                // Act and Assert
                Assert.AreEqual(0, GitRepo.GetChanges(directory).Count);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void Test_1010_GetChanges_FolderOutsideAnyRepositoryThrowsAClearError()
        {
            var directory = NewDirectory();

            try
            {
                // Arrange: a temp folder is not below a repository on a normal machine
                RequireGit(directory);

                if (GitRepo.TryGetRepositoryRoot(directory) != null)
                {
                    Assert.Inconclusive("The temp folder lies inside a git repository on this machine.");
                }

                // Act
                var ex = Assert.ThrowsException<GitException>(() => GitRepo.GetChanges(directory));

                // Assert
                StringAssert.Contains(ex.Message, "not inside a git repository");
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void Test_1011_GetChanges_MissingFolderThrows()
        {
            // Act and Assert
            Assert.ThrowsException<GitException>(() =>
                GitRepo.GetChanges(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        }

        [TestMethod]
        public void Test_1012_GetChangeSet_SeparatesFilesOutsideTheFolder()
        {
            var directory = NewDirectory();

            try
            {
                // Arrange
                InitRepository(directory);
                var inner = Directory.CreateDirectory(Path.Combine(directory, "PLC_1")).FullName;
                File.WriteAllText(Path.Combine(inner, "a.scl"), "one");
                File.WriteAllText(Path.Combine(directory, "outside.udt"), "one");
                Commit(directory);

                File.WriteAllText(Path.Combine(inner, "a.scl"), "two");
                File.WriteAllText(Path.Combine(directory, "outside.udt"), "two");

                // Act
                var set = GitRepo.GetChangeSet(inner);

                // Assert
                Assert.AreEqual("a.scl", Path.GetFileName(set.Inside.Single().FullPath));
                Assert.AreEqual("outside.udt", Path.GetFileName(set.Outside.Single().FullPath));
                Assert.AreEqual(Path.GetFullPath(directory).TrimEnd('\\'), set.RepositoryRoot.TrimEnd('\\'), ignoreCase: true);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        [TestMethod]
        public void Test_1013_RunCommand_StopsAProcessThatExceedsTheTimeout()
        {
            // Arrange: ping runs about 20 seconds
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var ex = Assert.ThrowsException<GitException>(() =>
                GitRepo.RunCommand("ping", Path.GetTempPath(), new[] { "-n", "20", "127.0.0.1" }, timeoutMilliseconds: 500));

            // Assert
            stopwatch.Stop();
            StringAssert.Contains(ex.Message, "did not finish");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, "the call returns at the timeout, not when the command ends");
        }

        [TestMethod]
        public void Test_1014_RunCommand_GivesTheProcessNoInput()
        {
            // Arrange: findstr without a file reads stdin; with an open stdin it would wait for input forever
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var ex = Assert.ThrowsException<GitException>(() =>
                GitRepo.RunCommand("findstr", Path.GetTempPath(), new[] { "needle" }, timeoutMilliseconds: 20000));

            // Assert
            stopwatch.Stop();
            Assert.AreEqual(1, ex.ExitCode, "end of input means no match, which findstr reports with exit code 1");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, "stdin is closed, so the command ends at once");
        }

        private static string NewDirectory()
        {
            return Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "tiamcp-git-" + Guid.NewGuid().ToString("N"))).FullName;
        }

        private static void RequireGit(string directory)
        {
            try
            {
                GitRepo.Run(directory, "--version");
            }
            catch (GitException ex)
            {
                Assert.Inconclusive("git is not available: " + ex.Message);
            }
        }

        private static void InitRepository(string directory)
        {
            RequireGit(directory);
            GitRepo.Run(directory, "init");
        }

        private static void Commit(string directory)
        {
            GitRepo.Run(directory, "add", "-A");
            GitRepo.Run(directory, "-c", "user.name=test", "-c", "user.email=test@example.com", "commit", "-m", "baseline");
        }

        private static void DeleteDirectory(string directory)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }

                Directory.Delete(directory, recursive: true);
            }
            catch (Exception)
            {
                // A temp folder that stays behind is harmless.
            }
        }
    }
}
