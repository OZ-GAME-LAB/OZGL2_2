using Cysharp.Threading.Tasks;
using Game.Core;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using TMPro;
using System;
using Units.Skills;


namespace Units
{
    public class UnitSpawnTest : MonoBehaviour
    {
        // ============================================================
        // References
        // ============================================================

        [SerializeField]
        private SpawnManager _spawnManager;

        [SerializeField]
        private RuntimeUnitManager _runtimeUnitManager;


        // ============================================================
        // Spawn Modifiers
        // ============================================================

        [Header("Spawn Stat Settings")]
        [Tooltip("스폰 전에 적용할 아군 스탯. Percent 값 0.1은 +10%입니다.")]
        [SerializeField]
        private List<AllyStatSettings> _allyStatModifiers = new();

        [Tooltip("스폰 전에 적용할 적 스탯. Percent 값 0.1은 +10%입니다.")]
        [SerializeField]
        private List<EnemyStatSettings> _enemyStatModifiers = new();


        [Header("Spawn Passive Settings")]
        [Tooltip("기본 패시브에 추가할 아군 패시브 SO. 이후 스폰에만 적용합니다.")]
        [SerializeField]
        private List<AllyPassiveSettings> _allyPassiveModifiers = new();

        [Tooltip("기본 패시브에 추가할 적 패시브 SO. 이후 스폰에만 적용합니다.")]
        [SerializeField]
        private List<EnemyPassiveSettings> _enemyPassiveModifiers = new();

        private UnitStatModifierManager _registeredModifierManager;


        // ============================================================
        // Engagement Debug UI
        // ============================================================

        [Header("Engagement Debug")]

        [SerializeField]
        private TMP_Text _engagementDebugText;


        // ============================================================
        // Ally Group 1
        // ============================================================

        [Header("Ally Group 1")]

        [SerializeField]
        private AllyUnitType _allyUnitType1;

        [SerializeField]
        private int _allyCount1 = 5;

        [SerializeField]
        private Transform _allySpawnPosition1;

        [SerializeField]
        private Vector2 _allyRallyPoint1;


        // ============================================================
        // Ally Group 2
        // ============================================================

        [Header("Ally Group 2")]

        [SerializeField]
        private AllyUnitType _allyUnitType2;

        [SerializeField]
        private int _allyCount2 = 5;

        [SerializeField]
        private Transform _allySpawnPosition2;

        [SerializeField]
        private Vector2 _allyRallyPoint2;


        // ============================================================
        // Ally Group 3
        // ============================================================

        [Header("Ally Group 3")]

        [SerializeField]
        private AllyUnitType _allyUnitType3;

        [SerializeField]
        private int _allyCount3 = 5;

        [SerializeField]
        private Transform _allySpawnPosition3;

        [SerializeField]
        private Vector2 _allyRallyPoint3;


        // ============================================================
        // Ally Group 4
        // ============================================================

        [Header("Ally Group 4")]

        [SerializeField]
        private AllyUnitType _allyUnitType4;

        [SerializeField]
        private int _allyCount4 = 5;

        [SerializeField]
        private Transform _allySpawnPosition4;

        [SerializeField]
        private Vector2 _allyRallyPoint4;


        // ============================================================
        // Enemy Wave
        // ============================================================

        [Header("Enemy Wave")]

        [SerializeField]
        private int _enemyCost = 20;


        [Header("Enemy Class Weights")]

        [SerializeField]
        private ClassWeights _enemyClassWeights;


        [Header("Enemy Unit Types")]

        [SerializeField]
        private List<EnemyUnitType> _enemyUnitTypes =
            new List<EnemyUnitType>();


        // ============================================================
        // Unity Lifecycle
        // ============================================================

        private void OnEnable()
        {
            if (_runtimeUnitManager == null)
                return;


            _runtimeUnitManager.PreparationCompleted +=
                OnPreparationCompleted;
        }


        private void Start()
        {
            SpawnAll();
        }


        private void OnDisable()
        {
            if (_runtimeUnitManager == null)
                return;


            _runtimeUnitManager.PreparationCompleted -=
                OnPreparationCompleted;
        }

        private void OnDestroy()
        {
            ClearSpawnModifiers();
        }


        private void Update()
        {
            UpdateEngagementDebugUI();
        }


        // ============================================================
        // Engagement Debug
        // ============================================================

