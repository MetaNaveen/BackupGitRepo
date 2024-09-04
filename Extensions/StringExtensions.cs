namespace BackupGitRepo;

public static class StringExtensions {
   public static string GetCommonPath (this string path1, string path2) {
      var commonPath = "";
      if (string.IsNullOrWhiteSpace (path1) || string.IsNullOrWhiteSpace (path2))
         return commonPath;
      
      var foldersList1 = Path.GetFullPath (path1).Split (Path.DirectorySeparatorChar);
      var foldersList2 = Path.GetFullPath (path2).Split (Path.DirectorySeparatorChar);

      var minLength = Math.Min (foldersList1.Length, foldersList2.Length);
      for (var i = 0; i < minLength; i++) {
         if (foldersList1[i] == foldersList2[i]) {
            commonPath = Path.Combine (commonPath, foldersList1[i]);
         } else break;
      }

      return commonPath;
   }
}
