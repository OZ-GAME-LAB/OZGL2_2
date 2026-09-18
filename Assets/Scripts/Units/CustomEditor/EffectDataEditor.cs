#if UNITY_EDITOR

using Units.Effects;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(EffectData))]
public class EffectDataEditor : Editor
{
    // ============================================================
    // Serialized Properties
    // ============================================================

    private SerializedProperty _effectIdProperty;

    private SerializedProperty _alignmentProperty;

    private SerializedProperty _durationTypeProperty;

    private SerializedProperty _durationProperty;

    private SerializedProperty _stackTypeProperty;

    private SerializedProperty _maxStackProperty;

    private SerializedProperty _actionsProperty;


    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void OnEnable()
    {
        _effectIdProperty =
            serializedObject.FindProperty(
                "_effectId"
            );

        _alignmentProperty =
            serializedObject.FindProperty(
                "_alignment"
            );

        _durationTypeProperty =
            serializedObject.FindProperty(
                "_durationType"
            );

        _durationProperty =
            serializedObject.FindProperty(
                "_duration"
            );

        _stackTypeProperty =
            serializedObject.FindProperty(
                "_stackType"
            );

        _maxStackProperty =
            serializedObject.FindProperty(
                "_maxStack"
            );

        _actionsProperty =
            serializedObject.FindProperty(
                "_actions"
            );
    }


    // ============================================================
    // Inspector
    // ============================================================

    public override void OnInspectorGUI()
    {
        serializedObject.Update();


        DrawIdentity();

        EditorGUILayout.Space(
            10f
        );


        DrawDuration();

        EditorGUILayout.Space(
            10f
        );


        DrawStack();

        EditorGUILayout.Space(
            10f
        );


        DrawActions();


        serializedObject.ApplyModifiedProperties();
    }


    // ============================================================
    // Identity
    // ============================================================

    private void DrawIdentity()
    {
        EditorGUILayout.LabelField(
            "Identity",
            EditorStyles.boldLabel
        );


        EditorGUILayout.PropertyField(
            _effectIdProperty
        );

        EditorGUILayout.PropertyField(
            _alignmentProperty
        );
    }


    // ============================================================
    // Duration
    // ============================================================

    private void DrawDuration()
    {
        EditorGUILayout.LabelField(
            "Duration",
            EditorStyles.boldLabel
        );


        EditorGUILayout.PropertyField(
            _durationTypeProperty
        );


        EffectDurationType durationType =
            (EffectDurationType)
            _durationTypeProperty.enumValueIndex;


        if (durationType !=
            EffectDurationType.Infinite)
        {
            EditorGUILayout.PropertyField(
                _durationProperty
            );
        }
    }


    // ============================================================
    // Stack
    // ============================================================

    private void DrawStack()
    {
        EditorGUILayout.LabelField(
            "Stack",
            EditorStyles.boldLabel
        );


        EditorGUILayout.PropertyField(
            _stackTypeProperty
        );


        EffectStackType stackType =
            (EffectStackType)
            _stackTypeProperty.enumValueIndex;


        if (stackType ==
            EffectStackType.Stack)
        {
            EditorGUILayout.PropertyField(
                _maxStackProperty
            );
        }
    }


    // ============================================================
    // Actions
    // ============================================================

    private void DrawActions()
    {
        EditorGUILayout.LabelField(
            "Actions",
            EditorStyles.boldLabel
        );


        if (_actionsProperty == null)
        {
            EditorGUILayout.HelpBox(
                "Actions SerializedProperty를 찾을 수 없습니다.",
                MessageType.Error
            );

            return;
        }


        DrawActionList();

        EditorGUILayout.Space(
            5f
        );


        DrawAddActionButtons();
    }


