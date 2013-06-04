using System;
using UnityEngine;
using MissionController;
using System.Collections.Generic;

/// <summary>
/// Draws the GUI and handles the user input and events (like launch)
/// </summary>
namespace MissionController
{
    public class MissionController : MonoBehaviour
    {
        public static string root = KSPUtil.ApplicationRootPath.Replace ("\\", "/");
        public static string pluginFolder = root + "GameData/MissionController/";
        public static string missionFolder = pluginFolder + "Plugins/PluginData/MissionController";
        private Texture2D menuIcon = null;
    
        private Manager manager {
            get {
                return Manager.instance;
            }
        }

        private List<MissionGoal> hiddenGoals = new List<MissionGoal> ();
    
        private Rect mainWindowPosition = new Rect (300, 70, 300, 500);
        private Rect testWindowPosition = new Rect (Screen.width / 2 - 150, Screen.height / 2 - 100, 300, 150);
        private bool showMainWindow = false;
        private bool showTestVesselWindow = false;
        private FileBrowser fileBrowser = null;
        private Mission currentMission = null;
        private Vector2 scrollPosition = new Vector2 (0, 0);
        private GUIStyle styleCaption;
        private GUIStyle styleText;
        private GUIStyle styleValueGreen;
        private GUIStyle styleValueRed;
        private GUIStyle styleButton;
        private GUIStyle styleValueName;
        private GUIStyle styleIcon;

        private void loadIcons ()
        {
            if (menuIcon == null) {
                menuIcon = new Texture2D (30, 30, TextureFormat.ARGB32, false);
                menuIcon.LoadImage (KSP.IO.File.ReadAllBytes<MissionController> ("icon.png"));
            }
        }
        
        private void loadStyles ()
        {
            styleCaption = new GUIStyle (GUI.skin.label);
            styleCaption.normal.textColor = Color.green;
            styleCaption.fontStyle = FontStyle.Normal;
            styleCaption.alignment = TextAnchor.MiddleLeft;
            
            styleText = new GUIStyle (GUI.skin.label);
            styleText.normal.textColor = Color.white;
            styleText.fontStyle = FontStyle.Normal;
            styleCaption.alignment = TextAnchor.UpperLeft;
            
            styleValueGreen = new GUIStyle (GUI.skin.label);
            styleValueGreen.normal.textColor = Color.green;
            styleValueGreen.fontStyle = FontStyle.Normal;
            styleValueGreen.alignment = TextAnchor.MiddleRight;
            
            styleValueRed = new GUIStyle (GUI.skin.label);
            styleValueRed.normal.textColor = Color.red;
            styleValueRed.fontStyle = FontStyle.Normal;
            styleValueRed.alignment = TextAnchor.MiddleRight;
            
            styleButton = new GUIStyle (GUI.skin.button);
            styleButton.normal.textColor = Color.white;
            styleButton.fontStyle = FontStyle.Bold;
            styleButton.alignment = TextAnchor.MiddleCenter;
            
            styleValueName = new GUIStyle (GUI.skin.label);
            styleValueName.normal.textColor = Color.white;
            styleValueName.fontStyle = FontStyle.Normal;
            styleValueName.alignment = TextAnchor.MiddleLeft;

            styleIcon = new GUIStyle ();
        }

        public void toggleWindow ()
        {
            showMainWindow = !showMainWindow;
        }

        public void Awake ()
        {
            GameEvents.onLaunch.Add (this.onLaunch);
            GameEvents.onVesselChange.Add (this.onVesselChange);
        }

        private void onVesselChange(Vessel v) {
            currentMission = null;
        }
        
        private void onLaunch (EventReport r)
        {
            manager.currentProgram.launch (resources.sum());
            manager.saveProgram ();
        }

        private Vessel vessel {
            get {
                if(HighLogic.LoadedSceneIsFlight) {
                    return FlightGlobals.ActiveVessel;
                } else {
                    return null;
                }
            }
        }

