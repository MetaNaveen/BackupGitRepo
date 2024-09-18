using System.Diagnostics;
using System.Reflection;
using System.Text.Json;

namespace Utils;

public class SelfUpdater {
   #region Public

   public static async Task<bool> Run (string owner, string repo, string assetName) {
      var (serverVersion, downloadUrl) = await GetLatestReleaseAsync (owner, repo, assetName);
      if (string.IsNullOrEmpty(serverVersion) || string.IsNullOrEmpty (downloadUrl)) {
         return false;
      }

      serverVersion = serverVersion.Trim (['v', 'V']);
      var serverVersionComponents = serverVersion.Split ('.', StringSplitOptions.RemoveEmptyEntries);
      _ = int.TryParse (serverVersionComponents[0], out int serverMajor);
      _ = int.TryParse (serverVersionComponents.Length == 2 ? serverVersionComponents[1] : "0", out int serverMinor);

      var localVersion = GetAssemblyVersion ();

      if (serverMajor > localVersion!.Major || (serverMajor == localVersion.Major && serverMinor > localVersion.Minor)) {
         Console.WriteLine ($"A newer version {serverMajor}.{serverMinor} is available. Downloading...");
         await UpdateApplicationAsync (downloadUrl);
      }
      return true;

      static (string Name, int Major, int Minor, int Revision) GetAssemblyVersion () {
         Assembly? currentAssembly = Assembly.GetEntryAssembly ();//Assembly.GetExecutingAssembly ();
         AssemblyName assemblyName = currentAssembly!.GetName ();
         Version? currentVersion = assemblyName.Version;
         return (assemblyName.Name!, currentVersion!.Major, currentVersion.Minor, currentVersion.Revision);
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
            timeout /t 2 /nobreak > NUL
            del ""{currentExePath}""
            move /Y ""{updatedExePath}"" ""{currentExePath}""
            timeout /t 2 /nobreak > NUL
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

      static string GetTempFile (string suffix = "") {
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

   static async Task<(string TagName, string DownloadUrl)> GetLatestReleaseAsync (string owner, string repo, string assetName) {
      var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
      httpClient.DefaultRequestHeaders.Add ("User-Agent", "BackupGitRepo");

      try {
         var response = await httpClient.GetStringAsync (url);
         using var jsonDocument = JsonDocument.Parse (response);
         var root = jsonDocument.RootElement;

         var tagName = root.GetProperty ("tag_name").GetString ();
         var assets = root.GetProperty ("assets");

         var assetsCount = assets.GetArrayLength ();
         if (assetsCount > 0) {
            var i = 0;
            string downloadUrl = "";
            do {
               downloadUrl = assets[i++].GetProperty ("browser_download_url").GetString () ?? "";
            } while (i < assetsCount && !downloadUrl.EndsWith (assetName));
            
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
