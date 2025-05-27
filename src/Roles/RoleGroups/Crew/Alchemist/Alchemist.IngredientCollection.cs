using System;
using System.Collections.Generic;
using System.Linq;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.RoleGroups.Crew.Ingredients;
using Lotus.Utilities;

namespace Lotus.Roles.RoleGroups.Crew;

public partial class Alchemist
{
    private FixedUpdateLock fixedUpdateLock = new(AlchemistFixedUpdate);
    private IAlchemyIngredient? collectableIngredient;

    [NewOnSetup] private Dictionary<IngredientInfo, int> heldIngredients;

    [RoleAction(LotusActionType.FixedUpdate)]
    private void CheckForIngredient()
    {
        if (!fixedUpdateLock.AcquireLock()) return;

        GlobalIngredients.RemoveWhere(i => i.IsExpired());
        LocalIngredients.RemoveWhere(i => i.IsExpired());

        collectableIngredient =
            LocalIngredients.FirstOrDefault(i => i.IsCollectable(this))
            ?? GlobalIngredients.FirstOrDefault(i => i.IsCollectable(this));

        CheckChaosSpawn();
        CheckDiscussionSpawn();
    }

    [RoleAction(LotusActionType.OnPet)]
    private void CollectIngredient()
    {
        if (craftingMode) return;
        if (collectableIngredient == null) return;
        heldIngredients[collectableIngredient.AsInfo()] = heldIngredients.GetValueOrDefault(collectableIngredient.AsInfo(), 0) + 1;

        GlobalIngredients.Remove(collectableIngredient);
        LocalIngredients.Remove(collectableIngredient);
        collectableIngredient.Collect();
    }
}