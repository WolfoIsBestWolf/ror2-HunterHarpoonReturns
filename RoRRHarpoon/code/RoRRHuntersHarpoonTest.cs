using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using R2API;
using System;
using UnityEngine;

namespace RoRRHarpoon
{
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInPlugin("com.Wolfo.RoRRHuntersHarpoon", "HuntersHarpoonReturns", "1.0.2")]
    public class RoRRHarpoonMain : BaseUnityPlugin
    {
        public void Awake()
        {
            //Thanks HIFU for Ultimate Custom Run teaching me IL and being a great resource
            IL.RoR2.CharacterBody.RecalculateStats += ChangeMoveSpeed;
            IL.RoR2.GlobalEventManager.OnCharacterDeath += ChangeDuration;
            //Idk if there's a way to change Strings without R2API
            UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<BuffDef>(key: "RoR2/DLC1/MoveSpeedOnKill/bdKillMoveSpeed.asset").WaitForCompletion().canStack = false;
            LanguageAPI.Add("ITEM_MOVESPEEDONKILL_DESC", "Killing an enemy increases <style=cIsUtility>movement speed</style> by <style=cIsUtility>125%</style> for <style=cIsUtility>1</style> <style=cStack>(+1 per stack)</style> seconds. Consecutive kills increase buff duration for up to 25 seconds.", "en");
        }

        public static void ChangeMoveSpeed(ILContext il)
        {
            ILCursor c = new(il);

            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcR4(0.25f),
                    x => x.MatchLdarg(0),
                    x => x.MatchLdsfld("RoR2.DLC1Content/Buffs", "KillMoveSpeed")))
            {
                c.Next.Operand = 1.25f;
            }
            else
            {
                Debug.LogWarning("Failed to apply Hunter's Harpoon Move Speed Increase hook");
            }
        }

        private void ChangeDuration(ILContext il)
        {
            ILCursor c = new ILCursor(il);

            if (c.TryGotoNext(MoveType.Before,
                    x => x.MatchLdcR4(1f),
                    x => x.MatchLdloc(out _),
                    x => x.MatchConvR4(),
                    x => x.MatchLdcR4(0.5f)))
            {
                //Need to solve it getting cleared

                c.Index -= 2; //980 
                c.Next.OpCode = OpCodes.Ldc_I4_0; //Don't apply normal buff
                c.Index += 9; //990;   
                c.RemoveRange(3);  //Removes the 3 lines that Clear the buff, couldn't figure out how to null the buff
                //c.Next = null; //Clear null buffs 
                c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt("RoR2.CharacterBody", "get_corePosition"));

                c.EmitDelegate<Func<CharacterBody, CharacterBody>>((attackerBody) =>
                {
                    //Debug.Log(attackerBody);
                    int countHarpoon = attackerBody.master.inventory.GetItemCount(DLC1Content.Items.MoveSpeedOnKill);
                    float newDuration = countHarpoon * 1;
                    if (attackerBody.HasBuff(DLC1Content.Buffs.KillMoveSpeed))
                    {
                        for (int i = 0; i < attackerBody.timedBuffs.Count; i++)
                        {
                            if (attackerBody.timedBuffs[i].buffIndex == DLC1Content.Buffs.KillMoveSpeed.buffIndex)
                            {
                                newDuration += attackerBody.timedBuffs[i].timer;
                                break;
                            }
                        }
                    }
                    if (newDuration > 25)
                    {
                        newDuration = 25;
                    };
                    //Debug.Log(newDuration);
                    attackerBody.ClearTimedBuffs(DLC1Content.Buffs.KillMoveSpeed);
                    attackerBody.AddTimedBuff(DLC1Content.Buffs.KillMoveSpeed, newDuration);

                    return attackerBody;
                });
                
                //Debug.Log("Applied Hunter's Harpoon Duration hook");
            }
            else
            {
                Debug.LogWarning("Failed to apply Hunter's Harpoon Duration hook");
            }
        }

    }
}