        public void OnGUI ()
        {
            if (!HighLogic.LoadedSceneIsFlight && !HighLogic.LoadedSceneIsEditor) {
                return;
            }
            
            loadIcons ();
            loadStyles ();
            
            if (GUI.Button (new Rect (Screen.width / 6 - 34, Screen.height - 34, 32, 32), menuIcon, styleIcon)) {
                toggleWindow ();
            }
            
            if (showMainWindow) {
                mainWindowPosition = GUILayout.Window (98765, mainWindowPosition, drawMainWindow, "Mission Controller");
            }
            
            if (showTestVesselWindow) {
                testWindowPosition = GUILayout.Window (98764, testWindowPosition, drawTestWindow, "Are you sure?");
            }
            
            if (fileBrowser != null) {
                GUI.skin = HighLogic.Skin;
                GUIStyle list = GUI.skin.FindStyle ("List Item");
                list.normal.textColor = XKCDColors.DarkYellow; 
                list.contentOffset = new Vector2 (1, 0);
                list.fontSize = 20;
                list.alignment = TextAnchor.MiddleLeft;
                
                fileBrowser.OnGUI ();
                
                // Reset Skin
                list.normal.textColor = new Color (0.739f, 0.739f, 0.739f);
                list.contentOffset = new Vector2 (1, 42.4f);
                list.fontSize = 10;
            }
        }

        /// <summary>
        /// Draws the window, that asks the user if he really wants to mark the active vessel as test vessel.
        /// </summary>
        /// <param name="id">Identifier.</param>
        private void drawTestWindow (int id)
        {
            GUI.skin = HighLogic.Skin;
            
            GUILayout.BeginVertical ();
            GUILayout.Label ("Do you want to mark this vessel as test vessel? Test vessel *CAN NOT* complete missions, but cost only half the price!", styleText);
            
            GUILayout.BeginHorizontal ();
            if (GUILayout.Button ("Yes")) {
                showTestVesselWindow = false;
//                isTestVessel = true;
            }
            if (GUILayout.Button ("NO!!!")) {
                showTestVesselWindow = false;
            }
            GUILayout.EndHorizontal ();
            GUILayout.EndVertical ();
            GUI.DragWindow ();
        }

        /// <summary>
        /// Draws the main mission window.
        /// </summary>
        /// <param name="id">Identifier.</param>
        private void drawMainWindow (int id)
        {
            GUI.skin = HighLogic.Skin;
            VesselResources res = resources;
            
            GUILayout.BeginVertical ();
            
            scrollPosition = GUILayout.BeginScrollView (scrollPosition);
            GUILayout.BeginVertical ();
            
            GUILayout.BeginHorizontal ();
            GUILayout.Label ("Current budget: ", styleValueName);
            GUILayout.Label (manager.budget + CurrencySuffix, (manager.currentProgram.money < 0 ? styleValueRed : styleValueGreen));
            GUILayout.EndHorizontal ();

            if (HighLogic.LoadedSceneIsEditor || (vessel != null && vessel.situation == Vessel.Situations.PRELAUNCH)) {
                showCostValue("Construction costs:", res.construction, styleValueGreen);
                showCostValue("Liquid fuel costs:", res.liquid (), styleValueGreen);
                showCostValue("Oxidizer costs:", res.oxidizer (), styleValueGreen);
                showCostValue("Solid fuel costs:", res.solid(), styleValueGreen);
                showCostValue("Other resource costs:", res.other(), styleValueGreen);
                showCostValue("Sum:", res.sum(), (res.sum () > manager.budget ? styleValueRed : styleValueGreen));
            }

            if (currentMission != null) {
                drawMission ();
            }
            
            GUILayout.EndVertical ();
            GUILayout.EndScrollView ();
            
            if (GUILayout.Button ("Select Mission")) {
                createFileBrowser ("Select mission", selectMission);
            }
            
//            if (vessel.situation == Vessel.Situations.LANDED && vessel.orbit.referenceBody.bodyName.Equals ("Kerbin") &&
//                !reusedVessel) {
//                if(GUILayout.Button("Reuse this spacecraft!")) {
//                    manager.currentProgram.reuse(currentCosts);
//                    reusedVessel = true;
//                }
//            }
            
//            if (!isTestVessel) {
//                if(vessel.situation == Vessel.Situations.PRELAUNCH) {
////                    if (GUILayout.Button ("This is a test vessel!")) {
////                        showTestVesselWindow = true;
////                    }
//                }
            if (currentMission != null && currentMission.isDone (vessel) && 
                !manager.isMissionAlreadyFinished (currentMission, vessel)) {
                if (GUILayout.Button ("Finish the mission!")) {
                    manager.finishMission (currentMission, vessel);
                }
            }
//            } else {
//                GUILayout.Label ("THIS IS A TEST VESSEL!", styleCaption);
//            }
            
            GUILayout.EndVertical ();
            GUI.DragWindow ();
        }

