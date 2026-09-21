using System;
using UnityEditor;
using UnityEngine;
using Units.Skills;


namespace Units.Editor
{
    [CustomEditor(typeof(PassiveSkillData))]
    public class PassiveSkillDataEditor : UnityEditor.Editor
    {
        // ============================================================
        // Properties
        // ============================================================

        private SerializedProperty _triggerType;
        private SerializedProperty _tickInterval;

        private SerializedProperty _conditions;

        private SerializedProperty _effectMode;

        private SerializedProperty _actions;


        // ============================================================
        // Foldouts
        // ============================================================

        private bool _triggerFoldout = true;
        private bool _conditionsFoldout = true;
        private bool _modeFoldout = true;
        private bool _actionsFoldout = true;


        // ============================================================
        // Initialize
        // ============================================================

        private void OnEnable()
        {
            _triggerType =
                serializedObject.FindProperty(
                    "_triggerType"
                );

            _tickInterval =
                serializedObject.FindProperty(
                    "_tickInterval"
                );

            _conditions =
                serializedObject.FindProperty(
                    "_conditions"
                );

            _effectMode =
                serializedObject.FindProperty(
                    "_effectMode"
                );

            _actions =
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


            DrawTriggerSection();

            EditorGUILayout.Space();

            DrawConditionsSection();

            EditorGUILayout.Space();

            DrawModeSection();

            EditorGUILayout.Space();

            DrawActionsSection();


            serializedObject.ApplyModifiedProperties();
        }


        // ============================================================
        // Trigger
        // ============================================================

        private void DrawTriggerSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _triggerFoldout =
                EditorGUILayout.Foldout(
                    _triggerFoldout,
                    "Trigger",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_triggerFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _triggerType
                );


                PassiveSkillTriggerType triggerType =
                    (PassiveSkillTriggerType)
                    _triggerType.enumValueIndex;


                if (triggerType ==
                    PassiveSkillTriggerType.Tick)
                {
                    EditorGUILayout.PropertyField(
                        _tickInterval
                    );
                }


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Conditions
        // ============================================================

        private void DrawConditionsSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _conditionsFoldout =
                EditorGUILayout.Foldout(
                    _conditionsFoldout,
                    "Conditions",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_conditionsFoldout)
            {
                EditorGUILayout.Space(2f);


                for (int i = 0;
                     i < _conditions.arraySize;
                     i++)
                {
                    DrawConditionElement(
                        i
                    );

                    EditorGUILayout.Space(2f);
                }


                if (GUILayout.Button(
                        "Add Condition"))
                {
                    ShowConditionMenu();
                }
            }


            EditorGUILayout.EndVertical();
        }


        private void DrawConditionElement(
            int index)
        {
            SerializedProperty element =
                _conditions.GetArrayElementAtIndex(
                    index
                );


            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            EditorGUILayout.BeginHorizontal();


            EditorGUILayout.LabelField(
                GetManagedReferenceTypeName(
                    element
                ),
                EditorStyles.boldLabel
            );


            if (GUILayout.Button(
                    "Remove",
                    GUILayout.Width(70f)))
            {
                RemoveManagedReferenceElement(
                    _conditions,
                    index
                );

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                return;
            }


            EditorGUILayout.EndHorizontal();


            if (element.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;

                if (element.managedReferenceValue
                    is PassiveRuntimeEffectConditionData)
                {
                    DrawRuntimeEffectCondition(
                        element
                    );
                }
                else
                {
                    EditorGUILayout.PropertyField(
                        element,
                        GUIContent.none,
                        true
                    );
                }

                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        private void ShowConditionMenu()
        {
            GenericMenu menu =
                new();


            menu.AddItem(
                new GUIContent(
                    "Always"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _conditions,
                        new PassiveAlwaysConditionData()
                    );
                }
            );


            menu.AddItem(
                new GUIContent(
                    "Health"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _conditions,
                        new PassiveHealthConditionData()
                    );
                }
            );


            menu.AddItem(
                new GUIContent(
                    "Unit Count"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _conditions,
                        new PassiveUnitCountConditionData()
                    );
                }
            );


