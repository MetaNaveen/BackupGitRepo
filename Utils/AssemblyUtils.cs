using System.Reflection;
using System.Resources;

namespace Utils;

public class AssemblyUtils {
   public static (string Name, int Major, int Minor, int Revision) GetAssemblyVersion () {
      Assembly? currentAssembly = Assembly.GetEntryAssembly (); // EXE name could be anything. //Assembly.GetExecutingAssembly ();
      AssemblyName assemblyName = currentAssembly!.GetName ();
      Version? currentVersion = assemblyName.Version;
      return (assemblyName.Name!, currentVersion!.Major, currentVersion.Minor, currentVersion.Revision);
   }

   public static string GetResXFromEmbeddedResource (string resourceFile, string resourceKey) {
      Assembly currentAssembly = Assembly.GetExecutingAssembly ()!;
      ResourceManager rm = new ($"{currentAssembly!.GetName ().Name}.Resources.{resourceFile}", currentAssembly);
      string secretKey = rm.GetString (resourceKey)!;
      return secretKey;
   }
}