        /// <summary>
        /// Selects the mission in the file
        /// </summary>
        /// <param name="file">File.</param>
        private void selectMission (String file)
        {
            fileBrowser = null;
            
            if (file == null) {
                return;
            }
            
            currentMission = manager.loadMission (file, vessel);
            hiddenGoals = new List<MissionGoal> ();
        }

        /// <summary>
        /// Draws the mission parameters
        /// </summary>
        private void drawMission ()
        {
            GUILayout.Label ("Current Mission: ", styleCaption);
            GUILayout.Label (currentMission.name, styleText);
            GUILayout.Label ("Description: ", styleCaption);
            GUILayout.Label (currentMission.description, styleText);
            
            GUILayout.BeginHorizontal ();
            GUILayout.Label ("Reward: ", styleValueName);
            GUILayout.Label (currentMission.reward + CurrencySuffix, styleValueGreen);
            GUILayout.EndHorizontal ();
            
            if (manager.isMissionAlreadyFinished (currentMission, vessel)) {
                GUILayout.Label ("Mission already finished!", styleCaption);
            } else {
                drawMissionGoals (currentMission);

                if(currentMission.isDone(vessel)) {
                    GUILayout.Label("All goals accomplished. You can finish the mission now!", styleCaption);
                }
            }
        }

        /// <summary>
        /// Draws the mission goals
        /// </summary>
        /// <param name="mission">Mission.</param>
        private void drawMissionGoals (Mission mission)
        {
            int index = 1;
            bool orderOk = true;
            foreach (MissionGoal c in mission.goals) {
                if (hiddenGoals.Contains(c)) {
                    index++;
                    continue;
                }

                GUILayout.Label ((index++) + ". Mission goal: " + c.getType () + (c.optional ? " (optional)" : ""), styleCaption);
                
                if (c.description.Length != 0) {
                    GUILayout.Label ("Description: ", styleCaption);
                    GUILayout.Label (c.description, styleText);
                }
                
                if (c.nonPermanent && c.reward != 0) {
                    GUILayout.BeginHorizontal ();
                    GUILayout.Label ("Reward:", styleValueName);
                    GUILayout.Label (c.reward + CurrencySuffix, styleValueGreen);
                    GUILayout.EndHorizontal ();
                }


                List<Value> values = c.getValues (vessel);

                foreach (Value v in values) {
                    GUILayout.BeginHorizontal ();
                    GUILayout.Label (v.name, styleValueName);
                    if(v.currentlyIs.Length == 0) {
                        GUILayout.Label(v.shouldBe, styleValueGreen);
                    } else {
                        GUILayout.Label (v.shouldBe + " : " + v.currentlyIs, (v.done ? styleValueGreen : styleValueRed));
                    }
                    GUILayout.EndHorizontal ();
                }
                    
                if(vessel != null) {
                    if ((c.isDone (vessel) && orderOk) || c.optional) {
                        if (c.nonPermanent && !hiddenGoals.Contains(c)) {
                            if(FlightInputHandler.state.mainThrottle != 0.0 && c.throttleDown) { 
                                GUILayout.Label("Throttle down in order to finish mission goal!", styleCaption);
                            } else {
                                manager.finishMissionGoal (c, vessel);
                                if (GUILayout.Button ("Hide finished goal!")) {
                                    hiddenGoals.Add(c);
                                }
                            }
                        }                    
                    } else {
                        orderOk = false;
                    }
                }
            }
        }
        