        private void UpdateEngagementDebugUI()
        {
            if (_engagementDebugText == null)
                return;


            if (_runtimeUnitManager == null)
            {
                _engagementDebugText.text =
                    "RuntimeUnitManager 없음";

                return;
            }


            IReadOnlyList<EngagementContext> engagements =
                _runtimeUnitManager.Engagements;


            if (engagements.Count == 0)
            {
                _engagementDebugText.text =
                    "[Engagement Debug]\n" +
                    "진행 중인 교전 없음";

                return;
            }


            System.Text.StringBuilder builder =
                new();


            builder.AppendLine(
                "[Engagement Debug]"
            );

            builder.AppendLine(
                $"진행 중인 교전 : {engagements.Count}"
            );


            for (int i = 0;
                 i < engagements.Count;
                 i++)
            {
                EngagementContext engagement =
                    engagements[i];


                if (engagement == null)
                    continue;


                builder.AppendLine();
                builder.AppendLine(
                    $"========== Engagement {i + 1} =========="
                );


                AppendGroupDebugInfo(
                    builder,
                    "Ally",
                    engagement.AllyGroups
                );


                AppendGroupDebugInfo(
                    builder,
                    "Enemy",
                    engagement.EnemyGroups
                );
            }


            _engagementDebugText.text =
                builder.ToString();
        }


        private void AppendGroupDebugInfo(
            System.Text.StringBuilder builder,
            string teamName,
            IReadOnlyList<Unit_GroupAI> groups)
        {
            builder.AppendLine(
                $"[{teamName}]"
            );


            if (groups == null ||
                groups.Count == 0)
            {
                builder.AppendLine(
                    "- 없음"
                );

                return;
            }


            for (int i = 0;
                 i < groups.Count;
                 i++)
            {
                Unit_GroupAI group =
                    groups[i];


                if (group == null)
                    continue;


                builder.AppendLine(
                    $"- {group.name} " +
                    $"({group.Members.Count}명)"
                );
            }
        }


        // ============================================================
        // Spawn Modifier Registration
        // ============================================================

        private bool ApplySpawnModifiers()
        {
            UnitStatModifierManager manager =
                _spawnManager != null ? _spawnManager.StatModifierManager : null;

            if (manager == null)
            {
                Debug.LogError("[UnitSpawnTest] SpawnManager의 UnitStatModifierManager가 없습니다.");

                return false;
            }

            List<AllyStatModifier> allyStats = new();

            List<EnemyStatModifier> enemyStats = new();

            List<AllyPassiveSkillModifier> allyPassives = new();

            List<EnemyPassiveSkillModifier> enemyPassives = new();

            if (_allyStatModifiers != null)
            {
                foreach (AllyStatSettings setting in _allyStatModifiers)
                {
                    if (setting != null)
                    {
                        allyStats.Add(setting.ToModifier(this));
                    }
                }
            }

            if (_enemyStatModifiers != null)
            {
                foreach (EnemyStatSettings setting in _enemyStatModifiers)
                {
                    if (setting != null)
                    {
                        enemyStats.Add(setting.ToModifier(this));
                    }
                }
            }

            if (_allyPassiveModifiers != null)
            {
                foreach (AllyPassiveSettings setting in _allyPassiveModifiers)
                {
                    if (setting != null)
                    {
                        allyPassives.Add(setting.ToModifier(this));
                    }
                }
            }

            if (_enemyPassiveModifiers != null)
            {
                foreach (EnemyPassiveSettings setting in _enemyPassiveModifiers)
                {
                    if (setting != null)
                    {
                        enemyPassives.Add(setting.ToModifier(this));
                    }
                }
            }

            if (_registeredModifierManager != null && _registeredModifierManager != manager)
            {
                ClearSpawnModifiers();
            }

            IUnitStatModifierRegister register = manager;

            // 이전 테스트 등록만 교체해서 반복 스폰의 스탯 누적을 막는다.
            register.RemoveModifiersBySource(this);

            register.AddBothModifiers(allyStats, enemyStats);

            register.AddBothPassiveSkills(allyPassives, enemyPassives);

            _registeredModifierManager = manager;

            return true;
        }


        [ContextMenu("Clear Test Modifiers")]
        private void ClearSpawnModifiers()
        {
            if (_registeredModifierManager == null)
                return;

            IUnitStatModifierRegister register = _registeredModifierManager;

            register.RemoveModifiersBySource(this);

            _registeredModifierManager = null;
        }


        // ============================================================
        // Test Spawn
        // ============================================================

