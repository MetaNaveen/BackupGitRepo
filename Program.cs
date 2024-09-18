using LibGit2Sharp;
using System.Reflection;
using System.Text;

namespace BackupGitRepo;

class Program {
   static string sRepositoryDir = "", sBackupDir = "", sGitStatusFilePath = "";
   static bool sSkipUntracked = false, sIncludeGitIgnored = false;

   static void Main (string[] args) {
      try {
         var isUpdated = Task.Run (async () => await SelfUpdater.Run ("MetaNaveen", "BackupGitRepo", "BackupGitRepo.exe")).GetAwaiter ().GetResult ();
         if (!isUpdated) throw new Exception ("Unknown reason.");
      } catch (Exception ex) {
         Console.WriteLine ($"Self update failed! with error '{ex.Message}'.\n Running with the current version...");
      }

      if (args.Length == 0) {
         var appName = Assembly.GetExecutingAssembly ().GetName ().Name;
         if (string.IsNullOrEmpty (appName)) appName = "BackupGitRepo";
         appName += ".exe";
         Console.WriteLine ($"{appName} [-su | -ii] <RepoDirectoryPath> [<BackupDirectoryPath>]");
         Environment.Exit (-1);
      }

      // -su - skipUntracked
      // -ii - Include GitIgnored
      if (args[0].StartsWith ("-su", StringComparison.OrdinalIgnoreCase)) {
         args = args.Skip (1).ToArray ();
         sSkipUntracked = true;
      }

      if (args[0].StartsWith ("-ii", StringComparison.OrdinalIgnoreCase)) {
         args = args.Skip (1).ToArray ();
         sIncludeGitIgnored = true;
      }

      sRepositoryDir = Path.GetFullPath (GetCurrentDirectoryOrArgument (args, 0));
      // Validates repo dir
      if (!Directory.Exists (Path.Combine (sRepositoryDir, ".git"))) {
         Console.WriteLine ($"Could not find .git directory at given path. '{sRepositoryDir}'");
         Environment.Exit (-1);
      }

      var repoName = Path.GetFileName (sRepositoryDir);
      if (string.IsNullOrEmpty (repoName)) repoName = sRepositoryDir.GetParentOrDrivePath ();

      using var repo = new Repository (sRepositoryDir); // Gets repo from the path to .git

      sBackupDir = Path.GetFullPath (GetBackupDirectoryOrDefault (args, 1, repoName, repo.Head.FriendlyName));
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
         #region RepoChangeLog
         var sbModified = new StringBuilder ();
         var sbUntracked = new StringBuilder ();

         // Retrieves git status without including .gitignored files
         var files = repo.RetrieveStatus (new StatusOptions { IncludeIgnored = sIncludeGitIgnored, IncludeUntracked = true });

         foreach (var file in files) {
            var state = file.State;
            var isUntracked = state.HasFlag (FileStatus.NewInWorkdir);

            var s = GetFileStatusName (state); // Gets status name - New/Modified/Deleted/Renamed/TypeChanged
            if (!file.FilePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase)) {
               if (isUntracked) {
                  sbUntracked.AppendLine ($"'{file.FilePath}'");
               } else sbModified.AppendLine ($"'{file.FilePath}'");
            }
         }

         var sb = new StringBuilder ();
         sb.AppendLine ($"BRANCH: {repo.Head.FriendlyName}\n");
         if (sbModified.Length > 0) {
            sb.AppendLine ("CHANGES IN TRACKED FILES: ********************************************************");
            sb.AppendLine (sbModified.ToString () + "\n");
         }
         if (sbUntracked.Length > 0) {
            sb.AppendLine ("UNTRACKED NEW FILES/FOLDERS: ********************************************************");
            sb.AppendLine (sbUntracked.ToString () + "\n");
         }

         File.WriteAllText (sGitStatusFilePath, sb.ToString ());
         //Console.WriteLine (sb.ToString ());

         #endregion

         #region BackupRepoFiles
         // Takes backup of the files
         foreach (var item in files) {
            if (item.State != FileStatus.Unaltered) { // git ignored files are not considered by default. // item.State != FileStatus.Ignored
               // checks for files in the parent folder/subfolders.
               var filePath = item.FilePath.NormalizePathSeparators ();
               //Console.WriteLine ($"{item.State} - {filePath}");
               if (!filePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase)) {
                  if (sSkipUntracked && item.State.HasFlag (FileStatus.NewInWorkdir)) continue; // Skips untracked files if required
                  string srcPath = Path.Combine (sRepositoryDir, item.FilePath);
                  var isDir = Directory.Exists (srcPath);
                  if (!File.Exists (srcPath) && !isDir) continue; // checks if the file is not deleted
                  string destFilePath = Path.Combine (sBackupDir, item.FilePath);
                  var dirName = Path.GetDirectoryName (destFilePath) ?? ""; // gets directory of file. if directory, returns same directory's name
                  if (!string.IsNullOrEmpty (dirName)) Directory.CreateDirectory (dirName);
                  if (isDir) { // only for gitignored (folder level)
                     Console.WriteLine ($"+ Copied directory: {item.FilePath}");
                     foreach (var f in Directory.GetFiles (srcPath)) {
                        var fileName = Path.GetFileName (f);
                        var dest = Path.Combine (destFilePath, fileName);
                        var src = Path.Combine (srcPath, fileName);
                        File.Copy (src, dest, true);
                     }
                  } else {
                     File.Copy (srcPath, destFilePath, true);
                     Console.WriteLine ($"+ Copied file: {item.FilePath}");
                  }
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

   static string GetBackupDirectoryOrDefault (string[] args, int index, string repoName, string branchName) {
      string dateSuffix = DateTime.Now.ToString ("yyyyMMdd");
      string defaultBackupDir = "";
      var isBackupDirArg = args.Length > index;
      string backupDir = isBackupDirArg ? Path.GetFullPath (args[index]) : sRepositoryDir;
      string backupFolderName = "";
      int i = 1;
      do {
         backupFolderName = $"Backup_{repoName}_{branchName}_{dateSuffix}_{i++}";
         defaultBackupDir = Path.Combine (backupDir, backupFolderName);
      }
      while (Directory.Exists (defaultBackupDir));
      Directory.CreateDirectory (defaultBackupDir);
      sGitStatusFilePath = Path.Combine (defaultBackupDir, backupFolderName + ".txt");
      File.WriteAllText (sGitStatusFilePath, ""); // Creating file with empty content.
      return defaultBackupDir;
   }

   /// <summary>Gets file status name w.r.t FileStatus enum flags.</summary>
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
