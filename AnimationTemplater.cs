#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SocialPlatforms;


public class AnimationTemplater : EditorWindow
{
	private TextAsset template;
	private AnimatorController controller;

	private Vector2 scrollPosition;
	
	private SDict<string, List<string>> clipNames;
	private SDict<string, List<AnimationClip>> replaceClips;
	private SDict<string, SDict<string, HashSet<AnimatorState>>> clipsToStates;
	private Dictionary<string, System> systems;

	private Dictionary<string, Func<AnimationClip, AnimationClip>> clipTransformations =
		new Dictionary<string, Func<AnimationClip, AnimationClip>>()
		{
			{"humanoid", TranformHumanoid}
		};

	private class System
	{
		public System(string name, TemplateData.SIDict1 animationTypeLists)
		{
			this.name = name;
			this.animationTypeLists = animationTypeLists;
		}

		public string name;
		public TemplateData.SIDict1 animationTypeLists;
	}

	[MenuItem("Tools/Animation Templater")]
	static void Init()
	{
		// Get existing open window or if none, make a new one:
		AnimationTemplater window = (AnimationTemplater)GetWindow(typeof(AnimationTemplater));
		window.titleContent = new GUIContent("Animation Templater");
		window.Show();
		EnsureFolderExists("jellejurre/AnimationScripts/Generated/Animations");
	}

	public static void EnsureFolderExists(string folder)
	{
		string[] folders = folder.Split('/');
		for (var i = 0; i < folders.Length; i++)
		{
			string currentPath = folders.Take(i+1).Aggregate("Assets", (x, y) => x + "/" +  y);
			string old = folders.Take(i).Aggregate("Assets", (x, y) => x + "/" +  y);

			if (!AssetDatabase.IsValidFolder(currentPath))
			{
				AssetDatabase.CreateFolder(old, folders[i]);
			}
		}
	}

	private void OnGUI()
	{
		if (template == null)
		{
			template = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("Template"), template, typeof(TextAsset));
			return;
		}
		
		if (systems == null)
		{
			InitSystems();
		}
		