        [ContextMenu("Spawn All")]
        private void SpawnAll()
        {
            if (!Validate())
                return;


            if (!ApplySpawnModifiers())
                return;


            RequestAllySpawns();
            RequestEnemySpawn();


            Debug.Log(
                "[UnitSpawnTest] " +
                "전체 Spawn 요청 완료."
            );
        }


        // ============================================================
        // Ally Spawn
        // ============================================================

        [ContextMenu("Spawn Allies")]
        private void SpawnAllies()
        {
            if (_spawnManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] SpawnManager가 없습니다."
                );

                return;
            }


            if (!ApplySpawnModifiers())
                return;


            RequestAllySpawns();
        }


        private void RequestAllySpawns()
        {
            _spawnManager.SpawnAllyGroup(
                _allyUnitType1,
                _allySpawnPosition1.position,
                _allyCount1,
                _allyRallyPoint1
            );


            _spawnManager.SpawnAllyGroup(
                _allyUnitType2,
                _allySpawnPosition2.position,
                _allyCount2,
                _allyRallyPoint2
            );


            _spawnManager.SpawnAllyGroup(
                _allyUnitType3,
                _allySpawnPosition3.position,
                _allyCount3,
                _allyRallyPoint3
            );


            _spawnManager.SpawnAllyGroup(
                _allyUnitType4,
                _allySpawnPosition4.position,
                _allyCount4,
                _allyRallyPoint4
            );


            Debug.Log(
                "[UnitSpawnTest] " +
                "아군 4개 그룹 Spawn 요청 완료."
            );
        }


        // ============================================================
        // Enemy Spawn
        // ============================================================

        [ContextMenu("Spawn Enemies")]
        private void SpawnEnemies()
        {
            if (_spawnManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] SpawnManager가 없습니다."
                );

                return;
            }


            if (!ApplySpawnModifiers())
                return;


            RequestEnemySpawn();
        }


        private void RequestEnemySpawn()
        {
            SpawnContext context =
                new SpawnContext(
                    _enemyClassWeights,
                    _enemyUnitTypes
                );


            CancellationToken cancellationToken =
                this.GetCancellationTokenOnDestroy();


            _spawnManager.SpawnEnemyWaveAsync(
                _enemyCost,
                context,
                cancellationToken
            ).Forget();


            Debug.Log(
                "[UnitSpawnTest] " +
                $"적군 Wave Spawn 요청 완료. " +
                $"Cost={_enemyCost}"
            );
        }


        // ============================================================
        // Preparation Completed
        // ============================================================

        private void OnPreparationCompleted()
        {
            Debug.Log(
                "[UnitSpawnTest] " +
                "모든 Unit Spawn 및 Rally 준비 완료."
            );


            StartBattle();
        }


        // ============================================================
        // Start Battle
        // ============================================================

        [ContextMenu("Start Battle")]
        private void StartBattle()
        {
            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] RuntimeUnitManager가 없습니다."
                );

                return;
            }


            _runtimeUnitManager.StartBattlePhase();


            Debug.Log(
                "[UnitSpawnTest] " +
                "전투 시작."
            );
        }


        // ============================================================
        // Reset Battle
        // ============================================================

        [ContextMenu("Reset Battle")]
        private void ResetBattle()
        {
            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] RuntimeUnitManager가 없습니다."
                );

                return;
            }


            _runtimeUnitManager.ClearRuntime();


            Debug.Log(
                "[UnitSpawnTest] " +
                "전투 상태 초기화."
            );
        }


        // ============================================================
        // Validation
        // ============================================================

        private bool Validate()
        {
            if (_spawnManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] SpawnManager가 없습니다."
                );

                return false;
            }


            if (_runtimeUnitManager == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] RuntimeUnitManager가 없습니다."
                );

                return false;
            }


            if (_allySpawnPosition1 == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 1 Spawn Position이 없습니다."
                );

                return false;
            }


            if (_allySpawnPosition2 == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 2 Spawn Position이 없습니다."
                );

                return false;
            }


            if (_allySpawnPosition3 == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 3 Spawn Position이 없습니다."
                );

                return false;
            }


            if (_allySpawnPosition4 == null)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 4 Spawn Position이 없습니다."
                );

                return false;
            }


            if (_allyCount1 <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 1 Count가 올바르지 않습니다."
                );

                return false;
            }


            if (_allyCount2 <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 2 Count가 올바르지 않습니다."
                );

                return false;
            }


            if (_allyCount3 <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 3 Count가 올바르지 않습니다."
                );

                return false;
            }


            if (_allyCount4 <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Ally Group 4 Count가 올바르지 않습니다."
                );

                return false;
            }


            if (_enemyCost <= 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Enemy Cost가 올바르지 않습니다."
                );

                return false;
            }


            if (_enemyUnitTypes == null ||
                _enemyUnitTypes.Count == 0)
            {
                Debug.LogError(
                    "[UnitSpawnTest] Enemy Unit Type이 설정되지 않았습니다."
                );

                return false;
            }


            return true;
        }

        // ============================================================
        // Inspector Settings
        // ============================================================

        // 인스펙터 설정을 출처가 있는 런타임 Modifier로 변환한다.
        [Serializable]
        private sealed class AllyStatSettings
        {
            [Tooltip("Ally: All / Class / Type / Tier. 선택한 Apply Type의 대상 필드만 사용합니다.")]
            [SerializeField]
            private UnitModifierApplyType _applyType = UnitModifierApplyType.All;

            [SerializeField]
            private AllyUnitClass _targetClass;

            [SerializeField]
            private AllyUnitType _targetUnitType;

            [SerializeField]
            private AllyUnitTier _targetTier;

            [SerializeField]
            private UnitStatType _statType = UnitStatType.AttackPower;

            [SerializeField]
            private UnitStatModifierType _modifierType;

            [SerializeField]
            private float _value;


            public AllyStatModifier ToModifier(object source)
            {
                return new AllyStatModifier(
                    source,
                    _applyType,
                    _targetClass,
                    _targetUnitType,
                    _statType,
                    _modifierType,
                    _value,
                    _targetTier
                );
            }
        }


        // 인스펙터 설정을 출처가 있는 런타임 Modifier로 변환한다.
        [Serializable]
        private sealed class AllyPassiveSettings
        {
            [Tooltip("Ally: All / Class / Type / Tier. 선택한 Apply Type의 대상 필드만 사용합니다.")]
            [SerializeField]
            private UnitModifierApplyType _applyType = UnitModifierApplyType.All;

            [SerializeField]
            private AllyUnitClass _targetClass;

            [SerializeField]
            private AllyUnitType _targetUnitType;

            [SerializeField]
            private AllyUnitTier _targetTier;

            [SerializeField]
            private PassiveSkillData _passiveSkill;


            public AllyPassiveSkillModifier ToModifier(object source)
            {
                return new AllyPassiveSkillModifier(
                    source,
                    _passiveSkill,
                    _applyType,
                    _targetClass,
                    _targetUnitType,
                    _targetTier
                );
            }
        }


        // 인스펙터 설정을 출처가 있는 런타임 Modifier로 변환한다.
        [Serializable]
        private sealed class EnemyStatSettings
        {
            [Tooltip("Enemy: All / Class / Type / Faction. 선택한 Apply Type의 대상 필드만 사용합니다.")]
            [SerializeField]
            private UnitModifierApplyType _applyType = UnitModifierApplyType.All;

            [SerializeField]
            private EnemyUnitClass _targetClass;

            [SerializeField]
            private EnemyUnitType _targetUnitType;

            [SerializeField]
            private EnemyUnitFaction _targetFaction;

            [SerializeField]
            private UnitStatType _statType = UnitStatType.AttackPower;

            [SerializeField]
            private UnitStatModifierType _modifierType;

            [SerializeField]
            private float _value;


            public EnemyStatModifier ToModifier(object source)
            {
                return new EnemyStatModifier(
                    source,
                    _applyType,
                    _targetClass,
                    _targetUnitType,
                    _statType,
                    _modifierType,
                    _value,
                    _targetFaction
                );
            }
        }


        // 인스펙터 설정을 출처가 있는 런타임 Modifier로 변환한다.
        [Serializable]
        private sealed class EnemyPassiveSettings
        {
            [Tooltip("Enemy: All / Class / Type / Faction. 선택한 Apply Type의 대상 필드만 사용합니다.")]
            [SerializeField]
            private UnitModifierApplyType _applyType = UnitModifierApplyType.All;

            [SerializeField]
            private EnemyUnitClass _targetClass;

            [SerializeField]
            private EnemyUnitType _targetUnitType;

            [SerializeField]
            private EnemyUnitFaction _targetFaction;

            [SerializeField]
            private PassiveSkillData _passiveSkill;


            public EnemyPassiveSkillModifier ToModifier(object source)
            {
                return new EnemyPassiveSkillModifier(
                    source,
                    _passiveSkill,
                    _applyType,
                    _targetClass,
                    _targetUnitType,
                    _targetFaction
                );
            }
        }
    }
}