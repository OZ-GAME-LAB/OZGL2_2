namespace Units.FX
{
    public enum VFXDirectionMode { Fixed, FlipHorizontal, RotateToDirection }
    public enum FXCatalogCategory
    {
        Unspecified = 0,
        PhysicalOnHit = 1,
        Projectile = 2,
        OnHit = 3,
        ActiveOnHit = 4,
        ActiveProjectile = 5,
        ActiveExplosion = 6,
        MeleeActivation = 7,
        MeleeHit = 8,
        DashImpact = 9,
        Support = 10
    }

    public enum FXKind { VFX, SFX }
    public enum FXAttachment { World, Owner, Target, Projectile }
    public enum FXCleanupReason { ActionEnded, ScopeEnded }
}
