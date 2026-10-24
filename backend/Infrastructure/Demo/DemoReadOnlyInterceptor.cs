using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;

namespace Infrastructure.Demo;

/// <summary>Defense against accidental service writes, registered only by the Demo HTTP host.</summary>
public sealed class DemoReadOnlyInterceptor(Func<bool>? canAcceptRecommendation = null) : SaveChangesInterceptor
{
    private void Check(DbContextEventData eventData)
    {
        if (canAcceptRecommendation?.Invoke() != true || eventData.Context is null) throw new InvalidOperationException("Persistência bloqueada na API HTTP DEMO.");
        foreach (var entry in eventData.Context.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            var valid = entry.Entity switch
            {
                Recommendation r => r.Id == DemoEvaluationPlan.PreventiveRecommendationId && entry.State == EntityState.Modified && entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is "Status" or "ResolvedAt"),
                Equipment e => e.Id == DemoEvaluationPlan.Id("equipment", 0) && entry.State == EntityState.Modified && entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is "MaintenanceNeed" or "NextMaintenanceDate"),
                MaintenanceRecord m => m.RecommendationId == DemoEvaluationPlan.PreventiveRecommendationId && m.EquipmentId == DemoEvaluationPlan.Id("equipment", 0) && entry.State == EntityState.Added,
                _ => false
            };
            if (!valid) throw new InvalidOperationException("Gravação fora do aceite demonstrativo bloqueada.");
        }
    }
    private InterceptionResult<int> Checked(DbContextEventData data, InterceptionResult<int> result) { Check(data); return result; }
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        Checked(eventData, result);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Checked(eventData, result));
}
