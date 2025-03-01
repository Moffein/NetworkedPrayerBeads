using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using System.Security.Permissions;
using System.Security;
using UnityEngine;
using UnityEngine.Networking;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
namespace NetworkedPrayerBeads
{
    [BepInDependency(R2API.ItemAPI.PluginGUID)]
    [BepInDependency(R2API.RecalculateStatsAPI.PluginGUID)]
    [BepInDependency(R2API.R2API.PluginGUID)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.DifferentModVersionsAreOk)]
    [BepInPlugin("com.Moffein.NetworkedPrayerBeads", "NetworkedPrayerBeads", "1.0.1")]
    public class NetworkedPrayerBeadsPlugin : BaseUnityPlugin
    {
        public static ItemDef BeadStatItem;
        private void Awake()
        {
            CreateItem();
            On.RoR2.CharacterMaster.OnBeadReset += CharacterMaster_OnBeadReset;
        }

        private void CreateItem()
        {
            BeadStatItem = ScriptableObject.CreateInstance<ItemDef>();
            BeadStatItem.name = "MoffeinBeadStatItem";
            BeadStatItem.deprecatedTier = ItemTier.NoTier;
            BeadStatItem.nameToken = "Prayer Beads Internal Stat Item";
            BeadStatItem.pickupToken = "Prayer Beads stats.";
            BeadStatItem.descriptionToken = "Prayer Beads stats.";
            BeadStatItem.tags = new[]
            {
                 ItemTag.WorldUnique,
                 ItemTag.BrotherBlacklist,
                 ItemTag.CannotSteal
            };
            BeadStatItem.hidden = true;
            ItemDisplayRule[] idr = new ItemDisplayRule[0];
            ItemAPI.Add(new CustomItem(BeadStatItem, idr));

            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        private void CharacterBody_RecalculateStats(MonoMod.Cil.ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(x => x.MatchStfld<CharacterBody>("extraStatsOnLevelUpCount_CachedLastApplied"))
                && c.TryGotoNext(x => x.MatchStfld<CharacterBody>("extraStatsOnLevelUpCount_CachedLastApplied")))
            {
                c.Emit(OpCodes.Ldarg_0);//self
                c.EmitDelegate<Func<int, CharacterBody, int>>((num, self) =>
                {
                    if (NetworkServer.active && self.inventory)
                    {
                        int diff = self.extraStatsOnLevelUpCount_CachedLastApplied - self.inventory.GetItemCount(DLC2Content.Items.ExtraStatsOnLevelUp);
                        if (diff > 0)
                        {
                            int itemsToGive = 4;    //+20% base
                            itemsToGive += diff - 1;    //+5% for extra stats
                            itemsToGive *= self.GetBuffCount(DLC2Content.Buffs.ExtraStatsOnLevelUpBuff);

                            self.inventory.GiveItem(BeadStatItem, itemsToGive);
                        }
                    }

                    return num;
                });
            }
            else
            {
                Debug.LogError("NetworkedPrayerBeads: RecalculateStats IL hook failed.");
            }
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (!sender.inventory) return;
            int count = sender.inventory.GetItemCount(BeadStatItem);
            if (count > 0)
            {
                float bonus = 0.05f * count;
                args.baseHealthAdd += bonus * sender.levelMaxHealth;
                args.baseRegenAdd += bonus * sender.levelRegen;
                args.baseDamageAdd += bonus * sender.levelDamage;
                args.baseShieldAdd += bonus * sender.levelMaxShield;
            }
        }

        private void CharacterMaster_OnBeadReset(On.RoR2.CharacterMaster.orig_OnBeadReset orig, CharacterMaster self, bool gainedStats)
        {
            orig(self, gainedStats);

            //Disable Vanilla Stats
            if (self.inventory)
            {
                self.inventory.beadAppliedDamage = 0f;
                self.inventory.beadAppliedHealth = 0f;
                self.inventory.beadAppliedRegen = 0f;
                self.inventory.beadAppliedShield = 0f;
            }
        }
    }
}