    private void DrawActionList()
    {
        for (int i = 0;
             i < _actionsProperty.arraySize;
             i++)
        {
            SerializedProperty actionProperty =
                _actionsProperty.GetArrayElementAtIndex(
                    i
                );


            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            DrawActionHeader(
                actionProperty,
                i
            );


            if (actionProperty.managedReferenceValue != null)
            {
                EditorGUILayout.PropertyField(
                    actionProperty,
                    GUIContent.none,
                    true
                );
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Action 데이터가 비어 있습니다.",
                    MessageType.Warning
                );
            }


            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(
                3f
            );
        }
    }


    private void DrawActionHeader(
        SerializedProperty actionProperty,
        int index)
    {
        object action =
            actionProperty.managedReferenceValue;


        string actionName =
            action != null
                ? action.GetType().Name
                : "Missing Action";


        EditorGUILayout.BeginHorizontal();


        EditorGUILayout.LabelField(
            actionName,
            EditorStyles.boldLabel
        );


        if (GUILayout.Button(
                "Remove",
                GUILayout.Width(
                    70f
                )))
        {
            RemoveAction(
                index
            );
        }


        EditorGUILayout.EndHorizontal();
    }


    // ============================================================
    // Add Action
    // ============================================================

    private void DrawAddActionButtons()
    {
        bool hasPeriodicAction =
            HasPeriodicAction();


        EditorGUILayout.BeginHorizontal();


        if (GUILayout.Button(
                "Add Stat"))
        {
            AddAction(
                new StatEffectActionData()
            );
        }


        if (GUILayout.Button(
                "Add Status"))
        {
            AddAction(
                new StatusEffectActionData()
            );
        }


        EditorGUILayout.EndHorizontal();


        EditorGUILayout.BeginHorizontal();


        using (new EditorGUI.DisabledScope(
                   hasPeriodicAction))
        {
            if (GUILayout.Button(
                    "Add Periodic Damage"))
            {
                AddAction(
                    new PeriodicDamageEffectActionData()
                );
            }


            if (GUILayout.Button(
                    "Add Periodic Heal"))
            {
                AddAction(
                    new PeriodicHealEffectActionData()
                );
            }
        }


        EditorGUILayout.EndHorizontal();


        if (hasPeriodicAction)
        {
            EditorGUILayout.HelpBox(
                "하나의 EffectData에는 Periodic Action을 하나만 사용할 수 있습니다.",
                MessageType.Info
            );
        }
    }


    private void AddAction(
        EffectActionData action)
    {
        if (action == null)
            return;


        if (IsPeriodicAction(
                action) &&
            HasPeriodicAction())
        {
            Debug.LogWarning(
                "[EffectDataEditor] 하나의 EffectData에는 Periodic Action을 하나만 추가할 수 있습니다."
            );

            return;
        }


        int index =
            _actionsProperty.arraySize;


        _actionsProperty.InsertArrayElementAtIndex(
            index
        );


        SerializedProperty actionProperty =
            _actionsProperty.GetArrayElementAtIndex(
                index
            );


        actionProperty.managedReferenceValue =
            action;


        serializedObject.ApplyModifiedProperties();
    }


    // ============================================================
    // Remove Action
    // ============================================================

    private void RemoveAction(
        int index)
    {
        if (index < 0 ||
            index >= _actionsProperty.arraySize)
        {
            return;
        }


        _actionsProperty.DeleteArrayElementAtIndex(
            index
        );


        serializedObject.ApplyModifiedProperties();
    }


    // ============================================================
    // Periodic Validation
    // ============================================================

    private bool HasPeriodicAction()
    {
        for (int i = 0;
             i < _actionsProperty.arraySize;
             i++)
        {
            SerializedProperty actionProperty =
                _actionsProperty.GetArrayElementAtIndex(
                    i
                );


            object action =
                actionProperty.managedReferenceValue;


            if (IsPeriodicAction(
                    action))
            {
                return true;
            }
        }


        return false;
    }


    private bool IsPeriodicAction(
        object action)
    {
        return action
            is PeriodicDamageEffectActionData
            or PeriodicHealEffectActionData;
    }
}

#endif