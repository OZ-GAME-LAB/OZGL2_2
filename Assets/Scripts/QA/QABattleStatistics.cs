using System;
using System.Collections.Generic;
using UnityEngine;
using Units;

namespace Game.QA
{
    public sealed class QAUnitStatistics
    {
        public int Id;
        public string Label;
        public UnitTeam Team;
        public Unit_Gateway Unit;
        public int Lifetime;
        public bool Dead;
        public int Kills, SkillSuccess, SkillFailure, SkillInterrupted;
        public float Hp, Shield, MaxHp, SurvivalTime;
        public float HpDamage, ShieldAbsorbed, Healing, ShieldGranted;
        public string LastSkillReason;
    }

    /// <summary>QA가 소유한 유닛의 실제 적용 결과만 수집한다. 전투 코드는 변경하지 않는다.</summary>
    public sealed class QABattleStatistics : IDisposable
    {
        private readonly List<Binding> _bindings = new();
        private readonly List<QAUnitStatistics> _records = new();
        public IReadOnlyList<QAUnitStatistics> Records => _records;
        public bool Collecting { get; private set; }
        public float Elapsed { get; private set; }

        public void Track(QAUnitEntry entry)
        {
            var record = new QAUnitStatistics { Id = entry.Id, Label = entry.Label, Team = entry.Team,
                Unit = entry.Unit, Lifetime = entry.Unit.LifetimeVersion };
            _records.Add(record);
            _bindings.Add(new Binding(this, record));
            Refresh();
        }

        public void Begin() { Elapsed = 0; Collecting = true; Refresh(); }
        public void Tick(float delta) { if (Collecting) { Elapsed += delta; Refresh(); } }
        public void Freeze() { Refresh(); Collecting = false; }

        public void Refresh()
        {
            foreach (var record in _records)
            {
                if (record.Unit != null && record.Unit.LifetimeVersion == record.Lifetime)
                {
                    record.Hp = record.Unit.CurrentHp;
                    record.Shield = record.Unit.CurrentShield;
                    record.MaxHp = record.Unit.RuntimeStatus.MaxHp;
                }
                if (!record.Dead) record.SurvivalTime = Elapsed;
            }
        }

        public void Dispose()
        {
            Collecting = false;
            foreach (var binding in _bindings) binding.Dispose();
            _bindings.Clear(); _records.Clear(); Elapsed = 0;
        }

        private sealed class Binding : IDisposable
        {
            private readonly QABattleStatistics _owner;
            private readonly QAUnitStatistics _record;
            private readonly Unit_Life _life;
            private readonly Unit_Gateway _unit;
            private readonly Unit_RuntimeStatus _status;
            private readonly Dictionary<long, Vector2> _pendingDamage = new();
            private readonly HashSet<long> _executions = new();
            private float _maxHp;
            private bool Valid => _owner.Collecting && _unit != null && _unit.LifetimeVersion == _record.Lifetime;

            public Binding(QABattleStatistics owner, QAUnitStatistics record)
            {
                _owner = owner; _record = record; _unit = record.Unit;
                _life = _unit.GetComponent<Unit_Life>(); _status = _unit.RuntimeStatus;
                _maxHp = _status.MaxHp;
                _life.HpChanged += OnHp; _life.ShieldChanged += OnShield;
                _life.Damaged += OnDamage; _life.Healed += OnHeal; _life.ShieldAdded += OnShieldAdded;
                // Unit_Life's max-HP handler is already registered by Core.Initialize.
                // Its cap adjustments run before this handler updates the previous ceiling.
                _status.MaxHpChanged += OnMaxHp;
                _unit.DeathConfirmed += OnDeath; _unit.SkillExecutionEnded += OnSkill;
            }

            private void OnMaxHp(float previous, float current) { _maxHp = current; }
            private void OnHp(float previous, float current)
            {
                // A ceiling clamp is not damage, even inside a nested combat event.
                if (_status.MaxHp < _maxHp && previous > _status.MaxHp && Mathf.Approximately(current, _status.MaxHp)) return;
                AddPending(previous - current, false);
            }
            private void OnShield(float previous, float current)
            {
                if (_status.MaxHp < _maxHp && previous > _life.ShieldLimit && Mathf.Approximately(current, _life.ShieldLimit)) return;
                AddPending(previous - current, true);
            }
            private void AddPending(float amount, bool shield)
            {
                long id = CombatEventContext.Current.EventId;
                if (!Valid || amount <= 0 || id == 0) return;
                _pendingDamage.TryGetValue(id, out var pending);
                if (shield) pending.y += amount; else pending.x += amount;
                _pendingDamage[id] = pending;
            }
            private void OnDamage(DamageResult result)
            {
                if (!Valid || !_pendingDamage.Remove(result.Metadata.EventId, out var pending)) return;
                _record.HpDamage += pending.x; _record.ShieldAbsorbed += pending.y;
            }
            private void OnHeal(float actual) { if (Valid) _record.Healing += actual; }
            private void OnShieldAdded(float actual) { if (Valid) _record.ShieldGranted += actual; }
            private void OnDeath(CombatDeathResult death)
            {
                if (!Valid || _record.Dead) return;
                _record.Dead = true; _record.SurvivalTime = _owner.Elapsed;
                foreach (var killer in _owner._records)
                    if (ReferenceEquals(killer.Unit, death.Killer.Target) && killer.Lifetime == death.Killer.LifetimeVersion && killer.Team != _record.Team)
                    { killer.Kills++; break; }
            }
            private void OnSkill(SkillExecutionResult result)
            {
                if (!Valid || !_executions.Add(result.ExecutionId)) return;
                switch (result.Kind)
                {
                    case SkillCompletionKind.Success: _record.SkillSuccess++; break;
                    case SkillCompletionKind.Failed: _record.SkillFailure++; break;
                    case SkillCompletionKind.Interrupted: _record.SkillInterrupted++; break;
                }
                _record.LastSkillReason = result.Reason;
                // TODO(QA-unconnected): completed actions exclude later projectile/DoT impacts.
                // Outgoing DPS and per-skill contribution need a shared application-result hook.
            }
            public void Dispose()
            {
                if (_life != null) { _life.HpChanged -= OnHp; _life.ShieldChanged -= OnShield;
                    _life.Damaged -= OnDamage; _life.Healed -= OnHeal; _life.ShieldAdded -= OnShieldAdded; }
                if (_status != null) _status.MaxHpChanged -= OnMaxHp;
                if (_unit != null) { _unit.DeathConfirmed -= OnDeath; _unit.SkillExecutionEnded -= OnSkill; }
                _pendingDamage.Clear(); _executions.Clear();
            }
        }
    }
}
