using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using HarmonyLib;
using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppRUMBLE.Players.Subsystems;
using Il2CppRUMBLE.Recording.LCK;
using Il2CppRUMBLE.Serialization;
using Il2CppRUMBLE.Tutorial.MoveLearning;
using Il2CppRUMBLE.Utilities;
using Il2CppTMPro;
using MelonLoader;
using Newtonsoft.Json.Linq;
using RumbleModdingAPI;
using UnityEngine;

namespace RumblePhotoAlbum;
public partial class MainClass : MelonMod
{
    private static GameObject AlbumInteractionItems = null;
    private static GameObject gearMarketButton = null;
    private static GameObject mailTubeObj = null;
    private static GameObject rockCamButton = null;
    private static Transform rockCamTf = null;
    private static GameObject rockCamHandle = null;
    private static PictureData rockCamPicture = null;
    private static bool rockCamInitialized = false;
    private static int PhotoPrintingIndex = 0;
    private static MailTube mailTube = null;
    private static GameObject mailTubeHandle = null;
    private static PictureData mailTubePicture = null;
    private static Transform purchaseSlab = null;
    private static bool animationRunning = false;

    private static JArray stashJson;
    private static JArray albumJson;

    private const float mailTubeScale = 0.505f;

    /**
    * <summary>
    * Initializes the buttons and various interactables.
    * </summary>
    */
    public static void initializeInteractionObjects()
    {
        MelonCoroutines.Start(InitObjects());
    }

    /**
    * <summary>
    * Initializes all the objects for both the scene and DontDestroyOnLoad.
    * </summary>
    */
    private static IEnumerator<WaitForSeconds> InitObjects()
    {
        rockCamInitialized = false;
        if (currentScene == "Loader")
        {
            yield break;
        }
        if (currentScene == "Gym")
        {
            yield return new WaitForSeconds(5f);
            if (AlbumInteractionItems is null)
            {
                initializeGlobals();
            }
            initializeGymObjects();
        }
        else if (currentScene == "Park")
        {
            initializeParkObjects();
        }
        initializeRockCam();
    }

    /**
    * <summary>
    * Initializes the objects that are to be saved to DontDestroyOnLoad (used in multiple scenes).
    * </summary>
    */
    public static void initializeGlobals()
    {
        AlbumInteractionItems = new GameObject();
        AlbumInteractionItems.name = "AlbumInteractionItems";
        GameObject.DontDestroyOnLoad(AlbumInteractionItems);

        // get Gear Market large button
        GameObject messageScreen = GameObject.Find("--------------LOGIC--------------").transform.GetChild(3).GetChild(14).GetChild(1).gameObject;
        gearMarketButton = GameObject.Instantiate(messageScreen.transform.GetChild(1).gameObject);
        gearMarketButton.name = "gearMarketButton";
        gearMarketButton.SetActive(false);
        gearMarketButton.transform.SetParent(AlbumInteractionItems.transform);

        mailTubeObj = GameObject.Instantiate(GameObject.Find("--------------LOGIC--------------").transform.GetChild(3).GetChild(14).GetChild(6).gameObject);
        mailTubeObj.name = "mailTube";
        mailTubeObj.SetActive(false);
        mailTubeObj.transform.SetParent(AlbumInteractionItems.transform);

        // get Rock Cam "flip camera" button
        rockCamTf = Calls.Players.GetPlayerController().gameObject.transform.GetChild(10).GetChild(2);
        rockCamButton = GameObject.Instantiate(rockCamTf.GetChild(2).GetChild(0).GetChild(1).GetChild(4).GetChild(0).gameObject);
        rockCamButton.name = "rockCamButton";
        rockCamButton.SetActive(false);
        rockCamButton.transform.SetParent(AlbumInteractionItems.transform);
    }

