namespace BackupGitRepo;

public static class PathExtensions {
   public static bool IsValidDirectoryPath (this string path) {
      if (string.IsNullOrWhiteSpace (path)) return false;

      try {
         string normalizedPath = Path.GetFullPath (path);
         return Directory.Exists (normalizedPath) || IsValidPathFormat (normalizedPath);
      } catch (Exception) {
         return false;
      }

      static bool IsValidPathFormat (string path) {
         char[] invalidChars = Path.GetInvalidPathChars ();
         foreach (char c in path) {
            if (Array.Exists (invalidChars, invalidChar => invalidChar == c)) return false;
         }
         if (path.Length > 260) return false;
         return true;
      }
   }

   // Converts all path separators to the current platform's separator
   public static string NormalizePathSeparators (this string path) {
      return path.Replace ('/', Path.DirectorySeparatorChar).Replace ('\\', Path.DirectorySeparatorChar);
   }

   // Gets ParentDirName or DrivePath without :\
   public static string GetParentOrDrivePath (this string path) {
      var cleanPath = path.Trim (Path.DirectorySeparatorChar);
      // Normalize the path and check if it's a valid drive letter
      if (cleanPath.Length == 2 && cleanPath[1] == ':') {
         return cleanPath[..^1]; // Return drive letter path without trailing backslash
      }
      // Check if the path exists
      if (!Directory.Exists (cleanPath) && !File.Exists (cleanPath)) {
         return "Path does not exist.";
      }
      // Get the directory info
      DirectoryInfo dirInfo = new DirectoryInfo (cleanPath);
      // If it's a file, get its directory
      if (dirInfo.Exists == false) {
         dirInfo = new FileInfo (cleanPath).Directory!;
      }
      // Get parent directory
      DirectoryInfo parentDir = dirInfo.Parent!;
      // Return drive letter path if no parent directory (root directory case)
      if (parentDir == null) {
         return dirInfo.FullName; // This will be the root directory or drive letter path
      }
      return parentDir.Name;
   }
}
