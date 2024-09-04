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
}