    /**
    * <summary>
    * Initializes the Rock Cam print button and handle to print to.
    * </summary>
    */
    public static void initializeRockCam()
    {
        try
        {
            if (rockCamInitialized || rockCamButton is null || gearMarketButton is null)
            {
                return;
            }
            var playerController = Calls.Players.GetPlayerController();
            if (playerController is null)
            {
                return;
            }
            rockCamTf = playerController.gameObject.transform.GetChild(10).GetChild(2);

            // add a "Print photo" button to the top edge of Rock Cam
            System.Action action = () => PrintPhoto();
            GameObject printButton = NewRockCamButton("printButton", "Print photo", action);
            printButton.transform.SetParent(rockCamTf.GetChild(2).GetChild(0), true);
            printButton.transform.localPosition = new Vector3(-0.08f, 0.034f, 0.143f);
            printButton.transform.localRotation = Quaternion.Euler(new Vector3(90f, 0, 0));

            // Create an inclined handle to attach "printed" pictures to (above Rock Cam)
            rockCamHandle = new GameObject();
            rockCamHandle.name = "printHandle";
            rockCamHandle.transform.localScale = Vector3.one;
            rockCamHandle.transform.SetParent(rockCamTf, true);
            rockCamHandle.transform.localPosition = new Vector3(0.02f, 0.24f, 0.01f);
            rockCamHandle.transform.localRotation = Quaternion.Euler(new Vector3(40, 180, 0));

            // If the print button is pressed, it will print the most recent photo
            PhotoPrintingIndex = 0;
            rockCamInitialized = true;
        }
        catch (Exception e)
        {
            rockCamInitialized = false;
        }
    }

    /**
    * <summary>
    * Initializes the objects that are specific to the Gym scene.
    * </summary>
    */
    public static void initializeGymObjects()
    {
        //Get the mail tube object in the gym
        mailTube = GameObject.Find("--------------LOGIC--------------").transform.GetChild(3).GetChild(14).GetChild(6).gameObject.GetComponent<MailTube>();

        // Create a new button on the gear market for spawning pictures
        System.Action action = () => SpawnPicture();
        GameObject spawnButton = NewGearMarketButton("spawnButton", "Spawn picture", action);
        GameObject gearMarket = GameObject.Find("--------------LOGIC--------------").transform.GetChild(3).GetChild(14).gameObject;
        spawnButton.transform.SetParent(gearMarket.transform);
        spawnButton.transform.localPosition = new Vector3(0.075f, 1.1f, 0.19f);
        spawnButton.transform.localRotation = Quaternion.Euler(new Vector3(270, 270, 0));

        initializeMailTubeObjects();
    }

    /**
    * <summary>
    * Initializes the objects that are specific to the Park scene.
    * </summary>
    */
    public static void initializeParkObjects()
    {
        //Copy the mail tube object that comes from the gym
        mailTube = NewMailTube().GetComponent<MailTube>();
        mailTube.gameObject.name = "mailTube";
        mailTube.transform.position = new Vector3(-13.3f, -5.88f, 4.71f);
        mailTube.transform.rotation = Quaternion.Euler(new Vector3(0, 180, 0));

        // Create a new button on the gear market for spawning pictures
        System.Action action = () => SpawnPicture();
        GameObject spawnButton = NewGearMarketButton("spawnButton", "Spawn picture", action);
        spawnButton.transform.position = new Vector3(-13.19f, -4.68f, 5.42f);
        spawnButton.transform.rotation = Quaternion.Euler(new Vector3(-90, 35, 0));

        initializeMailTubeObjects();
    }

    /**
    * <summary>
    * Initializes all the objects that are needed for the mail tube to work for delivering pictures.
    * </summary>
    */
    public static void initializeMailTubeObjects()
    {
        // reset state variables
        animationRunning = false;
        stashJson = (JArray)root[currentScene]["stash"];
        albumJson = (JArray)root[currentScene]["album"];
        mailTubePicture = null;

        // Initialize transforms for positioning the picture during the animation
        purchaseSlab = mailTube.gameObject.transform.GetChild(5);
        mailTubeHandle = new GameObject();
        mailTubeHandle.name = "mailTubeHandle";
        mailTubeHandle.transform.localScale = mailTubeScale * Vector3.one;
        mailTubeHandle.transform.SetParent(purchaseSlab, true);
        mailTubeHandle.transform.localPosition = Vector3.zero;
        mailTubeHandle.transform.localRotation = Quaternion.Euler(Vector3.zero);
    }

