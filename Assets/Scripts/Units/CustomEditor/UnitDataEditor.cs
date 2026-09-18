using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Units.UnitDatas;


namespace Units.Editor
{
    [CustomEditor(typeof(UnitData))]
    public class UnitDataEditor : UnityEditor.Editor
    {
        // ============================================================
        // Properties
        // ============================================================

        private SerializedProperty _unitId;
        private SerializedProperty _unitName;
        private SerializedProperty _team;

        private SerializedProperty _allyIdentity;
        private SerializedProperty _enemyIdentity;

        private SerializedProperty _stats;

        private SerializedProperty _basicAttackData;
        private SerializedProperty _activeSkillData;


        // ============================================================
        // Foldouts
        // ============================================================

        private bool _basicFoldout = true;
        private bool _identityFoldout = true;
        private bool _statsFoldout = true;
        private bool _combatFoldout = true;


        // ============================================================
        // Initialize
        // ============================================================

        private void OnEnable()
        {
            _unitId =
                serializedObject.FindProperty(
                    "_unitId"
                );

            _unitName =
                serializedObject.FindProperty(
                    "_unitName"
                );

            _team =
                serializedObject.FindProperty(
                    "_team"
                );

            _allyIdentity =
                serializedObject.FindProperty(
                    "_allyIdentity"
                );

            _enemyIdentity =
                serializedObject.FindProperty(
                    "_enemyIdentity"
                );

            _stats =
                serializedObject.FindProperty(
                    "_stats"
                );

            _basicAttackData =
                serializedObject.FindProperty(
                    "_basicAttackData"
                );

            _activeSkillData =
                serializedObject.FindProperty(
                    "_activeSkillData"
                );


            serializedObject.Update();

            SynchronizeStats();

            serializedObject.ApplyModifiedProperties();
        }


        // ============================================================
        // Inspector
        // ============================================================

        public override void OnInspectorGUI()
        {
            serializedObject.Update();


            SynchronizeStats();


            DrawBasicSection();

            EditorGUILayout.Space();

            DrawIdentitySection();

            EditorGUILayout.Space();

            DrawStatsSection();

            EditorGUILayout.Space();

            DrawCombatSection();


            serializedObject.ApplyModifiedProperties();
        }


        // ============================================================
        // Basic
        // ============================================================

        private void DrawBasicSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _basicFoldout =
                EditorGUILayout.Foldout(
                    _basicFoldout,
                    "Basic Data",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_basicFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _unitId
                );

                EditorGUILayout.PropertyField(
                    _unitName
                );