		AnimatorController oldController = controller;
		controller = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("Controller"), controller, typeof(AnimatorController), false);

		if (oldController != controller || GUILayout.Button("Update Animation List"))
		{
			InitSystems();
			UpdateClips();
		}
		
		foreach (var keyValuePair in clipNames)
		{
			if (keyValuePair.Value == null || keyValuePair.Value.Count == 0)
			{
				clipNames.Remove(keyValuePair.Key);
			}
		}

		EditorGUILayout.LabelField(new GUIContent("Found Animations:"));
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

		foreach (var keyValuePair in clipNames)
		{
			string systemName = keyValuePair.Key;
			EditorGUILayout.LabelField(systemName);
			List<string> systemClips = clipNames[systemName];
			for (var index = 0; index < systemClips.Count; index++)
			{
				var clipName = systemClips[index];
				EditorGUILayout.BeginHorizontal();
				EditorGUILayout.LabelField(clipName, new[] { GUILayout.Width(150) });
				EditorGUILayout.LabelField("->", new[] { GUILayout.Width(20) });
				replaceClips[systemName][index] = (AnimationClip)EditorGUILayout.ObjectField(replaceClips[systemName][index] , typeof(AnimationClip), true);
				EditorGUILayout.EndHorizontal();
			}
		}

		EditorGUILayout.EndScrollView();
		
		if (GUILayout.Button("Convert"))
		{
			UpdateClips();
			ReplaceClips();
			UpdateClips();
		}
	}
	
	[Serializable]
	public class TemplateData
	{
		[Serializable]
		public class SIDict : SDict<string, SIDict1> { };
		[Serializable]
		public class SIDict1 : SDict<string, SIDict2> { };
		[Serializable]
		public class SIDict2 : SDict<string, string> { };

		[field: SerializeField] public SIDict Systems;
	}
	
	public class SDict<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
	{
		[SerializeField, HideInInspector]
		private List<TKey> keyData = new List<TKey>();
	
		[SerializeField, HideInInspector]
		private List<TValue> valueData = new List<TValue>();

		public static SDict<TKey, TValue> SerializeDict(Dictionary<TKey, TValue> d)
		{
			SDict<TKey, TValue> val = new SDict<TKey, TValue>();
			foreach (var keyValuePair in d)
			{
				val.Add(keyValuePair.Key, keyValuePair.Value);
			}
			return val;
		}

		void ISerializationCallbackReceiver.OnAfterDeserialize()
		{
			this.Clear();
			for (int i = 0; i < this.keyData.Count && i < this.valueData.Count; i++)
			{
				this[this.keyData[i]] = this.valueData[i];
			}
		}

		void ISerializationCallbackReceiver.OnBeforeSerialize()
		{
			this.keyData.Clear();
			this.valueData.Clear();

			foreach (var item in this)
			{
				this.keyData.Add(item.Key);
				this.valueData.Add(item.Value);
			}
		}
	}
	
	public void InitSystems()
	{
		TemplateData test = JsonUtility.FromJson<TemplateData>(template.text);
		systems = test.Systems.Select(x => new System(x.Key, x.Value)).ToDictionary(x => x.name, x => x);
		clipNames = SDict<string, List<string>>.SerializeDict(systems
			.Select(system => (system.Key, system.Value
				.animationTypeLists
				.SelectMany(templateList => templateList
					.Value
					.Keys
					.ToList())
				.Distinct()
				.ToList()))
			.ToDictionary(x => x.Key, x => x.Item2));
		replaceClips = SDict<string, List<AnimationClip>>.SerializeDict(clipNames
			.ToDictionary(x => x.Key,
				x => x.Value.Select<string, AnimationClip>(y => null).ToList()));
	}
	
	private void UpdateClips()
	{
		if (controller == null)
		{
			return;
		}
		
		clipsToStates = SDict<string, SDict<string, HashSet<AnimatorState>>>.SerializeDict(systems
			.Select(system => (system.Key, system
				.Value
				.animationTypeLists
				.SelectMany(templateList => templateList
					.Value
					.Values
					.ToList())
				.ToList()))
			.ToDictionary(x => x.Key, x => SDict<string, HashSet<AnimatorState>>.SerializeDict(x
				.Item2
				.Select(y => (y, new HashSet<AnimatorState>()))
				.ToDictionary(y => y.y, y => y.Item2))));
		
		foreach (AnimatorControllerLayer layer in controller.layers)
		{
			foreach (var clipsToState in clipsToStates)
			{
				if (layer.name.Contains(clipsToState.Key))
				{
					SDict<string,HashSet<AnimatorState>> SDict = clipsToState.Value;
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
								HashSet<AnimatorState> states;
								if (SDict.TryGetValue(animationClip.name, out states))
								{
									states.Add(childAnimatorState.state);
								}
							}
						}
					}
				}
			}
		}
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
		foreach (KeyValuePair<string, List<AnimationClip>> system in replaceClips)
		{
			for (int index = 0; index < system.Value.Count; index++)
			{
				AnimationClip replaceClip = system.Value[index];
				if (replaceClip != null)
				{
					string oldClipName = clipNames[system.Key][index];
					System subsystem = systems[system.Key];
					foreach (KeyValuePair<string,TemplateData.SIDict2> subsystemAnimationTypeList in subsystem.animationTypeLists)
					{
						string animationType = subsystemAnimationTypeList.Key;
						TemplateData.SIDict2 newNames = subsystemAnimationTypeList.Value;
						if (newNames.ContainsKey(oldClipName))
						{
							string newName = newNames[oldClipName];
							AnimationClip newClip = TransformAnimation(replaceClip, animationType);
							HashSet<AnimatorState> animatorStates = clipsToStates[system.Key][newName];
							foreach (var animatorState in animatorStates)
							{
								ReplaceClipsInState(animatorState, newName, newClip);
							}
						}

					}
				}
				system.Value[index] = null;
			}
		}
		AssetDatabase.SaveAssets();
	}

	public AnimationClip TransformAnimation(AnimationClip clip, string transform)
	{
		if (transform == "noEdit")
		{
			return clip;
		}
		Func<AnimationClip, AnimationClip> transformFunc = clipTransformations[transform];
		AnimationClip newClip = transformFunc(clip);
		newClip.name = clip.name + transform;
		int index = 0;
		while (AssetDatabase.FindAssets(newClip.name.Replace("|", "").Replace("\\", "").Replace("/", "").Replace(" ", "") + index, new[] { "Assets/jellejurre/AnimationScripts/Generated/Animations" }).Length > 0)
		{
			index++;
		}
		AssetDatabase.CreateAsset(newClip, 
			"Assets/jellejurre/AnimationScripts/Generated/Animations/" +
			newClip.name.Replace("|", "").Replace("\\", "").Replace("/", "").Replace(" ", "") + index
			+ ".anim");
		return newClip;
	}

	public void ReplaceClipsInState(AnimatorState state, string oldClip, AnimationClip replaceClip)
	{
		if (state.motion is AnimationClip c && c.name == oldClip)
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
					if (treeChildren[i].motion is AnimationClip c2 && c2.name == oldClip)
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
	
	
	public static AnimationClip TranformHumanoid(AnimationClip clip)
	{
		AnimationClip newClip = Instantiate(clip);
		EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(newClip).Concat(AnimationUtility.GetObjectReferenceCurveBindings(newClip)).ToArray();
		foreach (EditorCurveBinding binding in bindings)
		{
			if (binding.type != typeof(Animator))
			{
				AnimationUtility.SetEditorCurve(newClip, binding, null);
			}
		}
		return newClip;
	}

	public static TValue GetOrAdd<TKey, TValue>(SDict<TKey, TValue> SDict, TKey key, TValue value)
	{
		if (!SDict.TryGetValue(key, out var r))
			SDict.Add(key, r = value);
		return r;
	}
}

#endif