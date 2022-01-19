using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDK3.Avatars.ScriptableObjects;
using ExpressionParameters = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters;
using ExpressionParameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;
using VRCMenu = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu;
using MenuParameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control.Parameter;
using Descriptor = VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using UnityEditor.Animations;
using System;
using System.IO;

public class actionAddition : EditorWindow
{
    //Todo: Create Menus
    private GameObject avatar;
    private AnimatorController ActionController;
    private ExpressionParameters ExpressionParameters;

    private List<AnimationClip> AnimationClipList = new List<AnimationClip>();
    Vector2 scrollPose;

    string folderPath = "";
    string[] files;

    [MenuItem("Yelby/Action Addition")]
    public static void ShowWindow()
    {
        GetWindow<actionAddition>("Action Addition");
    }
    public void OnGUI()
    {
        GUILayout.Label("Version: 1.3");

        avatar = EditorGUILayout.ObjectField("Avatar: ", avatar, typeof(GameObject), true) as GameObject;
        if(avatar != null)
        {
            var SDK = avatar.GetComponent<Descriptor>();
            if(SDK != null)
            {
                //Action Controller
                if (SDK.baseAnimationLayers[3].animatorController != null && SDK.expressionParameters != null)
                {
                    ActionController = (AnimatorController)SDK.baseAnimationLayers[3].animatorController;
                    ExpressionParameters = SDK.expressionParameters;
                    AnimationClipUI();
                }
                else
                {
                    GUILayout.Label("No Action Controller or Expression Parameter");
                    Debug.LogWarning("No Action Controller or Expression Parameter");
                }
            }
        }
    }

    private void AnimationClipUI()
    {
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Retrieve animations"))
            RetrieveAnimations(AnimationClipList, ActionController);
        if (GUILayout.Button("Add Folder"))
            AddFolder();
        GUILayout.EndHorizontal();


        if (AnimationClipList.Count != 0 && AnimationClipList[0] != null)
        {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("Inject Animations"))
            {
                Inject(ActionController);
                EditorUtility.DisplayDialog("Action Addition - Inject", "Finished Injecting Animations", "OK");
            }
            if (GUILayout.Button("Clear List"))
                AnimationClipList = new List<AnimationClip>();
            GUILayout.EndHorizontal();
        }

        ProcessList(AnimationClipList);

