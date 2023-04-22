#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;


public class AnimationRepather : EditorWindow
{
	private AnimatorController controller;
	private GameObject avatar;

	private Vector2 scrollPosition;
	
	private List<GameObject> animatedObjects;
	private List<GameObject> replaceObjects;
	private Dictionary<GameObject, HashSet<AnimationClip>> objectsAndClips;

	private Vector2 pathScrollPosition;
	
	private List<string> missingPaths;
	private List<GameObject> replacePathObjects;
	private Dictionary<string, HashSet<AnimationClip>> pathsAndClips;

	// Add menu named "My Window" to the Window menu
	[MenuItem("Tools/Animation Repather")]
	static void Init()
	{
		// Get existing open window or if none, make a new one:
		AnimationRepather window = (AnimationRepather)GetWindow(typeof(AnimationRepather));
		window.Show();
	}

	private void OnGUI()
	{
		AnimatorController oldController = controller;
		controller = (AnimatorController)EditorGUILayout.ObjectField(new GUIContent("Controller"), controller, typeof(AnimatorController), false);
		GameObject oldAvatar = avatar;
		avatar = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Avatar"), avatar, typeof(GameObject), true);

		animatedObjects = animatedObjects ?? new List<GameObject>();
		replaceObjects = replaceObjects ?? new List<GameObject>();
		objectsAndClips = objectsAndClips ?? new Dictionary<GameObject, HashSet<AnimationClip>>();
		
		missingPaths = missingPaths ?? new List<string>();
		replacePathObjects = replacePathObjects ?? new List<GameObject>();
		pathsAndClips = pathsAndClips ?? new Dictionary<string, HashSet<AnimationClip>>();

		if (oldController != controller || oldAvatar != avatar || GUILayout.Button("Update List"))
		{
			UpdateClips();
		}
		
		for (var index = 0; index < animatedObjects.Count; index++)
		{
			var animatedObject = animatedObjects[index];
			if (animatedObject == null)
			{
				animatedObjects.RemoveAt(index);
				replaceObjects.RemoveAt(index);
			}
		}

		EditorGUILayout.LabelField(new GUIContent("Found fields:"));
		
		scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

		for (var i = 0; i < animatedObjects.Count; i++)
		{
			GameObject animatedObject = animatedObjects[i];
			GameObject replaceObject = replaceObjects[i];
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.ObjectField(animatedObject, typeof(GameObject), true);
			EditorGUILayout.LabelField("->", new [] {GUILayout.Width(20)});
			replaceObjects[i] = (GameObject)EditorGUILayout.ObjectField(replaceObject, typeof(GameObject), true);
			EditorGUILayout.EndHorizontal();
		}

		EditorGUILayout.EndScrollView();
		
		EditorGUILayout.LabelField(new GUIContent("Missing fields:"));
		
		pathScrollPosition = EditorGUILayout.BeginScrollView(pathScrollPosition);

		for (var i = 0; i < missingPaths.Count; i++)
		{
			string missingPath = missingPaths[i];
			GameObject replaceObject = replacePathObjects[i];
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(missingPath);
			EditorGUILayout.LabelField("->", new [] {GUILayout.Width(20)});
			replacePathObjects[i] = (GameObject)EditorGUILayout.ObjectField(replaceObject, typeof(GameObject), true);
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
		if (controller == null || avatar == null)
		{
			return;
		}
		HashSet<GameObject> animatedObjectsLocal = new HashSet<GameObject>();
		HashSet<string> animatedPathsLocal = new HashSet<string>();
		objectsAndClips = new Dictionary<GameObject, HashSet<AnimationClip>>();
		foreach (AnimationClip ac in controller.animationClips)
		{
			EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(ac).Concat(AnimationUtility.GetObjectReferenceCurveBindings(ac)).ToArray();
			foreach (var editorCurveBinding in bindings)
			{
				if (editorCurveBinding.path != "")
				{
					Transform transform = avatar.transform.Find(editorCurveBinding.path);
					if (transform != null)
					{
						GameObject gameObject = transform.gameObject;
						animatedObjectsLocal.Add(gameObject);
						HashSet<AnimationClip> clipList = GetOrAdd(objectsAndClips, gameObject,
							new HashSet<AnimationClip>());
						clipList.Add(ac);
					}
					else
					{
						animatedPathsLocal.Add(editorCurveBinding.path);
						HashSet<AnimationClip> clipList = GetOrAdd(pathsAndClips, editorCurveBinding.path,
							new HashSet<AnimationClip>());
						clipList.Add(ac);
					}
				}
			}
		}

		animatedObjects = animatedObjectsLocal.OrderBy(x => x.name).ToList();
		Resize(replaceObjects, animatedObjects.Count);

		missingPaths = animatedPathsLocal.OrderBy(x => x).ToList();
		Resize(replacePathObjects, animatedPathsLocal.Count);
	}

	public void ReplaceClips()
	{
		for (var i = 0; i < replaceObjects.Count; i++)
		{
			GameObject replaceObject = replaceObjects[i];
			if (animatedObjects[i] == null)
			{
				continue;
			}
			string originalPath = AnimationUtility.CalculateTransformPath(animatedObjects[i].transform, avatar.transform);
			if (replaceObject != null)
			{
				string path = AnimationUtility.CalculateTransformPath(replaceObject.transform, avatar.transform);
				HashSet<AnimationClip> clips = objectsAndClips[animatedObjects[i]];
				foreach (AnimationClip animationClip in clips)
				{
					EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(animationClip);
					bindings = bindings.Concat(AnimationUtility.GetObjectReferenceCurveBindings(animationClip)).ToArray();
					for (var j = 0; j < bindings.Length; j++)
					{
						var editorCurveBinding = bindings[j];
						if (editorCurveBinding.path == originalPath)
						{
							if (editorCurveBinding.isPPtrCurve)
							{
								ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(animationClip, editorCurveBinding);
								AnimationUtility.SetObjectReferenceCurve(animationClip, editorCurveBinding, null);
								editorCurveBinding.path = path;
								AnimationUtility.SetObjectReferenceCurve(animationClip, editorCurveBinding, curve);								}
							else
							{
								AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding);
								AnimationUtility.SetEditorCurve(animationClip, editorCurveBinding, null);
								editorCurveBinding.path = path;
								AnimationUtility.SetEditorCurve(animationClip, editorCurveBinding, curve);
							}
						}
					}
				}
			}
		}
		replaceObjects = new List<GameObject>();

		for (var i = 0; i < replacePathObjects.Count; i++)
		{
			GameObject replaceObject = replacePathObjects[i];
			if (missingPaths[i] == null)
			{
				continue;
			}
			if (replaceObject != null)
			{
				string path = AnimationUtility.CalculateTransformPath(replaceObject.transform, avatar.transform);
				HashSet<AnimationClip> clips = pathsAndClips[missingPaths[i]];
				foreach (AnimationClip animationClip in clips)
				{
					EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(animationClip);
					bindings = bindings.Concat(AnimationUtility.GetObjectReferenceCurveBindings(animationClip)).ToArray();
					for (var j = 0; j < bindings.Length; j++)
					{
						var editorCurveBinding = bindings[j];
						if (editorCurveBinding.path == missingPaths[i])
						{
							if (editorCurveBinding.isPPtrCurve)
							{
								ObjectReferenceKeyframe[] curve = AnimationUtility.GetObjectReferenceCurve(animationClip, editorCurveBinding);
								AnimationUtility.SetObjectReferenceCurve(animationClip, editorCurveBinding, null);
								editorCurveBinding.path = path;
								AnimationUtility.SetObjectReferenceCurve(animationClip, editorCurveBinding, curve);								}
							else
							{
								AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, editorCurveBinding);
								AnimationUtility.SetEditorCurve(animationClip, editorCurveBinding, null);
								editorCurveBinding.path = path;
								AnimationUtility.SetEditorCurve(animationClip, editorCurveBinding, curve);
							}
						}
					}
				}
			}
		}
		replacePathObjects = new List<GameObject>();

		AssetDatabase.SaveAssets();
	}
	
	public static string GetGameObjectPath(GameObject obj)
	{
		string path = "/" + obj.name;
		while (obj.transform.parent != null)
		{
			obj = obj.transform.parent.gameObject;
			path = "/" + obj.name + path;
		}
		return path;
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