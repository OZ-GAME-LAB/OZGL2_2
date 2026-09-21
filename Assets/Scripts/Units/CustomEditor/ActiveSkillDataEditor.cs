using UnityEditor;
using UnityEngine;
using Units.Skills;

namespace Units.Editor
{
    [CustomEditor(typeof(ActiveSkillData))]
    public class ActiveSkillDataEditor : UnityEditor.Editor
    {
        // ============================================================
        // Properties
        // ============================================================

        private SerializedProperty _skillRange;
        private SerializedProperty _skillCooldown;

        private SerializedProperty _targetSide;
        private SerializedProperty _targetPolicy;

        private SerializedProperty _actionType;
        private SerializedProperty _castTime;

        private SerializedProperty _deliveryType;

        private SerializedProperty _maxTargetCount;
        private SerializedProperty _maxEffectTargetCount;

        private SerializedProperty _areaType;
        private SerializedProperty _areaRadius;
        private SerializedProperty _areaAngle;

        private SerializedProperty _effects;

        private SerializedProperty _projectileSpeed;

        private SerializedProperty _dashDistance;
        private SerializedProperty _dashSpeed;

        private SerializedProperty _skillFXType;
        private SerializedProperty _hitFXType;


        // ============================================================
        // Foldouts
        // ============================================================

        private bool _basicFoldout = true;
        private bool _targetFoldout = true;
        private bool _actionFoldout = true;
        private bool _deliveryFoldout = true;
        private bool _areaFoldout = true;
        private bool _effectsFoldout = true;
        private bool _projectileFoldout = true;
        private bool _dashFoldout = true;
        private bool _fxFoldout = true;


        // ============================================================
        // Initialize
        // ============================================================

        private void OnEnable()
        {
            _skillRange =
                serializedObject.FindProperty(
                    "_skillRange"
                );

            _skillCooldown =
                serializedObject.FindProperty(
                    "_skillCooldown"
                );


            _targetSide =
                serializedObject.FindProperty(
                    "_targetSide"
                );

            _targetPolicy =
                serializedObject.FindProperty(
                    "_targetPolicy"
                );


            _actionType =
                serializedObject.FindProperty(
                    "_actionType"
                );

            _castTime =
                serializedObject.FindProperty(
                    "_castTime"
                );


            _deliveryType =
                serializedObject.FindProperty(
                    "_deliveryType"
                );


            _maxTargetCount =
                serializedObject.FindProperty(
                    "_maxTargetCount"
                );

            _maxEffectTargetCount =
                serializedObject.FindProperty(
                    "_maxEffectTargetCount"
                );


            _areaType =
                serializedObject.FindProperty(
                    "_areaType"
                );

            _areaRadius =
                serializedObject.FindProperty(
                    "_areaRadius"
                );

            _areaAngle =
                serializedObject.FindProperty(
                    "_areaAngle"
                );


            _effects =
                serializedObject.FindProperty(
                    "_effects"
                );


            _projectileSpeed =
                serializedObject.FindProperty(
                    "_projectileSpeed"
                );


            _dashDistance =
                serializedObject.FindProperty(
                    "_dashDistance"
                );

            _dashSpeed =
                serializedObject.FindProperty(
                    "_dashSpeed"
                );


            _skillFXType =
                serializedObject.FindProperty(
                    "_skillFXType"
                );

            _hitFXType =
                serializedObject.FindProperty(
                    "_hitFXType"
                );
        }


        // ============================================================
        // Inspector
        // ============================================================

        public override void OnInspectorGUI()
        {
            serializedObject.Update();


            DrawBasicSection();

            EditorGUILayout.Space();

            DrawTargetSection();

            EditorGUILayout.Space();

            DrawActionSection();

            EditorGUILayout.Space();

            DrawDeliverySection();


            if (ShouldDrawAreaSection())
            {
                EditorGUILayout.Space();

                DrawAreaSection();
            }


            EditorGUILayout.Space();

            DrawEffectsSection();


            if (ShouldDrawProjectileSection())
            {
                EditorGUILayout.Space();

                DrawProjectileSection();
            }


            if (ShouldDrawDashSection())
            {
                EditorGUILayout.Space();

                DrawDashSection();
            }


            EditorGUILayout.Space();

            DrawFXSection();


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
                    "Basic",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_basicFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _skillRange
                );