            menu.AddItem(
                new GUIContent(
                    "Runtime Effect"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _conditions,
                        new PassiveRuntimeEffectConditionData()
                    );
                }
            );


            menu.ShowAsContext();
        }


        // ============================================================
        // Runtime Effect Condition
        // ============================================================

        private void DrawRuntimeEffectCondition(
            SerializedProperty condition)
        {
            SerializedProperty subjectType =
                condition.FindPropertyRelative(
                    "_subjectType"
                );

            SerializedProperty conditionType =
                condition.FindPropertyRelative(
                    "_conditionType"
                );

            SerializedProperty effectId =
                condition.FindPropertyRelative(
                    "_effectId"
                );

            SerializedProperty statusType =
                condition.FindPropertyRelative(
                    "_statusType"
                );

            SerializedProperty shouldExist =
                condition.FindPropertyRelative(
                    "_shouldExist"
                );


            EditorGUILayout.LabelField(
                "Subject",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                subjectType
            );


            EditorGUILayout.Space(2f);


            EditorGUILayout.LabelField(
                "Effect",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                conditionType
            );


            PassiveRuntimeEffectConditionType type =
                (PassiveRuntimeEffectConditionType)
                conditionType.enumValueIndex;


            switch (type)
            {
                case PassiveRuntimeEffectConditionType.Effect:

                    EditorGUILayout.PropertyField(
                        effectId
                    );

                    break;


                case PassiveRuntimeEffectConditionType.Status:

                    EditorGUILayout.PropertyField(
                        statusType
                    );

                    break;
            }


            EditorGUILayout.Space(2f);


            EditorGUILayout.LabelField(
                "Condition",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                shouldExist
            );
        }


        // ============================================================
        // Mode
        // ============================================================

        private void DrawModeSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _modeFoldout =
                EditorGUILayout.Foldout(
                    _modeFoldout,
                    "Mode",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_modeFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _effectMode
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Actions
        // ============================================================

        private void DrawActionsSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _actionsFoldout =
                EditorGUILayout.Foldout(
                    _actionsFoldout,
                    "Actions",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_actionsFoldout)
            {
                EditorGUILayout.Space(2f);


                for (int i = 0;
                     i < _actions.arraySize;
                     i++)
                {
                    DrawActionElement(
                        i
                    );

                    EditorGUILayout.Space(2f);
                }


                if (GUILayout.Button(
                        "Add Action"))
                {
                    ShowActionMenu();
                }
            }


            EditorGUILayout.EndVertical();
        }


        private void DrawActionElement(
            int index)
        {
            SerializedProperty element =
                _actions.GetArrayElementAtIndex(
                    index
                );


            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            EditorGUILayout.BeginHorizontal();


            EditorGUILayout.LabelField(
                GetManagedReferenceTypeName(
                    element
                ),
                EditorStyles.boldLabel
            );


            if (GUILayout.Button(
                    "Remove",
                    GUILayout.Width(70f)))
            {
                RemoveManagedReferenceElement(
                    _actions,
                    index
                );

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                return;
            }


            EditorGUILayout.EndHorizontal();


            if (element.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;


                switch (element.managedReferenceValue)
                {
                    case PassiveEffectActionData:

                        DrawPassiveEffectAction(
                            element
                        );

                        break;


                    case PassiveStatModifierActionData:

                        DrawPassiveStatModifierAction(
                            element
                        );

                        break;


                    case PassiveDamageValueModifierActionData:

                        DrawPassiveDamageValueModifierAction(
                            element
                        );

                        break;


                    case PassiveCriticalModifierActionData:

                        DrawPassiveCriticalModifierAction(
                            element
                        );

                        break;


                    case PassiveDefenseModifierActionData:

                        DrawPassiveDefenseModifierAction(
                            element
                        );

                        break;


                    default:

                        EditorGUILayout.PropertyField(
                            element,
                            GUIContent.none,
                            true
                        );

                        break;
                }


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        private void ShowActionMenu()
        {
            GenericMenu menu =
                new();


            menu.AddItem(
                new GUIContent(
                    "Effect"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _actions,
                        new PassiveEffectActionData()
                    );
                }
            );


            menu.AddItem(
                new GUIContent(
                    "Stat Modifier"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _actions,
                        new PassiveStatModifierActionData()
                    );
                }
            );


            menu.AddSeparator(
                "Damage Modifier/"
            );


            menu.AddItem(
                new GUIContent(
                    "Damage Modifier/Damage Value"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _actions,
                        new PassiveDamageValueModifierActionData()
                    );
                }
            );


            menu.AddItem(
                new GUIContent(
                    "Damage Modifier/Critical"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _actions,
                        new PassiveCriticalModifierActionData()
                    );
                }
            );


            menu.AddItem(
                new GUIContent(
                    "Damage Modifier/Defense"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        _actions,
                        new PassiveDefenseModifierActionData()
                    );
                }
            );


            menu.ShowAsContext();
        }


        // ============================================================
        // Stat Modifier Action
        // ============================================================

        private void DrawPassiveStatModifierAction(
            SerializedProperty action)
        {
            SerializedProperty statType =
                action.FindPropertyRelative(
                    "_statType"
                );

            SerializedProperty modifierType =
                action.FindPropertyRelative(
                    "_modifierType"
                );

            SerializedProperty value =
                action.FindPropertyRelative(
                    "_value"
                );


            EditorGUILayout.LabelField(
                "Stat",
                EditorStyles.boldLabel
            );


            EditorGUILayout.PropertyField(
                statType
            );

            EditorGUILayout.PropertyField(
                modifierType
            );


            UnitStatModifierType type =
                (UnitStatModifierType)
                modifierType.enumValueIndex;


            switch (type)
            {
                case UnitStatModifierType.Flat:

                    DrawFlatValueField(
                        value,
                        -100000000f,
                        100000000f
                    );

                    break;


                case UnitStatModifierType.Percent:

                    DrawPercentageValueField(
                        value,
                        -1f,
                        10f
                    );

                    break;
            }
        }


        // ============================================================
        // Damage Value Modifier Action
        // ============================================================

        private void DrawPassiveDamageValueModifierAction(
            SerializedProperty action)
        {
            SerializedProperty ownerType =
                action.FindPropertyRelative(
                    "_ownerType"
                );

            SerializedProperty modifierType =
                action.FindPropertyRelative(
                    "_modifierType"
                );

            SerializedProperty value =
                action.FindPropertyRelative(
                    "_value"
                );


            EditorGUILayout.LabelField(
                "Owner",
                EditorStyles.boldLabel
            );


            EditorGUILayout.PropertyField(
                ownerType
            );


            EditorGUILayout.Space(2f);


            EditorGUILayout.LabelField(
                "Modifier",
                EditorStyles.boldLabel
            );


            EditorGUILayout.PropertyField(
                modifierType
            );


            UnitStatModifierType type =
                (UnitStatModifierType)
                modifierType.enumValueIndex;


            switch (type)
            {
                case UnitStatModifierType.Flat:

                    DrawFlatValueField(
                        value,
                        -100000000f,
                        100000000f
                    );

                    break;


                case UnitStatModifierType.Percent:

                    DrawPercentageValueField(
                        value,
                        -1f,
                        10f
                    );

                    break;
            }
        }


        // ============================================================
        // Critical Modifier Action
        // ============================================================

        private void DrawPassiveCriticalModifierAction(
            SerializedProperty action)
        {
            SerializedProperty ownerType =
                action.FindPropertyRelative(
                    "_ownerType"
                );

            SerializedProperty modifierType =
                action.FindPropertyRelative(
                    "_modifierType"
                );

            SerializedProperty value =
                action.FindPropertyRelative(
                    "_value"
                );


            EditorGUILayout.LabelField(
                "Owner",
                EditorStyles.boldLabel
            );


            EditorGUILayout.PropertyField(
                ownerType
            );


            EditorGUILayout.Space(2f);


            EditorGUILayout.LabelField(
                "Critical",
                EditorStyles.boldLabel
            );


            EditorGUILayout.PropertyField(
                modifierType
            );


            PassiveCriticalModifierType type =
                (PassiveCriticalModifierType)
                modifierType.enumValueIndex;


            switch (type)
            {
                case PassiveCriticalModifierType.Chance:

                    DrawPercentageValueField(
                        value,
                        -1f,
                        1f
                    );

                    break;


                case PassiveCriticalModifierType.Damage:

                    DrawPercentageValueField(
                        value,
                        -10f,
                        10f
                    );

                    break;
            }
        }


        // ============================================================
        // Defense Modifier Action
        // ============================================================

        private void DrawPassiveDefenseModifierAction(
            SerializedProperty action)
        {
            SerializedProperty ownerType =
                action.FindPropertyRelative(
                    "_ownerType"
                );

            SerializedProperty value =
                action.FindPropertyRelative(
                    "_value"
                );


            EditorGUILayout.LabelField(
                "Owner",
                EditorStyles.boldLabel
            );


            EditorGUILayout.PropertyField(
                ownerType
            );


            EditorGUILayout.Space(2f);


            EditorGUILayout.LabelField(
                "Modifier",
                EditorStyles.boldLabel
            );


            DrawPercentageValueField(
                value,
                -1f,
                1f
            );
        }


        // ============================================================
        // Modifier Value
        // ============================================================

        private void DrawFlatValueField(
            SerializedProperty value,
            float minValue,
            float maxValue)
        {
            EditorGUI.BeginChangeCheck();


            float nextValue =
                EditorGUILayout.FloatField(
                    "Value",
                    value.floatValue
                );


            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }


            value.floatValue =
                Mathf.Clamp(
                    nextValue,
                    minValue,
                    maxValue
                );
        }


        private void DrawPercentageValueField(
            SerializedProperty value,
            float minValue,
            float maxValue)
        {
            float displayValue =
                value.floatValue * 100f;


            EditorGUI.BeginChangeCheck();


            EditorGUILayout.BeginHorizontal();


            EditorGUILayout.PrefixLabel(
                "Value"
            );


            float nextDisplayValue =
                EditorGUILayout.FloatField(
                    displayValue
                );


            GUILayout.Label(
                "%",
                GUILayout.Width(15f)
            );


            EditorGUILayout.EndHorizontal();


            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }


            float nextValue =
                nextDisplayValue / 100f;


            value.floatValue =
                Mathf.Clamp(
                    nextValue,
                    minValue,
                    maxValue
                );
        }


        // ============================================================
        // Passive Effect Action
        // ============================================================

        private void DrawPassiveEffectAction(
            SerializedProperty action)
        {
            SerializedProperty targetType =
                action.FindPropertyRelative(
                    "_targetType"
                );

            SerializedProperty targetRelation =
                action.FindPropertyRelative(
                    "_targetRelation"
                );

            SerializedProperty maxEffectTargetCount =
                action.FindPropertyRelative(
                    "_maxEffectTargetCount"
                );

            SerializedProperty areaType =
                action.FindPropertyRelative(
                    "_areaType"
                );

            SerializedProperty areaRadius =
                action.FindPropertyRelative(
                    "_areaRadius"
                );

            SerializedProperty areaAngle =
                action.FindPropertyRelative(
                    "_areaAngle"
                );

            SerializedProperty effects =
                action.FindPropertyRelative(
                    "_effects"
                );


            EditorGUILayout.LabelField(
                "Target",
                EditorStyles.boldLabel
            );

            EditorGUILayout.PropertyField(
                targetType
            );


            PassiveSkillTargetType passiveTargetType =
                (PassiveSkillTargetType)
                targetType.enumValueIndex;


            if (passiveTargetType ==
                PassiveSkillTargetType.Search)
            {
                EditorGUILayout.PropertyField(
                    targetRelation
                );


                EditorGUILayout.Space(2f);


                EditorGUILayout.LabelField(
                    "Target Count",
                    EditorStyles.boldLabel
                );

                EditorGUILayout.PropertyField(
                    maxEffectTargetCount
                );


                EditorGUILayout.Space(2f);


                EditorGUILayout.LabelField(
                    "Area",
                    EditorStyles.boldLabel
                );

                EditorGUILayout.PropertyField(
                    areaType
                );


                PassiveSkillAreaType passiveAreaType =
                    (PassiveSkillAreaType)
                    areaType.enumValueIndex;


                if (passiveAreaType !=
                    PassiveSkillAreaType.Single)
                {
                    EditorGUILayout.PropertyField(
                        areaRadius
                    );
                }


                if (passiveAreaType ==
                    PassiveSkillAreaType.Cone)
                {
                    EditorGUILayout.PropertyField(
                        areaAngle
                    );
                }
            }


            EditorGUILayout.Space(4f);


            DrawSkillEffects(
                effects
            );
        }


        // ============================================================
        // Skill Effects
        // ============================================================

        private void DrawSkillEffects(
            SerializedProperty effects)
        {
            EditorGUILayout.LabelField(
                "Effects",
                EditorStyles.boldLabel
            );


            if (effects == null)
            {
                EditorGUILayout.HelpBox(
                    "Effects Property를 찾을 수 없습니다.",
                    MessageType.Error
                );

                return;
            }


            for (int i = 0;
                 i < effects.arraySize;
                 i++)
            {
                DrawSkillEffectElement(
                    effects,
                    i
                );

                EditorGUILayout.Space(2f);
            }


            if (GUILayout.Button(
                    "Add Effect"))
            {
                ShowSkillEffectMenu(
                    effects
                );
            }
        }


        private void DrawSkillEffectElement(
            SerializedProperty effects,
            int index)
        {
            SerializedProperty element =
                effects.GetArrayElementAtIndex(
                    index
                );


            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            EditorGUILayout.BeginHorizontal();


            EditorGUILayout.LabelField(
                GetManagedReferenceTypeName(
                    element
                ),
                EditorStyles.boldLabel
            );


            if (GUILayout.Button(
                    "Remove",
                    GUILayout.Width(70f)))
            {
                RemoveManagedReferenceElement(
                    effects,
                    index
                );

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                return;
            }


            EditorGUILayout.EndHorizontal();


            if (element.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    element,
                    GUIContent.none,
                    true
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        private void ShowSkillEffectMenu(
            SerializedProperty effects)
        {
            GenericMenu menu =
                new();


            menu.AddItem(
                new GUIContent(
                    "Damage"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        effects,
                        new SkillDamageEffectData()
                    );
                }
            );


            menu.AddItem(
                new GUIContent(
                    "Heal"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        effects,
                        new SkillHealEffectData()
                    );
                }
            );


            menu.AddItem(
                new GUIContent(
                    "Runtime Effect"
                ),
                false,
                () =>
                {
                    AddManagedReferenceElement(
                        effects,
                        new SkillRuntimeEffectData()
                    );
                }
            );


            menu.ShowAsContext();
        }


        // ============================================================
        // Managed Reference
        // ============================================================

        private void AddManagedReferenceElement(
            SerializedProperty list,
            object value)
        {
            serializedObject.Update();


            int index =
                list.arraySize;

            list.InsertArrayElementAtIndex(
                index
            );


            SerializedProperty element =
                list.GetArrayElementAtIndex(
                    index
                );

            element.managedReferenceValue =
                value;


            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(
                target
            );
        }


        private void RemoveManagedReferenceElement(
            SerializedProperty list,
            int index)
        {
            serializedObject.Update();


            SerializedProperty element =
                list.GetArrayElementAtIndex(
                    index
                );

            element.managedReferenceValue =
                null;

            list.DeleteArrayElementAtIndex(
                index
            );


            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(
                target
            );
        }


        // ============================================================
        // Display
        // ============================================================

        private string GetManagedReferenceTypeName(
            SerializedProperty property)
        {
            object value =
                property.managedReferenceValue;


            if (value == null)
                return "None";


            string typeName =
                value.GetType().Name;


            return ObjectNames.NicifyVariableName(
                RemoveSuffix(
                    typeName
                )
            );
        }


        private string RemoveSuffix(
            string typeName)
        {
            const string conditionSuffix =
                "ConditionData";

            const string actionSuffix =
                "ActionData";

            const string effectSuffix =
                "EffectData";


            if (typeName.EndsWith(
                    conditionSuffix,
                    StringComparison.Ordinal))
            {
                return typeName.Substring(
                    0,
                    typeName.Length -
                    conditionSuffix.Length
                );
            }


            if (typeName.EndsWith(
                    actionSuffix,
                    StringComparison.Ordinal))
            {
                return typeName.Substring(
                    0,
                    typeName.Length -
                    actionSuffix.Length
                );
            }


            if (typeName.EndsWith(
                    effectSuffix,
                    StringComparison.Ordinal))
            {
                return typeName.Substring(
                    0,
                    typeName.Length -
                    effectSuffix.Length
                );
            }


            return typeName;
        }
    }
}