        scrollPose = EditorGUILayout.BeginScrollView(scrollPose);
        for (int i = 0; i < AnimationClipList.Count; i++)
            AnimationClipList[i] = EditorGUILayout.ObjectField("" + (i + 1), AnimationClipList[i], typeof(AnimationClip), true) as AnimationClip;
        GUILayout.EndScrollView();
    }

    private void CreateFolders(GameObject avatar)
    {
        string path = "Assets/Yelby/Programs/ActionAddition";
        if (!AssetDatabase.IsValidFolder(path + "/" + avatar.name))
        {
            AssetDatabase.CreateFolder(path, avatar.name);
            path += avatar.name;
            Debug.Log("Folder: " + path + " created");
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private void AddFolder()
    {
        folderPath = "";
        folderPath = EditorUtility.OpenFolderPanel("Folder to Inject Animations", "", "");

        if (folderPath == "")
            return;

        files = Directory.GetFiles(folderPath);

        for(int i = 0; i < files.Length; i++)
        {
            if (!files[i].Contains(".meta") && files[i].Contains(".anim"))
            {
                string assetPath = files[i].Substring(files[i].IndexOf("Assets/")).Replace('/', '\\');
                AnimationClip asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(AnimationClip)) as AnimationClip;
                if(!AnimationClipList.Contains(asset))
                    AnimationClipList.Add(asset);
            }
        }
    }

    private void RetrieveAnimations(List<AnimationClip> list, AnimatorController controller)
    {
        int actionLayerIndex = LayerIndex(controller.layers, "Action");
        if (actionLayerIndex == -1)
        {
            Debug.LogError("Action layer not found");
            return;
        }

        var actionLayer = controller.layers[actionLayerIndex].stateMachine;
        ChildAnimatorState prepareStanding = GetChildAnimatorByName(actionLayer, "Prepare Standing");

        var prepareStandingTransitions = prepareStanding.state.transitions;
        for (int i = 0; i < prepareStandingTransitions.Length; i++)
        {
            if(prepareStandingTransitions[i].destinationState != null)
            {
                if(!list.Contains((AnimationClip)prepareStandingTransitions[i].destinationState.motion))
                    list.Add((AnimationClip)prepareStandingTransitions[i].destinationState.motion);
            }
        }
    }

    private void Inject(AnimatorController controller)
    {
        CreateFolders(avatar);

        int actionLayerIndex = LayerIndex(controller.layers, "Action");
        if (actionLayerIndex == -1)
        {
            Debug.LogError("Action layer not found");
            return;
        }

        var actionLayer = controller.layers[actionLayerIndex].stateMachine;
        ChildAnimatorState waitAFKNode = GetChildAnimatorByName(actionLayer, "WaitForActionOrAFK");
        ChildAnimatorState prepareStanding = GetChildAnimatorByName(actionLayer, "Prepare Standing");

        waitAFKNode.state.transitions[0].conditions = null;
        waitAFKNode.state.transitions[0].AddCondition(AnimatorConditionMode.Greater, 0, "VRCEmote");
        prepareStanding.state.transitions = null;

        ChildAnimatorState blendOutStand = GetChildAnimatorByName(actionLayer, "BlendOut Stand");

        DeleteDefaultAnimationNodes(actionLayer);

        Vector3 location = new Vector3(((prepareStanding.position[0] + blendOutStand.position[0]) / 2), ((prepareStanding.position[1] + blendOutStand.position[1]) / 2));
        
        AnimatorStateMachine subState = CreateSubstate(actionLayer, location);
        FillSubState(subState, prepareStanding, blendOutStand);
    }

    private void FillSubState(AnimatorStateMachine subState, ChildAnimatorState prepareStanding, ChildAnimatorState blendOutStand)
    {
        Vector3 location = new Vector3(100, 0);
        List<VRCMenu> menuList = new List<VRCMenu>();
        List<VRCMenu.Control> controlMenuList = new List<VRCMenu.Control>();
        List<VRCMenu.Control> controlList = new List<VRCMenu.Control>();
        MenuParameter parameter = new MenuParameter();
        parameter.name = "VRCEmote";
        for(int i = 0; i < AnimationClipList.Count - 1; i++)
        {
            if (i % 8 == 0)
            {
                location[0] += 250;
                location[1] = 0;
            }

            var tempState = createState(AnimationClipList[i], subState, location);
            location[1] += 50;

            createTransition(prepareStanding.state, tempState, false, 0.0f, AnimatorConditionMode.Equals, (i + 1));
            createTransition(tempState, blendOutStand.state, false, 0.25f, AnimatorConditionMode.NotEqual, (i + 1));
            VRCMenu.Control control = new VRCMenu.Control();
            control.name = AnimationClipList[i].name;
            control.parameter = parameter;
            control.value = i + 1;
            control.type = VRCMenu.Control.ControlType.Toggle;

            controlList.Add(control);
        }

        int menuCount = 0;

        for(int i = 0; i < controlList.Count; i++)
        {
            controlMenuList.Add(controlList[i]);

            if(controlMenuList.Count == 8)
            {
                menuCount++;
                VRCMenu temp = CreateInstance<VRCMenu>();
                temp.name = "Animation " + menuCount;
                temp.controls = controlMenuList;
                menuList.Add(temp);
                controlMenuList = new List<VRCMenu.Control>();
            }
            else if (controlMenuList.Count < 8 && controlList.Count - i < 2)
            {
                menuCount++;
                VRCMenu temp = CreateInstance<VRCMenu>();
                temp.name = "Animation " + menuCount;
                temp.controls = controlMenuList;
                menuList.Add(temp);
            }
        }

        Debug.Log("Size: " + menuList.Count);
        for(int i = 0; i < menuList.Count; i++)
        {   
            AssetDatabase.CreateAsset(menuList[i], "Assets/Yelby/Programs/ActionAddition/" + avatar.name + "/" + menuList[i].name + ".asset");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private AnimatorStateMachine GetSubStateByName(AnimatorStateMachine actionLayer)
    {
        var stateMachines = actionLayer.stateMachines;
        for (int i = 0; i < stateMachines.Length; i++)
        {
            if (stateMachines[i].stateMachine.name == "New Actions")
                return stateMachines[i].stateMachine;
        }

        return null;
    }

    private AnimatorStateMachine CreateSubstate(AnimatorStateMachine stateMachine, Vector3 location)
    {
        var subMachines = stateMachine.stateMachines;
        string stateMachineName = "New Actions";
        int i;
        if (subMachines.Length != 0)
            for (i = 0; i < subMachines.Length; i++)
                if (subMachines[i].stateMachine.name == stateMachineName)
                {
                    stateMachine.RemoveStateMachine(subMachines[i].stateMachine);
                    break;
                }

        stateMachine.AddStateMachine(stateMachineName, location);
        for(i = 0; i < stateMachine.stateMachines.Length; i++)
        {
            if(stateMachine.stateMachines[i].stateMachine.name == stateMachineName)
            {
                Vector3 subLocation = new Vector3(0, 0);
                stateMachine.stateMachines[i].stateMachine.entryPosition = subLocation;
                subLocation[1] += 50;
                stateMachine.stateMachines[i].stateMachine.anyStatePosition = subLocation;
                subLocation[1] += 50;
                stateMachine.stateMachines[i].stateMachine.exitPosition = subLocation;
                subLocation[1] += 50;
                stateMachine.stateMachines[i].stateMachine.parentStateMachinePosition = subLocation;
                break;
            }
        }

        return stateMachine.stateMachines[i].stateMachine;
    }

    private void DeleteDefaultAnimationNodes(AnimatorStateMachine stateMachine)
    {
        var states = stateMachine.states;
        string[] deleteList = { "stand_wave", "stand_clap_loop", "stand_point", "stand_cheer_loop", "dance_loop", "backflip", "sadkick", "die_hold", "getup_from_back" };
        for (int i = 0; i < deleteList.Length; i++)
        {
            int index = GetChildAnimatorByNameIndex(stateMachine, deleteList[i]);
            if (index == -1)
                continue;
            else
                states[index].state = null;
        }
        stateMachine.states = states;
    }

    private void ProcessList(List<AnimationClip> list)
    {
        if (list.Count == 0)
            list.Add(default);

        if (list[list.Count - 1] != null)
            list.Add(default);

        for(int i = 0; i < list.Count; i++)
            if(list[i] == null && i < list.Count - 1)
                list.RemoveAt(i);
    }

    private int LayerIndex(AnimatorControllerLayer[] layers, string layerName)
    {
        for (int i = 0; i < layers.Length; i++)
            if (layers[i].name == layerName)
                return i;
        return -1;
    }
    private ChildAnimatorState GetChildAnimatorByName(AnimatorStateMachine actionLayer, string nodeName)
    {
        var states = actionLayer.states;

        for (int i = 0; i < states.Length; i++)
        {
            if(states[i].state.name == nodeName)
                return states[i];
        }

        return states[0];
    }

    private int GetChildAnimatorByNameIndex(AnimatorStateMachine actionLayer, string nodeName)
    {
        var states = actionLayer.states;

        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state.name == nodeName)
                return i;
        }

        return -1;
    }

    private AnimatorState createState(Motion motion, AnimatorStateMachine stateMachine, Vector3 location)
    {
        stateMachine.AddState(motion.name, location);
        int i = 0;
        for (i = 0; i < stateMachine.states.Length; i++)
        {
            if (stateMachine.states[i].state.name == motion.name)
            {
                stateMachine.states[i].state.motion = motion;
                stateMachine.states[i].state.writeDefaultValues = false;
                break;
            }
        }
        return stateMachine.states[i].state;
    }

    private void createTransition(AnimatorState start, AnimatorState end, bool exitTime, float duration, AnimatorConditionMode mode, float threshold)
    {
        AnimatorStateTransition transition = start.AddTransition(end);
        transition.hasExitTime = exitTime;
        transition.duration = duration;
        transition.AddCondition(mode, threshold, "VRCEmote");
    }
}
