#if UNITY_EDITOR

using Units;
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
        _effectIdProperty = serializedObject.FindProperty("_effectId");

        _alignmentProperty = serializedObject.FindProperty("_alignment");

        _durationTypeProperty = serializedObject.FindProperty("_durationType");

        _durationProperty = serializedObject.FindProperty("_duration");

        _stackTypeProperty = serializedObject.FindProperty("_stackType");

        _maxStackProperty = serializedObject.FindProperty("_maxStack");

        _actionsProperty = serializedObject.FindProperty("_actions");
    }


    // ============================================================
    // Inspector
    // ============================================================

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        Units.Editor.SkillInspectorUI.Header("상태 효과", "지속 시간·중첩 규칙과 적용할 효과를 설정합니다.");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawIdentity();

            if (Units.Editor.SkillInspectorUI.Foldout(serializedObject, "categories", "분류 태그"))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_categories"), true);

                EditorGUILayout.LabelField("비어 있으면 기존 설정에서 분류를 추론합니다.", EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space(6);

            DrawDuration();

            DrawStack();
        }

        EditorGUILayout.Space(6);

        EditorGUILayout.HelpBox("상태이상은 적용 효과에 '상태 부여'를 추가하고 Enum에서 선택합니다. 서로 다른 ID도 같은 상태로 판정됩니다. 주기 피해는 별도로 추가합니다. 상태 면역 대상에는 이 효과 전체가 적용되지 않습니다.", MessageType.Info);
        DrawActions();

        Units.Editor.SkillInspectorUI.Commit(serializedObject);

        Units.Editor.SkillInspectorUI.Diagnostics(serializedObject);

        if (Units.Editor.SkillInspectorUI.Foldout(serializedObject, "projectCheck", "프로젝트 검사"))
        {
            if (GUILayout.Button("전체 EffectId 중복 검사"))
            {
                var errors = Units.Editor.SkillAuthoringGUI.ValidateEffectIds();

                if (errors.Count == 0)
                    Debug.Log("EffectId 검사 통과");

                foreach (var error in errors)
                    Debug.LogError(error);
            }
        }
    }


    // ============================================================
    // Identity
    // ============================================================

    private void DrawIdentity()
    {
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_effectIdProperty);

        EditorGUILayout.PropertyField(_alignmentProperty);
    }


    // ============================================================
    // Duration
    // ============================================================

    private void DrawDuration()
    {
        EditorGUILayout.LabelField("Duration", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_durationTypeProperty);

        EffectDurationType durationType = (EffectDurationType)_durationTypeProperty.enumValueIndex;

        if (durationType != EffectDurationType.Infinite)
        {
            EditorGUILayout.PropertyField(_durationProperty);
        }
    }


    // ============================================================
    // Stack
    // ============================================================

    private void DrawStack()
    {
        EditorGUILayout.LabelField("Stack", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(_stackTypeProperty);

        EffectStackType stackType = (EffectStackType)_stackTypeProperty.enumValueIndex;

        if (stackType == EffectStackType.Stack)
        {
            EditorGUILayout.PropertyField(_maxStackProperty);
        }
    }


    // ============================================================
    // Actions
    // ============================================================

    private void DrawActions()
    {
        Units.Editor.SkillInspectorUI.Cards(_actionsProperty, (element, _) => DrawAction(element),
            ShowAddActionMenu, "적용 효과");
    }

    private void ShowAddActionMenu()
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("능력치 변경"), false, () => AddAction(new StatEffectActionData()));

        menu.AddItem(new GUIContent("상태 부여"), false, () => AddAction(new StatusEffectActionData()));

        if (HasPeriodicAction())
        {
            menu.AddDisabledItem(new GUIContent("주기 피해 (주기 효과는 1개만 가능)"));

            menu.AddDisabledItem(new GUIContent("주기 회복 (주기 효과는 1개만 가능)"));
        }
        else
        {
            menu.AddItem(new GUIContent("주기 피해"), false, () => AddAction(new PeriodicDamageEffectActionData()));

            menu.AddItem(new GUIContent("주기 회복"), false, () => AddAction(new PeriodicHealEffectActionData()));
        }

        menu.ShowAsContext();
    }

    private void DrawActionList()
    {
        for (int i = 0; i < _actionsProperty.arraySize; i++)
        {
            SerializedProperty actionProperty = _actionsProperty.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            DrawActionHeader(actionProperty, i);

            if (actionProperty.managedReferenceValue != null)
            {
                DrawAction(actionProperty);
            }
            else
            {
                EditorGUILayout.HelpBox("Action 데이터가 비어 있습니다.", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(3f);
        }
    }

    private void DrawAction(SerializedProperty actionProperty)
    {
        object action = actionProperty.managedReferenceValue;

        switch (action)
        {
            case StatEffectActionData:
                DrawStatAction(actionProperty);

                break;

            case PeriodicHealEffectActionData:
                DrawPeriodicHealAction(actionProperty);

                break;

            case PeriodicDamageEffectActionData:
                DrawPeriodicDamageAction(actionProperty);

                break;

            case StatusEffectActionData:
                DrawStatusAction(actionProperty);

                break;

            default:
                EditorGUILayout.PropertyField(
                    actionProperty,
                    GUIContent.none,
                    true
                );

                break;
        }
    }

    private void DrawActionHeader(
        SerializedProperty actionProperty,
        int index)
    {
        object action = actionProperty.managedReferenceValue;

        string actionName = action != null ? action.GetType().Name : "Missing Action";

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(actionName, EditorStyles.boldLabel);

        if (GUILayout.Button("Remove", GUILayout.Width(70f)))
        {
            RemoveAction(index);
        }

        EditorGUILayout.EndHorizontal();
    }


    // ============================================================
    // Stat Action
    // ============================================================

    private void DrawStatAction(SerializedProperty action)
    {
        SerializedProperty statType = action.FindPropertyRelative("_statType");

        SerializedProperty modifierType = action.FindPropertyRelative("_modifierType");

        SerializedProperty value = action.FindPropertyRelative("_value");

        EditorGUILayout.PropertyField(statType);

        EditorGUILayout.PropertyField(modifierType);

        UnitStatModifierType type = (UnitStatModifierType)modifierType.enumValueIndex;

        switch (type)
        {
            case UnitStatModifierType.Flat:
                DrawFlatValueField(
                    value,
                    "Value",
                    -100000000f,
                    100000000f
                );

                break;

            case UnitStatModifierType.Percent:
                DrawPercentageValueField(
                    value,
                    "Value",
                    -1f,
                    10f
                );

                break;
        }
    }


    // ============================================================
    // Status Action
    // ============================================================

    private void DrawStatusAction(SerializedProperty action)
    {
        SerializedProperty statusType = action.FindPropertyRelative("_statusType");

        EditorGUILayout.PropertyField(statusType);
    }


    // ============================================================
    // Periodic Damage Action
    // ============================================================

    private void DrawPeriodicDamageAction(SerializedProperty action)
    {
        SerializedProperty interval = action.FindPropertyRelative("_interval");

        SerializedProperty damage = action.FindPropertyRelative("_damage");

        DrawFloatValueField(
            interval,
            "Interval",
            0.01f,
            float.MaxValue
        );

        DrawFloatValueField(
            damage,
            "Damage",
            0f,
            100000000f
        );
    }


    // ============================================================
    // Periodic Heal Action
    // ============================================================

    private void DrawPeriodicHealAction(SerializedProperty action)
    {
        SerializedProperty interval = action.FindPropertyRelative("_interval");

        SerializedProperty healRatio = action.FindPropertyRelative("_healRatio");

        DrawFloatValueField(
            interval,
            "Interval",
            0.01f,
            float.MaxValue
        );

        DrawPercentageValueField(
            healRatio,
            "Heal Ratio",
            0f,
            10f
        );
    }


    // ============================================================
    // Value Fields
    // ============================================================

    private void DrawFloatValueField(
        SerializedProperty property,
        string label,
        float minValue,
        float maxValue)
    {
        EditorGUI.BeginChangeCheck();

        float nextValue = EditorGUILayout.FloatField(label, property.floatValue);

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        property.floatValue = Mathf.Clamp(
            nextValue,
            minValue,
            maxValue
        );
    }

    private void DrawFlatValueField(
        SerializedProperty property,
        string label,
        float minValue,
        float maxValue)
    {
        DrawFloatValueField(
            property,
            label,
            minValue,
            maxValue
        );
    }

    private void DrawPercentageValueField(
        SerializedProperty property,
        string label,
        float minValue,
        float maxValue)
    {
        float displayValue = property.floatValue * 100f;

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.PrefixLabel(label);

        float nextDisplayValue = EditorGUILayout.FloatField(displayValue);

        GUILayout.Label("%", GUILayout.Width(15f));

        EditorGUILayout.EndHorizontal();

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        float nextValue = nextDisplayValue / 100f;

        property.floatValue = Mathf.Clamp(
            nextValue,
            minValue,
            maxValue
        );
    }


    // ============================================================
    // Add Action
    // ============================================================

    private void DrawAddActionButtons()
    {
        bool hasPeriodicAction = HasPeriodicAction();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add Stat"))
        {
            AddAction(new StatEffectActionData());
        }

        if (GUILayout.Button("Add Status"))
        {
            AddAction(new StatusEffectActionData());
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        using (new EditorGUI.DisabledScope(hasPeriodicAction))
        {
            if (GUILayout.Button("Add Periodic Damage"))
            {
                AddAction(new PeriodicDamageEffectActionData());
            }

            if (GUILayout.Button("Add Periodic Heal"))
            {
                AddAction(new PeriodicHealEffectActionData());
            }
        }

        EditorGUILayout.EndHorizontal();

        if (hasPeriodicAction)
        {
            EditorGUILayout.HelpBox("하나의 EffectData에는 Periodic Action을 하나만 사용할 수 있습니다.", MessageType.Info);
        }
    }

    private void AddAction(EffectActionData action)
    {
        if (action == null)
            return;

        if (IsPeriodicAction(action) && HasPeriodicAction())
        {
            Debug.LogWarning("[EffectDataEditor] 하나의 EffectData에는 Periodic Action을 하나만 추가할 수 있습니다.");

            return;
        }

        Units.Editor.SkillInspectorUI.Add(_actionsProperty, action);
    }


    // ============================================================
    // Remove Action
    // ============================================================

    private void RemoveAction(int index)
    {
        if (index < 0 || index >= _actionsProperty.arraySize)
        {
            return;
        }

        SerializedProperty actionProperty = _actionsProperty.GetArrayElementAtIndex(index);

        actionProperty.managedReferenceValue = null;

        _actionsProperty.DeleteArrayElementAtIndex(index);

        serializedObject.ApplyModifiedProperties();
    }


    // ============================================================
    // Periodic Validation
    // ============================================================

    private bool HasPeriodicAction()
    {
        for (int i = 0; i < _actionsProperty.arraySize; i++)
        {
            SerializedProperty actionProperty = _actionsProperty.GetArrayElementAtIndex(i);

            object action = actionProperty.managedReferenceValue;

            if (IsPeriodicAction(action))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPeriodicAction(object action)
    {
        return action is PeriodicDamageEffectActionData or PeriodicHealEffectActionData;
    }
}

#endif