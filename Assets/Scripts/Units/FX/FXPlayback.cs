using Units.Skills;
using UnityEngine;

namespace Units.FX
{
    // 월드 부모를 유지하고 위치·방향만 추적하여 유닛 외형 반전과 풀 반환의 영향을 분리한다.
    internal sealed class FXPlayback
    {
        internal SkillFXRequest Request { get; }
        internal Vector2 Position { get; private set; }
        internal Vector2 Direction { get; private set; }
        internal Transform FollowTarget => _follow;

        private Transform _follow;
        private CombatTargetSnapshot _target;
        private bool _requiresFollow;

        internal FXPlayback(SkillFXRequest request)
        {
            Request = request;
            Position = request.Position;
            Direction = request.Direction;

            switch (request.Entry.Attachment)
            {
                case FXAttachment.Owner:
                    _target = request.Metadata.Owner;
                    break;

                case FXAttachment.Target:
                    _target = request.Target;
                    break;

                case FXAttachment.Projectile:
                    _follow = request.FollowTarget;
                    break;
            }

            if (_target.MatchesLifetime)
                _follow = _target.Target.Transform;

            _requiresFollow = request.Entry.Attachment != FXAttachment.World;
        }

        internal bool Tick()
        {
            if (!_requiresFollow)
                return true;

            if (_follow == null
                || !_follow.gameObject.activeInHierarchy
                || (_target.ObjectId != 0 && !_target.MatchesLifetime))
            {
                return false;
            }

            Position = _follow.position;

            if (Request.Entry.FollowDirection)
            {
                if (_target.MatchesLifetime)
                {
                    Direction = _target.Target.FacingDirection;
                }
                else if (_follow.TryGetComponent<Projectile_Controller>(out var projectile))
                {
                    Direction = projectile.FlightDirection;
                }
            }

            return true;
        }

        internal void Detach()
        {
            // 반환 직전에는 마지막 월드 위치를 반영하고, 수명이 바뀐 대상은 읽지 않는다.
            Tick();

            _follow = null;
            _target = default;
            _requiresFollow = false;
        }
    }
}