using BackupGitRepo;
using System.Diagnostics;
using System.Text.Json;

public class SelfUpdater {
   #region Public

   public static async Task<bool> Run () {
      string owner = "MetaNaveen";
      string repo = "BackupGitRepo";
      var (latestVersion, downloadUrl) = await GetLatestReleaseAsync (owner, repo);
      if (latestVersion == null || downloadUrl == null) {
         //Console.WriteLine ("Failed to retrieve the latest release information.");
         return false;
      }
      var currentVersion = GetCurrentVersion ();
      //Console.WriteLine ($"Current Version: {currentVersion}");
      //Console.WriteLine ($"Latest Version: {latestVersion}");
      if (IsNewerVersion (currentVersion, latestVersion)) {
         Console.WriteLine ("A new version is available. Downloading...");
         await UpdateApplicationAsync (downloadUrl);
      }
      return true;

      string GetCurrentVersion () {
         var version = FileVersionInfo.GetVersionInfo (typeof (Program).Assembly.Location).ProductVersion;
         return version;
      }

      bool IsNewerVersion (string currentVersion, string latestVersion) {
         return string.Compare (currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase) < 0;
      }

      async Task UpdateApplicationAsync (string downloadUrl) {
         var tempFilePath = GetTempFile (repo);
         using var httpClient = new HttpClient ();
         var response = await httpClient.GetAsync (downloadUrl);
         response.EnsureSuccessStatusCode ();
         await using var fileStream = new FileStream (tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
         await response.Content.CopyToAsync (fileStream);
         Console.WriteLine ("Download successful. Updating...");
         var exePath = Process.GetCurrentProcess ().MainModule.FileName;
         StartUpdater (exePath, tempFilePath);
         Console.WriteLine ("Update successful. You can use the updated version now!");
         Environment.Exit (0);
      }

      void StartUpdater (string currentExePath, string updatedExePath) {
         var tempPath = GetTempFile (repo);
         var batchFilePath = Path.ChangeExtension (tempPath, ".bat");

         var batchContent = $@"
            @echo off
            timeout /t 3 /nobreak > NUL
            del ""{currentExePath}""
            move /Y ""{updatedExePath}"" ""{currentExePath}""
            timeout /t 3 /nobreak > NUL
            REM del ""{updatedExePath}""
            REM start ""{Path.GetFileName (currentExePath)}""
            ";

         File.WriteAllText (batchFilePath, batchContent);

         // Start the batch file
         Process.Start (new ProcessStartInfo {
            FileName = "cmd.exe",
            Arguments = $"/c \"{batchFilePath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
         });
      }

      string GetTempFile (string suffix = "") {
         var tempFilePath = Path.GetTempFileName ();
         var tempDir = Path.GetDirectoryName (tempFilePath);
         var newTempName = Path.Combine (tempDir, Path.GetFileNameWithoutExtension (tempFilePath) + $"_{suffix}");
         File.Move (tempFilePath, newTempName);
         tempFilePath = newTempName;
         return tempFilePath;
      }
   }

   #endregion


   private static readonly HttpClient httpClient = new HttpClient ();

   static async Task<(string TagName, string DownloadUrl)> GetLatestReleaseAsync (string owner, string repo) {
      var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
      httpClient.DefaultRequestHeaders.Add ("User-Agent", "BackupGitRepo");

      try {
         var response = await httpClient.GetStringAsync (url);
         using var jsonDocument = JsonDocument.Parse (response);
         var root = jsonDocument.RootElement;

         var tagName = root.GetProperty ("tag_name").GetString ();
         var assets = root.GetProperty ("assets");

         if (assets.GetArrayLength () > 0) {
            var downloadUrl = assets[0].GetProperty ("browser_download_url").GetString ();
            return (tagName, downloadUrl);
         }

         Console.WriteLine ("No assets found in the latest release.");
         return (null, null);
      } catch (HttpRequestException e) {
         Console.WriteLine ($"Request error: {e.Message}");
         return (null, null);
      } catch (Exception e) {
         Console.WriteLine ($"Unexpected error: {e.Message}");
         return (null, null);
      }
   }
}