                EditorGUILayout.PropertyField(
                    _team
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Identity
        // ============================================================

        private void DrawIdentitySection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _identityFoldout =
                EditorGUILayout.Foldout(
                    _identityFoldout,
                    "Identity",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_identityFoldout)
            {
                EditorGUI.indentLevel++;


                UnitTeam team =
                    (UnitTeam)_team.intValue;


                switch (team)
                {
                    case UnitTeam.Ally:

                        EditorGUILayout.PropertyField(
                            _allyIdentity,
                            true
                        );

                        break;


                    case UnitTeam.Enemy:

                        EditorGUILayout.PropertyField(
                            _enemyIdentity,
                            true
                        );

                        break;
                }


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Stats
        // ============================================================

        private void DrawStatsSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _statsFoldout =
                EditorGUILayout.Foldout(
                    _statsFoldout,
                    "Stats",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_statsFoldout)
            {
                EditorGUILayout.Space(2f);


                DrawStatsHeader();


                EditorGUILayout.Space(2f);


                for (int i = 0;
                     i < _stats.arraySize;
                     i++)
                {
                    DrawStatElement(
                        i
                    );
                }
            }


            EditorGUILayout.EndVertical();
        }


        private void DrawStatsHeader()
        {
            EditorGUILayout.BeginHorizontal();


            GUILayout.Label(
                "Stat Type",
                EditorStyles.boldLabel
            );


            GUILayout.Label(
                "Value",
                EditorStyles.boldLabel,
                GUILayout.Width(120f)
            );


            EditorGUILayout.EndHorizontal();
        }


        private void DrawStatElement(
            int index)
        {
            SerializedProperty element =
                _stats.GetArrayElementAtIndex(
                    index
                );


            SerializedProperty statType =
                element.FindPropertyRelative(
                    "_statType"
                );


            SerializedProperty value =
                element.FindPropertyRelative(
                    "_value"
                );


            UnitStatType type =
                (UnitStatType)statType.intValue;


            EditorGUILayout.BeginHorizontal();


            EditorGUILayout.LabelField(
                ObjectNames.NicifyVariableName(
                    type.ToString()
                )
            );


            EditorGUILayout.PropertyField(
                value,
                GUIContent.none,
                GUILayout.Width(120f)
            );


            EditorGUILayout.EndHorizontal();
        }


        // ============================================================
        // Stat Synchronization
        // ============================================================

        private void SynchronizeStats()
        {
            UnitStatType[] statTypes =
                (UnitStatType[])Enum.GetValues(
                    typeof(UnitStatType)
                );


            Dictionary<UnitStatType, float> currentValues =
                GetCurrentStatValues();


            if (IsAlreadySynchronized(
                    statTypes))
            {
                return;
            }


            _stats.ClearArray();


            for (int i = 0;
                 i < statTypes.Length;
                 i++)
            {
                UnitStatType statType =
                    statTypes[i];


                _stats.InsertArrayElementAtIndex(
                    i
                );


                SerializedProperty element =
                    _stats.GetArrayElementAtIndex(
                        i
                    );


                SerializedProperty statTypeProperty =
                    element.FindPropertyRelative(
                        "_statType"
                    );


                SerializedProperty valueProperty =
                    element.FindPropertyRelative(
                        "_value"
                    );


                statTypeProperty.intValue =
                    (int)statType;


                if (currentValues.TryGetValue(
                        statType,
                        out float currentValue))
                {
                    valueProperty.floatValue =
                        currentValue;
                }
                else
                {
                    valueProperty.floatValue =
                        0f;
                }
            }
        }


        private Dictionary<UnitStatType, float> GetCurrentStatValues()
        {
            Dictionary<UnitStatType, float> result =
                new();


            for (int i = 0;
                 i < _stats.arraySize;
                 i++)
            {
                SerializedProperty element =
                    _stats.GetArrayElementAtIndex(
                        i
                    );


                SerializedProperty statType =
                    element.FindPropertyRelative(
                        "_statType"
                    );


                SerializedProperty value =
                    element.FindPropertyRelative(
                        "_value"
                    );


                UnitStatType type =
                    (UnitStatType)statType.intValue;


                if (result.ContainsKey(
                        type))
                {
                    continue;
                }


                result.Add(
                    type,
                    value.floatValue
                );
            }


            return result;
        }


        private bool IsAlreadySynchronized(
            UnitStatType[] statTypes)
        {
            if (_stats.arraySize !=
                statTypes.Length)
            {
                return false;
            }


            for (int i = 0;
                 i < statTypes.Length;
                 i++)
            {
                SerializedProperty element =
                    _stats.GetArrayElementAtIndex(
                        i
                    );


                SerializedProperty statType =
                    element.FindPropertyRelative(
                        "_statType"
                    );


                if (statType.intValue !=
                    (int)statTypes[i])
                {
                    return false;
                }
            }


            return true;
        }


        // ============================================================
        // Combat Data
        // ============================================================

        private void DrawCombatSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _combatFoldout =
                EditorGUILayout.Foldout(
                    _combatFoldout,
                    "Combat Data",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_combatFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _basicAttackData
                );

                EditorGUILayout.PropertyField(
                    _activeSkillData
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }
    }
}