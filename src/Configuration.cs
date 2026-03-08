using MelonLoader;
using RumbleModUI;
using UnityEngine;


namespace RumblePhotoAlbum;
public partial class MainClass : MelonMod
{
    private Mod Mod = new Mod();

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
        Mod.AddToList("Default frame color", "#7dc6e3", "You can set the frame color individually by adding a \"color\" field to the picture's JSON config.", new Tags { });
        Mod.AddToList("Default frame padding", 0.01f, "You can set the frame padding individually by adding a \"padding\" field to the picture's JSON config.", new Tags { });
        Mod.AddToList("Default frame thickness", 0.01f, "You can set the frame thickness individually by adding a \"thickness\" field to the picture's JSON config.", new Tags { });
        Mod.AddToList("Default frame metallicness", 0f, "You can set the frame's metallic property individually by adding a \"metallic\" field to the picture's JSON config.", new Tags { });
        Mod.AddToList("Default picture size", 0.5f, "This is the default size of the pictures when they spawn. It will not change the pictures that are already positioned.", new Tags { });
        Mod.AddToList("Enable transparency", false, 0, "WARNING: this option adds a lag spike on picture creation.\nTo lower the effect, you can enable transparency individually by adding a boolean field \"alpha\" to the picture's JSON config.", new Tags { });
        Mod.AddToList("Show on camera", true, 0, "If disabled, this will hide all pictures from legacy camera, as well as LIV and Rock Cam.\nYou can hide/show any individual picture by clicking the corresponding button while holding it.", new Tags { });
        Mod.AddToList("Show action buttons", true, 0, "If disabled, the 3 buttons on the held picture won't appear.", new Tags { });
        Mod.AddToList("Picture creation frequency", 0.02f, "How long to wait between picture spawning during the scene initialization. The bigger the number, the longer it will take for all the pictures to appear!.", new Tags { });
        Mod.AddToList("GIF playing speed", 1f, "The hardcoded mimimum delay between frames is 1000ms, so there is a maximum speed.", new Tags { });
        Mod.AddToList("GIF decoding frequency", 0.01f, "How long to wait between parsing two consecutives frames in a GIF. Smaller number means faster loading, but also higher performance impact during scene initialization.", new Tags { });
        
        Mod.AddToList("Grab Threshold", 0.6f,"How much you need to press the trigger to grab a picture, 1.0 is fully pressed 0.0 is not pressed. WARNING do not use 1.0 or above, it makes the picture ungrabbable");
        Mod.AddToList("Release Threshold",0.4f, "How little a trigger needs to be pressed to release a picture, 1.0 is fully pressed 0.0 is not pressed at all, WARNING anything bellow 0.0 will make you unable to let go of pictures");
        Mod.AddToList("Load album",true,0 ,"Weather the mod will show any photos, this essentially disables the mod when set to false. Very handy if you happen to have 300 images in the ring :3 -O");
        
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
        defaultMetallicness = (float)Mod.Settings[3].SavedValue;
        defaultSize = (float)Mod.Settings[4].SavedValue;
        enableAlpha = (bool)Mod.Settings[5].SavedValue;
        visibility = (bool)Mod.Settings[6].SavedValue;
        buttonsVisibility = (bool)Mod.Settings[7].SavedValue;
        spawningFrequency = (float)Mod.Settings[8].SavedValue;
        gifSpeed = (float)Mod.Settings[9].SavedValue;
        gifDecodingFrequency = (float)Mod.Settings[10].SavedValue;
        grabThreshold = (float)Mod.Settings[11].SavedValue;
        releaseThreshold = (float)Mod.Settings[12].SavedValue;
        shouldLoadAlbum = (bool)Mod.Settings[13].SavedValue;
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
        MelonCoroutines.Start(LoadAlbum(currentScene));
    }
}