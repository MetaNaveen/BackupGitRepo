﻿using LibGit2Sharp;
using System.Reflection;
using System.Text;

namespace BackupGitRepo;

class Program {
   static string sRepositoryDir = "", sBackupDir = "", sGitStatusFilePath = "";
   static bool sSkipUntracked = false, sSkipStaged = false, sSkipAdded = false, sSkipDeleted = false, sSkipModified = false;

   static void Main (string[] args) {
      if (args.Length == 0) {
         var appName = Assembly.GetExecutingAssembly ().GetName ().Name;
         if (string.IsNullOrEmpty (appName)) appName = "BackupGitRepo";
         appName += ".exe";
         Console.WriteLine ($"{appName} <RepoDirectoryPath> [<BackupDirectoryPath>]");
         Environment.Exit (-1);
      }

      // -su - skipUntracked
      // -ss - skipStaged
      // -sa - skipAdded
      // -sd - skipDeleted
      // -sm - skipModified
      // BackupGitRepo -su -ss -sd <> <>
      if(args[0].StartsWith("-su", StringComparison.OrdinalIgnoreCase)) {
         args = args.Skip (1).ToArray ();
         sSkipUntracked = true;
      }
      if (args[0].StartsWith ("-ss", StringComparison.OrdinalIgnoreCase)) {
         args = args.Skip (1).ToArray ();
         sSkipStaged = true;
      }
      if (args[0].StartsWith ("-sa", StringComparison.OrdinalIgnoreCase)) {
         args = args.Skip (1).ToArray ();
         sSkipAdded = true;
      }
      if (args[0].StartsWith ("-sd", StringComparison.OrdinalIgnoreCase)) {
         args = args.Skip (1).ToArray ();
         sSkipDeleted = true;
      }
      if (args[0].StartsWith ("-sm", StringComparison.OrdinalIgnoreCase)) {
         args = args.Skip (1).ToArray ();
         sSkipModified = true;
      }

      sRepositoryDir = Path.GetFullPath (GetCurrentDirectoryOrArgument (args, 0));
      // Validates repo dir
      if (!Directory.Exists (Path.Combine(sRepositoryDir, ".git"))) {
         Console.WriteLine ($"Could not find .git directory at given path. '{sRepositoryDir}'");
         Environment.Exit (-1);
      }

      sBackupDir = Path.GetFullPath (GetBackupDirectoryOrDefault (args, 1));
      var ext = Path.GetExtension (sBackupDir);
      // Validates backup dir
      if (!sBackupDir.IsValidDirectoryPath () || !string.IsNullOrEmpty (ext)) {
         Console.WriteLine ($"Not a valid directory. '{sBackupDir}'");
         Environment.Exit (-1);
      }
      // Checks if Repo and Backup dir are different
      //if (string.Equals (sRepositoryDir, sBackupDir, StringComparison.OrdinalIgnoreCase)) {
      //   Console.WriteLine ($"Repo and Backup directories path should not be same!!!");
      //   Environment.Exit (-1);
      //}

      var commonPath = sBackupDir.GetCommonPath (sRepositoryDir);
      // Takes subDirName\\backupFilename upto the datetime stamp. 
      var backupRelativeDir = sBackupDir[commonPath.Length..].TrimStart (Path.DirectorySeparatorChar)[..^2]; // Omits _1, _2, ...

      Console.WriteLine ($"=> Repo Directory: {sRepositoryDir}");
      Console.WriteLine ($"=> Backup Directory: {sBackupDir}");
      Console.WriteLine ($"=> Backup Started...");

      // Processing Repo to log changes and to take backup
      bool isBackupSuccess = false;
      try {
         using var repo = new Repository (sRepositoryDir); // Gets repo from the path to .git

         #region RepoChangeLog
         var sbStaged = new StringBuilder ();
         var sbUnstaged = new StringBuilder ();
         var sbUntracked = new StringBuilder ();
         var sbConflicted = new StringBuilder ();

         // Retrieves git status without including .gitignored files
         var files = repo.RetrieveStatus (new StatusOptions { IncludeIgnored = false, IncludeUntracked = true });

         foreach (var file in files) {
            var state = file.State;

            var isStaged = state.HasFlag (FileStatus.NewInIndex) || state.HasFlag (FileStatus.ModifiedInIndex) || state.HasFlag (FileStatus.RenamedInIndex) || state.HasFlag (FileStatus.DeletedFromIndex) || state.HasFlag (FileStatus.TypeChangeInIndex);
            var isUnstaged = state.HasFlag (FileStatus.ModifiedInWorkdir) || state.HasFlag (FileStatus.RenamedInWorkdir) || state.HasFlag (FileStatus.DeletedFromWorkdir) || state.HasFlag (FileStatus.TypeChangeInWorkdir);
            var isUntracked = state.HasFlag (FileStatus.NewInWorkdir);
            var isConflicted = state.HasFlag (FileStatus.Conflicted);

            var s = GetFileStatusName (state); // Gets status name - New/Modified/Deleted/Renamed/TypeChanged

            if (isStaged || isUnstaged) {
               sbStaged.AppendLine ($"{s} - '{file.FilePath}'");
            }
            //if (isUnstaged) {
            //   sbUnstaged.AppendLine ($"{s} - '{file.FilePath}'");
            //}
            if (isUntracked) {
               if (!file.FilePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase)) {
                  sbUntracked.AppendLine ($"{s} - '{file.FilePath}'");
               }
            }
            //if (isConflicted) {
            //   sbConflicted.AppendLine ($"{s} - '{file.FilePath}'");
            //}
         }

         var sb = new StringBuilder ();
         sb.AppendLine ($"BRANCH: {repo.Head.FriendlyName}\n");
         if (sbStaged.Length > 0) {
            sb.AppendLine ("CHANGES IN TRACKED FILES: ********************************************************");
            sb.AppendLine (sbStaged.ToString () + "\n");
         }
         //if (sbUnstaged.Length > 0) {
         //   sb.AppendLine ("UNSTAGED: ********************************************************");
         //   sb.AppendLine (sbUnstaged.ToString () + "\n");
         //}
         if (sbUntracked.Length > 0) {
            sb.AppendLine ("UNTRACKED: ********************************************************");
            sb.AppendLine (sbUntracked.ToString () + "\n");
         }
         //if (sbConflicted.Length > 0) {
         //   sb.AppendLine ("CONFLICTED: ********************************************************");
         //   sb.AppendLine (sbConflicted.ToString () + "\n");
         //}

         File.WriteAllText (sGitStatusFilePath, sb.ToString ());
         Console.WriteLine (sb.ToString ());
         /*

         #region Staged

         if (!sSkipStaged && files.Staged.Count () > 0) {
            sb.AppendLine ("********************************************************");
            sb.AppendLine ("STAGED FILES:");
            foreach (var f in files.Staged) {
               var s = GetFileStatus (f);
               sb.AppendLine ($"{s} - '{f.FilePath}'");
            }
         }

         //if (!sSkipAdded && files.Added.Count () > 0) {
         //   sb.AppendLine ("********************************************************");
         //   sb.AppendLine ("ADDED FILES:");
         //   foreach (var f in files.Added) {
         //      if (!f.FilePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase))
         //         sb.AppendLine (f.FilePath);
         //   }
         //}

         //if (!sSkipDeleted && files.Removed.Count () > 0) {
         //   sb.AppendLine ("********************************************************");
         //   sb.AppendLine ("REMOVED FILES:");
         //   foreach (var f in files.Removed) {
         //      if (!f.FilePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase))
         //         sb.AppendLine (f.FilePath);
         //   }
         //}

         #endregion

         #region Unstaged

         if (!sSkipModified && files.Modified.Count () > 0) {
            sb.AppendLine ("********************************************************");
            sb.AppendLine ("UNSTAGED FILES:");
            foreach (var f in files.Modified) {
               var s = GetFileStatus (f);
               sb.AppendLine ($"{s} - '{f.FilePath}'");
            }
         }

         #endregion

         #region Deleted

         if (!sSkipModified && files.Missing.Count () > 0) {
            sb.AppendLine ("********************************************************");
            sb.AppendLine ("MISSING FILES:");
            foreach (var f in files.Missing) {
               var s = GetFileStatus (f);
               sb.AppendLine ($"{s} - '{f.FilePath}'");
            }
         }

         #endregion

         #region Untracked

         if (!sSkipUntracked && files.Untracked.Count () > 0) {
            sb.AppendLine ("********************************************************");
            sb.AppendLine ("UNTRACKED FILES:");
            foreach (var f in files.Untracked) {
               if (!f.FilePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase)) {
                  var s = GetFileStatus (f);
                  sb.AppendLine ($"{s} - '{f.FilePath}'");
               }
            }
         }

         #endregion

         */
         #endregion

         #region BackupRepoFiles
         // Takes backup of the files
         foreach (var item in files) {
            if (item.State != FileStatus.Unaltered) { // git ignored files are not considered by default. // item.State != FileStatus.Ignored
               // checks for files in the parent folder/subfolders.
               var filePath = item.FilePath.NormalizePathSeparators ();
               Console.WriteLine ($"{item.State} - {filePath}");
               if (!filePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase)) {
                  if (sSkipUntracked && item.State.HasFlag (FileStatus.NewInWorkdir)) continue; // Skips untracked files if required
                  string srcFile = Path.Combine (sRepositoryDir, item.FilePath);
                  if (!File.Exists (srcFile)) continue; // checks if the file is not deleted
                  string destFilePath = Path.Combine (sBackupDir, item.FilePath);
                  var dirName = Path.GetDirectoryName (destFilePath) ?? "";
                  if (!string.IsNullOrEmpty (dirName)) Directory.CreateDirectory (dirName);
                  File.Copy (srcFile, destFilePath, true);
                  Console.WriteLine ($"+ Copied {item.State} file: {item.FilePath}");
               }
            }
         }
         #endregion