    /**
    * <summary>
    * Creates a copy of the Mail Tube on the Gear Market.
    * </summary>
    */
    public static GameObject NewMailTube()
    {
        // Copy the object that we saved to DontDestroyOnLoad earlier
        GameObject newMailTube = GameObject.Instantiate(mailTubeObj);
        newMailTube.SetActive(true);
        return newMailTube;
    }

    /**
    * <summary>
    * Creates a copy of the large gear market button.
    * </summary>
    */
    public static GameObject NewGearMarketButton(string name, string text, System.Action action)
    {
        // Copy the object that we saved to DontDestroyOnLoad earlier
        GameObject newButton = GameObject.Instantiate(gearMarketButton);
        newButton.SetActive(true);
        newButton.name = name;
        // onEndInteraction is the moment you release the button
        newButton.transform.GetChild(0).gameObject.GetComponent<InteractionTouch>().onEndInteraction.AddListener(action);
        TextMeshPro buttonText = newButton.transform.GetChild(0).GetChild(3).gameObject.GetComponent<TextMeshPro>();
        buttonText.fontSize = 0.6f;
        buttonText.m_text = text;
        return newButton;
    }

    /**
    * <summary>
    * Creates a copy of the "flip camera" button from Rock Cam.
    * </summary>
    */
    public static GameObject NewRockCamButton(string name, string text, System.Action action)
    {
        // Copy the object that we saved to DontDestroyOnLoad earlier
        GameObject newButton = GameObject.Instantiate(rockCamButton);
        newButton.SetActive(true);
        newButton.name = name;
        newButton.GetComponent<InteractionButton>().onPressed.AddListener(action);
        TextMeshPro buttonText = newButton.transform.GetChild(1).gameObject.GetComponent<TextMeshPro>();
        buttonText.m_text = text;
        return newButton;
    }

    /**
    * <summary>
    * Retrieves the N-th most recent photo, copies it to the pictures folder, and returns its file name.
    * </summary>
    */
    private static string GetNthMostRecentPhoto(string sourceFolder, int n)
    {
        string picturesPath = Path.Combine(Application.dataPath, "..", UserDataPath, picturesFolder);
        // order by creation time, most recent first
        var files = Directory.GetFiles(sourceFolder, "*.*")
                             .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg"))
                             .OrderByDescending(File.GetCreationTime)
                             .ToList();

        if (n >= files.Count) // there aren't enough photos in the folder
            return null;

        string src = files[n];
        string fileName = Path.GetFileName(src);
        string dst = Path.Combine(picturesPath, fileName);
        File.Copy(src, dst, overwrite: true);
        return fileName;
    }
    public static void PrintPhoto()
    {
        if (rockCamPicture is not null)
        {
            // if the rock cam slot is still busy, ignore the button press
            return;
        }

        RecordingConfiguration recordingConfig = Singleton<RecordingCamera>.instance.configuration;
        LCKTabletUtility rockCamUtility = rockCamTf.gameObject.GetComponent<LCKTabletUtility>();
        string recordingPath = Path.Combine(recordingConfig.LCKSavePath, rockCamUtility.photosFolderName);
        string imageFile = GetNthMostRecentPhoto(recordingPath, PhotoPrintingIndex);
        PhotoPrintingIndex++; // next time you press the buton, it will print the next photo
        if (imageFile is null)
        {
            LogWarn("No photo to print");
            return;
        }

        FramedPicture framedPicture = new FramedPicture();
        framedPicture.path = imageFile;

        // Create the json object that will be used to save the config
        rockCamPicture = new PictureData();
        rockCamPicture.jsonConfig = new JObject();
        rockCamPicture.jsonConfig["path"] = framedPicture.path;

        // The spawned picture will use the default size and color
        framedPicture.padding = defaultPadding;
        framedPicture.thickness = defaultThickness;
        framedPicture.color = defaultColor;
        GameObject obj = CreatePictureBlock(framedPicture, rockCamHandle.transform);
        obj.transform.localPosition = new Vector3(0, framedPicture.height / 2, 0);
        obj.transform.localRotation = Quaternion.Euler(Vector3.zero);

        // Make the picture interactable
        rockCamPicture.framedPicture = framedPicture;
        rockCamPicture.obj = obj;
        PicturesList.Add(rockCamPicture);
    }

