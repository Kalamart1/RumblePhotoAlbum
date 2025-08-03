using MelonLoader;
using RumbleModUI;
using UnityEngine;


namespace RumblePhotoAlbum;
public partial class MainClass : MelonMod
{
    Mod Mod = new Mod();

    /**
     * <summary>
     * Specify the different options that will be used in the ModUI settings
     * </summary>
     */
    private void InitModUI()
    {
        UI.instance.UI_Initialized += OnUIInit;
        SetUIOptions();
        ReadModUIOptions();
    }

    /**
     * <summary>
     * Specify the different options that will be used in the ModUI settings
     * </summary>
     */
    private void SetUIOptions()
    {
        Mod.ModName = BuildInfo.ModName;
        Mod.ModVersion = BuildInfo.ModVersion;

        Mod.SetFolder("RumblePhotoAlbum");
        Mod.AddToList("Default frame color", "#7accc2", "You can set the frame color individually by adding a \"color\" field to the picture's JSON config.", new Tags { });
        Mod.AddToList("Default frame padding", 0.01f, "You can set the frame padding individually by adding a \"padding\" field to the picture's JSON config.", new Tags { });
        Mod.AddToList("Default frame thickness", 0.01f, "You can set the frame thickness individually by adding a \"thickness\" field to the picture's JSON config.", new Tags { });
        Mod.AddToList("Default picture size", 0.5f, "This is the default size of the pictures when they spawn. It will not change the pictures that are already positioned.", new Tags { });
        Mod.AddToList("Enable transparency", false, 0, "WARNING: this option adds a lag spike on picture creation.\nTo lower the effect, you can enable transparency individually by adding a boolean field \"alpha\" to the picture's JSON config.", new Tags { });
        Mod.GetFromFile();
    }

    /**
     * <summary>
     * Called when the actual ModUI window is initialized
     * </summary>
     */
    private void OnUIInit()
    {
        Mod.ModSaved += OnUISaved;
        UI.instance.AddMod(Mod);
    }

    /**
     * <summary>
     * Reads the ModUI options and updates the global config values.
     * </summary>
     */
    private void ReadModUIOptions()
    {
        defaultColor = Hex2Color((string)Mod.Settings[0].SavedValue);
        defaultPadding = (float)Mod.Settings[1].SavedValue;
        defaultThickness = (float)Mod.Settings[2].SavedValue;
        defaultSize = (float)Mod.Settings[3].SavedValue;
        enableAlpha = (bool)Mod.Settings[4].SavedValue;
    }

    /**
     * <summary>
     * Called when the user saves a configuration in ModUI
     * </summary>
     */
    private void OnUISaved()
    {
        ReadModUIOptions();
        // reload whole album
        GameObject.Destroy(photoAlbum);
        LoadAlbum(currentScene);
    }
}