         isBackupSuccess = true;
      } catch (LibGit2Sharp.RepositoryNotFoundException ex) {
         Console.WriteLine ($"RepositoryNotFoundException: {ex.Message}");
         isBackupSuccess = false;
      } catch (Exception ex) {
         Console.WriteLine ($"Unknown Exception: {ex.Message}");
         isBackupSuccess = false;
      }
      Console.WriteLine ($"=> Backup {(isBackupSuccess ? "completed." : "failed.")}");
      Environment.Exit (0);
   }

   static string GetCurrentDirectoryOrArgument (string[] args, int index) {
      return args.Length > index ? args[index] : Directory.GetCurrentDirectory ();
   }

   static string GetBackupDirectoryOrDefault (string[] args, int index) {
      string dateSuffix = DateTime.Now.ToString ("ddMMMyyyy");
      string defaultBackupDir = "";
      var isBackupDirArg = args.Length > index;
      string backupDir = isBackupDirArg ? Path.GetFullPath (args[index]) : sRepositoryDir;
      string backupFolderName = "";
      int i = 1;
      do {
         backupFolderName = $"Backup_{dateSuffix}_{i++}";
         defaultBackupDir = Path.Combine (backupDir, backupFolderName);
      }
      while (Directory.Exists (defaultBackupDir));
      Directory.CreateDirectory (defaultBackupDir);
      sGitStatusFilePath = Path.Combine (defaultBackupDir, backupFolderName + ".txt");
      File.WriteAllText (sGitStatusFilePath, ""); // Creating file with empty content.
      return defaultBackupDir;
   }

   static string GetFileStatus (StatusEntry file) {
      return file.State switch {
         FileStatus.NewInIndex
            or FileStatus.NewInWorkdir
            or (FileStatus.NewInIndex | FileStatus.ModifiedInWorkdir)
            or (FileStatus.NewInIndex | FileStatus.NewInWorkdir) => "[ADDED]",

         FileStatus.ModifiedInIndex
            or FileStatus.ModifiedInWorkdir
            or (FileStatus.ModifiedInIndex | FileStatus.ModifiedInWorkdir) => "[MODIFIED]",

         FileStatus.DeletedFromIndex
            or FileStatus.DeletedFromWorkdir
            or FileStatus.Nonexistent
            or FileStatus.NewInIndex | FileStatus.DeletedFromWorkdir
            or (FileStatus.DeletedFromIndex | FileStatus.DeletedFromWorkdir) => "[DELETED]",

         FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir | (FileStatus.RenamedInIndex | FileStatus.RenamedInWorkdir) => "[RENAMED]",

         _ => $"[{file.State.ToString ()}]" // Default
      };
   }

   static string GetFileStatusName (FileStatus fileStatus) {
      return fileStatus switch {
         _ when fileStatus.HasFlag (FileStatus.NewInWorkdir) => "[ADDED]",
         _ when fileStatus.HasFlag (FileStatus.ModifiedInWorkdir) => "[MODIFIED]",
         _ when fileStatus.HasFlag (FileStatus.RenamedInWorkdir) => "[RENAMED]",
         _ when fileStatus.HasFlag (FileStatus.DeletedFromWorkdir) => "[DELETED]",
         _ when fileStatus.HasFlag (FileStatus.TypeChangeInWorkdir) => "[TYPECHANGED]",
         _ => string.Join (" | ", Enum.GetValues (typeof (FileStatus)).Cast<FileStatus> ().Where (f => fileStatus.HasFlag (f)).Select (f => f))
      };
   }

}
