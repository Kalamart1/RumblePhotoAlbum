﻿using MelonLoader;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

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
    // variables
    private static float defaultSize = 0.5f; // Default size of the frame (width or height depending on the orientation)
    private static float defaultThickness = 0.01f; // Default thickness of the frame
    private static float defaultPadding = 0.01f; // Default frame padding around the picture
    private static Color defaultColor = new Color(0.48f, 0.80f, 0.76f); // Rumble gym green as default frame color
    private static GameObject photoAlbum = null; // Parent object for all framed pictures
    private static string currentScene = "";

    private static List<PictureData> PicturesList = null;

    /**
    * <summary>
    * Structure of each element in the "album" field of the config file,
    * plus the corresponding GameObject in the scene (if any)
    * </summary>
    */
    public class FramedPicture
    {
        public string path;
        public Vector3 position;
        public Vector3 rotation;
        public float width = 0;
        public float height = 0;
        public float padding;
        public float thickness;
        public Color color;
    }

    /**
    * <summary>
    * Structure of each element in the "album" field of the config file,
    * plus the corresponding GameObject in the scene (if any)
    * </summary>
    */
    public class PictureData
    {
        public GameObject obj = null;
        public FramedPicture framedPicture;
        public JToken jsonConfig = null;
    }

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
     * Called when the mod is loaded into the game
     * </summary>
     */
    public override void OnLateInitializeMelon()
    {
        EnsureUserDataFolders();
    }

    /**
    * <summary>
    * Called when the scene has finished loading.
    * </summary>
    */
    public override void OnSceneWasLoaded(int buildIndex, string sceneName)
    {
        currentScene = sceneName;
        if (sceneName != "Loader")
        {
            LoadAlbum(sceneName);
            InitGrabbing();
        }
    }

    /**
     * <summary>
     * Called 50 times per second, used for frequent updates.
     * </summary>
     */
    public override void OnFixedUpdate()
    {
        if (currentScene != "Loader")
        {
            try
            {
                ProcessGrabbing();
            }
            catch (System.Exception e)
            {
                LogError($"Error in OnFixedUpdate: {e.Message}");
            }
        }
    }

    /**
     * <summary>
     * Called on every frame, used for updates that need to be really smooth.
     * </summary>
     */
    public override void OnUpdate()
    {
        if (currentScene != "Loader")
        {
            try
            {
                UpdateResizingIfNeeded();
            }
            catch (System.Exception e)
            {
                LogError($"Error in OnUpdate: {e.Message}");
            }
        }
    }
}
