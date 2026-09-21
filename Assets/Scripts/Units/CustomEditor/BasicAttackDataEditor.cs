using UnityEditor;
using Units.Skills;


namespace Units.Editor
{
    [CustomEditor(typeof(BasicAttackData))]
    public class BasicAttackDataEditor : UnityEditor.Editor
    {
        // ============================================================
        // Properties
        // ============================================================

        private SerializedProperty _basicAttackRange;
        private SerializedProperty _basicAttackDelay;
        private SerializedProperty _damageType;

        private SerializedProperty _executionType;

        private SerializedProperty _areaType;
        private SerializedProperty _maxTargetCount;
        private SerializedProperty _maxDamageableCount;

        private SerializedProperty _areaRadius;
        private SerializedProperty _areaAngle;

        private SerializedProperty _projectileSpeed;

        private SerializedProperty _attackFXType;
        private SerializedProperty _hitFXType;


        // ============================================================
        // Foldouts
        // ============================================================

        private bool _basicFoldout = true;
        private bool _executionFoldout = true;
        private bool _targetFoldout = true;
        private bool _areaFoldout = true;
        private bool _projectileFoldout = true;
        private bool _fxFoldout = true;


        // ============================================================
        // Initialize
        // ============================================================

        private void OnEnable()
        {
            _basicAttackRange =
                serializedObject.FindProperty(
                    "_basicAttackRange"
                );

            _basicAttackDelay =
                serializedObject.FindProperty(
                    "_basicAttackDelay"
                );

            _damageType =
                serializedObject.FindProperty(
                    "_damageType"
                );


            _executionType =
                serializedObject.FindProperty(
                    "_executionType"
                );


            _areaType =
                serializedObject.FindProperty(
                    "_areaType"
                );

            _maxTargetCount =
                serializedObject.FindProperty(
                    "_maxTargetCount"
                );

            _maxDamageableCount =
                serializedObject.FindProperty(
                    "_maxDamageableCount"
                );


            _areaRadius =
                serializedObject.FindProperty(
                    "_areaRadius"
                );

            _areaAngle =
                serializedObject.FindProperty(
                    "_areaAngle"
                );


            _projectileSpeed =
                serializedObject.FindProperty(
                    "_projectileSpeed"
                );


            _attackFXType =
                serializedObject.FindProperty(
                    "_attackFXType"
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

            DrawExecutionSection();

            EditorGUILayout.Space();

            DrawTargetSection();


            if (ShouldDrawAreaSection())
            {
                EditorGUILayout.Space();

                DrawAreaSection();
            }


            if (ShouldDrawProjectileSection())
            {
                EditorGUILayout.Space();

                DrawProjectileSection();
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
                    _basicAttackRange
                );

                EditorGUILayout.PropertyField(
                    _basicAttackDelay
                );

                EditorGUILayout.PropertyField(
                    _damageType
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Execution
        // ============================================================

        private void DrawExecutionSection()
        {
            EditorGUILayout.BeginVertical(
                EditorStyles.helpBox
            );


            _executionFoldout =
                EditorGUILayout.Foldout(
                    _executionFoldout,
                    "Execution",
                    true,
                    EditorStyles.foldoutHeader
                );


            if (_executionFoldout)
            {
                EditorGUI.indentLevel++;


                EditorGUILayout.PropertyField(
                    _executionType
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
                    _areaType
                );


                if (GetExecutionType()
                    == BasicAttackExecutionType.Projectile)
                {
                    EditorGUILayout.PropertyField(
                        _maxTargetCount
                    );
                }


                if (GetAreaType()
                    != BasicAttackAreaType.Single)
                {
                    EditorGUILayout.PropertyField(
                        _maxDamageableCount
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


                BasicAttackAreaType areaType =
                    GetAreaType();


                switch (areaType)
                {
                    case BasicAttackAreaType.TargetCircle:

                    case BasicAttackAreaType.SelfCircle:

                        EditorGUILayout.PropertyField(
                            _areaRadius
                        );

                        break;


                    case BasicAttackAreaType.SelfCone:

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
                    _attackFXType
                );

                EditorGUILayout.PropertyField(
                    _hitFXType
                );


                EditorGUI.indentLevel--;
            }


            EditorGUILayout.EndVertical();
        }


        // ============================================================
        // Conditions
        // ============================================================

        private bool ShouldDrawAreaSection()
        {
            return GetAreaType()
                != BasicAttackAreaType.Single;
        }


        private bool ShouldDrawProjectileSection()
        {
            return GetExecutionType()
                == BasicAttackExecutionType.Projectile;
        }


        // ============================================================
        // Data
        // ============================================================

        private BasicAttackExecutionType GetExecutionType()
        {
            return (BasicAttackExecutionType)
                _executionType.enumValueIndex;
        }


        private BasicAttackAreaType GetAreaType()
        {
            return (BasicAttackAreaType)
                _areaType.enumValueIndex;
        }
    }
}