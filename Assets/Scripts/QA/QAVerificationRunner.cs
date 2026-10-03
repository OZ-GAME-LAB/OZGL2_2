#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Units;
using Units.Effects;

namespace Game.QA
{
    /// <summary>Editor-only real-scene verification. Not part of the QA panel or a save/load feature.</summary>
    public sealed class QAVerificationRunner : MonoBehaviour
    {
        private readonly List<string> _checks = new();
        private QABattleController _qa;
        private string _failure;
        public bool Finished { get; private set; }
        public string Report { get; private set; }
        public void Run() { _qa = GetComponent<QABattleController>(); StartCoroutine(Guarded(Scenarios())); }
        private IEnumerator Guarded(IEnumerator scenarios)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(scenarios);
            while (stack.Count > 0)
            {
                object yielded;
                try { var current = stack.Peek(); if (!current.MoveNext()) { stack.Pop(); continue; } yielded = current.Current; if (yielded is IEnumerator nested) { stack.Push(nested); continue; } }
                catch (Exception error) { _failure = error.ToString(); break; }
                yield return yielded;
            }
            Finished = true;
            Report = string.Join("\n", _checks) + "\n" + (_failure == null ? "PASS: all scene verification scenarios completed." : "FAIL: " + _failure);
            Directory.CreateDirectory("Temp/QA"); File.WriteAllText("Temp/QA/verification.txt", Report);
            if (_failure == null) Debug.Log("[QA verification] " + Report); else Debug.LogError("[QA verification] " + Report);
        }
        private void Check(bool pass, string label)
        { if (!pass) throw new InvalidOperationException(label); _checks.Add("PASS: " + label); }
        private bool Near(float a, float b) => Mathf.Abs(a - b) < .02f;
        private IEnumerator Ready()
        {
            float until = Time.realtimeSinceStartup + 8;
            while (!_qa.CanEdit && Time.realtimeSinceStartup < until) yield return null;
            Check(_qa.CanEdit, "spawn/preparation completes within 8s");
        }
        private IEnumerator Scenarios()
        {
            _qa.ResetBattle(); yield return Ready(); _qa.ClearComposition(); yield return Ready();
            Check(!_qa.CanStart && _qa.Entries.Count == 0, "empty battlefield cannot start");
            var unavailable = _qa.Options.First(o => !o.Available);
            Check(!_qa.Spawn(unavailable, 1), "unregistered unit rejected without creating objects");
            var ally = _qa.Options.First(o => o.Available && o.Label.StartsWith("WA_T1"));
            var enemy = _qa.Options.First(o => o.Available && o.Team == UnitTeam.Enemy);
            _qa.Spawn(ally, 3); _qa.StartBattle();
            Check(_qa.Phase == QABattlePhase.Preparing, "start blocked while spawning");
            yield return Ready(); Check(!_qa.CanStart, "one-sided composition cannot start");
            _qa.Spawn(enemy, 3); yield return Ready();
            Check(_qa.CanStart && _qa.Runtime.AllyUnitCount == 3 && _qa.Runtime.EnemyUnitCount == 3, "exact 3v3 composition registered");
            int id = _qa.Entries[0].Id; _qa.Select(id); _qa.PlaceSelected(new Vector2(-10, 2)); yield return Ready();
            Check(Vector2.Distance(_qa.Entries.First(e => e.Id == id).Unit.transform.position, new Vector2(-10, 2)) < .1f, "selected unit placement updates actual start position");
            var positions = _qa.Entries.Select(e => e.Position).ToArray();
            _qa.StartBattle(); _qa.TogglePause();
            Check(_qa.Phase == QABattlePhase.Paused && Time.timeScale == 0 && !_qa.PlaceSelected(Vector2.zero), "pause freezes simulation and locks placement");
            float time = _qa.Statistics.Elapsed;
            yield return new WaitForSecondsRealtime(.35f);
            Check(Near(time, _qa.Statistics.Elapsed), "paused battle clock remains fixed");
            var attacker = _qa.Entries.First(e => e.Team == UnitTeam.Ally).Unit;
            var target = _qa.Entries.First(e => e.Team == UnitTeam.Enemy).Unit;
            var record = _qa.Statistics.Records.First(r => ReferenceEquals(r.Unit, target));
            float shieldBefore = record.ShieldGranted, damageBefore = record.HpDamage, absorbedBefore = record.ShieldAbsorbed;
            var shield = target.AddShieldWithResult(20, CombatEventMetadata.Create(attacker));
            var hit = target.TakeDamageWithResult(new DamageResult(attacker, target, 35, DamageSourceType.BasicAttack, false));
            Check(Near(record.ShieldGranted - shieldBefore, shield.ShieldAdded) && Near(record.HpDamage - damageBefore, hit.HpDamage) && Near(record.ShieldAbsorbed - absorbedBefore, hit.ShieldAbsorbed), "HP damage and shield absorption match actual application results");
            float healedBefore = record.Healing;
            var heal = target.HealWithResult(9999, CombatEventMetadata.Create(attacker));
            Check(Near(record.Healing - healedBefore, heal.HealedAmount), "effective healing excludes overheal");
            target.AddShield(99999);
            float hpDamageBeforeClamp = record.HpDamage, shieldDamageBeforeClamp = record.ShieldAbsorbed;
            object modifierSource = new object(); float maxHp = target.RuntimeStatus.MaxHp;
            using (CombatEventContext.Enter(CombatEventMetadata.Create(attacker)))
                target.RuntimeStatus.AddCombatModifier(new CombatStatModifier(modifierSource, UnitStatType.MaxHp, UnitStatModifierType.Flat, -maxHp * .2f));
            Check(Near(record.HpDamage, hpDamageBeforeClamp) && Near(record.ShieldAbsorbed, shieldDamageBeforeClamp), "max-HP and shield ceiling clamps inside combat context are excluded from damage");
            target.RuntimeStatus.RemoveCombatModifiers(modifierSource);
            var overkill = target.TakeDamageWithResult(new DamageResult(attacker, target, 99999, DamageSourceType.BasicAttack, false));
            Check(record.Dead && record.HpDamage - hpDamageBeforeClamp < 99999 && Near(record.HpDamage - hpDamageBeforeClamp, overkill.HpDamage), "overkill capped to applied HP and definitive death retained");
            Check(_qa.Statistics.Records.First(r => ReferenceEquals(r.Unit, attacker)).Kills == 1, "definitive killer credited once");
            int beforeSkills = _qa.Statistics.Records.First(r => ReferenceEquals(r.Unit, attacker)).SkillFailure;
            attacker.NotifySkillExecutionEnded(new SkillExecutionResult(long.MaxValue - 1, SkillCompletionKind.Failed, false, Array.Empty<ActionExecutionResult>(), "QA contract check"));
            attacker.NotifySkillExecutionEnded(new SkillExecutionResult(long.MaxValue - 1, SkillCompletionKind.Failed, false, Array.Empty<ActionExecutionResult>(), "duplicate"));
            Check(_qa.Statistics.Records.First(r => ReferenceEquals(r.Unit, attacker)).SkillFailure == beforeSkills + 1, "skill-result event contract deduplicates execution IDs");
            _qa.StopBattle(); Check(_qa.Phase == QABattlePhase.Finished && _qa.Result == "중단", "manual stop retains partial result");
            _qa.Select(record.Id); Check(_qa.SelectedId == record.Id, "dead unit remains selectable in result");
            _qa.ResetBattle(); yield return Ready();
            Check(_qa.Entries.Select(e => e.Position).SequenceEqual(positions) && _qa.Statistics.Records.All(r => r.Hp == r.MaxHp && r.HpDamage == 0 && !r.Dead), "reset preserves exact composition/placement and restores fresh HP/statistics");
            Check(_qa.GetComponentsInChildren<Unit_Gateway>(true).Length == 6, "reset removes corpses rather than only registered survivors");
            for (int i = 0; i < 3; i++)
            {
                _qa.StartBattle(); _qa.ResetBattle(); yield return Ready();
                Check(_qa.GetComponentsInChildren<Unit_Gateway>(true).Length == 6 && _qa.Runtime.AllyUnitCount == 3 && _qa.Runtime.EnemyUnitCount == 3, "battle reset repeat " + (i + 1) + " has no stale lifetimes/registrations");
            }
            _qa.StartBattle(); float deadline = Time.realtimeSinceStartup + 65;
            while (_qa.Phase != QABattlePhase.Finished && Time.realtimeSinceStartup < deadline) yield return null;
            Check(_qa.Phase == QABattlePhase.Finished && _qa.Result != "중단", "real 3v3 battle reaches automatic team-wipe result");
            Check(_qa.Statistics.Records.Any(r => r.HpDamage > 0 && r.SkillSuccess > 0) && _qa.Statistics.Records.Sum(r => r.Kills) == _qa.Statistics.Records.Count(r => r.Dead), "real battle damage/skill successes and total kills reconcile with deaths");
            float endTime = _qa.Statistics.Elapsed; var endHps = _qa.Statistics.Records.Select(r => r.Hp).ToArray();
            yield return new WaitForSecondsRealtime(.3f);
            Check(Near(endTime, _qa.Statistics.Elapsed) && _qa.Statistics.Records.Select(r => r.Hp).SequenceEqual(endHps), "finished result freezes HP and time");
            _qa.Replay(); float replayDeadline = Time.realtimeSinceStartup + 8;
            while (_qa.Phase != QABattlePhase.Fighting && Time.realtimeSinceStartup < replayDeadline) yield return null;
            Check(_qa.Phase == QABattlePhase.Fighting, "replay finishes preparation and starts within 8s");
            Check(_qa.Entries.Select(e => e.Position).SequenceEqual(positions), "same-condition replay recreates stored positions and starts battle");
            _qa.ResetBattle(); yield return Ready(); _qa.ClearComposition(); yield return Ready();
            Check(_qa.Entries.Count == 0 && _qa.GetComponentsInChildren<Unit_Gateway>(true).Length == 0 && _qa.Statistics.Records.Count == 0, "clear composition removes units and statistics");
            Check(ProjectileManager.Instance.GetComponentsInChildren<Projectile_Controller>(true).All(p => !p.gameObject.activeSelf), "cleanup leaves no active projectiles");
            // A preparation coroutine must never repopulate a cleared session later.
            _qa.Spawn(ally, 5); _qa.ResetBattle(); yield return Ready(); _qa.ClearComposition(); yield return Ready();
            yield return new WaitForSecondsRealtime(.2f);
            Check(_qa.Entries.Count == 0 && _qa.Runtime.AllyUnitCount == 0, "rapid spawn/reset/clear cannot create late units");

            var archer = _qa.Options.First(o => o.Available && o.Label.StartsWith("AR_T1"));
            _qa.Spawn(archer, 1); yield return Ready(); _qa.Spawn(enemy, 1); yield return Ready();
            _qa.StartBattle();
            var rangedSource = _qa.Entries.First(e => e.Team == UnitTeam.Ally).Unit;
            var rangedTarget = _qa.Entries.First(e => e.Team == UnitTeam.Enemy).Unit;
            var dot = UnityEditor.AssetDatabase.LoadAssetAtPath<EffectData>("Assets/Data/ConsumableItems/Effects/consumable_ToxicFlaskEffect.asset");
            Check(RuntimeEffectManager.Instance.ApplyEffect(new EffectRequest(dot, rangedSource, rangedTarget)), "existing periodic damage effect applies in the real scene");
            float projectileDeadline = Time.realtimeSinceStartup + 15;
            Projectile_Controller projectile = null;
            while (projectile == null && Time.realtimeSinceStartup < projectileDeadline)
            { projectile = ProjectileManager.Instance.GetComponentsInChildren<Projectile_Controller>().FirstOrDefault(p => p.gameObject.activeSelf); if (projectile == null) yield return null; }
            Check(projectile != null, "real archer combat launches a projectile");
            _qa.TogglePause(); var projectilePosition = projectile.transform.position;
            float targetHp = rangedTarget.CurrentHp, scaledTime = Time.time;
            Check(rangedTarget.RuntimeStatus.ActiveEffects.Count > 0, "periodic effect still active when paused");
            yield return new WaitForSecondsRealtime(1.2f);
            Check(projectile != null && projectile.transform.position == projectilePosition && Near(targetHp, rangedTarget.CurrentHp) && Near(scaledTime, Time.time), "pause freezes actual projectile movement, periodic damage and effect duration");
            _qa.TogglePause(); yield return new WaitForSeconds(.2f);
            Check(projectile == null || projectile.transform.position != projectilePosition, "resume advances the previously paused projectile");
            _qa.ResetBattle(); yield return Ready();
            var effectList = (System.Collections.ICollection)typeof(RuntimeEffectManager).GetField("_activeEffects", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(RuntimeEffectManager.Instance);
            Check(effectList.Count == 0 && ProjectileManager.Instance.GetComponentsInChildren<Projectile_Controller>(true).All(p => !p.gameObject.activeSelf), "reset clears the exercised runtime effect and projectile");
            _qa.ClearComposition(); yield return Ready();
        }
    }
}
#endif
