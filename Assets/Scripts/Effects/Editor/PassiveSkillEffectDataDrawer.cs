using Units;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PassiveSkillEffectData))]
public class PassiveSkillEffectDataDrawer : PropertyDrawer
{
    private static readonly GUIContent[] Teams = { new GUIContent("아군"), new GUIContent("적") };
    private static readonly int[] TeamValues = { (int)UnitTeam.Ally, (int)UnitTeam.Enemy };
    private static readonly GUIContent[] AllyModes = { new GUIContent("전체"), new GUIContent("클래스"), new GUIContent("타입"), new GUIContent("티어") };
    private static readonly GUIContent[] EnemyModes = { new GUIContent("전체"), new GUIContent("클래스"), new GUIContent("타입"), new GUIContent("진영") };
    private static readonly int[] AllyValues = { (int)UnitModifierApplyType.All, (int)UnitModifierApplyType.Class, (int)UnitModifierApplyType.Type, (int)UnitModifierApplyType.Tier };
    private static readonly int[] EnemyValues = { (int)UnitModifierApplyType.All, (int)UnitModifierApplyType.Class, (int)UnitModifierApplyType.Type, (int)UnitModifierApplyType.Faction };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = property.isExpanded ? (TargetField(property) == null ? 4 : 5) : 1;
        return lines * EditorGUIUtility.singleLineHeight + (lines - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            Next(ref line);
            EditorGUI.PropertyField(line, property.FindPropertyRelative("_passiveSkill"), new GUIContent("부여할 패시브"));
            var team = property.FindPropertyRelative("_targetTeam");
            var mode = property.FindPropertyRelative("_applyType");
            Next(ref line);
            EditorGUI.BeginChangeCheck();
            int selectedTeam = EditorGUI.IntPopup(line, new GUIContent("대상 팀"), team.intValue, Teams, TeamValues);
            if (EditorGUI.EndChangeCheck())
            {
                team.intValue = selectedTeam;
                // 팀 변경으로 사용할 수 없어진 조건만 전체로 초기화합니다.
                if (mode.intValue == (int)UnitModifierApplyType.Tier || mode.intValue == (int)UnitModifierApplyType.Faction)
                    mode.intValue = (int)UnitModifierApplyType.All;
            }
            bool ally = team.intValue == (int)UnitTeam.Ally;
            Next(ref line);
            EditorGUI.BeginChangeCheck();
            int selectedMode = EditorGUI.IntPopup(line, new GUIContent("적용 방식"), mode.intValue,
                ally ? AllyModes : EnemyModes, ally ? AllyValues : EnemyValues);
            if (EditorGUI.EndChangeCheck()) mode.intValue = selectedMode;
            string targetField = TargetField(property);
            if (targetField != null)
            {
                Next(ref line);
                EditorGUI.PropertyField(line, property.FindPropertyRelative(targetField), new GUIContent("적용 대상"));
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }

    private static void Next(ref Rect line) => line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

    private static string TargetField(SerializedProperty property)
    {
        bool ally = property.FindPropertyRelative("_targetTeam").intValue == (int)UnitTeam.Ally;
        return (UnitModifierApplyType)property.FindPropertyRelative("_applyType").intValue switch
        {
            UnitModifierApplyType.Class => ally ? "_allyClass" : "_enemyClass",
            UnitModifierApplyType.Type => ally ? "_allyType" : "_enemyType",
            UnitModifierApplyType.Tier when ally => "_allyTier",
            UnitModifierApplyType.Faction when !ally => "_enemyFaction",
            _ => null
        };
    }
}