        // Create the file browser
        private void createFileBrowser (string title, FileBrowser.FinishedCallback callback)
        {
            fileBrowser = new FileBrowser (new Rect (Screen.width / 2, 100, 350, 500), title, callback, true);
            fileBrowser.BrowserType = FileBrowserType.File;
            fileBrowser.CurrentDirectory = missionFolder;
            fileBrowser.disallowDirectoryChange = true;
            fileBrowser.SelectionPattern = "*.m";
        }

        private VesselResources resources {
            get {
                VesselResources res = new VesselResources ();
                try {
                    List<Part> parts;
                    if(vessel == null) {
                        parts = EditorLogic.SortedShipList;
                    } else {
                        parts = vessel.parts;
                    }

                    foreach (Part p in parts) {
                        res.construction += p.partInfo.cost;

                        if (p.Resources ["LiquidFuel"] != null) {
                            res.liquidFuel += p.Resources ["LiquidFuel"].amount;
                        }

                        if (p.Resources ["SolidFuel"] != null) {
                            res.solidFuel += p.Resources ["SolidFuel"].amount;
                        }

                        if (p.Resources ["MonoPropellant"] != null) {
                            res.monoFuel += p.Resources ["MonoPropellant"].amount;
                        }

                        if (p.Resources ["Oxidizer"] != null) {
                            res.oxidizerFuel += p.Resources ["Oxidizer"].amount;
                        }

                        if (p.Resources ["XenonGas"] != null) {
                            res.xenonFuel += p.Resources ["XenonGas"].amount;
                        }

                        res.mass += p.mass;
                    }
                } catch {
                }
                return res;
            }
        }

        public void OnDestroy() {
            manager.saveProgram ();
        }

        private class VesselResources
        {
            public double liquidFuel;
            public double oxidizerFuel;
            public double solidFuel;
            public double monoFuel;
            public double mass;
            public double xenonFuel;
            public double construction;


            public int liquid() {
                return (int)liquidFuel * 10;
            }
            
            public int mono() {
                return (int)monoFuel * 25;
            }
            
            public int solid() {
                return (int)solidFuel * 5;
            }
            
            public int xenon() {
                return (int)xenonFuel * 50;
            }
            
            public int other() {
                return (int)mass * 1000;
            }

            public int oxidizer() {
                return (int)oxidizerFuel * 2;
            }

            public int sum() {
                return (int)(construction + liquid () + solid () + mono () + xenon () + other () + oxidizer());
            }
        }

        private void showCostValue(String name, double value, GUIStyle style) {
            showStringValue (name, String.Format ("{0:0.##}{1}", value, CurrencySuffix), style);
        }

        private void showDoubleValue(String name, double value, GUIStyle style) {
            showStringValue (name, String.Format ("{0:0.##}", value), style);
        }

        private void showIntValue(String name, int value, GUIStyle style) {
            showStringValue (name, String.Format ("{0}", value), style);
        }

        private void showStringValue(String name, String value, GUIStyle style) {
            GUILayout.BeginHorizontal ();
            GUILayout.Label (name, styleValueName);
            GUILayout.Label (value, styleValueGreen);
            GUILayout.EndHorizontal ();
        }

        private const String CurrencySuffix = " ₭";
    }


    /// <summary>
    /// This code is necessary, so the user doesn't have to add a part to his vessel 
    /// </summary>
    public class MissionControllerTest : KSP.Testing.UnitTest
    {
        public MissionControllerTest () : base()
        {
            I.AddI<MissionController> ("MISSION_CONTROLLER");
        }
    }

    static class I
    {
        private static GameObject _gameObject;
    
        public static T AddI<T> (string name) where T : Component
        {
            if (_gameObject == null) {
                _gameObject = new GameObject (name, typeof(T));
                GameObject.DontDestroyOnLoad (_gameObject);
            
                return _gameObject.GetComponent<T> ();
            } else {
                if (_gameObject.GetComponent<T> () != null)
                    return _gameObject.GetComponent<T> ();
                else
                    return _gameObject.AddComponent<T> ();
            }
        }
    }
}

