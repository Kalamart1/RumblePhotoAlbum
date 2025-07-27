using System.Collections.Generic;
using MelonLoader;
using Il2CppRUMBLE.Interactions.InteractionBase;
using Il2CppRUMBLE.Tutorial.MoveLearning;
using Il2CppTMPro;
using Newtonsoft.Json.Linq;
using RumbleModdingAPI;
using UnityEngine;

namespace RumblePhotoAlbum;
public partial class MainClass : MelonMod
{
    private static GameObject AlbumInteractionItems = null;
    private static GameObject gearMarketButton = null;
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
        if (currentScene == "Gym")
        {
            yield return new WaitForSeconds(5f);
            if (AlbumInteractionItems is null)
            {
                initializeGlobals();
            }
            initializeGymObjects();
        }
    }

    /**
    * <summary>
    * Initializes the objects that are to be saved to DontDestroyOnLoad (used in multiple scenes)
    * </summary>
    */
    public static void initializeGlobals()
    {
        AlbumInteractionItems = new GameObject();
        AlbumInteractionItems.name = "AlbumInteractionItems";
        GameObject.DontDestroyOnLoad(AlbumInteractionItems);
        GameObject messageScreen = Calls.GameObjects.Gym.Scene.GymProduction.SubStaticGroupBuildings.GearMarket.MessageScreen.GetGameObject();
        gearMarketButton = GameObject.Instantiate(messageScreen.transform.GetChild(1).gameObject);
        gearMarketButton.name = "gearMarketButton";
        gearMarketButton.SetActive(false);
        gearMarketButton.transform.SetParent(AlbumInteractionItems.transform);
    }

    /**
    * <summary>
    * Initializes the objects that are specific to the Gym scene.
    * </summary>
    */
    public static void initializeGymObjects()
    {
        // reset state variables
        animationRunning = false;
        stashJson = (JArray)root[currentScene]["stash"];
        albumJson = (JArray)root[currentScene]["album"];
        mailTubePicture = null;

        //Get the mail tube object in the gym
        mailTube = Calls.GameObjects.Gym.Scene.GymProduction.SubStaticGroupBuildings.GearMarket.MailTube.GetGameObject().GetComponent<MailTube>();

        // Create a new button on the gear market for spawning pictures
        System.Action action = () => SpawnPicture();
        GameObject spawnButton = NewGearMarketButton("spawnButton", "Spawn picture", action);
        GameObject gearMarket = Calls.GameObjects.Gym.Scene.GymProduction.SubStaticGroupBuildings.GearMarket.GetGameObject();
        spawnButton.transform.SetParent(gearMarket.transform);
        spawnButton.transform.localPosition = new Vector3(0.075f, 1.1f, 0.19f);
        spawnButton.transform.localRotation = Quaternion.Euler(new Vector3(270, 270, 0));

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
    * Creates a copy of the large gear market button.
    * </summary>
    */
    public static GameObject NewGearMarketButton(string name, string text, System.Action action)
    {
        // Copy the object that we saved to DontDestroyOnLoad earlier
        GameObject newButtonGO = GameObject.Instantiate(gearMarketButton);
        newButtonGO.SetActive(true);
        newButtonGO.name = name;
        // onEndInteraction is the moment you release the button
        newButtonGO.transform.GetChild(0).gameObject.GetComponent<InteractionTouch>().onEndInteraction.AddListener(action);
        TextMeshPro buttonText = newButtonGO.transform.GetChild(0).GetChild(3).gameObject.GetComponent<TextMeshPro>();
        buttonText.fontSize = 0.6f;
        buttonText.m_text = text;
        return newButtonGO;
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
}