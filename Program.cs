using LibGit2Sharp;
using System.Reflection;
using System.Text;

namespace BackupGitRepo;

class Program {
   static string sRepositoryDir = "", sBackupDir = "", sGitStatusFilePath = "";

   static void Main (string[] args) {
      if (args.Length == 0) {
         var appName = Assembly.GetExecutingAssembly ().GetName ().Name;
         if (string.IsNullOrEmpty (appName)) appName = "BackupGitRepo";
         appName += ".exe";
         Console.WriteLine ($"{appName} <RepoDirectoryPath> [<BackupDirectoryPath>]");
         Environment.Exit (-1);
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
         var sb = new StringBuilder ();
         // Retrieves git status without including .gitignored files
         var files = repo.RetrieveStatus (new StatusOptions { IncludeIgnored = false });
         sb.AppendLine ($"BRANCH: {repo.Head.FriendlyName}\n");
         if (files.Staged.Count () > 0) {
            sb.AppendLine ("********************************************************");
            sb.AppendLine ("STAGED FILES:");
            foreach (var f in files.Staged) sb.AppendLine (f.FilePath);
         }
         if (files.Modified.Count () > 0) {
            sb.AppendLine ("********************************************************");
            sb.AppendLine ("MODIFIED FILES:");
            foreach (var f in files.Modified) sb.AppendLine (f.FilePath);
         }
         if (files.Untracked.Count () > 0) {
            sb.AppendLine ("********************************************************");
            sb.AppendLine ("UNTRACKED FILES:");
            foreach (var f in files.Untracked) {
               if (!f.FilePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase))
                  sb.AppendLine (f.FilePath);
            }
         }  
         if (files.Added.Count () > 0) {
            sb.AppendLine ("********************************************************");
            sb.AppendLine ("ADDED FILES:");
            foreach (var f in files.Added) {
               if (!f.FilePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase))
                  sb.AppendLine (f.FilePath);
            }
         }
         File.WriteAllText (sGitStatusFilePath, sb.ToString ());
         #endregion

         #region BackupRepoFiles
         // Takes backup of the files
         foreach (var item in files) {
            if (item.State != FileStatus.Unaltered) { // git ignored files are not considered by default. // item.State != FileStatus.Ignored
               // checks for files in the parent folder/subfolders.
               var filePath = item.FilePath.NormalizePathSeparators ();
               if (!filePath.StartsWith (backupRelativeDir, StringComparison.OrdinalIgnoreCase)) {
                  string srcFile = Path.Combine (sRepositoryDir, item.FilePath);
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
}
