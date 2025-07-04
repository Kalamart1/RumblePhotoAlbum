using MelonLoader;

namespace RumblePhotoAlbum;

public static class BuildInfo
{
    public const string ModName = "RumblePhotoAlbum";
    public const string ModVersion = "1.0.0";
    public const string Description = "Decorate your environment with framed pictures";
    public const string Author = "Kalamart";
    public const string Company = "";
}
public partial class MainClass : MelonMod
{
    /**
    * <summary>
    * Log to console.
    * </summary>
    */
    private static void Log(string msg)
    {
        MelonLogger.Msg(msg);
    }
    /**
    * <summary>
    * Log to console but in yellow.
    * </summary>
    */
    private static void LogWarn(string msg)
    {
        MelonLogger.Warning(msg);
    }
    /**
    * <summary>
    * Log to console but in red.
    * </summary>
    */
    private static void LogError(string msg)
    {
        MelonLogger.Error(msg);
    }

    /**
    * <summary>
    * Called when the scene has finished loading.
    * </summary>
    */
    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {

    }
}
