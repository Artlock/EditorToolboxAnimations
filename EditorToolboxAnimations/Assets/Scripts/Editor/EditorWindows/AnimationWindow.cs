using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;

public class AnimationWindow : EditorWindow
{
    private bool isInitialized = false;

    private List<Animator> animators = null; // Animators in scene
    private List<AnimationClip> animations = null; // Animations for selected animator

    private Animator selectedAnimator = null;
    private AnimationClip selectedAnimation = null;

    private bool isPlaying = false;
    private float editorLastTime;

    [MenuItem("Toolbox/Animations")] // Appears at the top under toolbox
    static void InitWindow()
    {
        EditorWindow window = GetWindow<AnimationWindow>();

        window.autoRepaintOnSceneChange = true;
        window.Show();
        window.titleContent = new GUIContent("Toolbox Animations");
    }

    private void OnGUI()
    {
        GUIAnimatorsDropDown();
    }

    private void OnHierarchyChange()
    {
        UpdateAll();
    }

    private void OnEnable()
    {
        SetupAll();

        EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
        EditorSceneManager.sceneClosed += EditorSceneManager_sceneClosed;
        EditorSceneManager.sceneOpened += EditorSceneManager_sceneOpened;
    }

    private void OnDisable()
    {
        ResetAll();

        EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
        EditorSceneManager.sceneClosed -= EditorSceneManager_sceneClosed;
        EditorSceneManager.sceneOpened -= EditorSceneManager_sceneOpened;
    }

    private void SetupAll()
    {
        if (!Application.isEditor) return;

        animators = FindAnimatorsInScene();
        animations = new List<AnimationClip>();

        isInitialized = true;
    }

    private void UpdateAll()
    {
        if (!Application.isEditor || !isInitialized) return;

        // Get everything in the scene and check if there is a change

        List<Animator> newAnimators = FindAnimatorsInScene();

        // If there is a change we replace everything

        if (!animators.SequenceEqual(newAnimators))
        {
            Debug.Log("Hierarchy update : Animators changed");

            animators = newAnimators;
        }

        if (!animators.Contains(selectedAnimator))
        {
            Debug.Log("Hierarchy update : Currently selected animator lost");

            StopAnim();

            selectedAnimator = null;
            selectedAnimation = null;
        }
        else if (!animations.Contains(selectedAnimation))
        {
            Debug.Log("Hierarchy update : Currently selected animation lost");

            StopAnim();

            selectedAnimation = null;
        }

        if (selectedAnimator != null)
        {
            animations = FindAnimationClipsInAnimator(selectedAnimator);
        }
        else
        {
            animations = new List<AnimationClip>();
        }
    }

    private void ResetAll()
    {
        if (!Application.isEditor) return;

        StopAnim();

        isInitialized = false;

        animators = null;
        animations = null;
    }

    private void GUIAnimatorsDropDown()
    {
        if (Application.isPlaying)
        {
            GUILayout.Label("Unavailable in playmode");

            return;
        }

        // Forced to do this because
        // When an animator that is selected is deleted
        // OnGUI happens before OnHierarchyChange
        // Trying to access destroyed animators
        List<Animator> guiAnimators = animators.Where(x => x != null).ToList();

        if (guiAnimators.Count == 0)
        {
            GUILayout.Label("No animators in scene");

            return;
        }

        Animator initiallySelectedAnimator = selectedAnimator;
        int selectedAnimatorIndex = 0;

        if (selectedAnimator != null)
        {
            selectedAnimatorIndex = EditorGUILayout.Popup(guiAnimators.IndexOf(selectedAnimator), guiAnimators.Select(x => x.gameObject.name + " - " + x.runtimeAnimatorController.name).ToArray());
        }
        else
        {
            selectedAnimatorIndex = EditorGUILayout.Popup(0, guiAnimators.Select(x => x.gameObject.name + " - " + x.runtimeAnimatorController.name).ToArray());
        }

        selectedAnimator = guiAnimators[selectedAnimatorIndex];

        if (selectedAnimator != initiallySelectedAnimator)
        {
            StopAnim();

            animations = FindAnimationClipsInAnimator(selectedAnimator);

            selectedAnimation = null;
        }

        if (animations.Count == 0)
        {
            GUILayout.Label("No animation clips in animator controller");

            return;
        }

        AnimationClip initiallySelectedAnimationClip = selectedAnimation;
        int selectedAnimationClipIndex = 0;

        if (selectedAnimation != null)
        {
            selectedAnimationClipIndex = EditorGUILayout.Popup(animations.IndexOf(selectedAnimation), animations.Select(x => x.name).ToArray());
        }
        else
        {
            selectedAnimationClipIndex = EditorGUILayout.Popup(0, animations.Select(x => x.name).ToArray());
        }

        selectedAnimation = animations[selectedAnimationClipIndex];

        if (isPlaying)
        {
            if (GUILayout.Button("Stop"))
            {
                StopAnim();
                isPlaying = false;
            }
        }
        else
        {
            if (GUILayout.Button("Play"))
            {
                PlayAnim();
                isPlaying = true;
            }
        }
    }

    private void PlayAnim()
    {
        if (isPlaying) return;

        editorLastTime = Time.realtimeSinceStartup;

        EditorApplication.update += EditorApplication_update;
        AnimationMode.StartAnimationMode();

        isPlaying = true;
    }

    private void StopAnim()
    {
        if (!isPlaying) return;

        EditorApplication.update -= EditorApplication_update;
        AnimationMode.StopAnimationMode();

        isPlaying = false;
    }

    private void EditorApplication_update()
    {
        // Failsafe
        if (selectedAnimation == null || selectedAnimator == null)
        {
            StopAnim();

            return;
        }

        float animationTime = Time.realtimeSinceStartup - editorLastTime;
        animationTime %= selectedAnimation.length; // Looping the animation

        AnimationMode.SampleAnimationClip(selectedAnimator.gameObject, selectedAnimation, animationTime);
    }

    private void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
    {
        if (obj == PlayModeStateChange.ExitingEditMode)
        {
            StopAnim();
        }
        else if (obj == PlayModeStateChange.ExitingPlayMode && !isInitialized)
        {
            SetupAll();
        }
    }

    private void EditorSceneManager_sceneClosed(Scene scene)
    {
        if (scene == SceneManager.GetActiveScene())
        {
            ResetAll();
        }
    }

    private void EditorSceneManager_sceneOpened(Scene scene, OpenSceneMode mode)
    {
        // Scene change
        if (scene == SceneManager.GetActiveScene())
        {
            SetupAll();
        }
    }

    private List<Animator> FindAnimatorsInScene()
    {
        List<Animator> animatorsList = new List<Animator>();

        int sceneCount = SceneManager.sceneCount;

        for (int i = 0; i < sceneCount; i++)
        {
            foreach (GameObject rootGa in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                animatorsList.AddRange(rootGa.GetComponentsInChildren<Animator>().Where(x => x.runtimeAnimatorController != null));
            }
        }

        return animatorsList;
    }

    private List<AnimationClip> FindAnimationClipsInAnimator(Animator animator)
    {
        List<AnimationClip> animationClipsList = new List<AnimationClip>();

        AnimatorController editorController = (AnimatorController)animator.runtimeAnimatorController;
        AnimatorControllerLayer controllerLayer = editorController.layers[0];

        foreach (ChildAnimatorState childState in controllerLayer.stateMachine.states)
        {
            AnimationClip animationClip = childState.state.motion as AnimationClip;

            if (animationClip != null)
            {
                animationClipsList.Add(animationClip);
            }
        }

        return animationClipsList;
    }
}
