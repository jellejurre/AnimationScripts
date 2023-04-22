#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;


public class AnimationReplacer : EditorWindow
{
	private AnimatorController controller;

	private Vector2 scrollPosition;
	
	private List<AnimationClip> clips;
	private List<AnimationClip> replaceClips;
	private Dictionary<AnimationClip, HashSet<AnimatorState>> clipsToStates;

	private Vector2 pathScrollPosition;
	
	[MenuItem("Tools/Animation Replacer")]
	static void Init()
	{
		// Get existing open window or if none, make a new one:
		AnimationReplacer window = (AnimationReplacer)GetWindow(typeof(AnimationReplacer));
		window.titleContent = new GUIContent("Animation Replacer");
		window.Show();
	}

	private void OnGUI()
	{
		AnimatorController oldController = controller;
		controller = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("Controller"), controller, typeof(AnimatorController), false);

		clips = clips ?? new List<AnimationClip>();
		replaceClips = replaceClips ?? new List<AnimationClip>();
		clipsToStates = clipsToStates ?? new Dictionary<AnimationClip, HashSet<AnimatorState>>();

		if (oldController != controller || GUILayout.Button("Update Animation List"))
		{
			UpdateClips();
		}
		
		for (var index = 0; index < clips.Count; index++)
		{
			var clip = clips[index];
			if (clip == null)
			{
				clips.RemoveAt(index);
			}
		}

		EditorGUILayout.LabelField(new GUIContent("Found Animations:"));
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

		for (var i = 0; i < clips.Count; i++)
		{
			AnimationClip animatedObject = clips[i];
			AnimationClip replaceObject = replaceClips[i];
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.ObjectField(animatedObject, typeof(GameObject), true);
			EditorGUILayout.LabelField("->", new [] {GUILayout.Width(20)});
			replaceClips[i] = (AnimationClip)EditorGUILayout.ObjectField(replaceObject, typeof(AnimationClip), true);
			// GUILayout.Label("Motion Time: ");
			// EditorGUILayout.FloatField(1, new GUILayoutOption[]{GUILayout.Width(50)});
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.EndScrollView();
		
		if (GUILayout.Button("Convert"))
		{
			UpdateClips();
			ReplaceClips();
			UpdateClips();
		}
	}

	private void UpdateClips()
	{
		if (controller == null)
		{
			return;
		}
		HashSet<AnimationClip> clipsLocal = new HashSet<AnimationClip>();
		clipsToStates = new Dictionary<AnimationClip, HashSet<AnimatorState>>();
		foreach (AnimatorControllerLayer layer in controller.layers)
		{
			Queue<AnimatorStateMachine> stateMachines = new Queue<AnimatorStateMachine>();
			stateMachines.Enqueue(layer.stateMachine);
			while(stateMachines.Count != 0)
			{
				AnimatorStateMachine stateMachine = stateMachines.Dequeue();
				foreach (var childAnimatorStateMachine in stateMachine.stateMachines)
				{
					stateMachines.Enqueue(childAnimatorStateMachine.stateMachine);
				}

				foreach (var childAnimatorState in stateMachine.states)
				{
					List<AnimationClip> clips = ExtractClips(childAnimatorState.state.motion);
					foreach (AnimationClip animationClip in clips)
					{
						clipsLocal.Add(animationClip);
						GetOrAdd(clipsToStates, animationClip, new HashSet<AnimatorState>())
							.Add(childAnimatorState.state);
					}
				}
			}
		}
		clips = clipsLocal.OrderBy(x => x.name).ToList();
		Resize(replaceClips, clips.Count);
	}

	public List<AnimationClip> ExtractClips(Motion m)
	{
		if (m is AnimationClip clip)
		{
			return new List<AnimationClip>() { clip };
		}

		if (m is BlendTree tree)
		{
			ChildMotion[] treeChildren = tree.children;
			List<AnimationClip> clips = new List<AnimationClip>();
			foreach (ChildMotion childMotion in treeChildren)
			{
				clips.AddRange(ExtractClips(childMotion.motion));
			}

			return clips;
		}
		
		return new List<AnimationClip>();
	}
	
	public void ReplaceClips()
	{
		for (var i = 0; i < replaceClips.Count; i++)
		{
			if (clips[i] == null || replaceClips[i] == null)
			{
				continue;
			}
			AnimationClip replaceClip = replaceClips[i];
			AnimationClip oldClip = clips[i];

			HashSet<AnimatorState> replaceStates = clipsToStates[oldClip];
			foreach (var animatorState in replaceStates)
			{
				ReplaceClipsInState(animatorState, oldClip, replaceClip);
			}
		}
		replaceClips = new List<AnimationClip>();
		AssetDatabase.SaveAssets();
	}

	public void ReplaceClipsInState(AnimatorState state, AnimationClip oldClip, AnimationClip replaceClip)
	{
		if (state.motion is AnimationClip c && c == oldClip)
		{
			state.motion = replaceClip;
			return;
		}

		if (state.motion is BlendTree tree)
		{
			Queue<BlendTree> trees = new Queue<BlendTree>();
			trees.Enqueue(tree);
			while (trees.Count != 0)
			{
				BlendTree currentTree = trees.Dequeue();
				ChildMotion[] treeChildren = currentTree.children;
				for (var i = 0; i < treeChildren.Length; i++)
				{
					if (treeChildren[i].motion is AnimationClip c2 && c2 == oldClip)
					{
						treeChildren[i].motion = replaceClip;
					}

					if (treeChildren[i].motion is BlendTree childTree)
					{
						trees.Enqueue(childTree);
					}
				}
				currentTree.children = treeChildren;
			}
		}
	}

	public static TValue GetOrAdd<TKey, TValue>(IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
	{
		if (!dictionary.TryGetValue(key, out var r))
			dictionary.Add(key, r = value);
		return r;
	}
	
	public static void Resize<T>(List<T> list, int size, T element = default(T))
	{
		int count = list.Count;

		if (size < count)
		{
			list.RemoveRange(size, count - size);
		}
		else if (size > count)
		{
			if (size > list.Capacity)   // Optimization
				list.Capacity = size;

			list.AddRange(Enumerable.Repeat(element, size - count));
		}
	}
}
#endif