    /**
    * <summary>
    * Action associated with the "Spawn picture" button.
    * Summons a new picture from the stash, and adds it to the album.
    * </summary>
    */
    public static void SpawnPicture()
    {
        if (animationRunning || mailTubePicture is not null)
        {
            // if the mail tube is still busy, ignore the button press
            return;
        }
        // reload the stash in case other pictures were added to the folder
        reloadStash();
        stashJson = (JArray)root[currentScene]["stash"];
        if (stashJson is not null && stashJson.Count == 0)
        {
            LogWarn("No pictures in stash, cannot spawn new picture");
            return;
        }
        // start the full mail tube animation in a coroutine in order to not block the main thread
        MelonCoroutines.Start(RunMailTubeAnimation());
    }

    /**
    * <summary>
    * Sets the visibility of the preview slab in the mail tube.
    * </summary>
    */
    private static void SetPreviewSlabVisibility(bool visible)
    {
        if (purchaseSlab is not null)
        {
            purchaseSlab.GetChild(0).gameObject.SetActive(visible);
            purchaseSlab.GetChild(1).gameObject.SetActive(visible);
            purchaseSlab.GetChild(2).gameObject.SetActive(visible);
        }
    }

    /**
    * <summary>
    * Delivers the first picture from the stash via the mail tube,
    * </summary>
    */
    private static IEnumerator<WaitForSeconds> RunMailTubeAnimation()
    {
        FramedPicture framedPicture = new FramedPicture();
        framedPicture.path = stashJson[0].ToString(); // first image in stash
        stashJson.RemoveAt(0); // remove it from the stash

        // Create the json object that will be used to save the config
        mailTubePicture = new PictureData();
        mailTubePicture.jsonConfig = new JObject();
        mailTubePicture.jsonConfig["path"] = framedPicture.path;

        // The spawned picture will use the default size and color
        framedPicture.padding = defaultPadding;
        framedPicture.thickness = defaultThickness;
        framedPicture.color = defaultColor;
        framedPicture.rotation = new Vector3(0, 180, 0);
        GameObject obj = CreatePictureBlock(framedPicture, mailTubeHandle.transform);

        // Make the picture interactable
        mailTubePicture.framedPicture = framedPicture;
        mailTubePicture.obj = obj;
        PicturesList.Add(mailTubePicture);

        // Start the built-in animation of the mail tube
        animationRunning = true;
        mailTube.ExecuteMailTubeAnimation();

        // make the preview slab invisible
        SetPreviewSlabVisibility(false);

        // wait 7 seconds for the animation to put the picture in a nice plase,
        // then stop its movement, so it can easily be grabbed.
        yield return new WaitForSeconds(7f);
        if (mailTubePicture is not null)
        {
            mailTubePicture.obj.transform.SetParent(photoAlbum.transform, true);
            mailTubePicture.obj.transform.localScale = Vector3.one; // ensure proper scale
        }

        // wait 2 seconds to re-enable the slab, so that it's back in the mail tube.
        yield return new WaitForSeconds(2f);
        SetPreviewSlabVisibility(true);
        animationRunning = false;
    }

    /**
     * <summary>
     * Harmony patch that catches the moment a photo is taken, and resets the index of the next printed photo.
     * </summary>
     */
    [HarmonyPatch(typeof(LCKTabletUtility), "TakePhoto", new Type[] { })]
    public static class PhotoTakenPatch
    {
        private static bool Prefix(ref LCKTabletUtility __instance)
        {
            PhotoPrintingIndex = 0;
            return true;
        }
    }
}
