#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public class ParameterRenamer : EditorWindow
{
	private VRCAvatarDescriptor avatar;
	private string oldString = "";
	private string newString = "";
	[MenuItem("Tools/Parmeter Renamer")]
	static void Init()
	{
		ParameterRenamer window = (ParameterRenamer)EditorWindow.GetWindow(typeof(ParameterRenamer));
		window.Show();
	}
	
	void OnGUI()
	{
		avatar = (VRCAvatarDescriptor) EditorGUILayout.ObjectField("Avatar", avatar, typeof(VRCAvatarDescriptor), true, Array.Empty<GUILayoutOption>());
		EditorGUILayout.BeginHorizontal();
		GUILayout.Label("Old parameter: ");
		oldString = GUILayout.TextField(oldString);
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.BeginHorizontal();
		GUILayout.Label("New parameter: ");
		newString = GUILayout.TextField(newString);
		EditorGUILayout.EndHorizontal();	
		if (GUILayout.Button("Replace"))
		{
			if (avatar == null)
			{
				Debug.LogError("Select your avatar");
				return;
			}

			try
			{
				AssetDatabase.StartAssetEditing();
				AnimatorController[] controllers = avatar.baseAnimationLayers.Where(x => x.animatorController != null)
					.Select(x => (AnimatorController)x.animatorController).ToArray();
				foreach (var animatorController in controllers)
				{
					var drivers = animatorController.GetBehaviours<VRCAvatarParameterDriver>();
					foreach (var vrcAvatarParameterDriver in drivers)
					{
						foreach (var parameter in vrcAvatarParameterDriver.parameters)
						{
							if (parameter.name == oldString)
							{
								parameter.name = newString;
							}

							if (parameter.source == oldString)
							{
								parameter.source = newString;
							}

							if ((string)parameter.sourceParam == oldString)
							{
								parameter.sourceParam = newString;
							}
						}
					}

					AnimatorControllerParameter[] parameters = animatorController.parameters;
					foreach (var animatorControllerParameter in parameters)
					{
						if (animatorControllerParameter.name == oldString)
						{
							animatorControllerParameter.name = newString;
						}
					}

					animatorController.parameters = parameters;
					Queue<AnimatorStateMachine> machines = new Queue<AnimatorStateMachine>();
					foreach (var animatorControllerLayer in animatorController.layers)
					{
						machines.Enqueue(animatorControllerLayer.stateMachine);
					}

					while (machines.Count != 0)
					{
						AnimatorStateMachine currentMachine = machines.Dequeue();
						var currentMachineEntryTransitions = currentMachine.entryTransitions;
						for (var i = 0; i < currentMachineEntryTransitions.Length; i++)
						{
							currentMachineEntryTransitions[i] = ProcessTransition(currentMachineEntryTransitions[i]);
						}

						currentMachine.entryTransitions = currentMachineEntryTransitions;

						var currentMachineAnyTransitions = currentMachine.anyStateTransitions;
						for (var i = 0; i < currentMachineAnyTransitions.Length; i++)
						{
							currentMachineAnyTransitions[i] = ProcessTransition(currentMachineAnyTransitions[i]);
						}

						currentMachine.anyStateTransitions = currentMachineAnyTransitions;

						var currentMachineStates = currentMachine.states;
						for (var i = 0; i < currentMachineStates.Length; i++)
						{
							var state = currentMachineStates[i];
							var animatorStateTransitions = state.state.transitions;
							for (var j = 0; j < animatorStateTransitions.Length; j++)
							{
								animatorStateTransitions[j] = ProcessTransition(animatorStateTransitions[j]);
							}

							if (state.state.motion is BlendTree tree)
							{
								Queue<BlendTree> trees = new Queue<BlendTree>();
								trees.Enqueue(tree);
								while (trees.Count>0)
								{
									tree = trees.Dequeue();
									if (tree.blendParameter == oldString)
									{
										tree.blendParameter = newString;
									}
									if (tree.blendParameterY == oldString)
									{
										tree.blendParameterY = newString;
									}

									var treeChildren = tree.children;
									for (var index = 0; index < treeChildren.Length; index++)
									{
										var childMotion = treeChildren[index];
										if (childMotion.motion is BlendTree childTree)
										{
											trees.Enqueue(childTree);
										}

										if (childMotion.directBlendParameter == oldString)
										{
											childMotion.directBlendParameter = newString;
										}

										treeChildren[index] = childMotion;
									}

									tree.children = treeChildren;
								}
							}

							state.state.transitions = animatorStateTransitions;
						}

						currentMachine.states = currentMachineStates;

						foreach (var currentMachineStateMachine in currentMachine.stateMachines)
						{
							machines.Enqueue(currentMachineStateMachine.stateMachine);
						}
					}

					EditorUtility.SetDirty(animatorController);
				}

				if (avatar.expressionParameters != null)
				{
					var expressionParametersParameters = avatar.expressionParameters.parameters;
					foreach (var expressionParametersParameter in expressionParametersParameters)
					{
						if (expressionParametersParameter.name == oldString)
						{
							expressionParametersParameter.name = newString;
						}
					}

					avatar.expressionParameters.parameters = expressionParametersParameters;
					EditorUtility.SetDirty(avatar.expressionParameters);
				}

				if (avatar.expressionsMenu != null)
				{
					Queue<VRCExpressionsMenu> menus = new Queue<VRCExpressionsMenu>();
					menus.Enqueue(avatar.expressionsMenu);
					while (menus.Count != 0)
					{
						var current = menus.Dequeue();
						foreach (var currentControl in current.controls)
						{
							if (currentControl.parameter != null && currentControl.parameter.name == oldString)
							{
								currentControl.parameter.name = newString;
							}

							if (currentControl.subParameters != null)
							{
								foreach (var currentControlSubParameter in currentControl.subParameters)
								{
									if (currentControlSubParameter.name == oldString)
									{
										currentControlSubParameter.name = newString;
									}
								}
							}

							if (currentControl.subMenu != null)
							{
								menus.Enqueue(currentControl.subMenu);
							}
						}

						EditorUtility.SetDirty(current);
					}
				}
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
			}
			
			AssetDatabase.SaveAssets();
		}
	}

	public T ProcessTransition<T>(T t) where T : AnimatorTransitionBase
	{
		var animatorConditions = t.conditions;
		for (var index = 0; index < animatorConditions.Length; index++)
		{
			var animatorCondition = animatorConditions[index];
			if (animatorCondition.parameter == oldString)
			{
				animatorCondition.parameter = newString;
			}
			animatorConditions[index] = animatorCondition;
		}

		t.conditions = animatorConditions;
		return t;
	}
}

#endif