                EditorGUILayout.PropertyField(
                    _skillCooldown
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Target
        // ============================================================

        private void DrawTargetSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _targetFoldout =
                EditorGUILayout.Foldout(
                    _targetFoldout,
                    "Target",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_targetFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _targetSide
                );


                if (GetTargetSide()
                    != SkillTargetRelation.Self)
                {
                    EditorGUILayout.PropertyField(
                        _targetPolicy
                    );
                }


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Action
        // ============================================================

        private void DrawActionSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _actionFoldout =
                EditorGUILayout.Foldout(
                    _actionFoldout,
                    "Action",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_actionFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _actionType
                );


                if (GetActionType()
                    == ActiveSkillActionType.Cast)
                {
                    EditorGUILayout.PropertyField(
                        _castTime
                    );
                }


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Delivery
        // ============================================================

        private void DrawDeliverySection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _deliveryFoldout =
                EditorGUILayout.Foldout(
                    _deliveryFoldout,
                    "Delivery",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_deliveryFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _deliveryType
                );

                EditorGUILayout.PropertyField(
                    _areaType
                );


                if (GetDeliveryType()
                    == ActiveSkillDeliveryType.Projectile)
                {
                    EditorGUILayout.PropertyField(
                        _maxTargetCount
                    );
                }


                if (GetAreaType()
                    != ActiveSkillAreaType.Single)
                {
                    EditorGUILayout.PropertyField(
                        _maxEffectTargetCount
                    );
                }


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Area
        // ============================================================

        private void DrawAreaSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _areaFoldout =
                EditorGUILayout.Foldout(
                    _areaFoldout,
                    "Area",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_areaFoldout)
            {
                EditorGUI.indentLevel++;


                switch (GetAreaType())
                {
                    case ActiveSkillAreaType.TargetCircle:

                    case ActiveSkillAreaType.SelfCircle:

                        EditorGUILayout.PropertyField(
                            _areaRadius
                        );

                        break;


                    case ActiveSkillAreaType.SelfCone:

                        EditorGUILayout.PropertyField(
                            _areaRadius
                        );

                        EditorGUILayout.PropertyField(
                            _areaAngle
                        );

                        break;
                }


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Effects
        // ============================================================

        private void DrawEffectsSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _effectsFoldout =
                EditorGUILayout.Foldout(
                    _effectsFoldout,
                    "Effects",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_effectsFoldout)
            {
                EditorGUI.indentLevel++;


                for (int i = 0;
                     i < _effects.arraySize;
                     i++)
                {
                    SerializedProperty effectProperty =
                        _effects.GetArrayElementAtIndex(
                            i
                        );


                    EditorGUILayout.BeginVertical(
                        EditorStyles.helpBox
                    );


                    EditorGUILayout.PropertyField(
                        effectProperty,
                        new GUIContent(
                            GetEffectLabel(
                                effectProperty,
                                i
                            )
                        ),
                        true
                    );


                    if (GUILayout.Button(
                        "Remove Effect"
                    ))
                    {
                        _effects.DeleteArrayElementAtIndex(
                            i
                        );

                        EditorGUILayout.EndVertical();

                        break;
                    }


                    EditorGUILayout.EndVertical();
                }


                EditorGUILayout.Space();


                EditorGUILayout.BeginHorizontal();


                if (GUILayout.Button(
                    "Add Damage"
                ))
                {
                    AddEffect(
                        new SkillDamageEffectData()
                    );
                }

                if (GUILayout.Button(
                    "Add Heal"
                ))
                {
                    AddEffect(
                        new SkillHealEffectData()
                    );
                }

                if (GUILayout.Button(
                    "Add Runtime Effect"
                ))
                {
                    AddEffect(
                        new SkillRuntimeEffectData()
                    );
                }


                EditorGUILayout.EndHorizontal();


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Projectile
        // ============================================================

        private void DrawProjectileSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _projectileFoldout =
                EditorGUILayout.Foldout(
                    _projectileFoldout,
                    "Projectile",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_projectileFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _projectileSpeed
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Dash
        // ============================================================

        private void DrawDashSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _dashFoldout =
                EditorGUILayout.Foldout(
                    _dashFoldout,
                    "Dash",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_dashFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _dashDistance
                );

                EditorGUILayout.PropertyField(
                    _dashSpeed
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // FX
        // ============================================================

        private void DrawFXSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _fxFoldout =
                EditorGUILayout.Foldout(
                    _fxFoldout,
                    "FX",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_fxFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _skillFXType
                );

                EditorGUILayout.PropertyField(
                    _hitFXType
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Effects
        // ============================================================

        private void AddEffect(
            SkillEffectData effect)
        {
            int index =
                _effects.arraySize;

            _effects.InsertArrayElementAtIndex(
                index
            );

            SerializedProperty effectProperty =
                _effects.GetArrayElementAtIndex(
                    index
                );

            effectProperty.managedReferenceValue =
                effect;
        }


        private string GetEffectLabel(
            SerializedProperty effectProperty,
            int index)
        {
            object effect =
                effectProperty.managedReferenceValue;


            if (effect == null)
            {
                return $"Effect {index}";
            }


            return effect switch
            {
                SkillDamageEffectData =>
                    $"Effect {index} - Damage",

                SkillHealEffectData =>
                    $"Effect {index} - Heal",

                SkillRuntimeEffectData =>
                    $"Effect {index} - Runtime Effect",

                _ =>
                    $"Effect {index}"
            };
        }


        // ============================================================
        // Conditions
        // ============================================================

        private bool ShouldDrawAreaSection()
        {
            return GetAreaType()
                != ActiveSkillAreaType.Single;
        }


        private bool ShouldDrawProjectileSection()
        {
            return GetDeliveryType()
                == ActiveSkillDeliveryType.Projectile;
        }


        private bool ShouldDrawDashSection()
        {
            return GetActionType()
                == ActiveSkillActionType.Dash;
        }


        // ============================================================
        // Data
        // ============================================================

        private SkillTargetRelation GetTargetSide()
        {
            return (SkillTargetRelation)
                _targetSide.enumValueIndex;
        }


        private ActiveSkillActionType GetActionType()
        {
            return (ActiveSkillActionType)
                _actionType.enumValueIndex;
        }


        private ActiveSkillDeliveryType GetDeliveryType()
        {
            return (ActiveSkillDeliveryType)
                _deliveryType.enumValueIndex;
        }


        private ActiveSkillAreaType GetAreaType()
        {
            return (ActiveSkillAreaType)
                _areaType.enumValueIndex;
        }
    }
}